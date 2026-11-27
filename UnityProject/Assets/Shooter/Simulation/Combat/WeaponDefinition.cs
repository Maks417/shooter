#nullable enable
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    /// <summary>Burst fires <see cref="WeaponDefinition.BurstCount"/> rounds per trigger press (ADR-102).</summary>
    public enum FireMode { Automatic = 0, SemiAutomatic = 1, Melee = 2, Throwable = 3, Burst = 4 }

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
        /// <summary>Rounds per trigger press for <see cref="FireMode.Burst"/>; 0 otherwise.</summary>
        public byte BurstCount { get; }
        /// <summary>Ticks between rounds inside a burst; <see cref="FireIntervalTicks"/> is the delay after the burst.</summary>
        public uint BurstIntervalTicks { get; }
        /// <summary>Full damage up to this range (ADR-102). 0 disables falloff.</summary>
        public float FalloffStartMeters { get; }
        /// <summary>Range where damage reaches <see cref="FalloffMinScale"/>.</summary>
        public float FalloffEndMeters { get; }
        public float FalloffMinScale { get; }
        /// <summary>Movement speed multiplier while this weapon is active.</summary>
        public float MoveSpeedScale { get; }
        /// <summary>Relative chance of being drawn into a random spawn loadout; 0 never.</summary>
        public ushort SpawnWeight { get; }
        /// <summary>ADR-118: hitscan rays per shot (a shotgun's pellets); 1 for every single-projectile gun.</summary>
        public byte PelletCount { get; }
        /// <summary>ADR-118: half-angle cone the pellets scatter in around the shot's aimed direction.</summary>
        public float PelletSpreadRadians { get; }
        /// <summary>ADR-118: reload one round per <see cref="ReloadTicks"/> phase (tube-fed shotgun) instead of a full magazine.</summary>
        public bool ReloadPerRound { get; }
        /// <summary>ADR-125: every hit takes the target's remaining health, whatever the region, range or armor (bolt-action snipers).</summary>
        public bool LethalOnHit { get; }

        public WeaponDefinition(
            DefinitionId id, FireMode fireMode, uint fireIntervalTicks, uint reloadTicks, uint equipTicks,
            ushort magazineSize, ushort reserveCapacity, ushort baseDamage,
            float hipSpreadRadians, float adsSpreadRadians, float recoilPitchRadians, float recoilYawHalfRadians,
            float maxRangeMeters = 300f, uint fuseTicks = 0, float throwSpeedMetersPerSecond = 0f,
            float blastRadiusMeters = 0f, bool consumesAmmo = true, bool allowsSprintFire = false,
            byte burstCount = 0, uint burstIntervalTicks = 0, float falloffStartMeters = 0f, float falloffEndMeters = 0f,
            float falloffMinScale = 1f, float moveSpeedScale = 1f, ushort spawnWeight = 1,
            byte pelletCount = 1, float pelletSpreadRadians = 0f, bool reloadPerRound = false, bool lethalOnHit = false)
        {
            Id = id; FireMode = fireMode; FireIntervalTicks = fireIntervalTicks; ReloadTicks = reloadTicks; EquipTicks = equipTicks;
            MagazineSize = magazineSize; ReserveCapacity = reserveCapacity; BaseDamage = baseDamage;
            HipSpreadRadians = hipSpreadRadians; AdsSpreadRadians = adsSpreadRadians;
            RecoilPitchRadians = recoilPitchRadians; RecoilYawHalfRadians = recoilYawHalfRadians;
            MaxRangeMeters = maxRangeMeters; FuseTicks = fuseTicks;
            ThrowSpeedMetersPerSecond = throwSpeedMetersPerSecond; BlastRadiusMeters = blastRadiusMeters;
            ConsumesAmmo = consumesAmmo; AllowsSprintFire = allowsSprintFire;
            BurstCount = burstCount; BurstIntervalTicks = burstIntervalTicks;
            FalloffStartMeters = falloffStartMeters; FalloffEndMeters = falloffEndMeters; FalloffMinScale = falloffMinScale;
            MoveSpeedScale = moveSpeedScale; SpawnWeight = spawnWeight;
            PelletCount = pelletCount == 0 ? (byte)1 : pelletCount; PelletSpreadRadians = pelletSpreadRadians; ReloadPerRound = reloadPerRound;
            LethalOnHit = lethalOnHit;
        }

        /// <summary>Damage multiplier at <paramref name="meters"/>: 1 before the falloff start, linear down to the minimum at its end.</summary>
        public float FalloffScale(float meters)
        {
            if (FalloffStartMeters <= 0f || FalloffEndMeters <= FalloffStartMeters || meters <= FalloffStartMeters) return 1f;
            if (meters >= FalloffEndMeters) return FalloffMinScale;
            float t = (meters - FalloffStartMeters) / (FalloffEndMeters - FalloffStartMeters);
            return 1f + (FalloffMinScale - 1f) * t;
        }

        public static WeaponDefinition Rifle => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.DefaultPrimaryId);
        public static WeaponDefinition RifleCarbine => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.CarbinePrimaryId);
        public static WeaponDefinition Pistol => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.PistolId);
        public static WeaponDefinition Knife => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.KnifeId);
        public static WeaponDefinition Grenade => WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.GrenadeId);

        public static WeaponDefinition FromId(DefinitionId id) => WeaponCatalog.RequireDefinition(id);
    }
}
