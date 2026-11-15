#nullable enable
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    public enum FireMode { Automatic = 0, SemiAutomatic = 1, Melee = 2, Throwable = 3 }

    public readonly struct WeaponDefinition
    {
        public DefinitionId Id { get; }
        public FireMode FireMode { get; }
        public uint FireIntervalTicks { get; }
        public uint ReloadTicks { get; }
        public uint EquipTicks { get; }
        public ushort MagazineSize { get; }
        public ushort ReserveCapacity { get; }
        public ushort BaseDamage { get; }
        public float HipSpreadRadians { get; }
        public float AdsSpreadRadians { get; }
        public float RecoilPitchRadians { get; }
        public float RecoilYawHalfRadians { get; }
        public float MaxRangeMeters { get; }
        public uint FuseTicks { get; }
        public float ThrowSpeedMetersPerSecond { get; }
        public float BlastRadiusMeters { get; }
        public bool ConsumesAmmo { get; }
        public bool AllowsSprintFire { get; }

        public WeaponDefinition(
            DefinitionId id, FireMode fireMode, uint fireIntervalTicks, uint reloadTicks, uint equipTicks,
            ushort magazineSize, ushort reserveCapacity, ushort baseDamage,
            float hipSpreadRadians, float adsSpreadRadians, float recoilPitchRadians, float recoilYawHalfRadians,
            float maxRangeMeters = 300f, uint fuseTicks = 0, float throwSpeedMetersPerSecond = 0f,
            float blastRadiusMeters = 0f, bool consumesAmmo = true, bool allowsSprintFire = false)
        {
            Id = id; FireMode = fireMode; FireIntervalTicks = fireIntervalTicks; ReloadTicks = reloadTicks; EquipTicks = equipTicks;
            MagazineSize = magazineSize; ReserveCapacity = reserveCapacity; BaseDamage = baseDamage;
            HipSpreadRadians = hipSpreadRadians; AdsSpreadRadians = adsSpreadRadians;
            RecoilPitchRadians = recoilPitchRadians; RecoilYawHalfRadians = recoilYawHalfRadians;
            MaxRangeMeters = maxRangeMeters; FuseTicks = fuseTicks;
            ThrowSpeedMetersPerSecond = throwSpeedMetersPerSecond; BlastRadiusMeters = blastRadiusMeters;
            ConsumesAmmo = consumesAmmo; AllowsSprintFire = allowsSprintFire;
        }

        public static WeaponDefinition Rifle => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.DefaultPrimaryId);
        public static WeaponDefinition RifleCarbine => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.CarbinePrimaryId);
        public static WeaponDefinition Pistol => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.PistolId);
        public static WeaponDefinition Knife => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.KnifeId);
        public static WeaponDefinition Grenade => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.GrenadeId);

        public static WeaponDefinition FromId(DefinitionId id) => WeaponCatalog.RequireDefinition(id);
    }
}
