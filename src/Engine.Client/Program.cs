using Engine.Client;
using Engine.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["Engine:ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<GraphicsSettingsClient>();
builder.Services.AddScoped<LeaderboardClient>();
builder.Services.AddScoped<SessionClient>();
builder.Services.AddScoped<RenderLoopService>();
builder.Services.AddScoped<DeveloperToolsClient>();
builder.Services.AddScoped<AccountClient>();
builder.Services.AddScoped<ClientErrorReporter>();

await builder.Build().RunAsync().ConfigureAwait(false);
