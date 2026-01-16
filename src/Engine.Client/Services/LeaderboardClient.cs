using System.Net.Http.Json;
using Engine.Core.Multiplayer;

namespace Engine.Client.Services;

internal sealed class LeaderboardClient
{
    private readonly HttpClient _httpClient;

    public LeaderboardClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<LeaderboardEntry>>("leaderboard", cancellationToken)
            .ConfigureAwait(false);
        return response ?? new List<LeaderboardEntry>();
    }

    public async Task SubmitAsync(string playerId, string displayName, double score, CancellationToken cancellationToken = default)
    {
        var payload = new { playerId, displayName, score };
        var response = await _httpClient.PostAsJsonAsync("leaderboard", payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
