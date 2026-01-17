using System;
using System.Linq;
using Engine.Core.Assets;
using Engine.Core.Contracts;
using Engine.Core.Rendering.Sprites;
using Engine.Core.Scheduling;
using Engine.Core.Statistics;
using Engine.Core.Time;
using Xunit;

namespace Engine.Core.Tests.Statistics;

public sealed class StatisticsModuleViewTests
{
    [Fact]
    public void DescribeModuleViewsEmitsOverviewGrid()
    {
        var scheduler = new TickScheduler(TimeSpan.FromMilliseconds(100), new DeterministicSystemClock());
        var spriteLibrary = new SpriteLibrary(new AssetManifest());
        var statisticsService = new StatisticsService(NullModuleStateStore.Instance);
        statisticsService.RegisterSkill(StatisticsModule.IdlingSkillId, "Idling", "Idle focus", 1d, "idle", "#72f5ff");
        statisticsService.ActivateSkill(StatisticsModule.IdlingSkillId);
        statisticsService.RegisterSkill("skill.mining", "Mining", "Process ore", 2d, "mine", "#ffb347");
        var module = new StatisticsModule(statisticsService, scheduler, spriteLibrary);

        var documents = module.DescribeModuleViews(ModuleViewContext.Empty);
        var document = Assert.Single(documents);
        var overviewSection = document.Blocks
            .OfType<ModuleViewSectionBlock>()
            .Single(section => section.Id == "statistics.overview");
        var grid = overviewSection.Children
            .OfType<ModuleViewGridBlock>()
            .Single(block => block.Id == "statistics.overview.grid");

        Assert.True(grid.Columns >= 1);
        var metricBlocks = grid.Cells
            .SelectMany(cell => cell.Children)
            .OfType<ModuleViewMetricBlock>()
            .ToArray();
        Assert.NotEmpty(metricBlocks);
        Assert.Contains(metricBlocks, metric => metric.Id == "statistics.total-currency");
    }
}
