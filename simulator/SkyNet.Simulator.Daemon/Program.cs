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

builder.Services.AddSingleton<ParameterStore>();
builder.Services.AddSingleton<ISimSystem>(sp => new HydraulicTrainingSystem(sp.GetRequiredService<ParameterStore>()));
builder.Services.AddSingleton(sp => new SimulationRunner(sp.GetRequiredService<ISimSystem>()));
builder.Services.AddHostedService<SimHostService>();

var app = builder.Build();

app.UseCors("DevDashboard");

app.MapGet("/", () => Results.Text("SkyNet Simulator Daemon", "text/plain"));

app.MapGet("/api/status", (SimulationRunner runner) =>
	Results.Ok(new SimStatus(
		Tick: runner.Time.Tick,
		TimeSeconds: runner.Time.TotalSeconds,
		StepSeconds: runner.StepSeconds,
		IsPaused: runner.IsPaused,
		LateTicks: runner.LateTicks,
		MaxBehindSeconds: runner.MaxBehindSeconds)));

app.MapGet("/api/parameters/definitions", (ISimSystem system) =>
{
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

app.MapGet("/api/parameters/values", (ISimSystem system) => Results.Ok(system.Parameters.Snapshot()));

app.MapPost("/api/parameters/{name}", (string name, SetParameterRequest request, ISimSystem system) =>
{
	try
	{
		system.Parameters.Set(name, request.Value);
		return Results.NoContent();
	}
	catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException)
	{
		return Results.BadRequest(new { error = ex.Message });
	}
});

app.MapGet("/api/signals", (ISimSystem system) => Results.Ok(system.Signals.Snapshot()));

app.MapPost("/api/pause", (SimulationRunner runner) =>
{
	runner.Pause();
	return Results.NoContent();
});

app.MapPost("/api/resume", (SimulationRunner runner) =>
{
	runner.Resume();
	return Results.NoContent();
});

app.MapPost("/api/step", (int? n, SimulationRunner runner) =>
{
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
	return Results.NoContent();
});

app.MapHub<SimHub>("/simhub");

app.Run();
