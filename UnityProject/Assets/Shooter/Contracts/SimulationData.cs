#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Contracts
{
    public readonly struct WeaponState
    {
        public DefinitionId DefinitionId { get; }
        public WeaponAction Action { get; }
        public ushort AmmoInMagazine { get; }
        public ushort Reserve { get; }
        public uint NextAllowedFireTick { get; }
        public uint PhaseStartTick { get; }
        public uint PhaseEndTick { get; }
        public uint ShotIndex { get; }
        public bool PreviousFireHeld { get; }
        public bool IsValid => DefinitionId.IsValid;
        public WeaponState(DefinitionId definitionId, WeaponAction action, ushort ammoInMagazine, ushort reserve, uint nextAllowedFireTick, uint phaseStartTick, uint phaseEndTick, uint shotIndex, bool previousFireHeld)
        {
            Require.That(definitionId.IsValid, nameof(definitionId)); Require.Enum(action, nameof(action));
            Serial32.ForwardDistance(phaseEndTick, phaseStartTick);
            DefinitionId = definitionId; Action = action; AmmoInMagazine = ammoInMagazine; Reserve = reserve;
            NextAllowedFireTick = nextAllowedFireTick; PhaseStartTick = phaseStartTick; PhaseEndTick = phaseEndTick; ShotIndex = shotIndex; PreviousFireHeld = previousFireHeld;
        }
    }
    public sealed class EntitySnapshot
    {
        public EntityId Id { get; }
        public Float3 Position { get; }
        public float Yaw { get; }
        public Float3 Velocity { get; }
        public Stance Stance { get; }
        public ushort Health { get; }
        public byte Team { get; }
        public byte ActiveSlot { get; }
        public WeaponState Weapon { get; }
        public float LocomotionDistanceMeters { get; }
        public float AimPitch { get; }
        public uint GameplayPoseVersion { get; }
        public EntitySnapshot(EntityId id, Float3 position, float yaw, Float3 velocity, Stance stance, ushort health, byte team, byte activeSlot, WeaponState weapon, float locomotionDistanceMeters, float aimPitch, uint gameplayPoseVersion)
        {
            Require.That(id.IsValid && weapon.IsValid && gameplayPoseVersion > 0, "snapshot identity");
            Require.Enum(stance, nameof(stance)); Require.Slot(activeSlot); Require.Aim(yaw, aimPitch);
            Require.That(Require.Finite(locomotionDistanceMeters, nameof(locomotionDistanceMeters)) >= 0, nameof(locomotionDistanceMeters));
            Require.That((stance == Stance.Dead) == (health == 0), "health/stance");
            Id = id; Position = position; Yaw = yaw; Velocity = velocity; Stance = stance; Health = health; Team = team; ActiveSlot = activeSlot;
            Weapon = weapon; LocomotionDistanceMeters = locomotionDistanceMeters; AimPitch = aimPitch; GameplayPoseVersion = gameplayPoseVersion;
        }
    }
    public readonly struct HitVolume
    {
        public EntityId EntityId { get; }
        public ushort RegionId { get; }
        public Capsule Capsule { get; }
        public uint Tick { get; }
        public HitVolume(EntityId entityId, ushort regionId, Capsule capsule, uint tick)
        {
            Require.That(entityId.IsValid && regionId >= 1 && regionId <= 6 && capsule.IsValid, "hit volume");
            EntityId = entityId; RegionId = regionId; Capsule = capsule; Tick = tick;
        }
    }
    public sealed class SimulationSnapshot
    {
        public uint Tick { get; }
        public uint WorldRevision { get; }
        public MatchState MatchState { get; }
        public EventId EventHighWater { get; }
        public FrozenList<EntitySnapshot> Entities { get; }
        public FrozenList<HitVolume> HitVolumes { get; }
        public SimulationSnapshot(uint tick, uint worldRevision, MatchState matchState, EventId eventHighWater, IReadOnlyList<EntitySnapshot> entities, IReadOnlyList<HitVolume> hitVolumes)
        {
            Require.Enum(matchState, nameof(matchState));
            if (!eventHighWater.IsEmpty) Require.That(!Serial32.IsNewer(eventHighWater.Tick, tick), nameof(eventHighWater));
            Entities = new FrozenList<EntitySnapshot>(entities, ContractVersion.MaxActors);
            HitVolumes = new FrozenList<HitVolume>(hitVolumes, ContractVersion.MaxActors * 6);
            ulong previous = 0; var actorIds = new HashSet<EntityId>();
            foreach (var entity in Entities)
            {
                Require.That(entity != null && entity.Id.Value > previous, "entity order/duplicates");
                previous = entity.Id.Value; actorIds.Add(entity.Id);
            }
            ulong previousActor = 0; ushort previousRegion = 0;
            foreach (var hit in HitVolumes)
            {
                Require.That(hit.EntityId.IsValid && hit.Capsule.IsValid && actorIds.Contains(hit.EntityId) && hit.Tick == tick && hit.RegionId >= 1 && hit.RegionId <= 6, "hit volume membership");
                Require.That(hit.EntityId.Value > previousActor || hit.EntityId.Value == previousActor && hit.RegionId > previousRegion, "hit volume order/duplicates");
                previousActor = hit.EntityId.Value; previousRegion = hit.RegionId;
            }
            Tick = tick; WorldRevision = worldRevision; MatchState = matchState; EventHighWater = eventHighWater;
        }
    }
    public readonly struct StepCounters
    {
        public int CommandsAccepted { get; }
        public int CommandsRejected { get; }
        public int Shots { get; }
        public int DamageEvents { get; }
        public int AiRays { get; }
        public int PathRequests { get; }
        public bool Overload { get; }
        public StepCounters(int commandsAccepted, int commandsRejected, int shots, int damageEvents, int aiRays, int pathRequests, bool overload)
        {
            Require.That(commandsAccepted >= 0 && commandsRejected >= 0 && (long)commandsAccepted + commandsRejected <= ContractVersion.MaxCommands && shots >= 0 && damageEvents >= 0 && aiRays >= 0 && pathRequests >= 0, "step counters");
            CommandsAccepted = commandsAccepted; CommandsRejected = commandsRejected; Shots = shots; DamageEvents = damageEvents; AiRays = aiRays; PathRequests = pathRequests; Overload = overload;
        }
    }
    public readonly struct StepResult
    {
        public uint Tick { get; }
        public ContentId StateDigest { get; }
        public int EventCount { get; }
        public StepCounters Counters { get; }
        public StepResult(uint tick, ContentId stateDigest, int eventCount, StepCounters counters)
        {
            Require.That(eventCount >= 0 && eventCount <= ContractVersion.MaxEvents, nameof(eventCount));
            Tick = tick; StateDigest = stateDigest; EventCount = eventCount; Counters = counters;
        }
    }

    /// <summary>Full restore state; weapon phase start/end ticks are the entire mechanical action phase.</summary>
    public sealed class PlayerPredictionState
    {
        public uint SchemaVersion { get; }
        public uint Tick { get; }
        public EntityId ActorId { get; }
        public uint WorldRevision { get; }
        public uint CollisionRevision { get; }
        public ContentId DefinitionHash { get; }
        public Float3 Position { get; }
        public Float3 Velocity { get; }
        public float AimYaw { get; }
        public float AimPitch { get; }
        public bool Grounded { get; }
        public SurfaceId GroundSurfaceId { get; }
        public Float3 GroundNormal { get; }
        public Stance Stance { get; }
        public float CapsuleHeight { get; }
        public uint LastGroundedTick { get; }
        public uint JumpConsumedSequence { get; }
        public byte ActiveSlot { get; }
        public byte DesiredSlot { get; }
        public FrozenList<WeaponState> WeaponSlots { get; }
        public float RecoilYaw { get; }
        public float RecoilPitch { get; }
        public float RecoilYawVelocity { get; }
        public float RecoilPitchVelocity { get; }
        public float LocomotionDistanceMeters { get; }
        public uint LastProcessedSequence { get; }
        public InputButtons LastButtons { get; }
        public uint LastValidCommandTick { get; }
        public bool Alive { get; }
        public ushort Health { get; }
        public PlayerPredictionState(uint schemaVersion, uint tick, EntityId actorId, uint worldRevision, uint collisionRevision, ContentId definitionHash,
            Float3 position, Float3 velocity, float aimYaw, float aimPitch, bool grounded, SurfaceId groundSurfaceId, Float3 groundNormal, Stance stance, float capsuleHeight,
            uint lastGroundedTick, uint jumpConsumedSequence, byte activeSlot, byte desiredSlot, IReadOnlyList<WeaponState> weaponSlots,
            float recoilYaw, float recoilPitch, float recoilYawVelocity, float recoilPitchVelocity, float locomotionDistanceMeters,
            uint lastProcessedSequence, InputButtons lastButtons, uint lastValidCommandTick, bool alive, ushort health)
        {
            Require.That(schemaVersion == ContractVersion.PredictionSchema, nameof(schemaVersion));
            Require.That(actorId.IsValid, nameof(actorId)); Require.Aim(aimYaw, aimPitch); Require.Enum(stance, nameof(stance));
            Require.That(!grounded || groundSurfaceId.IsValid && groundNormal.IsUnit, "ground state");
            Require.That(Require.Finite(capsuleHeight, nameof(capsuleHeight)) > 0, nameof(capsuleHeight));
            Require.That(alive == (health > 0) && alive == (stance != Stance.Dead), "life state");
            Require.Slot(activeSlot); Require.Slot(desiredSlot); Require.Buttons(lastButtons);
            WeaponSlots = new FrozenList<WeaponState>(weaponSlots, 4);
            Require.That(WeaponSlots.Count == 4
                && WeaponSlots[0].IsValid && WeaponSlots[1].IsValid
                && WeaponSlots[2].IsValid && WeaponSlots[3].IsValid, nameof(weaponSlots));
            RecoilYaw = Require.Finite(recoilYaw, nameof(recoilYaw)); RecoilPitch = Require.Finite(recoilPitch, nameof(recoilPitch));
            RecoilYawVelocity = Require.Finite(recoilYawVelocity, nameof(recoilYawVelocity)); RecoilPitchVelocity = Require.Finite(recoilPitchVelocity, nameof(recoilPitchVelocity));
            Require.That(Require.Finite(locomotionDistanceMeters, nameof(locomotionDistanceMeters)) >= 0, nameof(locomotionDistanceMeters));
            SchemaVersion = schemaVersion; Tick = tick; ActorId = actorId; WorldRevision = worldRevision; CollisionRevision = collisionRevision; DefinitionHash = definitionHash;
            Position = position; Velocity = velocity; AimYaw = aimYaw; AimPitch = aimPitch; Grounded = grounded; GroundSurfaceId = groundSurfaceId; GroundNormal = groundNormal;
            Stance = stance; CapsuleHeight = capsuleHeight; LastGroundedTick = lastGroundedTick; JumpConsumedSequence = jumpConsumedSequence; ActiveSlot = activeSlot; DesiredSlot = desiredSlot;
            LocomotionDistanceMeters = locomotionDistanceMeters; LastProcessedSequence = lastProcessedSequence; LastButtons = lastButtons; LastValidCommandTick = lastValidCommandTick; Alive = alive; Health = health;
        }
    }
}
