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
	}

	public static class SignalKeys
	{
		public static readonly SignalKey<PressurePsi> UpstreamPressurePsi = new("UpstreamPressurePsi");
		public static readonly SignalKey<PressurePsi> DownstreamPressurePsi = new("DownstreamPressurePsi");
		public static readonly SignalKey<PressurePsi> DownstreamPressureSensorPsi = new("DownstreamPressureSensorPsi");
		public static readonly SignalKey<FlowGpm> ValveFlowGpm = new("ValveFlowGpm");
		public static readonly SignalKey<Position> ActuatorPosition = new("ActuatorPosition");
		public static readonly SignalKey<Velocity> ActuatorVelocity = new("ActuatorVelocity");
	}

	public HydraulicTrainingSystem(ParameterStore parameters)
	{
		Parameters = parameters;
		Signals = new SignalBus();

		Parameters.Define(ParameterKeys.SupplyPressurePsi, PressurePsi.From(2000), minValue: PressurePsi.From(0), description: "Upstream supply pressure.");
		Parameters.Define(ParameterKeys.ValveOpening, Ratio.From(0.2), minValue: Ratio.From(0), maxValue: Ratio.From(1), description: "Valve opening ratio (0..1).");
		Parameters.Define(ParameterKeys.LoadForce, Ratio.From(800), minValue: Ratio.From(0), description: "Training-load value (unitless).");
		Parameters.Define(ParameterKeys.FlowGainGpm, FlowGpm.From(20), minValue: FlowGpm.From(0), description: "Flow gain (gpm at opening=1 and 1000 psi).");

		Signals.Set(SignalKeys.UpstreamPressurePsi, PressurePsi.From(0));
		Signals.Set(SignalKeys.DownstreamPressurePsi, PressurePsi.From(0));
		Signals.Set(SignalKeys.DownstreamPressureSensorPsi, PressurePsi.From(0));
		Signals.Set(SignalKeys.ValveFlowGpm, FlowGpm.From(0));
		Signals.Set(SignalKeys.ActuatorPosition, Position.From(0));
		Signals.Set(SignalKeys.ActuatorVelocity, Velocity.From(0));

		// Add intentionally not-in-topological order to demonstrate dependency sorting.
		_system = new SimSystemBuilder()
			.Add(new ActuatorComponent(Parameters, Signals))
			.Add(new PressureSensorComponent(Signals))
			.Add(new ValveComponent(Parameters, Signals))
			.Add(new SupplyComponent(Parameters, Signals))
			.Build();
	}

	public ParameterStore Parameters { get; }
	public SignalBus Signals { get; }

	public void Tick(SimTime time, double dtSeconds) => _system.Tick(time, dtSeconds);

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
