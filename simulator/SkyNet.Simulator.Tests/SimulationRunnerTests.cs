using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Tests;

public sealed class SimulationRunnerTests
{
	[Fact]
	public void Step_AdvancesTimeByStepCount()
	{
		var parameters = new ParameterStore();
		var system = new HydraulicTrainingSystem(parameters);
		var runner = new SimulationRunner(system, stepSeconds: 0.01);

		runner.Step(10);

		Assert.Equal(10, runner.Time.Tick);
		Assert.Equal(0.10, runner.Time.TotalSeconds, precision: 12);
	}
}
