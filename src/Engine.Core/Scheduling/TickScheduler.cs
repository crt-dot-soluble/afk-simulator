using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Engine.Core.Contracts;

namespace Engine.Core.Scheduling;

/// <summary>
/// Deterministic fixed-timestep scheduler responsible for orchestrating all `ITickConsumer` instances.
/// </summary>
public sealed class TickScheduler
{
    private readonly SortedDictionary<int, List<TickConsumerRegistration>> _pipelines = new();
    private readonly ISystemClock _clock;
    private long _tickDurationTicks;
    private long _tickIndex;
    private readonly object _gate = new();

    public TickScheduler(TimeSpan tickDuration, ISystemClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        UpdateTickDuration(tickDuration);
        _clock = clock;
    }

    public event EventHandler<TickTelemetryEventArgs>? TickExecuted;

    public TimeSpan TickDuration => TimeSpan.FromTicks(Interlocked.Read(ref _tickDurationTicks));

    public void UpdateTickDuration(TimeSpan tickDuration)
    {
        if (tickDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tickDuration), "Tick duration must be positive.");
        }

        Interlocked.Exchange(ref _tickDurationTicks, tickDuration.Ticks);
    }

    public void RegisterConsumer(ITickConsumer consumer, int priority = 0, TickRateProfile? rate = null)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        lock (_gate)
        {
            if (_pipelines.Values.SelectMany(static c => c).Any(c => c.Consumer.Id == consumer.Id))
            {
                throw new InvalidOperationException($"A consumer with id '{consumer.Id}' is already registered.");
            }

            if (!_pipelines.TryGetValue(priority, out var consumers))
            {
                consumers = [];
                _pipelines[priority] = consumers;
            }

            consumers.Add(new TickConsumerRegistration(consumer, rate ?? TickRateProfile.Normal));
        }
    }

    public bool UnregisterConsumer(string consumerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);

        lock (_gate)
        {
            foreach (var (priority, consumers) in _pipelines)
            {
                var removed = consumers.RemoveAll(c => c.Consumer.Id == consumerId);
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

    public bool UpdateConsumerRate(string consumerId, TickRateProfile rate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);
        ArgumentNullException.ThrowIfNull(rate);

        lock (_gate)
        {
            foreach (var consumers in _pipelines.Values)
            {
                var registration = consumers.FirstOrDefault(c => string.Equals(c.Consumer.Id, consumerId, StringComparison.OrdinalIgnoreCase));
                if (registration is null)
                {
                    continue;
                }

                registration.Update(rate);
                return true;
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
            var remaining = TickDuration - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunSingleTickAsync(CancellationToken cancellationToken)
    {
        List<TickConsumerRegistration>[] snapshot;
        lock (_gate)
        {
            snapshot = _pipelines.Values.Select(list => list.ToList()).ToArray();
        }

        var tickDuration = TickDuration;
        var tickTime = _clock.UtcNow;
        var sw = Stopwatch.StartNew();
        var invocationCount = 0;
        foreach (var consumers in snapshot)
        {
            foreach (var consumer in consumers)
            {
                var relativeSpeed = consumer.Profile.RelativeSpeed;
                if (relativeSpeed <= 0d)
                {
                    continue;
                }

                consumer.Accumulator += relativeSpeed;
                var invocations = (int)Math.Floor(consumer.Accumulator);
                if (invocations == 0)
                {
                    continue;
                }

                consumer.Accumulator -= invocations;
                var effectiveDuration = CalculateEffectiveDuration(tickDuration, relativeSpeed);
                for (var i = 0; i < invocations; i++)
                {
                    var context = new TickContext(_tickIndex, tickDuration, tickTime, effectiveDuration, relativeSpeed);
                    await consumer.Consumer.OnTickAsync(context, cancellationToken).ConfigureAwait(false);
                    invocationCount++;
                }
            }
        }

        sw.Stop();
        var consumerCount = snapshot.Sum(list => list.Count);
        TickExecuted?.Invoke(this,
            new TickTelemetryEventArgs(_tickIndex, sw.Elapsed, consumerCount, invocationCount));
        _tickIndex++;
    }

    private static TimeSpan CalculateEffectiveDuration(TimeSpan tickDuration, double relativeSpeed)
    {
        var ticks = tickDuration.Ticks / relativeSpeed;
        var rounded = (long)Math.Max(1d, Math.Round(ticks, MidpointRounding.AwayFromZero));
        return TimeSpan.FromTicks(rounded);
    }

    private sealed class TickConsumerRegistration
    {
        public TickConsumerRegistration(ITickConsumer consumer, TickRateProfile profile)
        {
            Consumer = consumer;
            Profile = profile;
        }

        public ITickConsumer Consumer { get; }
        public TickRateProfile Profile { get; private set; }
        public double Accumulator { get; set; }

        public void Update(TickRateProfile profile)
        {
            Profile = profile;
            Accumulator = 0d;
        }
    }
}

/// <summary>
/// Event payload representing a completed tick.
/// </summary>
public sealed class TickTelemetryEventArgs : EventArgs
{
    public TickTelemetryEventArgs(long tickIndex, TimeSpan elapsed, int consumerCount, int invocationCount)
    {
        TickIndex = tickIndex;
        Elapsed = elapsed;
        ConsumerCount = consumerCount;
        InvocationCount = invocationCount;
    }

    public long TickIndex { get; }
    public TimeSpan Elapsed { get; }
    public int ConsumerCount { get; }
    public int InvocationCount { get; }
}
