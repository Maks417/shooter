#nullable enable
using System;

namespace Shooter.Contracts
{
    public abstract class CombatPayload { private protected CombatPayload() { } }
    public sealed class ShotPayload : CombatPayload
    {
        public DefinitionId WeaponId { get; }
        public uint ShotIndex { get; }
        public Float3 Origin { get; }
        public Float3 UnitDirection { get; }
        public ulong Seed { get; }
        public uint CollisionRevision { get; }
        public ShotPayload(DefinitionId weaponId, uint shotIndex, Float3 origin, Float3 unitDirection, ulong seed, uint collisionRevision)
        {
            Require.That(weaponId.IsValid && unitDirection.IsUnit, "shot payload");
            WeaponId = weaponId; ShotIndex = shotIndex; Origin = origin; UnitDirection = unitDirection; Seed = seed; CollisionRevision = collisionRevision;
        }
    }
    public sealed class ImpactPayload : CombatPayload
    {
        public Float3 Position { get; }
        public Float3 Normal { get; }
        public ushort SurfaceTag { get; }
        public ushort RegionId { get; }
        public ImpactPayload(Float3 position, Float3 normal, ushort surfaceTag, ushort regionId)
        { Require.That(normal.IsUnit && regionId <= 6, "impact payload"); Position = position; Normal = normal; SurfaceTag = surfaceTag; RegionId = regionId; }
    }
    public sealed class DamagePayload : CombatPayload
    {
        public ushort Amount { get; }
        public ushort RemainingHealth { get; }
        public ushort RegionId { get; }
        public DamagePayload(ushort amount, ushort remainingHealth, ushort regionId)
        { Require.That(amount > 0 && regionId >= 1 && regionId <= 6, "damage payload"); Amount = amount; RemainingHealth = remainingHealth; RegionId = regionId; }
    }
    public sealed class ActionPayload : CombatPayload
    {
        public byte Slot { get; }
        public uint StartTick { get; }
        public uint EndTick { get; }
        public ActionPayload(byte slot, uint startTick, uint endTick)
        { Require.Slot(slot); Serial32.ForwardDistance(endTick, startTick); Slot = slot; StartTick = startTick; EndTick = endTick; }
    }
    public sealed class DeathPayload : CombatPayload
    {
        public EntityId Killer { get; }
        public DeathPayload(EntityId killer) { Killer = killer; }
    }
    public sealed class MatchPayload : CombatPayload
    {
        public MatchState OldState { get; }
        public MatchState NewState { get; }
        public ushort ReasonCode { get; }
        public MatchPayload(MatchState oldState, MatchState newState, ushort reasonCode)
        { Require.Enum(oldState, nameof(oldState)); Require.Enum(newState, nameof(newState)); OldState = oldState; NewState = newState; ReasonCode = reasonCode; }
    }
    public sealed class ProjectilePayload : CombatPayload
    {
        public EntityId ProjectileId { get; }
        public Float3 Origin { get; }
        public Float3 Velocity { get; }
        public DefinitionId DefinitionId { get; }
        public ProjectilePayload(EntityId projectileId, Float3 origin, Float3 velocity, DefinitionId definitionId)
        { Require.That(projectileId.IsValid && definitionId.IsValid, "projectile payload"); ProjectileId = projectileId; Origin = origin; Velocity = velocity; DefinitionId = definitionId; }
    }
    public sealed class CombatEvent
    {
        public EventId Id { get; }
        public CombatEventKind Kind { get; }
        public EntityId Instigator { get; }
        public EntityId Target { get; }
        public CombatPayload Payload { get; }
        public CombatEvent(EventId id, CombatEventKind kind, EntityId instigator, EntityId target, CombatPayload payload)
        {
            Require.That(!id.IsEmpty, nameof(id)); Require.Enum(kind, nameof(kind));
            bool matches = kind switch
            {
                CombatEventKind.ShotAccepted => payload is ShotPayload,
                CombatEventKind.ProjectileSpawned => payload is ProjectilePayload,
                CombatEventKind.Impact => payload is ImpactPayload,
                CombatEventKind.DamageApplied => payload is DamagePayload,
                CombatEventKind.ReloadStarted or CombatEventKind.ReloadCompleted => payload is ActionPayload,
                CombatEventKind.Death => payload is DeathPayload,
                CombatEventKind.MatchChanged => payload is MatchPayload,
                _ => false
            };
            Require.That(matches, nameof(payload));
            Id = id; Kind = kind; Instigator = instigator; Target = target; Payload = payload;
        }
    }
}
