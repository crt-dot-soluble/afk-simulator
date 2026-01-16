using System.Linq;
using Engine.Core.Multiplayer;

namespace Engine.Core.Tests.Multiplayer;

public sealed class LeaderboardServiceTests
{
    [Fact]
    public async Task ReportAsyncOrdersDescending()
    {
        var service = new InMemoryLeaderboardService();
        await service.ReportAsync(new LeaderboardEntry("a", "Alpha", 10_000, DateTimeOffset.UtcNow));
        await service.ReportAsync(new LeaderboardEntry("b", "Beta", 12_000, DateTimeOffset.UtcNow));

        var snapshot = service.Snapshot();
        Assert.Equal("Beta", snapshot.First().DisplayName);
    }
}
