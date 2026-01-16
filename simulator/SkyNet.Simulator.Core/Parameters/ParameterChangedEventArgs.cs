namespace SkyNet.Simulator.Core.Parameters;

public sealed class ParameterChangedEventArgs : EventArgs
{
	public ParameterChangedEventArgs(string name, double oldValue, double newValue)
		: this(name, oldValue, requestedValue: newValue, newValue: newValue, wasClamped: false)
	{
	}

	public ParameterChangedEventArgs(string name, double oldValue, double requestedValue, double newValue, bool wasClamped)
	{
		Name = name;
		OldValue = oldValue;
		RequestedValue = requestedValue;
		NewValue = newValue;
		WasClamped = wasClamped;
	}

	public string Name { get; }
	public double OldValue { get; }
	public double RequestedValue { get; }
	public double NewValue { get; }
	public bool WasClamped { get; }
}
