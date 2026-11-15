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

        public static WeaponDefinition Rifle { get; } = new WeaponDefinition(
            new DefinitionId("weapons/rifle"), FireMode.Automatic, 6, 144, 24, 30, 90, 30,
            Deg(1.0f), Deg(0.15f), Deg(0.35f), Deg(0.1f));

        public static WeaponDefinition Pistol { get; } = new WeaponDefinition(
            new DefinitionId("weapons/pistol"), FireMode.SemiAutomatic, 10, 108, 18, 15, 45, 25,
            Deg(1.5f), Deg(0.3f), Deg(0.55f), Deg(0.15f));

        public static WeaponDefinition Knife { get; } = new WeaponDefinition(
            new DefinitionId("weapons/knife"), FireMode.Melee, 24, 0, 12, 1, 0, 55,
            0f, 0f, 0f, 0f, maxRangeMeters: 1.8f, consumesAmmo: false, allowsSprintFire: true);

        public static WeaponDefinition Grenade { get; } = new WeaponDefinition(
            new DefinitionId("weapons/grenade"), FireMode.Throwable, 0, 0, 18, 1, 0, 100,
            0f, 0f, 0f, 0f, maxRangeMeters: 0f, fuseTicks: 120, throwSpeedMetersPerSecond: 18f,
            blastRadiusMeters: 5f, consumesAmmo: true, allowsSprintFire: false);

        public static WeaponDefinition FromId(DefinitionId id)
        {
            if (id.Equals(Rifle.Id)) return Rifle;
            if (id.Equals(Pistol.Id)) return Pistol;
            if (id.Equals(Knife.Id)) return Knife;
            if (id.Equals(Grenade.Id)) return Grenade;
            throw new System.ArgumentException("Unknown weapon definition", nameof(id));
        }

        private static float Deg(float degrees) => degrees * (float)System.Math.PI / 180f;
    }
}
