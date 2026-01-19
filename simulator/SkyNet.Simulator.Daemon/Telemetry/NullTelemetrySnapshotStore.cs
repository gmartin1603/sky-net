using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Daemon.Telemetry;

public sealed class NullTelemetrySnapshotStore : ITelemetrySnapshotStore
{
	public Task AppendAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task<TelemetrySnapshot?> GetLatestAsync(string simId, CancellationToken cancellationToken = default) =>
		Task.FromResult<TelemetrySnapshot?>(null);

	public Task<IReadOnlyList<TelemetrySnapshot>> GetRecentAsync(string simId, int take, CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<TelemetrySnapshot>>(Array.Empty<TelemetrySnapshot>());

	public Task<TelemetryStoreStats> GetStatsAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(new TelemetryStoreStats(TotalSnapshots: 0, MaxSnapshots: 0));
}
