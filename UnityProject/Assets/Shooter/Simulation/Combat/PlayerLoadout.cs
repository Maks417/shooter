#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    /// <summary>
    /// ADR-107 Armory choice for one soldier: a primary gun with its sight and a sidearm (knife and grenade are fixed).
    /// Always valid: <see cref="Create"/> falls back per field to the default primary, the weapon's factory sight and the
    /// default sidearm, so a stale or hand-edited Armory file can never block a match.
    /// </summary>
    public sealed class PlayerLoadout : IEquatable<PlayerLoadout>
    {
        public static DefinitionId DefaultPrimary { get; } = new DefinitionId("weapons/m16");
        public static DefinitionId DefaultSidearm { get; } = new DefinitionId("weapons/g17");

        public DefinitionId Primary { get; }
        /// <summary>Mounted sight on the primary; invalid when the primary takes none.</summary>
        public DefinitionId PrimarySight { get; }
        public DefinitionId Sidearm { get; }

        private PlayerLoadout(DefinitionId primary, DefinitionId primarySight, DefinitionId sidearm)
        {
            Primary = primary;
            PrimarySight = primarySight;
            Sidearm = sidearm;
        }

        public static PlayerLoadout Default => Create(DefaultPrimary, default, DefaultSidearm);

        public static PlayerLoadout Create(DefinitionId primary, DefinitionId primarySight, DefinitionId sidearm)
        {
            if (!IsSelectable(primary, WeaponFamily.PrimaryGun)) primary = Fallback(DefaultPrimary, WeaponFamily.PrimaryGun, WeaponLoadoutPolicy.DefaultPrimaryId);
            if (!IsSelectable(sidearm, WeaponFamily.Sidearm)) sidearm = Fallback(DefaultSidearm, WeaponFamily.Sidearm, WeaponLoadoutPolicy.PistolId);
            return new PlayerLoadout(primary, SightCatalog.Resolve(primary, primarySight), sidearm);
        }

        /// <summary>Product guns the Armory offers: catalog entries of <paramref name="family"/> that also spawn (weight &gt; 0).</summary>
        public static bool IsSelectable(DefinitionId id, WeaponFamily family) =>
            id.IsValid && WeaponCatalog.TryGet(id, out var entry) && entry.Family == family && entry.Definition.SpawnWeight > 0;

        private static DefinitionId Fallback(DefinitionId preferred, WeaponFamily family, DefinitionId last)
        {
            if (IsSelectable(preferred, family)) return preferred;
            var all = WeaponCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Family == family && all[i].Definition.SpawnWeight > 0) return all[i].Definition.Id;
            return last;
        }

        public bool Equals(PlayerLoadout? other) =>
            other != null && Primary.Equals(other.Primary) && PrimarySight.Equals(other.PrimarySight) && Sidearm.Equals(other.Sidearm);

        public override bool Equals(object? obj) => obj is PlayerLoadout other && Equals(other);

        public override int GetHashCode() => (Primary.GetHashCode() * 397 ^ PrimarySight.GetHashCode()) * 397 ^ Sidearm.GetHashCode();

        public override string ToString() => Primary + "+" + PrimarySight + "/" + Sidearm;
    }
}
