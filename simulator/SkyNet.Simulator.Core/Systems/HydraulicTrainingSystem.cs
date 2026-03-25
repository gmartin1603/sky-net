using SkyNet.Simulator.Core.Components;
using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Signals;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Units;

namespace SkyNet.Simulator.Core.Systems;

/// <summary>
/// Training-oriented hydraulic slice built from multiple components.
/// Still not a physically accurate hydraulic model; focuses on wiring, observability,
/// typed units, and deterministic update ordering.
/// </summary>
public sealed class HydraulicTrainingSystem : ISimSystem
{
	private readonly SimSystem _system;

	public static class ParameterKeys
	{
		public static readonly ParameterKey<PressurePsi> SupplyPressurePsi = new("SupplyPressurePsi");
		public static readonly ParameterKey<Ratio> ValveOpening = new("ValveOpening");
		public static readonly ParameterKey<Ratio> LoadForce = new("LoadForce");
		public static readonly ParameterKey<FlowGpm> FlowGainGpm = new("FlowGainGpm");
		public static readonly ParameterKey<Ratio> PositionControlEnable = new("PositionControlEnable");
		public static readonly ParameterKey<Position> ActuatorPositionCommand = new("ActuatorPositionCommand");
		public static readonly ParameterKey<Ratio> PositionControlKp = new("PositionControlKp");
		public static readonly ParameterKey<Ratio> PositionControlKi = new("PositionControlKi");
		public static readonly ParameterKey<Ratio> PositionControlKd = new("PositionControlKd");
	}

	public static class SignalKeys
	{
		public static readonly SignalKey<PressurePsi> UpstreamPressurePsi = new("UpstreamPressurePsi");
		public static readonly SignalKey<PressurePsi> DownstreamPressurePsi = new("DownstreamPressurePsi");
		public static readonly SignalKey<PressurePsi> DownstreamPressureSensorPsi = new("DownstreamPressureSensorPsi");
		public static readonly SignalKey<FlowGpm> ValveFlowGpm = new("ValveFlowGpm");
		public static readonly SignalKey<Position> ActuatorPosition = new("ActuatorPosition");
		public static readonly SignalKey<Velocity> ActuatorVelocity = new("ActuatorVelocity");
		public static readonly SignalKey<Ratio> PositionControlActive = new("PositionControlActive");
		public static readonly SignalKey<Position> ActuatorPositionCommand = new("ActuatorPositionCommand");
		public static readonly SignalKey<Position> ActuatorPositionError = new("ActuatorPositionError");
		public static readonly SignalKey<Ratio> ValveOpeningCommand = new("ValveOpeningCommand");
	}

