#nullable enable

namespace Shooter.Presentation.View
{
    /// <summary>
    /// ADR-108 presentation mirror of the local player's career buffs (the authority's <c>WeaponModifiers</c>):
    /// per-mille recoil, reload and equip scales. Read-only display data; it never changes authority.
    /// </summary>
    public readonly struct WeaponHandlingScale
    {
        private readonly ushort recoil;
        private readonly ushort reload;
        private readonly ushort equip;

        public ushort RecoilPerMille => recoil == 0 ? (ushort)1000 : recoil;
        public ushort ReloadPerMille => reload == 0 ? (ushort)1000 : reload;
        public ushort EquipPerMille => equip == 0 ? (ushort)1000 : equip;

        public WeaponHandlingScale(ushort recoilPerMille, ushort reloadPerMille, ushort equipPerMille)
        {
            recoil = recoilPerMille >= 1000 ? (ushort)0 : recoilPerMille;
            reload = reloadPerMille >= 1000 ? (ushort)0 : reloadPerMille;
            equip = equipPerMille >= 1000 ? (ushort)0 : equipPerMille;
        }

        public static WeaponHandlingScale Identity => default;

        public bool IsIdentity => RecoilPerMille == 1000 && ReloadPerMille == 1000 && EquipPerMille == 1000;

        /// <summary>Same rounding as the simulation: nearest tick, never below one tick for a nonzero phase.</summary>
        public static uint ScaleTicks(uint ticks, ushort perMille)
        {
            if (ticks == 0 || perMille == 1000) return ticks;
            uint scaled = (uint)((ticks * (ulong)perMille + 500) / 1000);
            return scaled == 0 ? 1u : scaled;
        }
    }
}
