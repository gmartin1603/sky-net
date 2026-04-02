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
		return (await GetLayoutAsync(simId, "tank-transfer", TankTransferSchematicLayout.Default, cancellationToken).ConfigureAwait(false)).Normalize();
	}

	public async Task SaveTankTransferLayoutAsync(string simId, TankTransferSchematicLayout layout, CancellationToken cancellationToken = default)
	{
		layout.Normalize();
		await SaveLayoutAsync(simId, "tank-transfer", layout, cancellationToken).ConfigureAwait(false);
	}

	public async Task ResetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		await ResetLayoutAsync(simId, "tank-transfer", cancellationToken).ConfigureAwait(false);
	}

	public async Task<GrainDryerSchematicLayout> GetGrainDryerLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		return (await GetLayoutAsync(simId, "grain-dryer", GrainDryerSchematicLayout.Default, cancellationToken).ConfigureAwait(false)).Normalize();
	}

	public async Task SaveGrainDryerLayoutAsync(string simId, GrainDryerSchematicLayout layout, CancellationToken cancellationToken = default)
	{
		layout.Normalize();
		await SaveLayoutAsync(simId, "grain-dryer", layout, cancellationToken).ConfigureAwait(false);
	}

	public async Task ResetGrainDryerLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		await ResetLayoutAsync(simId, "grain-dryer", cancellationToken).ConfigureAwait(false);
	}

	public async Task<TrainerPresetDto?> GetTrainerPresetAsync(string simId, string presetName, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		ArgumentException.ThrowIfNullOrWhiteSpace(presetName);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT preset_name, preset_json, updated_at_utc
			FROM sim_trainer_presets
			WHERE sim_id = $simId AND preset_name = $presetName
			LIMIT 1;
			""";
		command.Parameters.AddWithValue("$simId", simId.Trim());
		command.Parameters.AddWithValue("$presetName", presetName.Trim());

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			return null;
		}

		return DeserializePreset(reader.GetString(0), reader.GetString(1), reader.GetString(2));
	}

	public async Task<IReadOnlyList<TrainerPresetDto>> ListTrainerPresetsAsync(string simId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT preset_name, preset_json, updated_at_utc
			FROM sim_trainer_presets
			WHERE sim_id = $simId
			ORDER BY preset_name COLLATE NOCASE;
			""";
		command.Parameters.AddWithValue("$simId", simId.Trim());

		var results = new List<TrainerPresetDto>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var preset = DeserializePreset(reader.GetString(0), reader.GetString(1), reader.GetString(2));
			if (preset is not null)
			{
				results.Add(preset);
			}
		}

		return results;
	}

	public async Task SaveTrainerPresetAsync(string simId, TrainerPresetDto preset, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		ArgumentNullException.ThrowIfNull(preset);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = """
			INSERT INTO sim_trainer_presets (sim_id, preset_name, preset_json, updated_at_utc)
			VALUES ($simId, $presetName, $presetJson, $updatedAtUtc)
			ON CONFLICT(sim_id, preset_name)
			DO UPDATE SET
				preset_json = excluded.preset_json,
				updated_at_utc = excluded.updated_at_utc;
			""";
		command.Parameters.AddWithValue("$simId", simId.Trim());
		command.Parameters.AddWithValue("$presetName", preset.Name.Trim());
		command.Parameters.AddWithValue("$presetJson", JsonSerializer.Serialize(preset.Parameters, JsonOptions));
		command.Parameters.AddWithValue("$updatedAtUtc", preset.UpdatedAtUtc.ToString("O"));

		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task ResetTrainerPresetAsync(string simId, string presetName, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		ArgumentException.ThrowIfNullOrWhiteSpace(presetName);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM sim_trainer_presets WHERE sim_id = $simId AND preset_name = $presetName;";
		command.Parameters.AddWithValue("$simId", simId.Trim());
		command.Parameters.AddWithValue("$presetName", presetName.Trim());
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private void Initialize()
	{
		using var connection = new SqliteConnection(_connectionString);
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = """
			CREATE TABLE IF NOT EXISTS sim_view_layout_entries (
				sim_id TEXT NOT NULL,
				layout_type TEXT NOT NULL,
				layout_json TEXT NOT NULL,
				updated_at_utc TEXT NOT NULL,
				PRIMARY KEY (sim_id, layout_type)
			);

			CREATE TABLE IF NOT EXISTS sim_view_layouts (
				sim_id TEXT PRIMARY KEY,
				tank_transfer_layout_json TEXT NOT NULL,
				updated_at_utc TEXT NOT NULL
			);

			CREATE TABLE IF NOT EXISTS sim_trainer_presets (
				sim_id TEXT NOT NULL,
				preset_name TEXT NOT NULL,
				preset_json TEXT NOT NULL,
				updated_at_utc TEXT NOT NULL,
				PRIMARY KEY (sim_id, preset_name)
			);

			INSERT INTO sim_view_layout_entries (sim_id, layout_type, layout_json, updated_at_utc)
			SELECT sim_id, 'tank-transfer', tank_transfer_layout_json, updated_at_utc
			FROM sim_view_layouts
			WHERE tank_transfer_layout_json IS NOT NULL
			ON CONFLICT(sim_id, layout_type) DO NOTHING;
			""";
		command.ExecuteNonQuery();
	}

	private async Task<TLayout> GetLayoutAsync<TLayout>(string simId, string layoutType, TLayout fallback, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		ArgumentException.ThrowIfNullOrWhiteSpace(layoutType);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT layout_json
			FROM sim_view_layout_entries
			WHERE sim_id = $simId AND layout_type = $layoutType
			LIMIT 1;
			""";
		command.Parameters.AddWithValue("$simId", simId.Trim());
		command.Parameters.AddWithValue("$layoutType", layoutType.Trim());

		var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
		if (string.IsNullOrWhiteSpace(raw))
		{
			return fallback;
		}

		try
		{
			return JsonSerializer.Deserialize<TLayout>(raw, JsonOptions) ?? fallback;
		}
		catch
		{
			return fallback;
		}
	}

	private async Task SaveLayoutAsync<TLayout>(string simId, string layoutType, TLayout layout, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		ArgumentException.ThrowIfNullOrWhiteSpace(layoutType);
		ArgumentNullException.ThrowIfNull(layout);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = """
			INSERT INTO sim_view_layout_entries (sim_id, layout_type, layout_json, updated_at_utc)
			VALUES ($simId, $layoutType, $layoutJson, $updatedAtUtc)
			ON CONFLICT(sim_id, layout_type)
			DO UPDATE SET
				layout_json = excluded.layout_json,
				updated_at_utc = excluded.updated_at_utc;
			""";
		command.Parameters.AddWithValue("$simId", simId.Trim());
		command.Parameters.AddWithValue("$layoutType", layoutType.Trim());
		command.Parameters.AddWithValue("$layoutJson", JsonSerializer.Serialize(layout, JsonOptions));
		command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));

		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task ResetLayoutAsync(string simId, string layoutType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		ArgumentException.ThrowIfNullOrWhiteSpace(layoutType);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM sim_view_layout_entries WHERE sim_id = $simId AND layout_type = $layoutType;";
		command.Parameters.AddWithValue("$simId", simId.Trim());
		command.Parameters.AddWithValue("$layoutType", layoutType.Trim());
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static TrainerPresetDto? DeserializePreset(string presetName, string rawJson, string rawUpdatedAtUtc)
	{
		try
		{
			var parameters = JsonSerializer.Deserialize<Dictionary<string, double>>(rawJson, JsonOptions)
				?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			var updatedAtUtc = DateTimeOffset.TryParse(rawUpdatedAtUtc, out var parsed)
				? parsed
				: DateTimeOffset.UtcNow;
			return new TrainerPresetDto(presetName, updatedAtUtc, parameters);
		}
		catch
		{
			return null;
		}
	}
}
