namespace Engine.Core.Multiplayer;

public interface IMultiplayerSessionService
{
    ValueTask<SessionDescriptor> CreateSessionAsync(string name, CancellationToken cancellationToken = default);

    ValueTask<bool> JoinSessionAsync(string sessionId, string playerId, CancellationToken cancellationToken = default);

    ValueTask<bool> LeaveSessionAsync(string sessionId, string playerId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<SessionDescriptor> ListSessionsAsync(CancellationToken cancellationToken = default);

    IReadOnlyCollection<SessionDescriptor> Snapshot(int take = 100);
}

public sealed record SessionDescriptor(string Id, string Name, int PlayerCount, DateTimeOffset CreatedAt);
