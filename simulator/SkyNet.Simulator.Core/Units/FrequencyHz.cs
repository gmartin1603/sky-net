namespace SkyNet.Simulator.Core.Units;

/// <summary>
/// Frequency in hertz (Hz).
/// </summary>
public readonly record struct FrequencyHz(double Value) : IUnit<FrequencyHz>
{
	public static FrequencyHz From(double value) => new(value);

	public override string ToString() => $"{Value:0.###} Hz";
}
