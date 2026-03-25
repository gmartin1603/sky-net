using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Tests;

public class GrainDryerSystemTests
{
	private static (ParameterStore parameters, GrainDryerSystem system, SimulationRunner runner) CreateRig()
	{
		var parameters = new ParameterStore();
		var system = new GrainDryerSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(GrainDryerSystem.ParameterKeys.WetBinWeightLb.Name, 140000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinWeightLb.Name, 12000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinCapacityLb.Name, 180000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryerHoldUpCapacityLb.Name, 32000);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedRateCommandLbPerSec.Name, 18);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerFiringRatePercent.Name, 70);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name, 80);
		parameters.Set(GrainDryerSystem.ParameterKeys.InletMoisturePercent.Name, 20);
		parameters.Set(GrainDryerSystem.ParameterKeys.TargetOutletMoisturePercent.Name, 15);
		parameters.Set(GrainDryerSystem.ParameterKeys.AmbientAirTempF.Name, 55);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanEnable.Name, 1);

		return (parameters, system, runner);
	}

	private static IReadOnlyDictionary<string, double> RunScenario(
		double feedRateLbPerSec,
		double burnerPercent,
		double fanPercent,
		int steps = 1200)
	{
		var parameters = new ParameterStore();
		var system = new GrainDryerSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(GrainDryerSystem.ParameterKeys.WetBinWeightLb.Name, 160000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinWeightLb.Name, 10000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinCapacityLb.Name, 220000);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedRateCommandLbPerSec.Name, feedRateLbPerSec);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerFiringRatePercent.Name, burnerPercent);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name, fanPercent);
		parameters.Set(GrainDryerSystem.ParameterKeys.InletMoisturePercent.Name, 20);
		parameters.Set(GrainDryerSystem.ParameterKeys.TargetOutletMoisturePercent.Name, 15);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanEnable.Name, 1);

		for (var i = 0; i < steps; i++)
		{
			runner.StepOnce();
		}

		return system.Signals.Snapshot();
	}

	[Fact]
	public void IncreasingBurnerFiring_LowersOutletMoisture_AndRaisesPlenumTemperature()
	{
		var low = RunScenario(feedRateLbPerSec: 18, burnerPercent: 35, fanPercent: 80);
		var high = RunScenario(feedRateLbPerSec: 18, burnerPercent: 85, fanPercent: 80);

		Assert.True(high["PlenumTempF"] > low["PlenumTempF"]);
		Assert.True(high["OutletMoisturePercent"] < low["OutletMoisturePercent"]);
		Assert.True(high["MoistureRemovalRateLbPerSec"] >= low["MoistureRemovalRateLbPerSec"]);
	}

	[Fact]
	public void IncreasingFanSpeed_RaisesAirflow_AndImprovesDrying()
	{
		var low = RunScenario(feedRateLbPerSec: 18, burnerPercent: 70, fanPercent: 40);
		var high = RunScenario(feedRateLbPerSec: 18, burnerPercent: 70, fanPercent: 90);

		Assert.True(high["AirflowCfm"] > low["AirflowCfm"]);
		Assert.True(high["OutletMoisturePercent"] < low["OutletMoisturePercent"]);
		Assert.True(high["ResidenceTimeMinutes"] > 0);
	}

	[Fact]
	public void IncreasingFeedRate_ReducesResidenceTime_AndWorsensOutletMoisture()
	{
		var low = RunScenario(feedRateLbPerSec: 10, burnerPercent: 70, fanPercent: 80);
		var high = RunScenario(feedRateLbPerSec: 28, burnerPercent: 70, fanPercent: 80);

		Assert.True(high["FeedRateLbPerSec"] > low["FeedRateLbPerSec"]);
		Assert.True(high["ResidenceTimeMinutes"] < low["ResidenceTimeMinutes"]);
		Assert.True(high["OutletMoisturePercent"] > low["OutletMoisturePercent"]);
	}

	[Fact]
	public void WarmerAmbientAir_RaisesPlenumAndExhaustTemperatures_AtSameSettings()
	{
		var cold = RunScenario(feedRateLbPerSec: 18, burnerPercent: 70, fanPercent: 80, steps: 1200);
		var warmParameters = new ParameterStore();
		var warmSystem = new GrainDryerSystem(warmParameters);
		var warmRunner = new SimulationRunner(warmSystem);

		warmParameters.Set(GrainDryerSystem.ParameterKeys.WetBinWeightLb.Name, 160000);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.DryBinWeightLb.Name, 10000);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.DryBinCapacityLb.Name, 220000);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.FeedRateCommandLbPerSec.Name, 18);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.BurnerFiringRatePercent.Name, 70);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name, 80);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.InletMoisturePercent.Name, 20);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.TargetOutletMoisturePercent.Name, 15);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.AmbientAirTempF.Name, 95);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.FeedEnable.Name, 1);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.BurnerEnable.Name, 1);
		warmParameters.Set(GrainDryerSystem.ParameterKeys.FanEnable.Name, 1);

		for (var i = 0; i < 1200; i++)
		{
			warmRunner.StepOnce();
		}

		var warm = warmSystem.Signals.Snapshot();

		Assert.True(warm["PlenumTempF"] > cold["PlenumTempF"]);
		Assert.True(warm["ExhaustTempF"] > cold["ExhaustTempF"]);
	}

	[Fact]
	public void AirflowRestriction_LowersAirflow_AndRaisesLowAirflowAlarmRisk()
	{
		var parameters = new ParameterStore();
		var system = new GrainDryerSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(GrainDryerSystem.ParameterKeys.WetBinWeightLb.Name, 160000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinWeightLb.Name, 10000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinCapacityLb.Name, 220000);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedRateCommandLbPerSec.Name, 18);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerFiringRatePercent.Name, 70);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name, 80);
		parameters.Set(GrainDryerSystem.ParameterKeys.InletMoisturePercent.Name, 20);
		parameters.Set(GrainDryerSystem.ParameterKeys.TargetOutletMoisturePercent.Name, 15);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.AirflowRestrictionPercent.Name, 45);

		runner.Step(1200);

		Assert.True(system.Signals.Get("AirflowCfm") < 9000.0);
		Assert.True(system.Signals.Get("BurnerOutputPercent") < 60.0);
	}

	[Fact]
	public void FanDisable_DropsAirflow_StopsBurner_AndRaisesLowAirflowAlarmClear()
	{
		var (parameters, system, runner) = CreateRig();

		for (var i = 0; i < 420; i++)
		{
			runner.StepOnce();
		}

		var airflowBefore = system.Signals.Get("AirflowCfm");
		var burnerBefore = system.Signals.Get("BurnerRunning");

		parameters.Set(GrainDryerSystem.ParameterKeys.FanEnable.Name, 0);

		for (var i = 0; i < 360; i++)
		{
			runner.StepOnce();
		}

		Assert.True(airflowBefore > 8000);
		Assert.True(burnerBefore >= 0.999);
		Assert.True(system.Signals.Get("AirflowCfm") < 1500);
		Assert.True(system.Signals.Get("FanRunning") <= 1e-9);
		Assert.True(system.Signals.Get("BurnerRunning") <= 1e-9);
		Assert.True(system.Signals.Get("BurnerOutputPercent") < 1.0);
	}

	[Fact]
	public void Inventories_NeverGoNegative_AndStopAtDryBinLimit()
	{
		var parameters = new ParameterStore();
		var system = new GrainDryerSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(GrainDryerSystem.ParameterKeys.WetBinWeightLb.Name, 9000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinWeightLb.Name, 4800);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinCapacityLb.Name, 5000);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedRateCommandLbPerSec.Name, 20);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerFiringRatePercent.Name, 85);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name, 90);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanEnable.Name, 1);

		for (var i = 0; i < 1600; i++)
		{
			runner.StepOnce();
			Assert.True(system.Signals.Get("WetBinWeightLb") >= 0);
			Assert.True(system.Signals.Get("DryBinWeightLb") >= 0);
			Assert.True(system.Signals.Get("GrainHoldUpLb") >= 0);
		}

		Assert.True(system.Signals.Get("DryBinWeightLb") <= 5000 + 1e-6);
		Assert.True(system.Signals.Get("IsDryBinFull") >= 0.999);
	}

	[Fact]
	public void StartupAutomation_BringsDryerOnlineWithConfiguredBasics()
	{
		var parameters = new ParameterStore();
		var system = new GrainDryerSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(GrainDryerSystem.ParameterKeys.WetBinWeightLb.Name, 150000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinWeightLb.Name, 8000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinCapacityLb.Name, 180000);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanEnable.Name, 0);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerEnable.Name, 0);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedEnable.Name, 0);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name, 25);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerFiringRatePercent.Name, 10);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedRateCommandLbPerSec.Name, 4);
		parameters.Set(GrainDryerSystem.ParameterKeys.TargetOutletMoisturePercent.Name, 18);

		parameters.Set(GrainDryerSystem.ParameterKeys.StartupAutomationFanSpeedPercent.Name, 83);
		parameters.Set(GrainDryerSystem.ParameterKeys.StartupAutomationBurnerFiringRatePercent.Name, 72);
		parameters.Set(GrainDryerSystem.ParameterKeys.StartupAutomationFeedRateCommandLbPerSec.Name, 21);
		parameters.Set(GrainDryerSystem.ParameterKeys.StartupAutomationTargetOutletMoisturePercent.Name, 14.5);
		parameters.Set(GrainDryerSystem.ParameterKeys.StartupAutomationEnable.Name, 1);

		runner.StepOnce();

		Assert.True(system.Signals.Get("StartupAutomationActive") >= 0.999);
		Assert.True(system.Signals.Get("StartupAutomationComplete") <= 1e-9);
		Assert.True(parameters.Get(GrainDryerSystem.ParameterKeys.FanEnable.Name) >= 0.999);
		Assert.True(parameters.Get(GrainDryerSystem.ParameterKeys.BurnerEnable.Name) <= 1e-9);
		Assert.True(parameters.Get(GrainDryerSystem.ParameterKeys.FeedEnable.Name) <= 1e-9);
		Assert.Equal(83, parameters.Get(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name), 3);
		Assert.Equal(72, parameters.Get(GrainDryerSystem.ParameterKeys.BurnerFiringRatePercent.Name), 3);
		Assert.Equal(21, parameters.Get(GrainDryerSystem.ParameterKeys.FeedRateCommandLbPerSec.Name), 3);
		Assert.Equal(14.5, parameters.Get(GrainDryerSystem.ParameterKeys.TargetOutletMoisturePercent.Name), 3);

		runner.Step(3600);

		Assert.True(system.Signals.Get("StartupAutomationActive") <= 1e-9);
		Assert.True(system.Signals.Get("StartupAutomationComplete") >= 0.999);
		Assert.True(system.Signals.Get("FanRunning") >= 0.999);
		Assert.True(system.Signals.Get("BurnerRunning") >= 0.999);
		Assert.True(system.Signals.Get("FeedRunning") >= 0.999);
		Assert.True(system.Signals.Get("FeedRateLbPerSec") > 5.0);
		Assert.True(system.Signals.Get("AirflowCfm") > 10000.0);
		Assert.True(system.Signals.Get("PlenumTempF") > parameters.Get(GrainDryerSystem.ParameterKeys.AmbientAirTempF.Name) + 40.0);
	}

	[Fact]
	public void ShutdownAutomation_TakesDryerOfflineInSequence()
	{
		var parameters = new ParameterStore();
		var system = new GrainDryerSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(GrainDryerSystem.ParameterKeys.WetBinWeightLb.Name, 150000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinWeightLb.Name, 8000);
		parameters.Set(GrainDryerSystem.ParameterKeys.DryBinCapacityLb.Name, 180000);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedEnable.Name, 1);
		parameters.Set(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name, 88);
		parameters.Set(GrainDryerSystem.ParameterKeys.BurnerFiringRatePercent.Name, 76);
		parameters.Set(GrainDryerSystem.ParameterKeys.FeedRateCommandLbPerSec.Name, 20);
		parameters.Set(GrainDryerSystem.ParameterKeys.ShutdownAutomationFanSpeedPercent.Name, 68);

		runner.Step(900);

		Assert.True(system.Signals.Get("FeedRateLbPerSec") > 5.0);
		Assert.True(system.Signals.Get("BurnerRunning") >= 0.999);
		Assert.True(system.Signals.Get("FanRunning") >= 0.999);
		Assert.True(system.Signals.Get("PlenumTempF") > parameters.Get(GrainDryerSystem.ParameterKeys.AmbientAirTempF.Name) + 25.0);

		parameters.Set(GrainDryerSystem.ParameterKeys.ShutdownAutomationEnable.Name, 1);

		runner.StepOnce();

		Assert.True(system.Signals.Get("ShutdownAutomationActive") >= 0.999);
		Assert.True(system.Signals.Get("ShutdownAutomationComplete") <= 1e-9);
		Assert.True(parameters.Get(GrainDryerSystem.ParameterKeys.FeedEnable.Name) <= 1e-9);
		Assert.True(parameters.Get(GrainDryerSystem.ParameterKeys.BurnerEnable.Name) >= 0.999);
		Assert.True(parameters.Get(GrainDryerSystem.ParameterKeys.FanEnable.Name) >= 0.999);
		Assert.Equal(68, parameters.Get(GrainDryerSystem.ParameterKeys.FanSpeedPercent.Name), 3);

		runner.Step(7200);

		Assert.True(system.Signals.Get("ShutdownAutomationActive") <= 1e-9);
		Assert.True(system.Signals.Get("ShutdownAutomationComplete") >= 0.999);
		Assert.True(system.Signals.Get("FeedRunning") <= 1e-9);
		Assert.True(system.Signals.Get("BurnerRunning") <= 1e-9);
		Assert.True(system.Signals.Get("FanRunning") <= 1e-9);
		Assert.True(system.Signals.Get("FeedRateLbPerSec") < 0.5);
		Assert.True(system.Signals.Get("AirflowCfm") < 700.0);
		Assert.True(system.Signals.Get("PlenumTempF") <= parameters.Get(GrainDryerSystem.ParameterKeys.AmbientAirTempF.Name) + 16.0);
	}
}
