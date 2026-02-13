using System.Globalization;
using System.Text;
using SkyNet.Simulator.Cli.Services;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Cli;

public sealed class TuiApp
{
	private enum ColorMode
	{
		Auto,
		On,
		Off,
	}

	private readonly record struct StyledSegment(string Text, ConsoleColor? Foreground);

	private sealed class StyledLine
	{
		public StyledLine(params StyledSegment[] segments)
		{
			Segments = segments;
			PlainText = string.Concat(segments.Select(segment => segment.Text));
		}

		public IReadOnlyList<StyledSegment> Segments { get; }

		public string PlainText { get; }
	}

	private readonly record struct GaugeBarVisual(string Text, double Normalized);

	private readonly SimApiClient _api;
	private readonly SimHubClient _hub;
	private readonly ColorMode _colorMode;
	private readonly bool _colorEnabled;
	private readonly object _snapshotLock = new();
	private readonly Dictionary<string, GaugeRange> _signalGaugeRanges = new(StringComparer.OrdinalIgnoreCase);
	private string[] _lastFrame = Array.Empty<string>();
	private bool _frameInitialized;
	private TelemetrySnapshot? _latestSnapshot;

	private readonly record struct GaugeRange(double Min, double Max)
	{
		public GaugeRange Include(double value)
		{
			var min = Math.Min(Min, value);
			var max = Math.Max(Max, value);
			if (Math.Abs(max - min) < 1e-9)
			{
				var delta = Math.Max(Math.Abs(value) * 0.05, 0.001);
				min = value - delta;
				max = value + delta;
			}

			return new GaugeRange(min, max);
		}
	}

	public TuiApp(SimApiClient api, SimHubClient hub)
	{
		_api = api;
		_hub = hub;
		_colorMode = ParseColorMode(Environment.GetEnvironmentVariable("SKYNET_TUI_COLOR"));
		_colorEnabled = ResolveColorEnabled(_colorMode);
	}

	public async Task<int> RunAsync(CancellationToken cancellationToken = default)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			var selected = await ShowSimulationSelectionAsync(cancellationToken).ConfigureAwait(false);
			if (selected is null)
			{
				return 0;
			}

