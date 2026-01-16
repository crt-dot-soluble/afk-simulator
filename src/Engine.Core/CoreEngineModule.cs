using System;
using System.Collections.Generic;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;
using Engine.Core.Resources;
using Engine.Core.Scheduling;

namespace Engine.Core;

public sealed class CoreEngineModule : IModuleContract, IModuleDescriptorSource
{
    private static readonly string[] DescriptorCapabilities = { "tick", "resources", "developer-tools" };
    private static readonly string[] DescriptorResources = { "ResourceGraph", "AssetManifest" };
    private static readonly string[] DescriptorTelemetry = { "engine.tick.duration", "engine.resources.snapshot" };

    private readonly TickScheduler _scheduler;
    private readonly ResourceGraph _resourceGraph;
    private bool _initialized;
    private double _oreTransferRate = 1d;
    private double _oreToGoldEfficiency = 0.1d;
    private double _oreCapacity = 1_000d;
    private double _goldCapacity = 1_000_000d;
    private double _oreGeneration = 5d;
    private double _goldGeneration = 1.5d;

    public CoreEngineModule(TickScheduler scheduler, ResourceGraph resourceGraph)
    {
        _scheduler = scheduler;
        _resourceGraph = resourceGraph;
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
}
