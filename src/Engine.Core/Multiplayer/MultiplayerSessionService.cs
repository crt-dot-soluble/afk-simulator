using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Engine.Core.Multiplayer;

public sealed class MultiplayerSessionService : IMultiplayerSessionService
{
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new();

    public ValueTask<SessionDescriptor> CreateSessionAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        var id = GenerateSessionId(name);
        var state = _sessions.GetOrAdd(id, _ => new SessionState(name));
        return ValueTask.FromResult(state.Describe(id));
    }

    public ValueTask<bool> JoinSessionAsync(string sessionId, string playerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        cancellationToken.ThrowIfCancellationRequested();

        if (_sessions.TryGetValue(sessionId, out var state))
        {
            state.Join(playerId);
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> LeaveSessionAsync(string sessionId, string playerId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var state))
        {
            state.Leave(playerId);
            if (state.PlayerCount == 0)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }

    public async IAsyncEnumerable<SessionDescriptor> ListSessionsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var (id, state) in _sessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return state.Describe(id);
            await Task.Yield();
        }
    }

    public IReadOnlyCollection<SessionDescriptor> Snapshot(int take = 100)
    {
        if (take <= 0)
        {
            return Array.Empty<SessionDescriptor>();
        }

        return _sessions
            .Select(pair => pair.Value.Describe(pair.Key))
            .OrderByDescending(descriptor => descriptor.PlayerCount)
            .ThenByDescending(descriptor => descriptor.CreatedAt)
            .Take(take)
            .ToArray();
    }

    private static string GenerateSessionId(string seed)
    {
        var bytes = Encoding.UTF8.GetBytes($"{seed}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        var hashed = SHA256.HashData(bytes);
        return Convert.ToHexString(hashed.AsSpan(0, 8));
    }

    private sealed class SessionState
    {
        private readonly HashSet<string> _players = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new();

        public SessionState(string name)
        {
            Name = name;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public string Name { get; }
        public DateTimeOffset CreatedAt { get; }
        public int PlayerCount => _players.Count;

        public void Join(string playerId)
        {
            lock (_gate)
            {
                _players.Add(playerId);
            }
        }

        public void Leave(string playerId)
        {
            lock (_gate)
            {
                _players.Remove(playerId);
            }
        }

        public SessionDescriptor Describe(string id)
        {
            lock (_gate)
            {
                return new SessionDescriptor(id, Name, _players.Count, CreatedAt);
            }
        }
    }
}
