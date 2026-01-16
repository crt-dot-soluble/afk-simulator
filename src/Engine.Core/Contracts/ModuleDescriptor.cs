using System.Collections.Generic;

namespace Engine.Core.Contracts;

/// <summary>
/// Machine-readable descriptor used when advertising new modules or plugins to the loader.
/// </summary>
/// <param name="Name">Human-readable module label.</param>
/// <param name="Version">Semantic version for compatibility checks.</param>
/// <param name="Capabilities">Feature flags or resource exports.</param>
/// <param name="Resources">List of resource identifiers added by the module.</param>
/// <param name="TelemetryKeys">Structured telemetry identifiers used by monitoring systems.</param>
/// <param name="Description">Optional human-readable summary for display surfaces.</param>
/// <param name="Metadata">Key-value metadata useful for tooling (e.g. owner, domain, maturity).</param>
public sealed record ModuleDescriptor(
    string Name,
    string Version,
    IReadOnlyCollection<string> Capabilities,
    IReadOnlyCollection<string> Resources,
    IReadOnlyCollection<string> TelemetryKeys,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
