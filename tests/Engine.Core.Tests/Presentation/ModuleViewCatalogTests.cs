using System;
using System.Collections.Generic;
using Engine.Core.Contracts;
using Engine.Core.Presentation;
using Xunit;

namespace Engine.Core.Tests.Presentation;

internal sealed class ModuleViewCatalogTests
{
    [Fact]
    public void ListWithoutProvidersReturnsEmpty()
    {
        var catalog = new ModuleViewCatalog(Array.Empty<IModuleViewProvider>());

        var documents = catalog.List();

        Assert.NotNull(documents);
        Assert.Empty(documents);
    }

    [Fact]
    public void ListOrdersDocumentsByZoneOrderAndTitle()
    {
        var heroDescriptor = new DashboardViewDescriptor("core.simulation", "Core", "Simulation", "GPU viewport",
            DashboardViewZones.Hero, 5, 9);
        var statsDescriptor = new DashboardViewDescriptor("statistics.panel", "Statistics", "Stats", "Totals",
            DashboardViewZones.Primary, 1, 4);
        var leaderboardDescriptor = new DashboardViewDescriptor("core.leaderboard", "Core", "Leaderboard",
            "Scores", DashboardViewZones.Secondary, 1, 4);

        var docA = new ModuleViewDocument(heroDescriptor, Array.Empty<ModuleViewBlock>());
        var docB = new ModuleViewDocument(statsDescriptor, Array.Empty<ModuleViewBlock>());
        var docC = new ModuleViewDocument(leaderboardDescriptor, Array.Empty<ModuleViewBlock>());

        var providers = new IModuleViewProvider[]
        {
            new FakeModuleViewProvider(new[] { docC, docB }),
            new FakeModuleViewProvider(new[] { docA })
        };

        var catalog = new ModuleViewCatalog(providers);

        var documents = catalog.List();

        Assert.Collection(documents,
            doc => Assert.Equal(heroDescriptor.Id, doc.Descriptor.Id),
            doc => Assert.Equal(statsDescriptor.Id, doc.Descriptor.Id),
            doc => Assert.Equal(leaderboardDescriptor.Id, doc.Descriptor.Id));
    }

    private sealed class FakeModuleViewProvider : IModuleViewProvider
    {
        private readonly IReadOnlyCollection<ModuleViewDocument> _documents;

        public FakeModuleViewProvider(IReadOnlyCollection<ModuleViewDocument> documents)
        {
            _documents = documents;
        }

        public IReadOnlyCollection<ModuleViewDocument> DescribeModuleViews(ModuleViewContext context) => _documents;
    }
}
