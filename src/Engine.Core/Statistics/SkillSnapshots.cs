using System.Collections.Generic;

namespace Engine.Core.Statistics;

/// <summary>
/// Immutable snapshot of an individual skill's progress for UI surfaces.
/// </summary>
public sealed record SkillProgressSnapshot(
    string SkillId,
    double Experience,
    int Level,
    double BankedCurrency);

/// <summary>
/// Aggregated snapshot describing the live skill loop.
/// </summary>
public sealed record SkillStateSnapshot(
    string ActiveSkillId,
    double TotalCurrency,
    IReadOnlyCollection<SkillProgressSnapshot> Skills);

/// <summary>
/// High-level view of all statistics grouped by namespace and category.
/// </summary>
public sealed record StatisticsSnapshot(
    string ActiveSkillId,
    double TotalSkillCurrency,
    IReadOnlyCollection<StatisticNamespaceSnapshot> Namespaces);

public sealed record StatisticNamespaceSnapshot(
    string NamespaceId,
    string Name,
    IReadOnlyCollection<StatisticCategorySnapshot> Categories);

public sealed record StatisticCategorySnapshot(
    string CategoryId,
    string Name,
    IReadOnlyCollection<StatisticEntrySnapshot> Entries);

public sealed record StatisticEntrySnapshot(
    string EntryId,
    string Name,
    string Description,
    string Kind,
    StatisticValueSnapshot Value,
    string AccentColor,
    string DefaultAnimation,
    bool IsActive);

public sealed record StatisticValueSnapshot(
    int Level,
    double Experience,
    double BankedCurrency,
    double CurrencyPerSecond);

public static class StatisticNamespaces
{
    public const string Core = "statistics.core";
}

public static class StatisticCategories
{
    public const string Skills = "statistics.skills";
}

public static class StatisticEntryKinds
{
    public const string Skill = "skill";
}
