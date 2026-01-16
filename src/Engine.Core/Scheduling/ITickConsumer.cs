namespace Engine.Core.Scheduling;

/// <summary>
/// Represents a deterministic consumer that participates in the simulation tick pipeline.
/// </summary>
public interface ITickConsumer
{
    string Id { get; }

    ValueTask OnTickAsync(TickContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable context describing the tick being executed and scheduler metadata.
/// </summary>
/// <param name="TickIndex">Monotonic tick number starting at zero.</param>
/// <param name="TickDuration">Fixed timestep used by the scheduler.</param>
/// <param name="AbsoluteTime">Derived absolute UTC time for the tick start.</param>
public sealed record TickContext(long TickIndex, TimeSpan TickDuration, DateTimeOffset AbsoluteTime);
