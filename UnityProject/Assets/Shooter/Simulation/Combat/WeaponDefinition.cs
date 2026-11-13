#nullable enable
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    public enum FireMode { Automatic = 0, SemiAutomatic = 1 }

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

        public WeaponDefinition(
            DefinitionId id, FireMode fireMode, uint fireIntervalTicks, uint reloadTicks, uint equipTicks,
            ushort magazineSize, ushort reserveCapacity, ushort baseDamage,
            float hipSpreadRadians, float adsSpreadRadians, float recoilPitchRadians, float recoilYawHalfRadians)
        {
            Id = id; FireMode = fireMode; FireIntervalTicks = fireIntervalTicks; ReloadTicks = reloadTicks; EquipTicks = equipTicks;
            MagazineSize = magazineSize; ReserveCapacity = reserveCapacity; BaseDamage = baseDamage;
            HipSpreadRadians = hipSpreadRadians; AdsSpreadRadians = adsSpreadRadians;
            RecoilPitchRadians = recoilPitchRadians; RecoilYawHalfRadians = recoilYawHalfRadians;
        }

        public static WeaponDefinition Rifle { get; } = new WeaponDefinition(
            new DefinitionId("weapons/rifle"), FireMode.Automatic, 6, 144, 24, 30, 90, 30,
            Deg(1.0f), Deg(0.15f), Deg(0.35f), Deg(0.1f));

        public static WeaponDefinition Pistol { get; } = new WeaponDefinition(
            new DefinitionId("weapons/pistol"), FireMode.SemiAutomatic, 10, 108, 18, 15, 45, 25,
            Deg(1.5f), Deg(0.3f), Deg(0.55f), Deg(0.15f));

        public static WeaponDefinition FromId(DefinitionId id)
        {
            if (id.Equals(Rifle.Id)) return Rifle;
            if (id.Equals(Pistol.Id)) return Pistol;
            throw new System.ArgumentException("Unknown weapon definition", nameof(id));
        }

        private static float Deg(float degrees) => degrees * (float)System.Math.PI / 180f;
    }
}
