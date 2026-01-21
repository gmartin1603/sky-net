using Microsoft.AspNetCore.SignalR;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Daemon;

public sealed class SimHub : Hub
{
	private readonly SimulationRegistry _registry;
	private readonly ILogger<SimHub> _logger;

	public SimHub(SimulationRegistry registry, ILogger<SimHub> logger)
	{
		_registry = registry;
		_logger = logger;
	}

	public async Task JoinSim(string simId)
	{
		if (string.IsNullOrWhiteSpace(simId))
		{
			_logger.LogWarning("JoinSim called with empty simId. ConnectionId={ConnectionId}", Context.ConnectionId);
			throw new HubException("simId is required.");
		}

		simId = simId.Trim();
		if (!_registry.TryGet(simId, out var slot))
		{
			_logger.LogWarning("JoinSim called with unknown simId {SimId}. ConnectionId={ConnectionId}", simId, Context.ConnectionId);
			throw new HubException($"Unknown simulation '{simId}'.");
		}

		try
		{
			await Groups.AddToGroupAsync(Context.ConnectionId, $"sim:{simId}").ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to add connection {ConnectionId} to group for sim {SimId}.", Context.ConnectionId, simId);
			throw new HubException("Failed to join simulation group.");
		}

		// Send an immediate snapshot so the UI has something to render even if the sim is paused.
		// Keep schema version aligned with SimHostService.
		const int schemaVersion = 2;
		try
		{
			var snapshot = new TelemetrySnapshot(
				SimId: simId,
				SchemaVersion: schemaVersion,
				Tick: slot.Runner.Time.Tick,
				TimeSeconds: slot.Runner.Time.TotalSeconds,
				Parameters: slot.System.Parameters.Snapshot(),
				Signals: slot.System.Signals.Snapshot());

			await Clients.Caller.SendAsync("snapshot", snapshot).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to send initial snapshot to caller for sim {SimId}.", simId);
		}
	}

	public Task LeaveSim(string simId)
	{
		if (string.IsNullOrWhiteSpace(simId))
		{
			throw new HubException("simId is required.");
		}

		simId = simId.Trim();
		return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"sim:{simId}");
	}
}
