namespace SkyNet.Simulator.Core.Simulation;

public sealed class FixedStepClock
{
	public FixedStepClock(double stepSeconds)
	{
		if (stepSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(stepSeconds), stepSeconds, "Step must be positive.");
		}

		StepSeconds = stepSeconds;
		Time = SimTime.Zero;
	}

	public double StepSeconds { get; }
	public SimTime Time { get; private set; }

	public SimTime Advance()
	{
		Time = Time.Advance(StepSeconds);
		return Time;
	}
}