			await ShowSimulationScreenAsync(selected, cancellationToken).ConfigureAwait(false);
		}

		return 0;
	}

	private async Task<SimulationInfoDto?> ShowSimulationSelectionAsync(CancellationToken cancellationToken)
	{
		ResetFrame();
		string? error = null;
		var selectedIndex = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			SimulationInfoDto[] sims;
			string activeId;
			try
			{
				sims = await _api.GetSimulationsAsync(cancellationToken).ConfigureAwait(false);
				activeId = await _api.GetActiveSimulationIdAsync(cancellationToken).ConfigureAwait(false);
				error = null;
			}
			catch (Exception ex)
			{
				RenderSelectionScreen(Array.Empty<SimulationInfoDto>(), string.Empty, 0, ex.Message);
				var retryKey = Console.ReadKey(intercept: true);
				if (retryKey.Key == ConsoleKey.Q || retryKey.Key == ConsoleKey.Escape)
				{
					return null;
				}

				continue;
			}

			if (sims.Length == 0)
			{
				RenderSelectionScreen(sims, string.Empty, 0, "No simulations are registered in the daemon.");
				var emptyKey = Console.ReadKey(intercept: true);
				if (emptyKey.Key == ConsoleKey.Q || emptyKey.Key == ConsoleKey.Escape)
				{
					return null;
				}

				continue;
			}

			selectedIndex = Math.Clamp(selectedIndex, 0, sims.Length - 1);
			RenderSelectionScreen(sims, activeId, selectedIndex, error);

			var key = Console.ReadKey(intercept: true);
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					selectedIndex = (selectedIndex - 1 + sims.Length) % sims.Length;
					break;
				case ConsoleKey.DownArrow:
					selectedIndex = (selectedIndex + 1) % sims.Length;
					break;
				case ConsoleKey.Enter:
					return sims[selectedIndex];
				case ConsoleKey.R:
					error = null;
					break;
				case ConsoleKey.Q:
				case ConsoleKey.Escape:
					return null;
			}
		}

		return null;
	}

	private async Task ShowSimulationScreenAsync(SimulationInfoDto simulation, CancellationToken cancellationToken)
	{
		ResetFrame();
		_signalGaugeRanges.Clear();
		var selectedParamIndex = 0;
		string? notice = null;
		DateTimeOffset? noticeUntil = null;
		var signalRConnected = false;
		var parameterDefinitions = Array.Empty<ParameterDefinitionDto>();
		var parameterValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
		var signalValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
		var status = new SimStatus(0, 0, 1.0 / 60.0, true, 0, 0);

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var localCt = linkedCts.Token;

		void OnSnapshot(TelemetrySnapshot snapshot)
		{
			if (!string.Equals(snapshot.SimId, simulation.Id, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			lock (_snapshotLock)
			{
				_latestSnapshot = snapshot;
			}
		}

		_hub.SnapshotReceived += OnSnapshot;
		try
		{
			await _api.SelectSimulationAsync(simulation.Id, localCt).ConfigureAwait(false);
			await _api.ResumeAsync(simulation.Id, localCt).ConfigureAwait(false);

			parameterDefinitions = await _api.GetParameterDefinitionsAsync(simulation.Id, localCt).ConfigureAwait(false);
			parameterValues = await _api.GetParameterValuesAsync(simulation.Id, localCt).ConfigureAwait(false);
			signalValues = await _api.GetSignalsAsync(simulation.Id, localCt).ConfigureAwait(false);
			status = await _api.GetStatusAsync(simulation.Id, localCt).ConfigureAwait(false);

			try
			{
				await _hub.JoinSimulationAsync(simulation.Id, localCt).ConfigureAwait(false);
				signalRConnected = true;
			}
			catch (Exception ex)
			{
				notice = $"Live stream unavailable ({ex.Message}). Using polling fallback.";
				noticeUntil = DateTimeOffset.UtcNow.AddSeconds(5);
			}
		}
		catch (Exception ex)
		{
			RenderError($"Failed to open simulation '{simulation.Name}': {ex.Message}");
			Console.ReadKey(intercept: true);
			_hub.SnapshotReceived -= OnSnapshot;
			return;
		}

		var nextStatusPoll = DateTimeOffset.MinValue;
		var nextSnapshotPoll = DateTimeOffset.MinValue;
		try
		{
			while (!localCt.IsCancellationRequested)
			{
				while (Console.KeyAvailable)
				{
					var key = Console.ReadKey(intercept: true);
					switch (key.Key)
					{
						case ConsoleKey.UpArrow when parameterDefinitions.Length > 0:
							selectedParamIndex = (selectedParamIndex - 1 + parameterDefinitions.Length) % parameterDefinitions.Length;
							break;
						case ConsoleKey.DownArrow when parameterDefinitions.Length > 0:
							selectedParamIndex = (selectedParamIndex + 1) % parameterDefinitions.Length;
							break;
						case ConsoleKey.LeftArrow when parameterDefinitions.Length > 0:
							{
								var definition = parameterDefinitions[selectedParamIndex];
								var result = await NudgeSelectedParameterAsync(simulation.Id, definition, parameterValues, -1, localCt).ConfigureAwait(false);
								notice = result.IsBinaryToggle
									? $"{definition.Name} -> {(result.AppliedValue >= 0.5 ? "1 (Enabled)" : "0 (Disabled)")}."
									: $"Decreased {definition.Name} to {result.AppliedValue.ToString("0.###", CultureInfo.InvariantCulture)}.";
								noticeUntil = DateTimeOffset.UtcNow.AddSeconds(2);
							}
							break;
						case ConsoleKey.RightArrow when parameterDefinitions.Length > 0:
							{
								var definition = parameterDefinitions[selectedParamIndex];
								var result = await NudgeSelectedParameterAsync(simulation.Id, definition, parameterValues, 1, localCt).ConfigureAwait(false);
								notice = result.IsBinaryToggle
									? $"{definition.Name} -> {(result.AppliedValue >= 0.5 ? "1 (Enabled)" : "0 (Disabled)")}."
									: $"Increased {definition.Name} to {result.AppliedValue.ToString("0.###", CultureInfo.InvariantCulture)}.";
								noticeUntil = DateTimeOffset.UtcNow.AddSeconds(2);
							}
							break;
						case ConsoleKey.P:
							try
							{
								if (status.IsPaused)
								{
									await _api.ResumeAsync(simulation.Id, localCt).ConfigureAwait(false);
									notice = "Simulation resumed.";
								}
								else
								{
									await _api.PauseAsync(simulation.Id, localCt).ConfigureAwait(false);
									notice = "Simulation paused.";
								}
								noticeUntil = DateTimeOffset.UtcNow.AddSeconds(2);
								status = await _api.GetStatusAsync(simulation.Id, localCt).ConfigureAwait(false);
							}
							catch (Exception ex)
							{
								notice = ex.Message;
								noticeUntil = DateTimeOffset.UtcNow.AddSeconds(3);
							}
							break;
						case ConsoleKey.S:
							try
							{
								await _api.StepAsync(simulation.Id, 1, localCt).ConfigureAwait(false);
								notice = "Stepped 1 tick.";
								noticeUntil = DateTimeOffset.UtcNow.AddSeconds(2);
								status = await _api.GetStatusAsync(simulation.Id, localCt).ConfigureAwait(false);
							}
							catch (Exception ex)
							{
								notice = ex.Message;
								noticeUntil = DateTimeOffset.UtcNow.AddSeconds(3);
							}
							break;
						case ConsoleKey.E when parameterDefinitions.Length > 0:
						case ConsoleKey.Enter when parameterDefinitions.Length > 0:
							{
								var definition = parameterDefinitions[selectedParamIndex];
								var editResult = await PromptForParameterValueAsync(definition, parameterValues, localCt).ConfigureAwait(false);
								if (editResult is not null)
								{
									try
									{
										await _api.SetParameterAsync(simulation.Id, definition.Name, editResult.Value, localCt).ConfigureAwait(false);
										parameterValues[definition.Name] = editResult.Value;
										notice = $"Updated {definition.Name} to {editResult.Value.ToString(CultureInfo.InvariantCulture)}.";
										noticeUntil = DateTimeOffset.UtcNow.AddSeconds(2);
									}
									catch (Exception ex)
									{
										notice = ex.Message;
										noticeUntil = DateTimeOffset.UtcNow.AddSeconds(3);
									}
								}
							}
							break;
						case ConsoleKey.Q:
						case ConsoleKey.Escape:
							await StopSimulationAsync(simulation.Id, localCt).ConfigureAwait(false);
							return;
						case ConsoleKey.R:
							try
							{
								parameterValues = await _api.GetParameterValuesAsync(simulation.Id, localCt).ConfigureAwait(false);
								signalValues = await _api.GetSignalsAsync(simulation.Id, localCt).ConfigureAwait(false);
								status = await _api.GetStatusAsync(simulation.Id, localCt).ConfigureAwait(false);
								notice = "Data refreshed.";
								noticeUntil = DateTimeOffset.UtcNow.AddSeconds(2);
							}
							catch (Exception ex)
							{
								notice = ex.Message;
								noticeUntil = DateTimeOffset.UtcNow.AddSeconds(3);
							}
							break;
					}
				}

				if (DateTimeOffset.UtcNow >= nextStatusPoll)
				{
					nextStatusPoll = DateTimeOffset.UtcNow.AddMilliseconds(300);
					try
					{
						status = await _api.GetStatusAsync(simulation.Id, localCt).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						notice = ex.Message;
						noticeUntil = DateTimeOffset.UtcNow.AddSeconds(3);
					}
				}

				if (DateTimeOffset.UtcNow >= nextSnapshotPoll)
				{
					nextSnapshotPoll = DateTimeOffset.UtcNow.AddMilliseconds(250);
					try
					{
						signalValues = await _api.GetSignalsAsync(simulation.Id, localCt).ConfigureAwait(false);
						parameterValues = await _api.GetParameterValuesAsync(simulation.Id, localCt).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						notice = ex.Message;
						noticeUntil = DateTimeOffset.UtcNow.AddSeconds(3);
					}
				}

				if (signalRConnected)
				{
					TelemetrySnapshot? snapshot;
					lock (_snapshotLock)
					{
						snapshot = _latestSnapshot;
					}

					if (snapshot is not null && string.Equals(snapshot.SimId, simulation.Id, StringComparison.OrdinalIgnoreCase))
					{
						parameterValues = new Dictionary<string, double>(snapshot.Parameters, StringComparer.OrdinalIgnoreCase);
						signalValues = new Dictionary<string, double>(snapshot.Signals, StringComparer.OrdinalIgnoreCase);
					}
				}

				if (noticeUntil is not null && DateTimeOffset.UtcNow > noticeUntil.Value)
				{
					notice = null;
					noticeUntil = null;
				}

				selectedParamIndex = parameterDefinitions.Length == 0 ? 0 : Math.Clamp(selectedParamIndex, 0, parameterDefinitions.Length - 1);
				RenderSimulationScreen(simulation, status, parameterDefinitions, parameterValues, signalValues, selectedParamIndex, notice);

				await Task.Delay(60, localCt).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (localCt.IsCancellationRequested)
		{
			// normal shutdown path
		}
		finally
		{
			_hub.SnapshotReceived -= OnSnapshot;
			try { await _hub.LeaveCurrentSimulationAsync(CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
		}
	}

	private async Task<double?> PromptForParameterValueAsync(
		ParameterDefinitionDto definition,
		IReadOnlyDictionary<string, double> values,
		CancellationToken cancellationToken)
	{
		ResetFrame();
		RenderEditPrompt(definition, values);
		var text = Console.ReadLine();
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}

		if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
		{
			return null;
		}

		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();
		return parsed;
	}

	private async Task StopSimulationAsync(string simId, CancellationToken cancellationToken)
	{
		try { await _api.StopAsync(simId, cancellationToken).ConfigureAwait(false); } catch { /* best-effort */ }
	}

	private sealed record ParameterNudgeResult(double AppliedValue, bool IsBinaryToggle);

	private async Task<ParameterNudgeResult> NudgeSelectedParameterAsync(
		string simId,
		ParameterDefinitionDto definition,
		IDictionary<string, double> parameterValues,
		int direction,
		CancellationToken cancellationToken)
	{
		parameterValues.TryGetValue(definition.Name, out var current);
		var isBinaryToggle = IsBinaryEnableParameter(definition);
		double next;
		if (isBinaryToggle)
		{
			next = current >= 0.5 ? 0.0 : 1.0;
		}
		else
		{
			var step = ComputeNudgeStep(definition, current);
			next = current + (direction < 0 ? -step : step);
		}

		next = Clamp(next, definition.MinValue, definition.MaxValue);
		await _api.SetParameterAsync(simId, definition.Name, next, cancellationToken).ConfigureAwait(false);
		parameterValues[definition.Name] = next;
		return new ParameterNudgeResult(next, isBinaryToggle);
	}

	private static bool IsBinaryEnableParameter(ParameterDefinitionDto definition)
	{
		if (!definition.MinValue.HasValue || !definition.MaxValue.HasValue)
		{
			return false;
		}

		var hasBinaryRange = Math.Abs(definition.MinValue.Value) < 1e-9
			&& Math.Abs(definition.MaxValue.Value - 1.0) < 1e-9;
		if (!hasBinaryRange)
		{
			return false;
		}

		return definition.Name.Contains("enable", StringComparison.OrdinalIgnoreCase)
			|| definition.Name.Contains("enabled", StringComparison.OrdinalIgnoreCase);
	}

	private static double ComputeNudgeStep(ParameterDefinitionDto definition, double current)
	{
		if (definition.MinValue.HasValue && definition.MaxValue.HasValue)
		{
			var span = definition.MaxValue.Value - definition.MinValue.Value;
			if (span > 0)
			{
				return Math.Max(span / 100.0, 0.0001);
			}
		}

		var basis = Math.Max(Math.Abs(current), Math.Abs(definition.DefaultValue));
		return Math.Max(basis * 0.01, 0.001);
	}

	private static double Clamp(double value, double? min, double? max)
	{
		if (min.HasValue && value < min.Value)
		{
			value = min.Value;
		}

		if (max.HasValue && value > max.Value)
		{
			value = max.Value;
		}

		return value;
	}

	private void RenderSelectionScreen(
		IReadOnlyList<SimulationInfoDto> simulations,
		string activeId,
		int selectedIndex,
		string? error)
	{
		var lines = new List<StyledLine>
		{
			new StyledLine(Segment("SkyNet Simulator TUI", ConsoleColor.Cyan)),
			new StyledLine(Segment(string.Empty)),
			new StyledLine(Segment("Select a simulation:", ConsoleColor.Cyan)),
			new StyledLine(Segment(string.Empty)),
		};

		for (var i = 0; i < simulations.Count; i++)
		{
			var sim = simulations[i];
			var marker = i == selectedIndex
				? Segment(">", ConsoleColor.Yellow)
				: Segment(" ");
			var activeTag = string.Equals(sim.Id, activeId, StringComparison.OrdinalIgnoreCase) ? " [active]" : string.Empty;
			var description = string.IsNullOrWhiteSpace(sim.Description) ? string.Empty : $" - {sim.Description}";
			lines.Add(new StyledLine(
				marker,
				Segment($" {sim.Name} ({sim.Id})"),
				Segment(activeTag, ConsoleColor.Green),
				Segment(description)));
		}

		lines.Add(new StyledLine(Segment(string.Empty)));
		lines.Add(new StyledLine(Segment("Controls: Up/Down=Move  Enter=Open  R=Refresh  Q/Esc=Quit", ConsoleColor.DarkGray)));
		if (!string.IsNullOrWhiteSpace(error))
		{
			lines.Add(new StyledLine(Segment(string.Empty)));
			lines.Add(new StyledLine(Segment($"Error: {error}", ConsoleColor.Red)));
		}

		RenderFrame(lines);
	}

	private void RenderSimulationScreen(
		SimulationInfoDto simulation,
		SimStatus status,
		IReadOnlyList<ParameterDefinitionDto> parameterDefinitions,
		IReadOnlyDictionary<string, double> parameterValues,
		IReadOnlyDictionary<string, double> signalValues,
		int selectedParamIndex,
		string? notice)
	{
		var lines = new List<StyledLine>();
		lines.Add(new StyledLine(Segment($"Simulation: {simulation.Name} ({simulation.Id})", ConsoleColor.Cyan)));
		lines.Add(new StyledLine(
			Segment($"Tick: {status.Tick}  Time: {status.TimeSeconds:0.000}s  Step: {status.StepSeconds:0.000000}s  State: "),
			Segment(status.IsPaused ? "Paused" : "Running", status.IsPaused ? ConsoleColor.Yellow : ConsoleColor.Green)));
		lines.Add(new StyledLine(Segment(string.Empty)));

		UpdateSignalGaugeRanges(signalValues);

		var topSignals = signalValues
			.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
			.Take(12)
			.ToArray();

		lines.Add(new StyledLine(Segment("Signals", ConsoleColor.Cyan)));
		foreach (var signal in topSignals)
		{
			lines.Add(new StyledLine(Segment($"  {signal.Key,-30} {signal.Value,12:0.###}")));
		}

		if (signalValues.Count > topSignals.Length)
		{
			lines.Add(new StyledLine(Segment($"  ... {signalValues.Count - topSignals.Length} more signal(s)", ConsoleColor.DarkGray)));
		}

		lines.Add(new StyledLine(Segment(string.Empty)));
		lines.Add(new StyledLine(Segment("Signal Gauges (auto-scale)", ConsoleColor.Cyan)));
		foreach (var signal in topSignals.Take(6))
		{
			if (_signalGaugeRanges.TryGetValue(signal.Key, out var range))
			{
				var gauge = BuildGaugeBar(signal.Value, range.Min, range.Max, 22);
				lines.Add(new StyledLine(
					Segment($"  {signal.Key,-22} "),
					Segment(gauge.Text, SelectGaugeColor(gauge.Normalized)),
					Segment($" {signal.Value,10:0.###}", ConsoleColor.DarkGray)));
			}
		}

		if (parameterDefinitions.Count > 0)
		{
			var selectedDefinition = parameterDefinitions[selectedParamIndex];
			parameterValues.TryGetValue(selectedDefinition.Name, out var selectedValue);
			lines.Add(new StyledLine(Segment(string.Empty)));
			lines.Add(new StyledLine(
				Segment("Selected Parameter Gauge: ", ConsoleColor.Cyan),
				Segment(selectedDefinition.Name, ConsoleColor.Yellow)));
			if (selectedDefinition.MinValue.HasValue && selectedDefinition.MaxValue.HasValue && selectedDefinition.MaxValue.Value > selectedDefinition.MinValue.Value)
			{
				var gauge = BuildGaugeBar(selectedValue, selectedDefinition.MinValue.Value, selectedDefinition.MaxValue.Value, 28);
				lines.Add(new StyledLine(
					Segment("  "),
					Segment(gauge.Text, SelectGaugeColor(gauge.Normalized)),
					Segment($" {selectedValue:0.###}", ConsoleColor.DarkGray)));
			}
			else
			{
				lines.Add(new StyledLine(
					Segment("  [range unavailable] ", ConsoleColor.DarkYellow),
					Segment($"{selectedValue:0.###}", ConsoleColor.DarkGray)));
			}
		}

		lines.Add(new StyledLine(Segment(string.Empty)));
		lines.Add(new StyledLine(Segment("Parameters", ConsoleColor.Cyan)));
		for (var i = 0; i < parameterDefinitions.Count; i++)
		{
			var definition = parameterDefinitions[i];
			parameterValues.TryGetValue(definition.Name, out var value);
			var marker = i == selectedParamIndex
				? Segment(">", ConsoleColor.Yellow)
				: Segment(" ");
			var range = BuildRange(definition.MinValue, definition.MaxValue);
			lines.Add(new StyledLine(
				marker,
				Segment($" {definition.Name,-30} {value,12:0.###}   {range}")));
		}

		lines.Add(new StyledLine(Segment(string.Empty)));
		lines.Add(new StyledLine(Segment("Controls: Up/Down=Select Param  Left/Right=Adjust  Enter/E=Edit  P=Pause/Resume  S=Step (paused)  R=Refresh  Q/Esc=Back", ConsoleColor.DarkGray)));
		if (!string.IsNullOrWhiteSpace(notice))
		{
			var noticeColor = IsNoticeError(notice) ? ConsoleColor.Red : ConsoleColor.Green;
			lines.Add(new StyledLine(
				Segment("Notice: ", noticeColor),
				Segment(notice, noticeColor)));
		}

		RenderFrame(lines);
	}

	private void RenderEditPrompt(ParameterDefinitionDto definition, IReadOnlyDictionary<string, double> parameterValues)
	{
		Console.Clear();
		parameterValues.TryGetValue(definition.Name, out var current);
		var range = BuildRange(definition.MinValue, definition.MaxValue);
		var unit = string.IsNullOrWhiteSpace(definition.UnitLabel) ? definition.UnitType : definition.UnitLabel;

		var sb = new StringBuilder();
		sb.AppendLine($"Edit Parameter: {definition.Name}");
		sb.AppendLine($"Current: {current.ToString(CultureInfo.InvariantCulture)} {unit}");
		sb.AppendLine($"Range: {range}");
		if (!string.IsNullOrWhiteSpace(definition.Description))
		{
			sb.AppendLine($"Description: {definition.Description}");
		}
		sb.AppendLine();
		sb.AppendLine("Enter new value (blank cancels):");

		WriteWithColor(sb.ToString(), ConsoleColor.Cyan);
	}

	private void RenderError(string message)
	{
		Console.Clear();
		WriteLineWithColor("SkyNet Simulator TUI", ConsoleColor.Cyan);
		Console.WriteLine();
		WriteLineWithColor("Error", ConsoleColor.Red);
		WriteLineWithColor(message, ConsoleColor.Red);
		Console.WriteLine();
		WriteLineWithColor("Press any key to return to menu.", ConsoleColor.DarkGray);
	}

	private static string BuildRange(double? min, double? max)
	{
		var minText = min?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-inf";
		var maxText = max?.ToString("0.###", CultureInfo.InvariantCulture) ?? "+inf";
		return $"[{minText}, {maxText}]";
	}

	private void UpdateSignalGaugeRanges(IReadOnlyDictionary<string, double> signalValues)
	{
		foreach (var (name, value) in signalValues)
		{
			if (_signalGaugeRanges.TryGetValue(name, out var range))
			{
				_signalGaugeRanges[name] = range.Include(value);
			}
			else
			{
				var delta = Math.Max(Math.Abs(value) * 0.05, 0.001);
				_signalGaugeRanges[name] = new GaugeRange(value - delta, value + delta);
			}
		}
	}

	private static GaugeBarVisual BuildGaugeBar(double value, double min, double max, int width)
	{
		if (width < 4)
		{
			width = 4;
		}

		var span = max - min;
		if (span <= 0)
		{
			span = 1;
		}

		var normalized = (value - min) / span;
		normalized = Math.Clamp(normalized, 0.0, 1.0);
		var filled = (int)Math.Round(normalized * width, MidpointRounding.AwayFromZero);
		filled = Math.Clamp(filled, 0, width);

		var text = $"[{new string('#', filled)}{new string('-', width - filled)}] {(normalized * 100):0}%";
		return new GaugeBarVisual(text, normalized);
	}

	private static ConsoleColor SelectGaugeColor(double normalized)
	{
		if (normalized >= 0.85)
		{
			return ConsoleColor.Red;
		}

		if (normalized >= 0.65)
		{
			return ConsoleColor.Yellow;
		}

		return ConsoleColor.Green;
	}

	private void ResetFrame()
	{
		_frameInitialized = false;
		_lastFrame = Array.Empty<string>();
		if (_colorEnabled)
		{
			try { Console.ResetColor(); } catch { }
		}
	}

	private void RenderFrame(IReadOnlyList<string> lines)
	{
		RenderFrame(lines.Select(line => new StyledLine(Segment(line))).ToArray());
	}

	private void RenderFrame(IReadOnlyList<StyledLine> lines)
	{
		var width = Math.Max(2, Console.WindowWidth);
		var maxTextWidth = width - 1;

		if (!_frameInitialized)
		{
			Console.Clear();
			try { Console.CursorVisible = false; } catch { }
			_frameInitialized = true;
		}

		var normalized = lines.Select(line => NormalizeLine(line.PlainText, maxTextWidth)).ToArray();
		var maxLines = Math.Max(normalized.Length, _lastFrame.Length);

		for (var row = 0; row < maxLines; row++)
		{
			var next = row < normalized.Length ? normalized[row] : new string(' ', maxTextWidth);
			var prev = row < _lastFrame.Length ? _lastFrame[row] : string.Empty;
			if (string.Equals(next, prev, StringComparison.Ordinal))
			{
				continue;
			}

			if (row >= Console.BufferHeight)
			{
				break;
			}

			Console.SetCursorPosition(0, row);
			if (row < lines.Count)
			{
				WriteStyledLine(lines[row], maxTextWidth);
			}
			else
			{
				Console.Write(next);
			}
		}

		_lastFrame = normalized;
		if (_colorEnabled)
		{
			try { Console.ResetColor(); } catch { }
		}
		Console.SetCursorPosition(0, Math.Min(maxLines, Math.Max(0, Console.BufferHeight - 1)));
	}

	private void WriteStyledLine(StyledLine line, int width)
	{
		var remaining = width;
		foreach (var segment in line.Segments)
		{
			if (remaining <= 0)
			{
				break;
			}

			var text = segment.Text ?? string.Empty;
			if (text.Length > remaining)
			{
				text = text[..remaining];
			}

			if (_colorEnabled)
			{
				if (segment.Foreground.HasValue)
				{
					Console.ForegroundColor = segment.Foreground.Value;
				}
				else
				{
					Console.ResetColor();
				}
			}

			Console.Write(text);
			remaining -= text.Length;
		}

		if (_colorEnabled)
		{
			Console.ResetColor();
		}

		if (remaining > 0)
		{
			Console.Write(new string(' ', remaining));
		}
	}

	private StyledSegment Segment(string text, ConsoleColor? color = null)
	{
		return new StyledSegment(text, color.HasValue && _colorEnabled ? color.Value : null);
	}

	private void WriteWithColor(string text, ConsoleColor color)
	{
		if (_colorEnabled)
		{
			Console.ForegroundColor = color;
			Console.Write(text);
			Console.ResetColor();
			return;
		}

		Console.Write(text);
	}

	private void WriteLineWithColor(string text, ConsoleColor color)
	{
		if (_colorEnabled)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(text);
			Console.ResetColor();
			return;
		}

		Console.WriteLine(text);
	}

	private static bool IsNoticeError(string notice)
	{
		return notice.Contains("error", StringComparison.OrdinalIgnoreCase)
			|| notice.Contains("failed", StringComparison.OrdinalIgnoreCase)
			|| notice.Contains("exception", StringComparison.OrdinalIgnoreCase)
			|| notice.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
	}

	private static ColorMode ParseColorMode(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return ColorMode.Auto;
		}

		switch (raw.Trim().ToLowerInvariant())
		{
			case "on":
			case "true":
			case "1":
			case "yes":
				return ColorMode.On;
			case "off":
			case "false":
			case "0":
			case "no":
				return ColorMode.Off;
			default:
				return ColorMode.Auto;
		}
	}

	private static bool ResolveColorEnabled(ColorMode colorMode)
	{
		if (colorMode == ColorMode.On)
		{
			return true;
		}

		if (colorMode == ColorMode.Off)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_COLOR")))
		{
			return false;
		}

		var colorForce = Environment.GetEnvironmentVariable("CLICOLOR_FORCE");
		if (string.Equals(colorForce, "1", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(colorForce, "true", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (Console.IsOutputRedirected)
		{
			return false;
		}

		var term = Environment.GetEnvironmentVariable("TERM");
		if (string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return true;
	}

	private static string NormalizeLine(string line, int width)
	{
		if (width <= 0)
		{
			return string.Empty;
		}

		var text = line ?? string.Empty;
		if (text.Length > width)
		{
			text = text[..width];
		}

		if (text.Length < width)
		{
			text = text.PadRight(width);
		}

		return text;
	}
}
