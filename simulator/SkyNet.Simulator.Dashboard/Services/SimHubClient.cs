using Microsoft.AspNetCore.SignalR.Client;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class SimHubClient : IAsyncDisposable
{
	private readonly DaemonClientOptions _options;
	private HubConnection? _connection;
	private string? _joinedSimId;
	private readonly Dictionary<string, TelemetrySnapshot> _latestBySimId = new(StringComparer.OrdinalIgnoreCase);
	private readonly object _latestLock = new();
	private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);

	public SimHubClient(DaemonClientOptions options)
	{
		_options = options;
	}

	public event Action<TelemetrySnapshot>? SnapshotReceived;

	public bool IsConnected => _connection?.State == HubConnectionState.Connected;

	public bool TryGetLatestSnapshot(string simId, out TelemetrySnapshot snapshot)
	{
		if (string.IsNullOrWhiteSpace(simId))
		{
			snapshot = default!;
			return false;
		}

		lock (_latestLock)
		{
			return _latestBySimId.TryGetValue(simId.Trim(), out snapshot!);
		}
	}

	private HubConnection CreateConnection()
	{
		var hubUri = new Uri(_options.BaseUri, "simhub");
		var conn = new HubConnectionBuilder()
			.WithUrl(hubUri)
			.WithAutomaticReconnect()
			.Build();

		conn.On<TelemetrySnapshot>("snapshot", s =>
		{
			Action<TelemetrySnapshot>? handler;
			lock (_latestLock)
			{
				_latestBySimId[s.SimId] = s;
				handler = SnapshotReceived;
			}
			handler?.Invoke(s);
		});
		return conn;
	}

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		if (_connection is null)
		{
			_connection = CreateConnection();
		}

		if (_connection.State == HubConnectionState.Connected)
		{
			return;
		}

		// Only an explicitly disconnected connection can be started.
		if (_connection.State != HubConnectionState.Disconnected)
		{
			return;
		}

		try
		{
			await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (ObjectDisposedException ex)
		{
			// If some component disposed the connection, log, recreate, and retry.
			System.Console.Error.WriteLine($"[{nameof(SimHubClient)}] HubConnection was disposed; recreating connection. Exception: {ex}");
			_connection = CreateConnection();
			await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
	{
		await StartAsync(cancellationToken).ConfigureAwait(false);

		if (_connection is null)
		{
			throw new InvalidOperationException("Hub connection not initialized.");
		}

		if (_connection.State == HubConnectionState.Connected)
		{
			return;
		}

		var startedAt = DateTimeOffset.UtcNow;
		while (_connection.State != HubConnectionState.Connected)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (DateTimeOffset.UtcNow - startedAt > DefaultConnectTimeout)
			{
				throw new TimeoutException($"Timed out waiting for SignalR connection to connect. State={_connection.State}.");
			}

			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
		}
	}

	public async Task JoinSimulationAsync(string simId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

		HubConnection conn = _connection!;

		if (!string.IsNullOrWhiteSpace(_joinedSimId) && string.Equals(_joinedSimId, simId, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(_joinedSimId))
		{
			try { await conn.InvokeAsync("LeaveSim", new object?[] { _joinedSimId }, cancellationToken).ConfigureAwait(false); } catch { /* ignore */ }
		}

		await conn.InvokeAsync("JoinSim", new object?[] { simId }, cancellationToken).ConfigureAwait(false);
		_joinedSimId = simId;
	}

	public async Task LeaveCurrentSimulationAsync(CancellationToken cancellationToken = default)
	{
		if (_connection is null)
		{
			_joinedSimId = null;
			return;
		}

		HubConnection conn = _connection!;

		if (string.IsNullOrWhiteSpace(_joinedSimId))
		{
			return;
		}

		try
		{
			await conn.InvokeAsync("LeaveSim", new object?[] { _joinedSimId }, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// ignore; leaving is best-effort
		}
		finally
		{
			_joinedSimId = null;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_connection is null)
		{
			return;
		}

		try { await LeaveCurrentSimulationAsync().ConfigureAwait(false); } catch { /* ignore */ }
		try { await _connection.StopAsync().ConfigureAwait(false); } catch { /* ignore */ }
		await _connection.DisposeAsync().ConfigureAwait(false);
		_connection = null;
	}
}
