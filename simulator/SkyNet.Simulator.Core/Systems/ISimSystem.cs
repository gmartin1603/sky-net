using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Signals;
using SkyNet.Simulator.Core.Simulation;

namespace SkyNet.Simulator.Core.Systems;

public interface ISimSystem
{
	ParameterStore Parameters { get; }
	SignalBus Signals { get; }

	void Tick(SimTime time, double dtSeconds);
}
