using SkyNet.Simulator.Core.Components;
using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Signals;
using SkyNet.Simulator.Core.Simulation;

namespace SkyNet.Simulator.Core.Systems;

/// <summary>
/// Minimal, training-oriented demo system.
/// Not a physically accurate hydraulic model — just enough structure to prove:
/// (1) fixed-step ticking and (2) runtime parameter changes propagating to outputs.
/// </summary>
public sealed class DemoHydraulicSystem
{
	private readonly List<ISimComponent> _components = new();

	public DemoHydraulicSystem(ParameterStore parameters)
	{
		Parameters = parameters;
		Signals = new SignalBus();

		Parameters.Define("SupplyPressurePsi", 2000);
		Parameters.Define("ValveOpening", 0.2); // 0..1
		Parameters.Define("LoadForce", 800); // arbitrary units

		Signals.Set("DownstreamPressurePsi", 0);
		Signals.Set("ActuatorPosition", 0);
		Signals.Set("ActuatorVelocity", 0);

		_components.Add(new PressureDropComponent(Parameters, Signals));
		_components.Add(new ActuatorComponent(Parameters, Signals));
	}

	public ParameterStore Parameters { get; }
	public SignalBus Signals { get; }

	public void Tick(SimTime time, double dtSeconds)
	{
		foreach (var component in _components)
		{
			component.Tick(time, dtSeconds);
		}
	}

	private sealed class PressureDropComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		public void Tick(SimTime time, double dtSeconds)
		{
			var supply = parameters.Get("SupplyPressurePsi");
			var opening = Clamp01(parameters.Get("ValveOpening"));

			// Simple non-linear mapping: near closed -> almost no pressure downstream.
			var downstream = supply * Math.Pow(opening, 1.5);
			signals.Set("DownstreamPressurePsi", downstream);
		}

		private static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
	}

	private sealed class ActuatorComponent(ParameterStore parameters, SignalBus signals) : ISimComponent
	{
		private double _position;
		private double _velocity;

		public void Tick(SimTime time, double dtSeconds)
		{
			var pressure = signals.Get("DownstreamPressurePsi");
			var load = parameters.Get("LoadForce");

			// Toy dynamics: acceleration proportional to (pressure - load)
			// plus damping. Position integrates velocity.
			const double k = 0.002;
			const double damping = 0.8;

			var accel = (pressure - load) * k - damping * _velocity;
			_velocity += accel * dtSeconds;
			_position += _velocity * dtSeconds;

			signals.Set("ActuatorVelocity", _velocity);
			signals.Set("ActuatorPosition", _position);
		}
	}
}
