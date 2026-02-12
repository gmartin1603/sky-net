using Microsoft.AspNetCore.SignalR.Client;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class SimHubClient : IAsyncDisposable
{
	private readonly DaemonClientOptions _options;
	private readonly SemaphoreSlim _connectionGate = new(1, 1);
	private readonly CancellationTokenSource _disposeCts = new();
	private HubConnection? _connection;
	private string? _joinedSimId;
	private TaskCompletionSource<bool>? _connectionStateChanged;
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

		conn.Reconnecting += _ =>
		{
			SignalConnectionStateChanged();
			return Task.CompletedTask;
		};

		conn.Reconnected += async _ =>
		{
			SignalConnectionStateChanged();
			await RejoinAfterReconnectAsync(conn).ConfigureAwait(false);
		};

		conn.Closed += _ =>
		{
			SignalConnectionStateChanged();
			return Task.CompletedTask;
		};

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
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
		await _connectionGate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
		try
		{
			await EnsureConnectedCoreAsync(linkedCts.Token).ConfigureAwait(false);
		}
		finally
		{
			_connectionGate.Release();
		}
	}

	private async Task EnsureConnectedCoreAsync(CancellationToken cancellationToken)
	{
		if (_connection is null)
		{
			_connection = CreateConnection();
		}

		if (_connection.State == HubConnectionState.Connected)
		{
			return;
		}

		while (_connection.State != HubConnectionState.Connected)
		{
			if (_connection.State == HubConnectionState.Disconnected)
			{
				try
				{
					await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (ObjectDisposedException ex)
				{
					System.Console.Error.WriteLine($"[{nameof(SimHubClient)}] HubConnection was disposed; recreating connection. Exception: {ex}");
					_connection = CreateConnection();
					await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
				}
				if (_connection.State == HubConnectionState.Connected)
				{
					return;
				}
			}

			try
			{
				await WaitForConnectionStateChangeAsync(cancellationToken).WaitAsync(DefaultConnectTimeout, cancellationToken).ConfigureAwait(false);
			}
			catch (TimeoutException)
			{
				throw new TimeoutException($"Timed out waiting for SignalR connection to connect. State={_connection.State}.");
			}
		}
	}

	private Task WaitForConnectionStateChangeAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (_connectionStateChanged is null || _connectionStateChanged.Task.IsCompleted)
		{
			_connectionStateChanged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		}

		return _connectionStateChanged.Task;
	}

	private void SignalConnectionStateChanged()
	{
		_connectionStateChanged?.TrySetResult(true);
	}

	private async Task RejoinAfterReconnectAsync(HubConnection connection)
	{
		if (_disposeCts.IsCancellationRequested)
		{
			return;
		}

		await _connectionGate.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
		try
		{
			if (!ReferenceEquals(_connection, connection))
			{
				return;
			}

			if (connection.State != HubConnectionState.Connected)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(_joinedSimId))
			{
				return;
			}

			await connection.InvokeAsync("JoinSim", new object?[] { _joinedSimId }, _disposeCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
		{
			// ignore during disposal
		}
		catch
		{
			// ignore; reconnect rejoin is best-effort
		}
		finally
		{
			_connectionGate.Release();
		}
	}

	public async Task JoinSimulationAsync(string simId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simId);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
		await _connectionGate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
		try
		{
			await EnsureConnectedCoreAsync(linkedCts.Token).ConfigureAwait(false);

			HubConnection conn = _connection!;

			if (!string.IsNullOrWhiteSpace(_joinedSimId) && string.Equals(_joinedSimId, simId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (!string.IsNullOrWhiteSpace(_joinedSimId))
			{
				try { await conn.InvokeAsync("LeaveSim", new object?[] { _joinedSimId }, linkedCts.Token).ConfigureAwait(false); } catch { /* ignore */ }
			}

			await conn.InvokeAsync("JoinSim", new object?[] { simId }, linkedCts.Token).ConfigureAwait(false);
			_joinedSimId = simId;
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
			if (_connection is null)
			{
				_joinedSimId = null;
				return;
			}

			HubConnection conn = _connection;

			if (string.IsNullOrWhiteSpace(_joinedSimId))
			{
				return;
			}

			try
			{
				await conn.InvokeAsync("LeaveSim", new object?[] { _joinedSimId }, linkedCts.Token).ConfigureAwait(false);
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
		SignalConnectionStateChanged();

		HubConnection? connectionToDispose;
		string? joinedSimId;

		await _connectionGate.WaitAsync().ConfigureAwait(false);
		try
		{
			connectionToDispose = _connection;
			joinedSimId = _joinedSimId;
			_connection = null;
			_joinedSimId = null;
		}
		finally
		{
			_connectionGate.Release();
		}

		if (connectionToDispose is null)
		{
			_connectionGate.Dispose();
			_disposeCts.Dispose();
			return;
		}

		if (!string.IsNullOrWhiteSpace(joinedSimId))
		{
			try { await connectionToDispose.InvokeAsync("LeaveSim", new object?[] { joinedSimId }, CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
		}

		try { await connectionToDispose.StopAsync().ConfigureAwait(false); } catch { /* ignore */ }
		await connectionToDispose.DisposeAsync().ConfigureAwait(false);

		_connectionGate.Dispose();
		_disposeCts.Dispose();
	}
}
