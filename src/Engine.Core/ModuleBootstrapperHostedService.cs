using Engine.Core.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Engine.Core;

public sealed class ModuleBootstrapperHostedService : IHostedService
{
    private readonly IEnumerable<IModuleContract> _modules;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ISystemClock _clock;
    private readonly ModuleCatalog _catalog;

    public ModuleBootstrapperHostedService(
        IEnumerable<IModuleContract> modules,
        IServiceProvider services,
        IConfiguration configuration,
        ISystemClock clock,
        ModuleCatalog catalog)
    {
        _modules = modules;
        _services = services;
        _configuration = configuration;
        _clock = clock;
        _catalog = catalog;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var context = new ModuleContext(_services, _configuration, _clock);
        foreach (var module in _modules)
        {
            if (module is IModuleDescriptorSource descriptorSource)
            {
                var descriptor = descriptorSource.Describe();
                _catalog.Register(descriptor);
            }

            await module.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
