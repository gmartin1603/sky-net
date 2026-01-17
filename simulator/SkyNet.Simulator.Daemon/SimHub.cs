using Microsoft.AspNetCore.SignalR;

namespace SkyNet.Simulator.Daemon;

public sealed class SimHub : Hub
{
	public Task JoinSim(string simId)
	{
		if (string.IsNullOrWhiteSpace(simId))
		{
			throw new HubException("simId is required.");
		}

		simId = simId.Trim();
		return Groups.AddToGroupAsync(Context.ConnectionId, $"sim:{simId}");
	}

	public Task LeaveSim(string simId)
	{
		if (string.IsNullOrWhiteSpace(simId))
		{
			throw new HubException("simId is required.");
		}

		simId = simId.Trim();
		return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"sim:{simId}");
	}
}
