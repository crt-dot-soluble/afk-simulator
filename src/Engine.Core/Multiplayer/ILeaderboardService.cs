namespace Engine.Core.Multiplayer;

public interface ILeaderboardService
{
    Task ReportAsync(LeaderboardEntry entry, CancellationToken cancellationToken = default);

    IAsyncEnumerable<LeaderboardEntry> StreamTopAsync(int take, CancellationToken cancellationToken = default);

    IReadOnlyCollection<LeaderboardEntry> Snapshot(int take = 100);
}
