using System.Collections.Generic;
using Engine.Core.Multiplayer;

namespace Engine.Core.Tests.Multiplayer;

public sealed class MultiplayerSessionServiceTests
{
    [Fact]
    public async Task CreateJoinLeaveLifecycleRemovesEmptySessions()
    {
        var service = new MultiplayerSessionService();
        var descriptor = await service.CreateSessionAsync("echo-lab");
        Assert.Equal("echo-lab", descriptor.Name);

        var joined = await service.JoinSessionAsync(descriptor.Id, "pilot-01");
        Assert.True(joined);

        var left = await service.LeaveSessionAsync(descriptor.Id, "pilot-01");
        Assert.True(left);

        var snapshot = await CollectSessionsAsync(service);
        Assert.DoesNotContain(snapshot, session => session.Id == descriptor.Id);
    }

    [Fact]
    public async Task JoinSessionReturnsFalseForUnknownId()
    {
        var service = new MultiplayerSessionService();
        var result = await service.JoinSessionAsync("missing", "pilot-02");
        Assert.False(result);
    }

    private static async Task<List<SessionDescriptor>> CollectSessionsAsync(MultiplayerSessionService service)
    {
        var list = new List<SessionDescriptor>();
        await foreach (var session in service.ListSessionsAsync().ConfigureAwait(false))
        {
            list.Add(session);
        }

        return list;
    }
}
