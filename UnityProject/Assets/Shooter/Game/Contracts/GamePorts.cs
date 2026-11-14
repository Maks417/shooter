#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Contracts;

namespace Shooter.Game.Contracts
{
    public interface IGameSession : IDisposable
    {
        ulong AuthoritySessionId { get; }
        GameMatchPhase Phase { get; }
        SessionPauseState PauseState { get; }
        GameOperationResult Validate(SessionRequest request);
        Task<GameOperationResult> BeginLoadAsync(SessionRequest request, CancellationToken cancel);
        GameOperationResult SignalReady();
        GameOperationResult Start();
        GameOperationResult Pause();
        GameOperationResult Resume();
        GameOperationResult RequestAbort();
        MatchViewSnapshot? TryAcquireSnapshot();
        MatchResults? TryAcquireResults();
        GameOperationResult Stop();
    }

    public interface IActorLifecycle
    {
        EntityId SpawnLife(ParticipantId participant, LifeId life, Float3 feet, float yaw, byte team);
        void DespawnLife(EntityId entity);
        void ResetLife(EntityId entity, Float3 feet, float yaw);
        bool TryGetActorState(EntityId entity, out ActorLifecycleState state);
        int ActiveActorCount { get; }
    }

    public readonly struct ActorLifecycleState
    {
        public EntityId EntityId { get; }
        public byte Team { get; }
        public bool Alive { get; }
        public ushort Health { get; }
        public Float3 Feet { get; }
        public float AimYaw { get; }
        public float AimPitch { get; }
        public byte ActiveWeaponSlot { get; }
        public ushort Magazine { get; }
        public ushort Reserve { get; }

        public ActorLifecycleState(
            EntityId entityId,
            byte team,
            bool alive,
            ushort health,
            Float3 feet,
            float aimYaw,
            float aimPitch,
            byte activeWeaponSlot,
            ushort magazine,
            ushort reserve)
        {
            EntityId = entityId;
            Team = team;
            Alive = alive;
            Health = health;
            Feet = feet;
            AimYaw = aimYaw;
            AimPitch = aimPitch;
            ActiveWeaponSlot = activeWeaponSlot;
            Magazine = magazine;
            Reserve = reserve;
        }
    }

    public interface ITeamMatch
    {
        void Begin(SessionRequest request, IReadOnlyList<ParticipantState> roster, MapGameplayDefinition map);
        void ProcessDeath(DeathResolution death);
        void EvaluateEndOfTick(uint completedSimulationTick, bool advanceActiveClock);
        void ScheduleDueRespawns(uint activeTicksElapsed, IActorLifecycle lifecycle, IMapRuntime map);
        MatchViewSnapshot AcquireSnapshot(ulong authoritySessionId, uint completedSimulationTick);
        MatchResults? TryAcquireResults();
    }

    public interface ITeamBotPlanner
    {
        int Plan(uint nextTick, MatchViewSnapshot match, IActorLifecycle lifecycle, CommandBuffer destination);
        void ResetLife(EntityId entity);
        void CancelAll();
    }

    public interface IMapRuntime
    {
        Task<GameOperationResult> LoadAsync(MapGameplayDefinition definition, CancellationToken cancel);
        IWorldQueries WorldQueries { get; }
        INavigation Navigation { get; }
        bool TryValidateSpawn(SpawnSlot slot, out string diagnostic);
        GameOperationResult Unload();
    }

    public interface IMapCatalog
    {
        int Count { get; }
        bool TryGet(MapId mapId, out MapCatalogEntry entry);
        bool TryGetGameplay(MapId mapId, out MapGameplayDefinition gameplay);
        GameOperationResult RejectUnknown(MapId mapId, ModeId modeId, byte teamSize);
    }

    public interface IUserSettingsStore
    {
        GameOperationResult Load(out UserSettings settings);
        GameOperationResult Save(UserSettings settings);
    }

    /// <summary>
    /// Device adapter for applied preferences. Presentation/audio owners bind real devices;
    /// pure tests use MemoryClientSettingsApplier. Cannot mutate match/world gameplay rules.
    /// </summary>
    public interface IClientSettingsApplier
    {
        GameOperationResult Apply(UserSettings settings);
        GameOperationResult StageDisplayChange(UserSettings candidate);
        GameOperationResult ConfirmDisplayChange();
        GameOperationResult RevertDisplayChange();
        bool HasPendingDisplayChange { get; }
        UserSettings? PendingDisplaySettings { get; }
        UserSettings? AppliedSettings { get; }
    }
}
