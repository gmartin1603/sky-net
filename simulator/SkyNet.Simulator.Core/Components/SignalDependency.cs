namespace SkyNet.Simulator.Core.Components;

public readonly record struct SignalDependency(string Name, Type UnitType)
{
	public override string ToString() => $"{Name} ({UnitType.Name})";
}
