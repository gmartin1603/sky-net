using System.Collections.Concurrent;

namespace SkyNet.Simulator.Core.Signals;

public sealed class SignalBus
{
	private readonly ConcurrentDictionary<string, double> _signals = new(StringComparer.OrdinalIgnoreCase);

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
