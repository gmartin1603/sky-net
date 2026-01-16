using System.Globalization;
using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;

var parameters = new ParameterStore();
var system = new HydraulicTrainingSystem(parameters);
var runner = new SimulationRunner(system);

Task? runTask = null;
CancellationTokenSource? runCts = null;

Console.WriteLine("SkyNet Simulator CLI");
Console.WriteLine("Commands: start | stop | status | step [n] | pause | resume | params | param <name> | set <name> <value> | signals | signal <name> | watch (param|signal) <name> | watch off | help | quit");

// Avoid a constantly flashing caret while we reposition the cursor for live updates.
// (Typing still works; we render the input buffer ourselves.)
try { Console.CursorVisible = false; } catch { /* ignore (redirected output etc.) */ }

// Live status line (overwritten in place) + input line underneath.
// When the user presses Enter, we "snapshot" by finalizing the current two lines,
// then continue rendering on fresh lines.

static string FormatStatus(SimulationRunner runner, ISimSystem system, bool running)
{
	if (!running)
	{
		return $"State=Stopped  Tick={runner.Time.Tick}  Time={runner.Time.TotalSeconds:0.000}s";
	}

	var signals = system.Signals.Snapshot();
	return
		$"State=Running  " +
		$"t={runner.Time.TotalSeconds,7:0.000}s  " +
		$"DownstreamPressurePsi={signals.GetValueOrDefault("DownstreamPressurePsi"),8:0.0}  " +
		$"Pos={signals.GetValueOrDefault("ActuatorPosition"),8:0.000}  " +
		$"Vel={signals.GetValueOrDefault("ActuatorVelocity"),8:0.000}";
}

static void WriteAt(int left, int top, string text)
{
	Console.SetCursorPosition(left, top);
	var width = Math.Max(1, Console.BufferWidth);
	if (text.Length >= width)
	{
		Console.Write(text[..Math.Max(0, width - 1)]);
		return;
	}

	Console.Write(text);
	Console.Write(new string(' ', Math.Max(0, width - text.Length - 1)));
}

static string FitToWidth(string text, int width)
{
	if (width <= 1)
	{
		return string.Empty;
	}

	if (text.Length <= width - 1)
	{
		return text;
	}

	// Keep the tail so the most recent typing is visible.
	return text[^Math.Max(0, width - 1)..];
}

static int ClampRenderTop(int renderTop)
{
	var maxTopForTwoLines = Console.BufferHeight - 2;
	if (maxTopForTwoLines < 0)
	{
		maxTopForTwoLines = 0;
	}

	if (renderTop < 0) return 0;
	if (renderTop > maxTopForTwoLines) return maxTopForTwoLines;
	return renderTop;
}

var inputPrefix = "> ";
var inputBuffer = string.Empty;

var watchKind = "none"; // none | param | signal
var watchName = string.Empty;
Console.WriteLine(); // status line placeholder
Console.WriteLine(); // input line placeholder
var renderTop = ClampRenderTop(Console.CursorTop - 2);

var uiTick = TimeSpan.FromMilliseconds(100);
var nextUi = DateTimeOffset.UtcNow;

