using SkyNet.Simulator.Core.Units;

namespace SkyNet.Simulator.Core.Parameters;

public readonly record struct ParameterKey<TUnit>(string Name) where TUnit : struct, IUnit<TUnit>
{
	public override string ToString() => Name;
}
