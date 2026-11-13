#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Simulation.AI;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class TeamBotPlannerTests
    {
        [Fact]
        public void Director_Accepts_Eleven_Bots()
        {
            var ai = new CombatAiDirector(new NoWorld(), new NoNav(), 1u << 8);
            for (int i = 0; i < CombatAiDirector.MaxBots; i++)
                ai.RegisterBot(new EntityId((ulong)(i + 1)), new Float3(i, 0, 0), 0);
            Assert.Equal(11, ai.BotCount);
            Assert.Throws<System.InvalidOperationException>(() => ai.RegisterBot(new EntityId(99), default, 0));
        }

        [Fact]
        public async Task OfflineSession_Steps_With_Fake_Map()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(w, n), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            for (int i = 0; i < 30; i++)
                Assert.True(session.Step(null).Succeeded);
            var snap = session.TryAcquireSnapshot();
            Assert.NotNull(snap);
            Assert.Equal(10, snap!.Participants.Count);
            Assert.True(session.HumanEntityId.IsValid);
        }

        private sealed class NoWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
        }

        private sealed class NoNav : INavigation
        {
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) => new PathTicket(new NavTicketId(1), actor, navRevision);
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned) => new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            public void Cancel(PathTicket ticket) { }
        }
    }
}
