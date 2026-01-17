namespace Engine.Client.Rendering;

internal sealed class SpriteAnimationDescriptor
{
    public string SpriteId { get; init; } = string.Empty;
    public string Animation { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public IReadOnlyList<SpriteAnimationFrameDescriptor> Frames { get; init; } = Array.Empty<SpriteAnimationFrameDescriptor>();
    public double FrameDurationMs { get; init; }
    public bool Loop { get; init; } = true;
    public string AccentColor { get; init; } = "#ffffff";
}

internal sealed class SpriteAnimationFrameDescriptor
{
    public int Index { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
