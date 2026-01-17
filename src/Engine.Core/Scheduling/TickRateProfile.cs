using System;

namespace Engine.Core.Scheduling;

/// <summary>
/// Describes how frequently a consumer should execute relative to the global tick rate.
/// </summary>
public sealed class TickRateProfile
{
    public const double MinRelativeSpeed = 0.05d;
    public const double MaxRelativeSpeed = 20d;

    public TickRateProfile(double relativeSpeed, string? label = null)
    {
        if (double.IsNaN(relativeSpeed) || double.IsInfinity(relativeSpeed))
        {
            throw new ArgumentOutOfRangeException(nameof(relativeSpeed), "Relative speed must be finite.");
        }

        var clamped = Math.Clamp(relativeSpeed, MinRelativeSpeed, MaxRelativeSpeed);
        RelativeSpeed = clamped;
        Label = string.IsNullOrWhiteSpace(label) ? $"x{clamped:0.##}" : label;
    }

    /// <summary>
    /// Multiplier relative to the baseline tick duration (1.0 == default speed).
    /// </summary>
    public double RelativeSpeed { get; }

    /// <summary>
    /// Descriptive label useful for tooling surfaces.
    /// </summary>
    public string Label { get; }

    public static TickRateProfile Normal { get; } = new(1d, "Normal");

    public override string ToString() => $"TickRateProfile({Label})";
}
