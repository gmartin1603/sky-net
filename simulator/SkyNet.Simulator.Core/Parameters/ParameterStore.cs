using System.Collections.Concurrent;
using SkyNet.Simulator.Core.Units;

namespace SkyNet.Simulator.Core.Parameters;

public sealed class ParameterStore
{
	private readonly ConcurrentDictionary<string, double> _values = new(StringComparer.OrdinalIgnoreCase);
	private readonly ConcurrentDictionary<string, ParameterDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

	public event EventHandler<ParameterChangedEventArgs>? ParameterChanged;

	public void Define<TUnit>(ParameterKey<TUnit> key, TUnit defaultValue, TUnit? minValue = null, TUnit? maxValue = null, string? description = null)
		where TUnit : struct, IUnit<TUnit>
	{
		Define(new ParameterDefinition(
			Name: key.Name,
			UnitType: typeof(TUnit),
			DefaultValue: defaultValue.Value,
			MinValue: minValue?.Value,
			MaxValue: maxValue?.Value,
			UnitLabel: null,
			Description: description));
	}

	public void Define(ParameterDefinition definition)
	{
		ArgumentNullException.ThrowIfNull(definition);
		definition = definition with { Name = definition.Name.Trim() };
		definition.Validate();

		_definitions[definition.Name] = definition;
		_values.TryAdd(definition.Name, definition.DefaultValue);
	}

	public void Define(string name, double initialValue)
	{
		Define(new ParameterDefinition(
			Name: name,
			UnitType: typeof(double),
			DefaultValue: initialValue));
	}

	public bool Contains(string name) => _values.ContainsKey(name);

	public bool TryGetDefinition(string name, out ParameterDefinition definition)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			definition = null!;
			return false;
		}

		return _definitions.TryGetValue(name.Trim(), out definition!);
	}

	public IReadOnlyDictionary<string, ParameterDefinition> SnapshotDefinitions() =>
		new Dictionary<string, ParameterDefinition>(_definitions, StringComparer.OrdinalIgnoreCase);

	public double Get(string name)
	{
		if (!_values.TryGetValue(name, out var value))
		{
			throw new KeyNotFoundException($"Unknown parameter '{name}'.");
		}

		return value;
	}

	public TUnit Get<TUnit>(ParameterKey<TUnit> key) where TUnit : struct, IUnit<TUnit>
	{
		var raw = Get(key.Name);
		return TUnit.From(raw);
	}

	public IReadOnlyDictionary<string, double> Snapshot() => new Dictionary<string, double>(_values, StringComparer.OrdinalIgnoreCase);

	public void Set(string name, double value)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("Parameter name is required.", nameof(name));
		}

		name = name.Trim();
		var requestedValue = value;
		var wasClamped = false;

		if (_definitions.TryGetValue(name, out var definition))
		{
			(definition, value, wasClamped) = ApplyValidation(definition, value);
		}

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
					ParameterChanged?.Invoke(this, new ParameterChangedEventArgs(name, oldValue, requestedValue, value, wasClamped));
				}
				return;
			}
		}
	}

	public void Set<TUnit>(ParameterKey<TUnit> key, TUnit value) where TUnit : struct, IUnit<TUnit>
	{
		Set(key.Name, value.Value);
	}

	private static (ParameterDefinition definition, double value, bool wasClamped) ApplyValidation(ParameterDefinition definition, double value)
	{
		var wasClamped = false;
		if (definition.MinValue is { } min && value < min)
		{
			value = min;
			wasClamped = true;
		}

		if (definition.MaxValue is { } max && value > max)
		{
			value = max;
			wasClamped = true;
		}

		return (definition, value, wasClamped);
	}
}
