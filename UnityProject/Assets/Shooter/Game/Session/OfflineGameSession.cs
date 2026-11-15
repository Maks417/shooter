#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Simulation;
using Shooter.Simulation.AI;
using Shooter.Simulation.Session;
using Shooter.Simulation.TeamDeathmatch;
using Shooter.World.Runtime;

namespace Shooter.Game.Session
{
    /// <summary>
    /// M8 product session composition root. Diagnostic floor/box path remains FoundationEntry (-shooterSmokeSeconds / -shooterDiagnostic).
    /// </summary>
    public sealed class OfflineGameSession : IGameSession
    {
        private readonly IMapCatalog catalog;
        private readonly Func<IWorldQueries, INavigation, IMapRuntime> mapFactory;
        private readonly bool allowFakeMapRuntime;
        private IMapRuntime? mapRuntime;
        private CombatHarnessSimulation? harness;
        private HarnessActorLifecycle? lifecycle;
        private TeamBotPlanner? bots;
        private readonly TeamDeathmatchMatch match = new TeamDeathmatchMatch();
        private MapGameplayDefinition? gameplayDefinition;
        private SessionRequest? request;
        private GameMatchPhase phase = GameMatchPhase.Stopped;
        private SessionPauseState pause = SessionPauseState.Running;
        private ulong sessionId;
        private ulong epoch;
        private uint tick;
        private EntityId humanEntity;
        private readonly CommandBuffer botCommands = new CommandBuffer();
        private readonly List<EntityId> slideActors = new List<EntityId>();
        private readonly List<InputCommand> merged = new List<InputCommand>(64);
        private readonly List<EntityId> deathsThisTick = new List<EntityId>(GameContractVersion.MaxParticipants);
        private readonly ProductMeasurementTelemetry measurement = new ProductMeasurementTelemetry();
        private QueryCountingMapRuntime? countingMap;
        private long querySamplesAtMatchStart;
        private GameOperationResult? terminalFault;
        private bool disposed;

        public OfflineGameSession(IMapCatalog catalog, Func<IWorldQueries, INavigation, IMapRuntime> mapFactory, bool allowFakeMapRuntime = false)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.mapFactory = mapFactory ?? throw new ArgumentNullException(nameof(mapFactory));
            this.allowFakeMapRuntime = allowFakeMapRuntime;
        }

        public ulong AuthoritySessionId => sessionId;
        public GameMatchPhase Phase => phase;
        /// <summary>
        /// Terminal recoverable fault (for example SpawnFailure after the match aborted because no navigable,
        /// unoccupied slot exists). Null while healthy; cleared by the next BeginLoadAsync. Read-only for the shell.
        /// </summary>
        public GameOperationResult? TerminalFault => terminalFault;
        public SessionPauseState PauseState => pause;
        public uint CompletedTick => tick == 0 && phase != GameMatchPhase.Playing ? 0 : tick;
        public EntityId HumanEntityId => humanEntity;
        public IActorLifecycle? Lifecycle => lifecycle;
        public IMapRuntime? MapRuntime => mapRuntime;
        public int CosmeticCorpseCount => lifecycle?.CosmeticCorpseCount ?? 0;
        /// <summary>Read-only measurement counters for product TDM capture (T063/T070). Mutated only by this session and host binding hooks.</summary>
        public ProductMeasurementTelemetry Measurement => measurement;
        /// <summary>Load fence epoch; increments on every BeginLoadAsync / Stop.</summary>
        public ulong SessionEpoch => epoch;
        /// <summary>Adapter sample total for tests/diagnostics ( -1 when no counting map).</summary>
        public long DebugWorldQueryAdapterSamples => countingMap == null ? -1 : countingMap.CountingQueries.TotalSamples;

        public bool TryGetMovementView(EntityId entity, out LocalMovementView view)
        {
            view = default;
            if (harness == null || !harness.TryGetMotorState(entity, out var state)) return false;
            float eye = state.Stance == Stance.Dead ? 0.4f
                : state.Stance == Stance.Crouching ? 1.05f : 1.65f;
            view = new LocalMovementView(state.Stance, eye, state.SlideActive);
            return true;
        }

        public bool TryGetCosmeticCorpse(int index, out ActorLifecycleState state)
        {
            if (lifecycle == null)
            {
                state = default;
                return false;
            }
            return lifecycle.TryGetCosmeticCorpse(index, out state);
        }

