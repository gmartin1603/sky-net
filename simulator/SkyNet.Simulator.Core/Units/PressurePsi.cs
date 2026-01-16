namespace SkyNet.Simulator.Core.Units;

public readonly record struct PressurePsi(double Value) : IUnit<PressurePsi>
{
	public static PressurePsi From(double value) => new(value);

	public override string ToString() => $"{Value:0.###} psi";
}
