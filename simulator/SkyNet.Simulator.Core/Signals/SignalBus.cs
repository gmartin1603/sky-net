using System.Collections.Concurrent;
using SkyNet.Simulator.Core.Units;

namespace SkyNet.Simulator.Core.Signals;

public sealed class SignalBus
{
	private readonly ConcurrentDictionary<string, double> _signals = new(StringComparer.OrdinalIgnoreCase);

	public void Set<TUnit>(SignalKey<TUnit> key, TUnit value) where TUnit : struct, IUnit<TUnit>
	{
		Set(key.Name, value.Value);
	}

	public TUnit Get<TUnit>(SignalKey<TUnit> key) where TUnit : struct, IUnit<TUnit>
	{
		var value = Get(key.Name);
		return TUnit.From(value);
	}

	public bool TryGet<TUnit>(SignalKey<TUnit> key, out TUnit value) where TUnit : struct, IUnit<TUnit>
	{
		if (TryGet(key.Name, out var raw))
		{
			value = TUnit.From(raw);
			return true;
		}

		value = default;
		return false;
	}

	public void Set(string name, double value)
	{
		if (string.IsNullOrWhiteSpace(name))	
		{
			throw new ArgumentException("Signal name is required.", nameof(name));
		}

		_signals[name.Trim()] = value;
	}

	public double Get(string name)
	{
		if (!_signals.TryGetValue(name, out var value))
		{
			throw new KeyNotFoundException($"Unknown signal '{name}'.");
		}

		return value;
	}

	public bool TryGet(string name, out double value) => _signals.TryGetValue(name, out value);

	public IReadOnlyDictionary<string, double> Snapshot() => new Dictionary<string, double>(_signals, StringComparer.OrdinalIgnoreCase);
}