var lastRenderedStatus = string.Empty;
var lastRenderedInput = string.Empty;
void Render(bool force)
{
	renderTop = ClampRenderTop(renderTop);
	var running = runTask is not null && !runTask.IsCompleted;
	var status = FormatStatus(runner, system, running);
	if (!string.Equals(watchKind, "none", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(watchName))
	{
		var watchText = watchKind.ToLowerInvariant() switch
		{
			"param" => system.Parameters.Contains(watchName)
				? $"WatchParam={watchName}={system.Parameters.Get(watchName):0.###}"
				: $"WatchParam={watchName}=<unknown>",
			"signal" => system.Signals.TryGet(watchName, out var v)
				? $"WatchSignal={watchName}={v:0.###}"
				: $"WatchSignal={watchName}=<unknown>",
			_ => string.Empty,
		};
		if (!string.IsNullOrEmpty(watchText))
		{
			status += "  " + watchText;
		}
	}
	var width = Math.Max(1, Console.BufferWidth);
	var inputLine = FitToWidth(inputPrefix + inputBuffer, width);

	if (force || !string.Equals(status, lastRenderedStatus, StringComparison.Ordinal) || !string.Equals(inputLine, lastRenderedInput, StringComparison.Ordinal))
	{
		WriteAt(0, renderTop, FitToWidth(status, width));
		WriteAt(0, renderTop + 1, inputLine);
		lastRenderedStatus = status;
		lastRenderedInput = inputLine;
	}

	// Keep the (hidden) cursor in a safe place to avoid flicker.
	var cursorLeft = Math.Min(Math.Max(0, inputLine.Length), Math.Max(0, width - 1));
	var cursorTop = Math.Min(Math.Max(0, renderTop + 1), Math.Max(0, Console.BufferHeight - 1));
	Console.SetCursorPosition(cursorLeft, cursorTop);
}

while (true)
{
	var now = DateTimeOffset.UtcNow;
	if (now >= nextUi)
	{
		nextUi = now + uiTick;
		Render(force: false);
	}

	if (!Console.KeyAvailable)
	{
		await Task.Delay(15).ConfigureAwait(false);
		continue;
	}

	var key = Console.ReadKey(intercept: true);
	if (key.Key == ConsoleKey.Enter)
	{
		// Snapshot current status + command, then execute.
		Render(force: true);
		Console.SetCursorPosition(0, renderTop + 2);
		Console.WriteLine();

		var line = inputBuffer;
		inputBuffer = string.Empty;

		var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length != 0)
		{
			var cmd = parts[0].ToLowerInvariant();
			try
			{
				switch (cmd)
				{
					case "help":
						Console.WriteLine("Commands: start | stop | status | step [n] | pause | resume | params | param <name> | set <name> <value> | signals | signal <name> | watch (param|signal) <name> | watch off | help | quit");
						Console.WriteLine("Tip: 'params' shows ranges; out-of-range values are clamped.");
						break;

					case "start":
						if (runTask is not null && !runTask.IsCompleted)
						{
							Console.WriteLine("Already running.");
							break;
						}

						runCts = new CancellationTokenSource();
						runTask = runner.RunRealTimeAsync(runCts.Token);
						Console.WriteLine("Running (60Hz)...");
						break;

					case "stop":
						if (runCts is null)
						{
							Console.WriteLine("Not running.");
							break;
						}

						runCts.Cancel();
						try { await runTask!.ConfigureAwait(false); } catch { /* ignore */ }
						runCts.Dispose();
						runCts = null;
						runTask = null;
						Console.WriteLine("Stopped.");
						break;

					case "status":
						Console.WriteLine($"Tick={runner.Time.Tick} Time={runner.Time.TotalSeconds:0.000}s Step={runner.StepSeconds:0.000000}s");
						Console.WriteLine(runTask is not null && !runTask.IsCompleted ? (runner.IsPaused ? "State=Running (Paused)" : "State=Running") : "State=Stopped");
						break;

					case "step":
						if (runTask is not null && !runTask.IsCompleted)
						{
							Console.WriteLine("Stop the real-time run before stepping.");
							break;
						}

						var steps = 1;
						if (parts.Length == 2 && int.TryParse(parts[1], out var parsed) && parsed > 0)
						{
							steps = parsed;
						}
						else if (parts.Length != 1)
						{
							Console.WriteLine("Usage: step [n]");
							break;
						}

						runner.Step(steps);
						Console.WriteLine($"Stepped {steps} tick(s). Tick={runner.Time.Tick}");
						break;

					case "pause":
						runner.Pause();
						Console.WriteLine("Paused.");
						break;

					case "resume":
						runner.Resume();
						Console.WriteLine("Resumed.");
						break;

					case "list":
					case "params":
						var defs = parameters.SnapshotDefinitions();
						var vals = parameters.Snapshot();
						foreach (var def in defs.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
						{
							vals.TryGetValue(def.Name, out var v);
							var min = def.MinValue.HasValue ? def.MinValue.Value.ToString(CultureInfo.InvariantCulture) : "-inf";
							var max = def.MaxValue.HasValue ? def.MaxValue.Value.ToString(CultureInfo.InvariantCulture) : "+inf";
							var desc = string.IsNullOrWhiteSpace(def.Description) ? string.Empty : $"  {def.Description}";
							Console.WriteLine($"{def.Name} = {v.ToString(CultureInfo.InvariantCulture)}  range=[{min}, {max}]{desc}");
						}
						break;

					case "get":
					case "param":
						if (parts.Length != 2)
						{
							Console.WriteLine("Usage: param <name>");
							break;
						}

						if (parameters.TryGetDefinition(parts[1], out var defn))
						{
							var v = parameters.Get(defn.Name);
							Console.WriteLine($"{defn.Name} = {v.ToString(CultureInfo.InvariantCulture)}");
							Console.WriteLine($"UnitType={defn.UnitType.Name}");
							Console.WriteLine($"Default={defn.DefaultValue.ToString(CultureInfo.InvariantCulture)} Min={defn.MinValue?.ToString(CultureInfo.InvariantCulture) ?? "-inf"} Max={defn.MaxValue?.ToString(CultureInfo.InvariantCulture) ?? "+inf"}");
							if (!string.IsNullOrWhiteSpace(defn.Description))
							{
								Console.WriteLine(defn.Description);
							}
						}
						else
						{
							Console.WriteLine(parameters.Get(parts[1]).ToString(CultureInfo.InvariantCulture));
						}
						break;

					case "set":
						if (parts.Length != 3)
						{
							Console.WriteLine("Usage: set <name> <value>");
							break;
						}

						if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var newValue))
						{
							Console.WriteLine("Value must be a number (use '.' as decimal separator).");
							break;
						}

						parameters.Set(parts[1], newValue);
						var applied = parameters.Get(parts[1]);
						if (!applied.Equals(newValue))
						{
							Console.WriteLine($"Clamped to {applied.ToString(CultureInfo.InvariantCulture)}");
						}
						else
						{
							Console.WriteLine("OK");
						}
						break;

					case "signals":
						foreach (var kvp in system.Signals.Snapshot().OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
						{
							Console.WriteLine($"{kvp.Key} = {kvp.Value.ToString(CultureInfo.InvariantCulture)}");
						}
						break;

					case "signal":
						if (parts.Length != 2)
						{
							Console.WriteLine("Usage: signal <name>");
							break;
						}
						Console.WriteLine(system.Signals.Get(parts[1]).ToString(CultureInfo.InvariantCulture));
						break;

					case "watch":
						if (parts.Length == 2 && string.Equals(parts[1], "off", StringComparison.OrdinalIgnoreCase))
						{
							watchKind = "none";
							watchName = string.Empty;
							Console.WriteLine("Watch cleared.");
							break;
						}

						if (parts.Length != 3)
						{
							Console.WriteLine("Usage: watch (param|signal) <name> | watch off");
							break;
						}

						if (string.Equals(parts[1], "param", StringComparison.OrdinalIgnoreCase))
						{
							watchKind = "param";
							watchName = parts[2];
							Console.WriteLine($"Watching param '{watchName}'.");
						}
						else if (string.Equals(parts[1], "signal", StringComparison.OrdinalIgnoreCase))
						{
							watchKind = "signal";
							watchName = parts[2];
							Console.WriteLine($"Watching signal '{watchName}'.");
						}
						else
						{
							Console.WriteLine("Usage: watch (param|signal) <name> | watch off");
						}
						break;

					case "quit":
					case "exit":
						goto done;

					default:
						Console.WriteLine("Unknown command. Type 'help'.");
						break;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		// Allocate fresh live-render area after any command output.
		Console.WriteLine();
		Console.WriteLine();
		renderTop = ClampRenderTop(Console.CursorTop - 2);
		lastRenderedStatus = string.Empty;
		lastRenderedInput = string.Empty;
		Render(force: true);
		continue;
	}

	if (key.Key == ConsoleKey.Backspace)
	{
		if (inputBuffer.Length > 0)
		{
			inputBuffer = inputBuffer[..^1];
		}
		Render(force: true);
		continue;
	}

	if (key.Key == ConsoleKey.Escape)
	{
		inputBuffer = string.Empty;
		Render(force: true);
		continue;
	}

	if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C)
	{
		goto done;
	}

	if (!char.IsControl(key.KeyChar))
	{
		inputBuffer += key.KeyChar;
		Render(force: true);
	}
}

done:
if (runCts is not null)
{
	runCts.Cancel();
	try { await runTask!.ConfigureAwait(false); } catch { /* ignore */ }
	runCts.Dispose();
}
