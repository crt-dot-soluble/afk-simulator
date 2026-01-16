using Engine.Core.Rendering;

namespace Engine.Core.Tests.Rendering;

public sealed class RenderSettingsValidatorTests
{
    [Fact]
    public void ValidateAllowsBalancedPreset()
    {
        var exception = Record.Exception(() => RenderSettingsValidator.Validate(RenderSettings.Balanced));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(10, 1, 50, true, "tier1")]
    [InlineData(60, 3, 50, true, "tier2")]
    [InlineData(60, 1, 150, true, "tier2")]
    [InlineData(60, 1, 50, true, "tier9")]
    public void ValidateThrowsWhenOutsideBounds(int fps, double scale, int particles, bool smoothing, string gpuTier)
    {
        var settings = new RenderSettings(fps, scale, smoothing, particles, gpuTier);
        Assert.ThrowsAny<Exception>(() => RenderSettingsValidator.Validate(settings));
    }
}
