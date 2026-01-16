namespace Engine.Core.Rendering;

public static class RenderSettingsValidator
{
    private static readonly HashSet<string> AllowedTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "tier1",
        "tier2",
        "tier3"
    };

    public static void Validate(RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.TargetFps is < 30 or > 240)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Target FPS must be between 30 and 240.");
        }

        if (settings.ResolutionScale is < 0.25 or > 2.5)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Resolution scale must remain between 0.25 and 2.5.");
        }

        if (settings.ParticleDensity is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Particle density must be between 0 and 100.");
        }

        if (!AllowedTiers.Contains(settings.GpuTier))
        {
            throw new ArgumentException($"GPU tier '{settings.GpuTier}' is not recognized.");
        }
    }
}
