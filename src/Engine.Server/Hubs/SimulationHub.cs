using Engine.Core.Multiplayer;
using Microsoft.AspNetCore.SignalR;

namespace Engine.Server.Hubs;

public sealed class SimulationHub : Hub
{
    private readonly ILeaderboardService _leaderboardService;

    public SimulationHub(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    public async Task<IReadOnlyCollection<LeaderboardEntry>> GetLeaderboardAsync(int take = 25)
    {
        return _leaderboardService.Snapshot(take);
    }
}
