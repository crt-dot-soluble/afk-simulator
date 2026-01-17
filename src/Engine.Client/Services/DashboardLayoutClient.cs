using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Engine.Core.Contracts;

namespace Engine.Client.Services;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Created via dependency injection")]
internal sealed class DashboardLayoutClient
{
    private readonly HttpClient _httpClient;

    public DashboardLayoutClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<DashboardViewDescriptorDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient
            .GetFromJsonAsync<IReadOnlyList<DashboardViewDescriptorDto>>("dashboard/views", cancellationToken)
            .ConfigureAwait(false);
        return response ?? Array.Empty<DashboardViewDescriptorDto>();
    }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Deserialized from HTTP payloads")]
internal sealed class DashboardViewDescriptorDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("module")] public string Module { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("zone")] public string Zone { get; set; } = DashboardViewZones.Primary;
    [JsonPropertyName("order")] public int Order { get; set; }
    [JsonPropertyName("columnSpan")] public int ColumnSpan { get; set; } = 4;
}
