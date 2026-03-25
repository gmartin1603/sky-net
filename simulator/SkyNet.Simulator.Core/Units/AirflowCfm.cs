namespace SkyNet.Simulator.Core.Units;

/// <summary>
/// Volumetric airflow in cubic feet per minute.
/// </summary>
public readonly record struct AirflowCfm(double Value) : IUnit<AirflowCfm>
{
	public static AirflowCfm From(double value) => new(value);

	public override string ToString() => $"{Value:0.###} cfm";
}
