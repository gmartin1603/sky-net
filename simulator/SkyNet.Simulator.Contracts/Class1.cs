namespace SkyNet.Simulator.Contracts;

public sealed record TelemetrySnapshot(
	int SchemaVersion,
	long Tick,
	double TimeSeconds,
	IReadOnlyDictionary<string, double> Parameters,
	IReadOnlyDictionary<string, double> Signals);

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
