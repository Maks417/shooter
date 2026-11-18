#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    /// <summary>
    /// Builds the frozen four-slot loadout. Slot 0 holds the selected primary DefinitionId (ADR-072);
    /// slots 1–3 stay pistol / knife / grenade.
    /// </summary>
    public static class WeaponLoadoutPolicy
    {
        public static DefinitionId DefaultPrimaryId { get; } = new DefinitionId("weapons/rifle");
        public static DefinitionId CarbinePrimaryId { get; } = new DefinitionId("weapons/rifle-carbine");
        public static DefinitionId PistolId { get; } = new DefinitionId("weapons/pistol");
        public static DefinitionId KnifeId { get; } = new DefinitionId("weapons/knife");
        public static DefinitionId GrenadeId { get; } = new DefinitionId("weapons/grenade");

        /// <summary>Fixed default loadout for tests and fallbacks; the product draws random loadouts (ADR-102).</summary>
        public static WeaponState[] CreateDefaultLoadout() => CreateLoadout(DefaultPrimaryId, PistolId);

        public static WeaponState[] CreateLoadout(DefinitionId primaryId) => CreateLoadout(primaryId, PistolId);

        public static WeaponState[] CreateLoadout(DefinitionId primaryId, DefinitionId sidearmId) =>
            CreateLoadout(primaryId, sidearmId, WeaponModifiers.Identity);

        /// <summary>ADR-108: as above, with career buffs adding spare magazines to the primary and sidearm reserve.</summary>
        public static WeaponState[] CreateLoadout(DefinitionId primaryId, DefinitionId sidearmId, WeaponModifiers modifiers)
        {
            var primary = WeaponCatalog.Require(primaryId);
            if (primary.Family != WeaponFamily.PrimaryGun)
                throw new ArgumentException("Slot 0 requires a PrimaryGun catalog entry.", nameof(primaryId));
            var sidearm = WeaponCatalog.Require(sidearmId);
            if (sidearm.Family != WeaponFamily.Sidearm)
                throw new ArgumentException("Slot 1 requires a Sidearm catalog entry.", nameof(sidearmId));
            return new[]
            {
                CreateLoaded(primary.Definition, modifiers),
                CreateLoaded(sidearm.Definition, modifiers),
                CreateLoaded(WeaponCatalog.RequireDefinition(KnifeId)),
                CreateLoaded(WeaponCatalog.RequireDefinition(GrenadeId))
            };
        }

        /// <summary>
        /// ADR-102: a deterministic weighted draw of one primary and one sidearm for a new life. The same
        /// seed, actor and tick always give the same guns, so replays and tests stay reproducible.
        /// </summary>
        public static WeaponState[] CreateRandomLoadout(ulong seed, EntityId actor, uint tick)
        {
            ulong state = Mix(seed ^ Mix(actor.Value + 0x9E3779B97F4A7C15UL) ^ Mix(tick + 0xD1B54A32D192ED03UL));
            var primary = Pick(WeaponFamily.PrimaryGun, Mix(state), DefaultPrimaryId);
            var sidearm = Pick(WeaponFamily.Sidearm, Mix(state + 1), PistolId);
            return CreateLoadout(primary, sidearm);
        }

        /// <summary>
        /// ADR-107: a uniform deterministic draw among <paramref name="primary"/>'s allowed sights for the same life as
        /// <see cref="CreateRandomLoadout"/> (its own stream, so the gun draw is unchanged). Invalid for guns without options.
        /// </summary>
        public static DefinitionId RandomSight(ulong seed, EntityId actor, uint tick, DefinitionId primary)
        {
            ulong state = Mix(seed ^ Mix(actor.Value + 0x9E3779B97F4A7C15UL) ^ Mix(tick + 0xD1B54A32D192ED03UL));
            return SightCatalog.Pick(primary, Mix(state + 2));
        }

        /// <summary>Weighted pick among catalog entries of <paramref name="family"/> in catalog order.</summary>
        public static DefinitionId Pick(WeaponFamily family, ulong roll, DefinitionId fallback)
        {
            ulong total = 0;
            var all = WeaponCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Family == family) total += all[i].Definition.SpawnWeight;
            if (total == 0) return fallback;
            ulong target = roll % total;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Family != family) continue;
                ulong weight = all[i].Definition.SpawnWeight;
                if (target < weight) return all[i].Definition.Id;
                target -= weight;
            }
            return fallback;
        }

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public static WeaponState CreateLoaded(WeaponDefinition def) =>
            new WeaponState(def.Id, WeaponAction.Ready, def.MagazineSize, def.ReserveCapacity, 0, 0, 0, 0, false);

        public static WeaponState CreateLoaded(WeaponDefinition def, WeaponModifiers modifiers) =>
            new WeaponState(def.Id, WeaponAction.Ready, def.MagazineSize, modifiers.ReserveFor(def), 0, 0, 0, 0, false);
    }
}
