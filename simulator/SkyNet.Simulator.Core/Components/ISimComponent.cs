using SkyNet.Simulator.Core.Simulation;

namespace SkyNet.Simulator.Core.Components;

public interface ISimComponent
{
	void Tick(SimTime time, double dtSeconds);
}
