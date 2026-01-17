namespace Engine.Server.Models.Sprites;

internal sealed class SpriteAnimationResponse
{
    public string Name { get; init; } = string.Empty;
    public double FrameDurationMs { get; init; }
    public bool Loop { get; init; }

    public IReadOnlyList<SpriteAnimationFrameResponse> Frames { get; init; } =
        Array.Empty<SpriteAnimationFrameResponse>();
}
