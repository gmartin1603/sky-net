namespace SkyNet.Simulator.Core.Units;

/// <summary>
/// Unitless ratio, typically clamped to 0..1.
/// </summary>
public readonly record struct Ratio(double Value) : IUnit<Ratio>
{
	public static Ratio From(double value) => new(value);

	public override string ToString() => Value.ToString("0.###");
}
