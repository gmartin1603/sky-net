namespace SkyNet.Simulator.Core.Units;

public interface IUnit<TSelf> where TSelf : struct, IUnit<TSelf>
{
	double Value { get; }

	static abstract TSelf From(double value);
}
