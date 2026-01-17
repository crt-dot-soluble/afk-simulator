using System.Collections.Generic;

namespace Engine.Core.Statistics;

internal sealed record StatisticsPersistenceModel(string ActiveSkillId, double TotalCurrency,
    IReadOnlyList<StatisticsSkillPersistenceModel> Skills);

internal sealed record StatisticsSkillPersistenceModel(string SkillId, double Experience, double BankedCurrency);
