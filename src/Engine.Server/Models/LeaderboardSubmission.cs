namespace Engine.Server.Models;

public sealed record LeaderboardSubmission(string PlayerId, string DisplayName, double Score);
