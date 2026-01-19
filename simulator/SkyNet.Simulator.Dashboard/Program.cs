using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SkyNet.Simulator.Dashboard;
using SkyNet.Simulator.Dashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var daemonBaseUrl = builder.Configuration["DaemonBaseUrl"] ?? "http://localhost:5070";

builder.Services.AddSingleton(new DaemonClientOptions(daemonBaseUrl));
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(daemonBaseUrl, UriKind.Absolute) });
builder.Services.AddScoped<SimApiClient>();
builder.Services.AddScoped<SimHubClient>();
builder.Services.AddScoped<SimViewCache>();

await builder.Build().RunAsync();
