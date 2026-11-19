#nullable enable

namespace Shooter.Presentation.View
{
    /// <summary>
    /// ADR-108 presentation mirror of the local player's career buffs (the authority's <c>WeaponModifiers</c>):
    /// per-mille recoil, reload and equip scales, and (ADR-119 Slick Action) the bolt/pump cycle. Read-only display
    /// data; it never changes authority.
    /// </summary>
    public readonly struct WeaponHandlingScale
    {
        private readonly ushort recoil;
        private readonly ushort reload;
        private readonly ushort equip;
        private readonly ushort cycle;

        public ushort RecoilPerMille => recoil == 0 ? (ushort)1000 : recoil;
        public ushort ReloadPerMille => reload == 0 ? (ushort)1000 : reload;
        public ushort EquipPerMille => equip == 0 ? (ushort)1000 : equip;
        public ushort ManualCyclePerMille => cycle == 0 ? (ushort)1000 : cycle;

        public WeaponHandlingScale(ushort recoilPerMille, ushort reloadPerMille, ushort equipPerMille)
            : this(recoilPerMille, reloadPerMille, equipPerMille, 1000)
        {
        }

        public WeaponHandlingScale(ushort recoilPerMille, ushort reloadPerMille, ushort equipPerMille, ushort manualCyclePerMille)
        {
            recoil = recoilPerMille >= 1000 ? (ushort)0 : recoilPerMille;
            reload = reloadPerMille >= 1000 ? (ushort)0 : reloadPerMille;
            equip = equipPerMille >= 1000 ? (ushort)0 : equipPerMille;
            cycle = manualCyclePerMille >= 1000 ? (ushort)0 : manualCyclePerMille;
        }

        public static WeaponHandlingScale Identity => default;

        public bool IsIdentity => RecoilPerMille == 1000 && ReloadPerMille == 1000 && EquipPerMille == 1000 && ManualCyclePerMille == 1000;

        /// <summary>
        /// ADR-119 mirror of the simulation's manual-action rule (semi-automatic, at least 30 ticks between shots): in the
        /// shipped catalog these are exactly the bolt and pump handling styles.
        /// </summary>
        public const uint ManualActionMinIntervalTicks = 30;

        /// <summary>Same rounding as the simulation: nearest tick, never below one tick for a nonzero phase.</summary>
        public static uint ScaleTicks(uint ticks, ushort perMille)
        {
            if (ticks == 0 || perMille == 1000) return ticks;
            uint scaled = (uint)((ticks * (ulong)perMille + 500) / 1000);
            return scaled == 0 ? 1u : scaled;
        }
    }
}
