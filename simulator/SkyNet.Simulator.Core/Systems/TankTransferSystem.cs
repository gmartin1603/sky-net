using SkyNet.Simulator.Core.Components;
using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Signals;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Units;

namespace SkyNet.Simulator.Core.Systems;

/// <summary>
/// Tank-to-tank granular transfer through a rotary airlock into a blower/pressure line.
/// Training-grade (not fully physical), with realistic-ish relationships:
/// - Airlock speed and blowline pressure increase transfer rate.
/// - Higher flow and pressure increase blower motor %FLA.
/// - First-order lags on airlock speed and blowline pressure.
/// </summary>
public sealed class TankTransferSystem : ISimSystem
{
	private readonly SimSystem _system;

	public static class ParameterKeys
	{
		public static readonly ParameterKey<WeightLb> SourceTankWeightLb = new("SourceTankWeightLb");
		public static readonly ParameterKey<WeightLb> DestinationTankWeightLb = new("DestinationTankWeightLb");
		public static readonly ParameterKey<WeightLb> DestinationTankCapacityLb = new("DestinationTankCapacityLb");

		// Operator enable flags (0 or 1)
		public static readonly ParameterKey<Ratio> BlowerEnable = new("BlowerEnable");
		public static readonly ParameterKey<Ratio> AirlockEnable = new("AirlockEnable");

		public static readonly ParameterKey<PressurePsi> BlowlinePressureCommandPsi = new("BlowlinePressureCommandPsi");
		public static readonly ParameterKey<FrequencyHz> AirlockSpeedCommandHz = new("AirlockSpeedCommandHz");
	}

	public static class SignalKeys
	{
		public static readonly SignalKey<WeightLb> SourceTankWeightLb = new("SourceTankWeightLb");
		public static readonly SignalKey<WeightLb> DestinationTankWeightLb = new("DestinationTankWeightLb");

		public static readonly SignalKey<PressurePsi> BlowlinePressureCommandPsi = new("BlowlinePressureCommandPsi");
		public static readonly SignalKey<PressurePsi> BlowlinePressurePsi = new("BlowlinePressurePsi");

		public static readonly SignalKey<FrequencyHz> AirlockSpeedCommandHz = new("AirlockSpeedCommandHz");
		public static readonly SignalKey<FrequencyHz> AirlockSpeedHz = new("AirlockSpeedHz");

		public static readonly SignalKey<MassRateLbPerSec> TransferRateCommandLbPerSec = new("TransferRateCommandLbPerSec");
		public static readonly SignalKey<MassRateLbPerSec> TransferRateLbPerSec = new("TransferRateLbPerSec");

		public static readonly SignalKey<Percent> BlowerMotorPercentFla = new("BlowerMotorPercentFla");

		// Operator status flags (0 or 1)
		public static readonly SignalKey<Ratio> BlowerRunning = new("BlowerRunning");
		public static readonly SignalKey<Ratio> AirlockRunning = new("AirlockRunning");

		// Flags (0 or 1)
		public static readonly SignalKey<Ratio> IsStarved = new("IsStarved");
		public static readonly SignalKey<Ratio> IsFull = new("IsFull");
	}

