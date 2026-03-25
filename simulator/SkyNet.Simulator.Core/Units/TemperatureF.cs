namespace SkyNet.Simulator.Core.Units;

/// <summary>
/// Temperature in degrees Fahrenheit.
/// </summary>
public readonly record struct TemperatureF(double Value) : IUnit<TemperatureF>
{
	public static TemperatureF From(double value) => new(value);

	public override string ToString() => $"{Value:0.###} F";
}
