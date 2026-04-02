using System.Collections.Concurrent;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class SimulationViewLayoutStore(SimApiClient api)
{
	private readonly ConcurrentDictionary<string, TankTransferSchematicLayout> _tankTransferLayouts =
		new(StringComparer.OrdinalIgnoreCase);
	private readonly ConcurrentDictionary<string, GrainDryerSchematicLayout> _grainDryerLayouts =
		new(StringComparer.OrdinalIgnoreCase);

	public async Task<TankTransferSchematicLayout> GetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		var key = string.IsNullOrWhiteSpace(simId) ? "tank-transfer" : simId.Trim();
		if (_tankTransferLayouts.TryGetValue(key, out var cached))
		{
			return cached.Clone();
		}

		var loaded = (await api.GetTankTransferLayoutAsync(key, cancellationToken).ConfigureAwait(false)).Normalize();
		_tankTransferLayouts[key] = loaded.Clone();
		return loaded.Clone();
	}

	public async Task SetTankTransferLayoutAsync(string simId, TankTransferSchematicLayout layout, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(layout);
		var key = string.IsNullOrWhiteSpace(simId) ? "tank-transfer" : simId.Trim();
		layout.Normalize();
		await api.SetTankTransferLayoutAsync(key, layout, cancellationToken).ConfigureAwait(false);
		_tankTransferLayouts[key] = layout.Clone();
	}

	public async Task<GrainDryerSchematicLayout> GetGrainDryerLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		var key = string.IsNullOrWhiteSpace(simId) ? "grain-dryer" : simId.Trim();
		if (_grainDryerLayouts.TryGetValue(key, out var cached))
		{
			return cached.Clone();
		}

		var loaded = (await api.GetGrainDryerLayoutAsync(key, cancellationToken).ConfigureAwait(false)).Normalize();
		_grainDryerLayouts[key] = loaded.Clone();
		return loaded.Clone();
	}

	public async Task SetGrainDryerLayoutAsync(string simId, GrainDryerSchematicLayout layout, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(layout);
		var key = string.IsNullOrWhiteSpace(simId) ? "grain-dryer" : simId.Trim();
		layout.Normalize();
		await api.SetGrainDryerLayoutAsync(key, layout, cancellationToken).ConfigureAwait(false);
		_grainDryerLayouts[key] = layout.Clone();
	}

	public async Task ResetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		var key = string.IsNullOrWhiteSpace(simId) ? "tank-transfer" : simId.Trim();
		await api.ResetTankTransferLayoutAsync(key, cancellationToken).ConfigureAwait(false);
		_tankTransferLayouts[key] = TankTransferSchematicLayout.Default;
	}

	public async Task ResetGrainDryerLayoutAsync(string simId, CancellationToken cancellationToken = default)
	{
		var key = string.IsNullOrWhiteSpace(simId) ? "grain-dryer" : simId.Trim();
		await api.ResetGrainDryerLayoutAsync(key, cancellationToken).ConfigureAwait(false);
		_grainDryerLayouts[key] = GrainDryerSchematicLayout.Default;
	}
}