	public TankTransferSystem(ParameterStore parameters)
	{
		Parameters = parameters;
		Signals = new SignalBus();

		Parameters.Define(ParameterKeys.SourceTankWeightLb, WeightLb.From(20000), minValue: WeightLb.From(0), description: "Source tank inventory (lb). Can be adjusted live.");
		Parameters.Define(ParameterKeys.DestinationTankWeightLb, WeightLb.From(5000), minValue: WeightLb.From(0), description: "Destination tank inventory (lb). Can be adjusted live.");
		Parameters.Define(ParameterKeys.DestinationTankCapacityLb, WeightLb.From(40000), minValue: WeightLb.From(0), description: "Destination tank capacity (lb). Used for IsFull + clamping.");

		Parameters.Define(ParameterKeys.BlowerEnable, Ratio.From(0), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Operator blower enable (0..1). When disabled, blowline pressure decays and blower load is 0.");
		Parameters.Define(ParameterKeys.AirlockEnable, Ratio.From(0), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Operator airlock enable (0..1). When disabled, airlock speed decays and transfer stops.");

		Parameters.Define(ParameterKeys.BlowlinePressureCommandPsi, PressurePsi.From(8), minValue: PressurePsi.From(0), maxValue: PressurePsi.From(25), description: "Blowline pressure setpoint (psi).");
		Parameters.Define(ParameterKeys.AirlockSpeedCommandHz, FrequencyHz.From(8), minValue: FrequencyHz.From(0), maxValue: FrequencyHz.From(30), description: "Rotary airlock speed setpoint (Hz).");

		Signals.Set(SignalKeys.SourceTankWeightLb, parameters.Get(ParameterKeys.SourceTankWeightLb));
		Signals.Set(SignalKeys.DestinationTankWeightLb, parameters.Get(ParameterKeys.DestinationTankWeightLb));

		Signals.Set(SignalKeys.BlowlinePressureCommandPsi, parameters.Get(ParameterKeys.BlowlinePressureCommandPsi));
		Signals.Set(SignalKeys.BlowlinePressurePsi, PressurePsi.From(0));

		Signals.Set(SignalKeys.AirlockSpeedCommandHz, parameters.Get(ParameterKeys.AirlockSpeedCommandHz));
		Signals.Set(SignalKeys.AirlockSpeedHz, FrequencyHz.From(0));

		Signals.Set(SignalKeys.TransferRateCommandLbPerSec, MassRateLbPerSec.From(0));
		Signals.Set(SignalKeys.TransferRateLbPerSec, MassRateLbPerSec.From(0));
		Signals.Set(SignalKeys.BlowerMotorPercentFla, Percent.From(0));
		Signals.Set(SignalKeys.BlowerRunning, Ratio.From(0));
		Signals.Set(SignalKeys.AirlockRunning, Ratio.From(0));
		Signals.Set(SignalKeys.IsStarved, Ratio.From(0));
		Signals.Set(SignalKeys.IsFull, Ratio.From(0));

		_system = new SimSystemBuilder()
			.Add(new CommandSignalsComponent(Parameters, Signals))
			.Add(new RunStateSignalsComponent(Parameters, Signals))
			.Add(new AirlockDriveComponent(Signals))
			.Add(new TransferSetpointComponent(Signals))
			.Add(new TankInventoryComponent(Parameters, Signals))
			.Add(new BlowlinePressureDynamicsComponent(Signals))
			.Add(new BlowerMotorComponent(Signals))
			.Build();
	}

	public ParameterStore Parameters { get; }
	public SignalBus Signals { get; }

	public void Tick(SimTime time, double dtSeconds) => _system.Tick(time, dtSeconds);

	private sealed class CommandSignalsComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.BlowlinePressureCommandPsi.Name, typeof(PressurePsi)),
				new SignalDependency(SignalKeys.AirlockSpeedCommandHz.Name, typeof(FrequencyHz)),
			};

