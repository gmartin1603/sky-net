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

public sealed record SaveTrainerPresetRequest(
	IReadOnlyDictionary<string, double> Parameters);

public sealed record TrainerPresetDto(
	string Name,
	DateTimeOffset UpdatedAtUtc,
	IReadOnlyDictionary<string, double> Parameters);

public sealed class TankTransferSchematicLayout
{
	public const double DefaultCanvasWidth = 1160;
	public const double DefaultCanvasHeight = 840;

	public double CanvasWidth { get; set; } = DefaultCanvasWidth;
	public double CanvasHeight { get; set; } = DefaultCanvasHeight;
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

	public TankTransferSchematicLayout Normalize()
	{
		CanvasWidth = Math.Max(CanvasWidth, DefaultCanvasWidth);
		CanvasHeight = Math.Max(CanvasHeight, DefaultCanvasHeight);
		return this;
	}

	public TankTransferSchematicLayout Clone() => new TankTransferSchematicLayout
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
	}.Normalize();
}

public sealed class GrainDryerSchematicLayout
{
	public const double DefaultCanvasWidth = 1040;
	public const double DefaultCanvasHeight = 1240;

	public double CanvasWidth { get; set; } = DefaultCanvasWidth;
	public double CanvasHeight { get; set; } = DefaultCanvasHeight;

	public double FilterBoxX { get; set; } = 250;
	public double FilterBoxY { get; set; } = 52;
	public double ExhaustBoxX { get; set; } = 706;
	public double ExhaustBoxY { get; set; } = 52;

	public double WetBinX { get; set; } = 88;
	public double WetBinY { get; set; } = 168;
	public double DryerColumnX { get; set; } = 422;
	public double DryerColumnY { get; set; } = 146;
	public double DryBinX { get; set; } = 826;
	public double DryBinY { get; set; } = 168;

	public double FanModuleX { get; set; } = 244;
	public double FanModuleY { get; set; } = 430;
	public double FeedScrewX { get; set; } = 384;
	public double FeedScrewY { get; set; } = 348;
	public double BurnerModuleX { get; set; } = 414;
	public double BurnerModuleY { get; set; } = 430;
	public double FeedModuleX { get; set; } = 584;
	public double FeedModuleY { get; set; } = 430;
	public double DischargeConveyorX { get; set; } = 744;
	public double DischargeConveyorY { get; set; } = 448;

	public static GrainDryerSchematicLayout Default => new();

	public GrainDryerSchematicLayout Normalize()
	{
		CanvasWidth = Math.Max(CanvasWidth, DefaultCanvasWidth);
		CanvasHeight = Math.Max(CanvasHeight, DefaultCanvasHeight);
		return this;
	}

	public GrainDryerSchematicLayout Clone() => new GrainDryerSchematicLayout
	{
		CanvasWidth = CanvasWidth,
		CanvasHeight = CanvasHeight,
		FilterBoxX = FilterBoxX,
		FilterBoxY = FilterBoxY,
		ExhaustBoxX = ExhaustBoxX,
		ExhaustBoxY = ExhaustBoxY,
		WetBinX = WetBinX,
		WetBinY = WetBinY,
		DryerColumnX = DryerColumnX,
		DryerColumnY = DryerColumnY,
		DryBinX = DryBinX,
		DryBinY = DryBinY,
		FanModuleX = FanModuleX,
		FanModuleY = FanModuleY,
		FeedScrewX = FeedScrewX,
		FeedScrewY = FeedScrewY,
		BurnerModuleX = BurnerModuleX,
		BurnerModuleY = BurnerModuleY,
		FeedModuleX = FeedModuleX,
		FeedModuleY = FeedModuleY,
		DischargeConveyorX = DischargeConveyorX,
		DischargeConveyorY = DischargeConveyorY,
	}.Normalize();
}
