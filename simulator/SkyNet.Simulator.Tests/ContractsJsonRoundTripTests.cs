using System.Text.Json;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Tests;

public sealed class ContractsJsonRoundTripTests
{
	[Fact]
	public void TelemetrySnapshot_RoundTrips_Json()
	{
		var snapshot = new TelemetrySnapshot(
			SchemaVersion: 1,
			Tick: 123,
			TimeSeconds: 2.05,
			Parameters: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
			{
				["ValveOpening"] = 0.25,
				["SupplyPressurePsi"] = 2000,
			},
			Signals: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
			{
				["DownstreamPressurePsi"] = 800,
				["ActuatorPosition"] = 0.0123,
			});

		var json = JsonSerializer.Serialize(snapshot);
		var parsed = JsonSerializer.Deserialize<TelemetrySnapshot>(json);

		Assert.NotNull(parsed);
		Assert.Equal(snapshot.SchemaVersion, parsed!.SchemaVersion);
		Assert.Equal(snapshot.Tick, parsed.Tick);
		Assert.Equal(snapshot.TimeSeconds, parsed.TimeSeconds);
		Assert.Equal(snapshot.Parameters["ValveOpening"], parsed.Parameters["ValveOpening"]);
		Assert.Equal(snapshot.Signals["DownstreamPressurePsi"], parsed.Signals["DownstreamPressurePsi"]);
	}
}
