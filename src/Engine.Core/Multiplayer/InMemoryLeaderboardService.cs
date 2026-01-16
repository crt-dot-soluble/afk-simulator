using System.Collections.Immutable;

namespace Engine.Core.Multiplayer;

public sealed class InMemoryLeaderboardService : ILeaderboardService
{
    private readonly SortedSet<LeaderboardEntry> _entries = new(new LeaderboardComparer());
    private readonly object _gate = new();

    public Task ReportAsync(LeaderboardEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _entries.RemoveWhere(existing => existing.PlayerId == entry.PlayerId);
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<LeaderboardEntry> StreamTopAsync(int take, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var snapshot = Snapshot(take);
        foreach (var entry in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
            await Task.Yield();
        }
    }

    public IReadOnlyCollection<LeaderboardEntry> Snapshot(int take = 100)
    {
        lock (_gate)
        {
            return _entries.Take(take).ToImmutableArray();
        }
    }

    private sealed class LeaderboardComparer : IComparer<LeaderboardEntry>
    {
        public int Compare(LeaderboardEntry? x, LeaderboardEntry? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var scoreComparison = -x.Score.CompareTo(y.Score);
            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            return string.CompareOrdinal(x.PlayerId, y.PlayerId);
        }
    }
}