	public HydraulicTrainingSystem(ParameterStore parameters)
	{
		Parameters = parameters;
		Signals = new SignalBus();

		Parameters.Define(ParameterKeys.SupplyPressurePsi, PressurePsi.From(2000), minValue: PressurePsi.From(0), description: "Upstream supply pressure.");
		Parameters.Define(ParameterKeys.ValveOpening, Ratio.From(0.2), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Valve opening ratio (0..1).");
		Parameters.Define(ParameterKeys.LoadForce, Ratio.From(800), minValue: Ratio.From(0), description: "Training-load value (unitless).");
		Parameters.Define(ParameterKeys.FlowGainGpm, FlowGpm.From(20), minValue: FlowGpm.From(0), description: "Flow gain (gpm at opening=1 and 1000 psi).");
		Parameters.Define(ParameterKeys.PositionControlEnable, Ratio.From(0), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Enables PID position control. When enabled, valve opening is adjusted automatically to drive actuator position to the target.");
		Parameters.Define(ParameterKeys.ActuatorPositionCommand, Position.From(0), minValue: Position.From(-12), maxValue: Position.From(12), description: "Actuator position target used by the PID loop.");
		Parameters.Define(ParameterKeys.PositionControlKp, Ratio.From(0.08), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Proportional gain for the hydraulic position loop.");
		Parameters.Define(ParameterKeys.PositionControlKi, Ratio.From(0.02), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Integral gain for the hydraulic position loop.");
		Parameters.Define(ParameterKeys.PositionControlKd, Ratio.From(0.08), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Derivative gain for the hydraulic position loop.");

		Signals.Set(SignalKeys.UpstreamPressurePsi, PressurePsi.From(0));
		Signals.Set(SignalKeys.DownstreamPressurePsi, PressurePsi.From(0));
		Signals.Set(SignalKeys.DownstreamPressureSensorPsi, PressurePsi.From(0));
		Signals.Set(SignalKeys.ValveFlowGpm, FlowGpm.From(0));
		Signals.Set(SignalKeys.ActuatorPosition, Position.From(0));
		Signals.Set(SignalKeys.ActuatorVelocity, Velocity.From(0));
		Signals.Set(SignalKeys.PositionControlActive, Ratio.From(0));
		Signals.Set(SignalKeys.ActuatorPositionCommand, parameters.Get(ParameterKeys.ActuatorPositionCommand));
		Signals.Set(SignalKeys.ActuatorPositionError, Position.From(0));
		Signals.Set(SignalKeys.ValveOpeningCommand, parameters.Get(ParameterKeys.ValveOpening));

		// Add intentionally not-in-topological order to demonstrate dependency sorting.
		_system = new SimSystemBuilder()
			.Add(new ActuatorComponent(Parameters, Signals))
			.Add(new PositionControllerComponent(Parameters, Signals))
			.Add(new PressureSensorComponent(Signals))
			.Add(new ValveComponent(Parameters, Signals))
			.Add(new SupplyComponent(Parameters, Signals))
			.Build();
	}

	public ParameterStore Parameters { get; }
	public SignalBus Signals { get; }

	public void Tick(SimTime time, double dtSeconds) => _system.Tick(time, dtSeconds);

	private sealed class PositionControllerComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		private double _integral;
		private bool _wasEnabled;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
		[
			new SignalDependency(SignalKeys.ActuatorPosition.Name, typeof(Position)),
			new SignalDependency(SignalKeys.ActuatorVelocity.Name, typeof(Velocity)),
		];

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
		[
			new SignalDependency(SignalKeys.PositionControlActive.Name, typeof(Ratio)),
			new SignalDependency(SignalKeys.ActuatorPositionCommand.Name, typeof(Position)),
			new SignalDependency(SignalKeys.ActuatorPositionError.Name, typeof(Position)),
			new SignalDependency(SignalKeys.ValveOpeningCommand.Name, typeof(Ratio)),
		];

		public void Tick(SimTime time, double dtSeconds)
		{
			var enabled = parameters.Get(ParameterKeys.PositionControlEnable).Value >= 0.5;
			var targetPosition = parameters.Get(ParameterKeys.ActuatorPositionCommand).Value;
			var actualPosition = signals.Get(SignalKeys.ActuatorPosition).Value;
			var actualVelocity = signals.Get(SignalKeys.ActuatorVelocity).Value;
			var error = targetPosition - actualPosition;
			var currentOpening = Math.Clamp(parameters.Get(ParameterKeys.ValveOpening).Value, 0.0, 1.0);

			signals.Set(SignalKeys.PositionControlActive, Ratio.From(enabled ? 1.0 : 0.0));
			signals.Set(SignalKeys.ActuatorPositionCommand, Position.From(targetPosition));
			signals.Set(SignalKeys.ActuatorPositionError, Position.From(error));

			if (!enabled)
			{
				_integral = 0.0;
				_wasEnabled = false;
				signals.Set(SignalKeys.ValveOpeningCommand, Ratio.From(currentOpening));
				return;
			}

			var kp = Math.Max(0.0, parameters.Get(ParameterKeys.PositionControlKp).Value);
			var ki = Math.Max(0.0, parameters.Get(ParameterKeys.PositionControlKi).Value);
			var kd = Math.Max(0.0, parameters.Get(ParameterKeys.PositionControlKd).Value);
			var supplyPressure = Math.Max(parameters.Get(ParameterKeys.SupplyPressurePsi).Value, 1.0);
			var loadForce = Math.Max(parameters.Get(ParameterKeys.LoadForce).Value, 0.0);
			var biasOpening = Math.Pow(Math.Clamp(loadForce / supplyPressure, 0.0, 1.0), 2.0 / 3.0);

			if (!_wasEnabled)
			{
				_integral = 0.0;
			}

			var integralBefore = _integral;
			if (dtSeconds > 0.0)
			{
				_integral += error * dtSeconds;
			}

			var integralContribution = ClampSigned(ki * _integral, 0.35);
			var rawOpening = biasOpening + (kp * error) + integralContribution - (kd * actualVelocity);
			var commandedOpening = Math.Clamp(rawOpening, 0.0, 1.0);

			if ((rawOpening > 1.0 && error > 0.0) || (rawOpening < 0.0 && error < 0.0))
			{
				_integral = integralBefore;
				integralContribution = ClampSigned(ki * _integral, 0.35);
				rawOpening = biasOpening + (kp * error) + integralContribution - (kd * actualVelocity);
				commandedOpening = Math.Clamp(rawOpening, 0.0, 1.0);
			}

			parameters.Set(ParameterKeys.ValveOpening, Ratio.From(commandedOpening));
			signals.Set(SignalKeys.ValveOpeningCommand, Ratio.From(commandedOpening));
			_wasEnabled = true;
		}

		private static double ClampSigned(double value, double magnitude)
		{
			return Math.Clamp(value, -magnitude, magnitude);
		}
	}

	private sealed class SupplyComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[] { new SignalDependency(SignalKeys.UpstreamPressurePsi.Name, typeof(PressurePsi)) };

		public void Tick(SimTime time, double dtSeconds)
		{
			var p = parameters.Get(ParameterKeys.SupplyPressurePsi);
			signals.Set(SignalKeys.UpstreamPressurePsi, p);
		}
	}

	private sealed class ValveComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[] { new SignalDependency(SignalKeys.UpstreamPressurePsi.Name, typeof(PressurePsi)) };

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.DownstreamPressurePsi.Name, typeof(PressurePsi)),
				new SignalDependency(SignalKeys.ValveFlowGpm.Name, typeof(FlowGpm)),
			};

		public void Tick(SimTime time, double dtSeconds)
		{
			var supply = signals.Get(SignalKeys.UpstreamPressurePsi).Value;
			var opening = parameters.Get(ParameterKeys.ValveOpening).Value;
			var gain = parameters.Get(ParameterKeys.FlowGainGpm).Value;

			var downstream = PressurePsi.From(supply * Math.Pow(opening, 1.5));
			signals.Set(SignalKeys.DownstreamPressurePsi, downstream);

			var flow = FlowGpm.From(gain * opening * (supply / 1000.0));
			signals.Set(SignalKeys.ValveFlowGpm, flow);
		}
	}

