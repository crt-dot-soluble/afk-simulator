namespace Engine.Core.Assets;

public sealed record AssetRecord(
    string Id,
    string Type,
    string Path,
    int Width,
    int Height,
    string Hash,
    IReadOnlyCollection<string> Tags);
