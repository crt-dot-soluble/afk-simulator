using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Engine.Core.Contracts;

/// <summary>
/// Defines the minimal surface area every engine subsystem must expose so it can be
/// versioned, health-checked, and orchestrated deterministically.
/// </summary>
public interface IModuleContract
{
    string Name { get; }

    string Version { get; }

    ValueTask InitializeAsync(ModuleContext context, CancellationToken cancellationToken = default);

    ValueTask<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides initialization-time hooks for modules along with shared DI services.
/// </summary>
/// <param name="Services">Primary service provider for resolving dependencies.</param>
/// <param name="Configuration">Immutable configuration snapshot for the module.</param>
/// <param name="Clock">Deterministic clock abstraction for tick scheduling.</param>
public sealed record ModuleContext(IServiceProvider Services, IConfiguration Configuration, ISystemClock Clock);

/// <param name="Status">Current module health classification.</param>
/// <param name="Details">Optional machine-readable status payload.</param>
public sealed record ModuleHealth(ModuleHealthStatus Status, IReadOnlyDictionary<string, string>? Details = null)
{
    public static ModuleHealth Healthy(IReadOnlyDictionary<string, string>? details = null) =>
        new(ModuleHealthStatus.Healthy, details);

    public static ModuleHealth Degraded(IReadOnlyDictionary<string, string>? details = null) =>
        new(ModuleHealthStatus.Degraded, details);

    public static ModuleHealth Unhealthy(IReadOnlyDictionary<string, string>? details = null) =>
        new(ModuleHealthStatus.Unhealthy, details);
}

public enum ModuleHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Abstraction over time to ensure deterministic tick orchestration across server and client hosts.
/// </summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
