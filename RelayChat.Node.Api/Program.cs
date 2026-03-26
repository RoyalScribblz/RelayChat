using Microsoft.EntityFrameworkCore;
using RelayChat.Node.Api;
using RelayChat.Node.Contracts;
using RelayChat.Node.Database;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("NodeDatabase")
    ?? throw new InvalidOperationException("Connection string 'NodeDatabase' was not found.");

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
builder.Services.AddDbContext<NodeDbContext>(options => options.UseNpgsql(connectionString, npgsql =>
{
    npgsql.MigrationsAssembly(typeof(NodeDbContext).Assembly.FullName);
}));
builder.Services.AddSignalR();
builder.Services.AddScoped<ChannelRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<ServerMembershipRepository>();
builder.Services.AddScoped<ServerRepository>();

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

app.MapGet("/", () => "OK");
app.MapGet("/servers", async (ServerRepository repository, CancellationToken ct) =>
{
    var servers = await repository.GetAll(ct);
    return Results.Ok(servers.Select(server => server.ToDto()));
});
app.MapPost("/servers", async (
    CreateServerRequest request,
    ServerRepository serverRepository,
    ChannelRepository channelRepository,
    ServerMembershipRepository membershipRepository,
    CancellationToken ct) =>
{
    var server = new Server
    {
        Id = Guid.NewGuid(),
        Name = request.Name.Trim()
    };

    if (string.IsNullOrWhiteSpace(server.Name))
    {
        return Results.BadRequest();
    }

    var channel = new Channel
    {
        Id = Guid.NewGuid(),
        ServerId = server.Id,
        Name = "general"
    };

    await serverRepository.Add(server, ct);
    await channelRepository.Add(channel, ct);
    var membership = await membershipRepository.Add(server.Id, request.CreatorId, ServerMembershipRole.Admin, ct);

    return Results.Ok(new CreateServerResultDto(
        server.ToDto(),
        channel.ToDto(),
        membership.ToDto()));
});
app.MapGet("/servers/{serverId:guid}/channels", async (Guid serverId, ChannelRepository repository, CancellationToken ct) =>
{
    var channels = await repository.GetByServer(serverId, ct);
    return Results.Ok(channels.Select(channel => channel.ToDto()));
});
app.MapPost("/servers/{serverId:guid}/channels", async (
    Guid serverId,
    CreateChannelRequest request,
    ServerRepository serverRepository,
    ChannelRepository channelRepository,
    ServerMembershipRepository membershipRepository,
    CancellationToken ct) =>
{
    var server = await serverRepository.Get(serverId, ct);
    if (server is null)
    {
        return Results.NotFound();
    }

    var membership = await membershipRepository.Get(serverId, request.UserId, ct);
    if (membership?.Role != ServerMembershipRole.Admin)
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
        ServerId = serverId,
        Name = channelName
    };

    await channelRepository.Add(channel, ct);
    return Results.Ok(channel.ToDto());
});
app.MapGet("/servers/{serverId:guid}/memberships/{userId:guid}", async (
    Guid serverId,
    Guid userId,
    ServerRepository serverRepository,
    ServerMembershipRepository membershipRepository,
    CancellationToken ct) =>
{
    var server = await serverRepository.Get(serverId, ct);
    if (server is null)
    {
        return Results.NotFound();
    }

    var membership = await membershipRepository.Get(serverId, userId, ct);
    if (membership is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(membership.ToDto());
});
app.MapPost("/servers/{serverId:guid}/memberships", async (
    Guid serverId,
    JoinServerRequest request,
    ServerRepository serverRepository,
    ServerMembershipRepository membershipRepository,
    CancellationToken ct) =>
{
    var server = await serverRepository.Get(serverId, ct);
    if (server is null)
    {
        return Results.NotFound();
    }

    var existing = await membershipRepository.Get(serverId, request.UserId, ct);
    if (existing is not null)
    {
        return Results.Ok(existing.ToDto());
    }

    var membership = await membershipRepository.Add(serverId, request.UserId, ServerMembershipRole.Member, ct);
    return Results.Ok(membership.ToDto());
});
app.MapGet("/servers/{serverId:guid}/channels/{channelId:guid}/messages", async (
    Guid serverId,
    Guid channelId,
    Guid? before,
    Guid? after,
    int? limit,
    ChannelRepository channelRepository,
    MessageRepository repository,
    CancellationToken ct) =>
{
    var channel = await channelRepository.Get(serverId, channelId, ct);
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
app.MapHub<ChatHub>("/chathub");

app.Run();
