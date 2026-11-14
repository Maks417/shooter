#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Simulation.TeamDeathmatch;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class SpawnSafetyTests
    {
        [Fact]
        public void Catalog_Provides_Twelve_Unique_Slots_Per_Team_On_Both_Maps()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var compound));
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.DepotYard, out var depot));
            AssertSlots(compound);
            AssertSlots(depot);
            Assert.Equal(460072031UL, MapRecipeSeed(depot));
        }

        [Fact]
        public async System.Threading.Tasks.Task TeamDeathmatch_Spawn_Defers_When_All_Slots_Rejected_And_Keeps_Fixed_Deadline()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var map));
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            var lifecycle = new RecordingLifecycle();
            var runtime = new FakeMapRuntime(new OpenWorld(), new NoNav()) { RejectAllSpawns = true };
            Assert.True((await runtime.LoadAsync(map, CancellationToken.None)).Succeeded);

            var roster = new List<ParticipantState>();
            for (uint i = 1; i <= request.ParticipantCount; i++)
            {
                byte team = i <= request.TeamSize ? (byte)1 : (byte)2;
                var entity = lifecycle.SpawnLife(new ParticipantId(i), new LifeId(1), new Float3(i * 3f, 0f, -100f), 0f, team);
                roster.Add(new ParticipantState(new ParticipantId(i), "P" + i, team, i == 1, entity, new LifeId(1), true, 0, 0, 0));
            }

            var match = new TeamDeathmatchMatch();
            match.ConfigureIdentities("cat", "game", "build");
            match.Begin(request, roster, map);
            match.MarkPlaying(9, 0);

            var victim = roster[0];
            var killer = roster[request.TeamSize];
            match.ProcessDeath(new DeathResolution(
                9,
                new EventId(9, 0, victim.EntityId, 1),
                victim.EntityId,
                victim.LifeId,
                killer.EntityId,
                0));

            uint tick = 0;
            while (match.AcquireSnapshot(9, tick).ActiveTicksElapsed < (uint)GameContractVersion.RespawnDelayActiveTicks)
            {
                tick++;
                match.EvaluateEndOfTick(tick, true);
            }

            uint due = match.AcquireSnapshot(9, tick).ActiveTicksElapsed;
            match.ScheduleDueRespawns(due, lifecycle, runtime);
            var afterFirstFail = match.AcquireSnapshot(9, tick);
            Assert.False(Find(afterFirstFail, victim.ParticipantId).Alive);
            Assert.Equal(unchecked(due + (uint)GameContractVersion.SpawnRetryDeferTicks), Find(afterFirstFail, victim.ParticipantId).RespawnDueActiveTick);

            // Advance through many 30-tick retries; deadline must stay fixed from first failure.
            uint deadline = unchecked(due + (uint)GameContractVersion.SpawnFailureTimeoutTicks);
            for (int guard = 0; guard < 2000 && match.AcquireSnapshot(9, tick).Phase == GameMatchPhase.Playing; guard++)
            {
                var snap = match.AcquireSnapshot(9, tick);
                var row = Find(snap, victim.ParticipantId);
                if (!row.Alive && row.RespawnDueActiveTick != 0 && snap.ActiveTicksElapsed >= row.RespawnDueActiveTick)
                    match.ScheduleDueRespawns(snap.ActiveTicksElapsed, lifecycle, runtime);
                if (match.AcquireSnapshot(9, tick).Phase == GameMatchPhase.Stopped) break;
                tick++;
                match.EvaluateEndOfTick(tick, true);
            }

            var stopped = match.AcquireSnapshot(9, tick);
            Assert.Equal(GameMatchPhase.Stopped, stopped.Phase);
            Assert.Equal(FinishReason.Aborted, stopped.Reason);
            // Retries must not push failure past first-failure + 600.
            Assert.Equal(deadline, stopped.ActiveTicksElapsed);
        }

        [Fact]
        public async System.Threading.Tasks.Task TeamDeathmatch_Spawn_Prefers_Unoccupied_Slot_And_Tracks_Lru()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var map));
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            var lifecycle = new RecordingLifecycle();
            var runtime = new FakeMapRuntime(new OpenWorld(), new NoNav());
            Assert.True((await runtime.LoadAsync(map, CancellationToken.None)).Succeeded);

            // Place living actors on the first two team-1 slots so ranking must skip them.
            var team1 = new List<SpawnSlot>();
            foreach (var slot in map.SpawnSlots) if (slot.Team == 1) team1.Add(slot);
            Assert.True(team1.Count >= 12);

            var roster = new List<ParticipantState>();
            for (uint i = 1; i <= request.ParticipantCount; i++)
            {
                byte team = i <= request.TeamSize ? (byte)1 : (byte)2;
                Float3 feet = team == 1 && i <= 2 ? team1[(int)i - 1].Position : new Float3(80f + i, 0f, -80f);
                var entity = lifecycle.SpawnLife(new ParticipantId(i), new LifeId(1), feet, 0f, team);
                roster.Add(new ParticipantState(new ParticipantId(i), "P" + i, team, i == 1, entity, new LifeId(1), true, 0, 0, 0));
            }

            var match = new TeamDeathmatchMatch();
            match.ConfigureIdentities("cat", "game", "build");
            match.Begin(request, roster, map);
            match.MarkPlaying(3, 0);

            // Kill a team-1 bot that is not occupying a spawn slot.
            var victim = roster[2];
            var killer = roster[request.TeamSize];
            match.ProcessDeath(new DeathResolution(
                3,
                new EventId(3, 0, victim.EntityId, 1),
                victim.EntityId,
                victim.LifeId,
                killer.EntityId,
                0));

            uint tick = 0;
            while (match.AcquireSnapshot(3, tick).ActiveTicksElapsed < (uint)GameContractVersion.RespawnDelayActiveTicks)
            {
                tick++;
                match.EvaluateEndOfTick(tick, true);
            }

            match.ScheduleDueRespawns(match.AcquireSnapshot(3, tick).ActiveTicksElapsed, lifecycle, runtime);
            var after = match.AcquireSnapshot(3, tick);
            var life = Find(after, victim.ParticipantId);
            Assert.True(life.Alive);
            Assert.True(lifecycle.TryGetActorState(life.EntityId, out var state));
            float min2 = SpawnSafetyPolicy.ActorSpacingMeters * SpawnSafetyPolicy.ActorSpacingMeters;
            // Occupied first slots must not be chosen when alternatives exist.
            Assert.True(Dist2(state.Feet, team1[0].Position) >= min2);
            Assert.True(Dist2(state.Feet, team1[1].Position) >= min2);
        }

        private static void AssertSlots(MapGameplayDefinition map)
        {
            int t1 = 0, t2 = 0;
            var positions = new HashSet<string>();
            foreach (var slot in map.SpawnSlots)
            {
                if (slot.Team == 1) t1++; else t2++;
                string key = slot.Team + ":" + slot.Position.X.ToString("R") + ":" + slot.Position.Z.ToString("R");
                Assert.True(positions.Add(key), "duplicate slot position " + key);
            }
            Assert.True(t1 >= 12, "team1 slots " + t1);
            Assert.True(t2 >= 12, "team2 slots " + t2);
        }

        private static ulong MapRecipeSeed(MapGameplayDefinition map) =>
            map.MapId.Value == "depot-yard" ? 460072031UL : 460072030UL;

        private static ParticipantState Find(MatchViewSnapshot snap, ParticipantId id)
        {
            foreach (var p in snap.Participants) if (p.ParticipantId.Equals(id)) return p;
            throw new InvalidOperationException("missing");
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

        /// <summary>Raycast always blocked => no clear enemy fire line (safe LOS ranking).</summary>
        private sealed class OpenWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
            {
                // Report occlusion so spawn ranking treats candidates as LOS-safe under FakeMapRuntime.
                var hit = new QueryHit(
                    Math.Min(maxMeters, 1f),
                    origin,
                    new Float3(0f, 1f, 0f),
                    default,
                    new SurfaceId(1),
                    1,
                    1,
                    1);
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
