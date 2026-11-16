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
    /// Closed deterministic catalog keyed by <see cref="DefinitionId"/>. Adding a weapon is a new
    /// <c>Content/Weapons/&lt;name&gt;.weapon.json</c> file plus a generator run, not a slot fork (ADR-072, ADR-077).
    /// </summary>
    public static partial class WeaponCatalog
    {
        private static readonly Dictionary<string, WeaponCatalogEntry> ById;
        private static readonly WeaponCatalogEntry[] AllEntries;

        static WeaponCatalog()
        {
            // Rows come from Content/Weapons/*.weapon.json via Tools/generate_weapon_catalog.py (ADR-077).
            AllEntries = CreateGeneratedEntries();
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
