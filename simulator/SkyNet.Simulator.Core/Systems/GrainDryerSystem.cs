using SkyNet.Simulator.Core.Components;
using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Signals;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Units;

namespace SkyNet.Simulator.Core.Systems;

/// <summary>
/// Training-grade continuous-flow grain dryer.
/// The model intentionally favors operator-observable relationships over strict physical accuracy.
/// </summary>
public sealed class GrainDryerSystem : ISimSystem
{
	private readonly SimSystem _system;

	public static class ParameterKeys
	{
		public static readonly ParameterKey<WeightLb> WetBinWeightLb = new("WetBinWeightLb");
		public static readonly ParameterKey<WeightLb> DryBinWeightLb = new("DryBinWeightLb");
		public static readonly ParameterKey<WeightLb> DryBinCapacityLb = new("DryBinCapacityLb");
		public static readonly ParameterKey<WeightLb> DryerHoldUpCapacityLb = new("DryerHoldUpCapacityLb");

		public static readonly ParameterKey<Ratio> StartupAutomationEnable = new("StartupAutomationEnable");
		public static readonly ParameterKey<Percent> StartupAutomationFanSpeedPercent = new("StartupAutomationFanSpeedPercent");
		public static readonly ParameterKey<Percent> StartupAutomationBurnerFiringRatePercent = new("StartupAutomationBurnerFiringRatePercent");
		public static readonly ParameterKey<MassRateLbPerSec> StartupAutomationFeedRateCommandLbPerSec = new("StartupAutomationFeedRateCommandLbPerSec");
		public static readonly ParameterKey<Percent> StartupAutomationTargetOutletMoisturePercent = new("StartupAutomationTargetOutletMoisturePercent");
		public static readonly ParameterKey<Ratio> ShutdownAutomationEnable = new("ShutdownAutomationEnable");
		public static readonly ParameterKey<Percent> ShutdownAutomationFanSpeedPercent = new("ShutdownAutomationFanSpeedPercent");

		public static readonly ParameterKey<Ratio> FeedEnable = new("FeedEnable");
		public static readonly ParameterKey<Ratio> BurnerEnable = new("BurnerEnable");
		public static readonly ParameterKey<Ratio> FanEnable = new("FanEnable");

		public static readonly ParameterKey<MassRateLbPerSec> FeedRateCommandLbPerSec = new("FeedRateCommandLbPerSec");
		public static readonly ParameterKey<Percent> BurnerFiringRatePercent = new("BurnerFiringRatePercent");
		public static readonly ParameterKey<Percent> FanSpeedPercent = new("FanSpeedPercent");

		public static readonly ParameterKey<Percent> InletMoisturePercent = new("InletMoisturePercent");
		public static readonly ParameterKey<Percent> TargetOutletMoisturePercent = new("TargetOutletMoisturePercent");
		public static readonly ParameterKey<TemperatureF> AmbientAirTempF = new("AmbientAirTempF");
		public static readonly ParameterKey<TemperatureF> HighTempAlarmThresholdF = new("HighTempAlarmThresholdF");
	}

	public static class SignalKeys
	{
		public static readonly SignalKey<WeightLb> WetBinWeightLb = new("WetBinWeightLb");
		public static readonly SignalKey<WeightLb> DryBinWeightLb = new("DryBinWeightLb");
		public static readonly SignalKey<WeightLb> GrainHoldUpLb = new("GrainHoldUpLb");

		public static readonly SignalKey<MassRateLbPerSec> FeedRateCommandLbPerSec = new("FeedRateCommandLbPerSec");
		public static readonly SignalKey<Percent> BurnerFiringRatePercent = new("BurnerFiringRatePercent");
		public static readonly SignalKey<Percent> FanSpeedPercent = new("FanSpeedPercent");

		public static readonly SignalKey<MassRateLbPerSec> FeedRateLbPerSec = new("FeedRateLbPerSec");
		public static readonly SignalKey<MassRateLbPerSec> DischargeRateLbPerSec = new("DischargeRateLbPerSec");
		public static readonly SignalKey<TimeMinutes> ResidenceTimeMinutes = new("ResidenceTimeMinutes");

		public static readonly SignalKey<AirflowCfm> AirflowCfm = new("AirflowCfm");
		public static readonly SignalKey<TemperatureF> PlenumTempF = new("PlenumTempF");
		public static readonly SignalKey<TemperatureF> ExhaustTempF = new("ExhaustTempF");
		public static readonly SignalKey<Percent> BurnerOutputPercent = new("BurnerOutputPercent");

		public static readonly SignalKey<Percent> InletMoisturePercent = new("InletMoisturePercent");
		public static readonly SignalKey<Percent> OutletMoisturePercent = new("OutletMoisturePercent");
		public static readonly SignalKey<Percent> MoistureControlErrorPercent = new("MoistureControlErrorPercent");
		public static readonly SignalKey<MassRateLbPerSec> MoistureRemovalRateLbPerSec = new("MoistureRemovalRateLbPerSec");

		public static readonly SignalKey<Ratio> FeedRunning = new("FeedRunning");
		public static readonly SignalKey<Ratio> BurnerRunning = new("BurnerRunning");
		public static readonly SignalKey<Ratio> FanRunning = new("FanRunning");
		public static readonly SignalKey<Ratio> StartupAutomationActive = new("StartupAutomationActive");
		public static readonly SignalKey<Ratio> StartupAutomationComplete = new("StartupAutomationComplete");
		public static readonly SignalKey<Ratio> ShutdownAutomationActive = new("ShutdownAutomationActive");
		public static readonly SignalKey<Ratio> ShutdownAutomationComplete = new("ShutdownAutomationComplete");

