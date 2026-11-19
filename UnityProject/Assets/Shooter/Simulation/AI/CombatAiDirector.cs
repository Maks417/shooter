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
    public sealed class CombatAiDirector
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

        /// <summary>Legacy damage stimulus with a known source point (no attacker identity).</summary>
        public void NotifyDamage(EntityId victim, Float3 source) => ObserveDamage(victim, default, source);

        /// <summary>
        /// Authoritative damage on <paramref name="victim"/> (tick t), consumed by the next plan (t+1).
        /// The bot flinches; if it was not already fighting the attacker it turns toward a noisy source
        /// estimate and investigates.
        /// </summary>
        public void ObserveDamage(EntityId victim, EntityId attacker, Float3? attackerPoint)
        {
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (board.Self != victim) continue;
                if (!board.Alive || board.State == AiState.Dead) return;
                board.WasDamaged = true;
                board.LastDamagedTick = tick;
                board.FlinchUntilTick = tick + AiTuning.FlinchTicks;
                if (attacker.IsValid) board.LastAttacker = attacker;
                counters.DamageReactions++;
                bool fightingAttacker = board.HasVisibleTarget && attacker.IsValid && board.TargetId == attacker;
                if (!attackerPoint.HasValue || fightingAttacker) return;
                if (board.HasVisibleTarget && attacker.IsValid)
                {
                    // Shot by someone else while fighting: a second hit (or getting low) makes the bot turn on them.
                    bool sameUnseen = board.UnseenAttacker.HasValue && board.UnseenAttacker.Value == attacker &&
                        unchecked(tick - board.UnseenHitTick) <= AiTuning.UnseenHitWindowTicks;
                    board.UnseenHits = sameUnseen ? board.UnseenHits + 1 : 1;
                    board.UnseenAttacker = attacker;
                    board.UnseenHitTick = tick;
                    if (board.UnseenHits >= AiTuning.UnseenHitsToTurn || board.Health < AiTuning.TurnImmediatelyBelowHealth)
                    {
                        board.TurnToThreatUntilTick = tick + AiTuning.ThreatTurnTicks;
                        counters.ThreatTurns++;
                    }
                }
                float n = AiTuning.DamageSourceNoiseMeters;
                var p = attackerPoint.Value;
                board.LastHeard = new Float3(
                    p.X + n * AiNoise.Signed(board.Self.Value, tick, AiNoise.SaltHearingX),
                    p.Y,
                    p.Z + n * AiNoise.Signed(board.Self.Value, tick, AiNoise.SaltHearingZ));
                board.LastHeardTick = tick;
                board.Heard = HeardClass.Damage;
                if (IsCalm(board.State)) Enter(board, AiState.Investigate);
                return;
            }
        }

        /// <summary>
        /// A shot fired at <paramref name="origin"/> (tick t). Calm bots within hearing radius investigate a
        /// distance-attenuated estimate of the position. T090: a direct occlusion sample (not the vision budget)
        /// drops gunfire that is fully blocked by geometry; presence-range always passes.
        /// </summary>
        public void ObserveGunfire(EntityId shooter, Float3 origin)
        {
            float radiusSq = AiTuning.GunfireHearingRadius * AiTuning.GunfireHearingRadius;
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (!board.Alive || board.State == AiState.Dead || board.Self == shooter) continue;
                if (board.HasVisibleTarget || !IsCalm(board.State)) continue;
                var source = origin;
                var shooterBoard = FindBoard(shooter);
                if (shooterBoard != null && board.Team != 0 && PerceptionSystem.IsAlliedTeam(board.Team, shooterBoard.Team))
                {
                    // A teammate shooting is not a threat at the teammate: look where it is fighting, or ignore it.
                    if (!shooterBoard.LastSeenEnemy.HasValue ||
                        unchecked(tick - shooterBoard.LastSeenTick) > AiTuning.CombatMemoryTicks)
                    {
                        counters.AllyGunfireIgnored++;
                        continue;
                    }
                    source = shooterBoard.LastSeenEnemy.Value;
                }
                else if (shooterBoard == null && IsAlliedHuman(board, shooter)) { counters.AllyGunfireIgnored++; continue; }
                float dx = source.X - board.Feet.X, dz = source.Z - board.Feet.Z;
                float dSq = dx * dx + dz * dz;
                if (dSq > radiusSq) continue;
                if (board.LastHeard.HasValue && unchecked(tick - board.LastHeardTick) < AiTuning.StimulusRefreshTicks)
                {
                    // Recent stimulus: only a clearly closer source replaces it.
                    float hx = board.LastHeard.Value.X - board.Feet.X, hz = board.LastHeard.Value.Z - board.Feet.Z;
                    if (dSq >= (hx * hx + hz * hz) * 0.5f) continue;
                }
                if (!SoundReaches(board, source, useBudget: false))
                {
                    counters.HearingOccluded++;
                    continue;
                }
                float noise = MathF.Sqrt(dSq) * AiTuning.GunfireNoisePerMeter;
                board.LastHeard = new Float3(
                    source.X + noise * AiNoise.Signed(board.Self.Value, tick, AiNoise.SaltHearingX),
                    source.Y,
                    source.Z + noise * AiNoise.Signed(board.Self.Value, tick, AiNoise.SaltHearingZ));
                board.LastHeardTick = tick;
                board.Heard = HeardClass.Gunfire;
                counters.StimuliHeard++;
                EnterInvestigateUnlessCrowded(board);
            }
        }

        public void NotifyDeath(EntityId id)
        {
            for (int i = 0; i < botCount; i++)
            {
                if (boards[i].Self != id) continue;
                ReleaseBotResources(boards[i]);
                boards[i].Alive = false;
                boards[i].HasVisibleTarget = false;
                boards[i].VisibleTargetId = null;
                boards[i].VisibleTarget = null;
                boards[i].TargetId = null;
                boards[i].ReactionReadyTick = 0;
                Enter(boards[i], AiState.Dead);
                return;
            }
        }

        private AiBlackboard? FindBoard(EntityId id)
        {
            if (!id.IsValid) return null;
            for (int i = 0; i < botCount; i++)
                if (boards[i].Self == id && boards[i].Alive) return boards[i];
            return null;
        }

        /// <summary>Non-bot shooter (the human) on the listener's team, as last seen in the planner's actor list.</summary>
        private bool IsAlliedHuman(AiBlackboard board, EntityId shooter)
        {
            if (board.Team == 0) return false;
            for (int i = 0; i < humanTeamCount; i++)
                if (humanTeamIds[i] == shooter.Value) return PerceptionSystem.IsAlliedTeam(board.Team, humanTeams[i]);
            return false;
        }

        /// <summary>
        /// Investigate a stimulus unless enough allies already are; the rest keep their route and watch the
        /// point (ThreatLook), so a team does not run to the same spot in a line.
        /// </summary>
        private void EnterInvestigateUnlessCrowded(AiBlackboard board)
        {
            if (board.State == AiState.Investigate || !board.LastHeard.HasValue || board.Team == 0)
            {
                Enter(board, AiState.Investigate);
                return;
            }
            int crowd = 0;
            var p = board.LastHeard.Value;
            for (int i = 0; i < botCount; i++)
            {
                var other = boards[i];
                if (other == board || !other.Alive || other.State != AiState.Investigate || !other.LastHeard.HasValue) continue;
                if (!PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                if (Within(other.LastHeard.Value, p, AiTuning.InvestigateCrowdMeters)) crowd++;
            }
            // ADR-112: allies already fighting at the spot count toward the crowd too.
            crowd += AlliesPressing(board, p);
            if (crowd >= AiTuning.InvestigateCrowdLimit)
            {
                counters.CrowdedStimuliHeld++;
                // Enough allies are on it: take a flanking point round the contact instead of joining the queue.
                var flank = ChooseFlankGoal(board, p);
                if (flank.HasValue && IsCalm(board.State) && board.State != AiState.Reposition)
                {
                    board.FlankGoal = flank;
                    counters.FlankMoves++;
                    Enter(board, AiState.Reposition);
                }
                return;
            }
            Enter(board, AiState.Investigate);
        }

        private static bool IsCalm(AiState state) =>
            state == AiState.Patrol || state == AiState.Idle || state == AiState.Search ||
            state == AiState.Investigate || state == AiState.Reposition;

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

        /// <summary>Per-enemy horizontal speed from consecutive planner ticks (feeds footstep hearing).</summary>
        private void TrackEnemies(SenseTarget[]? enemies, int enemyCount)
        {
            humanTeamCount = 0;
            for (int k = 0; k < enemyCount; k++)
            {
                var e = enemies![k];
                if (FindBoard(e.Id) == null && humanTeamCount < MaxTracked)
                {
                    humanTeamIds[humanTeamCount] = e.Id.Value;
                    humanTeams[humanTeamCount++] = e.Team;
                }
                int slot = -1, oldest = 0;
                for (int i = 0; i < MaxTracked; i++)
                {
                    if (trackTick[i] != 0 && trackId[i] == e.Id.Value) { slot = i; break; }
                    if (trackTick[i] < trackTick[oldest]) oldest = i;
                }
                float speed = 0;
                if (slot >= 0)
                {
                    uint dt = unchecked(tick - trackTick[slot]);
                    if (dt > 0 && dt <= 6)
                    {
                        float dx = e.Point.X - trackPoint[slot].X, dz = e.Point.Z - trackPoint[slot].Z;
                        speed = MathF.Sqrt(dx * dx + dz * dz) * 60f / dt;
                    }
                    else if (dt == 0) speed = trackSpeed[slot];
                }
                else slot = oldest;
                trackId[slot] = e.Id.Value;
                trackPoint[slot] = e.Point;
                trackTick[slot] = tick == 0 ? 1u : tick;
                trackSpeed[slot] = speed;
                enemySpeed[k] = speed;
            }
        }

        /// <summary>
        /// Footsteps (T085/T090): an enemy moving fast enough within its audible radius, or anyone within arm reach,
        /// is heard from any direction. T090 occludes beyond presence with leftover AI rays (fail-open when deferred).
        /// </summary>
        private void HearFootsteps(AiBlackboard board, SenseTarget[] enemies, int enemyCount)
        {
            // Rank audible candidates by distance; skip occluded ones beyond presence.
            int best = -1;
            float bestSq = float.MaxValue;
            for (int k = 0; k < enemyCount; k++)
            {
                var e = enemies[k];
                if (!PerceptionSystem.IsOpponent(board, e)) continue;
                float sp = enemySpeed[k];
                float radius = sp >= AiTuning.SprintStepSpeed ? AiTuning.SprintStepRadius
                    : sp >= AiTuning.RunStepSpeed ? AiTuning.RunStepRadius
                    : sp >= AiTuning.WalkStepSpeed ? AiTuning.WalkStepRadius : 0f;
                if (radius < AiTuning.PresenceRadius) radius = AiTuning.PresenceRadius;
                float dx = e.Point.X - board.Feet.X, dz = e.Point.Z - board.Feet.Z;
                float dSq = dx * dx + dz * dz;
                if (dSq > radius * radius || dSq >= bestSq) continue;
                if (dSq > AiTuning.PresenceRadius * AiTuning.PresenceRadius &&
                    !SoundReaches(board, e.Point, useBudget: true))
                {
                    counters.HearingOccluded++;
                    continue;
                }
                best = k;
                bestSq = dSq;
            }
            if (best < 0) return;
            // A fresh damage report is better information than footsteps; keep it.
            if (board.Heard == HeardClass.Damage && unchecked(tick - board.LastHeardTick) < AiTuning.StimulusRefreshTicks) return;
            bool fresh = board.Heard != HeardClass.Footsteps || unchecked(tick - board.LastHeardTick) > AiTuning.StimulusRefreshTicks;
            var p = enemies[best].Point;
            float noise = MathF.Sqrt(bestSq) * AiTuning.FootstepNoisePerMeter;
            board.LastHeard = new Float3(
                p.X + noise * AiNoise.Signed(board.Self.Value, tick / 15, AiNoise.SaltHearingX),
                board.Feet.Y,
                p.Z + noise * AiNoise.Signed(board.Self.Value, tick / 15, AiNoise.SaltHearingZ));
            board.LastHeardTick = tick;
            board.Heard = HeardClass.Footsteps;
            if (fresh) counters.FootstepsHeard++;
            if (board.State == AiState.Patrol || board.State == AiState.Idle || board.State == AiState.Search)
                EnterInvestigateUnlessCrowded(board);
        }

        /// <summary>
        /// T090: true when sound from <paramref name="source"/> reaches the bot. Presence always passes.
        /// Budgeted samples use leftover AI rays (fail-open when exhausted); gunfire uses a direct collision ray.
        /// </summary>
        private bool SoundReaches(AiBlackboard board, Float3 source, bool useBudget)
        {
            float dx = source.X - board.Feet.X, dy = source.Y - (board.Feet.Y + EyeHeight(board)), dz = source.Z - board.Feet.Z;
            float dSq = dx * dx + dz * dz;
            if (dSq <= AiTuning.PresenceRadius * AiTuning.PresenceRadius) return true;
            float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 0.5f) return true;
            var ear = new Float3(board.Feet.X, board.Feet.Y + EyeHeight(board), board.Feet.Z);
            var dir = new Float3(dx / len, dy / len, dz / len);
            RayHit hit;
            if (useBudget)
            {
                if (board.LastHearOcclusionTick != 0 &&
                    unchecked(tick - board.LastHearOcclusionTick) < AiTuning.HearOcclusionIntervalTicks)
                    return board.HearOcclusionClear;
                board.LastHearOcclusionTick = tick;
                if (!perception.TryBudgetedRay(ear, dir, len, board.Self, out hit))
                {
                    board.HearOcclusionClear = true; // fail-open when the vision/cover budget is exhausted
                    return true;
                }
            }
            else
            {
                hit = world.Raycast(ear, dir, len, new QueryFilter(visionMask, board.Self, false));
            }
            counters.HearingOcclusionRays++;
            bool blocked = hit.Status == QueryStatus.Hit && hit.Hit.DistanceMeters < len - 0.3f;
            if (useBudget) board.HearOcclusionClear = !blocked;
            return !blocked;
        }

        private void Sense(AiBlackboard board, SenseTarget[] enemies, int enemyCount)
        {
            var eye = new Float3(board.Feet.X, board.Feet.Y + EyeHeight(board), board.Feet.Z);
            int stickyIdx = -1, attackerIdx = -1;
            bool attackerRecent = board.WasDamaged && board.LastAttacker.HasValue &&
                unchecked(tick - board.LastDamagedTick) <= AiTuning.AttackerPriorityTicks;
            for (int k = 0; k < enemyCount; k++)
            {
                var id = enemies[k].Id;
                if (board.TargetId.HasValue && board.TargetId.Value == id) stickyIdx = k;
                if (attackerRecent && board.LastAttacker!.Value == id) attackerIdx = k;
            }
            if (attackerIdx == stickyIdx) attackerIdx = -1;
            int preferred = attackerIdx >= 0 ? attackerIdx : stickyIdx;
            // A sticky target is periodically re-scanned against others (staggered per bot) so a much
            // closer threat can take over; the attacker priority never yields.
            bool scan = attackerIdx < 0 && stickyIdx >= 0 &&
                (tick + board.Self.Value) % AiTuning.TargetScanIntervalTicks == 0;

            int n = 0;
            if (preferred >= 0 && !scan) visitOrder[n++] = preferred;
            int candidateStart = (int)((tick + board.Self.Value) % (uint)enemyCount);
            for (int offset = 0; offset < enemyCount; offset++)
            {
                int idx = (candidateStart + offset) % enemyCount;
                if (idx == preferred) continue;
                visitOrder[n++] = idx;
            }
            if (preferred >= 0 && scan) visitOrder[n++] = preferred;

            bool any = false;
            for (int v = 0; v < n; v++)
            {
                var candidate = enemies[visitOrder[v]];
                // Filter team/life before perception so allies never consume LOS budget.
                if (!PerceptionSystem.IsOpponent(board, candidate))
                {
                    if (candidate.Alive && candidate.Id != board.Self &&
                        PerceptionSystem.IsAlliedTeam(board.Team, candidate.Team))
                        counters.AlliedCandidatesSkipped++;
                    continue;
                }
                var savedLastSeen = board.LastSeenEnemy;
                float savedConfidence = board.Confidence;
                int raysBefore = perception.RaysUsedThisTick;
                bool sensed = perception.TrySense(board, eye, board.Yaw, candidate, counters, out bool acquired);
                // A real negative answer (a ray, range or field-of-view verdict) on the held target ends the hold.
                if (sensed && !acquired && senseHeldTarget.HasValue && candidate.Id == senseHeldTarget.Value) senseHeldLost = true;
                if (sensed && acquired)
                {
                    float d = Distance(eye, candidate.Point);
                    bool challenger = scan && stickyIdx >= 0 && candidate.Id != enemies[stickyIdx].Id &&
                        unchecked(tick - board.LastSeenTick) <= AiTuning.TargetMemoryTicks;
                    if (challenger && !(d < board.TargetDistance * AiTuning.TargetSwitchCloserFactor))
                    {
                        // Not worth switching; this tick's opportunity is spent and the sticky target is unconfirmed.
                        board.HasVisibleTarget = false;
                        board.VisibleTargetId = null;
                        board.VisibleTarget = null;
                        board.LastSeenEnemy = savedLastSeen;
                        board.Confidence = savedConfidence;
                        break;
                    }
                    Acquire(board, candidate, d, savedLastSeen);
                    any = true;
                    break;
                }
                // One actual LOS opportunity per bot per pass keeps the shared budget fair.
                if (perception.RaysUsedThisTick != raysBefore) break;
            }
            if (!any && board.Confidence > 0)
            {
                board.Confidence = Math.Max(0, board.Confidence - 0.02f);
                if (board.Confidence <= 0 && board.State == AiState.Engage) Enter(board, AiState.Search);
            }
        }

        private readonly List<MapCover> mapCovers = new List<MapCover>();
        private EntityId? senseHeldTarget;
        private bool senseHeldLost;

        /// <summary>
        /// ADR-112: a bot that saw its target within <see cref="AiTuning.VisibleHoldTicks"/> keeps tracking it on a tick
        /// where the shared vision budget gave it no ray (or a target re-scan left the sticky target unconfirmed).
        /// Before this, every budget-starved tick dropped the engagement: the aim jumped to a different point, the
        /// strafe stopped and the burst was cut, so engaged bots flickered. A ray that says "not visible" ends it.
        /// </summary>
        private void HoldVisibleTarget(AiBlackboard board, EntityId? held, SenseTarget[] enemies, int enemyCount)
        {
            if (!held.HasValue || senseHeldLost || board.LastSeenTick == 0) return;
            if (unchecked(tick - board.LastSeenTick) > AiTuning.VisibleHoldTicks) return;
            for (int k = 0; k < enemyCount; k++)
            {
                var candidate = enemies[k];
                if (candidate.Id != held.Value) continue;
                if (!PerceptionSystem.IsOpponent(board, candidate)) return;
                board.HasVisibleTarget = true;
                board.VisibleTargetId = candidate.Id;
                board.VisibleTarget = candidate.Point;
                board.Confidence = Math.Max(board.Confidence, 0.5f);
                counters.VisibleHolds++;
                return;
            }
        }

        private void Acquire(AiBlackboard board, SenseTarget candidate, float distance, Float3? previousPoint)
        {
            bool sameTarget = board.TargetId.HasValue && board.TargetId.Value == candidate.Id &&
                board.LastSeenTick != 0 && unchecked(tick - board.LastSeenTick) <= AiTuning.TargetMemoryTicks;
            bool inCombat = board.LastSeenTick != 0 && unchecked(tick - board.LastSeenTick) <= AiTuning.CombatMemoryTicks;
            if (sameTarget && previousPoint.HasValue)
            {
                uint dt = Math.Max(1u, unchecked(tick - board.LastSeenTick));
                float s = 60f / dt;
                var p = previousPoint.Value;
                float vx = (candidate.Point.X - p.X) * s, vz = (candidate.Point.Z - p.Z) * s;
                float sx = board.TargetVelocity.X * 0.7f + vx * 0.3f, sz = board.TargetVelocity.Z * 0.7f + vz * 0.3f;
                float speed = MathF.Sqrt(sx * sx + sz * sz);
                if (speed > 10f) { sx *= 10f / speed; sz *= 10f / speed; speed = 10f; }
                board.TargetVelocity = new Float3(sx, 0, sz);
                board.TargetSpeed = speed;
            }
            else if (!sameTarget)
            {
                if (board.TargetId.HasValue && board.TargetId.Value != candidate.Id) counters.TargetSwitches++;
                uint reaction = inCombat
                    ? AiTuning.SwitchReactionBaseTicks + AiNoise.Range(board.Self.Value, candidate.Id.Value, AiNoise.SaltReaction, AiTuning.SwitchReactionSpreadTicks)
                    : AiTuning.ReactionBaseTicks + AiNoise.Range(board.Self.Value, candidate.Id.Value, AiNoise.SaltReaction, AiTuning.ReactionSpreadTicks);
                float extra = Math.Max(0f, distance - 15f) * 0.1f * AiTuning.RangeReactionTicksPer10m;
                reaction += Math.Min(AiTuning.MaxRangeReactionTicks, (uint)extra);
                board.ReactionReadyTick = tick + reaction;
                board.AcquiredTick = tick;
                board.TargetVelocity = default;
                board.TargetSpeed = 0;
                board.HasStrafeCenter = false;
                if (!inCombat) Callout(board, candidate.Point);
            }
            board.TargetId = candidate.Id;
            board.LastSeenTick = tick;
            board.LastSeenEnemy = candidate.Point;
            board.HasVisibleTarget = true;
            board.VisibleTargetId = candidate.Id;
            board.VisibleTarget = candidate.Point;
            board.Confidence = 1f;
            board.TargetDistance = distance;
            board.TargetHealth = candidate.Health;
            if (tick >= board.ReactionReadyTick && board.State != AiState.Engage && board.State != AiState.SeekCover &&
                !(board.State == AiState.Reload && board.MagAmmo <= 0))
                Enter(board, AiState.Engage);
        }

        /// <summary>First sighting of an enemy: nearby calm allies investigate it (no rays).</summary>
        private void Callout(AiBlackboard caller, Float3 point)
        {
            if (caller.Team == 0) return;
            float radiusSq = AiTuning.CalloutRadius * AiTuning.CalloutRadius;
            for (int i = 0; i < botCount; i++)
            {
                var ally = boards[i];
                if (ally == caller || !ally.Alive || ally.State == AiState.Dead) continue;
                if (!PerceptionSystem.IsAlliedTeam(caller.Team, ally.Team)) continue;
                if (ally.HasVisibleTarget || !IsCalm(ally.State)) continue;
                float dx = ally.Feet.X - caller.Feet.X, dz = ally.Feet.Z - caller.Feet.Z;
                if (dx * dx + dz * dz > radiusSq) continue;
                ally.LastHeard = point;
                ally.LastHeardTick = tick;
                ally.Heard = HeardClass.Callout;
                counters.Callouts++;
                EnterInvestigateUnlessCrowded(ally);
            }
        }

        private void ExpireStimuli(AiBlackboard board)
        {
            if (board.LastHeard.HasValue && unchecked(tick - board.LastHeardTick) > StimulusExpiryTicks)
            {
                board.LastHeard = null;
                board.Heard = HeardClass.None;
            }
        }

        // ---------------------------------------------------------------- utility decisions

        private void ApplyUtility(AiBlackboard board)
        {
            bool recentlyFighting = board.LastSeenTick != 0 && unchecked(tick - board.LastSeenTick) <= 60;
            if (board.State == AiState.Reload)
            {
                // Authority owns reload timing and ammunition; leave only once the magazine actually grew.
                int magSize = ActiveMagazineSize(board);
                if (board.MagAmmo > board.ReloadStartMag || board.MagAmmo >= magSize)
                    Enter(board, board.HasVisibleTarget ? AiState.Engage
                        : board.CoverPoint.HasValue || board.CoverReservation.HasValue ? AiState.SeekCover : AiState.Search);
            }
            else if (board.MagAmmo <= 0 && board.ReserveAmmo > 0)
            {
                board.ReloadStartMag = 0;
                Enter(board, AiState.Reload);
                if (recentlyFighting && board.LastSeenEnemy.HasValue && coverSites.Count == 0 &&
                    !board.CoverPoint.HasValue && !board.CoverProbeActive &&
                    !TryClaimMapCover(board, board.LastSeenEnemy.Value))
                    CoverFinder.Start(board, board.LastSeenEnemy.Value);
            }
            else if (board.MagAmmo < TacticalReloadThreshold(board) && board.ReserveAmmo > 0 && !board.HasVisibleTarget &&
                     (board.LastSeenTick == 0 || unchecked(tick - board.LastSeenTick) > AiTuning.CombatMemoryTicks) &&
                     (IsCalm(board.State) || board.InCover))
            {
                board.ReloadStartMag = board.MagAmmo;
                counters.TacticalReloads++;
                Enter(board, AiState.Reload);
            }
            else if (board.InCover && board.State == AiState.SeekCover && !board.HasVisibleTarget &&
                     board.MagAmmo < ActiveMagazineSize(board) && board.ReserveAmmo > 0)
            {
                board.ReloadStartMag = board.MagAmmo;
                Enter(board, AiState.Reload);
            }

            // Low health and losing the trade: break off toward cover (cooldown keeps it from flapping).
            if (board.State == AiState.Engage && board.HasVisibleTarget && board.Health < AiTuning.LowHealth &&
                tick >= board.RetreatAllowedTick &&
                (board.TargetHealth > board.Health + AiTuning.LosingTradeMargin ||
                 (board.WasDamaged && unchecked(tick - board.LastDamagedTick) < 30)))
            {
                board.RetreatAllowedTick = tick + AiTuning.RetreatCooldownTicks;
                counters.Retreats++;
                Enter(board, AiState.SeekCover);
            }
            // ADR-112: fights move. After a seeded 2-4 s exchange in the open, a bot with authored cover in reach
            // moves to it (the hiding face away from its target) and fights from there by peeking.
            else if (board.State == AiState.Engage && board.HasVisibleTarget && board.VisibleTarget.HasValue && mapCovers.Count > 0 &&
                     tick >= board.NextRepositionTick && board.TargetDistance > AiTuning.CloseRange &&
                     unchecked(tick - board.StateEnteredTick) >= AiTuning.EngageInOpenTicks +
                        AiNoise.Range(board.Self.Value, board.StateEnteredTick, AiNoise.SaltCover, AiTuning.EngageInOpenSpreadTicks))
            {
                board.NextRepositionTick = tick + AiTuning.CoverRepositionCooldownTicks;
                if (!board.InCover && TryClaimMapCover(board, board.VisibleTarget.Value))
                {
                    counters.CoverRepositions++;
                    Enter(board, AiState.SeekCover);
                }
            }
        }

        private void Decide(AiBlackboard board)
        {
            switch (board.State)
            {
                case AiState.Patrol:
                    EnsurePath(board, PatrolPoint(board));
                    break;
                case AiState.Investigate:
                    if (board.LastHeard.HasValue) EnsurePath(board, board.LastHeard.Value, retarget: true);
                    else Enter(board, AiState.Search);
                    break;
                case AiState.Engage:
                    if (board.HasVisibleTarget && board.VisibleTarget.HasValue)
                    {
                        if (board.TargetDistance >= AiTuning.AdvanceRange) EnsurePath(board, board.VisibleTarget.Value, retarget: true);
                        else DropPath(board); // tactical movement replaces route following while the enemy is in view
                    }
                    else if (board.LastSeenEnemy.HasValue && unchecked(tick - board.LastSeenTick) > AiTuning.TargetMemoryTicks)
                        EnsurePath(board, SearchPoint(board), retarget: true);
                    break;
                case AiState.SeekCover:
                    DecideCover(board);
                    break;
                case AiState.Reload:
                    if (board.ReserveAmmo <= 0) Enter(board, AiState.SeekCover);
                    else if (board.CoverPoint.HasValue && !board.InCover) MoveToCover(board);
                    break;
                case AiState.Reposition:
                case AiState.Search:
                    if (board.FlankGoal.HasValue)
                    {
                        if (Within(board.Feet, board.FlankGoal.Value, AiTuning.FlankArrivalMeters)) board.FlankGoal = null;
                        else { EnsurePath(board, board.FlankGoal.Value, retarget: true); break; }
                    }
                    if (board.LastSeenEnemy.HasValue) EnsurePath(board, SearchPoint(board), retarget: true);
                    else if (combatDestinations.Count > 0) EnsurePath(board, ChooseCombatDestination(board));
                    else Enter(board, AiState.Patrol);
                    break;
            }
        }

        private void DecideCover(AiBlackboard board)
        {
            if (coverSites.Count > 0)
            {
                if (!board.CoverReservation.HasValue) ClaimBestCover(board);
                if (board.CoverReservation.HasValue)
                {
                    var site = FindCover(board.CoverReservation.Value);
                    if (site.HasValue) EnsurePath(board, site.Value.Anchor);
                }
                else if (combatDestinations.Count > 0 || board.LastSeenEnemy.HasValue)
                    Enter(board, AiState.Search);
                return;
            }
            if (board.CoverPoint.HasValue)
            {
                if (!board.InCover) { MoveToCover(board); return; }
                if (board.HasVisibleTarget && board.TargetDistance < AiTuning.CloseRange) { Enter(board, AiState.Engage); return; }
                // ADR-112: a bot trading shots from cover keeps the spot; it moves on once the fight goes quiet.
                uint quietSince = board.LastSeenTick > board.InCoverSinceTick ? board.LastSeenTick : board.InCoverSinceTick;
                if (unchecked(tick - quietSince) >= AiTuning.CoverHoldTicks) Enter(board, AiState.Reposition);
                return;
            }
            if (board.CoverProbeActive) return; // probes run with leftover rays at the end of this tick
            // No cover found (or no threat to hide from): fall back to the old behaviour.
            if (board.MagAmmo <= 0 && board.ReserveAmmo > 0) Enter(board, AiState.Reload);
            else if (board.HasVisibleTarget) Enter(board, AiState.Engage);
            else if (combatDestinations.Count > 0 || board.LastSeenEnemy.HasValue)
            {
                // No free cover site: keep fighting through the map instead of idling in place.
                Enter(board, AiState.Search);
            }
        }

        private void MoveToCover(AiBlackboard board)
        {
            var point = board.CoverPoint!.Value;
            float dx = point.X - board.Feet.X, dz = point.Z - board.Feet.Z;
            if (dx * dx + dz * dz <= AiTuning.CoverArrivalMeters * AiTuning.CoverArrivalMeters)
            {
                board.InCover = true;
                board.InCoverSinceTick = tick;
                DropPath(board);
                return;
            }
            EnsurePath(board, point, retarget: true);
        }

        /// <summary>Last seen position pushed forward along the target's observed motion (≤1 s, ≤5 m).</summary>
        private static Float3 SearchPoint(AiBlackboard board)
        {
            var p = board.LastSeenEnemy!.Value;
            float vx = board.TargetVelocity.X, vz = board.TargetVelocity.Z;
            float len = MathF.Sqrt(vx * vx + vz * vz);
            if (len > 5f) { vx *= 5f / len; vz *= 5f / len; }
            return new Float3(p.X + vx, p.Y, p.Z + vz);
        }

        /// <summary>Combat destination that no ally is already heading for (or standing at).</summary>
        private Float3 ChooseCombatDestination(AiBlackboard board)
        {
            if (board.PathGoal.HasValue) return board.PathGoal.Value;
            return combatDestinations[ChooseSpreadDestination(board, combatDestinations, -1, -1)];
        }

        /// <summary>
        /// T085/T090 team spread: score every destination by how far it is from allies' goals, positions and
        /// active path corners (shared corridors), by a preferred leg length, and by a seeded per-bot preference;
        /// never repeat the last two picks. Deterministic; runs only when a bot needs a new destination.
        /// </summary>
        private int ChooseSpreadDestination(AiBlackboard board, List<Float3> points, int last, int previous)
        {
            int count = points.Count;
            if (count == 1) return 0;
            int best = Rotate(board, count);
            float bestScore = float.MinValue;
            bool hasEnemyHome = EnemyHome(board, out Float3 enemyHome);
            for (int k = 0; k < count; k++)
            {
                if ((k == last || k == previous) && count > 2) continue;
                var c = points[k];
                float dx = c.X - board.Feet.X, dz = c.Z - board.Feet.Z;
                float leg = MathF.Sqrt(dx * dx + dz * dz);
                if (leg < 3f) continue;
                float score = -Math.Abs(leg - AiTuning.PatrolPreferredMeters) / AiTuning.PatrolPreferredMeters;
                score += 0.6f * AiNoise.Unit(board.Self.Value, (ulong)(uint)board.DestinationCursor * 131UL + (ulong)k, AiNoise.SaltPatrol);
                score += TeamTerrainScore(board, c, hasEnemyHome, enemyHome);
                for (int i = 0; i < botCount; i++)
                {
                    var other = boards[i];
                    if (other == board || !other.Alive || other.State == AiState.Dead) continue;
                    if (board.Team != 0 && !PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                    var goal = other.State == AiState.Patrol && other.PatrolGoal.HasValue ? other.PatrolGoal : other.PathGoal;
                    if (goal.HasValue && Within(goal.Value, c, AiTuning.SpreadMeters)) score -= 2f;
                    if (Within(other.Feet, c, AiTuning.SpreadMeters * 0.5f)) score -= 0.5f;
                    // T090: penalize destinations that sit on an ally's current path corridor.
                    int corners = other.PathCornerCount;
                    for (int p = other.PathCornerIndex; p < corners; p++)
                    {
                        if (!Within(other.PathCorners[p], c, AiTuning.RouteCorridorMeters)) continue;
                        score -= AiTuning.RouteCorridorPenalty;
                        counters.RouteCorridorPenalties++;
                        break;
                    }
                }
                if (score > bestScore) { bestScore = score; best = k; }
            }
            return best;
        }

        // ---------------------------------------------------------------- navigation

        /// <summary>Goal moved further than this from the active path's goal: an urgent goal replaces the route.</summary>
        public const float RetargetDistance = 2f;

        /// <param name="retarget">
        /// Urgent goals (stimuli, last contact, cover, advancing on a visible enemy) replace an active route
        /// whose goal is elsewhere, within the replan cooldown and the per-tick path budget. Routine
        /// patrol/combat destinations keep the current route until it finishes.
        /// </param>
        private void EnsurePath(AiBlackboard board, Float3 goal, bool retarget = false)
        {
            if (board.PathTicket.HasValue || board.PathCornerIndex < board.PathCornerCount)
            {
                if (!retarget || !board.PathGoal.HasValue || Within(board.PathGoal.Value, goal, RetargetDistance)) return;
                if (pathsThisTick >= MaxNewPathsPerTick || tick - board.LastReplanTick < ReplanCooldownTicks) return;
                DropPath(board);
            }
            if (pathsThisTick >= MaxNewPathsPerTick) return;
            if (tick < board.RetryAfterTick) return;
            if (tick - board.LastReplanTick < ReplanCooldownTicks && board.PathGoal.HasValue) return;
            var ticket = navigation.RequestPath(board.Self, goal, navRevision);
            board.PathTicket = ticket;
            board.PathGoal = goal;
            board.PathRequestedTick = tick;
            board.LastReplanTick = tick;
            pathsThisTick++;
            counters.PathsRequestedThisTick++;
        }

        private void DropPath(AiBlackboard board)
        {
            if (board.PathTicket.HasValue)
            {
                navigation.Cancel(board.PathTicket.Value);
                board.PathTicket = null;
            }
            board.PathCornerCount = 0;
            board.PathCornerIndex = 0;
            board.PathGoal = null;
        }

        private void PollPaths()
        {
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (!board.PathTicket.HasValue) continue;
                if (unchecked(tick - board.PathRequestedTick) >= PathTimeoutTicks)
                {
                    navigation.Cancel(board.PathTicket.Value);
                    board.PathTicket = null;
                    board.PathGoal = null;
                    FailPath(board);
                    continue;
                }
                var result = navigation.Poll(board.PathTicket.Value, pathBuffer);
                if (result.Status == PathStatus.Pending) continue;
                if (result.Status == PathStatus.Stale || result.Status == PathStatus.Cancelled || result.Status == PathStatus.Failed)
                {
                    board.PathTicket = null;
                    board.PathGoal = null;
                    if (result.Status == PathStatus.Failed) FailPath(board);
                    continue;
                }
                if (result.Status == PathStatus.Partial) counters.PathsPartial++;
                else counters.PathsCompleted++;
                board.PathCornerCount = result.WrittenCount;
                board.PathCornerIndex = 0;
                for (int c = 0; c < result.WrittenCount; c++)
                    board.PathCorners[c] = pathBuffer[c];
                board.PathTicket = null;
                AdvancePathCorners(board);
            }
        }

        /// <summary>Back off and rotate to the next destination so an unreachable goal is not retried every tick.</summary>
        private void FailPath(AiBlackboard board)
        {
            counters.PathsFailed++;
            board.RetryAfterTick = tick + ReplanCooldownTicks;
            board.DestinationCursor++;
            if (board.CoverPoint.HasValue && !board.InCover && board.CoverReservation.HasValue &&
                (board.CoverReservation.Value & CoverFinder.DynamicCoverIdFlag) != 0)
            {
                // Unreachable probe point: give it up so the bot does not hold an orphan claim.
                ReleaseCover(board);
            }
        }

        private static int Rotate(AiBlackboard board, int count) =>
            (int)((board.Self.Value + (ulong)(uint)board.DestinationCursor) % (ulong)count);

        private static void AdvancePathCorners(AiBlackboard board)
        {
            float arrivalSq = CornerArrivalDistance * CornerArrivalDistance;
            while (board.PathCornerIndex < board.PathCornerCount)
            {
                var corner = board.PathCorners[board.PathCornerIndex];
                float dx = corner.X - board.Feet.X;
                float dz = corner.Z - board.Feet.Z;
                if (dx * dx + dz * dz > arrivalSq) break;
                board.PathCornerIndex++;
            }
            if (board.PathCornerIndex >= board.PathCornerCount && board.PathCornerCount > 0)
            {
                // Arrived: rotate patrol/search destinations; an investigated last-seen or heard point is spent.
                board.DestinationCursor++;
                if (board.LastSeenEnemy.HasValue && !board.HasVisibleTarget)
                {
                    float sx = board.LastSeenEnemy.Value.X - board.Feet.X;
                    float sz = board.LastSeenEnemy.Value.Z - board.Feet.Z;
                    var predicted = SearchPoint(board);
                    float px = predicted.X - board.Feet.X, pz = predicted.Z - board.Feet.Z;
                    if (sx * sx + sz * sz <= 2.25f || px * px + pz * pz <= 4f) board.LastSeenEnemy = null;
                }
                if (board.LastHeard.HasValue && board.State == AiState.Investigate)
                {
                    float hx = board.LastHeard.Value.X - board.Feet.X;
                    float hz = board.LastHeard.Value.Z - board.Feet.Z;
                    if (hx * hx + hz * hz <= 4f)
                    {
                        board.LastHeard = null;
                        board.Heard = HeardClass.None;
                    }
                }
                board.PathCornerCount = 0;
                board.PathCornerIndex = 0;
                board.PathGoal = null;
                board.StuckTicks = 0;
                board.StuckFailures = 0;
            }
        }

        private static float EyeHeight(AiBlackboard board) => board.Crouched ? CrouchedEyeHeight : StandingEyeHeight;

        private static float Distance(Float3 a, Float3 b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static void UpdateSpeed(AiBlackboard board)
        {
            if (board.HasPrevFeet)
            {
                float dx = board.Feet.X - board.PrevFeet.X, dz = board.Feet.Z - board.PrevFeet.Z;
                board.SpeedMps = MathF.Sqrt(dx * dx + dz * dz) * 60f;
            }
            else board.SpeedMps = 0;
            board.PrevFeet = board.Feet;
            board.HasPrevFeet = true;
        }

        private void UpdateStuck(AiBlackboard board)
        {
            bool hasPath = board.PathTicket.HasValue || board.PathCornerIndex < board.PathCornerCount;
            bool moveIntent = WantsHorizontalProgress(board);
            // No path and no move intent: idle is fine. Move intent without progress still counts as stuck
            // (bots wedged in geometry often drop their path ticket).
            if (!hasPath && !moveIntent)
            {
                board.StuckAnchor = board.Feet;
                board.StuckTicks = 0;
                return;
            }
            float dx = board.Feet.X - board.StuckAnchor.X;
            float dz = board.Feet.Z - board.StuckAnchor.Z;
            if (dx * dx + dz * dz >= StuckDistance * StuckDistance)
            {
                board.StuckAnchor = board.Feet;
                board.StuckTicks = 0;
                return;
            }
            board.StuckTicks++;
            // Hop obstacles before (or together with) the replan; cooldown prevents jump spam.
            if (board.StuckTicks >= JumpStuckTicks && tick >= board.NextJumpTick)
            {
                board.JumpPending = true;
                board.NextJumpTick = tick + JumpCooldownTicks;
            }
            if (board.StuckTicks < StuckTicksThreshold) return;
            counters.StuckEvents++;
            board.StuckTicks = 0;
            board.StuckFailures++;
            if (board.PathTicket.HasValue)
                navigation.Cancel(board.PathTicket.Value);
            board.PathTicket = null;
            board.PathCornerCount = 0;
            board.PathCornerIndex = 0;
            board.PathGoal = null;
            if (board.StuckFailures >= 3)
            {
                board.NeedsRelocation = true;
                Enter(board, AiState.Search);
            }
            else if (board.CoverReservation.HasValue) Enter(board, AiState.Reposition);
            else Enter(board, AiState.SeekCover);
        }

        /// <summary>States that expect horizontal travel; used when path tickets are already dropped.</summary>
        private static bool WantsHorizontalProgress(AiBlackboard board) =>
            board.State == AiState.Patrol
            || board.State == AiState.Investigate
            || board.State == AiState.SeekCover
            || board.State == AiState.Reposition
            || board.State == AiState.Search
            // ADR-112: an engaged bot only expects progress when it actually asked to move last tick; a long-range or
            // crouch shooter holding still is not stuck (it used to fall into SeekCover and spawn relocation).
            || (board.State == AiState.Engage && board.HasVisibleTarget && board.CommandedMove);

        /// <summary>Copy living bots that need a spawn-slot rescue into <paramref name="destination"/>.</summary>
        public int CollectRelocationRequests(List<EntityId> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int n = 0;
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (!board.Alive || board.State == AiState.Dead || !board.NeedsRelocation) continue;
                destination.Add(board.Self);
                n++;
            }
            return n;
        }

        public void ClearRelocationRequest(EntityId entity)
        {
            var board = FindBoard(entity);
            if (board == null) return;
            board.NeedsRelocation = false;
            board.StuckTicks = 0;
            board.StuckFailures = 0;
            board.StuckAnchor = board.Feet;
        }

        // ---------------------------------------------------------------- cover

        private void ClaimBestCover(AiBlackboard board)
        {
            for (int i = 0; i < coverSites.Count; i++)
            {
                var site = coverSites[i];
                if (covers.IsClaimed(site.Id)) continue;
                if (!covers.TryClaim(site.Id, board.Self)) continue;
                board.CoverReservation = site.Id;
                counters.CoverClaims++;
                return;
            }
        }

        private CoverSite? FindCover(ulong id)
        {
            for (int i = 0; i < coverSites.Count; i++)
                if (coverSites[i].Id == id) return coverSites[i];
            return null;
        }

        // ---------------------------------------------------------------- ADR-112 map tactics

        /// <summary>
        /// Claims the best authored cover against <paramref name="threat"/>: the hiding spot is on the box face away
        /// from the threat, nearest the bot's own lateral position. Scored by travel distance, hard cover and ally
        /// spacing; never a spot that is behind the threat or that the threat stands next to. No rays.
        /// </summary>
        private bool TryClaimMapCover(AiBlackboard board, Float3 threat)
        {
            if (mapCovers.Count == 0) return false;
            float botThreat = Distance2D(board.Feet, threat);
            int best = -1; float bestScore = float.MinValue; Float3 bestSpot = default;
            for (int k = 0; k < mapCovers.Count; k++)
            {
                var cover = mapCovers[k];
                if (covers.IsClaimed(cover.Id)) continue;
                var spot = HideSpot(cover, threat, board.Feet);
                float travel = Distance2D(board.Feet, spot);
                if (travel > AiTuning.MapCoverSearchMeters) continue;
                float spotThreat = Distance2D(spot, threat);
                if (spotThreat < AiTuning.MapCoverMinThreatMeters) continue;
                // The box must sit between the threat and the spot, and the spot must not be a charge at the enemy.
                if (Distance2D(cover.Center, threat) >= spotThreat) continue;
                if (spotThreat < botThreat - AiTuning.MapCoverMaxAdvanceMeters) continue;
                float score = -travel / AiTuning.MapCoverSearchMeters;
                if (cover.Hard) score += 0.25f;
                score += 0.1f * AiNoise.Unit(board.Self.Value, cover.Id, AiNoise.SaltCover);
                for (int i = 0; i < botCount; i++)
                {
                    var other = boards[i];
                    if (other == board || !other.Alive || other.State == AiState.Dead) continue;
                    if (board.Team != 0 && !PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                    if (Within(other.Feet, spot, AiTuning.MapCoverAllySpacing)) score -= 0.6f;
                    if (other.CoverPoint.HasValue && Within(other.CoverPoint.Value, spot, AiTuning.MapCoverAllySpacing)) score -= 0.6f;
                }
                if (score > bestScore) { bestScore = score; best = k; bestSpot = spot; }
            }
            if (best < 0) return false;
            var chosen = mapCovers[best];
            if (!covers.TryClaim(chosen.Id, board.Self)) return false;
            if (board.CoverReservation.HasValue && board.CoverReservation.Value != chosen.Id)
                covers.Release(board.CoverReservation.Value, board.Self);
            board.CoverReservation = chosen.Id;
            board.CoverPoint = bestSpot;
            board.CoverThreat = threat;
            board.CoverLow = chosen.Size.Y < AiTuning.StandingCoverHeight;
            board.InCover = false;
            board.CoverProbeActive = false;
            counters.CoverClaims++;
            counters.MapCoverClaims++;
            return true;
        }

        /// <summary>Spot 0.6 m off the box face that looks away from the threat, slid toward the bot along it.</summary>
        public static Float3 HideSpot(MapCover cover, Float3 threat, Float3 bot)
        {
            float hx = cover.Size.X * 0.5f, hz = cover.Size.Z * 0.5f;
            float dx = cover.Center.X - threat.X, dz = cover.Center.Z - threat.Z;
            float baseY = cover.Center.Y - cover.Size.Y * 0.5f;
            const float standOff = 0.6f, inset = 0.4f;
            // Which face the threat looks at, normalised by the box's extents (a long wall is hidden behind its length).
            if (Math.Abs(dx) * Math.Max(hz, 0.05f) >= Math.Abs(dz) * Math.Max(hx, 0.05f))
            {
                float x = cover.Center.X + (dx >= 0f ? 1f : -1f) * (hx + standOff);
                float z = Clamp(bot.Z, cover.Center.Z - Math.Max(0f, hz - inset), cover.Center.Z + Math.Max(0f, hz - inset));
                return new Float3(x, baseY, z);
            }
            else
            {
                float z = cover.Center.Z + (dz >= 0f ? 1f : -1f) * (hz + standOff);
                float x = Clamp(bot.X, cover.Center.X - Math.Max(0f, hx - inset), cover.Center.X + Math.Max(0f, hx - inset));
                return new Float3(x, baseY, z);
            }
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private static float Distance2D(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Allies (bots) engaging or searching within the press radius of <paramref name="point"/> (investigators are
        /// counted separately by the stimulus crowd check).</summary>
        private int AlliesPressing(AiBlackboard board, Float3 point)
        {
            if (board.Team == 0) return 0;
            int n = 0;
            for (int i = 0; i < botCount; i++)
            {
                var other = boards[i];
                if (other == board || !other.Alive || other.State == AiState.Dead) continue;
                if (!PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                bool pressing = other.State == AiState.Engage || other.State == AiState.Search;
                if (!pressing) continue;
                var at = other.LastSeenEnemy ?? other.LastHeard;
                if (at.HasValue && Within(at.Value, point, AiTuning.PressRadiusMeters)) n++;
            }
            return n;
        }

        /// <summary>
        /// A route/lane point beside the contact rather than on the straight line to it: 8-30 m from the contact,
        /// at a 45-135 degree angle off the bot's own approach, away from allies' goals. Null when none fits.
        /// </summary>
        private Float3? ChooseFlankGoal(AiBlackboard board, Float3 contact)
        {
            float ax = board.Feet.X - contact.X, az = board.Feet.Z - contact.Z;
            float alen = MathF.Sqrt(ax * ax + az * az);
            if (alen < 1e-3f) return null;
            ax /= alen; az /= alen;
            Float3? best = null; float bestScore = float.MinValue;
            for (int list = 0; list < 2; list++)
            {
                var points = list == 0 ? combatDestinations : patrolDestinations;
                for (int k = 0; k < points.Count; k++)
                {
                    var c = points[k];
                    float cx = c.X - contact.X, cz = c.Z - contact.Z;
                    float clen = MathF.Sqrt(cx * cx + cz * cz);
                    if (clen < AiTuning.FlankMinMeters || clen > AiTuning.FlankMaxMeters) continue;
                    float cos = (cx * ax + cz * az) / clen;
                    if (cos > 0.7071f || cos < -0.7071f) continue; // not ahead or behind the contact: beside it
                    if (Distance2D(board.Feet, c) > alen + AiTuning.FlankMaxMeters) continue;
                    float score = -Math.Abs(clen - AiTuning.FlankPreferredMeters) / AiTuning.FlankPreferredMeters;
                    score += 0.4f * AiNoise.Unit(board.Self.Value, (ulong)k * 7919UL + (ulong)list, AiNoise.SaltCover);
                    for (int i = 0; i < botCount; i++)
                    {
                        var other = boards[i];
                        if (other == board || !other.Alive || other.State == AiState.Dead) continue;
                        if (board.Team != 0 && !PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                        if (other.FlankGoal.HasValue && Within(other.FlankGoal.Value, c, AiTuning.SpreadMeters)) score -= 1.5f;
                        if (Within(other.Feet, c, AiTuning.SpreadMeters * 0.5f)) score -= 0.5f;
                    }
                    if (score > bestScore) { bestScore = score; best = c; }
                }
            }
            return best;
        }

        /// <summary>Centroid of the opposing bots' spawn points (their home side); false when none are known.</summary>
        private bool EnemyHome(AiBlackboard board, out Float3 home)
        {
            home = default;
            if (board.Team == 0) return false;
            float x = 0, y = 0, z = 0; int n = 0;
            for (int i = 0; i < botCount; i++)
            {
                var other = boards[i];
                if (other.Team == 0 || PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                x += other.SpawnPoint.X; y += other.SpawnPoint.Y; z += other.SpawnPoint.Z; n++;
            }
            if (n == 0) return false;
            home = new Float3(x / n, y / n, z / n);
            return true;
        }

        /// <summary>
        /// ADR-112 team-aware destination term: avoid loitering in the own spawn, lean toward the enemy's side, and
        /// avoid spots where two or more allies are already fighting (they do not need a third body there).
        /// </summary>
        private float TeamTerrainScore(AiBlackboard board, Float3 c, bool hasEnemyHome, Float3 enemyHome)
        {
            if (board.Team == 0) return 0f;
            float score = 0f;
            float own = Distance2D(c, board.SpawnPoint);
            if (own < AiTuning.OwnSpawnAvoidMeters) score -= 1f;
            if (hasEnemyHome)
            {
                // -1 at the own spawn, +1 at the enemy's, whatever the map size.
                float span = Math.Max(20f, Distance2D(board.SpawnPoint, enemyHome));
                score += 0.5f * Clamp((own - Distance2D(c, enemyHome)) / span, -1f, 1f);
            }
            int engaged = 0;
            for (int i = 0; i < botCount; i++)
            {
                var other = boards[i];
                if (other == board || !other.Alive || other.State != AiState.Engage) continue;
                if (!PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                if (Within(other.Feet, c, AiTuning.HotSpotMeters)) engaged++;
            }
            if (engaged >= 2) score -= 1.5f;
            return score;
        }

        private void ReleaseCover(AiBlackboard board)
        {
            if (board.CoverReservation.HasValue)
            {
                if (covers.Release(board.CoverReservation.Value, board.Self)) counters.CoverReleases++;
                else counters.OrphanReleases++;
                board.CoverReservation = null;
            }
            board.CoverPoint = null;
            board.InCover = false;
            board.InCoverSinceTick = 0;
            board.CoverProbeActive = false;
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

        private void Enter(AiBlackboard board, AiState state)
        {
            if (board.State == state) return;
            var previous = board.State;
            bool keepsCover = state == AiState.SeekCover || state == AiState.Reload;
            if ((previous == AiState.SeekCover || previous == AiState.Reload) && !keepsCover &&
                (board.CoverPoint.HasValue || board.CoverProbeActive ||
                 (board.CoverReservation.HasValue && (board.CoverReservation.Value & CoverFinder.DynamicCoverIdFlag) != 0)))
                ReleaseCover(board);
            board.State = state;
            board.StateEnteredTick = tick;
            counters.StateChanges++;
            if (state == AiState.Engage) { board.HasStrafeCenter = false; board.FlankGoal = null; }
            if (state == AiState.SeekCover)
            {
                if (coverSites.Count > 0) ClaimBestCover(board);
                else if (!board.CoverPoint.HasValue && !board.CoverProbeActive)
                {
                    Float3? threat = board.VisibleTarget ?? board.LastSeenEnemy ??
                        (board.Heard == HeardClass.Damage ? board.LastHeard : null);
                    if (threat.HasValue && !TryClaimMapCover(board, threat.Value)) CoverFinder.Start(board, threat.Value);
                }
            }
            // ADR-112: losing sight of an enemy that allies are already pressing sends this bot round a flank
            // instead of down the same line to the same last-seen point.
            if ((state == AiState.Search || state == AiState.Reposition) && !board.FlankGoal.HasValue && board.LastSeenEnemy.HasValue &&
                AlliesPressing(board, board.LastSeenEnemy.Value) >= 1)
                board.FlankGoal = ChooseFlankGoal(board, board.LastSeenEnemy.Value);
        }

        private Float3 PatrolPoint(AiBlackboard board)
        {
            if (patrolDestinations.Count > 0)
            {
                if (board.PatrolGoal.HasValue && board.PatrolGoalCursor == board.DestinationCursor) return board.PatrolGoal.Value;
                int index = ChooseSpreadDestination(board, patrolDestinations, board.PatrolIndex, board.PreviousPatrolIndex);
                board.PreviousPatrolIndex = board.PatrolIndex;
                board.PatrolIndex = index;
                board.PatrolGoal = patrolDestinations[index];
                board.PatrolGoalCursor = board.DestinationCursor;
                return board.PatrolGoal.Value;
            }
            // Deterministic local orbit based on entity id.
            double a = (board.Self.Value % 8) * (Math.PI / 4);
            return new Float3(board.Feet.X + (float)Math.Cos(a) * 4f, board.Feet.Y, board.Feet.Z + (float)Math.Sin(a) * 4f);
        }

        /// <summary>Atan2 yaw inside the C0 half-open interval [-π, π); float π and -π both fall outside it.</summary>
        internal static float ContractYaw(float dx, float dz)
        {
            float yaw = MathF.Atan2(dx, dz);
            if ((double)yaw >= Math.PI || (double)yaw < -Math.PI) yaw = -3.1415925f;
            return yaw;
        }

        private static float StraightMetersToCorner(AiBlackboard board)
        {
            if (board.PathCornerIndex >= board.PathCornerCount) return 0f;
            var c = board.PathCorners[board.PathCornerIndex];
            float dx = c.X - board.Feet.X, dz = c.Z - board.Feet.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Where a bot without a visible enemy should look: the last contact while the fight is fresh, a recent
        /// damage source, or a nearby heard/called-out stimulus (pre-aim instead of staring down the route).
        /// </summary>
        private Float3? ThreatLook(AiBlackboard board)
        {
            if (board.InCover && (board.State == AiState.SeekCover || board.State == AiState.Reload))
                return board.LastSeenEnemy ?? board.CoverThreat;
            if (board.State == AiState.SeekCover || board.State == AiState.Reload) return null; // run to cover facing the route
            // Something just hit or walked up on the bot and nothing has been seen since: face it first.
            if (board.LastHeard.HasValue && (board.Heard == HeardClass.Damage || board.Heard == HeardClass.Footsteps) &&
                unchecked(tick - board.LastHeardTick) <= AiTuning.CloseThreatTicks &&
                (board.LastSeenTick == 0 || board.LastHeardTick > board.LastSeenTick))
                return board.LastHeard;
            if (board.State == AiState.Engage && board.LastSeenEnemy.HasValue &&
                unchecked(tick - board.LastSeenTick) <= AiTuning.CombatMemoryTicks)
                return board.LastSeenEnemy;
            if (board.LastHeard.HasValue && unchecked(tick - board.LastHeardTick) <= AiTuning.ThreatLookTicks)
            {
                if (board.Heard == HeardClass.Damage) return board.LastHeard;
                if (Within(board.Feet, board.LastHeard.Value, AiTuning.ThreatLookRange)) return board.LastHeard;
            }
            if (board.LastSeenEnemy.HasValue && unchecked(tick - board.LastSeenTick) <= AiTuning.ThreatLookTicks * 2 &&
                Within(board.Feet, board.LastSeenEnemy.Value, AiTuning.ThreatLookRange))
                return board.LastSeenEnemy;
            return null;
        }

        private static bool Within(Float3 a, Float3 b, float meters)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz <= meters * meters;
        }

        // ---------------------------------------------------------------- command

        private InputCommand BuildCommand(AiBlackboard board, uint simulationTick, uint sequence)
        {
            var buttons = InputButtons.None;
            var eye = new Float3(board.Feet.X, board.Feet.Y + EyeHeight(board), board.Feet.Z);
            bool visible = board.HasVisibleTarget && board.VisibleTarget.HasValue;
            bool engaging = board.State == AiState.Engage && visible;
            Float3? movementTarget = board.PathCornerIndex < board.PathCornerCount
                ? board.PathCorners[board.PathCornerIndex]
                : (Float3?)null;

            // 1. Where to look.
            float desiredYaw = board.Yaw, desiredPitch = board.Pitch;
            bool combatTurn = false;
            Float3? threatLook = null;
            // Holds while the old target drops out of the vision cone, until the attacker is acquired or time runs out.
            bool turnToThreat = tick < board.TurnToThreatUntilTick && board.LastHeard.HasValue &&
                board.UnseenAttacker.HasValue && board.TargetId != board.UnseenAttacker;
            if (turnToThreat)
            {
                // Being shot from elsewhere: break off the current target and turn toward the attacker.
                var look = board.LastHeard!.Value;
                var lookPoint = new Float3(look.X, Math.Max(look.Y, board.Feet.Y + StandingAimHeight), look.Z);
                AimController.LookAngles(eye, lookPoint, out desiredYaw, out desiredPitch);
                combatTurn = true;
                threatLook = look;
                visible = false;
                engaging = false;
            }
            else if (visible)
            {
                AimController.IdealAngles(eye, board.VisibleTarget!.Value, out float iy, out float ip);
                AimController.ErrorOffset(board, tick, board.TargetDistance, board.TargetSpeed, out float ey, out float ep);
                desiredYaw = AimController.NormalizeYaw(iy + ey);
                desiredPitch = AimController.ClampPitch(ip + ep);
                if (board.DesiredSlot == 3)
                {
                    // ADR-111: lob the grenade on the ballistic arc instead of throwing along the sight line.
                    var grenade = Combat.WeaponDefinition.FromId(new DefinitionId("weapons/grenade"));
                    desiredPitch = AimController.ClampPitch(ep + AimController.GrenadeLaunchPitch(
                        eye, board.VisibleTarget!.Value, grenade.ThrowSpeedMetersPerSecond,
                        Combat.GrenadeSimulator.GravityY, Combat.GrenadeSimulator.LoftDegrees));
                }
                combatTurn = true;
            }
            else if ((threatLook = ThreatLook(board)).HasValue)
            {
                var look = threatLook.Value;
                // Stimulus points are feet-ish or muzzle height; look at standing chest height.
                var lookPoint = new Float3(look.X, Math.Max(look.Y, board.Feet.Y + StandingAimHeight), look.Z);
                AimController.LookAngles(eye, lookPoint, out desiredYaw, out desiredPitch);
                combatTurn = true;
            }
            else if (movementTarget.HasValue)
            {
                float dx = movementTarget.Value.X - board.Feet.X, dz = movementTarget.Value.Z - board.Feet.Z;
                if (dx * dx + dz * dz > 0.04f)
                {
                    // Without a threat, look where we walk so the forward vision cone sweeps the route; ADR-112 looks
                    // into the next leg as the corner nears so the head turns before the feet.
                    RouteDirection(board, movementTarget.Value, out float lx, out float lz);
                    if (lx != 0 || lz != 0) desiredYaw = ContractYaw(lx, lz);
                    desiredPitch = 0;
                }
            }
            AimController.Turn(board, desiredYaw, desiredPitch, combatTurn);
            board.AimOffsetRadians = AimController.AngleBetween(board.Yaw, board.Pitch, desiredYaw, desiredPitch);

            // 2. Where to move (world XZ), then into command space relative to the new aim yaw.
            float wx = 0, wz = 0;
            bool crouch = false, holdStill = false;
            board.StrafeFlippedThisTick = false;
            var range = FireDiscipline.Classify(board.TargetDistance);
            bool crouchShooter = board.Self.Value % AiTuning.CrouchShooterModulo == 0;
            int ledgeFlipsBefore = counters.StrafeLedgeFlips;
            if (engaging && board.TargetDistance < AiTuning.AdvanceRange)
            {
                // ADR-118: a shotgun never camps at range; it keeps moving instead of settling into a crouch.
                bool closeGun = IsCloseRangeGun(board);
                holdStill = !closeGun && (range == EngageRange.Long ||
                    (range == EngageRange.Mid && crouchShooter && FireDiscipline.InBurst(board, tick)));
                if (!holdStill) EngageMove(board, out wx, out wz);
                crouch = !closeGun && (range == EngageRange.Long || (range == EngageRange.Mid && crouchShooter && FireDiscipline.InBurst(board, tick)));
            }
            else if (movementTarget.HasValue)
            {
                RouteDirection(board, movementTarget.Value, out wx, out wz);
            }
            // ADR-112: ease the world move vector (strafe reversals and corner turns ramp over a few ticks instead of
            // flipping in one); a ledge flip snaps so the bot never coasts toward a drop.
            SmoothMove(board, ref wx, ref wz, counters.StrafeLedgeFlips != ledgeFlipsBefore || holdStill);
            if (!engaging && board.InCover && (board.State == AiState.SeekCover || board.State == AiState.Reload))
            {
                // ADR-112: behind low authored cover a bot rises to peek and shoot, then ducks again.
                bool peeking = board.State == AiState.SeekCover && board.CoverLow &&
                    unchecked(tick - board.InCoverSinceTick) % AiTuning.CoverPeekCycleTicks >= AiTuning.CoverPeekCycleTicks - AiTuning.CoverPeekStandTicks;
                crouch = !peeking;
            }

            float mx = 0, my = 0;
            if (wx != 0 || wz != 0)
            {
                // Move relative to aim yaw: +Y forward, +X right in command space.
                float fx = MathF.Sin(board.Yaw), fz = MathF.Cos(board.Yaw);
                float rx = fz, rz = -fx;
                mx = wx * rx + wz * rz;
                my = wx * fx + wz * fz;
                float mlen = MathF.Sqrt(mx * mx + my * my);
                // The eased vector keeps its magnitude (a reversal passes through a slow-down), capped to the disk.
                if (mlen > 1f) { mx /= mlen; my /= mlen; }
                else if (mlen <= 1e-5f) { mx = 0; my = 0; }
                // Keep strictly inside the contract unit-disk tolerance (≤1.0000001).
                double disk = (double)mx * mx + (double)my * my;
                if (disk > 0.999999) { double s = Math.Sqrt(0.999999 / disk); mx = (float)(mx * s); my = (float)(my * s); }
            }
            bool moving = mx != 0 || my != 0;
            board.CommandedMove = moving;

            // 3. Sprint on a route with no visible enemy and no threat to watch, mostly forward.
            bool travelState = board.State == AiState.Patrol || board.State == AiState.Investigate || board.State == AiState.Search
                || board.State == AiState.Reposition || board.State == AiState.SeekCover || board.State == AiState.Reload;
            bool sprint = travelState && !visible && !threatLook.HasValue && moving && my >= SprintMinForward;

            // 4. Loadout: keep guns when they still have ammo; otherwise knife / one grenade (T087).
            UpdateLoadout(board, visible);
            byte slot = board.DesiredSlot;

            // 5. Trigger discipline: bursts only when aligned, never while sprinting or reloading.
            bool canFire = visible && tick >= board.ReactionReadyTick && board.State != AiState.Reload && !sprint;
            bool fire = false;
            if (slot == 3)
            {
                // Cook briefly then release; one grenade per life.
                if (board.GrenadeRemaining > 0 && board.MagAmmo > 0 && visible
                    && board.TargetDistance >= AiTuning.GrenadeMinMeters
                    && board.TargetDistance <= AiTuning.GrenadeMaxMeters)
                {
                    if (!board.CookingGrenade)
                    {
                        board.CookingGrenade = true;
                        board.GrenadeCookStartTick = tick;
                        buttons |= InputButtons.FirePressed | InputButtons.FireHeld;
                        fire = true;
                    }
                    else if (unchecked(tick - board.GrenadeCookStartTick) < AiTuning.GrenadeCookTicks)
                    {
                        buttons |= InputButtons.FireHeld;
                        fire = true;
                    }
                    else
                    {
                        // Release this tick to throw; mark spent so we don't re-cook an empty slot.
                        board.CookingGrenade = false;
                        board.GrenadeRemaining = 0;
                        fire = true;
                    }
                }
                else
                {
                    board.CookingGrenade = false;
                    FireDiscipline.Cease(board, tick);
                }
            }
            else if (slot == 2)
            {
                // Knife: press-fire when inside melee range (ammo never depletes).
                if (canFire && board.TargetDistance <= AiTuning.KnifeEngageMeters)
                {
                    float tol = AimController.FireTolerance(board.TargetDistance);
                    fire = board.AimOffsetRadians <= tol * 2f;
                    if (fire)
                    {
                        buttons |= InputButtons.FirePressed | InputButtons.FireHeld;
                        counters.ShotsRequested++;
                    }
                }
                else FireDiscipline.Cease(board, tick);
            }
            else if (canFire && board.MagAmmo > 0 && !OutOfShotgunRange(board))
            {
                float tol = AimController.FireTolerance(board.TargetDistance);
                uint interval = ActiveGunFireInterval(board);
                // Precision guns only fire on settled aim; there is no follow-up spray to correct a miss.
                bool precision = IsPrecisionGun(board);
                fire = FireDiscipline.Update(board, tick, board.TargetDistance,
                    board.AimOffsetRadians <= (precision ? tol * 0.6f : tol), board.AimOffsetRadians <= (precision ? tol : tol * 2.5f), interval);
                if (fire && board.BurstStartTick == tick) counters.BurstsStarted++;
                if (fire)
                {
                    buttons |= InputButtons.FireHeld | InputButtons.FirePressed;
                    counters.ShotsRequested++;
                }
            }
            else FireDiscipline.Cease(board, tick);

            if (board.State == AiState.Reload && slot < 2) buttons |= InputButtons.ReloadPressed;
            if (engaging && holdStill && range == EngageRange.Long && slot < 2) buttons |= InputButtons.AimHeld;
            if (engaging && holdStill && range != EngageRange.Close && IsPrecisionGun(board)) buttons |= InputButtons.AimHeld;

            board.SlideIntent = false;
            if (sprint)
            {
                buttons |= InputButtons.SprintHeld;
                board.SprintTicks++;
            }
            else board.SprintTicks = 0;

            if (crouch) buttons |= InputButtons.CrouchHeld;

            // 6. Jump (edge) when stuck: only on the tick UpdateStuck scheduled it.
            bool firingOrReloading = (buttons & (InputButtons.FireHeld | InputButtons.FirePressed | InputButtons.ReloadPressed)) != 0;
            if (board.JumpPending)
            {
                if (!engaging && !firingOrReloading) buttons |= InputButtons.JumpPressed;
                board.JumpPending = false;
            }
            // ADR-100 traversal hop: a low obstacle (above the motor step, below jump clearance) right ahead on the route.
            if (!engaging && !firingOrReloading && moving && !crouch && tick >= board.NextJumpTick
                && (buttons & InputButtons.JumpPressed) == 0
                && unchecked(tick + (uint)board.Self.Value) % AiTuning.VaultProbeIntervalTicks == 0
                && LowObstacleAhead(board, wx, wz))
            {
                buttons |= InputButtons.JumpPressed;
                board.NextJumpTick = tick + JumpCooldownTicks;
                counters.TraversalJumps++;
            }
            // ADR-100 combat hop: sometimes hop as the strafe reverses, never mid-burst, reloading or crouch-shooting.
            if (engaging && board.StrafeFlippedThisTick && !holdStill && !crouch && !firingOrReloading
                && range != EngageRange.Long && !FireDiscipline.InBurst(board, tick)
                && tick >= board.NextHopTick && tick >= board.NextJumpTick
                && AiNoise.Unit(board.Self.Value, tick, AiNoise.SaltHop) < AiTuning.CombatHopChance)
            {
                buttons |= InputButtons.JumpPressed;
                board.NextHopTick = tick + AiTuning.CombatHopCooldownTicks;
                board.NextJumpTick = tick + JumpCooldownTicks;
                counters.CombatHops++;
            }

            // 7. Occasional slide when sprinting through open travel with a long straight leg left (ADR-100 widened).
            if (sprint && (buttons & InputButtons.JumpPressed) == 0
                && (board.State == AiState.SeekCover || board.State == AiState.Reposition
                    || board.State == AiState.Investigate || board.State == AiState.Search)
                && board.SprintTicks >= SlideMinSprintTicks
                && board.SpeedMps >= SlideRequestSpeed
                && tick >= board.NextSlideTick
                && StraightMetersToCorner(board) >= SlideMinStraightMeters)
            {
                board.SlideIntent = true;
                board.NextSlideTick = tick + SlideBotCooldownBaseTicks + (uint)(board.Self.Value % 5) * SlideBotCooldownSpreadTicks;
            }
            return new InputCommand(board.Self, simulationTick, sequence, mx, my, board.Yaw, board.Pitch, buttons, slot);
        }

        /// <summary>The gun in the commanded slot: the life's own primary or sidearm (ADR-102).</summary>
        private static Combat.WeaponDefinition ActiveGun(AiBlackboard board) =>
            Combat.WeaponCatalog.TryGet(board.DesiredSlot == 1 ? board.SidearmWeapon : board.PrimaryWeapon, out var entry)
                ? entry.Definition
                : Combat.WeaponCatalog.RequireDefinition(board.DesiredSlot == 1 ? Combat.WeaponLoadoutPolicy.PistolId : Combat.WeaponLoadoutPolicy.DefaultPrimaryId);

        private static int ActiveMagazineSize(AiBlackboard board) => ActiveGun(board).MagazineSize;

        private static int TacticalReloadThreshold(AiBlackboard board) =>
            Math.Max(1, ActiveMagazineSize(board) * 2 / 5);

        private static uint ActiveGunFireInterval(AiBlackboard board)
        {
            var gun = ActiveGun(board);
            uint interval = gun.FireMode == Combat.FireMode.Burst
                ? gun.BurstIntervalTicks * (uint)Math.Max(0, gun.BurstCount - 1) + gun.FireIntervalTicks
                : gun.FireIntervalTicks;
            return interval == 0 ? 1u : interval;
        }

        /// <summary>ADR-118 multi-pellet primary (the M870): only worth firing inside its falloff range.</summary>
        private static bool IsCloseRangeGun(AiBlackboard board) => board.DesiredSlot == 0 && ActiveGun(board).PelletCount > 1;

        /// <summary>True when a shotgun-armed bot's target is past the end of the pellet falloff (no point firing).</summary>
        private static bool OutOfShotgunRange(AiBlackboard board)
        {
            if (!IsCloseRangeGun(board)) return false;
            var gun = ActiveGun(board);
            float reach = gun.FalloffEndMeters > 0f ? gun.FalloffEndMeters : gun.MaxRangeMeters;
            return board.TargetDistance > reach;
        }

        /// <summary>Marksman and sniper guns: bots aim down sights from medium range and wait for settled aim.</summary>
        private static bool IsPrecisionGun(AiBlackboard board)
        {
            var gun = ActiveGun(board);
            return gun.FireMode == Combat.FireMode.SemiAutomatic && board.DesiredSlot == 0 && gun.AdsSpreadRadians < 0.1f * (float)Math.PI / 180f;
        }

        private static void UpdateLoadout(AiBlackboard board, bool visible)
        {
            if (board.DesiredSlot == 0 && board.MagAmmo <= 0 && board.ReserveAmmo <= 0)
                board.RifleEmpty = true;
            if (board.DesiredSlot == 1 && board.MagAmmo <= 0 && board.ReserveAmmo <= 0)
                board.PistolEmpty = true;

            bool gunsDry = board.RifleEmpty && board.PistolEmpty;
            if (!gunsDry)
            {
                if (board.DesiredSlot == 0 && board.RifleEmpty) board.DesiredSlot = 1;
                else if (board.DesiredSlot > 1) board.DesiredSlot = board.RifleEmpty ? (byte)1 : (byte)0;
                return;
            }

            // Guns empty: knife up close, otherwise spend the single grenade once if the geometry is right.
            if (visible && board.TargetDistance <= AiTuning.KnifeEngageMeters)
            {
                board.DesiredSlot = 2;
                board.CookingGrenade = false;
                return;
            }
            if (board.GrenadeRemaining > 0 && visible
                && board.TargetDistance >= AiTuning.GrenadeMinMeters
                && board.TargetDistance <= AiTuning.GrenadeMaxMeters)
            {
                board.DesiredSlot = 3;
                return;
            }
            board.DesiredSlot = 2;
            board.CookingGrenade = false;
        }

        /// <summary>
        /// ADR-112: unit world direction along the route. Inside <see cref="AiTuning.CornerBlendMeters"/> of the
        /// current corner it bends toward the following leg, so corners become short arcs instead of hard turns.
        /// ADR-114: the bend is taken only when the bent line to the blended point is clear of collision for the
        /// bot's body width; otherwise the bot follows the leg to the corner (never shaves a wall corner).
        /// </summary>
        private void RouteDirection(AiBlackboard board, Float3 corner, out float wx, out float wz)
        {
            wx = 0; wz = 0;
            float dx = corner.X - board.Feet.X, dz = corner.Z - board.Feet.Z;
            float len = MathF.Sqrt(dx * dx + dz * dz);
            if (len <= 0.2f) return;
            wx = dx / len; wz = dz / len;
            int next = board.PathCornerIndex + 1;
            if (len >= AiTuning.CornerBlendMeters || next >= board.PathCornerCount) return;
            var after = board.PathCorners[next];
            float nx = after.X - corner.X, nz = after.Z - corner.Z;
            float nlen = MathF.Sqrt(nx * nx + nz * nz);
            if (nlen < 0.05f) return;
            nx /= nlen; nz /= nlen;
            float t = 0.5f * (1f - len / AiTuning.CornerBlendMeters);
            float bx = wx + (nx - wx) * t, bz = wz + (nz - wz) * t;
            float blen = MathF.Sqrt(bx * bx + bz * bz);
            if (blen <= 1e-4f) return;
            bx /= blen; bz /= blen;
            if (!CornerBendClear(board, bx, bz, len)) return;
            wx = bx; wz = bz;
        }

        // One clearance answer per bot, tick and corner: RouteDirection runs for the look and again for the move.
        private ulong bendCacheSelf;
        private uint bendCacheTick;
        private int bendCacheCorner = -1;
        private bool bendCacheClear;

        /// <summary>
        /// ADR-114: three horizontal collision rays (centre and both body edges, at knee height above the step) from
        /// the feet along the bent direction, as far as the blended point plus the body radius.
        /// </summary>
        private bool CornerBendClear(AiBlackboard board, float bx, float bz, float len)
        {
            if (bendCacheTick == tick && bendCacheSelf == board.Self.Value && bendCacheCorner == board.PathCornerIndex)
                return bendCacheClear;
            var filter = new QueryFilter(visionMask, board.Self, false);
            var dir = new Float3(bx, 0f, bz);
            float y = board.Feet.Y + AiTuning.CornerBendProbeHeightMeters;
            float r = AiTuning.CornerBendProbeRadiusMeters;
            float reach = len + r;
            bool clear = true;
            for (int side = -1; side <= 1 && clear; side++)
            {
                // Perpendicular offset in XZ: (bz, -bx) is the right-hand side of the bent direction.
                var origin = new Float3(board.Feet.X + bz * r * side, y, board.Feet.Z - bx * r * side);
                clear = world.Raycast(origin, dir, reach, filter).Status != QueryStatus.Hit;
            }
            bendCacheTick = tick; bendCacheSelf = board.Self.Value; bendCacheCorner = board.PathCornerIndex; bendCacheClear = clear;
            return clear;
        }

        private static void SmoothMove(AiBlackboard board, ref float wx, ref float wz, bool snap)
        {
            // Starting and stopping are left to the motor's own acceleration; only a change of direction between two
            // moving ticks is eased (strafe reversals pass through a short slow-down, corners bend).
            bool fromStop = board.SmoothMoveX * board.SmoothMoveX + board.SmoothMoveZ * board.SmoothMoveZ < 0.0025f;
            if (snap || fromStop || (wx == 0 && wz == 0))
            {
                board.SmoothMoveX = wx; board.SmoothMoveZ = wz;
                return;
            }
            float a = AiTuning.MoveEaseAlpha;
            float sx = board.SmoothMoveX + (wx - board.SmoothMoveX) * a;
            float sz = board.SmoothMoveZ + (wz - board.SmoothMoveZ) * a;
            board.SmoothMoveX = sx; board.SmoothMoveZ = sz;
            wx = sx; wz = sz;
        }

        /// <summary>
        /// Strafe across the line of fire around the nav point where the fight started (bounded offset keeps the
        /// bot near walkable ground), reversing on a seeded timer, at the leash, when blocked, or when the next
        /// strafe step lacks footing (T090 ledge probe); back off when the enemy is too close.
        /// </summary>
        private void EngageMove(AiBlackboard board, out float wx, out float wz)
        {
            wx = 0; wz = 0;
            var target = board.VisibleTarget!.Value;
            float tx = target.X - board.Feet.X, tz = target.Z - board.Feet.Z;
            float tlen = MathF.Sqrt(tx * tx + tz * tz);
            if (tlen < 1e-3f) return;
            tx /= tlen; tz /= tlen;
            float rx = tz, rz = -tx; // right of the line of fire
            if (!board.HasStrafeCenter)
            {
                board.HasStrafeCenter = true;
                board.StrafeCenter = board.Feet;
                board.StrafeCheckFeet = board.Feet;
                board.StrafeCheckTick = tick;
                board.StrafeFlipTick = tick + NextStrafeInterval(board);
            }
            bool flip = tick >= board.StrafeFlipTick;
            if (unchecked(tick - board.StrafeCheckTick) >= AiTuning.StrafeBlockedCheckTicks)
            {
                float bx = board.Feet.X - board.StrafeCheckFeet.X, bz = board.Feet.Z - board.StrafeCheckFeet.Z;
                if (bx * bx + bz * bz < AiTuning.StrafeBlockedMeters * AiTuning.StrafeBlockedMeters) flip = true;
                board.StrafeCheckFeet = board.Feet;
                board.StrafeCheckTick = tick;
            }
            float offset = (board.Feet.X - board.StrafeCenter.X) * rx + (board.Feet.Z - board.StrafeCenter.Z) * rz;
            if (offset * board.StrafeDir > AiTuning.StrafeMaxOffset) flip = true;
            if (!StrafeFootingSafe(board, rx, rz, board.StrafeDir))
            {
                flip = true;
                counters.StrafeLedgeFlips++;
            }
            if (flip)
            {
                board.StrafeDir = -board.StrafeDir;
                board.StrafeFlipTick = tick + NextStrafeInterval(board);
                board.StrafeFlippedThisTick = true;
                // If the other side is also unsafe, cancel lateral strafe this tick (back-off may still apply).
                if (!StrafeFootingSafe(board, rx, rz, board.StrafeDir))
                {
                    board.StrafeFlippedThisTick = false;
                    wx = 0; wz = 0;
                    if (board.TargetDistance < AiTuning.BackOffRange)
                    {
                        wx = -tx; wz = -tz;
                    }
                    return;
                }
            }
            wx = rx * board.StrafeDir;
            wz = rz * board.StrafeDir;
            if (board.TargetDistance < AiTuning.BackOffRange)
            {
                wx -= tx; wz -= tz;
                float len = MathF.Sqrt(wx * wx + wz * wz);
                wx /= len; wz /= len;
            }
        }

        /// <summary>
        /// T090: sample ground under the next strafe step with a downward collision ray (not the vision budget).
        /// Open/diagnostic worlds with no ground under the bot or the probe fail-open as safe.
        /// </summary>
        private bool StrafeFootingSafe(AiBlackboard board, float rx, float rz, int dir)
        {
            float step = AiTuning.StrafeProbeMeters;
            var here = board.Feet;
            var probe = new Float3(here.X + rx * dir * step, here.Y, here.Z + rz * dir * step);
            bool hereHit = TryGroundY(here, board.Self, out float hereY);
            bool probeHit = TryGroundY(probe, board.Self, out float probeY);
            if (!hereHit && !probeHit) return true;
            if (!probeHit) return false;
            if (!hereHit) return probeY >= here.Y - AiTuning.StrafeMaxDropMeters;
            return hereY - probeY <= AiTuning.StrafeMaxDropMeters;
        }

        private bool TryGroundY(Float3 feet, EntityId self, out float groundY)
        {
            var origin = new Float3(feet.X, feet.Y + AiTuning.StrafeGroundLiftMeters, feet.Z);
            float max = AiTuning.StrafeMaxDropMeters + AiTuning.StrafeGroundLiftMeters + 0.5f;
            var hit = world.Raycast(origin, new Float3(0f, -1f, 0f), max, new QueryFilter(visionMask, self, false));
            if (hit.Status != QueryStatus.Hit)
            {
                groundY = feet.Y;
                return false;
            }
            groundY = hit.Hit.Point.Y;
            return true;
        }

        /// <summary>
        /// ADR-100: two horizontal collision rays along the move direction. Hits at knee height (above the motor step)
        /// with clear space at hop clearance height mean a low obstacle the motor can only cross with a jump.
        /// </summary>
        private bool LowObstacleAhead(AiBlackboard board, float wx, float wz)
        {
            float len = MathF.Sqrt(wx * wx + wz * wz);
            if (len < 1e-4f) return false;
            var dir = new Float3(wx / len, 0f, wz / len);
            var filter = new QueryFilter(visionMask, board.Self, false);
            var feet = board.Feet;
            var knee = world.Raycast(new Float3(feet.X, feet.Y + AiTuning.VaultProbeHeightMeters, feet.Z), dir, AiTuning.VaultProbeMeters, filter);
            if (knee.Status != QueryStatus.Hit) return false;
            var clear = world.Raycast(new Float3(feet.X, feet.Y + AiTuning.VaultClearHeightMeters, feet.Z), dir, AiTuning.VaultProbeMeters + 0.3f, filter);
            return clear.Status != QueryStatus.Hit;
        }

        private uint NextStrafeInterval(AiBlackboard board) =>
            AiTuning.StrafeFlipMinTicks + AiNoise.Range(board.Self.Value, tick, AiNoise.SaltStrafe, AiTuning.StrafeFlipSpreadTicks);
    }
}
