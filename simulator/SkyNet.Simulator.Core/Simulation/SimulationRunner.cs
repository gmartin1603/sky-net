using System.Diagnostics;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Core.Simulation;

public sealed class SimulationRunner
{
	private readonly DemoHydraulicSystem _system;
	private readonly FixedStepClock _clock;

	public SimulationRunner(DemoHydraulicSystem system, double stepSeconds = 1.0 / 60.0)
	{
		_system = system;
		_clock = new FixedStepClock(stepSeconds);
	}

	public SimTime Time => _clock.Time;
	public double StepSeconds => _clock.StepSeconds;

	public void StepOnce()
	{
		_system.Tick(_clock.Advance(), _clock.StepSeconds);
	}

	public async Task RunRealTimeAsync(CancellationToken cancellationToken)
	{
		// Best-effort pacing to wall-clock. Deterministic stepping is provided via StepOnce().
		var stopwatch = Stopwatch.StartNew();
		var nextTickAt = stopwatch.Elapsed;

		while (!cancellationToken.IsCancellationRequested)
		{
			StepOnce();

			nextTickAt += TimeSpan.FromSeconds(_clock.StepSeconds);
			var delay = nextTickAt - stopwatch.Elapsed;
			if (delay > TimeSpan.Zero)
			{
				try
				{
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					return;
				}
			}
			else
			{
				// If we're behind, skip sleeping and keep ticking.
				// (No catch-up burst logic yet; keep phase-1 simple.)
			}
		}
	}
}
