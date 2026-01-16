using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.Core.DeveloperTools;

namespace Engine.Core.Tests.DeveloperTools;

public sealed class DeveloperProfileStoreTests
{
    [Fact]
    public void PersistsProfilesToDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "profiles.json");
        var store = new DeveloperProfileStore(new DeveloperProfileStoreOptions(path));
        store.Upsert("alpha", new Dictionary<string, string> { ["note"] = "first" });
        store.Upsert("beta", new Dictionary<string, string> { ["note"] = "second" });

        var rehydrated = new DeveloperProfileStore(new DeveloperProfileStoreOptions(path));
        var snapshot = rehydrated.List();
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, profile => profile.Id == "alpha");

        store.Clear();
        Assert.Empty(store.List());
    }
}
