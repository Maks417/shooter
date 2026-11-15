#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Simulation.Session;
using Shooter.Simulation.TeamDeathmatch;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>Relaxed spawn fallback (no safe slot for 120 active ticks) and recoverable SpawnFailure abort.</summary>
    public sealed class SpawnRelaxedFallbackTests
    {
        [Fact]
        public async Task All_Slots_Exposed_Relaxed_Fallback_Respawns_By_Due_Plus_120_And_Picks_Farthest_Enemy_Slot()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var map));
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            var lifecycle = new RecordingLifecycle();
            var runtime = new FakeMapRuntime(new ClearWorld(), new NoNav());
            Assert.True((await runtime.LoadAsync(map, CancellationToken.None)).Succeeded);

            var team1 = new List<SpawnSlot>();
            foreach (var slot in map.SpawnSlots) if (slot.Team == 1) team1.Add(slot);
            float cx = 0f, cz = 0f;
            foreach (var slot in team1) { cx += slot.Position.X; cz += slot.Position.Z; }
            cx /= team1.Count; cz /= team1.Count;
            var centroid = new Float3(cx, 0f, cz);

            var roster = new List<ParticipantState>();
            for (uint i = 1; i <= request.ParticipantCount; i++)
            {
                byte team = i <= request.TeamSize ? (byte)1 : (byte)2;
                // Enemies sit at the team-1 spawn centroid with clear fire lines to every slot; friendlies far away.
                Float3 feet = team == 2 ? new Float3(cx + i * 0.01f, 0f, cz) : new Float3(900f + i * 5f, 0f, 900f);
                var entity = lifecycle.SpawnLife(new ParticipantId(i), new LifeId(1), feet, 0f, team);
                roster.Add(new ParticipantState(new ParticipantId(i), "P" + i, team, i == 1, entity, new LifeId(1), true, 0, 0, 0));
            }

            var living = new List<EntityId>();
            foreach (var p in roster) living.Add(p.EntityId);
            // Precondition: the strict policy rejects every slot.
            Assert.False(SpawnSafetyPolicy.TryPick(1, map.SpawnSlots, runtime, lifecycle, living,
                new Dictionary<uint, uint>(), new List<Float3>(), out _));

            var match = new TeamDeathmatchMatch();
            match.ConfigureIdentities("cat", "game", "build");
            match.Begin(request, roster, map);
            match.MarkPlaying(9, 0);
            var victim = roster[0];
            var killer = roster[request.TeamSize];
            match.ProcessDeath(new DeathResolution(9, new EventId(9, 0, victim.EntityId, 1), victim.EntityId, victim.LifeId, killer.EntityId, 0));

            uint tick = 0;
            while (match.AcquireSnapshot(9, tick).ActiveTicksElapsed < (uint)GameContractVersion.RespawnDelayActiveTicks)
            {
                tick++;
                match.EvaluateEndOfTick(tick, true);
            }
            uint due = match.AcquireSnapshot(9, tick).ActiveTicksElapsed;

            for (int guard = 0; guard < 400; guard++)
            {
                var snap = match.AcquireSnapshot(9, tick);
                var row = Find(snap, victim.ParticipantId);
                if (row.Alive) break;
                if (row.RespawnDueActiveTick != 0 && snap.ActiveTicksElapsed >= row.RespawnDueActiveTick)
                    match.ScheduleDueRespawns(snap.ActiveTicksElapsed, lifecycle, runtime);
                if (Find(match.AcquireSnapshot(9, tick), victim.ParticipantId).Alive) break;
                Assert.Equal(GameMatchPhase.Playing, match.AcquireSnapshot(9, tick).Phase);
                tick++;
                match.EvaluateEndOfTick(tick, true);
            }

            var after = match.AcquireSnapshot(9, tick);
            var respawned = Find(after, victim.ParticipantId);
            Assert.True(respawned.Alive);
            Assert.Equal(GameMatchPhase.Playing, after.Phase);
            // Never earlier than the full relax window, never later than relax window + one retry period.
            Assert.True(after.ActiveTicksElapsed >= due + (uint)SpawnSafetyPolicy.SpawnRelaxAfterTicks);
            Assert.True(after.ActiveTicksElapsed <= due + (uint)SpawnSafetyPolicy.SpawnRelaxAfterTicks + (uint)GameContractVersion.SpawnRetryDeferTicks);

            // Deterministic ranking: farthest nearest enemy wins (ties by LRU then slot id).
            Assert.True(lifecycle.TryGetActorState(respawned.EntityId, out var state));
            float chosen = Dist2(state.Feet, centroid);
            foreach (var slot in team1)
                Assert.True(Dist2(slot.Position, centroid) <= chosen + 0.01f);
        }

        [Fact]
        public async Task Session_Human_Dies_And_Respawns_With_New_Entity_Full_Health_And_Rebound_Input()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var gameplay));
            using var session = new OfflineGameSession(catalog, (_, __) => new FakeMapRuntime(new ClearWorld(), new NoNav()), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var lifecycle = Assert.IsType<HarnessActorLifecycle>(session.Lifecycle);

            var before = Human(session.TryAcquireSnapshot()!);
            EntityId oldEntity = before.EntityId;
            Assert.Equal(oldEntity, session.HumanEntityId);
            Assert.True(lifecycle.Simulation.RelocateActor(oldEntity,
                new Float3(gameplay!.Bounds.Min.X, gameplay.Bounds.Min.Y - 10.5f, gameplay.Bounds.Min.Z), 0));
            Assert.True(session.Step(null).Succeeded);
            Assert.False(Human(session.TryAcquireSnapshot()!).Alive);

            bool respawned = false;
            for (int i = 0; i < 180 + 600 && !respawned; i++)
            {
                Assert.True(session.Step(null).Succeeded);
                Assert.Null(session.TerminalFault);
                respawned = Human(session.TryAcquireSnapshot()!).Alive;
            }
            Assert.True(respawned);
            var after = Human(session.TryAcquireSnapshot()!);
            Assert.NotEqual(oldEntity, after.EntityId);
            Assert.Equal(before.LifeId.Value + 1, after.LifeId.Value);
            Assert.Equal(after.EntityId, session.HumanEntityId);
            Assert.True(lifecycle.TryGetActorState(after.EntityId, out var state));
            Assert.Equal(100, state.Health);
            // Rebound input addressed to the new entity is accepted.
            var command = new InputCommand(after.EntityId, session.CompletedTick, 1, 0, 0, 0, 0, InputButtons.None, 0);
            Assert.True(session.Step(command).Succeeded);
            Assert.Equal(GameMatchPhase.Playing, session.Phase);
        }

        [Fact]
        public async Task Session_Reports_SpawnFailure_Terminal_Phase_When_No_Slot_Is_Navigable()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var gameplay));
            FakeMapRuntime? fake = null;
            using var session = new OfflineGameSession(catalog, (_, __) => fake = new FakeMapRuntime(new ClearWorld(), new NoNav()), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var lifecycle = Assert.IsType<HarnessActorLifecycle>(session.Lifecycle);
            Assert.NotNull(fake);
            fake!.RejectAllSpawns = true;

            var human = Human(session.TryAcquireSnapshot()!);
            Assert.True(lifecycle.Simulation.RelocateActor(human.EntityId,
                new Float3(gameplay!.Bounds.Min.X, gameplay.Bounds.Min.Y - 10.5f, gameplay.Bounds.Min.Z), 0));

            Assert.Null(session.TerminalFault);
            int guard = 0;
            while (session.TerminalFault == null && guard++ < 2000)
                Assert.True(session.Step(null).Succeeded);

            Assert.NotNull(session.TerminalFault);
            Assert.Equal(GameErrorCode.SpawnFailure, session.TerminalFault!.Error);
            Assert.Equal(GameMatchPhase.Stopped, session.Phase);
            // Clean terminal state: further steps are harmless no-ops and the shell can stop and reload.
            Assert.True(session.Step(null).Succeeded);
            Assert.True(session.Stop().Succeeded);
            Assert.Equal(GameErrorCode.SpawnFailure, session.TerminalFault!.Error);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.Null(session.TerminalFault);
        }

        private static ParticipantState Find(MatchViewSnapshot snap, ParticipantId id)
        {
            foreach (var p in snap.Participants) if (p.ParticipantId.Equals(id)) return p;
            throw new InvalidOperationException("missing");
        }

        private static ParticipantState Human(MatchViewSnapshot snap)
        {
            foreach (var p in snap.Participants) if (p.IsHuman) return p;
            throw new InvalidOperationException("human");
        }

        private static float Dist2(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private sealed class RecordingLifecycle : IActorLifecycle
        {
            private readonly Dictionary<ulong, ActorLifecycleState> states = new Dictionary<ulong, ActorLifecycleState>();
            private ulong next = 1;
            public int ActiveActorCount => states.Count;
            public EntityId SpawnLife(ParticipantId participant, LifeId life, Float3 feet, float yaw, byte team)
            {
                var id = new EntityId(next++);
                states[id.Value] = new ActorLifecycleState(id, team, true, 100, feet, yaw, 0, 0, 30, 90);
                return id;
            }
            public void DespawnLife(EntityId entity) => states.Remove(entity.Value);
            public void ResetLife(EntityId entity, Float3 feet, float yaw)
            {
                var prev = states[entity.Value];
                states[entity.Value] = new ActorLifecycleState(entity, prev.Team, true, 100, feet, yaw, 0, 0, 30, 90);
            }
            public bool TryGetActorState(EntityId entity, out ActorLifecycleState state) => states.TryGetValue(entity.Value, out state);
        }

        /// <summary>Nothing is ever hit: every enemy has a clear fire line (strict spawn filter rejects).</summary>
        private sealed class ClearWorld : IWorldQueries
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
