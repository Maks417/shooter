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

        [Fact]
        public async Task Respawn_rebinds_bot_slots_without_rebuilding_the_planner_and_forwards_stimuli()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(w, n), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var counters = session.BotCounters;
            Assert.NotNull(counters);
            for (int i = 0; i < 30000 && session.Measurement.Respawns == 0; i++)
            {
                if (!session.Step(null).Succeeded) break;
            }
            Assert.True(session.Measurement.Respawns > 0, "no respawn happened within the step budget");
            // T082: the planner (and therefore every surviving bot's blackboard) outlives a respawn.
            Assert.Same(counters, session.BotCounters);
        }

        [Fact]
        public void Planner_damage_stimulus_uses_the_attackers_authoritative_position()
        {
            var planner = new TeamBotPlanner(new NoWorld(), new NoNav(), 1u << 8);
            var bot = new EntityId(1);
            var attacker = new EntityId(2);
            planner.RegisterBot(bot, new Float3(0, 0, 0), 0, team: 1);
            var lifecycle = new OneActorLifecycle(new ActorLifecycleState(attacker, 2, true, 100, new Float3(0, 0, -20), 0, 0, 0, 30, 90));
            planner.ObserveDamage(bot, attacker, lifecycle);
            var board = planner.Director.GetBoard(0);
            Assert.Equal(AiState.Investigate, board.State);
            Assert.Equal(HeardClass.Damage, board.Heard);
            Assert.InRange(board.LastHeard!.Value.Z, -21.6f, -18.4f);
            Assert.Equal(attacker, board.LastAttacker);
            planner.ObserveGunfire(attacker, new Float3(0, 1.5f, -20));
            Assert.Equal(1, planner.Counters.DamageReactions);
        }

        private sealed class OneActorLifecycle : IActorLifecycle
        {
            private readonly ActorLifecycleState state;
            public OneActorLifecycle(ActorLifecycleState state) { this.state = state; }
            public EntityId SpawnLife(ParticipantId participant, LifeId life, Float3 feet, float yaw, byte team) => throw new System.NotSupportedException();
            public void DespawnLife(EntityId entity) { }
            public void ResetLife(EntityId entity, Float3 feet, float yaw) { }
            public bool TryGetActorState(EntityId entity, out ActorLifecycleState actor)
            {
                actor = state;
                return entity.Equals(state.EntityId);
            }
            public int ActiveActorCount => 1;
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
