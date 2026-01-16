using SkyNet.Simulator.Core.Components;

namespace SkyNet.Simulator.Core.Systems;

public sealed class SimSystemBuilder
{
	private readonly List<ISimComponent> _components = new();

	public SimSystemBuilder Add(ISimComponent component)
	{
		ArgumentNullException.ThrowIfNull(component);
		_components.Add(component);
		return this;
	}

	public SimSystem Build()
	{
		if (_components.Count == 0)
		{
			return new SimSystem(Array.Empty<ISimComponent>());
		}

		var writersBySignal = new Dictionary<string, (Type unitType, ISimComponent component)>(StringComparer.OrdinalIgnoreCase);
		foreach (var component in _components)
		{
			foreach (var write in component.Writes)
			{
				if (string.IsNullOrWhiteSpace(write.Name))
				{
					throw new InvalidOperationException($"Component '{component.Name}' declares a write with an empty signal name.");
				}

				if (writersBySignal.TryGetValue(write.Name, out var existing))
				{
					throw new InvalidOperationException(
						$"Signal '{write.Name}' has multiple writers: '{existing.component.Name}' and '{component.Name}'.");
				}

				writersBySignal[write.Name] = (write.UnitType, component);
			}
		}

		// Build dependency edges: writer -> reader
		var outgoing = new Dictionary<ISimComponent, HashSet<ISimComponent>>();
		var indegree = new Dictionary<ISimComponent, int>();
		foreach (var component in _components)
		{
			outgoing[component] = new HashSet<ISimComponent>();
			indegree[component] = 0;
		}

		foreach (var reader in _components)
		{
			foreach (var read in reader.Reads)
			{
				if (!writersBySignal.TryGetValue(read.Name, out var writerInfo))
				{
					// External inputs (e.g., parameters or initial conditions) are allowed.
					continue;
				}

				if (writerInfo.unitType != read.UnitType)
				{
					throw new InvalidOperationException(
						$"Signal '{read.Name}' unit mismatch: writer '{writerInfo.component.Name}' uses {writerInfo.unitType.Name} but reader '{reader.Name}' expects {read.UnitType.Name}.");
				}

				var writer = writerInfo.component;
				if (ReferenceEquals(writer, reader))
				{
					continue;
				}

				if (outgoing[writer].Add(reader))
				{
					indegree[reader]++;
				}
			}
		}

		// Kahn's algorithm with stable ordering (preserve add order when possible)
		var orderIndex = _components
			.Select((c, idx) => (c, idx))
			.ToDictionary(x => x.c, x => x.idx);

		var ready = new List<ISimComponent>(indegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
		ready.Sort((a, b) => orderIndex[a].CompareTo(orderIndex[b]));

		var sorted = new List<ISimComponent>(_components.Count);
		while (ready.Count > 0)
		{
			var node = ready[0];
			ready.RemoveAt(0);
			sorted.Add(node);

			foreach (var dep in outgoing[node])
			{
				indegree[dep]--;
				if (indegree[dep] == 0)
				{
					ready.Add(dep);
				}
			}

			ready.Sort((a, b) => orderIndex[a].CompareTo(orderIndex[b]));
		}

		if (sorted.Count != _components.Count)
		{
			var cyclic = indegree.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key.Name);
			throw new InvalidOperationException(
				$"Dependency cycle detected among components: {string.Join(", ", cyclic)}");
		}

		return new SimSystem(sorted);
	}
}
