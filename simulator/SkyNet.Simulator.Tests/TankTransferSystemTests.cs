using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Tests;

public class TankTransferSystemTests
{
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
}
