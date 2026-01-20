namespace SkyNet.Simulator.Core.Units;

/// <summary>
/// Percentage value (0..100 typically).
/// </summary>
public readonly record struct Percent(double Value) : IUnit<Percent>
{
	public static Percent From(double value) => new(value);

	public override string ToString() => $"{Value:0.###}%";
}
