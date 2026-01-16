using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;

namespace Engine.Core.Assets;

/// <summary>
/// Machine-readable manifest describing all assets contributed by modules for hot-loading and validation.
/// </summary>
public sealed class AssetManifest
{
    private readonly Dictionary<string, AssetRecord> _assets = new(StringComparer.OrdinalIgnoreCase);

    public AssetRecord Register(string id, string type, string path, int width, int height, Stream contentStream, params string[] tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contentStream);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive.");
        }

        var hash = ComputeHash(contentStream);
        var record = new AssetRecord(id, type, path, width, height, hash, tags);
        _assets[id] = record;
        return record;
    }

    public bool TryGet(string id, out AssetRecord record) => _assets.TryGetValue(id, out record!);

    public IReadOnlyCollection<AssetRecord> FindByTag(string tag) =>
        _assets.Values.Where(asset => asset.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToImmutableArray();

    private static string ComputeHash(Stream content)
    {
        content.Position = 0;
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(content);
        return Convert.ToHexString(hash);
    }
}