		public void Tick(SimTime time, double dtSeconds)
		{
			signals.Set(SignalKeys.BlowlinePressureCommandPsi, parameters.Get(ParameterKeys.BlowlinePressureCommandPsi));
			signals.Set(SignalKeys.AirlockSpeedCommandHz, parameters.Get(ParameterKeys.AirlockSpeedCommandHz));
		}
	}

	private sealed class RunStateSignalsComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.BlowerRunning.Name, typeof(Ratio)),
				new SignalDependency(SignalKeys.AirlockRunning.Name, typeof(Ratio)),
			};

		public void Tick(SimTime time, double dtSeconds)
		{
			var blowerEnable = parameters.Get(ParameterKeys.BlowerEnable).Value >= 0.5 ? 1.0 : 0.0;
			var airlockEnable = parameters.Get(ParameterKeys.AirlockEnable).Value >= 0.5 ? 1.0 : 0.0;
			signals.Set(SignalKeys.BlowerRunning, Ratio.From(blowerEnable));
			signals.Set(SignalKeys.AirlockRunning, Ratio.From(airlockEnable));
		}
	}

	private sealed class AirlockDriveComponent(SignalBus signals) : ISimComponent
	{
		private double _speedHz;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.AirlockSpeedCommandHz.Name, typeof(FrequencyHz)),
				new SignalDependency(SignalKeys.AirlockRunning.Name, typeof(Ratio)),
			};

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[] { new SignalDependency(SignalKeys.AirlockSpeedHz.Name, typeof(FrequencyHz)) };

		public void Tick(SimTime time, double dtSeconds)
		{
			var running = signals.Get(SignalKeys.AirlockRunning).Value >= 0.5;
			var cmd = running ? signals.Get(SignalKeys.AirlockSpeedCommandHz).Value : 0.0;

			// First-order lag to represent drive inertia.
			const double tauSeconds = 0.6;
			var alpha = 1.0 - Math.Exp(-dtSeconds / tauSeconds);
			_speedHz += (cmd - _speedHz) * alpha;
			if (_speedHz < 0) _speedHz = 0;

			signals.Set(SignalKeys.AirlockSpeedHz, FrequencyHz.From(_speedHz));
		}
	}

	private sealed class TransferSetpointComponent(SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.AirlockSpeedHz.Name, typeof(FrequencyHz)),
				new SignalDependency(SignalKeys.BlowlinePressureCommandPsi.Name, typeof(PressurePsi)),
				new SignalDependency(SignalKeys.AirlockRunning.Name, typeof(Ratio)),
				new SignalDependency(SignalKeys.BlowerRunning.Name, typeof(Ratio)),
			};

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[] { new SignalDependency(SignalKeys.TransferRateCommandLbPerSec.Name, typeof(MassRateLbPerSec)) };

		public void Tick(SimTime time, double dtSeconds)
		{
			var airlockRunning = signals.Get(SignalKeys.AirlockRunning).Value >= 0.5;
			var blowerRunning = signals.Get(SignalKeys.BlowerRunning).Value >= 0.5;
			if (!airlockRunning || !blowerRunning)
			{
				signals.Set(SignalKeys.TransferRateCommandLbPerSec, MassRateLbPerSec.From(0));
				return;
			}

			var airlockHz = signals.Get(SignalKeys.AirlockSpeedHz).Value;
			var pCmd = signals.Get(SignalKeys.BlowlinePressureCommandPsi).Value;

			// Training-grade conveying curve: pressure helps up to a saturation.
			// f(p)=1-exp(-p/P50) gives diminishing returns.
			const double p50 = 6.0;
			var pressureFactor = 1.0 - Math.Exp(-Math.Max(0, pCmd) / p50);

			// Base feed per Hz. Tuned for reasonable ranges: at 10 Hz and 10 psi -> ~25 lb/s.
			const double baseLbPerSecPerHz = 3.0;
			var commanded = baseLbPerSecPerHz * Math.Max(0, airlockHz) * pressureFactor;

			signals.Set(SignalKeys.TransferRateCommandLbPerSec, MassRateLbPerSec.From(commanded));
		}
	}

	private sealed class TankInventoryComponent : ISimComponent
	{
		private readonly ParameterStore _parameters;
		private readonly SignalBus _signals;

		private double _sourceWeightLb;
		private double _destWeightLb;
		private double _destCapacityLb;

		public TankInventoryComponent(ParameterStore parameters, SignalBus signals)
		{
			_parameters = parameters;
			_signals = signals;

			_sourceWeightLb = parameters.Get(ParameterKeys.SourceTankWeightLb).Value;
			_destWeightLb = parameters.Get(ParameterKeys.DestinationTankWeightLb).Value;
			_destCapacityLb = parameters.Get(ParameterKeys.DestinationTankCapacityLb).Value;

			// Allow live edits (refill/empty) without adding extra control plumbing.
			_parameters.ParameterChanged += OnParameterChanged;
		}

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[] { new SignalDependency(SignalKeys.TransferRateCommandLbPerSec.Name, typeof(MassRateLbPerSec)) };

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.SourceTankWeightLb.Name, typeof(WeightLb)),
				new SignalDependency(SignalKeys.DestinationTankWeightLb.Name, typeof(WeightLb)),
				new SignalDependency(SignalKeys.TransferRateLbPerSec.Name, typeof(MassRateLbPerSec)),
				new SignalDependency(SignalKeys.IsStarved.Name, typeof(Ratio)),
				new SignalDependency(SignalKeys.IsFull.Name, typeof(Ratio)),
			};

		public void Tick(SimTime time, double dtSeconds)
		{
			var cmdRate = Math.Max(0, _signals.Get(SignalKeys.TransferRateCommandLbPerSec).Value);

			var available = Math.Max(0, _sourceWeightLb);
			var destSpace = Math.Max(0, _destCapacityLb - _destWeightLb);

			var requestedDelta = cmdRate * dtSeconds;
			var actualDelta = Math.Min(requestedDelta, Math.Min(available, destSpace));

			_sourceWeightLb = Math.Max(0, _sourceWeightLb - actualDelta);
			_destWeightLb = Math.Max(0, _destWeightLb + actualDelta);

			var actualRate = dtSeconds > 0 ? actualDelta / dtSeconds : 0;

			_signals.Set(SignalKeys.SourceTankWeightLb, WeightLb.From(_sourceWeightLb));
			_signals.Set(SignalKeys.DestinationTankWeightLb, WeightLb.From(_destWeightLb));
			_signals.Set(SignalKeys.TransferRateLbPerSec, MassRateLbPerSec.From(actualRate));

			_signals.Set(SignalKeys.IsStarved, Ratio.From(_sourceWeightLb <= 0.0001 ? 1.0 : 0.0));
			_signals.Set(SignalKeys.IsFull, Ratio.From(_destCapacityLb > 0 && _destWeightLb >= _destCapacityLb - 0.0001 ? 1.0 : 0.0));
		}

		private void OnParameterChanged(object? sender, ParameterChangedEventArgs e)
		{
			// Keep internal state aligned when users “teleport” inventories via param edits.
			if (e.Name.Equals(ParameterKeys.SourceTankWeightLb.Name, StringComparison.OrdinalIgnoreCase))
			{
				_sourceWeightLb = Math.Max(0, e.NewValue);
			}
			else if (e.Name.Equals(ParameterKeys.DestinationTankWeightLb.Name, StringComparison.OrdinalIgnoreCase))
			{
				_destWeightLb = Math.Max(0, e.NewValue);
			}
			else if (e.Name.Equals(ParameterKeys.DestinationTankCapacityLb.Name, StringComparison.OrdinalIgnoreCase))
			{
				_destCapacityLb = Math.Max(0, e.NewValue);
				if (_destCapacityLb > 0 && _destWeightLb > _destCapacityLb)
				{
					_destWeightLb = _destCapacityLb;
				}
			}
		}
	}

	private sealed class BlowlinePressureDynamicsComponent(SignalBus signals) : ISimComponent
	{
		private double _pressurePsi;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.BlowlinePressureCommandPsi.Name, typeof(PressurePsi)),
				new SignalDependency(SignalKeys.TransferRateLbPerSec.Name, typeof(MassRateLbPerSec)),
				new SignalDependency(SignalKeys.AirlockSpeedHz.Name, typeof(FrequencyHz)),
				new SignalDependency(SignalKeys.AirlockRunning.Name, typeof(Ratio)),
				new SignalDependency(SignalKeys.BlowerRunning.Name, typeof(Ratio)),
			};

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[] { new SignalDependency(SignalKeys.BlowlinePressurePsi.Name, typeof(PressurePsi)) };

		public void Tick(SimTime time, double dtSeconds)
		{
			var blowerRunning = signals.Get(SignalKeys.BlowerRunning).Value >= 0.5;
			if (!blowerRunning)
			{
				const double tauSecondsOff = 0.9;
				var alphaOff = 1.0 - Math.Exp(-dtSeconds / tauSecondsOff);
				_pressurePsi += (0.0 - _pressurePsi) * alphaOff;
				signals.Set(SignalKeys.BlowlinePressurePsi, PressurePsi.From(_pressurePsi));
				return;
			}

			var pCmd = Math.Max(0, signals.Get(SignalKeys.BlowlinePressureCommandPsi).Value);
			var airlockHz = Math.Max(0, signals.Get(SignalKeys.AirlockSpeedHz).Value);
			var airlockRunning = signals.Get(SignalKeys.AirlockRunning).Value >= 0.5;

			// Training-grade behavior: achieved pressure increases with airlock speed (conveying demand).
			// When the airlock is off, pressure is intentionally kept at the lowest level.
			const double maxHz = 30.0;
			var speedNorm = Math.Clamp(airlockHz / maxHz, 0, 1);
			var minFactor = airlockRunning ? 0.20 : 0.05;
			var speedFactor = minFactor + (1.0 - minFactor) * speedNorm;
			var target = pCmd * speedFactor;

			const double tauSeconds = 0.9;
			var alpha = 1.0 - Math.Exp(-dtSeconds / tauSeconds);
			_pressurePsi += (target - _pressurePsi) * alpha;

			signals.Set(SignalKeys.BlowlinePressurePsi, PressurePsi.From(_pressurePsi));
		}
	}

	private sealed class BlowerMotorComponent(SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.BlowlinePressurePsi.Name, typeof(PressurePsi)),
				new SignalDependency(SignalKeys.TransferRateLbPerSec.Name, typeof(MassRateLbPerSec)),
				new SignalDependency(SignalKeys.BlowerRunning.Name, typeof(Ratio)),
			};

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[] { new SignalDependency(SignalKeys.BlowerMotorPercentFla.Name, typeof(Percent)) };

		public void Tick(SimTime time, double dtSeconds)
		{
			var blowerRunning = signals.Get(SignalKeys.BlowerRunning).Value >= 0.5;
			if (!blowerRunning)
			{
				signals.Set(SignalKeys.BlowerMotorPercentFla, Percent.From(0));
				return;
			}

			var p = Math.Max(0, signals.Get(SignalKeys.BlowlinePressurePsi).Value);
			var flow = Math.Max(0, signals.Get(SignalKeys.TransferRateLbPerSec).Value);

			// Training-grade approximation: blower load rises with both pressure and conveying mass flow.
			// Tuned for ~20..120% typical.
			var pressureTerm = 55.0 * (1.0 - Math.Exp(-p / 8.0));
			var flowTerm = 70.0 * (1.0 - Math.Exp(-flow / 18.0));
			var baseTerm = 15.0;

			var percent = baseTerm + pressureTerm + flowTerm;
			percent = Math.Clamp(percent, 0, 140);

			signals.Set(SignalKeys.BlowerMotorPercentFla, Percent.From(percent));
		}
	}
}
