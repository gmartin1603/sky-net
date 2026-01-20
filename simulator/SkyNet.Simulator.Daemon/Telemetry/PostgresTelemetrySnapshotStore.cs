using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Daemon.Telemetry;

public sealed class PostgresTelemetrySnapshotStore : ITelemetrySnapshotStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly NpgsqlDataSource _dataSource;
	private readonly TelemetryStoreOptions _options;
	private readonly ILogger<PostgresTelemetrySnapshotStore> _logger;

	private DateTimeOffset _nextMaintenanceUtc = DateTimeOffset.MinValue;
	private DateTimeOffset _nextWarnUtc = DateTimeOffset.MinValue;
	private int _maintenanceRunning;

	public PostgresTelemetrySnapshotStore(
		NpgsqlDataSource dataSource,
		IOptions<TelemetryStoreOptions> options,
		ILogger<PostgresTelemetrySnapshotStore> logger)
	{
		_dataSource = dataSource;
		_options = options.Value;
		_logger = logger;
	}

	public async Task AppendAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.SimId);

		await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
INSERT INTO telemetry_snapshots(sim_id, tick, time_seconds, schema_version, snapshot)
VALUES (@sim_id, @tick, @time_seconds, @schema_version, @snapshot::jsonb)
ON CONFLICT (sim_id, tick) DO NOTHING;";

		cmd.Parameters.AddWithValue("sim_id", snapshot.SimId);
		cmd.Parameters.AddWithValue("tick", snapshot.Tick);
		cmd.Parameters.AddWithValue("time_seconds", snapshot.TimeSeconds);
		cmd.Parameters.AddWithValue("schema_version", snapshot.SchemaVersion);
		cmd.Parameters.Add(new NpgsqlParameter("snapshot", NpgsqlDbType.Jsonb)
		{
			Value = JsonSerializer.Serialize(snapshot, JsonOptions)
		});

		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await MaybeMaintainAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<TelemetrySnapshot?> GetLatestAsync(string simId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);

		await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
SELECT snapshot
FROM telemetry_snapshots
WHERE sim_id = @sim_id
ORDER BY tick DESC
LIMIT 1;";
		cmd.Parameters.AddWithValue("sim_id", simId);

		var obj = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (obj is null || obj is DBNull)
		{
			return null;
		}

		var json = (string)obj;
		return JsonSerializer.Deserialize<TelemetrySnapshot>(json, JsonOptions);
	}

	public async Task<IReadOnlyList<TelemetrySnapshot>> GetRecentAsync(string simId, int take, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		take = Math.Clamp(take, 1, 5_000);

		await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
SELECT snapshot
FROM telemetry_snapshots
WHERE sim_id = @sim_id
ORDER BY tick DESC
LIMIT @take;";
		cmd.Parameters.AddWithValue("sim_id", simId);
		cmd.Parameters.AddWithValue("take", take);

		var results = new List<TelemetrySnapshot>(take);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var json = reader.GetString(0);
			var snapshot = JsonSerializer.Deserialize<TelemetrySnapshot>(json, JsonOptions);
			if (snapshot is not null)
			{
				results.Add(snapshot);
			}
		}

		return results;
	}

	public async Task<TelemetryStoreStats> GetStatsAsync(CancellationToken cancellationToken = default)
	{
		await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT COUNT(*)::bigint FROM telemetry_snapshots;";
		var total = (long)(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
		return new TelemetryStoreStats(total, _options.MaxRowsTotal);
	}

	private async Task MaybeMaintainAsync(CancellationToken cancellationToken)
	{
		if (!_options.Enabled)
		{
			return;
		}

		var now = DateTimeOffset.UtcNow;
		if (now < _nextMaintenanceUtc)
		{
			return;
		}

		if (Interlocked.Exchange(ref _maintenanceRunning, 1) == 1)
		{
			return;
		}

		try
		{
			_nextMaintenanceUtc = now.AddSeconds(Math.Max(1, _options.MaintenanceIntervalSeconds));

			var stats = await GetStatsAsync(cancellationToken).ConfigureAwait(false);
			await MaybeWarnAsync(stats, now).ConfigureAwait(false);
			await PruneIfNeededAsync(stats, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Telemetry maintenance failed.");
		}
		finally
		{
			Interlocked.Exchange(ref _maintenanceRunning, 0);
		}
	}

	private Task MaybeWarnAsync(TelemetryStoreStats stats, DateTimeOffset now)
	{
		if (stats.MaxSnapshots <= 0)
		{
			return Task.CompletedTask;
		}

		var warnAt = (long)Math.Floor(stats.MaxSnapshots * _options.WarnAtFraction);
		if (stats.TotalSnapshots < warnAt)
		{
			return Task.CompletedTask;
		}

		if (now < _nextWarnUtc)
		{
			return Task.CompletedTask;
		}

		_nextWarnUtc = now.AddMinutes(5);
		_logger.LogWarning(
			"Telemetry DB nearing cap: {Total}/{Max} snapshots stored.",
			stats.TotalSnapshots,
			stats.MaxSnapshots);
		return Task.CompletedTask;
	}

	private async Task PruneIfNeededAsync(TelemetryStoreStats stats, CancellationToken cancellationToken)
	{
		if (stats.MaxSnapshots <= 0)
		{
			return;
		}

		if (stats.TotalSnapshots <= stats.MaxSnapshots)
		{
			return;
		}

		var over = stats.TotalSnapshots - stats.MaxSnapshots;
		var toDelete = (int)Math.Min(over, _options.PruneBatchSize);
		if (toDelete <= 0)
		{
			return;
		}

		await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
WITH doomed AS (
	SELECT sim_id, tick
	FROM telemetry_snapshots
	ORDER BY created_at ASC
	LIMIT @take
)
DELETE FROM telemetry_snapshots t
USING doomed d
WHERE t.sim_id = d.sim_id AND t.tick = d.tick;";
		cmd.Parameters.AddWithValue("take", toDelete);

		var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		_logger.LogInformation(
			"Pruned {Deleted} telemetry snapshots (cap {Max}).",
			deleted,
			stats.MaxSnapshots);
	}
}
