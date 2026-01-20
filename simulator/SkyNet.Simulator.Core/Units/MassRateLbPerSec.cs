namespace SkyNet.Simulator.Core.Units;

/// <summary>
/// Mass/weight transfer rate in pounds per second (lb/s).
/// </summary>
public readonly record struct MassRateLbPerSec(double Value) : IUnit<MassRateLbPerSec>
{
	public static MassRateLbPerSec From(double value) => new(value);

	public override string ToString() => $"{Value:0.###} lb/s";
}