		public static readonly SignalKey<Ratio> IsWetBinEmpty = new("IsWetBinEmpty");
		public static readonly SignalKey<Ratio> IsDryBinFull = new("IsDryBinFull");
		public static readonly SignalKey<Ratio> IsHighTempAlarm = new("IsHighTempAlarm");
		public static readonly SignalKey<Ratio> IsAirflowLowAlarm = new("IsAirflowLowAlarm");
	}

	public GrainDryerSystem(ParameterStore parameters)
	{
		Parameters = parameters;
		Signals = new SignalBus();

		Parameters.Define(ParameterKeys.WetBinWeightLb, WeightLb.From(120000), minValue: WeightLb.From(0), description: "Wet grain available ahead of the dryer.");
		Parameters.Define(ParameterKeys.DryBinWeightLb, WeightLb.From(18000), minValue: WeightLb.From(0), description: "Dried grain inventory in the receiving bin.");
		Parameters.Define(ParameterKeys.DryBinCapacityLb, WeightLb.From(160000), minValue: WeightLb.From(1000), description: "Dry bin capacity used for overflow protection.");
		Parameters.Define(ParameterKeys.DryerHoldUpCapacityLb, WeightLb.From(32000), minValue: WeightLb.From(1000), description: "Working grain hold-up inside the drying column.");
		Parameters.Define(ParameterKeys.StartupAutomationEnable, Ratio.From(0), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Runs the basic startup sequence that brings the dryer online.");
		Parameters.Define(ParameterKeys.StartupAutomationFanSpeedPercent, Percent.From(80), minValue: Percent.From(25), maxValue: Percent.From(100), description: "Fan speed used by the startup automation.");
		Parameters.Define(ParameterKeys.StartupAutomationBurnerFiringRatePercent, Percent.From(70), minValue: Percent.From(0), maxValue: Percent.From(100), description: "Burner firing rate used by the startup automation.");
		Parameters.Define(ParameterKeys.StartupAutomationFeedRateCommandLbPerSec, MassRateLbPerSec.From(18), minValue: MassRateLbPerSec.From(0), maxValue: MassRateLbPerSec.From(60), description: "Feed rate command applied by the startup automation.");
		Parameters.Define(ParameterKeys.StartupAutomationTargetOutletMoisturePercent, Percent.From(15), minValue: Percent.From(10), maxValue: Percent.From(20), description: "Outlet moisture target applied by the startup automation.");
		Parameters.Define(ParameterKeys.ShutdownAutomationEnable, Ratio.From(0), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Runs the sequenced shutdown that stops feed, cools the dryer, and shuts the fan down.");
		Parameters.Define(ParameterKeys.ShutdownAutomationFanSpeedPercent, Percent.From(75), minValue: Percent.From(25), maxValue: Percent.From(100), description: "Fan speed used by the shutdown automation during purge and cooldown.");

		Parameters.Define(ParameterKeys.FeedEnable, Ratio.From(0), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Feed conveyor enable (0..1).");
		Parameters.Define(ParameterKeys.BurnerEnable, Ratio.From(0), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Burner enable (0..1).");
		Parameters.Define(ParameterKeys.FanEnable, Ratio.From(0), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Main drying fan enable (0..1).");

		Parameters.Define(ParameterKeys.FeedRateCommandLbPerSec, MassRateLbPerSec.From(18), minValue: MassRateLbPerSec.From(0), maxValue: MassRateLbPerSec.From(60), description: "Wet grain feed command to the drying column.");
		Parameters.Define(ParameterKeys.BurnerFiringRatePercent, Percent.From(68), minValue: Percent.From(0), maxValue: Percent.From(100), description: "Requested burner firing rate.");
		Parameters.Define(ParameterKeys.FanSpeedPercent, Percent.From(78), minValue: Percent.From(0), maxValue: Percent.From(100), description: "Requested fan speed.");

		Parameters.Define(ParameterKeys.InletMoisturePercent, Percent.From(19.5), minValue: Percent.From(10), maxValue: Percent.From(35), description: "Incoming wet grain moisture.");
		Parameters.Define(ParameterKeys.TargetOutletMoisturePercent, Percent.From(15), minValue: Percent.From(10), maxValue: Percent.From(20), description: "Target dried grain moisture used for quality error reporting.");
		Parameters.Define(ParameterKeys.AmbientAirTempF, TemperatureF.From(55), minValue: TemperatureF.From(-20), maxValue: TemperatureF.From(120), description: "Ambient intake air temperature.");
		Parameters.Define(ParameterKeys.HighTempAlarmThresholdF, TemperatureF.From(180), minValue: TemperatureF.From(100), maxValue: TemperatureF.From(260), description: "High temperature alarm threshold.");

		Signals.Set(SignalKeys.WetBinWeightLb, parameters.Get(ParameterKeys.WetBinWeightLb));
		Signals.Set(SignalKeys.DryBinWeightLb, parameters.Get(ParameterKeys.DryBinWeightLb));
		Signals.Set(SignalKeys.GrainHoldUpLb, WeightLb.From(parameters.Get(ParameterKeys.DryerHoldUpCapacityLb).Value * 0.72));

		Signals.Set(SignalKeys.FeedRateCommandLbPerSec, parameters.Get(ParameterKeys.FeedRateCommandLbPerSec));
		Signals.Set(SignalKeys.BurnerFiringRatePercent, parameters.Get(ParameterKeys.BurnerFiringRatePercent));
		Signals.Set(SignalKeys.FanSpeedPercent, parameters.Get(ParameterKeys.FanSpeedPercent));

		Signals.Set(SignalKeys.FeedRateLbPerSec, MassRateLbPerSec.From(0));
		Signals.Set(SignalKeys.DischargeRateLbPerSec, MassRateLbPerSec.From(0));
		Signals.Set(SignalKeys.ResidenceTimeMinutes, TimeMinutes.From(0));

		Signals.Set(SignalKeys.AirflowCfm, AirflowCfm.From(0));
		Signals.Set(SignalKeys.PlenumTempF, parameters.Get(ParameterKeys.AmbientAirTempF));
		Signals.Set(SignalKeys.ExhaustTempF, parameters.Get(ParameterKeys.AmbientAirTempF));
		Signals.Set(SignalKeys.BurnerOutputPercent, Percent.From(0));

		Signals.Set(SignalKeys.InletMoisturePercent, parameters.Get(ParameterKeys.InletMoisturePercent));
		Signals.Set(SignalKeys.OutletMoisturePercent, Percent.From(17.6));
		Signals.Set(SignalKeys.MoistureControlErrorPercent, Percent.From(2.6));
		Signals.Set(SignalKeys.MoistureRemovalRateLbPerSec, MassRateLbPerSec.From(0));

		Signals.Set(SignalKeys.FeedRunning, Ratio.From(0));
		Signals.Set(SignalKeys.BurnerRunning, Ratio.From(0));
		Signals.Set(SignalKeys.FanRunning, Ratio.From(0));
		Signals.Set(SignalKeys.StartupAutomationActive, Ratio.From(0));
		Signals.Set(SignalKeys.StartupAutomationComplete, Ratio.From(0));
		Signals.Set(SignalKeys.ShutdownAutomationActive, Ratio.From(0));
		Signals.Set(SignalKeys.ShutdownAutomationComplete, Ratio.From(0));

		Signals.Set(SignalKeys.IsWetBinEmpty, Ratio.From(0));
		Signals.Set(SignalKeys.IsDryBinFull, Ratio.From(0));
		Signals.Set(SignalKeys.IsHighTempAlarm, Ratio.From(0));
		Signals.Set(SignalKeys.IsAirflowLowAlarm, Ratio.From(0));

		_system = new SimSystemBuilder()
			.Add(new ShutdownAutomationComponent(Parameters, Signals))
			.Add(new StartupAutomationComponent(Parameters, Signals))
			.Add(new CommandSignalsComponent(Parameters, Signals))
			.Add(new RunStateSignalsComponent(Parameters, Signals))
			.Add(new FanAirflowComponent(Signals))
			.Add(new BurnerHeatComponent(Parameters, Signals))
			.Add(new InventoryDryingComponent(Parameters, Signals))
			.Add(new ExhaustAndAlarmComponent(Parameters, Signals))
			.Build();
	}

	public ParameterStore Parameters { get; }
	public SignalBus Signals { get; }

	public void Tick(SimTime time, double dtSeconds) => _system.Tick(time, dtSeconds);

	private sealed class ShutdownAutomationComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		private enum ShutdownStage
		{
			Idle,
			StopFeed,
			CoolDown,
			StopFan,
			Complete,
		}

		private ShutdownStage _stage;
		private bool _wasEnabled;
		private double _stageElapsedSeconds;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
		[
			new SignalDependency(SignalKeys.FeedRateLbPerSec.Name, typeof(MassRateLbPerSec)),
			new SignalDependency(SignalKeys.PlenumTempF.Name, typeof(TemperatureF)),
			new SignalDependency(SignalKeys.AirflowCfm.Name, typeof(AirflowCfm)),
		];

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.ShutdownAutomationActive.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.ShutdownAutomationComplete.Name, typeof(Ratio)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			var enabled = parameters.Get(ParameterKeys.ShutdownAutomationEnable).Value >= 0.5;
			if (!enabled)
			{
				Reset();
				PublishStatusSignals();
				return;
			}

			if (!_wasEnabled)
			{
				parameters.Set(ParameterKeys.StartupAutomationEnable, Ratio.From(0));
				_stage = ShutdownStage.StopFeed;
				_stageElapsedSeconds = 0.0;
			}

			_wasEnabled = true;
			_stageElapsedSeconds += Math.Max(dtSeconds, 0.0);

			switch (_stage)
			{
				case ShutdownStage.StopFeed:
					ApplyShutdownSetpoints();
					parameters.Set(ParameterKeys.FeedEnable, Ratio.From(0));
					parameters.Set(ParameterKeys.BurnerEnable, Ratio.From(1));
					parameters.Set(ParameterKeys.FanEnable, Ratio.From(1));

					if (signals.Get(SignalKeys.FeedRateLbPerSec).Value <= 1.0 || _stageElapsedSeconds >= 20.0)
					{
						Advance(ShutdownStage.CoolDown);
					}
					break;

				case ShutdownStage.CoolDown:
					ApplyShutdownSetpoints();
					parameters.Set(ParameterKeys.FeedEnable, Ratio.From(0));
					parameters.Set(ParameterKeys.BurnerEnable, Ratio.From(0));
					parameters.Set(ParameterKeys.FanEnable, Ratio.From(1));

					var ambientF = parameters.Get(ParameterKeys.AmbientAirTempF).Value;
					var plenumTempF = signals.Get(SignalKeys.PlenumTempF).Value;
					if ((plenumTempF <= ambientF + 15.0 && signals.Get(SignalKeys.AirflowCfm).Value >= 4000.0)
						|| _stageElapsedSeconds >= 90.0)
					{
						Advance(ShutdownStage.StopFan);
					}
					break;

				case ShutdownStage.StopFan:
					parameters.Set(ParameterKeys.FeedEnable, Ratio.From(0));
					parameters.Set(ParameterKeys.BurnerEnable, Ratio.From(0));
					parameters.Set(ParameterKeys.FanEnable, Ratio.From(0));

					if (signals.Get(SignalKeys.AirflowCfm).Value <= 600.0 || _stageElapsedSeconds >= 12.0)
					{
						Advance(ShutdownStage.Complete);
					}
					break;

				case ShutdownStage.Complete:
					parameters.Set(ParameterKeys.FeedEnable, Ratio.From(0));
					parameters.Set(ParameterKeys.BurnerEnable, Ratio.From(0));
					parameters.Set(ParameterKeys.FanEnable, Ratio.From(0));
					break;

				default:
					_stage = ShutdownStage.StopFeed;
					_stageElapsedSeconds = 0.0;
					break;
			}

			PublishStatusSignals();
		}

		private void ApplyShutdownSetpoints()
		{
			parameters.Set(ParameterKeys.FanSpeedPercent, parameters.Get(ParameterKeys.ShutdownAutomationFanSpeedPercent));
		}

		private void Advance(ShutdownStage nextStage)
		{
			_stage = nextStage;
			_stageElapsedSeconds = 0.0;
		}

		private void Reset()
		{
			_stage = ShutdownStage.Idle;
			_stageElapsedSeconds = 0.0;
			_wasEnabled = false;
		}

		private void PublishStatusSignals()
		{
			var active = _stage is ShutdownStage.StopFeed or ShutdownStage.CoolDown or ShutdownStage.StopFan ? 1.0 : 0.0;
			var complete = _stage == ShutdownStage.Complete ? 1.0 : 0.0;

			signals.Set(SignalKeys.ShutdownAutomationActive, Ratio.From(active));
			signals.Set(SignalKeys.ShutdownAutomationComplete, Ratio.From(complete));
		}
	}

	private sealed class StartupAutomationComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		private enum StartupStage
		{
			Idle,
			StartFan,
			WarmPlenum,
			StartFeed,
			Complete,
		}

		private StartupStage _stage;
		private bool _wasEnabled;
		private double _stageElapsedSeconds;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
		[
			new SignalDependency(SignalKeys.FanRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.BurnerRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.FeedRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.AirflowCfm.Name, typeof(AirflowCfm)),
			new SignalDependency(SignalKeys.PlenumTempF.Name, typeof(TemperatureF)),
			new SignalDependency(SignalKeys.FeedRateLbPerSec.Name, typeof(MassRateLbPerSec)),
		];

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.StartupAutomationActive.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.StartupAutomationComplete.Name, typeof(Ratio)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			if (parameters.Get(ParameterKeys.ShutdownAutomationEnable).Value >= 0.5)
			{
				Reset();
				PublishStatusSignals();
				return;
			}

			var enabled = parameters.Get(ParameterKeys.StartupAutomationEnable).Value >= 0.5;
			if (!enabled)
			{
				Reset();
				PublishStatusSignals();
				return;
			}

			if (!_wasEnabled)
			{
				_stage = StartupStage.StartFan;
				_stageElapsedSeconds = 0.0;
			}

			_wasEnabled = true;
			_stageElapsedSeconds += Math.Max(dtSeconds, 0.0);

			switch (_stage)
			{
				case StartupStage.StartFan:
					ApplyStartupSetpoints();
					parameters.Set(ParameterKeys.FanEnable, Ratio.From(1));
					parameters.Set(ParameterKeys.BurnerEnable, Ratio.From(0));
					parameters.Set(ParameterKeys.FeedEnable, Ratio.From(0));

					if ((signals.Get(SignalKeys.FanRunning).Value >= 0.5 && signals.Get(SignalKeys.AirflowCfm).Value >= 7500.0)
						|| _stageElapsedSeconds >= 20.0)
					{
						Advance(StartupStage.WarmPlenum);
					}
					break;

				case StartupStage.WarmPlenum:
					ApplyStartupSetpoints();
					parameters.Set(ParameterKeys.FanEnable, Ratio.From(1));
					parameters.Set(ParameterKeys.BurnerEnable, Ratio.From(1));
					parameters.Set(ParameterKeys.FeedEnable, Ratio.From(0));

					var ambientF = parameters.Get(ParameterKeys.AmbientAirTempF).Value;
					if ((signals.Get(SignalKeys.BurnerRunning).Value >= 0.5 && signals.Get(SignalKeys.PlenumTempF).Value >= ambientF + 55.0)
						|| _stageElapsedSeconds >= 45.0)
					{
						Advance(StartupStage.StartFeed);
					}
					break;

				case StartupStage.StartFeed:
					ApplyStartupSetpoints();
					parameters.Set(ParameterKeys.FanEnable, Ratio.From(1));
					parameters.Set(ParameterKeys.BurnerEnable, Ratio.From(1));
					parameters.Set(ParameterKeys.FeedEnable, Ratio.From(1));

					var targetFeedRate = parameters.Get(ParameterKeys.StartupAutomationFeedRateCommandLbPerSec).Value;
					if ((signals.Get(SignalKeys.FeedRunning).Value >= 0.5 && signals.Get(SignalKeys.FeedRateLbPerSec).Value >= Math.Max(2.0, targetFeedRate * 0.25))
						|| _stageElapsedSeconds >= 20.0)
					{
						Advance(StartupStage.Complete);
					}
					break;

				case StartupStage.Complete:
					break;

				default:
					_stage = StartupStage.StartFan;
					_stageElapsedSeconds = 0.0;
					break;
			}

			PublishStatusSignals();
		}

		private void ApplyStartupSetpoints()
		{
			parameters.Set(ParameterKeys.FanSpeedPercent, parameters.Get(ParameterKeys.StartupAutomationFanSpeedPercent));
			parameters.Set(ParameterKeys.BurnerFiringRatePercent, parameters.Get(ParameterKeys.StartupAutomationBurnerFiringRatePercent));
			parameters.Set(ParameterKeys.FeedRateCommandLbPerSec, parameters.Get(ParameterKeys.StartupAutomationFeedRateCommandLbPerSec));
			parameters.Set(ParameterKeys.TargetOutletMoisturePercent, parameters.Get(ParameterKeys.StartupAutomationTargetOutletMoisturePercent));
		}

		private void Advance(StartupStage nextStage)
		{
			_stage = nextStage;
			_stageElapsedSeconds = 0.0;
		}

		private void Reset()
		{
			_stage = StartupStage.Idle;
			_stageElapsedSeconds = 0.0;
			_wasEnabled = false;
		}

		private void PublishStatusSignals()
		{
			var active = _stage is StartupStage.StartFan or StartupStage.WarmPlenum or StartupStage.StartFeed ? 1.0 : 0.0;
			var complete = _stage == StartupStage.Complete ? 1.0 : 0.0;

			signals.Set(SignalKeys.StartupAutomationActive, Ratio.From(active));
			signals.Set(SignalKeys.StartupAutomationComplete, Ratio.From(complete));
		}
	}

	private sealed class CommandSignalsComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.FeedRateCommandLbPerSec.Name, typeof(MassRateLbPerSec)),
			new SignalDependency(SignalKeys.BurnerFiringRatePercent.Name, typeof(Percent)),
			new SignalDependency(SignalKeys.FanSpeedPercent.Name, typeof(Percent)),
			new SignalDependency(SignalKeys.InletMoisturePercent.Name, typeof(Percent)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			signals.Set(SignalKeys.FeedRateCommandLbPerSec, parameters.Get(ParameterKeys.FeedRateCommandLbPerSec));
			signals.Set(SignalKeys.BurnerFiringRatePercent, parameters.Get(ParameterKeys.BurnerFiringRatePercent));
			signals.Set(SignalKeys.FanSpeedPercent, parameters.Get(ParameterKeys.FanSpeedPercent));
			signals.Set(SignalKeys.InletMoisturePercent, parameters.Get(ParameterKeys.InletMoisturePercent));
		}
	}

	private sealed class RunStateSignalsComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.FeedRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.BurnerRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.FanRunning.Name, typeof(Ratio)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			var fanRunning = parameters.Get(ParameterKeys.FanEnable).Value >= 0.5 ? 1.0 : 0.0;
			var burnerRunning = fanRunning > 0.5 && parameters.Get(ParameterKeys.BurnerEnable).Value >= 0.5 ? 1.0 : 0.0;
			var feedRunning = fanRunning > 0.5 && parameters.Get(ParameterKeys.FeedEnable).Value >= 0.5 ? 1.0 : 0.0;

			signals.Set(SignalKeys.FanRunning, Ratio.From(fanRunning));
			signals.Set(SignalKeys.BurnerRunning, Ratio.From(burnerRunning));
			signals.Set(SignalKeys.FeedRunning, Ratio.From(feedRunning));
		}
	}

	private sealed class FanAirflowComponent(SignalBus signals) : ISimComponent
	{
		private const double MaxAirflowCfm = 18500;
		private double _airflowCfm;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
		[
			new SignalDependency(SignalKeys.FanRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.FanSpeedPercent.Name, typeof(Percent)),
		];

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.AirflowCfm.Name, typeof(AirflowCfm)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			var fanRunning = signals.Get(SignalKeys.FanRunning).Value >= 0.5;
			var speedPct = fanRunning ? Math.Clamp(signals.Get(SignalKeys.FanSpeedPercent).Value, 0, 100) : 0.0;
			var targetCfm = MaxAirflowCfm * (speedPct / 100.0);

			const double tauSeconds = 2.2;
			var alpha = 1.0 - Math.Exp(-dtSeconds / tauSeconds);
			_airflowCfm += (targetCfm - _airflowCfm) * alpha;
			if (_airflowCfm < 0)
			{
				_airflowCfm = 0;
			}

			signals.Set(SignalKeys.AirflowCfm, AirflowCfm.From(_airflowCfm));
		}
	}

	private sealed class BurnerHeatComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		private const double MaxAirflowCfm = 18500;
		private double _plenumTempF = parameters.Get(ParameterKeys.AmbientAirTempF).Value;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
		[
			new SignalDependency(SignalKeys.BurnerRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.BurnerFiringRatePercent.Name, typeof(Percent)),
			new SignalDependency(SignalKeys.AirflowCfm.Name, typeof(AirflowCfm)),
		];

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.BurnerOutputPercent.Name, typeof(Percent)),
			new SignalDependency(SignalKeys.PlenumTempF.Name, typeof(TemperatureF)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			var ambientF = parameters.Get(ParameterKeys.AmbientAirTempF).Value;
			var burnerRunning = signals.Get(SignalKeys.BurnerRunning).Value >= 0.5;
			var airflowNorm = Math.Clamp(signals.Get(SignalKeys.AirflowCfm).Value / MaxAirflowCfm, 0.0, 1.0);
			var commandPct = burnerRunning ? Math.Clamp(signals.Get(SignalKeys.BurnerFiringRatePercent).Value, 0, 100) : 0.0;

			var airflowPermit = burnerRunning ? Math.Clamp((airflowNorm - 0.18) / 0.82, 0.0, 1.0) : 0.0;
			var burnerOutputPct = commandPct * airflowPermit;

			var targetRise = 220.0 * (burnerOutputPct / 100.0) / (0.60 + (0.95 * Math.Max(airflowNorm, 0.1)));
			var targetTempF = ambientF + Math.Clamp(targetRise, 0.0, 240.0);

			const double tauSeconds = 7.5;
			var alpha = 1.0 - Math.Exp(-dtSeconds / tauSeconds);
			_plenumTempF += (targetTempF - _plenumTempF) * alpha;

			signals.Set(SignalKeys.BurnerOutputPercent, Percent.From(burnerOutputPct));
			signals.Set(SignalKeys.PlenumTempF, TemperatureF.From(_plenumTempF));
		}
	}

	private sealed class InventoryDryingComponent : ISimComponent
	{
		private readonly ParameterStore _parameters;
		private readonly SignalBus _signals;

		private double _wetBinWeightLb;
		private double _dryBinWeightLb;
		private double _dryBinCapacityLb;
		private double _holdUpCapacityLb;
		private double _grainHoldUpLb;
		private double _grainMoisturePercent;
		private double _feedRateLbPerSec;
		private double _dischargeRateLbPerSec;

		public InventoryDryingComponent(ParameterStore parameters, SignalBus signals)
		{
			_parameters = parameters;
			_signals = signals;

			_wetBinWeightLb = parameters.Get(ParameterKeys.WetBinWeightLb).Value;
			_dryBinWeightLb = parameters.Get(ParameterKeys.DryBinWeightLb).Value;
			_dryBinCapacityLb = parameters.Get(ParameterKeys.DryBinCapacityLb).Value;
			_holdUpCapacityLb = parameters.Get(ParameterKeys.DryerHoldUpCapacityLb).Value;
			_grainHoldUpLb = Math.Clamp(_holdUpCapacityLb * 0.72, 0.0, _holdUpCapacityLb);
			_grainMoisturePercent = Math.Clamp(parameters.Get(ParameterKeys.TargetOutletMoisturePercent).Value + 2.6, 9.0, parameters.Get(ParameterKeys.InletMoisturePercent).Value);

			_parameters.ParameterChanged += OnParameterChanged;
		}

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
		[
			new SignalDependency(SignalKeys.FeedRateCommandLbPerSec.Name, typeof(MassRateLbPerSec)),
			new SignalDependency(SignalKeys.PlenumTempF.Name, typeof(TemperatureF)),
			new SignalDependency(SignalKeys.AirflowCfm.Name, typeof(AirflowCfm)),
			new SignalDependency(SignalKeys.InletMoisturePercent.Name, typeof(Percent)),
			new SignalDependency(SignalKeys.FeedRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.BurnerRunning.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.FanRunning.Name, typeof(Ratio)),
		];

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.WetBinWeightLb.Name, typeof(WeightLb)),
			new SignalDependency(SignalKeys.DryBinWeightLb.Name, typeof(WeightLb)),
			new SignalDependency(SignalKeys.GrainHoldUpLb.Name, typeof(WeightLb)),
			new SignalDependency(SignalKeys.FeedRateLbPerSec.Name, typeof(MassRateLbPerSec)),
			new SignalDependency(SignalKeys.DischargeRateLbPerSec.Name, typeof(MassRateLbPerSec)),
			new SignalDependency(SignalKeys.ResidenceTimeMinutes.Name, typeof(TimeMinutes)),
			new SignalDependency(SignalKeys.OutletMoisturePercent.Name, typeof(Percent)),
			new SignalDependency(SignalKeys.MoistureControlErrorPercent.Name, typeof(Percent)),
			new SignalDependency(SignalKeys.MoistureRemovalRateLbPerSec.Name, typeof(MassRateLbPerSec)),
			new SignalDependency(SignalKeys.IsWetBinEmpty.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.IsDryBinFull.Name, typeof(Ratio)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			if (dtSeconds <= 0)
			{
				return;
			}

			var inletMoisture = Math.Clamp(_signals.Get(SignalKeys.InletMoisturePercent).Value, 10.0, 35.0);
			var targetOutletMoisture = Math.Clamp(_parameters.Get(ParameterKeys.TargetOutletMoisturePercent).Value, 10.0, 20.0);
			var ambientF = _parameters.Get(ParameterKeys.AmbientAirTempF).Value;
			var plenumTempF = _signals.Get(SignalKeys.PlenumTempF).Value;
			var airflowCfm = Math.Max(0.0, _signals.Get(SignalKeys.AirflowCfm).Value);
			var feedRunning = _signals.Get(SignalKeys.FeedRunning).Value >= 0.5;
			var burnerRunning = _signals.Get(SignalKeys.BurnerRunning).Value >= 0.5;
			var fanRunning = _signals.Get(SignalKeys.FanRunning).Value >= 0.5;
			var feedCommand = Math.Max(0.0, _signals.Get(SignalKeys.FeedRateCommandLbPerSec).Value);

			var wetAvailableLb = Math.Max(0.0, _wetBinWeightLb);
			var drySpaceLb = Math.Max(0.0, _dryBinCapacityLb - _dryBinWeightLb);
			var holdUpSpaceLb = Math.Max(0.0, _holdUpCapacityLb - _grainHoldUpLb);

			var targetFeedRate = feedRunning && wetAvailableLb > 0.0001 && drySpaceLb > 0.0001 ? feedCommand : 0.0;
			const double feedTauSeconds = 1.4;
			var feedAlpha = 1.0 - Math.Exp(-dtSeconds / feedTauSeconds);
			_feedRateLbPerSec += (targetFeedRate - _feedRateLbPerSec) * feedAlpha;
			_feedRateLbPerSec = Math.Max(0.0, _feedRateLbPerSec);

			var purgeRate = drySpaceLb > 0.0001
				? Math.Min(Math.Max(feedCommand * 0.55, 4.0), _grainHoldUpLb / dtSeconds)
				: 0.0;
			var targetDischargeRate = drySpaceLb <= 0.0001
				? 0.0
				: feedRunning && wetAvailableLb > 0.0001
					? Math.Max(_feedRateLbPerSec, Math.Min(_grainHoldUpLb / 1800.0, feedCommand * 1.15))
					: purgeRate;

			const double dischargeTauSeconds = 1.9;
			var dischargeAlpha = 1.0 - Math.Exp(-dtSeconds / dischargeTauSeconds);
			_dischargeRateLbPerSec += (targetDischargeRate - _dischargeRateLbPerSec) * dischargeAlpha;
			_dischargeRateLbPerSec = Math.Max(0.0, _dischargeRateLbPerSec);

			var dischargeMassLb = Math.Min(_dischargeRateLbPerSec * dtSeconds, Math.Min(_grainHoldUpLb, drySpaceLb));
			var feedMassLimitBySpaceLb = holdUpSpaceLb + dischargeMassLb;
			var feedMassLb = Math.Min(_feedRateLbPerSec * dtSeconds, Math.Min(wetAvailableLb, Math.Max(0.0, feedMassLimitBySpaceLb)));

			_wetBinWeightLb = Math.Max(0.0, _wetBinWeightLb - feedMassLb);
			_grainHoldUpLb = Math.Clamp(_grainHoldUpLb + feedMassLb - dischargeMassLb, 0.0, _holdUpCapacityLb);
			_dryBinWeightLb = Math.Clamp(_dryBinWeightLb + dischargeMassLb, 0.0, _dryBinCapacityLb);

			var moistureBeforeMixPercent = _grainMoisturePercent;
			if (_grainHoldUpLb > 0.0001 && feedMassLb > 0.0)
			{
				var mixFraction = Math.Clamp(feedMassLb / _grainHoldUpLb, 0.0, 1.0);
				_grainMoisturePercent += (inletMoisture - _grainMoisturePercent) * mixFraction;
			}

			var airflowNorm = Math.Clamp(airflowCfm / 18500.0, 0.0, 1.0);
			var tempDrive = Math.Clamp((plenumTempF - ambientF - 15.0) / 140.0, 0.0, 1.5);
			var residenceMinutes = _dischargeRateLbPerSec > 0.05
				? _grainHoldUpLb / _dischargeRateLbPerSec / 60.0
				: (_grainHoldUpLb > 0.0001 ? 45.0 : 0.0);
			var residenceNorm = Math.Clamp(residenceMinutes / 24.0, 0.0, 2.0);

			var equilibriumMoisture = Math.Clamp(
				inletMoisture - (0.8 + (6.8 * tempDrive) + (3.8 * airflowNorm) + (1.7 * residenceNorm)),
				9.0,
				inletMoisture);

			var dryingStrength = (0.012 + (0.050 * airflowNorm)) * (fanRunning ? 0.30 : 0.0) * (0.20 + tempDrive + (burnerRunning ? 0.25 : 0.0));
			var dryingAlpha = 1.0 - Math.Exp(-dtSeconds * dryingStrength);
			var moistureBeforeDryingPercent = _grainMoisturePercent;
			_grainMoisturePercent += (equilibriumMoisture - _grainMoisturePercent) * dryingAlpha;
			_grainMoisturePercent = Math.Clamp(_grainMoisturePercent, 9.0, inletMoisture);

			var moistureDropPercent = Math.Max(0.0, moistureBeforeDryingPercent - _grainMoisturePercent);
			var effectiveDryingMassLb = Math.Max(dischargeMassLb, _grainHoldUpLb * 0.03);
			var moistureRemovalRateLbPerSec = (effectiveDryingMassLb * (moistureDropPercent / 100.0)) / dtSeconds;

			_signals.Set(SignalKeys.WetBinWeightLb, WeightLb.From(_wetBinWeightLb));
			_signals.Set(SignalKeys.DryBinWeightLb, WeightLb.From(_dryBinWeightLb));
			_signals.Set(SignalKeys.GrainHoldUpLb, WeightLb.From(_grainHoldUpLb));

			_signals.Set(SignalKeys.FeedRateLbPerSec, MassRateLbPerSec.From(feedMassLb / dtSeconds));
			_signals.Set(SignalKeys.DischargeRateLbPerSec, MassRateLbPerSec.From(dischargeMassLb / dtSeconds));
			_signals.Set(SignalKeys.ResidenceTimeMinutes, TimeMinutes.From(residenceMinutes));

			_signals.Set(SignalKeys.OutletMoisturePercent, Percent.From(_grainMoisturePercent));
			_signals.Set(SignalKeys.MoistureControlErrorPercent, Percent.From(_grainMoisturePercent - targetOutletMoisture));
			_signals.Set(SignalKeys.MoistureRemovalRateLbPerSec, MassRateLbPerSec.From(Math.Max(0.0, moistureRemovalRateLbPerSec)));

			_signals.Set(SignalKeys.IsWetBinEmpty, Ratio.From(_wetBinWeightLb <= 0.0001 ? 1.0 : 0.0));
			_signals.Set(SignalKeys.IsDryBinFull, Ratio.From(_dryBinCapacityLb > 0.0 && _dryBinWeightLb >= _dryBinCapacityLb - 0.0001 ? 1.0 : 0.0));
		}

		private void OnParameterChanged(object? sender, ParameterChangedEventArgs e)
		{
			if (e.Name.Equals(ParameterKeys.WetBinWeightLb.Name, StringComparison.OrdinalIgnoreCase))
			{
				_wetBinWeightLb = Math.Max(0.0, e.NewValue);
			}
			else if (e.Name.Equals(ParameterKeys.DryBinWeightLb.Name, StringComparison.OrdinalIgnoreCase))
			{
				_dryBinWeightLb = Math.Max(0.0, e.NewValue);
			}
			else if (e.Name.Equals(ParameterKeys.DryBinCapacityLb.Name, StringComparison.OrdinalIgnoreCase))
			{
				_dryBinCapacityLb = Math.Max(1.0, e.NewValue);
				_dryBinWeightLb = Math.Min(_dryBinWeightLb, _dryBinCapacityLb);
			}
			else if (e.Name.Equals(ParameterKeys.DryerHoldUpCapacityLb.Name, StringComparison.OrdinalIgnoreCase))
			{
				_holdUpCapacityLb = Math.Max(1.0, e.NewValue);
				_grainHoldUpLb = Math.Min(_grainHoldUpLb, _holdUpCapacityLb);
			}
		}
	}

	private sealed class ExhaustAndAlarmComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		private double _exhaustTempF = parameters.Get(ParameterKeys.AmbientAirTempF).Value;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
		[
			new SignalDependency(SignalKeys.PlenumTempF.Name, typeof(TemperatureF)),
			new SignalDependency(SignalKeys.AirflowCfm.Name, typeof(AirflowCfm)),
			new SignalDependency(SignalKeys.OutletMoisturePercent.Name, typeof(Percent)),
			new SignalDependency(SignalKeys.BurnerRunning.Name, typeof(Ratio)),
		];

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.ExhaustTempF.Name, typeof(TemperatureF)),
			new SignalDependency(SignalKeys.IsHighTempAlarm.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.IsAirflowLowAlarm.Name, typeof(Ratio)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			var ambientF = parameters.Get(ParameterKeys.AmbientAirTempF).Value;
			var alarmThresholdF = parameters.Get(ParameterKeys.HighTempAlarmThresholdF).Value;
			var plenumTempF = signals.Get(SignalKeys.PlenumTempF).Value;
			var airflowCfm = Math.Max(0.0, signals.Get(SignalKeys.AirflowCfm).Value);
			var outletMoisturePercent = Math.Clamp(signals.Get(SignalKeys.OutletMoisturePercent).Value, 8.0, 35.0);
			var burnerRunning = signals.Get(SignalKeys.BurnerRunning).Value >= 0.5;

			var coolingEffectF = Math.Clamp((outletMoisturePercent - 10.0) * 3.1, 0.0, 28.0);
			var airflowBoost = Math.Clamp(airflowCfm / 18500.0, 0.0, 1.0);
			var targetExhaustF = ambientF + ((plenumTempF - ambientF) * (0.42 + (0.18 * airflowBoost))) - coolingEffectF;
			targetExhaustF = Math.Clamp(targetExhaustF, ambientF, plenumTempF);

			const double tauSeconds = 5.0;
			var alpha = 1.0 - Math.Exp(-dtSeconds / tauSeconds);
			_exhaustTempF += (targetExhaustF - _exhaustTempF) * alpha;

			var highTempAlarm = plenumTempF >= alarmThresholdF ? 1.0 : 0.0;
			var lowAirflowAlarm = burnerRunning && airflowCfm < 7000.0 ? 1.0 : 0.0;

			signals.Set(SignalKeys.ExhaustTempF, TemperatureF.From(_exhaustTempF));
			signals.Set(SignalKeys.IsHighTempAlarm, Ratio.From(highTempAlarm));
			signals.Set(SignalKeys.IsAirflowLowAlarm, Ratio.From(lowAirflowAlarm));
		}
	}
}
