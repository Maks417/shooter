#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Simulation.TeamDeathmatch;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class GameContractTests
    {
        [Fact]
        public void Version_Is_Game_v1()
        {
            Assert.Equal("Game-v1", GameContractVersion.Current);
            Assert.Equal(1u, GameContractVersion.RuleVersion);
            Assert.Equal(150, GameContractVersion.ScoreLimit);
            Assert.Equal(54000, GameContractVersion.TimeLimitActiveTicks);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(7)]
        public void SessionRequest_Rejects_Invalid_TeamSize(byte size)
        {
            Assert.Throws<ArgumentException>(() => new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, size, 460072030UL));
        }

        [Fact]
        public void SessionRequest_Accepts_5_And_6()
        {
            var a = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            var b = new SessionRequest(WellKnownMaps.DepotYard, WellKnownModes.TeamDeathmatch, 6, 460072031UL);
            Assert.Equal(10, a.ParticipantCount);
            Assert.Equal(12, b.ParticipantCount);
        }

        [Fact]
        public void Catalog_Rejects_Unknown_And_Bad_Size()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.Equal(2, catalog.Count);
            Assert.False(catalog.RejectUnknown(new MapId("missing-map"), WellKnownModes.TeamDeathmatch, 5).Succeeded);
            Assert.False(catalog.RejectUnknown(WellKnownMaps.TrainingCompound, new ModeId("deathmatch"), 5).Succeeded);
            Assert.False(catalog.RejectUnknown(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 4).Succeeded);
            Assert.True(catalog.RejectUnknown(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5).Succeeded);
        }

        [Fact]
        public void Settings_Store_Handles_Corrupt_And_Newer()
        {
            var store = new MemoryUserSettingsStore { CorruptOnLoad = true };
            Assert.False(store.Load(out _).Succeeded);
            store.CorruptOnLoad = false;
            store.NewerSchemaOnLoad = 9;
            Assert.Equal(GameErrorCode.UnsupportedVersion, store.Load(out _).Error);
            store.NewerSchemaOnLoad = null;
            Assert.True(store.Save(UserSettings.Defaults).Succeeded);
            Assert.True(store.Load(out var loaded).Succeeded);
            Assert.Equal(1u, loaded.Schema);
        }

        [Fact]
        public void TeamDeathmatch_ScoreLimit_Trade_Draw()
        {
            var match = CreateMatch(5, out var roster, out _, out var lifecycle, out var runtime);
            match.MarkPlaying(42, 0);
            uint tick = 0;
            CreditKills(match, lifecycle, runtime, ref roster, teamScoring: 1, count: 149, ref tick);
            CreditKills(match, lifecycle, runtime, ref roster, teamScoring: 2, count: 149, ref tick);
            var snap = match.AcquireSnapshot(42, tick);
            Assert.Equal(149u, snap.Team1Kills);
            Assert.Equal(149u, snap.Team2Kills);
            var killer1 = FindAlive(roster, 1);
            var victim2 = FindAlive(roster, 2);
            var killer2 = FindAlive(roster, 2, skip: victim2.ParticipantId);
            var victim1 = FindAlive(roster, 1, skip: killer1.ParticipantId);
            match.ProcessDeath(Death(42, killer1.EntityId, victim2.EntityId, victim2.LifeId, tick, 1));
            match.ProcessDeath(Death(42, killer2.EntityId, victim1.EntityId, victim1.LifeId, tick, 2));
            match.EvaluateEndOfTick(tick, true);
            var results = match.TryAcquireResults();
            Assert.NotNull(results);
            Assert.Equal(MatchOutcome.Draw, results!.Outcome);
            Assert.Equal(FinishReason.ScoreLimit, results.Reason);
            Assert.Equal(150u, results.Team1Kills);
            Assert.Equal(150u, results.Team2Kills);
        }

        [Fact]
        public void TeamDeathmatch_Deadline_53999_Then_54000()
        {
            var match = CreateMatch(5, out _, out _, out _, out _);
            match.MarkPlaying(7, 0);
            for (uint i = 0; i < 53999; i++) match.EvaluateEndOfTick(i, true);
            Assert.Null(match.TryAcquireResults());
            match.EvaluateEndOfTick(53999, true);
            var results = match.TryAcquireResults();
            Assert.NotNull(results);
            Assert.Equal(FinishReason.TimeLimit, results!.Reason);
            Assert.Equal(MatchOutcome.Draw, results.Outcome);
            Assert.Equal(54000u, results.ActiveDurationTicks);
        }

        [Fact]
        public void TeamDeathmatch_Duplicate_Death_Ignored()
        {
            var match = CreateMatch(5, out var roster, out _, out _, out _);
            match.MarkPlaying(9, 0);
            var victim = FindTeam(roster, 2);
            var killer = FindTeam(roster, 1);
            var death = Death(9, killer.EntityId, victim.EntityId, victim.LifeId, 3, 1);
            match.ProcessDeath(death);
            match.ProcessDeath(death);
            var snap = match.AcquireSnapshot(9, 3);
            Assert.Equal(1u, snap.Team1Kills);
            Assert.Equal(1u, FindById(snap, victim.ParticipantId).Deaths);
        }

        [Fact]
        public void TeamDeathmatch_Stale_Session_Death_Ignored()
        {
            var match = CreateMatch(5, out var roster, out _, out _, out _);
            match.MarkPlaying(11, 0);
            var victim = FindTeam(roster, 2);
            var killer = FindTeam(roster, 1);
            match.ProcessDeath(Death(999, killer.EntityId, victim.EntityId, victim.LifeId, 1, 1));
            Assert.Equal(0u, match.AcquireSnapshot(11, 1).Team1Kills);
        }

        [Fact]
        public async Task OfflineSession_Load_Start_Pause_Abort()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(w, n), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True(session.Validate(request).Succeeded);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.Equal(GameMatchPhase.Ready, session.Phase);
            Assert.True(session.Start().Succeeded);
            Assert.True(session.Pause().Succeeded);
            Assert.Equal(SessionPauseState.Paused, session.PauseState);
            Assert.True(session.Resume().Succeeded);
            Assert.True(session.RequestAbort().Succeeded);
            Assert.Null(session.TryAcquireResults());
        }

        [Fact]
        public async Task OfflineSession_Rejects_Fake_When_Not_Allowed()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(w, n), allowFakeMapRuntime: false);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            var result = await session.BeginLoadAsync(request, CancellationToken.None);
            Assert.Equal(GameErrorCode.InternalError, result.Error);
        }

        [Fact]
        public void MapId_Rejects_Uppercase()
        {
            Assert.Throws<ArgumentException>(() => new MapId("Training-Compound"));
        }

        private static TeamDeathmatchMatch CreateMatch(byte teamSize, out List<ParticipantState> roster, out MapGameplayDefinition map, out RecordingLifecycle lifecycle, out FakeMapRuntime runtime)
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out map!));
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, teamSize, 460072030UL);
            lifecycle = new RecordingLifecycle();
            runtime = new FakeMapRuntime(new NoWorld(), new NoNav());
            Assert.True(runtime.LoadAsync(map, CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            roster = new List<ParticipantState>();
            uint id = 1;
            int t1 = 0, t2 = 0;
            for (int i = 0; i < request.ParticipantCount; i++)
            {
                byte team = t1 < teamSize ? (byte)1 : (byte)2;
                if (team == 1) t1++; else t2++;
                var life = new LifeId(1);
                var entity = lifecycle.SpawnLife(new ParticipantId(id), life, new Float3(i, 0, 0), 0, team);
                roster.Add(new ParticipantState(new ParticipantId(id), "P" + id, team, team == 1 && t1 == 1, entity, life, true, 0, 0, 0));
                id++;
            }
            var match = new TeamDeathmatchMatch();
            match.ConfigureIdentities("cat", "game", "build");
            match.Begin(request, roster, map);
            return match;
        }

        private static void CreditKills(
            TeamDeathmatchMatch match,
            RecordingLifecycle lifecycle,
            FakeMapRuntime runtime,
            ref List<ParticipantState> roster,
            byte teamScoring,
            uint count,
            ref uint tick)
        {
            byte victimTeam = teamScoring == 1 ? (byte)2 : (byte)1;
            for (uint i = 0; i < count; i++)
            {
                var killer = FindAlive(roster, teamScoring);
                var victim = FindAlive(roster, victimTeam);
                match.ProcessDeath(Death(42, killer.EntityId, victim.EntityId, victim.LifeId, tick, 1));
                // Advance past respawn delay and install a fresh life.
                for (int step = 0; step < GameContractVersion.RespawnDelayActiveTicks; step++)
                {
                    tick++;
                    match.EvaluateEndOfTick(tick, true);
                }
                match.ScheduleDueRespawns(match.AcquireSnapshot(42, tick).ActiveTicksElapsed, lifecycle, runtime);
                roster = SnapshotRoster(match, tick);
            }
        }

        private static List<ParticipantState> SnapshotRoster(TeamDeathmatchMatch match, uint tick)
        {
            var snap = match.AcquireSnapshot(42, tick);
            var list = new List<ParticipantState>(snap.Participants.Count);
            foreach (var p in snap.Participants) list.Add(p);
            return list;
        }

        private static DeathResolution Death(ulong session, EntityId killer, EntityId victim, LifeId life, uint tick, ushort ordinal) =>
            new DeathResolution(session, new EventId(session, tick, victim, ordinal), victim, life, killer, tick);

        private static ParticipantState FindAlive(List<ParticipantState> roster, byte team, ParticipantId? skip = null)
        {
            foreach (var p in roster)
            {
                if (p.Team != team || !p.Alive) continue;
                if (skip != null && p.ParticipantId.Equals(skip.Value)) continue;
                return p;
            }
            throw new InvalidOperationException("alive team member");
        }

        private static ParticipantState FindTeam(List<ParticipantState> roster, byte team, ParticipantId? skip = null)
        {
            foreach (var p in roster)
            {
                if (p.Team != team) continue;
                if (skip != null && p.ParticipantId.Equals(skip.Value)) continue;
                return p;
            }
            throw new InvalidOperationException("team member");
        }

        private static ParticipantState FindById(MatchViewSnapshot snap, ParticipantId id)
        {
            foreach (var p in snap.Participants) if (p.ParticipantId.Equals(id)) return p;
            throw new InvalidOperationException("missing");
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
