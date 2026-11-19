#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.AI
{
    public readonly struct CoverSite
    {
        public ulong Id { get; }
        public Float3 Anchor { get; }
        public Float3 Peek { get; }
        public CoverSite(ulong id, Float3 anchor, Float3 peek)
        {
            if (id == 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id; Anchor = anchor; Peek = peek;
        }
    }

    /// <summary>
    /// ADR-112: an authored map cover object (axis-aligned box). The hiding spot is not fixed: it is chosen against
    /// the live threat, on the face of the box away from it, so the same crate covers either team.
    /// </summary>
    public readonly struct MapCover
    {
        public ulong Id { get; }
        public Float3 Center { get; }
        public Float3 Size { get; }
        public bool Hard { get; }
        public MapCover(ulong id, Float3 center, Float3 size, bool hard)
        {
            if (id == 0 || (id & CoverFinder.DynamicCoverIdFlag) != 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id; Center = center; Size = size; Hard = hard;
        }
    }

    /// <summary>
    /// Team AI: ≤16 AI rays (vision + T082 cover probes + T090 peeks/hearing) and ≤2 new paths per tick;
    /// cover/reload/stuck through normal commands. Capacity 29 for ADR-101 15v15. T082/T085/T090 add aim, fire,
    /// strafing with ledge probes, sticky targeting, wall-aware hearing, spread, cover peeks and callouts.
    /// </summary>
    public sealed partial class CombatAiDirector
    {
        public const int MaxBots = 29;
        public const int MaxNewPathsPerTick = 2;
        public const float StuckDistance = 0.2f;
        public const float CornerArrivalDistance = 0.35f;
        public const uint StuckTicksThreshold = 120; // 2 s at 60 Hz
        public const uint ReplanCooldownTicks = 30;
        public const uint PathTimeoutTicks = 300;
        /// <summary>An unconfirmed heard stimulus is dropped after this long.</summary>
        public const uint StimulusExpiryTicks = 600;

        // Eye / aim heights matching MotorConfig eyes and GameplayPoseEvaluator hit volumes.
        public const float StandingEyeHeight = 1.65f;
        public const float CrouchedEyeHeight = 1.05f;
        /// <summary>Chest inside the ADR-112 standing torso whether the target is still (0.76 .. 1.45 m) or moving
        /// (0.60 .. 1.29 m).</summary>
        public const float StandingAimHeight = 1.15f;
        /// <summary>Chest inside the ADR-112 crouched/sliding torso capsule (0.48 .. 1.17 m).</summary>
        public const float CrouchedAimHeight = 0.80f;

        // Movement abilities (all deterministic from entity id / tick / authority feedback).
        /// <summary>Engagements at or beyond this range are fought crouched and stationary with taps.</summary>
        public const float LongRangeCrouchDistance = AiTuning.LongRange;
        /// <summary>Stuck ticks (of StuckTicksThreshold) before the first jump attempt: 0.75 s.</summary>
        public const uint JumpStuckTicks = 45;
        public const uint JumpCooldownTicks = 90;
        /// <summary>Continuous sprint ticks before a slide may be requested (motor needs previous SprintActive and speed).
        /// ADR-100 lowered it from 30 so slides are actually seen.</summary>
        public const uint SlideMinSprintTicks = 18;
        public const float SlideMinStraightMeters = 4f;
        public const float SlideRequestSpeed = MotorConfig.SlideMinEntrySpeed + 0.3f;
        public const uint SlideBotCooldownBaseTicks = 300;
        public const uint SlideBotCooldownSpreadTicks = 60;
        /// <summary>Minimum forward component of the move axes for sprint.</summary>
        public const float SprintMinForward = 0.5f;

        private readonly AiBlackboard[] boards = new AiBlackboard[MaxBots];
        private readonly IWorldQueries world;
        private readonly uint visionMask;
        private readonly PerceptionSystem perception;
        private readonly CoverReservationBoard covers = new CoverReservationBoard();
        private readonly INavigation navigation;
        private readonly List<CoverSite> coverSites = new List<CoverSite>();
        private readonly List<Float3> patrolDestinations = new List<Float3>();
        private readonly List<Float3> combatDestinations = new List<Float3>();
        private readonly PathBuffer pathBuffer = new PathBuffer(ContractVersion.MaxPathCorners);
        private readonly AiCounters counters = new AiCounters();
        private int[] visitOrder = new int[32];
        // T085 footstep hearing: last known point/speed per enemy id (fixed capacity, no per-tick allocation).
        private const int MaxTracked = 32;
        private readonly ulong[] trackId = new ulong[MaxTracked];
        private readonly Float3[] trackPoint = new Float3[MaxTracked];
        private readonly uint[] trackTick = new uint[MaxTracked];
        private readonly float[] trackSpeed = new float[MaxTracked];
        private float[] enemySpeed = new float[32];
        // Non-bot actors (humans) seen in the enemy list, with their team, so their gunfire can be attributed.
        private readonly ulong[] humanTeamIds = new ulong[MaxTracked];
        private readonly byte[] humanTeams = new byte[MaxTracked];
        private int humanTeamCount;
        private int botCount;
        private uint tick;
        private int pathsThisTick;
        private uint navRevision = 1;

        public AiCounters Counters => counters;
        /// <summary>Vision + cover/peek + budgeted hearing rays consumed this plan tick (≤16). Ground/strafe probes are separate.</summary>
        public int AiRaysUsedThisTick => perception.RaysUsedThisTick;

        /// <summary>True when the command built for the bot this tick requests a slide.</summary>
        public bool WantsSlide(EntityId id)
        {
            for (int i = 0; i < botCount; i++)
                if (boards[i].Self == id) return boards[i].SlideIntent;
            return false;
        }

        public int BotCount => botCount;
        public CoverReservationBoard CoverBoard => covers;
        public uint NavRevision { get => navRevision; set => navRevision = value == 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value; }

        public CombatAiDirector(IWorldQueries world, INavigation navigation, uint visionMask, uint navRevision = 1)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.visionMask = visionMask;
            perception = new PerceptionSystem(world, visionMask);
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.navRevision = navRevision == 0 ? throw new ArgumentOutOfRangeException(nameof(navRevision)) : navRevision;
            for (int i = 0; i < MaxBots; i++) boards[i] = new AiBlackboard();
        }

        public void SetCoverSites(IReadOnlyList<CoverSite> sites)
        {
            coverSites.Clear();
            if (sites == null) return;
            for (int i = 0; i < sites.Count; i++) coverSites.Add(sites[i]);
        }

        /// <summary>ADR-112: authored map cover. Replaces the previous set; null or empty clears it.</summary>
        public void SetMapCovers(IReadOnlyList<MapCover>? covers)
        {
            mapCovers.Clear();
            if (covers == null) return;
            for (int i = 0; i < covers.Count; i++) mapCovers.Add(covers[i]);
        }

        public int MapCoverCount => mapCovers.Count;

        public void SetNavigationDestinations(
            IReadOnlyList<Float3>? patrol,
            IReadOnlyList<Float3>? combat)
        {
            patrolDestinations.Clear();
            combatDestinations.Clear();
            if (patrol != null)
                for (int i = 0; i < patrol.Count; i++) patrolDestinations.Add(patrol[i]);
            if (combat != null)
                for (int i = 0; i < combat.Count; i++) combatDestinations.Add(combat[i]);
        }

        public AiBlackboard RegisterBot(EntityId id, Float3 feet, float yaw, byte team = 0)
        {
            if (botCount >= MaxBots) throw new InvalidOperationException("Team AI capacity exhausted");
            var board = boards[botCount++];
            ResetBoardForLife(board, id, feet, yaw, team);
            return board;
        }

        public AiBlackboard GetBoard(int index)
        {
            if (index < 0 || index >= botCount) throw new ArgumentOutOfRangeException(nameof(index));
            return boards[index];
        }

        /// <summary>
        /// Rebind a dead/retired bot slot onto a fresh life. Cancels path/cover, clears
        /// Dead/reaction/target knowledge and starts a clean Patrol blackboard.
        /// </summary>
        public bool TryReplaceLife(EntityId previous, EntityId next, Float3 feet, float yaw, byte team)
        {
            if (!next.IsValid) throw new ArgumentOutOfRangeException(nameof(next));
            for (int i = 0; i < botCount; i++)
            {
                if (boards[i].Self != previous) continue;
                ReleaseBotResources(boards[i]);
                ResetBoardForLife(boards[i], next, feet, yaw, team);
                return true;
            }
            return false;
        }

        private AiBlackboard? FindBoard(EntityId id)
        {
            if (!id.IsValid) return null;
            for (int i = 0; i < botCount; i++)
                if (boards[i].Self == id && boards[i].Alive) return boards[i];
            return null;
        }

        private void ResetBoardForLife(AiBlackboard board, EntityId id, Float3 feet, float yaw, byte team)
        {
            board.Self = id;
            board.Team = team;
            board.Feet = feet;
            board.Yaw = yaw;
            board.Pitch = 0;
            board.Alive = true;
            board.State = AiState.Patrol;
            board.Health = 100;
            // The lifecycle sync overwrites ammo and the issued guns before the next plan (ADR-102).
            var primary = ActiveGun(board);
            board.MagAmmo = primary.MagazineSize;
            board.ReserveAmmo = primary.ReserveCapacity;
            board.DesiredSlot = 0;
            board.GrenadeRemaining = 1;
            board.RifleEmpty = false;
            board.PistolEmpty = false;
            board.CookingGrenade = false;
            board.GrenadeCookStartTick = 0;
            board.VisibleTargetId = null;
            board.HasVisibleTarget = false;
            board.VisibleTarget = null;
            board.LastSeenEnemy = null;
            board.LastSeenTick = 0;
            board.Confidence = 0;
            board.ReactionReadyTick = 0;
            board.LastHeard = null;
            board.LastHeardTick = 0;
            board.Heard = HeardClass.None;
            board.PathTicket = null;
            board.PathGoal = null;
            board.PathCornerCount = 0;
            board.PathCornerIndex = 0;
            board.StuckAnchor = feet;
            board.StuckTicks = 0;
            board.StuckFailures = 0;
            board.NeedsRelocation = false;
            board.CoverReservation = null;
            board.LastReplanTick = 0;
            board.SenseAgeTicks = 0;
            board.LastSenseOpportunityTick = 0;
            board.DestinationCursor = 0;
            board.RetryAfterTick = 0;
            board.PatrolGoal = null;
            board.SpawnPoint = feet;
            board.FlankGoal = null;
            board.CoverLow = false;
            board.NextRepositionTick = 0;
            board.PatrolGoalCursor = -1;
            board.PatrolIndex = -1;
            board.PreviousPatrolIndex = -1;
            ResetMovement(board);
            ResetCombat(board);
            board.StateEnteredTick = tick;
        }

        private static void ResetMovement(AiBlackboard board)
        {
            board.Crouched = false;
            board.HasPrevFeet = false;
            board.SmoothMoveX = 0; board.SmoothMoveZ = 0;
            board.PrevFeet = board.Feet;
            board.SpeedMps = 0;
            board.SprintTicks = 0;
            board.JumpPending = false;
            board.NextJumpTick = 0;
            board.NextSlideTick = 0;
            board.SlideIntent = false;
            board.NextHopTick = 0;
            board.StrafeFlippedThisTick = false;
        }

        private static void ResetCombat(AiBlackboard board)
        {
            board.TargetId = null;
            board.AcquiredTick = 0;
            board.TargetDistance = 0;
            board.TargetHealth = 100;
            board.TargetVelocity = default;
            board.TargetSpeed = 0;
            board.YawRate = 0;
            board.PitchRate = 0;
            board.AimOffsetRadians = 0;
            board.BurstStartTick = 0;
            board.BurstEndTick = 0;
            board.BurstPauseUntilTick = 0;
            board.BurstIndex = 0;
            board.StrafeDir = (board.Self.Value & 1) == 0 ? 1 : -1;
            board.StrafeFlipTick = 0;
            board.HasStrafeCenter = false;
            board.StrafeCenter = board.Feet;
            board.StrafeCheckFeet = board.Feet;
            board.StrafeCheckTick = 0;
            board.FlinchUntilTick = 0;
            board.LastDamagedTick = 0;
            board.WasDamaged = false;
            board.LastAttacker = null;
            board.RetreatAllowedTick = 0;
            board.ReloadStartMag = 0;
            board.CoverProbeActive = false;
            board.CoverProbeFailed = false;
            board.CoverProbeIndex = 0;
            board.CoverPoint = null;
            board.CoverProbeHasFallback = false;
            board.CoverProbeFallbackPoint = default;
            board.CoverProbeFallbackId = 0;
            board.LastHearOcclusionTick = 0;
            board.HearOcclusionClear = true;
            board.InCover = false;
            board.InCoverSinceTick = 0;
            board.UnseenAttacker = null;
            board.UnseenHits = 0;
            board.UnseenHitTick = 0;
            board.TurnToThreatUntilTick = 0;
        }

        /// <summary>Plan next-tick commands for all living bots. Caller feeds them through normal simulation command path.</summary>
        public int Plan(uint simulationTick, SenseTarget? playerTarget, InputCommand[] destination, int destinationOffset)
        {
            SenseTarget[]? single = null;
            int count = 0;
            if (playerTarget.HasValue)
            {
                single = new[] { playerTarget.Value };
                count = 1;
            }
            return PlanEnemies(simulationTick, single, count, destination, destinationOffset);
        }

        /// <summary>Multi-enemy sensing with global ray budget and round-robin candidate order.</summary>
        public int PlanEnemies(uint simulationTick, SenseTarget[]? enemies, int enemyCount, InputCommand[] destination, int destinationOffset)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (enemyCount < 0 || (enemies == null && enemyCount != 0) || (enemies != null && enemyCount > enemies.Length))
                throw new ArgumentOutOfRangeException(nameof(enemyCount));
            tick = simulationTick;
            pathsThisTick = 0;
            counters.VisionRaysThisTick = 0;
            counters.PathsRequestedThisTick = 0;
            counters.SenseOpportunitiesThisTick = 0;
            counters.AlliedCandidatesSkipped = 0;
            perception.BeginTick();
            PollPaths();
            if (visitOrder.Length < enemyCount + 1) visitOrder = new int[enemyCount + 1];
            if (enemySpeed.Length < enemyCount) enemySpeed = new float[enemyCount];
            TrackEnemies(enemies, enemyCount);

            int written = 0;
            int firstBot = botCount == 0 ? 0 : (int)(simulationTick % (uint)botCount);
            for (int turn = 0; turn < botCount; turn++)
            {
                int i = (firstBot + turn) % botCount;
                var board = boards[i];
                if (!board.Alive || board.State == AiState.Dead) continue;
                AdvancePathCorners(board);
                UpdateSpeed(board);
                UpdateStuck(board);
                EntityId? heldTarget = board.HasVisibleTarget ? board.VisibleTargetId : null;
                board.HasVisibleTarget = false;
                board.VisibleTargetId = null;
                board.VisibleTarget = null;
                board.LastSenseOpportunityTick = tick;
                counters.SenseOpportunitiesThisTick++;
                if (enemies != null && enemyCount > 0)
                {
                    senseHeldTarget = heldTarget;
                    senseHeldLost = false;
                    Sense(board, enemies, enemyCount);
                    if (!board.HasVisibleTarget) HoldVisibleTarget(board, heldTarget, enemies, enemyCount);
                    if (!board.HasVisibleTarget) HearFootsteps(board, enemies, enemyCount);
                }
                ExpireStimuli(board);
                ApplyUtility(board);
                Decide(board);
                if (destinationOffset + written >= destination.Length) break;
                destination[destinationOffset + written] = BuildCommand(board, simulationTick, (uint)(written + 1));
                written++;
            }
            // Cover probes only get what vision left of the shared per-tick budget.
            CoverFinder.Run(boards, botCount, tick, perception, covers, counters);
            counters.MaxPathQueue = Math.Max(counters.MaxPathQueue, pathsThisTick);
            counters.VisionRaysThisTick = perception.RaysUsedThisTick;
            return written;
        }

        // ---------------------------------------------------------------- perception / targeting

        private readonly List<MapCover> mapCovers = new List<MapCover>();
        private EntityId? senseHeldTarget;
        private bool senseHeldLost;

        /// <summary>Goal moved further than this from the active path's goal: an urgent goal replaces the route.</summary>
        public const float RetargetDistance = 2f;

        private static float EyeHeight(AiBlackboard board) => board.Crouched ? CrouchedEyeHeight : StandingEyeHeight;

        private static float Distance(Float3 a, Float3 b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private static float Distance2D(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        private void ReleaseBotResources(AiBlackboard board)
        {
            if (board.PathTicket.HasValue)
            {
                navigation.Cancel(board.PathTicket.Value);
                board.PathTicket = null;
            }
            board.PathCornerCount = 0;
            board.PathCornerIndex = 0;
            board.PathGoal = null;
            board.CoverPoint = null;
            board.InCover = false;
            board.CoverProbeActive = false;
            if (board.CoverReservation.HasValue)
            {
                if (covers.Release(board.CoverReservation.Value, board.Self)) counters.CoverReleases++;
                else counters.OrphanReleases++;
                board.CoverReservation = null;
            }
            else
            {
                int n = covers.ReleaseAll(board.Self);
                counters.CoverReleases += n;
                if (n > 0) counters.OrphanReleases += n;
            }
        }

        public void CancelAll()
        {
            for (int i = 0; i < botCount; i++)
                ReleaseBotResources(boards[i]);
        }

        // One clearance answer per bot, tick and corner: RouteDirection runs for the look and again for the move.
        private ulong bendCacheSelf;
        private uint bendCacheTick;
        private int bendCacheCorner = -1;
        private bool bendCacheClear;

    }
}
