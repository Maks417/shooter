#nullable enable
using Shooter.Contracts;

namespace Shooter.Simulation.AI
{
    public enum AiState : byte
    {
        Idle = 0,
        Patrol = 1,
        Investigate = 2,
        Engage = 3,
        SeekCover = 4,
        Reload = 5,
        Reposition = 6,
        Search = 7,
        Dead = 8
    }

    public sealed class AiBlackboard
    {
        public EntityId Self { get; set; }
        public byte Team { get; set; }
        public AiState State { get; set; } = AiState.Patrol;
        public Float3 Feet { get; set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public ushort Health { get; set; } = 100;
        public int MagAmmo { get; set; } = 30;
        public int ReserveAmmo { get; set; } = 90;
        public bool Alive { get; set; } = true;
        public EntityId? VisibleTargetId { get; set; }
        public Float3? LastSeenEnemy { get; set; }
        public uint LastSeenTick { get; set; }
        public float Confidence { get; set; }
        public Float3? LastHeard { get; set; }
        public uint LastHeardTick { get; set; }
        public ulong? CoverReservation { get; set; }
        public PathTicket? PathTicket { get; set; }
        public uint PathRequestedTick { get; set; }
        public Float3? PathGoal { get; set; }
        public Float3[] PathCorners { get; } = new Float3[ContractVersion.MaxPathCorners];
        public int PathCornerCount { get; set; }
        public int PathCornerIndex { get; set; }
        public uint StuckTicks { get; set; }
        public Float3 StuckAnchor { get; set; }
        public int StuckFailures { get; set; }
        public uint LastReplanTick { get; set; }
        public uint SenseAgeTicks { get; set; }
        public bool HasVisibleTarget { get; set; }
        public Float3? VisibleTarget { get; set; }
        public uint ReactionReadyTick { get; set; }
        public uint StateEnteredTick { get; set; }
        public uint LastSenseOpportunityTick { get; set; }
        /// <summary>Advances on every arrival or failed path so patrol/search rotate through map destinations.</summary>
        public int DestinationCursor { get; set; }
        /// <summary>No new path request before this tick after a failed/timed-out request (no per-tick retry storm).</summary>
        public uint RetryAfterTick { get; set; }

        // Movement-ability state (sprint/crouch/jump/slide). Deterministic, derived only from authority feedback.
        /// <summary>Actual crouched stance reported by authority (eye 1.05 m instead of 1.65 m).</summary>
        public bool Crouched { get; set; }
        public Float3 PrevFeet { get; set; }
        public bool HasPrevFeet { get; set; }
        /// <summary>Horizontal speed estimated from consecutive planned feet positions (m/s).</summary>
        public float SpeedMps { get; set; }
        /// <summary>Consecutive ticks SprintHeld was emitted (motor needs previous SprintActive to slide).</summary>
        public uint SprintTicks { get; set; }
        public bool JumpPending { get; set; }
        public uint NextJumpTick { get; set; }
        public uint NextSlideTick { get; set; }
        /// <summary>Set by BuildCommand for the tick being planned; consumed by the session as the slide request.</summary>
        public bool SlideIntent { get; set; }

        // T082 combat behaviour. All reset per life; deterministic from authority feedback, entity id and tick.
        /// <summary>Sticky current enemy; survives short visibility gaps (unlike per-tick <see cref="VisibleTargetId"/>).</summary>
        public EntityId? TargetId { get; set; }
        public uint AcquiredTick { get; set; }
        public float TargetDistance { get; set; }
        public ushort TargetHealth { get; set; } = 100;
        public Float3 TargetVelocity { get; set; }
        public float TargetSpeed { get; set; }
        public float YawRate { get; set; }
        public float PitchRate { get; set; }
        /// <summary>Angle between current aim and the aim the bot is trying to hold (debug/tests).</summary>
        public float AimOffsetRadians { get; set; }
        public uint BurstStartTick { get; set; }
        public uint BurstEndTick { get; set; }
        public uint BurstPauseUntilTick { get; set; }
        public int BurstIndex { get; set; }
        public int StrafeDir { get; set; } = 1;
        public uint StrafeFlipTick { get; set; }
        public Float3 StrafeCenter { get; set; }
        public bool HasStrafeCenter { get; set; }
        public Float3 StrafeCheckFeet { get; set; }
        public uint StrafeCheckTick { get; set; }
        public uint FlinchUntilTick { get; set; }
        public uint LastDamagedTick { get; set; }
        public bool WasDamaged { get; set; }
        public EntityId? LastAttacker { get; set; }
        /// <summary>Source class of <see cref="LastHeard"/> (ai.md hearing stimulus).</summary>
        public HeardClass Heard { get; set; }
        public uint RetreatAllowedTick { get; set; }
        public int ReloadStartMag { get; set; }
        public bool CoverProbeActive { get; set; }
        public bool CoverProbeFailed { get; set; }
        public int CoverProbeIndex { get; set; }
        public Float3 CoverProbeOrigin { get; set; }
        public Float3 CoverThreat { get; set; }
        public Float3? CoverPoint { get; set; }
        public uint InCoverSinceTick { get; set; }
        public bool InCover { get; set; }
    }

    public enum HeardClass : byte { None = 0, Gunfire = 1, Damage = 2, Callout = 3 }

    public sealed class AiCounters
    {
        public int VisionRaysThisTick;
        public int PathsRequestedThisTick;
        public int StateChanges;
        public int ShotsRequested;
        public int CoverClaims;
        public int CoverReleases;
        public int OrphanReleases;
        public int StuckEvents;
        public int DeferredSenses;
        public int MaxPathQueue;
        public int SenseOpportunitiesThisTick;
        public int AlliedCandidatesSkipped;
        public int PathsCompleted;
        public int PathsPartial;
        public int PathsFailed;
        // T082
        public int CoverProbeRays;
        public int CoverProbesFailed;
        public int BurstsStarted;
        public int StimuliHeard;
        public int DamageReactions;
        public int Callouts;
        public int Retreats;
        public int TacticalReloads;
        public int TargetSwitches;
    }
}
