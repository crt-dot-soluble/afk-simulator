namespace Engine.Core.Multiplayer;

public interface IMultiplayerSessionService
{
    ValueTask<SessionDescriptor> CreateSessionAsync(string name, CancellationToken cancellationToken = default);

    ValueTask<bool> JoinSessionAsync(string sessionId, string playerId, CancellationToken cancellationToken = default);

    ValueTask<bool> LeaveSessionAsync(string sessionId, string playerId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<SessionDescriptor> ListSessionsAsync(CancellationToken cancellationToken = default);
}

public sealed record SessionDescriptor(string Id, string Name, int PlayerCount, DateTimeOffset CreatedAt);
