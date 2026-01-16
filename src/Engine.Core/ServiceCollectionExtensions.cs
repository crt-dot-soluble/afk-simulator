using System;
using System.IO;
using Engine.Core.Accounts;
using Engine.Core.Assets;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;
using Engine.Core.Diagnostics;
using Engine.Core.Multiplayer;
using Engine.Core.Rendering;
using Engine.Core.Resources;
using Engine.Core.Scheduling;
using Engine.Core.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIncrementalEngine(this IServiceCollection services)
    {
        services.AddSingleton<ISystemClock, DeterministicSystemClock>();
        services.AddSingleton(provider =>
            new TickScheduler(TimeSpan.FromMilliseconds(100), provider.GetRequiredService<ISystemClock>()));
        services.AddSingleton<AccountRulebook>();
        services.AddSingleton<ResourceGraph>();
        services.AddSingleton<AssetManifest>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<ILeaderboardService, InMemoryLeaderboardService>();
        services.AddSingleton<IMultiplayerSessionService, MultiplayerSessionService>();
        services.AddSingleton<RenderSettingsStore>();
        services.AddSingleton<ModuleCatalog>();
        services.AddSingleton(provider =>
        {
            var environment = provider.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            var root = environment?.ContentRootPath ?? AppContext.BaseDirectory;
            var storagePath = Path.Combine(root, "App_Data", "developer-profiles.json");
            return new DeveloperProfileStore(new DeveloperProfileStoreOptions(storagePath));
        });
        services.AddSingleton<IModuleExplorer, ModuleExplorer>();
        services.AddSingleton<IModuleContract, CoreEngineModule>();
        services.AddSingleton<IModuleContract, AccountModule>();
        services.AddSingleton<IModuleContract, DiagnosticsModule>();
        services.AddHostedService<TickHostedService>();
        services.AddHostedService<ModuleBootstrapperHostedService>();
        return services;
    }
}
