using SkyNet.Simulator.Contracts;
using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

namespace SkyNet.Simulator.Daemon;

public sealed class SimulationRegistry
{
	private readonly Dictionary<string, SimulationSlot> _sims;
	private readonly object _lock = new();
	private string _activeId;

	public SimulationRegistry(IEnumerable<SimulationSlot> sims, string defaultActiveId)
	{
		ArgumentNullException.ThrowIfNull(sims);
		_sims = sims.ToDictionary(s => s.Info.Id, StringComparer.OrdinalIgnoreCase);
		if (_sims.Count == 0)
		{
			throw new InvalidOperationException("No simulations registered.");
		}

		if (!_sims.ContainsKey(defaultActiveId))
		{
			defaultActiveId = _sims.Keys.First();
		}

		_activeId = defaultActiveId;
	}

	public IReadOnlyList<SimulationInfoDto> List() =>
		_sims.Values
			.Select(s => s.Info)
			.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
			.ToArray();

	public string ActiveId
	{
		get
		{
			lock (_lock) return _activeId;
		}
	}

	public bool TryGet(string simId, out SimulationSlot slot) => _sims.TryGetValue(simId, out slot!);

	public SimulationSlot GetActive()
	{
		lock (_lock)
		{
			return _sims[_activeId];
		}
	}

	public bool TrySetActive(string simId)
	{
		if (!_sims.ContainsKey(simId))
		{
			return false;
		}

		lock (_lock)
		{
			_activeId = simId;
			return true;
		}
	}
}

public sealed record SimulationSlot(
	SimulationInfoDto Info,
	ISimSystem System,
	SimulationRunner Runner,
	ParameterStore Parameters,
	InMemoryLogStore Logs);
