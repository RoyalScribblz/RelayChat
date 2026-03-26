using Microsoft.EntityFrameworkCore;
using RelayChat.Node.Api;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddDbContext<NodeDbContext>(options => options.UseInMemoryDatabase("RelayChatNode"));
builder.Services.AddSignalR();
builder.Services.AddScoped<MessageRepository>();

var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/openapi/v1.json", "v1");
});
app.UseCors();

app.MapGet("/", () => "OK");
app.MapGet("/channels/{channelId:guid}/messages", async (Guid channelId, MessageRepository repository, CancellationToken ct) =>
{
    var messages = await repository.GetByChannel(channelId, ct);
    return Results.Ok(messages
        .OrderBy(message => message.CreatedAt)
        .Select(MessageDto.FromMessage));
});
app.MapHub<ChatHub>("/chathub");

app.Run();
