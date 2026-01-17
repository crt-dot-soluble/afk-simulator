namespace Engine.Server.Models.Sprites;

internal sealed class SpriteDefinitionResponse
{
    public string SpriteId { get; init; } = string.Empty;
    public string AssetPath { get; init; } = string.Empty;
    public int FrameWidth { get; init; }
    public int FrameHeight { get; init; }
    public string DefaultAnimation { get; init; } = string.Empty;
    public IReadOnlyList<SpriteAnimationResponse> Animations { get; init; } = Array.Empty<SpriteAnimationResponse>();
}
