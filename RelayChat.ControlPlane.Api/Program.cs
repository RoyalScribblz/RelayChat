using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RelayChat.ControlPlane.Api;
using RelayChat.ControlPlane.Database;

var builder = WebApplication.CreateBuilder(args);

var authentik = builder.Configuration.GetSection("Authentik").Get<AuthentikOptions>()
    ?? throw new InvalidOperationException("Missing Authentik configuration.");
var relayTokens = builder.Configuration.GetSection("RelayTokens").Get<RelayTokensOptions>()
    ?? throw new InvalidOperationException("Missing Relay token configuration.");
var connectionString = builder.Configuration.GetConnectionString("ControlPlaneDatabase")
    ?? throw new InvalidOperationException("Connection string 'ControlPlaneDatabase' was not found.");
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5000"];

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddSingleton(relayTokens);
builder.Services.AddSingleton<RelayTokenService>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<UserProvisioningService>();
builder.Services.AddDbContext<ControlPlaneDbContext>(options => options.UseNpgsql(connectionString, npgsql =>
{
    npgsql.MigrationsAssembly(typeof(ControlPlaneDbContext).Assembly.FullName);
}));
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.Authority = authentik.Authority;
        options.ClientId = authentik.ClientId;
        options.ClientSecret = authentik.ClientSecret;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.RequireHttpsMetadata = false;
        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = RelayTokenService.CreateTokenValidationParameters(
            relayTokens,
            relayTokens.AccessAudience);
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var tokenType = context.Principal?.FindFirst(RelayClaimTypes.TokenType)?.Value;
                if (tokenType != RelayTokenTypes.Access)
                {
                    context.Fail("Invalid token type.");
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
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

app.MapGet("/auth/login", (string? returnUrl) =>
    Results.Challenge(
        new AuthenticationProperties
        {
            RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "http://localhost:5000/auth/complete" : returnUrl
        },
        [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapPost("/auth/logout", [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)] () =>
    Results.SignOut(
        new AuthenticationProperties
        {
            RedirectUri = "/"
        },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

app.MapGet("/auth/session", [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)] async (
    ClaimsPrincipal user,
    UserProvisioningService provisioningService,
    CancellationToken ct) =>
{
    var localUser = await provisioningService.GetOrCreateUser(user, ct);
    return Results.Ok(localUser.ToDto());
});

app.MapPost("/auth/exchange", [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)] async (
    ClaimsPrincipal user,
    UserProvisioningService provisioningService,
    RelayTokenService tokenService,
    CancellationToken ct) =>
{
    var localUser = await provisioningService.GetOrCreateUser(user, ct);
    return Results.Ok(tokenService.IssueTokens(localUser));
});

app.MapPost("/auth/refresh", async (
    RefreshTokenRequest request,
    UserRepository userRepository,
    RelayTokenService tokenService,
    CancellationToken ct) =>
{
    var principal = tokenService.ValidateRefreshToken(request.RefreshToken);
    if (principal is null)
    {
        return Results.Unauthorized();
    }

    var user = await userRepository.Get(principal.GetRequiredUserId(), ct);
    return user is null ? Results.Unauthorized() : Results.Ok(tokenService.IssueTokens(user));
});

app.MapPost("/auth/node-token",
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
        IssueNodeTokenRequest request,
        ClaimsPrincipal user,
        UserRepository userRepository,
        RelayTokenService tokenService,
        CancellationToken ct) =>
    {
        var localUser = await userRepository.Get(user.GetRequiredUserId(), ct);
        return localUser is null ? Results.Unauthorized() : Results.Ok(tokenService.IssueNodeToken(localUser));
    });

app.MapGet("/users/me",
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
        ClaimsPrincipal user,
        UserRepository userRepository,
        CancellationToken ct) =>
    {
        var localUser = await userRepository.Get(user.GetRequiredUserId(), ct);
        return localUser is null ? Results.Unauthorized() : Results.Ok(localUser.ToDto());
    });

app.MapPut("/users/me",
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
        UpdateProfileRequest request,
        ClaimsPrincipal user,
        UserRepository userRepository,
        CancellationToken ct) =>
    {
        var localUser = await userRepository.Get(user.GetRequiredUserId(), ct);
        if (localUser is null)
        {
            return Results.Unauthorized();
        }

        var name = UserProvisioningService.NormalizeName(request.Name);
        var normalizedHandle = UserProvisioningService.NormalizeHandle(request.Handle);
        var avatarUrl = UserProvisioningService.NormalizeAvatarUrl(request.AvatarUrl);

        if (string.IsNullOrWhiteSpace(name) || normalizedHandle.Length < 3)
        {
            return Results.BadRequest();
        }

        var handleExists = await userRepository.HandleExists(normalizedHandle, localUser.Id, ct);
        if (handleExists)
        {
            return Results.Conflict(new { error = "Handle is already in use." });
        }

        localUser.Name = name;
        localUser.Handle = normalizedHandle;
        localUser.HandleNormalized = normalizedHandle;
        localUser.AvatarUrl = avatarUrl;
        localUser.UpdatedAt = DateTimeOffset.UtcNow;

        await userRepository.SaveChanges(ct);
        return Results.Ok(localUser.ToDto());
    });

app.Run();
