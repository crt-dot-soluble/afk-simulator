using Engine.Core.Assets;

namespace Engine.Core.Rendering.Sprites;

public sealed class SpriteLibrary
{
    private readonly AssetManifest _assetManifest;
    private readonly Dictionary<string, SpriteDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    public SpriteLibrary(AssetManifest assetManifest)
    {
        _assetManifest = assetManifest;
    }

    public SpriteDefinition RegisterSingleFrame(string spriteId, string assetId, string defaultAnimation,
        IEnumerable<SpriteAnimationClip> animations)
    {
        var asset = GetAsset(assetId);
        var layout = SpriteSheetLayout.SingleFrame(asset.Width, asset.Height);
        return RegisterInternal(spriteId, asset, layout, animations, defaultAnimation);
    }

    public SpriteDefinition RegisterSheet(string spriteId, string assetId, SpriteSheetLayout layout,
        string defaultAnimation, IEnumerable<SpriteAnimationClip> animations)
    {
        var asset = GetAsset(assetId);
        return RegisterInternal(spriteId, asset, layout, animations, defaultAnimation);
    }

    public bool TryGet(string spriteId, out SpriteDefinition definition)
    {
        return _definitions.TryGetValue(spriteId, out definition!);
    }

    public IReadOnlyCollection<SpriteDefinition> List() => _definitions.Values;

    private SpriteDefinition RegisterInternal(string spriteId, AssetRecord asset, SpriteSheetLayout layout,
        IEnumerable<SpriteAnimationClip> animations, string defaultAnimation)
    {
        var definition = new SpriteDefinition(spriteId, asset.Id, asset.Path, layout, animations, defaultAnimation);
        _definitions[spriteId] = definition;
        return definition;
    }

    private AssetRecord GetAsset(string assetId)
    {
        if (!_assetManifest.TryGet(assetId, out var asset))
        {
            throw new InvalidOperationException($"Asset '{assetId}' was not registered in the manifest.");
        }

        return asset;
    }
}
