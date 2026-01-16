using SkyNet.Simulator.Core.Components;
using SkyNet.Simulator.Core.Signals;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;
using SkyNet.Simulator.Core.Units;

namespace SkyNet.Simulator.Tests;

public sealed class SimSystemBuilderTests
{
	[Fact]
	public void Build_TopologicallySorts_WriterBeforeReader()
	{
		var signals = new SignalBus();
		var a = new Writer(signals);
		var b = new Reader(signals);

		// Add in the wrong order; builder should sort it.
		var system = new SimSystemBuilder()
			.Add(b)
			.Add(a)
			.Build();

		system.Tick(SimTime.Zero, dtSeconds: 1.0 / 60.0);

		Assert.Equal(20.0, signals.Get(Reader.Out).Value, precision: 12);
	}

	[Fact]
	public void Build_Throws_WhenMultipleWritersExist()
	{
		var signals = new SignalBus();
		var a = new Writer(signals);
		var b = new Writer(signals);

		Assert.Throws<InvalidOperationException>(() =>
			new SimSystemBuilder().Add(a).Add(b).Build());
	}

	[Fact]
	public void Build_Throws_OnUnitMismatch()
	{
		var signals = new SignalBus();
		var writer = new Writer(signals);
		var badReader = new BadUnitReader(signals);

		Assert.Throws<InvalidOperationException>(() =>
			new SimSystemBuilder().Add(writer).Add(badReader).Build());
	}

	private sealed class Writer(SignalBus signals) : ISimComponent
	{
		public static readonly SignalKey<PressurePsi> Out = new("A");

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[] { new SignalDependency(Out.Name, typeof(PressurePsi)) };

		public void Tick(SimTime time, double dtSeconds)	=>
			signals.Set(Out, PressurePsi.From(10));
	}

	private sealed class Reader(SignalBus signals) : ISimComponent
	{
		public static readonly SignalKey<PressurePsi> In = new("A");
		public static readonly SignalKey<PressurePsi> Out = new("B");

		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[] { new SignalDependency(In.Name, typeof(PressurePsi)) };

		public IReadOnlyCollection<SignalDependency> Writes { get; } =
			new[] { new SignalDependency(Out.Name, typeof(PressurePsi)) };

		public void Tick(SimTime time, double dtSeconds)
		{
			var a = signals.Get(In).Value;
			signals.Set(Out, PressurePsi.From(a * 2));
		}
	}

	private sealed class BadUnitReader(SignalBus signals) : ISimComponent
	{
		public IReadOnlyCollection<SignalDependency> Reads { get; } =
			new[] { new SignalDependency(Writer.Out.Name, typeof(FlowGpm)) };

		public void Tick(SimTime time, double dtSeconds)	=>
			_ = signals.Get(Writer.Out.Name);
	}
}
