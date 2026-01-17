using System.Linq;
using Engine.Core.Contracts;
using Engine.Core.Scheduling;
using Engine.Core.Time;

namespace Engine.Core.Tests.Scheduling;

internal sealed class TickSchedulerTests
{
    [Fact]
    public async Task RunTicksAsyncInvokesConsumersDeterministically()
    {
        var clock = new TestClock();
        var scheduler = new TickScheduler(TimeSpan.FromMilliseconds(100), clock);
        var consumer = new RecordingConsumer("core");
        scheduler.RegisterConsumer(consumer);

        await scheduler.RunTicksAsync(5, CancellationToken.None);

        Assert.Equal(new long[] { 0, 1, 2, 3, 4 }, consumer.SeenTicks);
        Assert.All(consumer.EffectiveDurations, duration => Assert.Equal(TimeSpan.FromMilliseconds(100), duration));
    }

    [Fact]
    public void RegisterConsumerThrowsOnDuplicateIds()
    {
        var scheduler = new TickScheduler(TimeSpan.FromMilliseconds(100), new TestClock());
        var consumer = new RecordingConsumer("dup");
        scheduler.RegisterConsumer(consumer);

        Assert.Throws<InvalidOperationException>(() => scheduler.RegisterConsumer(new RecordingConsumer("dup")));
    }

    [Fact]
    public async Task TickRateProfilesAdjustInvocationFrequency()
    {
        var clock = new TestClock();
        var scheduler = new TickScheduler(TimeSpan.FromMilliseconds(100), clock);
        var slow = new RecordingConsumer("slow");
        var fast = new RecordingConsumer("fast");
        scheduler.RegisterConsumer(slow, rate: new TickRateProfile(0.5d));
        scheduler.RegisterConsumer(fast, rate: new TickRateProfile(2d));

        await scheduler.RunTicksAsync(4, CancellationToken.None);

        Assert.Equal(2, slow.SeenTicks.Count);
        Assert.All(slow.EffectiveDurations, duration => Assert.Equal(TimeSpan.FromMilliseconds(200), duration));
        Assert.Equal(8, fast.SeenTicks.Count);
        Assert.All(fast.EffectiveDurations, duration => Assert.Equal(TimeSpan.FromMilliseconds(50), duration));
    }

    [Fact]
    public void UpdateTickDurationChangesSchedulerProperty()
    {
        var scheduler = new TickScheduler(TimeSpan.FromMilliseconds(100), new TestClock());
        scheduler.UpdateTickDuration(TimeSpan.FromMilliseconds(40));

        Assert.Equal(TimeSpan.FromMilliseconds(40), scheduler.TickDuration);
    }

    [Fact]
    public async Task UpdateConsumerRateTakesEffectImmediately()
    {
        var clock = new TestClock();
        var scheduler = new TickScheduler(TimeSpan.FromMilliseconds(100), clock);
        var consumer = new RecordingConsumer("dynamic");
        scheduler.RegisterConsumer(consumer, rate: new TickRateProfile(0.5d));

        await scheduler.RunTicksAsync(2, CancellationToken.None);
        scheduler.UpdateConsumerRate("dynamic", new TickRateProfile(2d));
        await scheduler.RunTicksAsync(2, CancellationToken.None);

        Assert.Equal(1, consumer.EffectiveDurations.Count(duration => duration == TimeSpan.FromMilliseconds(200)));
        Assert.Equal(4, consumer.EffectiveDurations.Count(duration => duration == TimeSpan.FromMilliseconds(50)));
    }

    private sealed class RecordingConsumer(string id) : ITickConsumer
    {
        public string Id { get; } = id;
        public List<long> SeenTicks { get; } = new();
        public List<TimeSpan> EffectiveDurations { get; } = new();

        public ValueTask OnTickAsync(TickContext context, CancellationToken cancellationToken = default)
        {
            SeenTicks.Add(context.TickIndex);
            EffectiveDurations.Add(context.EffectiveDuration);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestClock : ISystemClock
    {
        private DateTimeOffset _time = DateTimeOffset.UnixEpoch;

        public DateTimeOffset UtcNow
        {
            get
            {
                _time = _time.AddMilliseconds(100);
                return _time;
            }
        }
    }
}
