using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Tests;

public sealed class HydraulicTrainingSystemGoldenSnapshotTests
{
	[Fact]
	public void GoldenSnapshot_DefaultParams_After300Steps()
	{
		var parameters = new ParameterStore();
		var system = new HydraulicTrainingSystem(parameters);
		var runner = new SimulationRunner(system); // 60Hz default

		runner.Step(300);

		Assert.Equal(300, runner.Time.Tick);
		Assert.Equal(5.0, runner.Time.TotalSeconds, precision: 12);

		var signals = system.Signals.Snapshot();

		// These values are intentionally treated as a "golden" baseline.
		// If behavior changes, update the expected numbers intentionally.
		Assert.Equal(178.885438, signals["DownstreamPressurePsi"], precision: 6);
		Assert.Equal(178.885438, signals["DownstreamPressureSensorPsi"], precision: 6);
		Assert.Equal(8.0, signals["ValveFlowGpm"], precision: 6);
		Assert.Equal(-1.525101144, signals["ActuatorVelocity"], precision: 6);
		Assert.Equal(-5.882973945, signals["ActuatorPosition"], precision: 6);
	}
}