	private sealed class PressureSensorComponent(SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[] { new SignalDependency(SignalKeys.DownstreamPressurePsi.Name, typeof(PressurePsi)) };

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[] { new SignalDependency(SignalKeys.DownstreamPressureSensorPsi.Name, typeof(PressurePsi)) };

		public void Tick(SimTime time, double dtSeconds)
		{
			signals.Set(SignalKeys.DownstreamPressureSensorPsi, signals.Get(SignalKeys.DownstreamPressurePsi));
		}
	}

	private sealed class ActuatorComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		private double _position;
		private double _velocity;

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[] { new SignalDependency(SignalKeys.DownstreamPressurePsi.Name, typeof(PressurePsi)) };

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[]
			{
				new SignalDependency(SignalKeys.ActuatorVelocity.Name, typeof(Velocity)),
				new SignalDependency(SignalKeys.ActuatorPosition.Name, typeof(Position)),
			};

		public void Tick(SimTime time, double dtSeconds)
		{
			var pressure = signals.Get(SignalKeys.DownstreamPressurePsi).Value;
			var load = parameters.Get(ParameterKeys.LoadForce).Value;

			const double k = 0.002;
			const double damping = 0.8;

			var accel = (pressure - load) * k - damping * _velocity;
			_velocity += accel * dtSeconds;
			_position += _velocity * dtSeconds;

			signals.Set(SignalKeys.ActuatorVelocity, Velocity.From(_velocity));
			signals.Set(SignalKeys.ActuatorPosition, Position.From(_position));
		}
	}
}
