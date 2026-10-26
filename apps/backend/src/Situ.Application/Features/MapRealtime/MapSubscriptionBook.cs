using Situ.Domain.Reports;

namespace Situ.Application.Features.MapRealtime;

public sealed record MapSubscriptionLimits(double Degrees, int MaxCells, int MaxSubscribeCallsPerMinute);

public sealed record SubscriptionDecision(bool Close, string? Reason, IReadOnlyList<string> Added, IReadOnlyList<string> Removed)
{
    public static SubscriptionDecision Closed(string reason) => new(true, reason, [], []);

    public static SubscriptionDecision Accepted(IReadOnlyList<string> added, IReadOnlyList<string> removed) =>
        new(false, null, added, removed);
}

/// <summary>
/// Cells subscribed by each connection. Memory only, dropped when the connection ends.
/// Nothing here is a user id or a coordinate.
/// </summary>
public sealed class MapSubscriptionBook
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = [];

    public int ConnectionCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public int SubscribedCellCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.SelectMany(entry => entry.Value.Cells).Distinct(StringComparer.Ordinal).Count();
            }
        }
    }

    public void Connect(string connectionId)
    {
        lock (_gate)
        {
            _entries[connectionId] = new Entry();
        }
    }

    public void Disconnect(string connectionId)
    {
        lock (_gate)
        {
            _entries.Remove(connectionId);
        }
    }

    public SubscriptionDecision Subscribe(
        string connectionId,
        IReadOnlyList<string>? cellIds,
        DateTimeOffset now,
        MapSubscriptionLimits limits)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(connectionId, out var entry))
            {
                return SubscriptionDecision.Closed("unknown_connection");
            }

            entry.Calls.RemoveAll(moment => moment <= now.AddMinutes(-1));
            if (entry.Calls.Count >= limits.MaxSubscribeCallsPerMinute)
            {
                return SubscriptionDecision.Closed("subscribe_rate");
            }

            entry.Calls.Add(now);
            if (cellIds is null || cellIds.Any(cellId => !MapCell.IsValid(cellId, limits.Degrees)))
            {
                return SubscriptionDecision.Closed("invalid_cell");
            }

            var unique = cellIds.Distinct(StringComparer.Ordinal).ToArray();
            var next = entry.Cells.Count + unique.Count(cellId => !entry.Cells.Contains(cellId));
            if (next > limits.MaxCells)
            {
                return SubscriptionDecision.Closed("too_many_cells");
            }

            var added = unique.Where(cellId => entry.Cells.Add(cellId)).ToArray();
            return SubscriptionDecision.Accepted(added, []);
        }
    }

    public SubscriptionDecision Unsubscribe(
        string connectionId,
        IReadOnlyList<string>? cellIds,
        MapSubscriptionLimits limits)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(connectionId, out var entry))
            {
                return SubscriptionDecision.Closed("unknown_connection");
            }

            if (cellIds is null || cellIds.Any(cellId => !MapCell.IsValid(cellId, limits.Degrees)))
            {
                return SubscriptionDecision.Closed("invalid_cell");
            }

            var removed = cellIds.Distinct(StringComparer.Ordinal).Where(entry.Cells.Remove).ToArray();
            return SubscriptionDecision.Accepted([], removed);
        }
    }

    private sealed class Entry
    {
        public HashSet<string> Cells { get; } = new(StringComparer.Ordinal);

        public List<DateTimeOffset> Calls { get; } = [];
    }
}
