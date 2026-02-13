using System.Globalization;
using System.Text;
using SkyNet.Simulator.Cli.Services;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Cli;

public sealed class TuiApp
{
	private readonly SimApiClient _api;
	private readonly SimHubClient _hub;
	private readonly object _snapshotLock = new();
	private string[] _lastFrame = Array.Empty<string>();
	private bool _frameInitialized;
	private TelemetrySnapshot? _latestSnapshot;

	public TuiApp(SimApiClient api, SimHubClient hub)
	{
		_api = api;
		_hub = hub;
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
		var lines = new List<string>
		{
			"SkyNet Simulator TUI",
			string.Empty,
			"Select a simulation:",
			string.Empty,
		};

		for (var i = 0; i < simulations.Count; i++)
		{
			var sim = simulations[i];
			var marker = i == selectedIndex ? ">" : " ";
			var activeTag = string.Equals(sim.Id, activeId, StringComparison.OrdinalIgnoreCase) ? " [active]" : string.Empty;
			var description = string.IsNullOrWhiteSpace(sim.Description) ? string.Empty : $" - {sim.Description}";
			lines.Add($"{marker} {sim.Name} ({sim.Id}){activeTag}{description}");
		}

		lines.Add(string.Empty);
		lines.Add("Controls: Up/Down=Move  Enter=Open  R=Refresh  Q/Esc=Quit");
		if (!string.IsNullOrWhiteSpace(error))
		{
			lines.Add(string.Empty);
			lines.Add($"Error: {error}");
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
		var lines = new List<string>
		{
			$"Simulation: {simulation.Name} ({simulation.Id})",
			$"Tick: {status.Tick}  Time: {status.TimeSeconds:0.000}s  Step: {status.StepSeconds:0.000000}s  State: {(status.IsPaused ? "Paused" : "Running")}",
			string.Empty,
		};

		var topSignals = signalValues
			.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
			.Take(12)
			.ToArray();

		lines.Add("Signals");
		foreach (var signal in topSignals)
		{
			lines.Add($"  {signal.Key,-30} {signal.Value,12:0.###}");
		}

		if (signalValues.Count > topSignals.Length)
		{
			lines.Add($"  ... {signalValues.Count - topSignals.Length} more signal(s)");
		}

		lines.Add(string.Empty);
		lines.Add("Parameters");
		for (var i = 0; i < parameterDefinitions.Count; i++)
		{
			var definition = parameterDefinitions[i];
			parameterValues.TryGetValue(definition.Name, out var value);
			var marker = i == selectedParamIndex ? ">" : " ";
			var range = BuildRange(definition.MinValue, definition.MaxValue);
			lines.Add($"{marker} {definition.Name,-30} {value,12:0.###}   {range}");
		}

		lines.Add(string.Empty);
		lines.Add("Controls: Up/Down=Select Param  Left/Right=Adjust  Enter/E=Edit  P=Pause/Resume  S=Step (paused)  R=Refresh  Q/Esc=Back");
		if (!string.IsNullOrWhiteSpace(notice))
		{
			lines.Add($"Notice: {notice}");
		}

		RenderFrame(lines);
	}

	private static void RenderEditPrompt(ParameterDefinitionDto definition, IReadOnlyDictionary<string, double> parameterValues)
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

		Console.Write(sb.ToString());
	}

	private static void RenderError(string message)
	{
		Console.Clear();
		Console.WriteLine("SkyNet Simulator TUI");
		Console.WriteLine();
		Console.WriteLine("Error");
		Console.WriteLine(message);
		Console.WriteLine();
		Console.WriteLine("Press any key to return to menu.");
	}

	private static string BuildRange(double? min, double? max)
	{
		var minText = min?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-inf";
		var maxText = max?.ToString("0.###", CultureInfo.InvariantCulture) ?? "+inf";
		return $"[{minText}, {maxText}]";
	}

	private void ResetFrame()
	{
		_frameInitialized = false;
		_lastFrame = Array.Empty<string>();
	}

	private void RenderFrame(IReadOnlyList<string> lines)
	{
		var width = Math.Max(2, Console.WindowWidth);
		var maxTextWidth = width - 1;

		if (!_frameInitialized)
		{
			Console.Clear();
			try { Console.CursorVisible = false; } catch { }
			_frameInitialized = true;
		}

		var normalized = lines.Select(line => NormalizeLine(line, maxTextWidth)).ToArray();
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
			Console.Write(next);
		}

		_lastFrame = normalized;
		Console.SetCursorPosition(0, Math.Min(maxLines, Math.Max(0, Console.BufferHeight - 1)));
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
