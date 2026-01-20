using Microsoft.Extensions.Options;
using Npgsql;

namespace SkyNet.Simulator.Daemon.Telemetry;

public sealed class PostgresTelemetrySchemaInitializer : IHostedService
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly TelemetryStoreOptions _options;
	private readonly ILogger<PostgresTelemetrySchemaInitializer> _logger;

	public PostgresTelemetrySchemaInitializer(
		NpgsqlDataSource dataSource,
		IOptions<TelemetryStoreOptions> options,
		ILogger<PostgresTelemetrySchemaInitializer> logger)
	{
		_dataSource = dataSource;
		_options = options.Value;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_options.Enabled)
		{
			_logger.LogInformation("Telemetry store disabled; skipping DB schema init.");
			return;
		}

		const int maxAttempts = 15;
		var delay = TimeSpan.FromSeconds(1);

		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogInformation("Telemetry DB schema ready.");
				return;
			}
			catch (Exception ex) when (attempt < maxAttempts)
			{
				_logger.LogWarning(ex, "Telemetry DB schema init attempt {Attempt}/{Max} failed; retrying in {Delay}.", attempt, maxAttempts, delay);
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 10));
			}
		}

		// Final attempt (let exception bubble if it fails)
		await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
	{
		await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS telemetry_snapshots(
	sim_id TEXT NOT NULL,
	tick BIGINT NOT NULL,
	time_seconds DOUBLE PRECISION NOT NULL,
	schema_version INTEGER NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
	snapshot JSONB NOT NULL,
	PRIMARY KEY(sim_id, tick)
);

CREATE INDEX IF NOT EXISTS telemetry_snapshots_created_at_idx
	ON telemetry_snapshots(created_at);

CREATE INDEX IF NOT EXISTS telemetry_snapshots_sim_tick_desc_idx
	ON telemetry_snapshots(sim_id, tick DESC);
";

		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
