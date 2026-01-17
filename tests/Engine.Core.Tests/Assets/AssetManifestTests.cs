using Engine.Core.Assets;

namespace Engine.Core.Tests.Assets;

internal sealed class AssetManifestTests
{
    [Fact]
    public void RegisterComputesHashAndRetrievable()
    {
        var manifest = new AssetManifest();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var record = manifest.Register("test", "sprite", "assets/test.png", 32, 32, stream, "ui");

        Assert.True(manifest.TryGet("test", out var stored));
        Assert.Equal(record.Hash, stored!.Hash);
        Assert.Single(manifest.FindByTag("ui"));
    }
}
