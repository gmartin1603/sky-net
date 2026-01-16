namespace SkyNet.Simulator.Core.Parameters;

public sealed record ParameterDefinition(
	string Name,
	Type UnitType,
	double DefaultValue,
	double? MinValue = null,
	double? MaxValue = null,
	string? UnitLabel = null,
	string? Description = null)
{
	public ParameterDefinition()
		: this(Name: string.Empty, UnitType: typeof(double), DefaultValue: 0)
	{
	}

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			throw new ArgumentException("Parameter name is required.", nameof(Name));
		}

		if (MinValue.HasValue && MaxValue.HasValue && MinValue.Value > MaxValue.Value)
		{
			throw new ArgumentException($"Parameter '{Name}' has MinValue > MaxValue.");
		}
	}
}
