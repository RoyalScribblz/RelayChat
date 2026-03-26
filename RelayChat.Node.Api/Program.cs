using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RelayChat.Node.Api;
using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("NodeDatabase")
    ?? throw new InvalidOperationException("Connection string 'NodeDatabase' was not found.");
var relayTokens = builder.Configuration.GetSection("RelayTokens").Get<NodeRelayTokensOptions>()
    ?? throw new InvalidOperationException("Missing Relay token configuration.");

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = relayTokens.Issuer,
            ValidateAudience = true,
            ValidAudience = relayTokens.NodeAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(relayTokens.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var tokenType = context.Principal?.FindFirst(NodeRelayClaimTypes.TokenType)?.Value;
                if (tokenType != NodeRelayTokenTypes.Node)
                {
                    context.Fail("Invalid token type.");
                    return Task.CompletedTask;
                }

                var hasAccess = context.Principal?.HasNodeAccess() == true;
                if (!hasAccess)
                {
                    context.Fail("Missing node access scope.");
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddDbContext<NodeDbContext>(options => options.UseNpgsql(connectionString, npgsql =>
{
    npgsql.MigrationsAssembly(typeof(NodeDbContext).Assembly.FullName);
}));
builder.Services.AddSignalR();
builder.Services.AddScoped<ChannelRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<MembershipRepository>();
builder.Services.AddScoped<NodeStateRepository>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NodeDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapOpenApi();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/openapi/v1.json", "v1");
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "OK");
app.MapGet("/node", async (NodeStateRepository repository, CancellationToken ct) =>
{
    var node = await repository.Get(ct);
    return node is null ? Results.NotFound() : Results.Ok(node.ToDto());
});
app.MapGet("/channels", async (ChannelRepository repository, CancellationToken ct) =>
{
    var channels = await repository.GetAll(ct);
    return Results.Ok(channels.Select(channel => channel.ToDto()));
});
app.MapPost("/channels", [Authorize] async (
    CreateChannelRequest request,
    ClaimsPrincipal user,
    ChannelRepository channelRepository,
    MembershipRepository membershipRepository,
    CancellationToken ct) =>
{
    if (!user.HasNodeAccess())
    {
        return Results.Forbid();
    }

    var membership = await membershipRepository.Get(user.GetRequiredUserId(), ct);
    if (membership?.Role != MembershipRole.Admin)
    {
        return Results.Forbid();
    }

    var channelName = request.Name.Trim();
    if (string.IsNullOrWhiteSpace(channelName))
    {
        return Results.BadRequest();
    }

    var channel = new Channel
    {
        Id = Guid.NewGuid(),
        Name = channelName
    };

    await channelRepository.Add(channel, ct);
    return Results.Ok(channel.ToDto());
});
app.MapGet("/memberships/me", [Authorize] async (
    ClaimsPrincipal user,
    MembershipRepository membershipRepository,
    CancellationToken ct) =>
{
    if (!user.HasNodeAccess())
    {
        return Results.Forbid();
    }

    var membership = await membershipRepository.Get(user.GetRequiredUserId(), ct);
    return membership is null ? Results.NotFound() : Results.Ok(membership.ToDto());
});
app.MapPost("/memberships", [Authorize] async (
    ClaimsPrincipal user,
    MembershipRepository membershipRepository,
    CancellationToken ct) =>
{
    if (!user.HasNodeAccess())
    {
        return Results.Forbid();
    }

    var userId = user.GetRequiredUserId();
    var existing = await membershipRepository.Get(userId, ct);
    if (existing is not null)
    {
        return Results.Ok(existing.ToDto());
    }

    var role = await membershipRepository.Any(ct) ? MembershipRole.Member : MembershipRole.Admin;
    var membership = await membershipRepository.Add(userId, role, ct);
    return Results.Ok(membership.ToDto());
});
app.MapGet("/channels/{channelId:guid}/messages", [Authorize] async (
    Guid channelId,
    Guid? before,
    Guid? after,
    int? limit,
    ClaimsPrincipal user,
    ChannelRepository channelRepository,
    MembershipRepository membershipRepository,
    MessageRepository repository,
    CancellationToken ct) =>
{
    if (!user.HasNodeAccess())
    {
        return Results.Forbid();
    }

    var membership = await membershipRepository.Get(user.GetRequiredUserId(), ct);
    if (membership is null)
    {
        return Results.Forbid();
    }

    var channel = await channelRepository.Get(channelId, ct);
    if (channel is null)
    {
        return Results.NotFound();
    }

    var messages = await repository.GetByChannel(channelId, before, after, limit, ct);
    return Results.Ok(messages
        .OrderBy(message => message.CreatedAt)
        .ThenBy(message => message.Id)
        .Select(message => message.ToDto()));
});
app.MapHub<ChatHub>("/chathub").RequireAuthorization();

app.Run();
