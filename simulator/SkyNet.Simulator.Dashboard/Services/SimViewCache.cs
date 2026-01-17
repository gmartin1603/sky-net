using System.Collections.Concurrent;
using SkyNet.Simulator.Contracts;
using SkyNet.Simulator.Dashboard.Components;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class SimViewCache
{
	private readonly ConcurrentDictionary<string, SimViewState> _states =
		new(StringComparer.OrdinalIgnoreCase);

	public SimViewState GetOrCreate(string simId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		return _states.GetOrAdd(simId.Trim(), _ => new SimViewState());
	}

	public bool TryGet(string simId, out SimViewState state)
	{
		if (string.IsNullOrWhiteSpace(simId))
		{
			state = null!;
			return false;
		}

		return _states.TryGetValue(simId.Trim(), out state!);
	}
}

public sealed class SimViewState
{
	private readonly object _lock = new();
	private readonly Dictionary<string, double> _parameterEdits = new(StringComparer.OrdinalIgnoreCase);

	public TelemetrySnapshot? LatestSnapshot { get; private set; }
	public LineChart.Point[] PressureSeries { get; private set; } = Array.Empty<LineChart.Point>();
	public LineChart.Point[] PositionSeries { get; private set; } = Array.Empty<LineChart.Point>();
	public LineChart.Point[] FlowSeries { get; private set; } = Array.Empty<LineChart.Point>();
	public KeyValuePair<string, double>[] SortedSignals { get; private set; } = Array.Empty<KeyValuePair<string, double>>();
	public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.MinValue;
	public DateTimeOffset ParametersUpdatedAt { get; private set; } = DateTimeOffset.MinValue;

	public void SetSnapshot(TelemetrySnapshot snapshot)
	{
		lock (_lock)
		{
			LatestSnapshot = snapshot;
			UpdatedAt = DateTimeOffset.UtcNow;
		}
	}

	public void MergeParameterEditsFromValues(IReadOnlyDictionary<string, double> values)
	{
		ArgumentNullException.ThrowIfNull(values);
		lock (_lock)
		{
			foreach (var kvp in values)
			{
				// Only initialize missing entries; preserve user-entered drafts.
				if (!_parameterEdits.ContainsKey(kvp.Key))
				{
					_parameterEdits[kvp.Key] = kvp.Value;
				}
			}
			ParametersUpdatedAt = DateTimeOffset.UtcNow;
		}
	}

	public void RemoveUnknownParameterEdits(IReadOnlyCollection<string> allowedNames)
	{
		ArgumentNullException.ThrowIfNull(allowedNames);
		lock (_lock)
		{
			var allow = new HashSet<string>(allowedNames, StringComparer.OrdinalIgnoreCase);
			var remove = _parameterEdits.Keys.Where(k => !allow.Contains(k)).ToArray();
			foreach (var k in remove)
			{
				_parameterEdits.Remove(k);
			}
		}
	}

	public void SetParameterEdit(string name, double value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		lock (_lock)
		{
			_parameterEdits[name] = value;
			ParametersUpdatedAt = DateTimeOffset.UtcNow;
		}
	}

	public Dictionary<string, double> ParameterEditsSnapshot()
	{
		lock (_lock)
		{
			return new Dictionary<string, double>(_parameterEdits, StringComparer.OrdinalIgnoreCase);
		}
	}

	public void SetRenderedState(
		TelemetrySnapshot snapshot,
		LineChart.Point[] pressureSeries,
		LineChart.Point[] positionSeries,
		LineChart.Point[] flowSeries,
		KeyValuePair<string, double>[] sortedSignals)
	{
		lock (_lock)
		{
			LatestSnapshot = snapshot;
			PressureSeries = pressureSeries;
			PositionSeries = positionSeries;
			FlowSeries = flowSeries;
			SortedSignals = sortedSignals;
			UpdatedAt = DateTimeOffset.UtcNow;
		}
	}

	public bool TrySnapshot(out TelemetrySnapshot? snapshot)
	{
		lock (_lock)
		{
			snapshot = LatestSnapshot;
			return snapshot is not null;
		}
	}

	public SimViewStateSnapshot Snapshot()
	{
		lock (_lock)
		{
			return new SimViewStateSnapshot(
				LatestSnapshot,
				PressureSeries,
				PositionSeries,
				FlowSeries,
				SortedSignals,
				UpdatedAt,
				new Dictionary<string, double>(_parameterEdits, StringComparer.OrdinalIgnoreCase),
				ParametersUpdatedAt);
		}
	}
}

public sealed record SimViewStateSnapshot(
	TelemetrySnapshot? LatestSnapshot,
	LineChart.Point[] PressureSeries,
	LineChart.Point[] PositionSeries,
	LineChart.Point[] FlowSeries,
	KeyValuePair<string, double>[] SortedSignals,
	DateTimeOffset UpdatedAt,
	Dictionary<string, double> ParameterEdits,
	DateTimeOffset ParametersUpdatedAt);
