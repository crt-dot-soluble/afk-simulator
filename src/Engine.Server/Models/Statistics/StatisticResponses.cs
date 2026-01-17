using System.Collections.Generic;

namespace Engine.Server.Models.Statistics;

internal sealed class StatisticNamespaceResponse
{
    public string NamespaceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<StatisticCategoryResponse> Categories { get; init; } =
        Array.Empty<StatisticCategoryResponse>();
}

internal sealed class StatisticCategoryResponse
{
    public string CategoryId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<StatisticEntryResponse> Entries { get; init; } = Array.Empty<StatisticEntryResponse>();
}

internal sealed class StatisticEntryResponse
{
    public string EntryId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public StatisticValueResponse Value { get; init; } = new();
    public string AccentColor { get; init; } = string.Empty;
    public string DefaultAnimation { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

internal sealed class StatisticValueResponse
{
    public int Level { get; init; }
    public double Experience { get; init; }
    public double BankedCurrency { get; init; }
    public double CurrencyPerSecond { get; init; }
}

internal sealed class StatisticsSnapshotResponse
{
    public string ActiveSkillId { get; init; } = string.Empty;
    public double TotalSkillCurrency { get; init; }

    public IReadOnlyList<StatisticNamespaceResponse> Namespaces { get; init; } =
        Array.Empty<StatisticNamespaceResponse>();
}