        public GameOperationResult Validate(SessionRequest request)
        {
            if (request == null) return new GameOperationResult(GameErrorCode.InvalidRequest, "null-request", "Invalid session request.");
            if (request.RuleVersion != GameContractVersion.RuleVersion)
                return new GameOperationResult(GameErrorCode.UnsupportedVersion, "rule-version", "Unsupported rule version.");
            return catalog.RejectUnknown(request.MapId, request.ModeId, request.TeamSize);
        }

        public async Task<GameOperationResult> BeginLoadAsync(SessionRequest request, CancellationToken cancel)
        {
            ThrowIfDisposed();
            var validation = Validate(request);
            if (!validation.Succeeded) return validation;
            if (!catalog.TryGetGameplay(request.MapId, out var gameplay))
                return new GameOperationResult(GameErrorCode.MissingContent, "missing-gameplay", "Map content missing.");
            if (!catalog.TryGet(request.MapId, out var entry))
                return new GameOperationResult(GameErrorCode.MissingContent, "missing-catalog", "Map catalog missing.");
            if (entry.RecipeSeed != request.Seed)
                return new GameOperationResult(GameErrorCode.InvalidRequest, "seed-mismatch", "Seed does not match packaged map.");

            ReleaseRuntime();
            terminalFault = null;
            epoch++;
            sessionId = unchecked(epoch + 1000UL);
            this.request = request;
            gameplayDefinition = gameplay;
            phase = GameMatchPhase.Loading;
            pause = SessionPauseState.Running;
            tick = 0;
            humanEntity = default;

            var emptyWorld = new EmptyWorldQueries();
            var emptyNav = new EmptyNavigation();
            var loadingRuntime = mapFactory(emptyWorld, emptyNav);
            if (!allowFakeMapRuntime && loadingRuntime is FakeMapRuntime)
            {
                loadingRuntime.Unload();
                phase = GameMatchPhase.Stopped;
                return new GameOperationResult(GameErrorCode.InternalError, "fake-map-rejected", "Shipping session rejects fake map runtime.");
            }

            var loadEpoch = epoch;
            var load = await loadingRuntime.LoadAsync(gameplay, cancel).ConfigureAwait(true);
            if (loadEpoch != epoch)
            {
                loadingRuntime.Unload();
                return new GameOperationResult(GameErrorCode.StaleSession, "stale-load", "Stale load ignored.");
            }
            if (!load.Succeeded) { phase = GameMatchPhase.Stopped; return load; }

            // One shared counter for map spawn queries, harness motor/hitscan, and AI perception.
            // Reuse an existing CountingWorldQueries if the map factory already wrapped (tests),
            // so FakeMapRuntime.WorldQueries and session adapters share one counter instance.
            var baseQueries = loadingRuntime.WorldQueries;
            var queries = baseQueries as CountingWorldQueries ?? new CountingWorldQueries(baseQueries);
            countingMap = new QueryCountingMapRuntime(loadingRuntime, queries);
            mapRuntime = countingMap;
            querySamplesAtMatchStart = queries.TotalSamples;
            harness = new CombatHarnessSimulation(queries, sessionId, request.Seed);
            harness.SetWorldKillBounds(gameplay.Bounds);
            lifecycle = new HarnessActorLifecycle(harness);
            bots = new TeamBotPlanner(queries, mapRuntime.Navigation, 0, gameplay.NavRevision);
            ApplyMapDestinations(bots, gameplay);

            var roster = BuildRoster(request, gameplay, lifecycle, bots);
            match.ConfigureIdentities("catalog.v1", gameplay.MapId.Value + ".gameplay", "local-dev");
            match.Begin(request, roster, gameplay);
            // Preserve WorkloadId / cumulatives across replay loads; host re-applies WorkloadId after this returns.
            measurement.ResetForNewMatch();
            measurement.SessionEpoch = epoch;
            measurement.MapId = request.MapId.Value;
            measurement.Seed = request.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            measurement.TeamSize = request.TeamSize;
            measurement.Actors = roster.Count;
            if (catalog.TryGet(request.MapId, out var catalogEntry) &&
                !catalogEntry.GameplayRootHash.Equals(default))
            {
                // Real binding only when the catalog supplies a non-default package root.
                measurement.PackageRootHash = catalogEntry.GameplayRootHash.ToString();
                measurement.ContentManifestHash = catalogEntry.GameplayRootHash.ToString();
                measurement.RecordContentBinding();
            }
            phase = GameMatchPhase.Ready;
            return GameOperationResult.Ok;
        }

