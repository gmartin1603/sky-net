using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SkyNet.Simulator.Dashboard;
using SkyNet.Simulator.Dashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configured = builder.Configuration["DaemonBaseUrl"];
Uri daemonBaseUri;
if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out var abs))
{
	daemonBaseUri = abs;
}
else
{
	// Production default: same-origin (e.g., nginx reverse proxy in Docker Compose)
	daemonBaseUri = new Uri(builder.HostEnvironment.BaseAddress, UriKind.Absolute);
}

builder.Services.AddSingleton(new DaemonClientOptions(daemonBaseUri));
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = daemonBaseUri });
builder.Services.AddScoped<SimApiClient>();
builder.Services.AddScoped<SimHubClient>();
builder.Services.AddScoped<SimViewCache>();

await builder.Build().RunAsync();
