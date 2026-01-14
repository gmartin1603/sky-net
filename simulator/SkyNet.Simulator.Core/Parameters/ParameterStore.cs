using System.Collections.Concurrent;

namespace SkyNet.Simulator.Core.Parameters;

public sealed class ParameterStore
{
	private readonly ConcurrentDictionary<string, double> _values = new(StringComparer.OrdinalIgnoreCase);

	public event EventHandler<ParameterChangedEventArgs>? ParameterChanged;

	public void Define(string name, double initialValue)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("Parameter name is required.", nameof(name));
		}

		_values.TryAdd(name.Trim(), initialValue);
	}

	public bool Contains(string name) => _values.ContainsKey(name);

	public double Get(string name)
	{
		if (!_values.TryGetValue(name, out var value))
		{
			throw new KeyNotFoundException($"Unknown parameter '{name}'.");
		}

		return value;
	}

	public IReadOnlyDictionary<string, double> Snapshot() => new Dictionary<string, double>(_values, StringComparer.OrdinalIgnoreCase);

	public void Set(string name, double value)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("Parameter name is required.", nameof(name));
		}

		name = name.Trim();

		while (true)
		{
			if (!_values.TryGetValue(name, out var oldValue))
			{
				throw new KeyNotFoundException($"Unknown parameter '{name}'.");
			}

			if (_values.TryUpdate(name, value, oldValue))
			{
				if (!oldValue.Equals(value))
				{
					ParameterChanged?.Invoke(this, new ParameterChangedEventArgs(name, oldValue, value));
				}
				return;
			}
		}
	}
}
