using System.Collections.Concurrent;
using SkyNet.Simulator.Contracts;

namespace SkyNet.Simulator.Daemon;

public sealed class InMemoryLogStore
{
	private readonly ConcurrentQueue<LogEntryDto> _entries = new();
	private long _seq;
	private readonly int _maxEntries;

	public InMemoryLogStore(int maxEntries = 2000)
	{
		_maxEntries = Math.Max(100, maxEntries);
	}

	public void Add(string simId, string level, string message)
	{
		var entry = new LogEntryDto(
			Seq: Interlocked.Increment(ref _seq),
			Timestamp: DateTimeOffset.UtcNow,
			SimId: simId,
			Level: level,
			Message: message);

		_entries.Enqueue(entry);
		TrimIfNeeded();
	}

	public LogBatchDto Get(long after, int take)
	{
		take = Math.Clamp(take, 1, 500);

		// ConcurrentQueue doesn't support efficient range reads.
		// For our small in-memory diagnostics use-case, a snapshot is fine.
		var snapshot = _entries.ToArray();
		var filtered = snapshot
			.Where(e => e.Seq > after)
			.OrderBy(e => e.Seq)
			.Take(take)
			.ToArray();

		var nextAfter = filtered.Length == 0 ? after : filtered[^1].Seq;
		return new LogBatchDto(NextAfter: nextAfter, Entries: filtered);
	}

	private void TrimIfNeeded()
	{
		// Best-effort trimming.
		while (_entries.Count > _maxEntries && _entries.TryDequeue(out _))
		{
		}
	}
}
