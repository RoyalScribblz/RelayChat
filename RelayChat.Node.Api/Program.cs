using Microsoft.EntityFrameworkCore;
using RelayChat.Node.Api;
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
    return Results.Ok(servers.Select(ServerDto.FromServer));
});
app.MapGet("/servers/{serverId:guid}/channels", async (Guid serverId, ChannelRepository repository, CancellationToken ct) =>
{
    var channels = await repository.GetByServer(serverId, ct);
    return Results.Ok(channels.Select(ChannelDto.FromChannel));
});
app.MapGet("/channels/{channelId:guid}/messages", async (
    Guid channelId,
    Guid? before,
    Guid? after,
    int? limit,
    MessageRepository repository,
    CancellationToken ct) =>
{
    var messages = await repository.GetByChannel(channelId, before, after, limit, ct);
    return Results.Ok(messages
        .OrderBy(message => message.CreatedAt)
        .ThenBy(message => message.Id)
        .Select(MessageDto.FromMessage));
});
app.MapHub<ChatHub>("/chathub");

app.Run();
