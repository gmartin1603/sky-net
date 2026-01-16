using SkyNet.Simulator.Core.Components;
using SkyNet.Simulator.Core.Simulation;

namespace SkyNet.Simulator.Core.Systems;

public sealed class SimSystem
{
	private readonly IReadOnlyList<ISimComponent> _orderedComponents;

	internal SimSystem(IReadOnlyList<ISimComponent> orderedComponents)
	{
		_orderedComponents = orderedComponents;
	}

	public IReadOnlyList<ISimComponent> Components => _orderedComponents;

	public void Tick(SimTime time, double dtSeconds)
	{
		foreach (var component in _orderedComponents)
		{
			component.Tick(time, dtSeconds);
		}
	}
}
