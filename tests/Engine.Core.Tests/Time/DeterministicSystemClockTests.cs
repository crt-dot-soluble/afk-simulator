using System.Threading;
using Engine.Core.Time;

namespace Engine.Core.Tests.Time;

internal sealed class DeterministicSystemClockTests
{
    [Fact]
    public void UtcNowAdvancesFromOrigin()
    {
        var origin = DateTimeOffset.UtcNow;
        var clock = new DeterministicSystemClock(origin);

        var first = clock.UtcNow;
        Thread.Sleep(10);
        var second = clock.UtcNow;

        Assert.True(second > first);
        Assert.True(first >= origin);
    }
}
