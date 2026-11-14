#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Simulation.Session;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class WorldKillVolumeSessionTests
    {
        [Fact]
        public async Task Fallen_human_dies_environmentally_with_deaths_plus_one_no_team_point_and_respawn_scheduled()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var gameplay));
            var session = new OfflineGameSession(catalog, (_, __) => new FakeMapRuntime(new OpenWorld(), new NoNav()), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var lifecycle = Assert.IsType<HarnessActorLifecycle>(session.Lifecycle);
            using (session)
            {
                var before = session.TryAcquireSnapshot()!;
                var human = Find(before);
                Assert.True(lifecycle.Simulation.RelocateActor(human.EntityId,
                    new Float3(gameplay!.Bounds.Min.X, gameplay.Bounds.Min.Y - 10.5f, gameplay.Bounds.Min.Z), 0));

                Assert.True(session.Step(null).Succeeded);

                var after = session.TryAcquireSnapshot()!;
                var now = Find(after);
                Assert.False(now.Alive);
                Assert.Equal(human.Deaths + 1, now.Deaths);
                Assert.Equal(human.Kills, now.Kills);
                Assert.Equal(before.Team1Kills, after.Team1Kills);
                Assert.Equal(before.Team2Kills, after.Team2Kills);
                Assert.True(now.RespawnDueActiveTick > after.ActiveTicksElapsed);

                // Dead actor is neither killed again nor rescued on later ticks.
                Assert.True(session.Step(null).Succeeded);
                Assert.Equal(human.Deaths + 1, Find(session.TryAcquireSnapshot()!).Deaths);
            }
        }

        private static ParticipantState Find(MatchViewSnapshot snap)
        {
            foreach (var p in snap.Participants) if (p.IsHuman) return p;
            throw new InvalidOperationException("human");
        }

        private sealed class OpenWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
            {
                var hit = new QueryHit(Math.Min(maxMeters, 1f), origin, new Float3(0f, 1f, 0f), default, new SurfaceId(1), 1, 1, 1);
                return new RayHit(QueryStatus.Hit, hit);
            }
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
