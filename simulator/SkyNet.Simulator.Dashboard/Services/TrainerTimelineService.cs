using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class TrainerTimelineService(SimApiClient api)
{
	public async Task<IReadOnlyList<LogEntryDto>> GetRecentEventsAsync(string simId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(simId))
		{
			return Array.Empty<LogEntryDto>();
		}

		var batch = await api.GetLogsAsync(simId, take: 120, cancellationToken: cancellationToken).ConfigureAwait(false);
		return batch.Entries
			.Where(entry =>
				entry.Level.Contains("trainer", StringComparison.OrdinalIgnoreCase)
				|| entry.Message.Contains("preset", StringComparison.OrdinalIgnoreCase)
				|| entry.Message.Contains("disturbance", StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(entry => entry.Timestamp)
			.Take(12)
			.ToArray();
	}
}
