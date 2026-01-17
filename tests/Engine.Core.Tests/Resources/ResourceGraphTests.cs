using Engine.Core.Resources;

namespace Engine.Core.Tests.Resources;

internal sealed class ResourceGraphTests
{
    [Fact]
    public void AdvanceTransfersResourcesAlongEdges()
    {
        var graph = new ResourceGraph();
        graph.UpsertNode(new ResourceNodeDefinition("ore", 1000, 10));
        graph.UpsertNode(new ResourceNodeDefinition("ingot", 1000));
        graph.UpsertEdge(new ResourceEdgeDefinition("smelt", "ore", "ingot", 5, 0.5));

        var snapshot = graph.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(50, snapshot["ore"].Value, 3);
        Assert.Equal(25, snapshot["ingot"].Value, 3);
    }
}
