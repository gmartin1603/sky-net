using System.Net.Http.Json;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class SimApiClient(HttpClient http)
{
	public async Task<SimulationInfoDto[]> GetSimulationsAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<SimulationInfoDto[]>("/api/sims", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/sims.");

	public async Task<string> GetActiveSimulationIdAsync(CancellationToken cancellationToken = default)
	{
		var result = await http.GetFromJsonAsync<Dictionary<string, string>>("/api/sims/active", cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("Missing response body from /api/sims/active.");
		return result.TryGetValue("id", out var id) ? id : throw new InvalidOperationException("Missing 'id' from /api/sims/active.");
	}

	public Task SelectSimulationAsync(string simId, CancellationToken cancellationToken = default) =>
		http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/select", content: null, cancellationToken);

	public async Task<LogBatchDto> GetLogsAsync(string? simId = null, long after = 0, int take = 200, CancellationToken cancellationToken = default)
	{
		var qs = $"after={after}&take={take}";
		if (!string.IsNullOrWhiteSpace(simId))
		{
			qs += $"&simId={Uri.EscapeDataString(simId)}";
		}

		return await http.GetFromJsonAsync<LogBatchDto>($"/api/logs?{qs}", cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("Missing response body from /api/logs.");
	}

	public async Task<SimStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<SimStatus>("/api/status", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/status.");

	public async Task<SimStatus> GetStatusAsync(string simId, CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<SimStatus>($"/api/sims/{Uri.EscapeDataString(simId)}/status", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException($"Missing response body from /api/sims/{simId}/status.");

	public async Task<ParameterDefinitionDto[]> GetParameterDefinitionsAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<ParameterDefinitionDto[]>("/api/parameters/definitions", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/parameters/definitions.");

	public async Task<ParameterDefinitionDto[]> GetParameterDefinitionsAsync(string simId, CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<ParameterDefinitionDto[]>($"/api/sims/{Uri.EscapeDataString(simId)}/parameters/definitions", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException($"Missing response body from /api/sims/{simId}/parameters/definitions.");

	public async Task<Dictionary<string, double>> GetParameterValuesAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<Dictionary<string, double>>("/api/parameters/values", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/parameters/values.");

	public async Task<Dictionary<string, double>> GetParameterValuesAsync(string simId, CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<Dictionary<string, double>>($"/api/sims/{Uri.EscapeDataString(simId)}/parameters/values", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException($"Missing response body from /api/sims/{simId}/parameters/values.");

	public async Task<Dictionary<string, double>> GetSignalsAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<Dictionary<string, double>>("/api/signals", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/signals.");

	public async Task<Dictionary<string, double>> GetSignalsAsync(string simId, CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<Dictionary<string, double>>($"/api/sims/{Uri.EscapeDataString(simId)}/signals", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException($"Missing response body from /api/sims/{simId}/signals.");

	public Task PauseAsync(CancellationToken cancellationToken = default) =>
		http.PostAsync("/api/pause", content: null, cancellationToken);

	public Task PauseAsync(string simId, CancellationToken cancellationToken = default) =>
		http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/pause", content: null, cancellationToken);

	public Task ResumeAsync(CancellationToken cancellationToken = default) =>
		http.PostAsync("/api/resume", content: null, cancellationToken);

	public Task ResumeAsync(string simId, CancellationToken cancellationToken = default) =>
		http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/resume", content: null, cancellationToken);

	public Task StepAsync(int steps, CancellationToken cancellationToken = default) =>
		http.PostAsync($"/api/step?n={steps}", content: null, cancellationToken);

	public Task StepAsync(string simId, int steps, CancellationToken cancellationToken = default) =>
		http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/step?n={steps}", content: null, cancellationToken);

	public Task SetParameterAsync(string name, double value, CancellationToken cancellationToken = default) =>
		http.PostAsJsonAsync($"/api/parameters/{Uri.EscapeDataString(name)}", new SetParameterRequest(value), cancellationToken);

	public Task SetParameterAsync(string simId, string name, double value, CancellationToken cancellationToken = default) =>
		http.PostAsJsonAsync($"/api/sims/{Uri.EscapeDataString(simId)}/parameters/{Uri.EscapeDataString(name)}", new SetParameterRequest(value), cancellationToken);
}