        /// <summary>
        /// Host records a verified visual/content package bind. Does not invent bindings when packages are absent.
        /// </summary>
        public void RecordVerifiedContentBinding()
        {
            measurement.RecordContentBinding();
        }

        /// <summary>Fold finished-match counters into cumulative soak totals before the next load resets per-match fields.</summary>
        public void NotifyMatchFinishedForMeasurement()
        {
            if (phase != GameMatchPhase.Finished) return;
            measurement.FoldMatchIntoCumulative();
        }

        public GameOperationResult SignalReady()
        {
            if (phase != GameMatchPhase.Ready) return new GameOperationResult(GameErrorCode.Busy, "not-ready", "Session is not ready.");
            return GameOperationResult.Ok;
        }

        public GameOperationResult Start()
        {
            if (phase != GameMatchPhase.Ready) return new GameOperationResult(GameErrorCode.Busy, "not-ready", "Cannot start.");
            match.MarkPlaying(sessionId, 0);
            phase = GameMatchPhase.Playing;
            return GameOperationResult.Ok;
        }

        public GameOperationResult Pause()
        {
            if (phase != GameMatchPhase.Playing) return new GameOperationResult(GameErrorCode.Busy, "not-playing", "Cannot pause.");
            pause = SessionPauseState.Paused;
            return GameOperationResult.Ok;
        }

        public GameOperationResult Resume()
        {
            if (pause != SessionPauseState.Paused) return new GameOperationResult(GameErrorCode.Busy, "not-paused", "Cannot resume.");
            pause = SessionPauseState.Running;
            return GameOperationResult.Ok;
        }

