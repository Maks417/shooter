#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Networking.LagCompensation
{
    public enum FireValidationResult
    {
        Accepted = 0,
        RejectDuplicate = 1,
        RejectDead = 2,
        RejectAmmo = 3,
        RejectCadence = 4,
        RejectOwnership = 5,
        RejectForged = 6
    }

    public readonly struct FireIntent
    {
        public EntityId Shooter { get; }
        public uint CommandSequence { get; }
        public uint ShotTick { get; }
        public uint ShotIndex { get; }
        public ushort AmmoInMagazine { get; }
        public uint NextAllowedFireTick { get; }
        public bool AliveAtStart { get; }
        public FireIntent(EntityId shooter, uint commandSequence, uint shotTick, uint shotIndex, ushort ammoInMagazine, uint nextAllowedFireTick, bool aliveAtStart)
        {
            if (!shooter.IsValid) throw new ArgumentException(nameof(shooter));
            Shooter = shooter; CommandSequence = commandSequence; ShotTick = shotTick; ShotIndex = shotIndex;
            AmmoInMagazine = ammoInMagazine; NextAllowedFireTick = nextAllowedFireTick; AliveAtStart = aliveAtStart;
        }
    }

    /// <summary>Authoritative fire gate: ammo/cadence/alive, shot index, duplicate suppression; same-tick trades allowed, prior-dead never fires.</summary>
    public sealed class AuthoritativeFireValidator
    {
        private readonly HashSet<ulong> acceptedKeys = new HashSet<ulong>();
        private readonly List<(EntityId shooter, EntityId target, uint tick, ushort damage)> orderedDamage = new List<(EntityId, EntityId, uint, ushort)>();

        public IReadOnlyList<(EntityId shooter, EntityId target, uint tick, ushort damage)> OrderedDamage => orderedDamage;

        public FireValidationResult Validate(FireIntent intent)
        {
            if (!intent.AliveAtStart) return FireValidationResult.RejectDead;
            if (intent.AmmoInMagazine == 0) return FireValidationResult.RejectAmmo;
            if (intent.ShotTick < intent.NextAllowedFireTick) return FireValidationResult.RejectCadence;
            ulong key = (intent.Shooter.Value << 32) ^ intent.CommandSequence ^ ((ulong)intent.ShotIndex << 16);
            if (!acceptedKeys.Add(key)) return FireValidationResult.RejectDuplicate;
            return FireValidationResult.Accepted;
        }

        public FireValidationResult ValidateAndQueueDamage(FireIntent intent, EntityId target, ushort damage)
        {
            var result = Validate(intent);
            if (result != FireValidationResult.Accepted) return result;
            if (!target.IsValid || damage == 0) return FireValidationResult.RejectForged;
            orderedDamage.Add((intent.Shooter, target, intent.ShotTick, damage));
            return FireValidationResult.Accepted;
        }

        /// <summary>Apply queued damage in shooter-id then shot order already recorded; same-tick mutual lethality may trade.</summary>
        public Dictionary<EntityId, ushort> ResolveHealth(IReadOnlyDictionary<EntityId, ushort> startingHealth)
        {
            var health = new Dictionary<EntityId, ushort>(startingHealth);
            orderedDamage.Sort((a, b) =>
            {
                int c = a.tick.CompareTo(b.tick);
                if (c != 0) return c;
                c = a.shooter.Value.CompareTo(b.shooter.Value);
                return c != 0 ? c : a.target.Value.CompareTo(b.target.Value);
            });
            foreach (var (shooter, target, tick, damage) in orderedDamage)
            {
                _ = shooter; _ = tick;
                if (!health.TryGetValue(target, out ushort hp) || hp == 0) continue;
                int next = hp - damage;
                health[target] = next <= 0 ? (ushort)0 : (ushort)next;
            }
            return health;
        }

        public void ClearSession()
        {
            acceptedKeys.Clear();
            orderedDamage.Clear();
        }
    }
}
