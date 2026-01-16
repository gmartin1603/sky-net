using SkyNet.Simulator.Core.Simulation;

namespace SkyNet.Simulator.Core.Components;

public interface ISimComponent
{
	string Name => GetType().Name;

	IReadOnlyCollection<SignalDependency> Reads => Array.Empty<SignalDependency>();
	IReadOnlyCollection<SignalDependency> Writes => Array.Empty<SignalDependency>();

	void Tick(SimTime time, double dtSeconds);
}


