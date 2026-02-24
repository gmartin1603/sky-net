using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Daemon.ViewLayouts;

public interface ISimulationViewLayoutStore
{
	Task<TankTransferSchematicLayout> GetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default);
	Task SaveTankTransferLayoutAsync(string simId, TankTransferSchematicLayout layout, CancellationToken cancellationToken = default);
	Task ResetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default);
}
