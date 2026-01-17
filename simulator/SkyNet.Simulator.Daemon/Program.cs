using SkyNet.Simulator.Contracts;
using SkyNet.Simulator.Core.Parameters;
using SkyNet.Simulator.Core.Simulation;
using SkyNet.Simulator.Core.Systems;
using SkyNet.Simulator.Daemon;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://localhost:5070");

builder.Services.AddCors(options =>
{
	options.AddPolicy("DevDashboard", policy => policy
		.AllowAnyHeader()
		.AllowAnyMethod()
		.SetIsOriginAllowed(_ => true)
		.AllowCredentials());
});

builder.Services.AddSignalR();

builder.Services.AddSingleton(sp =>
{
	// Register scenarios here (code-first for now).
	// Each scenario gets its own ParameterStore/Runner/Logs.
	var sims = new List<SimulationSlot>();

	SimulationSlot Create(string id, string name, string? description, Func<ParameterStore, ISimSystem> createSystem)
	{
		var parameters = new ParameterStore();
		var system = createSystem(parameters);
		var runner = new SimulationRunner(system);
		runner.Pause();
		var logs = new InMemoryLogStore();
		return new SimulationSlot(
			Info: new SimulationInfoDto(id, name, description, Tags: Array.Empty<string>()),
			System: system,
			Runner: runner,
			Parameters: parameters,
			Logs: logs);
	}

	sims.Add(Create(
		id: "hydraulic-training",
		name: "Hydraulic Training",
		description: "Training-oriented hydraulic slice with parameters + signals.",
		createSystem: ps => new HydraulicTrainingSystem(ps)));

	sims.Add(Create(
		id: "hydraulic-demo",
		name: "Hydraulic Demo",
		description: "Minimal demo system used for quick validation.",
		createSystem: ps => new DemoHydraulicSystem(ps)));

	return new SimulationRegistry(sims, defaultActiveId: "hydraulic-training");
});

builder.Services.AddHostedService<SimHostService>();

var app = builder.Build();

app.UseCors("DevDashboard");

app.MapGet("/", () => Results.Text("SkyNet Simulator Daemon", "text/plain"));

// Simulation catalog + selection
app.MapGet("/api/sims", (SimulationRegistry registry) => Results.Ok(registry.List()));

app.MapGet("/api/sims/active", (SimulationRegistry registry) => Results.Ok(new { id = registry.ActiveId }));

app.MapPost("/api/sims/{id}/select", (string id, SimulationRegistry registry) =>
{
	if (!registry.TrySetActive(id))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}
	return Results.NoContent();
});

