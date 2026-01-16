using System.Diagnostics;
using Engine.Core.Contracts;

namespace Engine.Core.Time;

/// <summary>
/// Provides a deterministic clock that can be seeded for reproducible simulations.
/// </summary>
public sealed class DeterministicSystemClock : ISystemClock
{
    private readonly DateTimeOffset _origin;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public DeterministicSystemClock(DateTimeOffset? origin = null)
    {
        _origin = origin ?? DateTimeOffset.UnixEpoch;
    }

    public DateTimeOffset UtcNow => _origin.Add(_stopwatch.Elapsed);
}
