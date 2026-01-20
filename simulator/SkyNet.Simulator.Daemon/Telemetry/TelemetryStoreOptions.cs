using System.ComponentModel.DataAnnotations;

namespace SkyNet.Simulator.Daemon.Telemetry;

public sealed class TelemetryStoreOptions
{
	public const string SectionName = "TelemetryStore";

	public bool Enabled { get; set; } = false;

	/// <summary>
	/// Max snapshots retained across all simulations.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxRowsTotal { get; set; } = 200_000;

	/// <summary>
	/// Logs a warning when total rows exceed this fraction of MaxRowsTotal.
	/// </summary>
	[Range(0.1, 1.0)]
	public double WarnAtFraction { get; set; } = 0.90;

	/// <summary>
	/// Max number of rows deleted per pruning run.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int PruneBatchSize { get; set; } = 10_000;

	/// <summary>
	/// Minimum interval between maintenance runs (prune/warn).
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaintenanceIntervalSeconds { get; set; } = 30;
}
