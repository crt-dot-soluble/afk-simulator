using Engine.Core.Contracts;
using Engine.Core.Scheduling;
using Engine.Core.Time;

namespace Engine.Core.Tests.Scheduling;

public sealed class TickSchedulerTests
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
    }

    [Fact]
    public void RegisterConsumerThrowsOnDuplicateIds()
    {
        var scheduler = new TickScheduler(TimeSpan.FromMilliseconds(100), new TestClock());
        var consumer = new RecordingConsumer("dup");
        scheduler.RegisterConsumer(consumer);

        Assert.Throws<InvalidOperationException>(() => scheduler.RegisterConsumer(new RecordingConsumer("dup")));
    }

    private sealed class RecordingConsumer(string id) : ITickConsumer
    {
        public string Id { get; } = id;
        public List<long> SeenTicks { get; } = new();

        public ValueTask OnTickAsync(TickContext context, CancellationToken cancellationToken = default)
        {
            SeenTicks.Add(context.TickIndex);
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
