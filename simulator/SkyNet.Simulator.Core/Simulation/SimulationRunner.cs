using System.Diagnostics;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Core.Simulation;

public sealed class SimulationRunner
{
	private readonly ISimSystem _system;
	private readonly FixedStepClock _clock;
	private volatile bool _paused;
	private long _lateTicks;
	private double _maxBehindSeconds;

	public SimulationRunner(ISimSystem system, double stepSeconds = 1.0 / 60.0)
	{
		_system = system;
		_clock = new FixedStepClock(stepSeconds);
	}

	public SimTime Time => _clock.Time;
	public double StepSeconds => _clock.StepSeconds;
	public bool IsPaused => _paused;
	public long LateTicks => Interlocked.Read(ref _lateTicks);
	public double MaxBehindSeconds => Volatile.Read(ref _maxBehindSeconds);

	public void Pause() => _paused = true;
	public void Resume() => _paused = false;

	public void StepOnce()
	{
		_system.Tick(_clock.Advance(), _clock.StepSeconds);
	}

	public void Step(int steps)
	{
		if (steps <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(steps), "Steps must be >= 1.");
		}

		for (var i = 0; i < steps; i++)
		{
			StepOnce();
		}
	}

	public async Task RunRealTimeAsync(CancellationToken cancellationToken)
	{
		// Best-effort pacing to wall-clock. Deterministic stepping is provided via StepOnce().
		var stopwatch = Stopwatch.StartNew();
		var nextTickAt = stopwatch.Elapsed;

		while (!cancellationToken.IsCancellationRequested)
		{
			if (_paused)
			{
				try
				{
					await Task.Delay(25, cancellationToken).ConfigureAwait(false);
					continue;
				}
				catch (OperationCanceledException)
				{
					return;
				}
			}

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
				Interlocked.Increment(ref _lateTicks);
				var behind = -delay.TotalSeconds;
				if (behind > _maxBehindSeconds)
				{
					Volatile.Write(ref _maxBehindSeconds, behind);
				}
			}
		}
	}
}
