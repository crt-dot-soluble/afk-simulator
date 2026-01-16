using System;
using Engine.Core.Rendering;

namespace Engine.Core.Tests.Rendering;

public sealed class RenderSettingsStoreTests
{
    [Fact]
    public void UpdatePersistsLatestSettings()
    {
        var store = new RenderSettingsStore();
        var request = new RenderSettings(90, 1.25, true, 60, "tier2");

        var updated = store.Update(request);

        Assert.Equal(request, updated);
        Assert.Equal(updated, store.Current);
    }

    [Fact]
    public void UpdateValidatesIncomingSettings()
    {
        var store = new RenderSettingsStore();
        var invalid = new RenderSettings(10, 1.0, true, 50, "tier2");

        Assert.Throws<ArgumentOutOfRangeException>(() => store.Update(invalid));
    }
}
