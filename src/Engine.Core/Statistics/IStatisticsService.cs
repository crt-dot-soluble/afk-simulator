using System.Collections.Generic;

namespace Engine.Core.Statistics;

/// <summary>
/// Provides deterministic access to player statistics surfaced via Mission Control.
/// </summary>
public interface IStatisticsService
{
    IReadOnlyCollection<SkillDefinition> ListSkillDefinitions();

    SkillStateSnapshot SnapshotSkillState();

    StatisticsSnapshot SnapshotStatistics();

    void ActivateSkill(string skillId);
}
