using System.Net.Http.Json;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class SimApiClient(HttpClient http)
{
	public async Task<SimStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<SimStatus>("/api/status", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/status.");

	public async Task<ParameterDefinitionDto[]> GetParameterDefinitionsAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<ParameterDefinitionDto[]>("/api/parameters/definitions", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/parameters/definitions.");

	public async Task<Dictionary<string, double>> GetParameterValuesAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<Dictionary<string, double>>("/api/parameters/values", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/parameters/values.");

	public async Task<Dictionary<string, double>> GetSignalsAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<Dictionary<string, double>>("/api/signals", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/signals.");

	public Task PauseAsync(CancellationToken cancellationToken = default) =>
		http.PostAsync("/api/pause", content: null, cancellationToken);

	public Task ResumeAsync(CancellationToken cancellationToken = default) =>
		http.PostAsync("/api/resume", content: null, cancellationToken);

	public Task StepAsync(int steps, CancellationToken cancellationToken = default) =>
		http.PostAsync($"/api/step?n={steps}", content: null, cancellationToken);

	public Task SetParameterAsync(string name, double value, CancellationToken cancellationToken = default) =>
		http.PostAsJsonAsync($"/api/parameters/{Uri.EscapeDataString(name)}", new SetParameterRequest(value), cancellationToken);
}
