using System.Collections.Immutable;

namespace Engine.Core.Rendering.Sprites;

public sealed class SpriteDefinition
{
    public SpriteDefinition(string spriteId, string assetId, string assetPath, SpriteSheetLayout layout,
        IEnumerable<SpriteAnimationClip> animations, string defaultAnimation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(animations);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultAnimation);

        SpriteId = spriteId;
        AssetId = assetId;
        AssetPath = assetPath;
        Layout = layout;
        Animations = animations.ToImmutableDictionary(static clip => clip.Name, StringComparer.OrdinalIgnoreCase);
        if (Animations.Count == 0)
        {
            throw new ArgumentException("Sprite must declare at least one animation.", nameof(animations));
        }

        if (!Animations.ContainsKey(defaultAnimation))
        {
            throw new ArgumentException($"Default animation '{defaultAnimation}' not found.", nameof(defaultAnimation));
        }

        DefaultAnimation = defaultAnimation;
    }

    public string SpriteId { get; }
    public string AssetId { get; }
    public string AssetPath { get; }
    public SpriteSheetLayout Layout { get; }
    public IReadOnlyDictionary<string, SpriteAnimationClip> Animations { get; }
    public string DefaultAnimation { get; }

    public bool TryGetAnimation(string name, out SpriteAnimationClip clip)
    {
        return Animations.TryGetValue(name, out clip!);
    }
}
