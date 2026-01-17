namespace SkyNet.Simulator.Contracts;

public sealed record TelemetrySnapshot(
	string SimId,
	int SchemaVersion,
	long Tick,
	double TimeSeconds,
	IReadOnlyDictionary<string, double> Parameters,
	IReadOnlyDictionary<string, double> Signals);

public sealed record SimulationInfoDto(
	string Id,
	string Name,
	string? Description,
	string[] Tags);

public sealed record LogEntryDto(
	long Seq,
	DateTimeOffset Timestamp,
	string SimId,
	string Level,
	string Message);

public sealed record LogBatchDto(
	long NextAfter,
	IReadOnlyList<LogEntryDto> Entries);

public sealed record SimStatus(
	long Tick,
	double TimeSeconds,
	double StepSeconds,
	bool IsPaused,
	long LateTicks,
	double MaxBehindSeconds);

public sealed record ParameterDefinitionDto(
	string Name,
	string UnitType,
	double DefaultValue,
	double? MinValue,
	double? MaxValue,
	string? UnitLabel,
	string? Description);

public sealed record SetParameterRequest(double Value);
