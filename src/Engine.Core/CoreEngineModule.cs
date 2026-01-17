using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;
using Engine.Core.Multiplayer;
using Engine.Core.Resources;
using Engine.Core.Scheduling;

namespace Engine.Core;

public sealed class CoreEngineModule : IModuleContract, IModuleDescriptorSource, IDashboardViewProvider,
    IModuleViewProvider
{
    private static readonly string[] DescriptorCapabilities = { "tick", "resources", "developer-tools" };
    private static readonly string[] DescriptorResources = { "ResourceGraph", "AssetManifest" };
    private static readonly string[] DescriptorTelemetry = { "engine.tick.duration", "engine.resources.snapshot" };
    private const string LeaderboardListBlockId = "core.leaderboard.list";
    private const string SessionsListBlockId = "core.sessions.list";
    private const string LeaderboardFormBlockId = "core.leaderboard.form";
    private const string SessionsFormBlockId = "core.sessions.form";
    private const string PlayerAliasParameter = "playerAlias";

    private readonly TickScheduler _scheduler;
    private readonly ResourceGraph _resourceGraph;
    private readonly ILeaderboardService _leaderboard;
    private readonly IMultiplayerSessionService _sessions;
    private bool _initialized;
    private double _oreTransferRate = 1d;
    private double _oreToGoldEfficiency = 0.1d;
    private double _oreCapacity = 1_000d;
    private double _goldCapacity = 1_000_000d;
    private double _oreGeneration = 5d;
    private double _goldGeneration = 1.5d;
    private double _ticksPerSecond;

    public CoreEngineModule(TickScheduler scheduler, ResourceGraph resourceGraph,
        ILeaderboardService leaderboard, IMultiplayerSessionService sessions)
    {
        _scheduler = scheduler;
        _resourceGraph = resourceGraph;
        _leaderboard = leaderboard;
        _sessions = sessions;
        _ticksPerSecond = Math.Round(1d / _scheduler.TickDuration.TotalSeconds, 2, MidpointRounding.AwayFromZero);
    }

    public string Name => "CoreEngine";
    public string Version => "0.1.0";

    [ModuleInspectable("Ore transfer rate", Description = "Units of ore moved toward refining per second.",
        Group = "Resource Graph")]
    public double OreTransferRate
    {
        get => _oreTransferRate;
        set
        {
            var clamped = Math.Clamp(value, 0.1d, 25d);
            _oreTransferRate = clamped;
            if (_initialized)
            {
                ConfigureEdge();
            }
        }
    }

    [ModuleInspectable("Ore→Gold efficiency", Description = "Multiplier applied when ore converts to gold.",
        Group = "Resource Graph")]
    public double OreToGoldEfficiency
    {
        get => _oreToGoldEfficiency;
        set
        {
            var clamped = Math.Clamp(value, 0.01d, 10d);
            _oreToGoldEfficiency = clamped;
            if (_initialized)
            {
                ConfigureEdge();
            }
        }
    }

    [ModuleInspectable("Ore capacity", Description = "Maximum ore storage before overflow.", Group = "Resource Graph")]
    public double OreCapacity
    {
        get => _oreCapacity;
        set
        {
            _oreCapacity = Math.Clamp(value, 100d, 10_000_000d);
            if (_initialized)
            {
                ConfigureNodes();
            }
        }
    }

    [ModuleInspectable("Gold capacity", Description = "Maximum refined gold reserve.", Group = "Resource Graph")]
    public double GoldCapacity
    {
        get => _goldCapacity;
        set
        {
            _goldCapacity = Math.Clamp(value, 10_000d, 100_000_000d);
            if (_initialized)
            {
                ConfigureNodes();
            }
        }
    }

    [ModuleInspectable("Global tick rate", Description = "Deterministic ticks per second for the simulation.",
        Group = "Scheduler")]
    public double TickRate
    {
        get => _ticksPerSecond;
        set
        {
            var clamped = Math.Clamp(value, 0.5d, 60d);
            _ticksPerSecond = clamped;
            var duration = TimeSpan.FromSeconds(1d / clamped);
            _scheduler.UpdateTickDuration(duration);
        }
    }

    public ValueTask InitializeAsync(ModuleContext context, CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return ValueTask.CompletedTask;
        }

        ConfigureNodes();
        ConfigureEdge();
        _initialized = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var status = _initialized ? ModuleHealthStatus.Healthy : ModuleHealthStatus.Degraded;
        return ValueTask.FromResult(new ModuleHealth(status));
    }

    [ModuleCommand("reseed-resources", Description = "Reseed ore and gold reserves with explicit magnitudes.")]
    public ValueTask ReseedResourcesAsync(double oreStock, double goldStock)
    {
        ConfigureNodes(oreStock, goldStock);
        return ValueTask.CompletedTask;
    }

    [ModuleCommand("resource-snapshot", Description = "Return the latest deterministic resource snapshot.",
        IsQuery = true)]
    public IReadOnlyDictionary<string, double> SnapshotResources() => _resourceGraph.ExportState();

    public ModuleDescriptor Describe()
    {
        return new ModuleDescriptor(
            Name,
            Version,
            DescriptorCapabilities,
            DescriptorResources,
            DescriptorTelemetry,
            "Primary deterministic scheduler and resource economy",
            new Dictionary<string, string>
            {
                ["owner"] = "engine",
                ["tier"] = "system",
                ["area"] = "core"
            });
    }

    public IReadOnlyCollection<DashboardViewDescriptor> DescribeViews()
    {
        return new[]
        {
            new DashboardViewDescriptor(DashboardViewIds.Simulation, Name, "Simulation View",
                "Primary hero viewport for the active pilot.", DashboardViewZones.Hero, Order: 0, ColumnSpan: 9),
            new DashboardViewDescriptor(DashboardViewIds.Graphics, Name, "Graphics Controls",
                "GPU tuning and render cadence toggles.", DashboardViewZones.Secondary, Order: 10, ColumnSpan: 4),
            new DashboardViewDescriptor(DashboardViewIds.Leaderboard, Name, "Global Leaderboard",
                "Live scores across shards.", DashboardViewZones.Secondary, Order: 20, ColumnSpan: 4),
            new DashboardViewDescriptor(DashboardViewIds.Sessions, Name, "Multiplayer Sessions",
                "Instanced lobby overview.", DashboardViewZones.Secondary, Order: 30, ColumnSpan: 4)
        };
    }

    public IReadOnlyCollection<ModuleViewDocument> DescribeModuleViews(ModuleViewContext context)
    {
        var descriptors = DescribeViews()
            .ToDictionary(descriptor => descriptor.Id, descriptor => descriptor, StringComparer.OrdinalIgnoreCase);
        var documents = new List<ModuleViewDocument>();

        if (descriptors.TryGetValue(DashboardViewIds.Graphics, out var graphicsDescriptor))
        {
            documents.Add(BuildGraphicsDocument(graphicsDescriptor));
        }

        if (descriptors.TryGetValue(DashboardViewIds.Leaderboard, out var leaderboardDescriptor))
        {
            documents.Add(BuildLeaderboardDocument(leaderboardDescriptor, context));
        }

        if (descriptors.TryGetValue(DashboardViewIds.Sessions, out var sessionsDescriptor))
        {
            documents.Add(BuildSessionsDocument(sessionsDescriptor));
        }

        return documents;
    }

    private ModuleViewDocument BuildGraphicsDocument(DashboardViewDescriptor descriptor)
    {
        var blocks = new ModuleViewBlock[]
        {
            new ModuleViewSectionBlock(
                "core.graphics.section",
                "Graphics & Audio Controls",
                "Open Settings to adjust GPU tier, render cadence, and sound mix. These values mirror the current render loop state.",
                new ModuleViewBlock[]
                {
                    new ModuleViewMetricBlock(
                        "core.graphics.tick-rate",
                        "Tick Rate",
                        $"{_ticksPerSecond.ToString("N1", CultureInfo.InvariantCulture)} tps",
                        Secondary:
                        $"{_scheduler.TickDuration.TotalMilliseconds.ToString("N0", CultureInfo.InvariantCulture)} ms/frame",
                        Icon: "timer",
                        Trend: _scheduler.TickDuration.TotalMilliseconds.ToString("N0", CultureInfo.InvariantCulture),
                        TrendLabel: "ms"),
                    new ModuleViewActionBarBlock(
                        "core.graphics.actions",
                        new[]
                        {
                            new ModuleViewActionDescriptor(
                                "open-settings",
                                "Open Settings",
                                "navigate:/settings",
                                Icon: "settings",
                                IsPrimary: true)
                        })
                },
                new ModuleViewStyle("#72f5ff"))
        };

        return new ModuleViewDocument(descriptor, blocks, new ModuleViewDataSource("PT30S"));
    }

    private ModuleViewDocument BuildLeaderboardDocument(DashboardViewDescriptor descriptor, ModuleViewContext context)
    {
        var entries = _leaderboard.Snapshot(10);
        var items = entries
            .Select((entry, index) => new ModuleViewListItem(
                entry.PlayerId,
                entry.DisplayName,
                entry.Timestamp.ToLocalTime().ToString("MMM d · HH:mm", CultureInfo.InvariantCulture),
                $"{entry.Score.ToString("N0", CultureInfo.InvariantCulture)} pts",
                Icon: index == 0 ? "trophy" : null,
                Badges: new Dictionary<string, string>
                {
                    ["Rank"] = (index + 1).ToString(CultureInfo.InvariantCulture)
                }))
            .ToArray();

        if (items.Length == 0)
        {
            items = new[]
            {
                new ModuleViewListItem("core.leaderboard.empty", "No pilots yet",
                    "Submit a score from Mission Control to seed the board.")
            };
        }

        var defaultAlias = ResolveContextParameter(context, PlayerAliasParameter) ?? "Pilot";
        var section = new ModuleViewSectionBlock(
            "core.leaderboard.section",
            "Global Leaderboard",
            "Deterministic pilot rankings streamed from the authoritative shard.",
            new ModuleViewBlock[]
            {
                new ModuleViewListBlock(LeaderboardListBlockId, "Top Pilots", items, ShowOrder: true),
                new ModuleViewFormBlock(
                    LeaderboardFormBlockId,
                    "Submit Score",
                    new ModuleViewFormField[]
                    {
                        new ModuleViewFormField(
                            "alias",
                            "Callsign",
                            "text",
                            Placeholder: "Pilot-000",
                            Value: defaultAlias,
                            Required: true,
                            MaxLength: 32,
                            Description: "Display name for the leaderboard submission."),
                        new ModuleViewFormField(
                            "score",
                            "Score",
                            "number",
                            Placeholder: "1000",
                            Value: "1000",
                            Required: true,
                            Min: 0,
                            Description: "Deterministic score to publish.")
                    },
                    new[]
                    {
                        new ModuleViewActionDescriptor(
                            "submit-score",
                            "Submit Score",
                            "leaderboard.submit",
                            Icon: "upload",
                            IsPrimary: true)
                    },
                    "Post a deterministic score to the global board."),
                new ModuleViewActionBarBlock(
                    "core.leaderboard.actions",
                    new[]
                    {
                        new ModuleViewActionDescriptor("sync", "Sync", "leaderboard.sync", Icon: "refresh")
                    })
            });

        return new ModuleViewDocument(descriptor, new ModuleViewBlock[] { section },
            new ModuleViewDataSource("PT10S"));
    }

    private ModuleViewDocument BuildSessionsDocument(DashboardViewDescriptor descriptor)
    {
        var sessions = _sessions.Snapshot(25)
            .OrderByDescending(session => session.PlayerCount)
            .ThenBy(session => session.CreatedAt)
            .ToArray();
        var totalPilots = sessions.Sum(session => session.PlayerCount);
        var items = sessions
            .Select(session => new ModuleViewListItem(
                session.Id,
                session.Name,
                session.CreatedAt.ToLocalTime().ToString("MMM d · HH:mm", CultureInfo.InvariantCulture),
                $"{session.PlayerCount.ToString("N0", CultureInfo.InvariantCulture)} pilots",
                Icon: session.PlayerCount > 0 ? "users" : "sparkles"))
            .ToArray();

        if (items.Length == 0)
        {
            items = new[]
            {
                new ModuleViewListItem("core.sessions.empty", "No sessions online",
                    "Spawn a shard to host multiplayer pilots.")
            };
        }

        var blocks = new ModuleViewBlock[]
        {
            new ModuleViewSectionBlock(
                "core.sessions.section",
                "Instanced Sessions",
                "Join or spawn deterministic multiplayer shards.",
                new ModuleViewBlock[]
                {
                    new ModuleViewMetricBlock(
                        "core.sessions.metrics",
                        "Active Sessions",
                        sessions.Length.ToString("N0", CultureInfo.InvariantCulture),
                        Secondary: $"{totalPilots.ToString("N0", CultureInfo.InvariantCulture)} pilots connected",
                        Icon: "server"),
                    new ModuleViewListBlock(SessionsListBlockId, "Shards", items, AllowSelection: true),
                    new ModuleViewFormBlock(
                        SessionsFormBlockId,
                        "Spawn Session",
                        new ModuleViewFormField[]
                        {
                            new ModuleViewFormField(
                                "sessionName",
                                "Session Name",
                                "text",
                                Placeholder: "Echo Node",
                                Value: "Echo Node",
                                Required: true,
                                MaxLength: 32,
                                Description: "Name shown to pilots when joining.")
                        },
                        new[]
                        {
                            new ModuleViewActionDescriptor(
                                "spawn",
                                "Create Session",
                                "sessions.spawn",
                                Icon: "plus",
                                IsPrimary: true)
                        },
                        "Provision a deterministic shard for multiplayer pilots."),
                    new ModuleViewActionBarBlock(
                        "core.sessions.actions",
                        new[]
                        {
                            new ModuleViewActionDescriptor("refresh", "Refresh", "sessions.refresh", Icon: "refresh")
                        })
                })
        };

        return new ModuleViewDocument(descriptor, blocks, new ModuleViewDataSource("PT5S"));
    }

    private void ConfigureNodes(double? oreOverride = null, double? goldOverride = null)
    {
        var state = _resourceGraph.ExportState();
        var oreValue = oreOverride ?? TryGet(state, "ore");
        var goldValue = goldOverride ?? TryGet(state, "gold");

        _resourceGraph.UpsertNode(new ResourceNodeDefinition("gold", _goldCapacity, _goldGeneration), goldValue);
        _resourceGraph.UpsertNode(new ResourceNodeDefinition("ore", _oreCapacity, _oreGeneration), oreValue);
    }

    private void ConfigureEdge()
    {
        _resourceGraph.UpsertEdge(new ResourceEdgeDefinition("ore-to-gold", "ore", "gold", _oreTransferRate,
            _oreToGoldEfficiency));
    }

    private static double TryGet(IReadOnlyDictionary<string, double> state, string key)
    {
        return state.TryGetValue(key, out var value) ? value : 0d;
    }

    private static string? ResolveContextParameter(ModuleViewContext context, string key)
    {
        if (context.Parameters is null)
        {
            return null;
        }

        return context.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}
