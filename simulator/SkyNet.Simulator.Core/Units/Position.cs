namespace SkyNet.Simulator.Core.Units;

public readonly record struct Position(double Value) : IUnit<Position>
{
	public static Position From(double value) => new(value);

	public override string ToString() => Value.ToString("0.###");
}
