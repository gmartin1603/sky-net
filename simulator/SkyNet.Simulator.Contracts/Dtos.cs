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

public sealed class TankTransferSchematicLayout
{
	public double CanvasWidth { get; set; } = 1160;
	public double CanvasHeight { get; set; } = 420;
	public double BoxWidth { get; set; } = 220;

	public double TopRowY { get; set; } = 0;
	public double BottomRowY { get; set; } = 196;

	public double SourceX { get; set; } = 250;
	public double DestinationX { get; set; } = 970;
	public double BlowerX { get; set; } = 0;
	public double AirlockX { get; set; } = 250;
	public double BlowlineX { get; set; } = 560;

	public double TankBottomAnchorY { get; set; } = 135;
	public double DestinationInletAnchorY { get; set; } = 155;
	public double EquipmentTopAnchorY { get; set; } = 26;
	public double EquipmentMidAnchorY { get; set; } = 104;
	public double DestinationInletOffsetX { get; set; } = 50;

	public static TankTransferSchematicLayout Default => new();

	public TankTransferSchematicLayout Clone() => new()
	{
		CanvasWidth = CanvasWidth,
		CanvasHeight = CanvasHeight,
		BoxWidth = BoxWidth,
		TopRowY = TopRowY,
		BottomRowY = BottomRowY,
		SourceX = SourceX,
		DestinationX = DestinationX,
		BlowerX = BlowerX,
		AirlockX = AirlockX,
		BlowlineX = BlowlineX,
		TankBottomAnchorY = TankBottomAnchorY,
		DestinationInletAnchorY = DestinationInletAnchorY,
		EquipmentTopAnchorY = EquipmentTopAnchorY,
		EquipmentMidAnchorY = EquipmentMidAnchorY,
		DestinationInletOffsetX = DestinationInletOffsetX,
	};
}
