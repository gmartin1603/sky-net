namespace SkyNet.Simulator.Core.Units;

/// <summary>
/// Weight in pounds (lb). Used as a training-friendly proxy for granular mass.
/// </summary>
public readonly record struct WeightLb(double Value) : IUnit<WeightLb>
{
	public static WeightLb From(double value) => new(value);

	public override string ToString() => $"{Value:0.###} lb";
}
