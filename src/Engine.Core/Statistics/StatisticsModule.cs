using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;
using Engine.Core.Rendering.Sprites;
using Engine.Core.Scheduling;

namespace Engine.Core.Statistics;

public sealed class StatisticsModule : IModuleContract, IModuleDescriptorSource, IDashboardViewProvider,
    IModuleViewProvider
{
    public const string IdlingSkillId = "skill.idle";
    private const string OverviewGridBlockId = "statistics.overview.grid";
    private static readonly string[] Capabilities = { "statistics", "dashboard" };
    private static readonly string[] Resources = { nameof(StatisticsService) };
    private static readonly string[] Telemetry = { "statistics.skills.currency.gain" };

    private readonly StatisticsService _statistics;
    private readonly TickScheduler _scheduler;
    private readonly SpriteLibrary _sprites;
    private StatisticTickConsumer? _consumer;
    private double _skillTickMultiplier = 1d;

    public StatisticsModule(StatisticsService statistics, TickScheduler scheduler, SpriteLibrary sprites)
    {
        _statistics = statistics;
        _scheduler = scheduler;
        _sprites = sprites;
    }

    public string Name => "Statistics";
    public string Version => "0.1.0";

    public ValueTask InitializeAsync(ModuleContext context, CancellationToken cancellationToken = default)
    {
        RegisterIdlingSkill();
        _statistics.ActivateSkill(IdlingSkillId);
        if (_consumer is null)
        {
            _consumer = new StatisticTickConsumer(_statistics);
            _scheduler.RegisterConsumer(_consumer, priority: 100, rate: new TickRateProfile(_skillTickMultiplier));
        }

        return ValueTask.CompletedTask;
    }

    [ModuleInspectable("Skill tick speed", Description = "Relative multiplier applied to statistic skill processing.",
        Group = "Statistics")]
    public double SkillTickMultiplier
    {
        get => _skillTickMultiplier;
        set
        {
            var clamped = Math.Clamp(value, TickRateProfile.MinRelativeSpeed, TickRateProfile.MaxRelativeSpeed);
            _skillTickMultiplier = clamped;
            if (_consumer is not null)
            {
                _scheduler.UpdateConsumerRate(_consumer.Id, new TickRateProfile(clamped));
            }
        }
    }

    public ValueTask<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var definitions = _statistics.ListSkillDefinitions();
        var status = definitions.Count > 0 ? ModuleHealthStatus.Healthy : ModuleHealthStatus.Degraded;
        var details = new Dictionary<string, string>
        {
            ["skills"] = definitions.Count.ToString(CultureInfo.InvariantCulture)
        };
        return ValueTask.FromResult(new ModuleHealth(status, details));
    }

    public ModuleDescriptor Describe() => new(
        Name,
        Version,
        Capabilities,
        Resources,
        Telemetry,
        "Namespace-aware statistic collection with deterministic skill loops",
        new Dictionary<string, string>
        {
            ["owner"] = "content",
            ["tier"] = "system",
            ["area"] = "statistics"
        });

    public IReadOnlyCollection<DashboardViewDescriptor> DescribeViews()
    {
        return new[]
        {
            new DashboardViewDescriptor(
                DashboardViewIds.Statistics,
                Name,
                "Statistics",
                "Namespace hierarchy + skill controls",
                DashboardViewZones.Primary,
                Order: 20,
                ColumnSpan: 4)
        };
    }

    public IReadOnlyCollection<ModuleViewDocument> DescribeModuleViews(ModuleViewContext context)
    {
        var descriptor = DescribeViews().First();
        var snapshot = _statistics.SnapshotStatistics();
        var blocks = BuildViewBlocks(snapshot);
        return new[] { new ModuleViewDocument(descriptor, blocks, new ModuleViewDataSource("PT1S")) };
    }

    private static List<ModuleViewBlock> BuildViewBlocks(StatisticsSnapshot snapshot)
    {
        var blocks = new List<ModuleViewBlock>();
        var entries = snapshot.Namespaces
            .SelectMany(ns => ns.Categories)
            .SelectMany(category => category.Entries)
            .ToArray();
        var active = entries.FirstOrDefault(entry => entry.IsActive) ?? entries.FirstOrDefault();

        var metricBlocks = new List<ModuleViewBlock>();
        if (active is not null)
        {
            metricBlocks.Add(new ModuleViewMetricBlock(
                "statistics.active-skill",
                active.Name,
                $"Lvl {active.Value.Level.ToString(CultureInfo.InvariantCulture)}",
                Secondary: $"{active.Value.Experience.ToString("N1", CultureInfo.InvariantCulture)} xp",
                Accent: active.AccentColor,
                Trend: active.Value.CurrencyPerSecond.ToString("N1", CultureInfo.InvariantCulture),
                TrendLabel: "c/s",
                Tags: new Dictionary<string, string>
                {
                    ["bank"] = $"{active.Value.BankedCurrency.ToString("N1", CultureInfo.InvariantCulture)} c"
                }));
        }

        metricBlocks.Add(new ModuleViewMetricBlock(
            "statistics.total-currency",
            "Total Skill Currency",
            snapshot.TotalSkillCurrency.ToString("N1", CultureInfo.InvariantCulture),
            Secondary: $"Active: {snapshot.ActiveSkillId}",
            Icon: "database"));

        var cells = metricBlocks
            .Select(block => new ModuleViewGridCell(1, new ModuleViewBlock[] { block }))
            .ToArray();

        var overviewChildren = new ModuleViewBlock[]
        {
            new ModuleViewGridBlock(
                OverviewGridBlockId,
                Math.Clamp(metricBlocks.Count, 1, 2),
                cells)
        };

        blocks.Add(new ModuleViewSectionBlock(
            "statistics.overview",
            "Skill Overview",
            active?.Description,
            overviewChildren,
            active is null ? null : new ModuleViewStyle(active.AccentColor)));

        if (entries.Length > 0)
        {
            var items = entries
                .Select((entry, index) => new ModuleViewListItem(
                    entry.EntryId,
                    entry.Name,
                    entry.Description,
                    $"{entry.Value.CurrencyPerSecond.ToString("N1", CultureInfo.InvariantCulture)} c/s",
                    entry.AccentColor,
                    Icon: entry.IsActive ? "play" : null,
                    IsActive: entry.IsActive,
                    Badges: new Dictionary<string, string>
                    {
                        ["Lvl"] = entry.Value.Level.ToString(CultureInfo.InvariantCulture),
                        ["XP"] = entry.Value.Experience.ToString("N1", CultureInfo.InvariantCulture),
                        ["Bank"] = entry.Value.BankedCurrency.ToString("N1", CultureInfo.InvariantCulture)
                    }))
                .ToArray();

            blocks.Add(new ModuleViewListBlock(
                "statistics.skills",
                "Skills",
                items,
                ShowOrder: true,
                AllowSelection: true));
        }

        return blocks;
    }

    private void RegisterIdlingSkill()
    {
        if (_statistics.ListSkillDefinitions()
            .Any(def => string.Equals(def.Id, IdlingSkillId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (_sprites.TryGet("avatars/ember-nomad", out var definition))
        {
            var firstAnimation = definition.Animations.FirstOrDefault();
            var idleAnimation = firstAnimation.Value?.Name ?? "idle";
            _statistics.RegisterSkill(IdlingSkillId, "Idling",
                "Channel ambient cosmic energy while doing absolutely nothing.",
                currencyPerSecond: 1d,
                defaultAnimation: idleAnimation,
                accentColor: "#72f5ff");
            return;
        }

        _statistics.RegisterSkill(IdlingSkillId, "Idling",
            "Channel ambient cosmic energy while doing absolutely nothing.",
            currencyPerSecond: 1d,
            defaultAnimation: "idle",
            accentColor: "#72f5ff");
    }

    private sealed class StatisticTickConsumer : ITickConsumer
    {
        private readonly StatisticsService _statistics;

        public StatisticTickConsumer(StatisticsService statistics)
        {
            _statistics = statistics;
        }

        public string Id => "statistics.skills.runtime";

        public ValueTask OnTickAsync(TickContext context, CancellationToken cancellationToken = default)
        {
            return _statistics.ProcessTickAsync(context.EffectiveDuration, cancellationToken);
        }
    }
}
