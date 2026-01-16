using System.Collections.Generic;
using Engine.Core.Multiplayer;
using Microsoft.AspNetCore.SignalR;

namespace Engine.Server.Hubs;

internal sealed class SimulationHub : Hub
{
    private readonly ILeaderboardService _leaderboardService;

    public SimulationHub(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    public Task<IReadOnlyCollection<LeaderboardEntry>> GetLeaderboardAsync(int take = 25)
        => Task.FromResult(_leaderboardService.Snapshot(take));
}
