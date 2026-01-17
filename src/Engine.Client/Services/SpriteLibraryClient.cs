using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Engine.Client.Services;

[SuppressMessage("Performance", "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Activated via HttpClientFactory.")]
internal sealed class SpriteLibraryClient
{
    private readonly HttpClient _httpClient;

    public SpriteLibraryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SpriteDefinitionDto?> GetAsync(string spriteId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        return await _httpClient.GetFromJsonAsync<SpriteDefinitionDto>($"assets/sprites/{spriteId}", cancellationToken)
            .ConfigureAwait(false);
    }
}

[SuppressMessage("Performance", "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Created by System.Text.Json")]
internal sealed class SpriteDefinitionDto
{
    [JsonPropertyName("spriteId")] public string SpriteId { get; set; } = string.Empty;

    [JsonPropertyName("assetPath")] public string AssetPath { get; set; } = string.Empty;

    [JsonPropertyName("frameWidth")] public int FrameWidth { get; set; }

    [JsonPropertyName("frameHeight")] public int FrameHeight { get; set; }

    [JsonPropertyName("defaultAnimation")] public string DefaultAnimation { get; set; } = string.Empty;

    [JsonPropertyName("animations")]
    public IReadOnlyList<SpriteAnimationDto> Animations { get; set; } = Array.Empty<SpriteAnimationDto>();
}

[SuppressMessage("Performance", "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Created by System.Text.Json")]
internal sealed class SpriteAnimationDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("frameDurationMs")] public double FrameDurationMs { get; set; }

    [JsonPropertyName("loop")] public bool Loop { get; set; }

    [JsonPropertyName("frames")]
    public IReadOnlyList<SpriteAnimationFrameDto> Frames { get; set; } = Array.Empty<SpriteAnimationFrameDto>();
}

[SuppressMessage("Performance", "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Created by System.Text.Json")]
internal sealed class SpriteAnimationFrameDto
{
    [JsonPropertyName("index")] public int Index { get; set; }

    [JsonPropertyName("x")] public int X { get; set; }

    [JsonPropertyName("y")] public int Y { get; set; }

    [JsonPropertyName("width")] public int Width { get; set; }

    [JsonPropertyName("height")] public int Height { get; set; }
}
