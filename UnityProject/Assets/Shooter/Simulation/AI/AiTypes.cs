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
        public AiState State { get; set; } = AiState.Patrol;
        public Float3 Feet { get; set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public ushort Health { get; set; } = 100;
        public int MagAmmo { get; set; } = 30;
        public bool Alive { get; set; } = true;
        public Float3? LastSeenEnemy { get; set; }
        public uint LastSeenTick { get; set; }
        public float Confidence { get; set; }
        public Float3? LastHeard { get; set; }
        public uint LastHeardTick { get; set; }
        public ulong? CoverReservation { get; set; }
        public PathTicket? PathTicket { get; set; }
        public uint PathRequestedTick { get; set; }
        public Float3? PathGoal { get; set; }
        public uint StuckTicks { get; set; }
        public Float3 StuckAnchor { get; set; }
        public int StuckFailures { get; set; }
        public uint LastReplanTick { get; set; }
        public uint SenseAgeTicks { get; set; }
        public bool HasVisibleTarget { get; set; }
        public Float3? VisibleTarget { get; set; }
        public uint ReactionReadyTick { get; set; }
        public uint StateEnteredTick { get; set; }
    }

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
    }
}
