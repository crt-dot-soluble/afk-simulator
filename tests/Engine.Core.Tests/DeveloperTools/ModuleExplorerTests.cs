using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;

namespace Engine.Core.Tests.DeveloperTools;

internal sealed class ModuleExplorerTests
{
    [Fact]
    public async Task UpdatesPropertyAndExecutesCommands()
    {
        var module = new TestModule();
        var explorer = new ModuleExplorer(new[] { module });

        var initial = explorer.DescribeSurface(module.Name);
        var threshold = initial.Properties.Single(property => property.Name == nameof(TestModule.Threshold));
        Assert.Equal(1.5, Assert.IsType<double>(threshold.Value));

        await explorer.UpdatePropertyAsync(module.Name, nameof(TestModule.Threshold), 3d, CancellationToken.None);
        var snapshot =
            await explorer.ExecuteAsync(module.Name, "resource-snapshot", null, CancellationToken.None) as
                IReadOnlyDictionary<string, double>;
        Assert.NotNull(snapshot);

        var result = await explorer.ExecuteAsync(module.Name, "multiply",
            new Dictionary<string, object?> { { "value", 2d } }, CancellationToken.None);
        Assert.Equal(6d, result);
    }

    private sealed class TestModule : IModuleContract, IModuleDescriptorSource
    {
        public string Name => "Test";
        public string Version => "0.0.1";

        [ModuleInspectable("Threshold", Description = "Multiplier", Group = "Tuning")]
        public double Threshold { get; set; } = 1.5d;

        public ValueTask InitializeAsync(ModuleContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ModuleHealth.Healthy());

        [ModuleCommand("multiply", Description = "Multiply value", IsQuery = true)]
        public double Multiply(double value) => value * Threshold;

        [ModuleCommand("resource-snapshot", Description = "Snapshot", IsQuery = true)]
        public Dictionary<string, double> Snapshot() => new() { ["threshold"] = Threshold };

        public ModuleDescriptor Describe() => new(Name, Version, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>());
    }
}
