namespace SkyNet.Simulator.Core.Parameters;

public sealed class ParameterChangedEventArgs(string name, double oldValue, double newValue) : EventArgs
{
	public string Name { get; } = name;
	public double OldValue { get; } = oldValue;
	public double NewValue { get; } = newValue;
}
