using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Engine.Client.Services;

[SuppressMessage("Performance", "CA1812", Justification = "Created via DI")]
internal sealed class RuntimeConfigClient
{
    private readonly HttpClient _httpClient;

    public RuntimeConfigClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RuntimeConfigDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var payload = await _httpClient.GetFromJsonAsync<RuntimeConfigDto>("runtime/config", cancellationToken)
            .ConfigureAwait(false);
        return payload ?? new RuntimeConfigDto();
    }
}

[SuppressMessage("Performance", "CA1812", Justification = "Deserialized from JSON")]
internal sealed class RuntimeConfigDto
{
    [JsonPropertyName("requireLogin")] public bool RequireLogin { get; set; } = true;

    [JsonPropertyName("developerAutoLoginEnabled")] public bool DeveloperAutoLoginEnabled { get; set; }

    [JsonPropertyName("developerAutoLogin")] public DeveloperAutoLoginDto? DeveloperAutoLogin { get; set; }
}

[SuppressMessage("Performance", "CA1812", Justification = "Deserialized from JSON")]
internal sealed class DeveloperAutoLoginDto
{
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
}
