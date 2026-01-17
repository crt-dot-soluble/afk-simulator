using System.Collections.Generic;

namespace Engine.Core.Contracts;

/// <summary>
/// Allows modules to contribute layout metadata for Mission Control surfaces.
/// </summary>
public interface IDashboardViewProvider
{
    IReadOnlyCollection<DashboardViewDescriptor> DescribeViews();
}

/// <summary>
/// Immutable descriptor used by the client to arrange dashboard panels.
/// </summary>
/// <param name="Id">Stable identifier for the panel content.</param>
/// <param name="Module">Ownning module name.</param>
/// <param name="Title">Display name rendered in the UI.</param>
/// <param name="Description">Short summary for accessibility.</param>
/// <param name="Zone">Preferred layout zone (hero/primary/secondary).</param>
/// <param name="Order">Sort hint within the zone.</param>
/// <param name="ColumnSpan">How many grid columns the panel prefers to span (1-12).</param>
public sealed record DashboardViewDescriptor(
    string Id,
    string Module,
    string Title,
    string Description,
    string Zone,
    int Order,
    int ColumnSpan);

public static class DashboardViewZones
{
    public const string Hero = "hero";
    public const string Primary = "primary";
    public const string Secondary = "secondary";
}

public static class DashboardViewIds
{
    public const string Simulation = "core.simulation";
    public const string Statistics = "statistics.panel";
    public const string AccountUniverse = "accounts.universe";
    public const string Graphics = "core.graphics";
    public const string Leaderboard = "core.leaderboard";
    public const string Sessions = "core.sessions";
}
