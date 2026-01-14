using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Tests;

public class UnitTest1
{
    [Fact]
    public void StepOnce_IsDeterministic_ForSameInitialState()
    {
        static (double pos, double vel, double pressure) Run(int steps)
        {
            var parameters = new ParameterStore();
            var system = new DemoHydraulicSystem(parameters);
            var runner = new SimulationRunner(system);

            for (var i = 0; i < steps; i++)
            {
                runner.StepOnce();
            }

            var signals = system.Signals.Snapshot();
            return (
                signals["ActuatorPosition"],
                signals["ActuatorVelocity"],
                signals["DownstreamPressurePsi"]);
        }

        var a = Run(300);
        var b = Run(300);

        Assert.Equal(a.pressure, b.pressure, precision: 10);
        Assert.Equal(a.vel, b.vel, precision: 10);
        Assert.Equal(a.pos, b.pos, precision: 10);
    }

    [Fact]
    public void ChangingParameter_PropagatesToOutputs_NextTick()
    {
        var parameters = new ParameterStore();
        var system = new DemoHydraulicSystem(parameters);
        var runner = new SimulationRunner(system);

        parameters.Set("SupplyPressurePsi", 2000);
        parameters.Set("ValveOpening", 0.2);
        runner.StepOnce();
        var p1 = system.Signals.Get("DownstreamPressurePsi");

        parameters.Set("ValveOpening", 0.8);
        runner.StepOnce();
        var p2 = system.Signals.Get("DownstreamPressurePsi");

        Assert.True(p2 > p1);
    }
}
