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
    /// <summary>Pre-match countdown: no movement/fire/bot planning and a stopped match clock for PreMatchCountdownTicks.</summary>
    public sealed class PreMatchCountdownTests
    {
        private static async Task<OfflineGameSession> StartAsync(int countdown)
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            var session = new OfflineGameSession(catalog, (_, __) => new FakeMapRuntime(new FloorWorld(), new NoNav()), allowFakeMapRuntime: true);
            session.PreMatchCountdownTicks = countdown;
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            return session;
        }

        private static InputCommand Walk(EntityId entity, uint tick, uint sequence, bool fire) =>
            new InputCommand(entity, tick, sequence, 0f, 1f, 0f, 0f,
                fire ? InputButtons.FireHeld | InputButtons.FirePressed : InputButtons.None, 0);

        private static ActorLifecycleState Human(OfflineGameSession s)
        {
            Assert.True(s.Lifecycle!.TryGetActorState(s.HumanEntityId, out var state));
            return state;
        }

        [Fact]
        public void Default_has_no_countdown_and_constant_is_300()
        {
            Assert.Equal(300, SessionDefaults.PreMatchCountdownTicks);
            var session = new OfflineGameSession(StaticMapCatalog.CreateReleaseSeed(), (_, __) => new FakeMapRuntime(new FloorWorld(), new NoNav()), true);
            Assert.Equal(0, session.PreMatchCountdownTicks);
            Assert.Equal(0, session.PreMatchTicksRemaining);
        }

        [Fact]
        public async Task Countdown_blocks_human_movement_and_fire_and_bots_and_clock()
        {
            using var session = await StartAsync(SessionDefaults.PreMatchCountdownTicks);
            Assert.Equal(300, session.PreMatchTicksRemaining);
            var before = Human(session);
            var human = session.HumanEntityId;
            for (uint i = 0; i < 300; i++)
            {
                Assert.True(session.Step(Walk(human, session.CompletedTick, i + 1, fire: true)).Succeeded);
                Assert.Equal(GameMatchPhase.Playing, session.Phase);
                Assert.Equal(299 - (int)i, session.PreMatchTicksRemaining);
            }
            var after = Human(session);
            Assert.Equal(before.Feet.X, after.Feet.X, 3);
            Assert.Equal(before.Feet.Z, after.Feet.Z, 3);
            Assert.True(after.Alive);
            Assert.Equal(0L, session.Measurement.Shots);
            Assert.Equal(0L, session.Measurement.DamageEvents);
            // Bots were not planned: no vision rays, no path requests.
            Assert.Equal(0L, session.Measurement.AiRays);
            Assert.Equal(0L, session.Measurement.PathRequests);
            Assert.Equal(300u, session.CompletedTick);
            Assert.Equal(0u, session.TryAcquireSnapshot()!.ActiveTicksElapsed);
        }

        [Fact]
        public async Task After_countdown_movement_fire_and_clock_work()
        {
            using var session = await StartAsync(SessionDefaults.PreMatchCountdownTicks);
            var human = session.HumanEntityId;
            for (int i = 0; i < 300; i++) Assert.True(session.Step(null).Succeeded);
            Assert.Equal(0, session.PreMatchTicksRemaining);
            var before = Human(session);
            for (uint i = 0; i < 60; i++)
                Assert.True(session.Step(Walk(human, session.CompletedTick, i + 1, fire: i == 0)).Succeeded);
            var after = Human(session);
            float moved = System.Math.Abs(after.Feet.X - before.Feet.X) + System.Math.Abs(after.Feet.Z - before.Feet.Z);
            Assert.True(moved > 1f, "moved " + moved);
            Assert.True(session.Measurement.Shots > 0);
            Assert.Equal(60u, session.TryAcquireSnapshot()!.ActiveTicksElapsed);
        }

        [Fact]
        public async Task Zero_countdown_keeps_original_tick_semantics()
        {
            using var session = await StartAsync(0);
            Assert.Equal(0, session.PreMatchTicksRemaining);
            var human = session.HumanEntityId;
            var before = Human(session);
            for (uint i = 0; i < 30; i++)
                Assert.True(session.Step(Walk(human, session.CompletedTick, i + 1, fire: false)).Succeeded);
            Assert.Equal(30u, session.TryAcquireSnapshot()!.ActiveTicksElapsed);
            var after = Human(session);
            Assert.True(System.Math.Abs(after.Feet.X - before.Feet.X) + System.Math.Abs(after.Feet.Z - before.Feet.Z) > 0.5f);
        }

        /// <summary>Walkable horizontal floor planes at integer heights so the motor settles instead of falling out of the world.</summary>
        internal sealed class FloorWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
            {
                if (displacement.Y >= 0f) return new SweepHit(QueryStatus.NoHit);
                float bottom = capsule.BottomSphereCenter.Y - capsule.Radius;
                float plane = (float)System.Math.Floor(bottom + 0.001f);
                if (bottom + displacement.Y > plane) return new SweepHit(QueryStatus.NoHit);
                float distance = System.Math.Max(0f, (bottom - plane) / -displacement.Y * System.Math.Abs(displacement.Y));
                var point = new Float3(capsule.BottomSphereCenter.X, plane, capsule.BottomSphereCenter.Z);
                return new SweepHit(QueryStatus.Hit, new QueryHit(distance, point, new Float3(0f, 1f, 0f), default, new SurfaceId(1), 1, 1, 1));
            }
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
        }

        internal sealed class NoNav : INavigation
        {
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) => new PathTicket(new NavTicketId(1), actor, navRevision);
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned) => new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            public void Cancel(PathTicket ticket) { }
        }
    }
}
