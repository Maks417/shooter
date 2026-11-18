#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    /// <summary>
    /// ADR-107: the rail-mounted sights one primary gun can carry. Rows are generated from the <c>visual.sights</c>
    /// block of <c>Content/Weapons/*.weapon.json</c> (Tools/generate_weapon_catalog.py).
    /// </summary>
    public sealed class WeaponSightOptions
    {
        public DefinitionId WeaponId { get; }
        /// <summary>geometry.weapon archetype whose rail the sight mounts on (ak, ar, dmr, bolt).</summary>
        public string MountKind { get; }
        public DefinitionId Default { get; }
        public IReadOnlyList<DefinitionId> Allowed { get; }

        public WeaponSightOptions(DefinitionId weaponId, string mountKind, DefinitionId defaultSight, DefinitionId[] allowed)
        {
            if (!weaponId.IsValid) throw new ArgumentException("Weapon id required.", nameof(weaponId));
            if (string.IsNullOrEmpty(mountKind)) throw new ArgumentException("Mount kind required.", nameof(mountKind));
            if (allowed == null || allowed.Length == 0) throw new ArgumentException("At least one sight required.", nameof(allowed));
            foreach (var sight in allowed)
                if (!SightCatalog.IsSight(sight)) throw new ArgumentException("Unknown sight " + sight + ".", nameof(allowed));
            if (Array.IndexOf(allowed, defaultSight) < 0) throw new ArgumentException("Default sight must be allowed.", nameof(defaultSight));
            WeaponId = weaponId;
            MountKind = mountKind;
            Default = defaultSight;
            Allowed = (DefinitionId[])allowed.Clone();
        }

        public bool Allows(DefinitionId sight)
        {
            for (int i = 0; i < Allowed.Count; i++)
                if (Allowed[i].Equals(sight)) return true;
            return false;
        }
    }

    /// <summary>
    /// ADR-107 sight ids and per-weapon options. A sight never changes combat rules (spread, damage, recoil): it is a
    /// loadout choice carried by the authority so every client draws the same optic, and presentation reads it for the
    /// mounted mesh, ADS framing and reticle. Guns without options (sidearms, legacy test rifles) use their own irons.
    /// </summary>
    public static partial class SightCatalog
    {
        public static DefinitionId Irons { get; } = new DefinitionId("sights/irons");
        public static DefinitionId Collimator { get; } = new DefinitionId("sights/collimator");
        public static DefinitionId Prism2x { get; } = new DefinitionId("sights/scope2");
        public static DefinitionId Scope4x { get; } = new DefinitionId("sights/scope4");
        public static DefinitionId Scope8x { get; } = new DefinitionId("sights/scope8");

        /// <summary>Every sight in loadout order.</summary>
        public static IReadOnlyList<DefinitionId> AllSights { get; } = new[] { Irons, Collimator, Prism2x, Scope4x, Scope8x };

        private static readonly WeaponSightOptions[] Options = CreateGeneratedOptions();
        private static readonly Dictionary<DefinitionId, WeaponSightOptions> ByWeapon = Index(Options);

        public static IReadOnlyList<WeaponSightOptions> All => Options;

        public static bool IsSight(DefinitionId sight)
        {
            if (!sight.IsValid) return false;
            for (int i = 0; i < AllSights.Count; i++)
                if (AllSights[i].Equals(sight)) return true;
            return false;
        }

        /// <summary>Bare sight name used by geometry and mesh keys (<c>sights/collimator</c> → <c>collimator</c>).</summary>
        public static string NameOf(DefinitionId sight)
        {
            if (!IsSight(sight)) throw new ArgumentException("Unknown sight.", nameof(sight));
            string id = sight.ToString();
            return id.Substring(id.IndexOf('/') + 1);
        }

        public static bool TryGetOptions(DefinitionId weaponId, out WeaponSightOptions options)
        {
            options = null!;
            return weaponId.IsValid && ByWeapon.TryGetValue(weaponId, out options!);
        }

        /// <summary>The weapon's factory sight, or an invalid id when the gun takes no mounted sight.</summary>
        public static DefinitionId DefaultFor(DefinitionId weaponId) =>
            TryGetOptions(weaponId, out var options) ? options.Default : default;

        public static bool IsAllowed(DefinitionId weaponId, DefinitionId sight) =>
            TryGetOptions(weaponId, out var options) && options.Allows(sight);

        /// <summary>
        /// <paramref name="sight"/> when <paramref name="weaponId"/> allows it, else that weapon's factory sight; an
        /// invalid id for guns without options.
        /// </summary>
        public static DefinitionId Resolve(DefinitionId weaponId, DefinitionId sight)
        {
            if (!TryGetOptions(weaponId, out var options)) return default;
            return options.Allows(sight) ? sight : options.Default;
        }

        /// <summary>Uniform deterministic pick among the weapon's allowed sights (bots, ADR-107).</summary>
        public static DefinitionId Pick(DefinitionId weaponId, ulong roll)
        {
            if (!TryGetOptions(weaponId, out var options)) return default;
            return options.Allowed[(int)(roll % (ulong)options.Allowed.Count)];
        }

        private static Dictionary<DefinitionId, WeaponSightOptions> Index(WeaponSightOptions[] options)
        {
            var map = new Dictionary<DefinitionId, WeaponSightOptions>();
            foreach (var option in options)
            {
                if (!WeaponCatalog.TryGet(option.WeaponId, out var entry) || entry.Family != WeaponFamily.PrimaryGun)
                    throw new InvalidOperationException("Sight options need a primary gun: " + option.WeaponId);
                map.Add(option.WeaponId, option);
            }
            return map;
        }
    }
}
