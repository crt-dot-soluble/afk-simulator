namespace Engine.Core.Statistics;

/// <summary>
/// Immutable descriptor for a statistic-driven skill surfaced to the gameplay shell.
/// </summary>
/// <param name="Id">Stable identifier used by clients when activating the skill.</param>
/// <param name="Name">Display name rendered in the UX.</param>
/// <param name="Description">Short lore-friendly description.</param>
/// <param name="CurrencyPerSecond">Base currency awarded per second when active.</param>
/// <param name="DefaultAnimation">Animation clip identifier used for the hero viewport.</param>
/// <param name="AccentColor">Hex color accent applied across the UI.</param>
public sealed record SkillDefinition(
    string Id,
    string Name,
    string Description,
    double CurrencyPerSecond,
    string DefaultAnimation,
    string AccentColor);