        /// <summary>Advance one authority tick. Local human command optional; bots planned from match snapshot.</summary>
        public GameOperationResult Step(InputCommand? humanCommand, bool slideRequested = false)
        {
            if (terminalFault != null && phase == GameMatchPhase.Stopped)
                return GameOperationResult.Ok; // Faulted: no-op until the shell returns to the menu.
            if (phase != GameMatchPhase.Playing || pause != SessionPauseState.Running)
                return new GameOperationResult(GameErrorCode.Busy, "not-playing", "Session is not advancing.");
            if (harness == null || lifecycle == null || bots == null || mapRuntime == null || request == null)
                return new GameOperationResult(GameErrorCode.InternalError, "missing-services", "Session services missing.");

            // Install due respawns before movement (Game-v1 tick order).
            var pre = match.AcquireSnapshot(sessionId, tick);
            measurement.LeaseAcquires++;
            match.ScheduleDueRespawns(pre.ActiveTicksElapsed, lifecycle, mapRuntime);

            var snap = match.AcquireSnapshot(sessionId, tick);
            measurement.LeaseAcquires++;
            if (snap.Phase == GameMatchPhase.Stopped && snap.Reason == FinishReason.Aborted)
            {
                terminalFault = new GameOperationResult(GameErrorCode.SpawnFailure, "spawn-failure", "No safe spawn available. Returning to the menu.");
                phase = GameMatchPhase.Stopped;
                pause = SessionPauseState.Running;
                return GameOperationResult.Ok;
            }
            // Real respawn semantics: count life/entity replacements, not net alive growth
            // (same-tick death elsewhere must not cancel a successful respawn event).
            measurement.Respawns += CountRespawnEvents(pre, snap);
            if (RebindReplacements(pre, snap))
            {
                bots = RebuildBotPlanner(snap, lifecycle, mapRuntime);
            }
            int botCount = bots.Plan(tick, snap, lifecycle, botCommands);
            measurement.AiRays += bots.Counters.VisionRaysThisTick;
            measurement.PathRequests += bots.Counters.PathsRequestedThisTick;

            merged.Clear();
            var sampledHuman = humanCommand.GetValueOrDefault();
            bool validHuman = humanCommand.HasValue &&
                sampledHuman.IsValid &&
                sampledHuman.EntityId.Equals(humanEntity);
            if (validHuman)
            {
                merged.Add(sampledHuman);
            }
            for (int i = 0; i < botCount; i++) merged.Add(botCommands[i]);

            slideActors.Clear();
            if (validHuman && slideRequested) slideActors.Add(humanEntity);
            var botSlides = bots.SlideActors;
            for (int i = 0; i < botSlides.Count; i++) slideActors.Add(botSlides[i]);
            var result = harness.StepWithSlideActors(tick, merged.ToArray(), slideActors);
            measurement.Shots += result.Counters.Shots;
            measurement.DamageEvents += result.Counters.DamageEvents;
            // querySamples after Plan + harness.Step so motor/hitscan samples are included
            // (adapter total, not aiRays+pathRequests sum).
            if (countingMap != null)
            {
                long total = countingMap.CountingQueries.TotalSamples;
                measurement.QuerySamples = total - querySamplesAtMatchStart;
            }
            using (var lease = harness.AcquireSnapshot())
            {
                measurement.LeaseAcquires++;
                lifecycle.SyncFromSnapshot(lease.Snapshot);
            }
            // Ordering: the out-of-world kill volume already ran inside harness.Step (after movement and bullets), so a
            // fallen actor is Alive=false after the sync above and is skipped by the rescue below (never rescued).
            // Position rescue runs after the motor step and snapshot sync (so the sync cannot overwrite the new feet
            // with the pre-rescue snapshot) and before bot planning / spawn-safety checks of the next tick, which
            // therefore see the relocated feet. Actors killed this tick are Alive=false after the sync and skipped.
            RescueEmbeddedActors(snap, lifecycle, harness, mapRuntime);
            deathsThisTick.Clear();
            using (var events = harness.AcquireEvents())
            {
                measurement.LeaseAcquires++;
                for (int i = 0; i < events.Events.Count; i++)
                {
                    var ev = events.Events[i];
                    if (ev.Kind != CombatEventKind.Death) continue;
                    if (!lifecycle.TryGetLife(ev.Target, out _, out var life)) continue;
                    EntityId? killer = ev.Instigator.IsValid ? ev.Instigator : (EntityId?)null;
                    match.ProcessDeath(new DeathResolution(sessionId, ev.Id, ev.Target, life, killer, tick));
                    bots.ResetLife(ev.Target);
                    if (!ContainsEntity(deathsThisTick, ev.Target)) deathsThisTick.Add(ev.Target);
                }
            }
            // Preserve ordered scoring for every same-tick death before retiring any old life.
            uint activeTick = snap.ActiveTicksElapsed;
            for (int i = 0; i < deathsThisTick.Count; i++)
                lifecycle.RetireLife(deathsThisTick[i], activeTick);

            match.EvaluateEndOfTick(tick, advanceActiveClock: true);
            var endSnap = match.AcquireSnapshot(sessionId, tick);
            measurement.LeaseAcquires++;
            measurement.Team1Kills = endSnap.Team1Kills;
            measurement.Team2Kills = endSnap.Team2Kills;
            measurement.ActiveTicks = endSnap.ActiveTicksElapsed;
            measurement.Actors = endSnap.Participants.Count;
            lifecycle.AdvanceActiveTick(endSnap.ActiveTicksElapsed);
            if (match.TryAcquireResults() != null) phase = GameMatchPhase.Finished;
            tick = unchecked(tick + 1);
            return GameOperationResult.Ok;
        }

        public GameOperationResult RequestAbort()
        {
            match.Abort(sessionId);
            phase = GameMatchPhase.Stopped;
            pause = SessionPauseState.Running;
            return Stop();
        }

        public MatchViewSnapshot? TryAcquireSnapshot()
        {
            if (phase == GameMatchPhase.Stopped || phase == GameMatchPhase.Loading) return null;
            return match.AcquireSnapshot(sessionId, tick == 0 ? 0 : unchecked(tick - 1));
        }

        /// <summary>
        /// Read-only lease of the last completed combat event list for presentation (audio/FX).
        /// Null when no completed tick or harness is absent. Caller must dispose the lease.
        /// </summary>
        public EventLease? TryLeaseCombatEvents()
        {
            if (harness == null || tick == 0) return null;
            if (phase == GameMatchPhase.Stopped || phase == GameMatchPhase.Loading) return null;
            return harness.AcquireEvents();
        }

        public MatchResults? TryAcquireResults() => match.TryAcquireResults();

