namespace SkyNet.Simulator.Core.Units;

public readonly record struct Velocity(double Value) : IUnit<Velocity>
{
	public static Velocity From(double value) => new(value);

	public override string ToString() => Value.ToString("0.###");
}
