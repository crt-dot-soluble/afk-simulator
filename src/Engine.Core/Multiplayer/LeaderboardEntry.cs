namespace Engine.Core.Multiplayer;

public sealed record LeaderboardEntry(string PlayerId, string DisplayName, double Score, DateTimeOffset Timestamp);
