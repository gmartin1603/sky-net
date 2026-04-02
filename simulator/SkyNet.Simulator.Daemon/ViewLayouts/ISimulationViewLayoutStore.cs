using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Daemon.ViewLayouts;

public interface ISimulationViewLayoutStore
{
	Task<TankTransferSchematicLayout> GetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default);
	Task SaveTankTransferLayoutAsync(string simId, TankTransferSchematicLayout layout, CancellationToken cancellationToken = default);
	Task ResetTankTransferLayoutAsync(string simId, CancellationToken cancellationToken = default);
	Task<GrainDryerSchematicLayout> GetGrainDryerLayoutAsync(string simId, CancellationToken cancellationToken = default);
	Task SaveGrainDryerLayoutAsync(string simId, GrainDryerSchematicLayout layout, CancellationToken cancellationToken = default);
	Task ResetGrainDryerLayoutAsync(string simId, CancellationToken cancellationToken = default);
	Task<TrainerPresetDto?> GetTrainerPresetAsync(string simId, string presetName, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<TrainerPresetDto>> ListTrainerPresetsAsync(string simId, CancellationToken cancellationToken = default);
	Task SaveTrainerPresetAsync(string simId, TrainerPresetDto preset, CancellationToken cancellationToken = default);
	Task ResetTrainerPresetAsync(string simId, string presetName, CancellationToken cancellationToken = default);
}
