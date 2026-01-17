using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core;
using Engine.Core.Contracts;
using Engine.Core.Multiplayer;
using Engine.Core.Resources;
using Engine.Core.Scheduling;
using Engine.Core.Time;
using Xunit;

namespace Engine.Core.Tests.Core;

internal sealed class CoreEngineModuleTests
{
    [Fact]
    public void DescribeModuleViewsEmitsCorePanelDocuments()
    {
        var clock = new DeterministicSystemClock();
        var scheduler = new TickScheduler(TimeSpan.FromMilliseconds(100), clock);
        var resourceGraph = new ResourceGraph();
        var leaderboard = new TestLeaderboardService();
        var sessions = new TestSessionService();
        var module = new CoreEngineModule(scheduler, resourceGraph, leaderboard, sessions);

        var documents = module.DescribeModuleViews(ModuleViewContext.Empty);

        Assert.Contains(documents, doc => doc.Descriptor.Id == DashboardViewIds.Graphics);
        Assert.Contains(documents, doc => doc.Descriptor.Id == DashboardViewIds.Leaderboard);
        Assert.Contains(documents, doc => doc.Descriptor.Id == DashboardViewIds.Sessions);

        var leaderboardDoc = documents.Single(doc => doc.Descriptor.Id == DashboardViewIds.Leaderboard);
        var leaderboardList = leaderboardDoc.Blocks
            .OfType<ModuleViewSectionBlock>()
            .SelectMany(section => section.Children.OfType<ModuleViewListBlock>())
            .Single(list => list.Id == "core.leaderboard.list");
        Assert.NotEmpty(leaderboardList.Items);

        var sessionDoc = documents.Single(doc => doc.Descriptor.Id == DashboardViewIds.Sessions);
        var sessionList = sessionDoc.Blocks
            .OfType<ModuleViewSectionBlock>()
            .SelectMany(section => section.Children.OfType<ModuleViewListBlock>())
            .Single(list => list.Id == "core.sessions.list");
        Assert.True(sessionList.AllowSelection);
    }

    private sealed class TestLeaderboardService : ILeaderboardService
    {
        public Task ReportAsync(LeaderboardEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IAsyncEnumerable<LeaderboardEntry> StreamTopAsync(int take, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<LeaderboardEntry>();

        public IReadOnlyCollection<LeaderboardEntry> Snapshot(int take = 100) =>
            new[] { new LeaderboardEntry("pilot", "Pilot", 1_234, DateTimeOffset.UtcNow) };
    }

    private sealed class TestSessionService : IMultiplayerSessionService
    {
        public ValueTask<SessionDescriptor> CreateSessionAsync(string name, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new SessionDescriptor("session", name, 0, DateTimeOffset.UtcNow));

        public ValueTask<bool> JoinSessionAsync(string sessionId, string playerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);

        public ValueTask<bool> LeaveSessionAsync(string sessionId, string playerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);

        public IAsyncEnumerable<SessionDescriptor> ListSessionsAsync(CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<SessionDescriptor>();

        public IReadOnlyCollection<SessionDescriptor> Snapshot(int take = 100) =>
            new[] { new SessionDescriptor("sess-1", "Alpha Node", 3, DateTimeOffset.UtcNow) };
    }
}
