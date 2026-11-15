#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    /// <summary>Loadout family for a catalogued weapon — independent of which slot index holds it.</summary>
    public enum WeaponFamily : byte
    {
        PrimaryGun = 0,
        Sidearm = 1,
        Melee = 2,
        Throwable = 3
    }

    /// <summary>
    /// Frozen in-process weapon identity row (ADR-072). Combat stats live on <see cref="Definition"/>;
    /// <see cref="VisualProfileId"/> keys packaged meshes / presentation profiles.
    /// </summary>
    public readonly struct WeaponCatalogEntry
    {
        public WeaponDefinition Definition { get; }
        public WeaponFamily Family { get; }
        public string VisualProfileId { get; }

        public WeaponCatalogEntry(WeaponDefinition definition, WeaponFamily family, string visualProfileId)
        {
            if (!definition.Id.IsValid) throw new ArgumentException("Definition id required.", nameof(definition));
            if (string.IsNullOrEmpty(visualProfileId)) throw new ArgumentException("Visual profile id required.", nameof(visualProfileId));
            Definition = definition;
            Family = family;
            VisualProfileId = visualProfileId;
        }
    }

    /// <summary>
    /// Closed deterministic catalog keyed by <see cref="DefinitionId"/>. Adding a rifle is a new row, not a slot fork.
    /// </summary>
    public static class WeaponCatalog
    {
        private static readonly Dictionary<string, WeaponCatalogEntry> ById;
        private static readonly WeaponCatalogEntry[] AllEntries;

        static WeaponCatalog()
        {
            var rifle = new WeaponDefinition(
                new DefinitionId("weapons/rifle"), FireMode.Automatic, 6, 144, 24, 30, 90, 30,
                Deg(1.0f), Deg(0.15f), Deg(0.35f), Deg(0.1f));
            var carbine = new WeaponDefinition(
                new DefinitionId("weapons/rifle-carbine"), FireMode.Automatic, 5, 132, 22, 25, 75, 28,
                Deg(1.15f), Deg(0.18f), Deg(0.40f), Deg(0.12f));
            var pistol = new WeaponDefinition(
                new DefinitionId("weapons/pistol"), FireMode.SemiAutomatic, 10, 108, 18, 15, 45, 25,
                Deg(1.5f), Deg(0.3f), Deg(0.55f), Deg(0.15f));
            var knife = new WeaponDefinition(
                new DefinitionId("weapons/knife"), FireMode.Melee, 24, 0, 12, 1, 0, 55,
                0f, 0f, 0f, 0f, maxRangeMeters: 1.8f, consumesAmmo: false, allowsSprintFire: true);
            var grenade = new WeaponDefinition(
                new DefinitionId("weapons/grenade"), FireMode.Throwable, 0, 0, 18, 1, 0, 100,
                0f, 0f, 0f, 0f, maxRangeMeters: 0f, fuseTicks: 120, throwSpeedMetersPerSecond: 18f,
                blastRadiusMeters: 5f, consumesAmmo: true, allowsSprintFire: false);

            AllEntries = new[]
            {
                new WeaponCatalogEntry(rifle, WeaponFamily.PrimaryGun, "weapons/rifle"),
                new WeaponCatalogEntry(carbine, WeaponFamily.PrimaryGun, "weapons/rifle-carbine"),
                new WeaponCatalogEntry(pistol, WeaponFamily.Sidearm, "weapons/pistol"),
                new WeaponCatalogEntry(knife, WeaponFamily.Melee, "weapons/knife"),
                new WeaponCatalogEntry(grenade, WeaponFamily.Throwable, "weapons/grenade")
            };
            ById = new Dictionary<string, WeaponCatalogEntry>(AllEntries.Length, StringComparer.Ordinal);
            for (int i = 0; i < AllEntries.Length; i++)
                ById.Add(AllEntries[i].Definition.Id.ToString(), AllEntries[i]);
        }

        public static IReadOnlyList<WeaponCatalogEntry> All => AllEntries;

        public static bool TryGet(DefinitionId id, out WeaponCatalogEntry entry)
        {
            entry = default;
            if (!id.IsValid) return false;
            return ById.TryGetValue(id.ToString(), out entry);
        }

        public static WeaponCatalogEntry Require(DefinitionId id)
        {
            if (!TryGet(id, out var entry))
                throw new ArgumentException("Unknown weapon definition", nameof(id));
            return entry;
        }

        public static WeaponDefinition RequireDefinition(DefinitionId id) => Require(id).Definition;

        public static IEnumerable<WeaponCatalogEntry> Primaries()
        {
            for (int i = 0; i < AllEntries.Length; i++)
                if (AllEntries[i].Family == WeaponFamily.PrimaryGun)
                    yield return AllEntries[i];
        }

        private static float Deg(float degrees) => degrees * (float)Math.PI / 180f;
    }
}
