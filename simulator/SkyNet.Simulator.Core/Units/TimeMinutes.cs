namespace SkyNet.Simulator.Core.Units;

/// <summary>
/// Time span in minutes.
/// </summary>
public readonly record struct TimeMinutes(double Value) : IUnit<TimeMinutes>
{
	public static TimeMinutes From(double value) => new(value);

	public override string ToString() => $"{Value:0.###} min";
}
