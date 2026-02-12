using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Tests;

public class TankTransferSystemTests
{
	private static (ParameterStore parameters, TankTransferSystem system, SimulationRunner runner) CreateRig()
	{
		var parameters = new ParameterStore();
		var system = new TankTransferSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(TankTransferSystem.ParameterKeys.SourceTankWeightLb.Name, 50000);
		parameters.Set(TankTransferSystem.ParameterKeys.DestinationTankWeightLb.Name, 0);
		parameters.Set(TankTransferSystem.ParameterKeys.DestinationTankCapacityLb.Name, 500000);
		parameters.Set(TankTransferSystem.ParameterKeys.AirlockSpeedCommandHz.Name, 15);
		parameters.Set(TankTransferSystem.ParameterKeys.BlowlinePressureCommandPsi.Name, 15);
		parameters.Set(TankTransferSystem.ParameterKeys.BlowerEnable.Name, 1);
		parameters.Set(TankTransferSystem.ParameterKeys.AirlockEnable.Name, 1);

		return (parameters, system, runner);
	}

	private static (double srcLb, double dstLb, double rateLbPerSec, double blowerPercent, double pPsi) Run(
		double airlockHz,
		double blowlinePsi,
		int steps = 600)
	{
		var parameters = new ParameterStore();
		var system = new TankTransferSystem(parameters);
		var runner = new SimulationRunner(system);

		// Ensure plenty of material/space so we measure flow, not clamping.
		parameters.Set(TankTransferSystem.ParameterKeys.SourceTankWeightLb.Name, 50000);
		parameters.Set(TankTransferSystem.ParameterKeys.DestinationTankWeightLb.Name, 0);
		parameters.Set(TankTransferSystem.ParameterKeys.DestinationTankCapacityLb.Name, 500000);

		parameters.Set(TankTransferSystem.ParameterKeys.AirlockSpeedCommandHz.Name, airlockHz);
		parameters.Set(TankTransferSystem.ParameterKeys.BlowlinePressureCommandPsi.Name, blowlinePsi);
		parameters.Set(TankTransferSystem.ParameterKeys.BlowerEnable.Name, 1);
		parameters.Set(TankTransferSystem.ParameterKeys.AirlockEnable.Name, 1);

		for (var i = 0; i < steps; i++)
		{
			runner.StepOnce();
		}

		var s = system.Signals.Snapshot();
		return (
			s["SourceTankWeightLb"],
			s["DestinationTankWeightLb"],
			s["TransferRateLbPerSec"],
			s["BlowerMotorPercentFla"],
			s["BlowlinePressurePsi"]);
	}

	[Fact]
	public void IncreasingAirlockSpeed_IncreasesTransferRate_AndBlowerLoad()
	{
		var low = Run(airlockHz: 5, blowlinePsi: 10);
		var high = Run(airlockHz: 15, blowlinePsi: 10);

		Assert.True(high.rateLbPerSec > low.rateLbPerSec);
		Assert.True(high.blowerPercent > low.blowerPercent);

		// More transfer should move more inventory.
		Assert.True(high.dstLb > low.dstLb);
		Assert.True(high.srcLb < low.srcLb);
	}

	[Fact]
	public void IncreasingBlowlinePressure_IncreasesTransferRate_AndBlowerLoad()
	{
		var low = Run(airlockHz: 10, blowlinePsi: 4);
		var high = Run(airlockHz: 10, blowlinePsi: 14);

		Assert.True(high.rateLbPerSec > low.rateLbPerSec);
		Assert.True(high.blowerPercent > low.blowerPercent);

		// Achieved pressure should generally increase with command (under same load).
		Assert.True(high.pPsi > low.pPsi);
	}

