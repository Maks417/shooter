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
        private SessionRequest? request;
        private GameMatchPhase phase = GameMatchPhase.Stopped;
        private SessionPauseState pause = SessionPauseState.Running;
        private ulong sessionId;
        private ulong epoch;
        private uint tick;
        private EntityId humanEntity;
        private readonly CommandBuffer botCommands = new CommandBuffer();
        private readonly List<InputCommand> merged = new List<InputCommand>(64);
        private bool disposed;

        public OfflineGameSession(IMapCatalog catalog, Func<IWorldQueries, INavigation, IMapRuntime> mapFactory, bool allowFakeMapRuntime = false)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.mapFactory = mapFactory ?? throw new ArgumentNullException(nameof(mapFactory));
            this.allowFakeMapRuntime = allowFakeMapRuntime;
        }

        public ulong AuthoritySessionId => sessionId;
        public GameMatchPhase Phase => phase;
        public SessionPauseState PauseState => pause;
        public uint CompletedTick => tick == 0 && phase != GameMatchPhase.Playing ? 0 : tick;
        public EntityId HumanEntityId => humanEntity;
        public IActorLifecycle? Lifecycle => lifecycle;
        public IMapRuntime? MapRuntime => mapRuntime;

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

            epoch++;
            sessionId = unchecked(epoch + 1000UL);
            this.request = request;
            phase = GameMatchPhase.Loading;
            pause = SessionPauseState.Running;
            tick = 0;
            humanEntity = default;

            var emptyWorld = new EmptyWorldQueries();
            var emptyNav = new EmptyNavigation();
            mapRuntime = mapFactory(emptyWorld, emptyNav);
            if (!allowFakeMapRuntime && mapRuntime is FakeMapRuntime)
                return new GameOperationResult(GameErrorCode.InternalError, "fake-map-rejected", "Shipping session rejects fake map runtime.");

            var loadEpoch = epoch;
            var load = await mapRuntime.LoadAsync(gameplay, cancel).ConfigureAwait(true);
            if (loadEpoch != epoch) return new GameOperationResult(GameErrorCode.StaleSession, "stale-load", "Stale load ignored.");
            if (!load.Succeeded) { phase = GameMatchPhase.Stopped; return load; }

            harness = new CombatHarnessSimulation(mapRuntime.WorldQueries, sessionId, request.Seed);
            lifecycle = new HarnessActorLifecycle(harness);
            bots = new TeamBotPlanner(mapRuntime.WorldQueries, mapRuntime.Navigation, 0, gameplay.NavRevision);

            var roster = BuildRoster(request, gameplay, lifecycle, bots);
            match.ConfigureIdentities("catalog.v1", gameplay.MapId.Value + ".gameplay", "local-dev");
            match.Begin(request, roster, gameplay);
            phase = GameMatchPhase.Ready;
            return GameOperationResult.Ok;
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
        public GameOperationResult Step(InputCommand? humanCommand)
        {
            if (phase != GameMatchPhase.Playing || pause != SessionPauseState.Running)
                return new GameOperationResult(GameErrorCode.Busy, "not-playing", "Session is not advancing.");
            if (harness == null || lifecycle == null || bots == null || mapRuntime == null || request == null)
                return new GameOperationResult(GameErrorCode.InternalError, "missing-services", "Session services missing.");

            // Install due respawns before movement (Game-v1 tick order).
            var pre = match.AcquireSnapshot(sessionId, tick);
            match.ScheduleDueRespawns(pre.ActiveTicksElapsed, lifecycle, mapRuntime);

            var snap = match.AcquireSnapshot(sessionId, tick);
            int botCount = bots.Plan(tick, snap, lifecycle, botCommands);

            merged.Clear();
            if (humanCommand.HasValue && humanCommand.Value.IsValid) merged.Add(humanCommand.Value);
            for (int i = 0; i < botCount; i++) merged.Add(botCommands[i]);

            var result = harness.Step(tick, merged.ToArray());
            using (var lease = harness.AcquireSnapshot())
                lifecycle.SyncFromSnapshot(lease.Snapshot);
            using (var events = harness.AcquireEvents())
            {
                for (int i = 0; i < events.Events.Count; i++)
                {
                    var ev = events.Events[i];
                    if (ev.Kind != CombatEventKind.Death) continue;
                    if (!lifecycle.TryGetLife(ev.Target, out _, out var life)) continue;
                    EntityId? killer = ev.Instigator.IsValid ? ev.Instigator : (EntityId?)null;
                    match.ProcessDeath(new DeathResolution(sessionId, ev.Id, ev.Target, life, killer, tick));
                    bots.ResetLife(ev.Target);
                }
            }

            match.EvaluateEndOfTick(tick, advanceActiveClock: true);
            if (match.TryAcquireResults() != null) phase = GameMatchPhase.Finished;
            tick = unchecked(tick + 1);
            _ = result;
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

        public MatchResults? TryAcquireResults() => match.TryAcquireResults();

        public GameOperationResult Stop()
        {
            bots?.CancelAll();
            mapRuntime?.Unload();
            mapRuntime = null;
            harness = null;
            lifecycle = null;
            bots = null;
            request = null;
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
                    planner.RegisterBot(entity, slot.Position, slot.Yaw);
                    botsRegistered++;
                }
                roster.Add(new ParticipantState(new ParticipantId(participant), name, team, human, entity, life, true, 0, 0, 0));
                participant++;
            }
            return roster;
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
