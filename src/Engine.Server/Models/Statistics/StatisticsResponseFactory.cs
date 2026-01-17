using System.Collections.Generic;
using System.Linq;
using Engine.Core.Statistics;

namespace Engine.Server.Models.Statistics;

internal static class StatisticsResponseFactory
{
    public static StatisticsSnapshotResponse CreateSnapshot(StatisticsSnapshot snapshot)
    {
        return new StatisticsSnapshotResponse
        {
            ActiveSkillId = snapshot.ActiveSkillId,
            TotalSkillCurrency = snapshot.TotalSkillCurrency,
            Namespaces = snapshot.Namespaces.Select(CreateNamespace).ToArray()
        };
    }

    private static StatisticNamespaceResponse CreateNamespace(StatisticNamespaceSnapshot snapshot)
    {
        return new StatisticNamespaceResponse
        {
            NamespaceId = snapshot.NamespaceId,
            Name = snapshot.Name,
            Categories = snapshot.Categories.Select(CreateCategory).ToArray()
        };
    }

    private static StatisticCategoryResponse CreateCategory(StatisticCategorySnapshot snapshot)
    {
        return new StatisticCategoryResponse
        {
            CategoryId = snapshot.CategoryId,
            Name = snapshot.Name,
            Entries = snapshot.Entries.Select(CreateEntry).ToArray()
        };
    }

    private static StatisticEntryResponse CreateEntry(StatisticEntrySnapshot snapshot)
    {
        return new StatisticEntryResponse
        {
            EntryId = snapshot.EntryId,
            Name = snapshot.Name,
            Description = snapshot.Description,
            Kind = snapshot.Kind,
            Value = new StatisticValueResponse
            {
                Level = snapshot.Value.Level,
                Experience = snapshot.Value.Experience,
                BankedCurrency = snapshot.Value.BankedCurrency,
                CurrencyPerSecond = snapshot.Value.CurrencyPerSecond
            },
            AccentColor = snapshot.AccentColor,
            DefaultAnimation = snapshot.DefaultAnimation,
            IsActive = snapshot.IsActive
        };
    }
}
