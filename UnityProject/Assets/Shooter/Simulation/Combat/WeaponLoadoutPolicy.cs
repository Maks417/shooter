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

        /// <summary>Product default: classic rifle in slot 0.</summary>
        public static WeaponState[] CreateDefaultLoadout() => CreateLoadout(DefaultPrimaryId);

        public static WeaponState[] CreateLoadout(DefinitionId primaryId)
        {
            var primary = WeaponCatalog.Require(primaryId);
            if (primary.Family != WeaponFamily.PrimaryGun)
                throw new ArgumentException("Slot 0 requires a PrimaryGun catalog entry.", nameof(primaryId));
            return new[]
            {
                CreateLoaded(primary.Definition),
                CreateLoaded(WeaponCatalog.RequireDefinition(PistolId)),
                CreateLoaded(WeaponCatalog.RequireDefinition(KnifeId)),
                CreateLoaded(WeaponCatalog.RequireDefinition(GrenadeId))
            };
        }

        public static WeaponState CreateLoaded(WeaponDefinition def) =>
            new WeaponState(def.Id, WeaponAction.Ready, def.MagazineSize, def.ReserveCapacity, 0, 0, 0, 0, false);
    }
}
