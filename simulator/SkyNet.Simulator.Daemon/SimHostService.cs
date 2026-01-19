using Microsoft.AspNetCore.SignalR;
using SkyNet.Simulator.Contracts;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Daemon;

public sealed class SimHostService : BackgroundService
{
	private readonly SimulationRegistry _registry;
	private readonly IHubContext<SimHub> _hub;
	private readonly ILogger<SimHostService> _logger;

	public SimHostService(
		SimulationRegistry registry,
		IHubContext<SimHub> hub,
		ILogger<SimHostService> logger)
	{
		_registry = registry;
		_hub = hub;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var sims = _registry.List();
		var runTasks = new List<Task>(sims.Count);
		foreach (var info in sims)
		{
			if (_registry.TryGet(info.Id, out var slot))
			{
				runTasks.Add(slot.Runner.RunRealTimeAsync(stoppingToken));
				slot.Logs.Add(slot.Info.Id, "Info", "Started");
			}
		}

		// Telemetry broadcaster: publish snapshots when tick advances.
		// This keeps UI traffic bounded while still feeling "live".
		const int schemaVersion = 2;
		var lastPublished = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				foreach (var sim in sims)
				{
					if (!_registry.TryGet(sim.Id, out var slot))
					{
						continue;
					}

					var tick = slot.Runner.Time.Tick;
					lastPublished.TryGetValue(sim.Id, out var lastTick);
					if (tick == lastTick)
					{
						continue;
					}

					lastPublished[sim.Id] = tick;
					var snapshot = new TelemetrySnapshot(
						SimId: sim.Id,
						SchemaVersion: schemaVersion,
						Tick: tick,
						TimeSeconds: slot.Runner.Time.TotalSeconds,
						Parameters: slot.System.Parameters.Snapshot(),
						Signals: slot.System.Signals.Snapshot());

					// New: per-sim group
					await _hub.Clients.Group($"sim:{sim.Id}").SendAsync("snapshot", snapshot, stoppingToken).ConfigureAwait(false);

					// Back-compat: active sim only, broadcast to everyone.
					if (string.Equals(_registry.ActiveId, sim.Id, StringComparison.OrdinalIgnoreCase))
					{
						await _hub.Clients.All.SendAsync("snapshot", snapshot, stoppingToken).ConfigureAwait(false);
					}
				}

				await Task.Delay(100, stoppingToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			// normal shutdown
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "SimHostService failed.");
		}
		finally
		{
			foreach (var t in runTasks)
			{
				try { await t.ConfigureAwait(false); } catch { /* ignore */ }
			}
		}
	}
}