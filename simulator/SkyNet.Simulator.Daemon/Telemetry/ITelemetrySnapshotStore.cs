using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Daemon.Telemetry;

public interface ITelemetrySnapshotStore
{
	Task AppendAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default);

	Task<TelemetrySnapshot?> GetLatestAsync(string simId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<TelemetrySnapshot>> GetRecentAsync(string simId, int take, CancellationToken cancellationToken = default);

	Task<TelemetryStoreStats> GetStatsAsync(CancellationToken cancellationToken = default);
}