// Preferred: sim-specific endpoints (avoid global 'active sim' coupling)
app.MapGet("/api/sims/{id}/status", (string id, SimulationRegistry registry) =>
{
	if (!registry.TryGet(id, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}

	var runner = slot.Runner;
	return Results.Ok(new SimStatus(
		Tick: runner.Time.Tick,
		TimeSeconds: runner.Time.TotalSeconds,
		StepSeconds: runner.StepSeconds,
		IsPaused: runner.IsPaused,
		LateTicks: runner.LateTicks,
		MaxBehindSeconds: runner.MaxBehindSeconds));
});

app.MapGet("/api/sims/{id}/parameters/definitions", (string id, SimulationRegistry registry) =>
{
	if (!registry.TryGet(id, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}

	var defs = slot.System.Parameters.SnapshotDefinitions().Values
		.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
		.Select(d => new ParameterDefinitionDto(
			Name: d.Name,
			UnitType: d.UnitType.Name,
			DefaultValue: d.DefaultValue,
			MinValue: d.MinValue,
			MaxValue: d.MaxValue,
			UnitLabel: d.UnitLabel,
			Description: d.Description))
		.ToArray();

	return Results.Ok(defs);
});

app.MapGet("/api/sims/{id}/parameters/values", (string id, SimulationRegistry registry) =>
{
	if (!registry.TryGet(id, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}

	return Results.Ok(slot.System.Parameters.Snapshot());
});

app.MapPost("/api/sims/{id}/parameters/{name}", (string id, string name, SetParameterRequest request, SimulationRegistry registry) =>
{
	if (!registry.TryGet(id, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}

	try
	{
		slot.System.Parameters.Set(name, request.Value);
		slot.Logs.Add(slot.Info.Id, "Info", $"Parameter set: {name}={request.Value:0.###}");
		return Results.NoContent();
	}
	catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException)
	{
		return Results.BadRequest(new { error = ex.Message });
	}
});

app.MapGet("/api/sims/{id}/signals", (string id, SimulationRegistry registry) =>
{
	if (!registry.TryGet(id, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}

	return Results.Ok(slot.System.Signals.Snapshot());
});

app.MapPost("/api/sims/{id}/pause", (string id, SimulationRegistry registry) =>
{
	if (!registry.TryGet(id, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}

	slot.Runner.Pause();
	slot.Logs.Add(slot.Info.Id, "Info", "Paused");
	return Results.NoContent();
});

app.MapPost("/api/sims/{id}/resume", (string id, SimulationRegistry registry) =>
{
	if (!registry.TryGet(id, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}

	slot.Runner.Resume();
	slot.Logs.Add(slot.Info.Id, "Info", "Resumed");
	return Results.NoContent();
});

app.MapPost("/api/sims/{id}/step", (string id, int? n, SimulationRegistry registry) =>
{
	if (!registry.TryGet(id, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{id}'." });
	}

	if (!slot.Runner.IsPaused)
	{
		return Results.Conflict(new { error = "Runner must be paused before stepping." });
	}

	var steps = n.GetValueOrDefault(1);
	if (steps <= 0)
	{
		return Results.BadRequest(new { error = "n must be >= 1" });
	}

	slot.Runner.Step(steps);
	slot.Logs.Add(slot.Info.Id, "Info", $"Stepped {steps} tick(s)");
	return Results.NoContent();
});

// Logs (simple tail/poll model)
app.MapGet("/api/logs", (string? simId, long? after, int? take, SimulationRegistry registry) =>
{
	var targetId = string.IsNullOrWhiteSpace(simId) ? registry.ActiveId : simId;
	if (!registry.TryGet(targetId!, out var slot))
	{
		return Results.NotFound(new { error = $"Unknown simulation '{targetId}'." });
	}
	return Results.Ok(slot.Logs.Get(after.GetValueOrDefault(0), take.GetValueOrDefault(200)));
});

app.MapGet("/api/status", (SimulationRegistry registry) =>
{
	var runner = registry.GetActive().Runner;
	return Results.Ok(new SimStatus(
		Tick: runner.Time.Tick,
		TimeSeconds: runner.Time.TotalSeconds,
		StepSeconds: runner.StepSeconds,
		IsPaused: runner.IsPaused,
		LateTicks: runner.LateTicks,
		MaxBehindSeconds: runner.MaxBehindSeconds));
});

app.MapGet("/api/parameters/definitions", (SimulationRegistry registry) =>
{
	var system = registry.GetActive().System;
	var defs = system.Parameters.SnapshotDefinitions().Values
		.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
		.Select(d => new ParameterDefinitionDto(
			Name: d.Name,
			UnitType: d.UnitType.Name,
			DefaultValue: d.DefaultValue,
			MinValue: d.MinValue,
			MaxValue: d.MaxValue,
			UnitLabel: d.UnitLabel,
			Description: d.Description))
		.ToArray();

	return Results.Ok(defs);
});

app.MapGet("/api/parameters/values", (SimulationRegistry registry) => Results.Ok(registry.GetActive().System.Parameters.Snapshot()));

app.MapPost("/api/parameters/{name}", (string name, SetParameterRequest request, SimulationRegistry registry) =>
{
	var active = registry.GetActive();
	var system = active.System;
	try
	{
		system.Parameters.Set(name, request.Value);
		active.Logs.Add(active.Info.Id, "Info", $"Parameter set: {name}={request.Value:0.###}");
		return Results.NoContent();
	}
	catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException)
	{
		return Results.BadRequest(new { error = ex.Message });
	}
});

app.MapGet("/api/signals", (SimulationRegistry registry) => Results.Ok(registry.GetActive().System.Signals.Snapshot()));

app.MapPost("/api/pause", (SimulationRegistry registry) =>
{
	var active = registry.GetActive();
	var runner = active.Runner;
	runner.Pause();
	active.Logs.Add(active.Info.Id, "Info", "Paused");
	return Results.NoContent();
});

app.MapPost("/api/resume", (SimulationRegistry registry) =>
{
	var active = registry.GetActive();
	var runner = active.Runner;
	runner.Resume();
	active.Logs.Add(active.Info.Id, "Info", "Resumed");
	return Results.NoContent();
});

app.MapPost("/api/step", (int? n, SimulationRegistry registry) =>
{
	var active = registry.GetActive();
	var runner = active.Runner;
	if (!runner.IsPaused)
	{
		return Results.Conflict(new { error = "Runner must be paused before stepping." });
	}

	var steps = n.GetValueOrDefault(1);
	if (steps <= 0)
	{
		return Results.BadRequest(new { error = "n must be >= 1" });
	}

	runner.Step(steps);
	active.Logs.Add(active.Info.Id, "Info", $"Stepped {steps} tick(s)");
	return Results.NoContent();
});

app.MapHub<SimHub>("/simhub");

app.Run();
