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
		var conn = new HubConnectionBuilder()
			.WithUrl($"{_options.BaseUrl.TrimEnd('/')}/simhub")
			.WithAutomaticReconnect()
			.Build();

		conn.On<TelemetrySnapshot>("snapshot", s =>
		{
			lock (_latestLock)
			{
				_latestBySimId[s.SimId] = s;
			}
			SnapshotReceived?.Invoke(s);
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

		try
		{
			await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (ObjectDisposedException)
		{
			// If some component disposed the connection, recreate and retry.
			_connection = CreateConnection();
			await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public async Task JoinSimulationAsync(string simId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		if (_connection is null)
		{
			await StartAsync(cancellationToken).ConfigureAwait(false);
		}

		if (_connection is null)
		{
			throw new InvalidOperationException("Hub connection not initialized.");
		}

		if (!string.IsNullOrWhiteSpace(_joinedSimId) && string.Equals(_joinedSimId, simId, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(_joinedSimId))
		{
			try { await _connection.InvokeAsync("LeaveSim", _joinedSimId, cancellationToken).ConfigureAwait(false); } catch { /* ignore */ }
		}

		await _connection.InvokeAsync("JoinSim", simId, cancellationToken).ConfigureAwait(false);
		_joinedSimId = simId;
	}

	public async Task LeaveCurrentSimulationAsync(CancellationToken cancellationToken = default)
	{
		if (_connection is null)
		{
			_joinedSimId = null;
			return;
		}

		if (string.IsNullOrWhiteSpace(_joinedSimId))
		{
			return;
		}

		try
		{
			await _connection.InvokeAsync("LeaveSim", _joinedSimId, cancellationToken).ConfigureAwait(false);
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
