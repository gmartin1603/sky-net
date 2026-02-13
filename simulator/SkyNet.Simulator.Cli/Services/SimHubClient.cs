using Microsoft.AspNetCore.SignalR.Client;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Cli.Services;

public sealed class SimHubClient : IAsyncDisposable
{
	private readonly DaemonClientOptions _options;
	private readonly SemaphoreSlim _connectionGate = new(1, 1);
	private readonly CancellationTokenSource _disposeCts = new();
	private HubConnection? _connection;
	private string? _joinedSimId;

	public SimHubClient(DaemonClientOptions options)
	{
		_options = options;
	}

	public event Action<TelemetrySnapshot>? SnapshotReceived;

	private HubConnection CreateConnection()
	{
		var hubUri = new Uri(_options.BaseUri, "simhub");
		var conn = new HubConnectionBuilder()
			.WithUrl(hubUri)
			.WithAutomaticReconnect()
			.Build();

		conn.Reconnected += async _ =>
		{
			if (!string.IsNullOrWhiteSpace(_joinedSimId))
			{
				try
				{
					await conn.InvokeAsync("JoinSim", new object?[] { _joinedSimId }, _disposeCts.Token).ConfigureAwait(false);
				}
				catch
				{
					// reconnect rejoin is best-effort
				}
			}
		};

		conn.On<TelemetrySnapshot>("snapshot", s => SnapshotReceived?.Invoke(s));
		return conn;
	}

	private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
	{
		if (_connection is null)
		{
			_connection = CreateConnection();
		}

		if (_connection.State == HubConnectionState.Connected)
		{
			return;
		}

		await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task JoinSimulationAsync(string simId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
		await _connectionGate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
		try
		{
			await EnsureConnectedAsync(linkedCts.Token).ConfigureAwait(false);
			var conn = _connection!;

			if (!string.IsNullOrWhiteSpace(_joinedSimId) && !string.Equals(_joinedSimId, simId, StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					await conn.InvokeAsync("LeaveSim", new object?[] { _joinedSimId }, linkedCts.Token).ConfigureAwait(false);
				}
				catch
				{
					// ignore
				}
			}

			if (!string.Equals(_joinedSimId, simId, StringComparison.OrdinalIgnoreCase))
			{
				await conn.InvokeAsync("JoinSim", new object?[] { simId }, linkedCts.Token).ConfigureAwait(false);
				_joinedSimId = simId;
			}
		}
		finally
		{
			_connectionGate.Release();
		}
	}

	public async Task LeaveCurrentSimulationAsync(CancellationToken cancellationToken = default)
	{
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
		await _connectionGate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
		try
		{
			if (_connection is null || string.IsNullOrWhiteSpace(_joinedSimId))
			{
				_joinedSimId = null;
				return;
			}

			try
			{
				await _connection.InvokeAsync("LeaveSim", new object?[] { _joinedSimId }, linkedCts.Token).ConfigureAwait(false);
			}
			catch
			{
				// ignore
			}
			finally
			{
				_joinedSimId = null;
			}
		}
		finally
		{
			_connectionGate.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposeCts.IsCancellationRequested)
		{
			return;
		}

		_disposeCts.Cancel();

		HubConnection? connectionToDispose;
		await _connectionGate.WaitAsync().ConfigureAwait(false);
		try
		{
			connectionToDispose = _connection;
			_connection = null;
			_joinedSimId = null;
		}
		finally
		{
			_connectionGate.Release();
		}

		if (connectionToDispose is not null)
		{
			try { await connectionToDispose.StopAsync().ConfigureAwait(false); } catch { /* ignore */ }
			await connectionToDispose.DisposeAsync().ConfigureAwait(false);
		}

		_connectionGate.Dispose();
		_disposeCts.Dispose();
	}
}
