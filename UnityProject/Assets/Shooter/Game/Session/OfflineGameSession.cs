#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Simulation;
using Shooter.Simulation.Session;
using Shooter.Simulation.TeamDeathmatch;
using Shooter.World.Runtime;

namespace Shooter.Game.Session
{
    /// <summary>
    /// M8 product session composition root. Diagnostic floor/box path remains FoundationEntry.
    /// </summary>
    public sealed class OfflineGameSession : IGameSession
    {
        private readonly IMapCatalog catalog;
        private readonly Func<IWorldQueries, INavigation, IMapRuntime> mapFactory;
        private readonly bool allowFakeMapRuntime;
        private IMapRuntime? mapRuntime;
        private CombatHarnessSimulation? harness;
        private HarnessActorLifecycle? lifecycle;
        private readonly TeamDeathmatchMatch match = new TeamDeathmatchMatch();
        private SessionRequest? request;
        private GameMatchPhase phase = GameMatchPhase.Stopped;
        private SessionPauseState pause = SessionPauseState.Running;
        private ulong sessionId;
        private ulong epoch;
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

            // Product world/nav adapters are supplied by the caller; fakes only when explicitly allowed for pure tests.
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
            var roster = BuildRoster(request, gameplay, lifecycle);
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
            return match.AcquireSnapshot(sessionId, 0);
        }

        public MatchResults? TryAcquireResults() => match.TryAcquireResults();

        public GameOperationResult Stop()
        {
            mapRuntime?.Unload();
            mapRuntime = null;
            harness = null;
            lifecycle = null;
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

        private static List<ParticipantState> BuildRoster(SessionRequest request, MapGameplayDefinition map, IActorLifecycle lifecycle)
        {
            var roster = new List<ParticipantState>(request.ParticipantCount);
            uint participant = 1;
            int team1 = 0, team2 = 0;
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
                    break;
                }
                var life = new LifeId(1);
                var entity = lifecycle.SpawnLife(new ParticipantId(participant), life, slot.Position, slot.Yaw, team);
                string name = human ? "Player" : "Bot" + participant;
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
