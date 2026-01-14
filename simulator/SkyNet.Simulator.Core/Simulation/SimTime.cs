namespace SkyNet.Simulator.Core.Simulation;

public sealed record SimTime(double TotalSeconds, long Tick)
{
	public static readonly SimTime Zero = new(0, 0);

	public SimTime Advance(double deltaSeconds) =>
		new(TotalSeconds + deltaSeconds, Tick + 1);
}
