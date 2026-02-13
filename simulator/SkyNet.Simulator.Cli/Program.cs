using SkyNet.Simulator.Cli;
using SkyNet.Simulator.Cli.Services;

var daemonBaseUrl = Environment.GetEnvironmentVariable("SKYNET_DAEMON_URL") ?? "http://localhost:5070";
if (!Uri.TryCreate(daemonBaseUrl, UriKind.Absolute, out var baseUri))
{
	Console.Error.WriteLine($"Invalid daemon URL '{daemonBaseUrl}'. Set SKYNET_DAEMON_URL to a valid absolute URI.");
	return 1;
}

using var http = new HttpClient
{
	BaseAddress = baseUri,
	Timeout = TimeSpan.FromSeconds(10),
};

var options = new DaemonClientOptions(baseUri);
var api = new SimApiClient(http);
await using var hub = new SimHubClient(options);

var app = new TuiApp(api, hub);
return await app.RunAsync().ConfigureAwait(false);
