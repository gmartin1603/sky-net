using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Tests;

public sealed class ParameterStoreTests
{
	[Fact]
	public void Set_ClampsToDefinitionRange_AndRaisesWasClamped()
	{
		var parameters = new ParameterStore();
		_ = new DemoHydraulicSystem(parameters);

		ParameterChangedEventArgs? last = null;
		parameters.ParameterChanged += (_, e) => last = e;

		parameters.Set(DemoHydraulicSystem.ParameterKeys.ValveOpening.Name, 2.0);

		var clamped = parameters.Get(DemoHydraulicSystem.ParameterKeys.ValveOpening).Value;
		Assert.Equal(1.0, clamped, precision: 12);
		Assert.NotNull(last);
		Assert.True(last!.WasClamped);
		Assert.Equal(2.0, last.RequestedValue, precision: 12);
		Assert.Equal(1.0, last.NewValue, precision: 12);
	}

	[Fact]
	public void SnapshotDefinitions_IncludesDefinedParameters()
	{
		var parameters = new ParameterStore();
		_ = new DemoHydraulicSystem(parameters);

		var defs = parameters.SnapshotDefinitions();
		Assert.True(defs.ContainsKey(DemoHydraulicSystem.ParameterKeys.SupplyPressurePsi.Name));
		Assert.True(defs.ContainsKey(DemoHydraulicSystem.ParameterKeys.ValveOpening.Name));
		Assert.True(defs.ContainsKey(DemoHydraulicSystem.ParameterKeys.LoadForce.Name));
	}
}
