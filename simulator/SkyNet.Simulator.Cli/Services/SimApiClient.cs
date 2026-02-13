using System.Net.Http.Json;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Cli.Services;

public sealed class SimApiClient(HttpClient http)
{
	private static async Task EnsureSuccessAsync(HttpResponseMessage response)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var body = string.Empty;
		try
		{
			body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
		}
		catch
		{
			// ignore body parsing failures
		}

		throw new HttpRequestException(
			$"Request failed: {(int)response.StatusCode} {response.ReasonPhrase}{(string.IsNullOrWhiteSpace(body) ? string.Empty : $" - {body}")}");
	}

	public async Task<SimulationInfoDto[]> GetSimulationsAsync(CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<SimulationInfoDto[]>("/api/sims", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("Missing response body from /api/sims.");

	public async Task<string> GetActiveSimulationIdAsync(CancellationToken cancellationToken = default)
	{
		var result = await http.GetFromJsonAsync<Dictionary<string, string>>("/api/sims/active", cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("Missing response body from /api/sims/active.");

		return result.TryGetValue("id", out var id)
			? id
			: throw new InvalidOperationException("Missing 'id' from /api/sims/active.");
	}

	public async Task SelectSimulationAsync(string simId, CancellationToken cancellationToken = default)
	{
		var response = await http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/select", content: null, cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response).ConfigureAwait(false);
	}

	public async Task<SimStatus> GetStatusAsync(string simId, CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<SimStatus>($"/api/sims/{Uri.EscapeDataString(simId)}/status", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException($"Missing response body from /api/sims/{simId}/status.");

	public async Task<ParameterDefinitionDto[]> GetParameterDefinitionsAsync(string simId, CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<ParameterDefinitionDto[]>($"/api/sims/{Uri.EscapeDataString(simId)}/parameters/definitions", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException($"Missing response body from /api/sims/{simId}/parameters/definitions.");

	public async Task<Dictionary<string, double>> GetParameterValuesAsync(string simId, CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<Dictionary<string, double>>($"/api/sims/{Uri.EscapeDataString(simId)}/parameters/values", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException($"Missing response body from /api/sims/{simId}/parameters/values.");

	public async Task<Dictionary<string, double>> GetSignalsAsync(string simId, CancellationToken cancellationToken = default) =>
		await http.GetFromJsonAsync<Dictionary<string, double>>($"/api/sims/{Uri.EscapeDataString(simId)}/signals", cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException($"Missing response body from /api/sims/{simId}/signals.");

	public async Task PauseAsync(string simId, CancellationToken cancellationToken = default)
	{
		var response = await http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/pause", content: null, cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response).ConfigureAwait(false);
	}

	public async Task ResumeAsync(string simId, CancellationToken cancellationToken = default)
	{
		var response = await http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/resume", content: null, cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response).ConfigureAwait(false);
	}

	public async Task StepAsync(string simId, int steps, CancellationToken cancellationToken = default)
	{
		var response = await http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/step?n={steps}", content: null, cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response).ConfigureAwait(false);
	}

	public async Task StopAsync(string simId, CancellationToken cancellationToken = default)
	{
		var response = await http.PostAsync($"/api/sims/{Uri.EscapeDataString(simId)}/stop", content: null, cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response).ConfigureAwait(false);
	}

	public async Task SetParameterAsync(string simId, string name, double value, CancellationToken cancellationToken = default)
	{
		var response = await http.PostAsJsonAsync(
			$"/api/sims/{Uri.EscapeDataString(simId)}/parameters/{Uri.EscapeDataString(name)}",
			new SetParameterRequest(value),
			cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response).ConfigureAwait(false);
	}
}
