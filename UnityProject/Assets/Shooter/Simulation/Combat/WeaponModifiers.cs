#nullable enable
using System;

namespace Shooter.Simulation.Combat
{
    /// <summary>
    /// ADR-108 career buffs: small per-soldier scales applied to every firearm the soldier holds (primary, sidearm and
    /// picked-up guns; never the knife or grenade). Scales are integer per-mille so tick math stays exact and
    /// deterministic. <see cref="Identity"/> leaves every definition bit-identical, which keeps bots, harness scenarios
    /// and replays unchanged. Damage, fire rate and movement are never modified.
    /// </summary>
    public readonly struct WeaponModifiers : IEquatable<WeaponModifiers>
    {
        public const ushort One = 1000;
        /// <summary>Smallest allowed scale (−15 %), matching the ADR-108 caps.</summary>
        public const ushort MinScale = 850;
        /// <summary>Smallest equip scale (−20 %, Quick Draw).</summary>
        public const ushort MinEquipScale = 800;
        public const byte MaxExtraMagazines = 2;

        private readonly ushort recoil;
        private readonly ushort reload;
        private readonly ushort adsSpread;
        private readonly ushort equip;

        public ushort RecoilPerMille => recoil == 0 ? One : recoil;
        public ushort ReloadPerMille => reload == 0 ? One : reload;
        public ushort AdsSpreadPerMille => adsSpread == 0 ? One : adsSpread;
        public ushort EquipPerMille => equip == 0 ? One : equip;
        public byte ExtraMagazines { get; }

        public WeaponModifiers(ushort recoilPerMille, ushort reloadPerMille, ushort adsSpreadPerMille, ushort equipPerMille, byte extraMagazines)
        {
            recoil = Clamp(recoilPerMille, MinScale);
            reload = Clamp(reloadPerMille, MinScale);
            adsSpread = Clamp(adsSpreadPerMille, MinScale);
            equip = Clamp(equipPerMille, MinEquipScale);
            ExtraMagazines = extraMagazines > MaxExtraMagazines ? MaxExtraMagazines : extraMagazines;
        }

        public static WeaponModifiers Identity => default;

        public bool IsIdentity =>
            RecoilPerMille == One && ReloadPerMille == One && AdsSpreadPerMille == One && EquipPerMille == One && ExtraMagazines == 0;

        /// <summary>True for guns the buffs touch (automatic, semi-automatic and burst fire).</summary>
        public static bool Applies(in WeaponDefinition def) =>
            def.FireMode == FireMode.Automatic || def.FireMode == FireMode.SemiAutomatic || def.FireMode == FireMode.Burst;

        /// <summary>The buffed copy of <paramref name="def"/>; the same value when identity or not a firearm.</summary>
        public WeaponDefinition Apply(in WeaponDefinition def)
        {
            if (IsIdentity || !Applies(def)) return def;
            return new WeaponDefinition(
                def.Id, def.FireMode, def.FireIntervalTicks,
                ScaleTicks(def.ReloadTicks, ReloadPerMille),
                ScaleTicks(def.EquipTicks, EquipPerMille),
                def.MagazineSize,
                ReserveFor(def),
                def.BaseDamage,
                def.HipSpreadRadians,
                def.AdsSpreadRadians * AdsSpreadPerMille / One,
                def.RecoilPitchRadians * RecoilPerMille / One,
                def.RecoilYawHalfRadians * RecoilPerMille / One,
                def.MaxRangeMeters, def.FuseTicks, def.ThrowSpeedMetersPerSecond, def.BlastRadiusMeters,
                def.ConsumesAmmo, def.AllowsSprintFire, def.BurstCount, def.BurstIntervalTicks,
                def.FalloffStartMeters, def.FalloffEndMeters, def.FalloffMinScale, def.MoveSpeedScale, def.SpawnWeight);
        }

        /// <summary>Starting reserve: the catalog reserve plus <see cref="ExtraMagazines"/> full magazines for firearms.</summary>
        public ushort ReserveFor(in WeaponDefinition def)
        {
            if (ExtraMagazines == 0 || !Applies(def) || !def.ConsumesAmmo) return def.ReserveCapacity;
            int total = def.ReserveCapacity + ExtraMagazines * def.MagazineSize;
            return total > ushort.MaxValue ? ushort.MaxValue : (ushort)total;
        }

        /// <summary>Rounded per-mille tick scale; never below one tick for a nonzero phase.</summary>
        public static uint ScaleTicks(uint ticks, ushort perMille)
        {
            if (ticks == 0 || perMille == One) return ticks;
            uint scaled = (uint)((ticks * (ulong)perMille + One / 2) / One);
            return scaled == 0 ? 1u : scaled;
        }

        private static ushort Clamp(ushort value, ushort min)
        {
            if (value == 0 || value >= One) return 0;
            return value < min ? min : value;
        }

        public bool Equals(WeaponModifiers other) =>
            RecoilPerMille == other.RecoilPerMille && ReloadPerMille == other.ReloadPerMille && AdsSpreadPerMille == other.AdsSpreadPerMille
            && EquipPerMille == other.EquipPerMille && ExtraMagazines == other.ExtraMagazines;

        public override bool Equals(object? obj) => obj is WeaponModifiers other && Equals(other);

        public override int GetHashCode() =>
            ((RecoilPerMille * 31 + ReloadPerMille) * 31 + AdsSpreadPerMille) * 31 + EquipPerMille + ExtraMagazines * 7919;

        public override string ToString() =>
            IsIdentity ? "none" : "recoil=" + RecoilPerMille + " reload=" + ReloadPerMille + " ads=" + AdsSpreadPerMille + " equip=" + EquipPerMille + " mags=+" + ExtraMagazines;
    }
}