        public GameOperationResult Stop()
        {
            epoch++;
            ReleaseRuntime();
            request = null;
            gameplayDefinition = null;
            if (phase != GameMatchPhase.Finished) phase = GameMatchPhase.Stopped;
            return GameOperationResult.Ok;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineGameSession));
        }

        private List<ParticipantState> BuildRoster(SessionRequest request, MapGameplayDefinition map, IActorLifecycle lifecycle, TeamBotPlanner planner)
        {
            var roster = new List<ParticipantState>(request.ParticipantCount);
            uint participant = 1;
            int team1 = 0, team2 = 0;
            int botsRegistered = 0;
            // M8 composition workload is eight bots; M9 full 5v5/6v6 registers the rest.
            int botCap = Math.Min(CombatAiDirector.MaxBots, request.ParticipantCount - 1);
            for (int i = 0; i < request.ParticipantCount; i++)
            {
                byte team = team1 < request.TeamSize ? (byte)1 : (byte)2;
                if (team == 1) team1++; else team2++;
                bool human = team == 1 && team1 == 1;
                SpawnSlot slot = default;
                foreach (var candidate in map.SpawnSlots)
                {
                    if (candidate.Team != team) continue;
                    slot = candidate;
                    // Prefer unused slots by walking; first match is fine for initial spawn.
                    break;
                }
                // Distribute team spawns: pick nth matching slot when available.
                int teamIndex = team == 1 ? team1 : team2;
                int seen = 0;
                foreach (var candidate in map.SpawnSlots)
                {
                    if (candidate.Team != team) continue;
                    seen++;
                    if (seen == teamIndex) { slot = candidate; break; }
                }
                if (mapRuntime != null && !mapRuntime.TryValidateSpawn(slot, out _))
                {
                    foreach (var candidate in map.SpawnSlots)
                    {
                        if (candidate.Team != team) continue;
                        if (mapRuntime.TryValidateSpawn(candidate, out _)) { slot = candidate; break; }
                    }
                }

                var life = new LifeId(1);
                var entity = lifecycle.SpawnLife(new ParticipantId(participant), life, slot.Position, slot.Yaw, team);
                string name = human ? "Player" : "Bot" + participant;
                if (human) humanEntity = entity;
                else if (botsRegistered < botCap)
                {
                    planner.RegisterBot(entity, slot.Position, slot.Yaw, team);
                    botsRegistered++;
                }
                roster.Add(new ParticipantState(new ParticipantId(participant), name, team, human, entity, life, true, 0, 0, 0));
                participant++;
            }
            return roster;
        }

        private readonly Dictionary<uint, uint> rescueSlotLastUsed = new Dictionary<uint, uint>();
        private readonly List<EntityId> rescueLiving = new List<EntityId>(GameContractVersion.MaxParticipants);
        private readonly List<Float3> rescueOccupancy = new List<Float3>(1);

        /// <summary>
        /// Relocates living actors whose motor reports OverlapUnresolved to a valid same-team spawn chosen by
        /// SpawnSafetyPolicy (deterministic, participant order). No death, score, LifeId, EntityId, health or ammo
        /// change. If no safe slot exists the actor stays put and is retried next tick.
        /// </summary>
        private void RescueEmbeddedActors(
            MatchViewSnapshot snap, HarnessActorLifecycle actorLifecycle, CombatHarnessSimulation sim, IMapRuntime runtime)
        {
            if (gameplayDefinition == null) return;
            for (int i = 0; i < snap.Participants.Count; i++)
            {
                var p = snap.Participants[i];
                if (!p.Alive) continue;
                if (!sim.TryGetMotorState(p.EntityId, out var motorState) || !motorState.OverlapUnresolved) continue;
                if (!actorLifecycle.TryGetActorState(p.EntityId, out var state) || !state.Alive) continue;

                rescueLiving.Clear();
                for (int j = 0; j < snap.Participants.Count; j++)
                    if (j != i && snap.Participants[j].Alive) rescueLiving.Add(snap.Participants[j].EntityId);
                // Block slots next to the embedded position so the actor is not put back where it was.
                rescueOccupancy.Clear();
                rescueOccupancy.Add(motorState.Feet);
                if (!SpawnSafetyPolicy.TryPick(
                        p.Team, gameplayDefinition.SpawnSlots, runtime, actorLifecycle, rescueLiving,
                        rescueSlotLastUsed, rescueOccupancy, out var slot))
                    continue;
                if (!actorLifecycle.RelocateLife(p.EntityId, slot.Position, slot.Yaw)) continue;
                rescueSlotLastUsed[slot.SlotId] = snap.ActiveTicksElapsed;
                if (!p.IsHuman)
                {
                    // Drop stale path/target state and restart the bot command sequence with the harness epoch.
                    actorLifecycle.ResetCommandEpoch(p.EntityId);
                    bots?.TryReplaceLife(p.EntityId, p.EntityId, slot.Position, slot.Yaw, p.Team);
                }
            }
        }

        private bool RebindReplacements(MatchViewSnapshot before, MatchViewSnapshot after)
        {
            bool changed = false;
            for (int i = 0; i < after.Participants.Count; i++)
            {
                var current = after.Participants[i];
                var prior = before.Participants[i];
                if (current.EntityId.Equals(prior.EntityId)) continue;
                changed = true;
                if (current.IsHuman) humanEntity = current.EntityId;
            }
            return changed;
        }

        private TeamBotPlanner RebuildBotPlanner(
            MatchViewSnapshot snapshot,
            HarnessActorLifecycle actorLifecycle,
            IMapRuntime runtime)
        {
            CancelBotPlanner();
            var planner = new TeamBotPlanner(
                runtime.WorldQueries,
                runtime.Navigation,
                0,
                gameplayDefinition?.NavRevision ?? 1);
            if (gameplayDefinition != null) ApplyMapDestinations(planner, gameplayDefinition);
            for (int i = 0; i < snapshot.Participants.Count; i++)
            {
                var participant = snapshot.Participants[i];
                if (participant.IsHuman || !participant.Alive) continue;
                if (!actorLifecycle.TryGetActorState(participant.EntityId, out var state)) continue;
                if (!actorLifecycle.ResetCommandEpoch(participant.EntityId))
                    throw new InvalidOperationException("bot authority entity missing");
                planner.RegisterBot(participant.EntityId, state.Feet, state.AimYaw, state.Team);
            }
            return planner;
        }

        /// <summary>
        /// Bots roam the packaged patrol/combat destinations. Without them every bot orbits 4 m around
        /// its spawn and opposing teams never meet (T070 capture finding, 2026-11-14).
        /// </summary>
        private static void ApplyMapDestinations(TeamBotPlanner planner, MapGameplayDefinition gameplay) =>
            planner.SetNavigationDestinations(gameplay.PatrolDestinations, gameplay.CombatDestinations);

        /// <summary>Read-only AI diagnostics for capture summaries; null when no match is loaded.</summary>
        public AiCounters? BotCounters => bots?.Counters;

        private void ReleaseRuntime()
        {
            rescueSlotLastUsed.Clear();
            CancelBotPlanner();
            // Unload the inner runtime; counting decorator forwards Unload.
            mapRuntime?.Unload();
            mapRuntime = null;
            countingMap = null;
            harness = null;
            lifecycle = null;
            bots = null;
            deathsThisTick.Clear();
            querySamplesAtMatchStart = 0;
        }

        private static int CountRespawnEvents(MatchViewSnapshot before, MatchViewSnapshot after)
        {
            int respawns = 0;
            int count = Math.Min(before.Participants.Count, after.Participants.Count);
            for (int i = 0; i < count; i++)
            {
                var prior = before.Participants[i];
                var current = after.Participants[i];
                if (prior.ParticipantId.Value != current.ParticipantId.Value) continue;
                if (!prior.EntityId.Equals(current.EntityId) && current.Alive)
                    respawns++;
            }
            return respawns;
        }

        private void CancelBotPlanner()
        {
            if (bots == null) return;
            for (int i = 0; i < bots.Director.BotCount; i++)
                bots.ResetLife(bots.Director.GetBoard(i).Self);
            bots.CancelAll();
        }

        private static bool ContainsEntity(List<EntityId> entities, EntityId candidate)
        {
            for (int i = 0; i < entities.Count; i++)
                if (entities[i].Equals(candidate)) return true;
            return false;
        }

        private sealed class EmptyWorldQueries : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) =>
                new SweepHit(QueryStatus.NoHit);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) =>
                new RayHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) =>
                new OverlapResult(QueryStatus.NoHit, 0, 0);
        }

        private sealed class EmptyNavigation : INavigation
        {
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) =>
                new PathTicket(new NavTicketId(1), actor, navRevision);
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned) =>
                new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            public void Cancel(PathTicket ticket) { }
        }
    }
}
