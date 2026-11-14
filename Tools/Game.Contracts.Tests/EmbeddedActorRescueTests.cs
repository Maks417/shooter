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
    public sealed class EmbeddedActorRescueTests
    {
        [Fact]
        public async Task Human_with_unresolved_overlap_is_relocated_without_scoring_or_identity_change()
        {
            var world = new EmbedWorld();
            var (session, lifecycle) = await StartAsync(world);
            using (session)
            {
                var before = session.TryAcquireSnapshot()!;
                var human = Find(before, true);
                Assert.True(lifecycle.TryGetActorState(human.EntityId, out var start));
                world.BadCenter = start.Feet;

                Assert.True(session.Step(null).Succeeded);

                var after = session.TryAcquireSnapshot()!;
                var now = Find(after, true);
                Assert.Equal(human.EntityId, now.EntityId);
                Assert.Equal(human.LifeId, now.LifeId);
                Assert.Equal(session.HumanEntityId, human.EntityId);
                Assert.True(now.Alive);
                Assert.Equal(human.Kills, now.Kills);
                Assert.Equal(human.Deaths, now.Deaths);
                Assert.Equal(before.Team1Kills, after.Team1Kills);
                Assert.Equal(before.Team2Kills, after.Team2Kills);
                Assert.True(lifecycle.TryGetActorState(human.EntityId, out var moved));
                Assert.True(moved.Alive);
                Assert.Equal(start.Health, moved.Health);
                Assert.Equal(start.Magazine, moved.Magazine);
                Assert.Equal(start.Reserve, moved.Reserve);
                Assert.True(Horizontal(start.Feet, moved.Feet) > 2f, "actor must leave the embedded position");
                Assert.True(lifecycle.Simulation.TryGetMotorState(human.EntityId, out var motor));
                Assert.Equal(moved.Feet.X, motor.Feet.X);
                Assert.Equal(moved.Feet.Z, motor.Feet.Z);

                // Next tick the actor stands in clear space: no further relocation.
                Assert.True(session.Step(null).Succeeded);
                Assert.True(lifecycle.TryGetActorState(human.EntityId, out var settled));
                Assert.True(Horizontal(moved.Feet, settled.Feet) < 0.5f);
            }
        }

        [Fact]
        public async Task Bot_with_unresolved_overlap_is_relocated_keeping_life()
        {
            var world = new EmbedWorld();
            var (session, lifecycle) = await StartAsync(world);
            using (session)
            {
                var before = session.TryAcquireSnapshot()!;
                var bot = Find(before, false);
                Assert.True(lifecycle.TryGetActorState(bot.EntityId, out var start));
                world.BadCenter = start.Feet;

                Assert.True(session.Step(null).Succeeded);

                var now = Find(session.TryAcquireSnapshot()!, false, bot.ParticipantId);
                Assert.Equal(bot.EntityId, now.EntityId);
                Assert.Equal(bot.LifeId, now.LifeId);
                Assert.True(now.Alive);
                Assert.Equal(bot.Deaths, now.Deaths);
                Assert.True(lifecycle.TryGetActorState(bot.EntityId, out var moved));
                Assert.True(Horizontal(start.Feet, moved.Feet) > 2f);
                Assert.Equal(start.Health, moved.Health);
            }
        }

        [Fact]
        public async Task Resolved_overlap_never_moves_anyone()
        {
            var (session, lifecycle) = await StartAsync(new EmbedWorld());
            using (session)
            {
                var before = session.TryAcquireSnapshot()!;
                var human = Find(before, true);
                Assert.True(lifecycle.TryGetActorState(human.EntityId, out var start));
                for (int i = 0; i < 5; i++) Assert.True(session.Step(null).Succeeded);
                Assert.True(lifecycle.TryGetActorState(human.EntityId, out var end));
                Assert.True(Horizontal(start.Feet, end.Feet) < 0.5f);
                Assert.Equal(before.Team1Kills, session.TryAcquireSnapshot()!.Team1Kills);
            }
        }

        private static async Task<(OfflineGameSession, HarnessActorLifecycle)> StartAsync(EmbedWorld world)
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            var session = new OfflineGameSession(catalog, (_, __) => new FakeMapRuntime(world, new NoNav()), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            return (session, Assert.IsType<HarnessActorLifecycle>(session.Lifecycle));
        }

        private static ParticipantState Find(MatchViewSnapshot snap, bool human, ParticipantId? id = null)
        {
            foreach (var p in snap.Participants)
            {
                if (id.HasValue ? p.ParticipantId.Equals(id.Value) : p.IsHuman == human && (human || p.Team == 1)) return p;
            }
            throw new InvalidOperationException("participant");
        }

        private static float Horizontal(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Open world whose motor overlap query reports an unresolvable penetration near BadCenter.</summary>
        private sealed class EmbedWorld : IWorldQueries
        {
            public Float3? BadCenter;
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
            {
                // Occluded: spawn ranking treats candidates as LOS-safe.
                var hit = new QueryHit(Math.Min(maxMeters, 1f), origin, new Float3(0f, 1f, 0f), default, new SurfaceId(1), 1, 1, 1);
                return new RayHit(QueryStatus.Hit, hit);
            }
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned)
            {
                if (BadCenter.HasValue)
                {
                    float dx = capsule.BottomSphereCenter.X - BadCenter.Value.X;
                    float dz = capsule.BottomSphereCenter.Z - BadCenter.Value.Z;
                    // BufferOverflow without written hits is the motor's "cannot resolve" path.
                    if (dx * dx + dz * dz < 1f) return new OverlapResult(QueryStatus.BufferOverflow, 0, 1);
                }
                return new OverlapResult(QueryStatus.NoHit, 0, 0);
            }
        }

        private sealed class NoNav : INavigation
        {
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) => new PathTicket(new NavTicketId(1), actor, navRevision);
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned) => new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            public void Cancel(PathTicket ticket) { }
        }
    }
}
