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
using Shooter.Simulation;
using Shooter.Simulation.Session;
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
            Assert.Equal(2u, loaded.Schema);
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
            foreach (var row in results.FinalRows) Assert.Equal(0u, row.RespawnDueActiveTick);
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
        public void TeamDeathmatch_Abort_Cancels_Pending_Respawns()
        {
            var match = CreateMatch(5, out var roster, out _, out _, out _);
            match.MarkPlaying(12, 0);
            var victim = FindTeam(roster, 2);
            var killer = FindTeam(roster, 1);
            match.ProcessDeath(Death(12, killer.EntityId, victim.EntityId, victim.LifeId, 0, 1));
            Assert.Equal(1u, match.AcquireSnapshot(12, 0).PendingRespawns);
            match.Abort(12);
            var stopped = match.AcquireSnapshot(12, 0);
            Assert.Equal(GameMatchPhase.Stopped, stopped.Phase);
            Assert.Equal(0u, stopped.PendingRespawns);
        }

        [Fact]
        public async Task RealHarness_Replaces_OneHundred_Lives_With_Bounded_Authority()
        {
            const ulong sessionId = 77;
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var map));
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 6, 460072030UL);
            var runtime = new FakeMapRuntime(new NoWorld(), new NoNav());
            Assert.True((await runtime.LoadAsync(map, CancellationToken.None)).Succeeded);
            var harness = new CombatHarnessSimulation(runtime.WorldQueries, sessionId, request.Seed);
            var lifecycle = new HarnessActorLifecycle(harness);
            var roster = new List<ParticipantState>(request.ParticipantCount);
            var allEntityIds = new HashSet<ulong>();
            for (int i = 0; i < request.ParticipantCount; i++)
            {
                byte team = i < request.TeamSize ? (byte)1 : (byte)2;
                var participant = new ParticipantId((uint)(i + 1));
                var entity = lifecycle.SpawnLife(participant, new LifeId(1), new Float3(i * 4, 0, 0), 0, team); // >40 m from both compact-arena staging zones
                allEntityIds.Add(entity.Value);
                roster.Add(new ParticipantState(participant, "P" + (i + 1), team, i == 0, entity, new LifeId(1), true, 0, 0, 0));
            }

            var match = new TeamDeathmatchMatch();
            match.ConfigureIdentities("cat", "game", "build");
            match.Begin(request, roster, map);
            match.MarkPlaying(sessionId, 0);
            uint simulationTick = 0;
            ulong previousMaximumId = (ulong)request.ParticipantCount;

            for (int replacement = 0; replacement < 100; replacement++)
            {
                var before = match.AcquireSnapshot(sessionId, simulationTick);
                var victim = before.Participants[replacement % before.Participants.Count];
                ParticipantState killer = FindAliveOnOtherTeam(before, victim.Team);
                lifecycle.ResetLife(killer.EntityId, new Float3(0, 0, 0), 0);
                lifecycle.ResetLife(victim.EntityId, new Float3(0, 0, 5), 0);

                bool killed = false;
                for (uint fireTick = 0; fireTick < 120 && !killed; fireTick++)
                {
                    var command = new InputCommand(
                        killer.EntityId,
                        simulationTick,
                        fireTick + 1,
                        0,
                        0,
                        0,
                        0,
                        InputButtons.FireHeld | (fireTick == 0 ? InputButtons.FirePressed : InputButtons.None),
                        0);
                    harness.Step(simulationTick, new[] { command });
                    using (var authority = harness.AcquireSnapshot())
                        lifecycle.SyncFromSnapshot(authority.Snapshot);
                    using (var events = harness.AcquireEvents())
                    {
                        for (int i = 0; i < events.Events.Count; i++)
                        {
                            var combatEvent = events.Events[i];
                            if (combatEvent.Kind != CombatEventKind.Death || !combatEvent.Target.Equals(victim.EntityId)) continue;
                            match.ProcessDeath(new DeathResolution(
                                sessionId,
                                combatEvent.Id,
                                victim.EntityId,
                                victim.LifeId,
                                combatEvent.Instigator,
                                simulationTick));
                            killed = true;
                        }
                    }
                    if (killed) lifecycle.RetireLife(victim.EntityId, before.ActiveTicksElapsed);
                    match.EvaluateEndOfTick(simulationTick, true);
                    simulationTick++;
                }
                Assert.True(killed);

                var dead = FindById(match.AcquireSnapshot(sessionId, simulationTick - 1), victim.ParticipantId);
                uint deathActiveTick = unchecked(dead.RespawnDueActiveTick - (uint)GameContractVersion.RespawnDelayActiveTicks);
                while (match.AcquireSnapshot(sessionId, simulationTick).ActiveTicksElapsed < dead.RespawnDueActiveTick)
                {
                    harness.Step(simulationTick, ReadOnlyMemory<InputCommand>.Empty);
                    using (var authority = harness.AcquireSnapshot())
                        lifecycle.SyncFromSnapshot(authority.Snapshot);
                    match.EvaluateEndOfTick(simulationTick, true);
                    lifecycle.AdvanceActiveTick(match.AcquireSnapshot(sessionId, simulationTick).ActiveTicksElapsed);
                    simulationTick++;
                }

                var due = match.AcquireSnapshot(sessionId, simulationTick);
                Assert.Equal((uint)GameContractVersion.RespawnDelayActiveTicks, due.ActiveTicksElapsed - deathActiveTick);
                match.ScheduleDueRespawns(due.ActiveTicksElapsed, lifecycle, runtime);
                var after = match.AcquireSnapshot(sessionId, simulationTick);
                var replacementLife = FindById(after, victim.ParticipantId);
                Assert.True(replacementLife.Alive);
                Assert.Equal(unchecked(victim.LifeId.Value + 1), replacementLife.LifeId.Value);
                Assert.True(replacementLife.EntityId.Value > previousMaximumId);
                previousMaximumId = replacementLife.EntityId.Value;
                Assert.True(allEntityIds.Add(replacementLife.EntityId.Value));
                Assert.Equal(request.ParticipantCount, lifecycle.ActiveActorCount);
                Assert.Equal(request.ParticipantCount, harness.ActiveActorCount);
                Assert.InRange(lifecycle.CosmeticCorpseCount, 0, HarnessActorLifecycle.MaxCosmeticCorpses);
            }

            harness.Step(simulationTick, ReadOnlyMemory<InputCommand>.Empty);
            using var finalAuthority = harness.AcquireSnapshot();
            Assert.Equal(request.ParticipantCount, finalAuthority.Snapshot.Entities.Count);
            Assert.Equal(request.ParticipantCount * 6, finalAuthority.Snapshot.HitVolumes.Count);
            var finalMatch = match.AcquireSnapshot(sessionId, simulationTick);
            Assert.Equal(100u, finalMatch.Team1Kills + finalMatch.Team2Kills);
            uint retainedDeaths = 0;
            foreach (var participant in finalMatch.Participants) retainedDeaths += participant.Deaths;
            Assert.Equal(100u, retainedDeaths);
            Assert.True(FindById(finalMatch, new ParticipantId(1)).LifeId.Value > 1);
            Assert.True(FindById(finalMatch, new ParticipantId(7)).LifeId.Value > 1);
        }

        [Fact]
        public void HarnessLifecycle_CapsAndExpiresCosmeticCorpses()
        {
            var lifecycle = new HarnessActorLifecycle(new CombatHarnessSimulation(new NoWorld()));
            for (uint i = 0; i < 5; i++)
            {
                var entity = lifecycle.SpawnLife(new ParticipantId(i + 1), new LifeId(1), default, 0, i < 3 ? (byte)1 : (byte)2);
                Assert.True(lifecycle.RetireLife(entity, 10));
            }
            Assert.Equal(HarnessActorLifecycle.MaxCosmeticCorpses, lifecycle.CosmeticCorpseCount);
            lifecycle.AdvanceActiveTick(10 + HarnessActorLifecycle.CosmeticCorpseLifetimeActiveTicks);
            Assert.Equal(0, lifecycle.CosmeticCorpseCount);
        }

        [Fact]
        public async Task OfflineSessionMovementViewUsesAuthorityStanceAndClearsOnStop()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(w, n), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var human = session.HumanEntityId;
            Assert.True(session.TryGetMovementView(human, out var standing));
            Assert.Equal(1.65f, standing.EyeHeight);
            Assert.True(session.Step(new InputCommand(human, 0, 1, 0, 0, 0, 0, InputButtons.CrouchHeld, 0)).Succeeded);
            Assert.True(session.TryGetMovementView(human, out var crouching));
            Assert.Equal(Stance.Crouching, crouching.Stance);
            Assert.Equal(1.05f, crouching.EyeHeight);
            Assert.False(crouching.Sliding);
            Assert.True(session.Pause().Succeeded);
            Assert.False(session.Step(new InputCommand(human, 1, 2, 0, 1, 0, 0, InputButtons.SprintHeld, 0), true).Succeeded);
            Assert.True(session.TryGetMovementView(human, out var paused));
            Assert.Equal(crouching.Stance, paused.Stance);
            session.Stop();
            Assert.False(session.TryGetMovementView(human, out _));
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
        public async Task OfflineSession_Rebinds_Bot_And_Human_Replacement_Lives()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(new NoWorld(), new NoNav()), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 6, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var lifecycle = Assert.IsType<HarnessActorLifecycle>(session.Lifecycle);
            var initial = session.TryAcquireSnapshot();
            Assert.NotNull(initial);
            var human = FindById(initial!, new ParticipantId(1));
            var botVictim = FindTeamSnapshot(initial!, 2);
            ArrangeDuel(lifecycle, initial!, human.EntityId, botVictim.EntityId);

            for (uint sequence = 1; sequence <= 120; sequence++)
            {
                var command = new InputCommand(
                    human.EntityId,
                    session.CompletedTick,
                    sequence,
                    0,
                    0,
                    0,
                    0,
                    InputButtons.FireHeld | (sequence == 1 ? InputButtons.FirePressed : InputButtons.None),
                    0);
                Assert.True(session.Step(command).Succeeded);
                var current = session.TryAcquireSnapshot()!;
                if (!FindById(current, botVictim.ParticipantId).Alive) break;
            }
            var botDead = FindById(session.TryAcquireSnapshot()!, botVictim.ParticipantId);
            Assert.False(botDead.Alive);
            uint botLife = botDead.LifeId.Value;
            while (FindById(session.TryAcquireSnapshot()!, botVictim.ParticipantId).LifeId.Value == botLife)
                Assert.True(session.Step(null).Succeeded);
            var botReplacement = FindById(session.TryAcquireSnapshot()!, botVictim.ParticipantId);
            Assert.True(botReplacement.Alive);
            Assert.NotEqual(botVictim.EntityId, botReplacement.EntityId);
            Assert.Equal(request.ParticipantCount, lifecycle.ActiveActorCount);

            var beforeHumanDeath = session.TryAcquireSnapshot()!;
            human = FindById(beforeHumanDeath, human.ParticipantId);
            var enemyShooter = FindById(beforeHumanDeath, botVictim.ParticipantId);
            ArrangeDuel(lifecycle, beforeHumanDeath, enemyShooter.EntityId, human.EntityId);
            for (int i = 0; i < 240 && FindById(session.TryAcquireSnapshot()!, human.ParticipantId).Alive; i++)
                Assert.True(session.Step(null).Succeeded);

            var humanDead = FindById(session.TryAcquireSnapshot()!, human.ParticipantId);
            Assert.False(humanDead.Alive);
            uint humanLife = humanDead.LifeId.Value;
            EntityId oldHumanEntity = humanDead.EntityId;
            while (FindById(session.TryAcquireSnapshot()!, human.ParticipantId).LifeId.Value == humanLife)
                Assert.True(session.Step(null).Succeeded);
            var humanReplacement = FindById(session.TryAcquireSnapshot()!, human.ParticipantId);
            Assert.True(humanReplacement.Alive);
            Assert.NotEqual(oldHumanEntity, humanReplacement.EntityId);
            Assert.Equal(humanReplacement.EntityId, session.HumanEntityId);
            Assert.Equal(CountAlive(session.TryAcquireSnapshot()!), lifecycle.ActiveActorCount);
            Assert.InRange(lifecycle.ActiveActorCount, 1, request.ParticipantCount);
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
        public async Task OfflineSession_Stop_Fences_Stale_Load_Completion()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            var delayed = new DelayedMapRuntime();
            using var session = new OfflineGameSession(catalog, (w, n) => delayed, allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            var loading = session.BeginLoadAsync(request, CancellationToken.None);
            Assert.Equal(GameMatchPhase.Loading, session.Phase);
            Assert.True(session.Stop().Succeeded);
            delayed.Complete();
            var result = await loading;
            Assert.Equal(GameErrorCode.StaleSession, result.Error);
            Assert.True(delayed.Unloaded);
            Assert.Equal(GameMatchPhase.Stopped, session.Phase);
            Assert.Null(session.Lifecycle);
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

        private static ParticipantState FindAliveOnOtherTeam(MatchViewSnapshot snapshot, byte team)
        {
            foreach (var participant in snapshot.Participants)
                if (participant.Alive && participant.Team != team) return participant;
            throw new InvalidOperationException("opposing participant");
        }

        private static ParticipantState FindTeamSnapshot(MatchViewSnapshot snapshot, byte team)
        {
            foreach (var participant in snapshot.Participants)
                if (participant.Team == team) return participant;
            throw new InvalidOperationException("team participant");
        }

        private static int CountAlive(MatchViewSnapshot snapshot)
        {
            int count = 0;
            foreach (var participant in snapshot.Participants)
                if (participant.Alive) count++;
            return count;
        }

        private static void ArrangeDuel(
            HarnessActorLifecycle lifecycle,
            MatchViewSnapshot snapshot,
            EntityId shooter,
            EntityId victim)
        {
            for (int i = 0; i < snapshot.Participants.Count; i++)
            {
                var participant = snapshot.Participants[i];
                if (!participant.Alive) continue;
                if (participant.EntityId.Equals(shooter))
                    lifecycle.ResetLife(participant.EntityId, new Float3(0, 0, 0), 0);
                else if (participant.EntityId.Equals(victim))
                    lifecycle.ResetLife(participant.EntityId, new Float3(0, 0, 5), 0);
                else
                    lifecycle.ResetLife(participant.EntityId, new Float3(80 + i * 3, 0, -80), 3f);
            }
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

        private sealed class DelayedMapRuntime : IMapRuntime
        {
            private readonly TaskCompletionSource<GameOperationResult> completion =
                new TaskCompletionSource<GameOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IWorldQueries WorldQueries { get; } = new NoWorld();
            public INavigation Navigation { get; } = new NoNav();
            public bool Unloaded { get; private set; }

            public Task<GameOperationResult> LoadAsync(MapGameplayDefinition definition, CancellationToken cancel) => completion.Task;
            public bool TryValidateSpawn(SpawnSlot slot, out string diagnostic)
            {
                diagnostic = "ok";
                return true;
            }
            public GameOperationResult Unload()
            {
                Unloaded = true;
                return GameOperationResult.Ok;
            }
            public void Complete() => completion.SetResult(GameOperationResult.Ok);
        }

    }
}
