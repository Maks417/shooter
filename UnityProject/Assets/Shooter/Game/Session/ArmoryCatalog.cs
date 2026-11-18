#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Combat;

namespace Shooter.Game.Session
{
    /// <summary>
    /// ADR-107: what the Armory offers, expressed in contract ids so the shell needs no Simulation reference. Guns are
    /// the catalog entries that also spawn for bots (spawn weight &gt; 0); legacy test rifles and pistols stay hidden.
    /// </summary>
    public static class ArmoryCatalog
    {
        private static readonly DefinitionId[] PrimaryIds = Collect(WeaponFamily.PrimaryGun);
        private static readonly DefinitionId[] SidearmIds = Collect(WeaponFamily.Sidearm);

        public static IReadOnlyList<DefinitionId> Primaries => PrimaryIds;
        public static IReadOnlyList<DefinitionId> Sidearms => SidearmIds;

        /// <summary>Combat numbers the Armory shows for a gun (damage, magazine, rounds per minute, fire mode).</summary>
        public static bool TryGetStats(DefinitionId weapon, out int damage, out int magazine, out int roundsPerMinute, out string fireMode)
        {
            damage = magazine = roundsPerMinute = 0;
            fireMode = "";
            if (!WeaponCatalog.TryGet(weapon, out var entry)) return false;
            var d = entry.Definition;
            damage = d.BaseDamage;
            magazine = d.MagazineSize;
            roundsPerMinute = d.FireMode == FireMode.Burst && d.BurstIntervalTicks > 0
                ? (int)(3600u / d.BurstIntervalTicks)
                : d.FireIntervalTicks == 0 ? 0 : (int)(3600u / d.FireIntervalTicks);
            fireMode = d.FireMode switch
            {
                FireMode.Automatic => "AUTO",
                FireMode.SemiAutomatic => "SEMI",
                FireMode.Burst => d.BurstCount + "-ROUND BURST",
                _ => d.FireMode.ToString().ToUpperInvariant()
            };
            return true;
        }

        /// <summary>The always-valid loadout these ids resolve to (unknown or disallowed ids fall back per field).</summary>
        public static PlayerLoadout Resolve(DefinitionId primary, DefinitionId sight, DefinitionId sidearm) =>
            PlayerLoadout.Create(primary, sight, sidearm);

        private static DefinitionId[] Collect(WeaponFamily family)
        {
            var list = new List<DefinitionId>();
            var all = WeaponCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Family == family && all[i].Definition.SpawnWeight > 0) list.Add(all[i].Definition.Id);
            return list.ToArray();
        }
    }
}
