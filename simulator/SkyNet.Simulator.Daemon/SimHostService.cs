using Microsoft.AspNetCore.SignalR;
using SkyNet.Simulator.Contracts;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Daemon;

public sealed class SimHostService : BackgroundService
{
	private readonly ISimSystem _system;
	private readonly SimulationRunner _runner;
	private readonly IHubContext<SimHub> _hub;
	private readonly ILogger<SimHostService> _logger;

	public SimHostService(
		ISimSystem system,
		SimulationRunner runner,
		IHubContext<SimHub> hub,
		ILogger<SimHostService> logger)
	{
		_system = system;
		_runner = runner;
		_hub = hub;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Runner loop (ticks at 60Hz unless paused).
		var runTask = _runner.RunRealTimeAsync(stoppingToken);

		// Telemetry broadcaster: publish snapshots when tick advances.
		// This keeps UI traffic bounded while still feeling "live".
		const int schemaVersion = 1;
		var lastPublishedTick = -1L;

		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				var tick = _runner.Time.Tick;
				if (tick != lastPublishedTick)
				{
					lastPublishedTick = tick;
					var snapshot = new TelemetrySnapshot(
						SchemaVersion: schemaVersion,
						Tick: tick,
						TimeSeconds: _runner.Time.TotalSeconds,
						Parameters: _system.Parameters.Snapshot(),
						Signals: _system.Signals.Snapshot());

					await _hub.Clients.All.SendAsync("snapshot", snapshot, stoppingToken).ConfigureAwait(false);
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
			try { await runTask.ConfigureAwait(false); } catch { /* ignore */ }
		}
	}
}