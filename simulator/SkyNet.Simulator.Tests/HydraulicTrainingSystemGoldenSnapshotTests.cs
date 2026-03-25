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

	[Fact]
	public void PositionPid_DrivesActuatorTowardTargetAndAdjustsValveOpening()
	{
		var parameters = new ParameterStore();
		var system = new HydraulicTrainingSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(HydraulicTrainingSystem.ParameterKeys.SupplyPressurePsi.Name, 2200);
		parameters.Set(HydraulicTrainingSystem.ParameterKeys.LoadForce.Name, 700);
		parameters.Set(HydraulicTrainingSystem.ParameterKeys.ValveOpening.Name, 0.05);
		parameters.Set(HydraulicTrainingSystem.ParameterKeys.ActuatorPositionCommand.Name, 3.0);
		parameters.Set(HydraulicTrainingSystem.ParameterKeys.PositionControlEnable.Name, 1.0);

		runner.Step(720);

		var signals = system.Signals.Snapshot();
		var finalPosition = signals["ActuatorPosition"];
		var error = signals["ActuatorPositionError"];
		var valveCommand = signals["ValveOpeningCommand"];

		Assert.True(signals["PositionControlActive"] >= 0.999);
		Assert.True(finalPosition > 1.5);
		Assert.True(Math.Abs(error) < 1.6);
		Assert.True(valveCommand > 0.0);
		Assert.True(Math.Abs(parameters.Get(HydraulicTrainingSystem.ParameterKeys.ValveOpening.Name) - valveCommand) < 1e-9);
	}

	[Fact]
	public void PositionPid_Disabled_LeavesManualValveOpeningAlone()
	{
		var parameters = new ParameterStore();
		var system = new HydraulicTrainingSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(HydraulicTrainingSystem.ParameterKeys.ValveOpening.Name, 0.37);
		parameters.Set(HydraulicTrainingSystem.ParameterKeys.ActuatorPositionCommand.Name, 4.0);
		parameters.Set(HydraulicTrainingSystem.ParameterKeys.PositionControlEnable.Name, 0.0);

		runner.Step(60);

		var signals = system.Signals.Snapshot();
		Assert.True(signals["PositionControlActive"] <= 1e-9);
		Assert.Equal(0.37, parameters.Get(HydraulicTrainingSystem.ParameterKeys.ValveOpening.Name), 6);
		Assert.Equal(0.37, signals["ValveOpeningCommand"], 6);
	}
}
