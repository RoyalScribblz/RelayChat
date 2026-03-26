using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RelayChat.Client.Services;
using RelayChat.Client;
using RelayChat.Node.Contracts;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var nodeApiBaseUrl = builder.Configuration["NodeApi:BaseUrl"]
    ?? throw new InvalidOperationException("Configuration value 'NodeApi:BaseUrl' was not found.");
var controlPlaneApiBaseUrl = builder.Configuration["ControlPlaneApi:BaseUrl"]
    ?? throw new InvalidOperationException("Configuration value 'ControlPlaneApi:BaseUrl' was not found.");

builder.Services.AddMudServices();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton(new NodeApiOptions(nodeApiBaseUrl));
builder.Services.AddSingleton(new ControlPlaneApiOptions(controlPlaneApiBaseUrl));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ControlPlaneApiClient>();
builder.Services.AddScoped<NodeApiClient>();
builder.Services.AddScoped<ChatClient>();
builder.Services.AddScoped<VoiceClient>();

await builder.Build().RunAsync();
