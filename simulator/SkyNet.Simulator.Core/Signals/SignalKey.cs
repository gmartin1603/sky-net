using SkyNet.Simulator.Core.Units;

namespace SkyNet.Simulator.Core.Signals;

public readonly record struct SignalKey<TUnit>(string Name) where TUnit : struct, IUnit<TUnit>
{
	public override string ToString() => Name;
}
