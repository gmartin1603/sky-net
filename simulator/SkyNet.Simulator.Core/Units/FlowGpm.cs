namespace SkyNet.Simulator.Core.Units;

public readonly record struct FlowGpm(double Value) : IUnit<FlowGpm>
{
	public static FlowGpm From(double value) => new(value);

	public override string ToString() => $"{Value:0.###} gpm";
}
