namespace Engine.Core.Rendering;

public sealed record RenderSettings(
    int TargetFps,
    double ResolutionScale,
    bool SpriteSmoothing,
    int ParticleDensity,
    string GpuTier)
{
    public static RenderSettings HighPerformance => new(120, 1.5, true, 100, "tier3");
    public static RenderSettings Balanced => new(60, 1.0, true, 70, "tier2");
    public static RenderSettings BatterySaver => new(30, 0.75, false, 40, "tier1");
}
