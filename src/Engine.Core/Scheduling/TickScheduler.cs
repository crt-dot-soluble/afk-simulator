using System.Diagnostics;
using System.Linq;
using Engine.Core.Contracts;

namespace Engine.Core.Scheduling;

/// <summary>
/// Deterministic fixed-timestep scheduler responsible for orchestrating all `ITickConsumer` instances.
/// </summary>
public sealed class TickScheduler
{
    private readonly SortedDictionary<int, List<ITickConsumer>> _pipelines = new();
    private readonly TimeSpan _tickDuration;
    private readonly ISystemClock _clock;
    private long _tickIndex;
    private readonly object _gate = new();

    public TickScheduler(TimeSpan tickDuration, ISystemClock clock)
    {
        if (tickDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tickDuration), "Tick duration must be positive.");
        }

        _tickDuration = tickDuration;
        _clock = clock;
    }

    public event EventHandler<TickTelemetryEventArgs>? TickExecuted;

    public void RegisterConsumer(ITickConsumer consumer, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        lock (_gate)
        {
            if (_pipelines.Values.SelectMany(static c => c).Any(c => c.Id == consumer.Id))
            {
                throw new InvalidOperationException($"A consumer with id '{consumer.Id}' is already registered.");
            }

            if (!_pipelines.TryGetValue(priority, out var consumers))
            {
                consumers = [];
                _pipelines[priority] = consumers;
            }

            consumers.Add(consumer);
        }
    }

    public bool UnregisterConsumer(string consumerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);

        lock (_gate)
        {
            foreach (var (priority, consumers) in _pipelines)
            {
                var removed = consumers.RemoveAll(c => c.Id == consumerId);
                if (removed > 0)
                {
                    if (consumers.Count == 0)
                    {
                        _pipelines.Remove(priority);
                    }

                    return true;
                }
            }
        }

        return false;
    }

    public async Task RunTicksAsync(int tickCount, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickCount);

        for (var i = 0; i < tickCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RunSingleTickAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RunContinuouslyAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var tickStart = _clock.UtcNow;
            await RunSingleTickAsync(cancellationToken).ConfigureAwait(false);
            var elapsed = _clock.UtcNow - tickStart;
            var remaining = _tickDuration - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunSingleTickAsync(CancellationToken cancellationToken)
    {
        List<ITickConsumer>[] snapshot;
        lock (_gate)
        {
            snapshot = _pipelines.Values.Select(list => list.ToList()).ToArray();
        }

        var context = new TickContext(_tickIndex, _tickDuration, _clock.UtcNow);
        var sw = Stopwatch.StartNew();
        foreach (var consumers in snapshot)
        {
            foreach (var consumer in consumers)
            {
                await consumer.OnTickAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }

        sw.Stop();
        var consumerCount = snapshot.Sum(list => list.Count);
        TickExecuted?.Invoke(this, new TickTelemetryEventArgs(context.TickIndex, sw.Elapsed, consumerCount));
        _tickIndex++;
    }
}

    /// <summary>
    /// Event payload representing a completed tick.
    /// </summary>
    public sealed class TickTelemetryEventArgs : EventArgs
    {
        public TickTelemetryEventArgs(long tickIndex, TimeSpan elapsed, int consumerCount)
        {
            TickIndex = tickIndex;
            Elapsed = elapsed;
            ConsumerCount = consumerCount;
        }

        public long TickIndex { get; }
        public TimeSpan Elapsed { get; }
        public int ConsumerCount { get; }
    }
