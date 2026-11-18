#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// ADR-107 Armory document (<c>shooter.armory.v1</c>): the primary and sidearm the player takes into every match,
    /// plus the sight chosen for each primary (so switching guns remembers each gun's sight). Ids are stored as text and
    /// are only shape-checked here; the session resolves them against the weapon and sight catalogs and falls back per
    /// field, so a stale file never blocks a match.
    /// </summary>
    public sealed class ArmoryProfile
    {
        public const string SchemaId = "shooter.armory.v1";
        public const int Schema = 1;
        public const int MaxSights = 64;
        public const int MaxIdLength = 64;
        public const string DefaultPrimary = "weapons/m16";
        public const string DefaultSidearm = "weapons/g17";

        private readonly SortedDictionary<string, string> sights;

        public string Primary { get; }
        public string Sidearm { get; }
        /// <summary>Weapon id → sight id, ordinal key order.</summary>
        public IReadOnlyDictionary<string, string> Sights => sights;

        public ArmoryProfile(string primary, string sidearm, IReadOnlyDictionary<string, string>? sights = null)
        {
            Primary = RequireId(primary, nameof(primary));
            Sidearm = RequireId(sidearm, nameof(sidearm));
            this.sights = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (sights != null)
            {
                if (sights.Count > MaxSights) throw new ArgumentException("Too many sight choices.", nameof(sights));
                foreach (var pair in sights) this.sights[RequireId(pair.Key, nameof(sights))] = RequireId(pair.Value, nameof(sights));
            }
        }

        public static ArmoryProfile Defaults => new ArmoryProfile(DefaultPrimary, DefaultSidearm);

        /// <summary>The sight saved for <paramref name="weapon"/>, or null when none was chosen.</summary>
        public string? SightFor(string weapon) => weapon != null && sights.TryGetValue(weapon, out var sight) ? sight : null;

        public ArmoryProfile WithPrimary(string primary) => new ArmoryProfile(primary, Sidearm, sights);

        public ArmoryProfile WithSidearm(string sidearm) => new ArmoryProfile(Primary, sidearm, sights);

        public ArmoryProfile WithSight(string weapon, string sight)
        {
            var copy = new Dictionary<string, string>(sights, StringComparer.Ordinal) { [RequireId(weapon, nameof(weapon))] = RequireId(sight, nameof(sight)) };
            return new ArmoryProfile(Primary, Sidearm, copy);
        }

        public bool SameAs(ArmoryProfile? other)
        {
            if (other == null || other.Primary != Primary || other.Sidearm != Sidearm || other.sights.Count != sights.Count) return false;
            foreach (var pair in sights)
                if (!other.sights.TryGetValue(pair.Key, out var value) || value != pair.Value) return false;
            return true;
        }

        /// <summary>Lower-case <c>namespace/name</c> ids (the DefinitionId alphabet), at most 64 characters.</summary>
        public static bool IsId(string? value)
        {
            if (string.IsNullOrEmpty(value) || value!.Length > MaxIdLength || value[0] == '/' || value[value.Length - 1] == '/' || value.Contains("//")) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '/' || c == '-' || c == '_' || c == '.')) return false;
            foreach (string part in value.Split('/'))
                if (part == "." || part == "..") return false;
            return value.IndexOf('/') > 0;
        }

        private static string RequireId(string value, string name)
        {
            if (!IsId(value)) throw new ArgumentException("Armory ids are lower-case namespace/name text.", name);
            return value;
        }
    }

    /// <summary>ADR-107 Armory persistence port (file in the product, memory in tests).</summary>
    public interface IArmoryStore
    {
        GameOperationResult Load(out ArmoryProfile profile);
        GameOperationResult Save(ArmoryProfile profile);
    }

    public sealed class MemoryArmoryStore : IArmoryStore
    {
        public ArmoryProfile? Stored { get; private set; }
        public int SaveCount { get; private set; }

        public GameOperationResult Load(out ArmoryProfile profile)
        {
            profile = Stored ?? ArmoryProfile.Defaults;
            return GameOperationResult.Ok;
        }

        public GameOperationResult Save(ArmoryProfile profile)
        {
            Stored = profile ?? throw new ArgumentNullException(nameof(profile));
            SaveCount++;
            return GameOperationResult.Ok;
        }
    }
}
