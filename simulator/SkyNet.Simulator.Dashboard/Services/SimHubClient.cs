using Microsoft.AspNetCore.SignalR.Client;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class SimHubClient : IAsyncDisposable
{
	private readonly DaemonClientOptions _options;
	private HubConnection? _connection;

	public SimHubClient(DaemonClientOptions options)
	{
		_options = options;
	}

	public event Action<TelemetrySnapshot>? SnapshotReceived;

	public bool IsConnected => _connection?.State == HubConnectionState.Connected;

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		if (_connection is not null)
		{
			return;
		}

		_connection = new HubConnectionBuilder()
			.WithUrl($"{_options.BaseUrl.TrimEnd('/')}/simhub")
			.WithAutomaticReconnect()
			.Build();

		_connection.On<TelemetrySnapshot>("snapshot", s => SnapshotReceived?.Invoke(s));
		await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		if (_connection is null)
		{
			return;
		}

		try { await _connection.StopAsync().ConfigureAwait(false); } catch { /* ignore */ }
		await _connection.DisposeAsync().ConfigureAwait(false);
	}
}