	[Fact]
	public void Weights_NeverGoNegative_AndStopAtLimits()
	{
		var parameters = new ParameterStore();
		var system = new TankTransferSystem(parameters);
		var runner = new SimulationRunner(system);

		parameters.Set(TankTransferSystem.ParameterKeys.SourceTankWeightLb.Name, 50);
		parameters.Set(TankTransferSystem.ParameterKeys.DestinationTankWeightLb.Name, 0);
		parameters.Set(TankTransferSystem.ParameterKeys.DestinationTankCapacityLb.Name, 20);
		parameters.Set(TankTransferSystem.ParameterKeys.AirlockSpeedCommandHz.Name, 20);
		parameters.Set(TankTransferSystem.ParameterKeys.BlowlinePressureCommandPsi.Name, 15);
		parameters.Set(TankTransferSystem.ParameterKeys.BlowerEnable.Name, 1);
		parameters.Set(TankTransferSystem.ParameterKeys.AirlockEnable.Name, 1);

		for (var i = 0; i < 600; i++)
		{
			runner.StepOnce();
			var src = system.Signals.Get("SourceTankWeightLb");
			var dst = system.Signals.Get("DestinationTankWeightLb");

			Assert.True(src >= 0);
			Assert.True(dst >= 0);
		}

		var finalSrc = system.Signals.Get("SourceTankWeightLb");
		var finalDst = system.Signals.Get("DestinationTankWeightLb");
		var isFull = system.Signals.Get("IsFull");

		Assert.True(finalDst <= 20 + 1e-6);
		Assert.True(finalSrc >= 30 - 1e-6); // with dest capped at 20, at least 30 lb must remain
		Assert.True(isFull >= 0.999);
	}

	[Fact]
	public void BlowerDisable_DecaysPressureToZero_AndStopsTransfer()
	{
		var (parameters, system, runner) = CreateRig();

		for (var i = 0; i < 360; i++)
		{
			runner.StepOnce();
		}

		var pressureBeforeDisable = system.Signals.Get("BlowlinePressurePsi");
		var destinationBeforeDisable = system.Signals.Get("DestinationTankWeightLb");
		var blowerRunningBeforeDisable = system.Signals.Get("BlowerRunning");

		parameters.Set(TankTransferSystem.ParameterKeys.BlowerEnable.Name, 0);

		for (var i = 0; i < 240; i++)
		{
			runner.StepOnce();
		}

		var pressureAfterDisable = system.Signals.Get("BlowlinePressurePsi");
		var blowerLoadAfterDisable = system.Signals.Get("BlowerMotorPercentFla");
		var transferRateAfterDisable = system.Signals.Get("TransferRateLbPerSec");
		var destinationAfterDisable = system.Signals.Get("DestinationTankWeightLb");
		var blowerRunningAfterDisable = system.Signals.Get("BlowerRunning");

		Assert.True(blowerRunningBeforeDisable >= 0.999);
		Assert.True(blowerRunningAfterDisable <= 1e-9);
		Assert.True(pressureBeforeDisable > 1.0);
		Assert.True(pressureAfterDisable < 0.5);
		Assert.True(blowerLoadAfterDisable <= 1e-9);
		Assert.True(transferRateAfterDisable <= 1e-9);
		Assert.True(Math.Abs(destinationAfterDisable - destinationBeforeDisable) <= 1e-6);
	}

	[Fact]
	public void AirlockDisable_DecaysSpeedToZero_StopsTransfer_AndKeepsLowPressureFloor()
	{
		var (parameters, system, runner) = CreateRig();

		for (var i = 0; i < 360; i++)
		{
			runner.StepOnce();
		}

		var speedBeforeDisable = system.Signals.Get("AirlockSpeedHz");
		var pressureBeforeDisable = system.Signals.Get("BlowlinePressurePsi");
		var destinationBeforeDisable = system.Signals.Get("DestinationTankWeightLb");
		var airlockRunningBeforeDisable = system.Signals.Get("AirlockRunning");

		parameters.Set(TankTransferSystem.ParameterKeys.AirlockEnable.Name, 0);

		for (var i = 0; i < 240; i++)
		{
			runner.StepOnce();
		}

		var speedAfterDisable = system.Signals.Get("AirlockSpeedHz");
		var pressureAfterDisable = system.Signals.Get("BlowlinePressurePsi");
		var transferRateAfterDisable = system.Signals.Get("TransferRateLbPerSec");
		var destinationAfterDisable = system.Signals.Get("DestinationTankWeightLb");
		var airlockRunningAfterDisable = system.Signals.Get("AirlockRunning");

		Assert.True(airlockRunningBeforeDisable >= 0.999);
		Assert.True(airlockRunningAfterDisable <= 1e-9);
		Assert.True(speedBeforeDisable > 1.0);
		Assert.True(speedAfterDisable < 0.2);
		Assert.True(transferRateAfterDisable <= 1e-9);
		Assert.True(Math.Abs(destinationAfterDisable - destinationBeforeDisable) <= 1e-6);
		Assert.True(pressureBeforeDisable > 1.0);
		Assert.True(pressureAfterDisable > 0.2);
		Assert.True(pressureAfterDisable < 1.5);
	}
}
