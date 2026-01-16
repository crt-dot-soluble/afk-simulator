using System.Net.Http.Json;
using Engine.Core.Multiplayer;

namespace Engine.Client.Services;

public sealed class SessionClient
{
    private readonly HttpClient _httpClient;

    public SessionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<SessionDescriptor>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<SessionDescriptor>>("sessions", cancellationToken);
        return response ?? new List<SessionDescriptor>();
    }

    public async Task<SessionDescriptor> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var payload = new { name };
        var response = await _httpClient.PostAsJsonAsync("sessions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionDescriptor>(cancellationToken: cancellationToken))!;
    }

    public async Task JoinAsync(string sessionId, string playerId, CancellationToken cancellationToken = default)
    {
        var payload = new { playerId };
        var response = await _httpClient.PostAsJsonAsync($"sessions/{sessionId}/join", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task LeaveAsync(string sessionId, string playerId, CancellationToken cancellationToken = default)
    {
        var payload = new { playerId };
        var response = await _httpClient.PostAsJsonAsync($"sessions/{sessionId}/leave", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
