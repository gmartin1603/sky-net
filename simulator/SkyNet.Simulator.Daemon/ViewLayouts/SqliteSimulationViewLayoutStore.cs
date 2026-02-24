using System.Text.Json;
using Microsoft.Data.Sqlite;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Daemon.ViewLayouts;

public sealed class SqliteSimulationViewLayoutStore : ISimulationViewLayoutStore
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false
	};

	private readonly string _connectionString;

	public SqliteSimulationViewLayoutStore(IHostEnvironment environment)
	{
		var dataDir = Path.Combine(environment.ContentRootPath, "data");
		Directory.CreateDirectory(dataDir);
		var dbPath = Path.Combine(dataDir, "view-layouts.sqlite");
		_connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
		Initialize();
	}

	public async Task<TankTransferSchematicLayout> GetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT tank_transfer_layout_json
			FROM sim_view_layouts
			WHERE sim_id = $simId
			LIMIT 1;
			""";
		command.Parameters.AddWithValue("$simId", simId.Trim());

		var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
		if (string.IsNullOrWhiteSpace(raw))
		{
			return TankTransferSchematicLayout.Default;
		}

		try
		{
			return JsonSerializer.Deserialize<TankTransferSchematicLayout>(raw, JsonOptions) ?? TankTransferSchematicLayout.Default;
		}
		catch
		{
			return TankTransferSchematicLayout.Default;
		}
	}

	public async Task SaveTankTransferLayoutAsync(string simId, TankTransferSchematicLayout layout, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		ArgumentNullException.ThrowIfNull(layout);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = """
			INSERT INTO sim_view_layouts (sim_id, tank_transfer_layout_json, updated_at_utc)
			VALUES ($simId, $layoutJson, $updatedAtUtc)
			ON CONFLICT(sim_id)
			DO UPDATE SET
				tank_transfer_layout_json = excluded.tank_transfer_layout_json,
				updated_at_utc = excluded.updated_at_utc;
			""";
		command.Parameters.AddWithValue("$simId", simId.Trim());
		command.Parameters.AddWithValue("$layoutJson", JsonSerializer.Serialize(layout, JsonOptions));
		command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));

		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task ResetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM sim_view_layouts WHERE sim_id = $simId;";
		command.Parameters.AddWithValue("$simId", simId.Trim());
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private void Initialize()
	{
		using var connection = new SqliteConnection(_connectionString);
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = """
			CREATE TABLE IF NOT EXISTS sim_view_layouts (
				sim_id TEXT PRIMARY KEY,
				tank_transfer_layout_json TEXT NOT NULL,
				updated_at_utc TEXT NOT NULL
			);
			""";
		command.ExecuteNonQuery();
	}
}
