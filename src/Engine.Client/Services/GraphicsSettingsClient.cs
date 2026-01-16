using System.Net.Http.Json;
using Engine.Core.Rendering;

namespace Engine.Client.Services;

internal sealed class GraphicsSettingsClient
{
    private readonly HttpClient _httpClient;

    public GraphicsSettingsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RenderSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<RenderSettings>("graphics/settings", cancellationToken)
                   .ConfigureAwait(false)
               ?? RenderSettings.Balanced;
    }

    public async Task<RenderSettings> UpdateAsync(RenderSettings settings,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("graphics/settings", settings, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RenderSettings>(cancellationToken: cancellationToken)
                   .ConfigureAwait(false)
               ?? settings;
    }
}
