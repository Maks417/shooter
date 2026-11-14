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
    public sealed class ProductMeasurementTests
    {
        [Fact]
        public void CountingWorldQueries_counts_ray_sweep_and_overlap()
        {
            var inner = new RecordingWorld();
            var counting = new CountingWorldQueries(inner);
            counting.Raycast(default, new Float3(0, 1, 0), 1f, default);
            counting.SweepCapsule(default, new Float3(0, 0.1f, 0), default);
            counting.Overlap(default, default, new HitBuffer(4));
            Assert.Equal(3, counting.TotalSamples);
        }

        [Fact]
        public void CaptureCoordinator_finish_results_replay_preserves_request_and_epochs()
        {
            var coord = new ProductTdmCaptureCoordinator();
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 6, 460072030UL);
            Assert.True(coord.TryBeginLoad(request, out _));
            Assert.Equal(1, coord.MatchIndex);
            ulong epoch1 = coord.LoadEpoch;
            coord.CompleteLoadSuccess();
            coord.MarkFinished(null, 54000);
            Assert.Equal(1, coord.MatchesCompleted);
            Assert.Equal(54000, coord.CumulativeActiveTicks);
            coord.EnterResults();
            Assert.True(coord.NeedsReplay(108000, 54000, soak: true));
            Assert.True(coord.TryPrepareReplay(out var replay, out var error));
            Assert.Equal("", error);
            Assert.NotNull(replay);
            Assert.Equal(request.MapId, replay!.MapId);
            Assert.Equal(ProductTdmCaptureCoordinator.Phase.Replaying, coord.Current);
            Assert.Equal(2, coord.MatchIndex);
            Assert.True(coord.LoadEpoch > epoch1);
            coord.CompleteLoadSuccess();
            Assert.False(coord.NeedsReplay(108000, 108000, soak: true));
        }

        [Fact]
        public void CaptureCoordinator_replays_when_capture_window_incomplete()
        {
            var coord = new ProductTdmCaptureCoordinator();
            var request = new SessionRequest(WellKnownMaps.DepotYard, WellKnownModes.TeamDeathmatch, 6, 460072031UL);
            Assert.True(coord.TryBeginLoad(request, out _));
            coord.CompleteLoadSuccess();
            coord.MarkFinished(null, 12000);
            coord.EnterResults();
            Assert.True(coord.NeedsReplay(10800, 6000, soak: false));
            Assert.False(coord.NeedsReplay(10800, 10800, soak: false));
        }

        [Fact]
        public async Task OfflineSession_WorkloadId_survives_load_and_host_can_reapply()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(w, n), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            // Host may set WorkloadId before load; ResetForNewMatch must not wipe identity.
            session.Measurement.WorkloadId = ProductTdmWorkloads.Compound5v5;
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.Equal(ProductTdmWorkloads.Compound5v5, session.Measurement.WorkloadId);
            // Host also re-applies after BeginLoad (belt-and-suspenders against older Reset wipe).
            session.Measurement.WorkloadId = ProductTdmWorkloads.Compound5v5;
            Assert.Equal(ProductTdmWorkloads.Compound5v5, session.Measurement.WorkloadId);
            Assert.True(session.Measurement.ContentBindings >= 1);
            Assert.False(string.IsNullOrEmpty(session.Measurement.PackageRootHash));
        }

        [Fact]
        public async Task OfflineSession_querySamples_follow_world_adapter_not_ai_path_sum()
        {
            var world = new RecordingWorld();
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (_, __) => new FakeMapRuntime(world, new NoNav()), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.IsType<QueryCountingMapRuntime>(session.MapRuntime);
            Assert.True(session.Start().Succeeded);
            Assert.True(session.Step(null).Succeeded);
            // querySamples tracks the CountingWorldQueries adapter (all ray/sweep/overlap), not aiRays+pathRequests.
            Assert.True(world.Total > 0);
            Assert.Equal(world.Total, session.DebugWorldQueryAdapterSamples);
            Assert.Equal(world.Total, session.Measurement.QuerySamples);
            Assert.NotEqual(
                session.Measurement.AiRays + session.Measurement.PathRequests,
                session.Measurement.QuerySamples);
        }

        [Fact]
        public async Task OfflineSession_respawns_count_life_replacements()
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
            var victim = FindTeam(initial!, 2);
            ArrangeDuel(lifecycle, initial!, human.EntityId, victim.EntityId);

            for (uint sequence = 1; sequence <= 180; sequence++)
            {
                var command = new InputCommand(
                    human.EntityId, session.CompletedTick, sequence, 0, 0, 0, 0,
                    InputButtons.FireHeld | (sequence == 1 ? InputButtons.FirePressed : InputButtons.None), 0);
                Assert.True(session.Step(command).Succeeded);
                if (!FindById(session.TryAcquireSnapshot()!, victim.ParticipantId).Alive) break;
            }
            Assert.False(FindById(session.TryAcquireSnapshot()!, victim.ParticipantId).Alive);
            long before = session.Measurement.Respawns;
            uint life = FindById(session.TryAcquireSnapshot()!, victim.ParticipantId).LifeId.Value;
            while (FindById(session.TryAcquireSnapshot()!, victim.ParticipantId).LifeId.Value == life)
                Assert.True(session.Step(null).Succeeded);
            Assert.True(session.Measurement.Respawns > before);
        }

        [Fact]
        public async Task OfflineSession_folds_cumulative_counters_across_replay_load()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(w, n), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            session.Measurement.WorkloadId = ProductTdmWorkloads.SoakCompound6v6;
            session.Measurement.ActiveTicks = 100;
            session.Measurement.Respawns = 2;
            session.Measurement.FoldMatchIntoCumulative();
            Assert.Equal(1, session.Measurement.MatchesCompleted);
            Assert.Equal(100, session.Measurement.CumulativeActiveTicks);

            Assert.True(session.Stop().Succeeded);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.Equal(1, session.Measurement.MatchesCompleted);
            Assert.Equal(100, session.Measurement.CumulativeActiveTicks);
            Assert.Equal(0, session.Measurement.ActiveTicks);
            // WorkloadId survives ResetForNewMatch across replay loads.
            Assert.Equal(ProductTdmWorkloads.SoakCompound6v6, session.Measurement.WorkloadId);
        }

        [Theory]
        [InlineData("training-compound", 460072030UL)]
        [InlineData("depot-yard", 460072031UL)]
        public void Both_maps_timeout_finish_under_production_54000_tick_rule(string map, ulong seed)
        {
            var mapId = new MapId(map);
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(mapId, out var gameplay));
            Assert.True(catalog.TryGet(mapId, out var entry));
            Assert.Equal(seed, entry.RecipeSeed);

            var match = new TeamDeathmatchMatch();
            var request = new SessionRequest(mapId, WellKnownModes.TeamDeathmatch, 5, seed);
            var roster = new System.Collections.Generic.List<ParticipantState>(10);
            for (uint i = 1; i <= 10; i++)
            {
                byte team = i <= 5 ? (byte)1 : (byte)2;
                roster.Add(new ParticipantState(new ParticipantId(i), "P" + i, team, i == 1, new EntityId(i), new LifeId(1), true, 0, 0, 0));
            }
            match.ConfigureIdentities("test", mapId.Value, "test");
            match.Begin(request, roster, gameplay);
            match.MarkPlaying(1, 0);
            for (uint i = 0; i < 53999; i++) match.EvaluateEndOfTick(i, true);
            Assert.Null(match.TryAcquireResults());
            match.EvaluateEndOfTick(53999, true);
            var results = match.TryAcquireResults();
            Assert.NotNull(results);
            Assert.Equal(FinishReason.TimeLimit, results!.Reason);
            Assert.Equal(54000u, results.ActiveDurationTicks);
        }

        [Theory]
        [InlineData("training-compound", 460072030UL)]
        [InlineData("depot-yard", 460072031UL)]
        public void Both_maps_accept_score_limit_configuration_without_rule_changes(string map, ulong seed)
        {
            // Full 150-kill credit path is covered by GameContractTests score cases; here verify both
            // map identities still Begin under production score/time limits (no soak rule extension).
            var mapId = new MapId(map);
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(mapId, out var gameplay));
            Assert.True(catalog.TryGet(mapId, out var entry));
            Assert.Equal(seed, entry.RecipeSeed);
            var match = new TeamDeathmatchMatch();
            var request = new SessionRequest(mapId, WellKnownModes.TeamDeathmatch, 5, seed);
            Assert.Equal(150, request.ScoreLimit);
            Assert.Equal(54000, request.TimeLimitActiveTicks);
            var roster = new System.Collections.Generic.List<ParticipantState>(10);
            for (uint i = 1; i <= 10; i++)
            {
                byte team = i <= 5 ? (byte)1 : (byte)2;
                roster.Add(new ParticipantState(new ParticipantId(i), "P" + i, team, i == 1, new EntityId(i), new LifeId(1), true, 0, 0, 0));
            }
            match.ConfigureIdentities("test", mapId.Value, "test");
            match.Begin(request, roster, gameplay);
            match.MarkPlaying(1, 0);
            Assert.Null(match.TryAcquireResults());
        }

        private static ParticipantState FindById(MatchViewSnapshot snap, ParticipantId id)
        {
            foreach (var p in snap.Participants)
                if (p.ParticipantId.Equals(id)) return p;
            throw new System.InvalidOperationException("missing");
        }

        private static ParticipantState FindTeam(MatchViewSnapshot snap, byte team)
        {
            foreach (var p in snap.Participants)
                if (p.Team == team && !p.IsHuman) return p;
            throw new System.InvalidOperationException("team participant");
        }

        private static void ArrangeDuel(HarnessActorLifecycle lifecycle, MatchViewSnapshot snapshot, EntityId shooter, EntityId victim)
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
            private readonly System.Collections.Generic.Dictionary<ulong, ActorLifecycleState> states =
                new System.Collections.Generic.Dictionary<ulong, ActorLifecycleState>();
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
            public bool TryGetActorState(EntityId entity, out ActorLifecycleState state) => states.TryGetValue(entity.Value, out state!);
        }

        [Fact]
        public async Task OfflineSession_bots_roam_packaged_patrol_destinations()
        {
            // T070 finding: the session never handed map destinations to the planner, so bots orbited spawn.
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var gameplay));
            Assert.True(gameplay.PatrolDestinations.Count > 0);
            var nav = new RecordingNav();
            using var session = new OfflineGameSession(catalog, (_, __) => new FakeMapRuntime(new NoWorld(), nav), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            for (int i = 0; i < 10; i++) Assert.True(session.Step(null).Succeeded);

            Assert.NotEmpty(nav.Destinations);
            foreach (var destination in nav.Destinations)
            {
                bool packaged = false;
                for (int i = 0; i < gameplay.PatrolDestinations.Count; i++)
                    packaged |= gameplay.PatrolDestinations[i].Equals(destination);
                Assert.True(packaged, "bot path goal is not a packaged patrol destination: " + destination);
            }
        }

        private sealed class RecordingNav : INavigation
        {
            public readonly System.Collections.Generic.List<Float3> Destinations = new System.Collections.Generic.List<Float3>();
            private ulong next;
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
            {
                Destinations.Add(destination);
                return new PathTicket(new NavTicketId(++next), actor, navRevision);
            }
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned) => new PathResult(PathStatus.Pending, 0, 0, ErrorCode.None);
            public void Cancel(PathTicket ticket) { }
        }

        private sealed class NoNav : INavigation
        {
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) =>
                new PathTicket(new NavTicketId(1), actor, navRevision);
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned) =>
                new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            public void Cancel(PathTicket ticket) { }
        }

        private sealed class NoWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
        }

        private sealed class RecordingWorld : IWorldQueries
        {
            public int Rays, Sweeps, Overlaps;
            public int Total => Rays + Sweeps + Overlaps;
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
            {
                Sweeps++;
                return new SweepHit(QueryStatus.NoHit);
            }
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
            {
                Rays++;
                return new RayHit(QueryStatus.NoHit);
            }
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned)
            {
                Overlaps++;
                return new OverlapResult(QueryStatus.NoHit, 0, 0);
            }
        }
    }
}
