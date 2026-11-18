#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// ADR-107 presentation row for one rail-mounted sight: how it reads in the Armory and how ADS presents it.
    /// </summary>
    public readonly struct SightVisualProfile
    {
        public DefinitionId Id { get; }
        /// <summary>Bare geometry name (<c>collimator</c>); mesh keys are <c>sight-&lt;name&gt;-&lt;kind&gt;</c>.</summary>
        public string Name { get; }
        public string DisplayLabel { get; }
        /// <summary>One-line Armory effect text (zoom and what stays in view).</summary>
        public string Summary { get; }
        public WeaponOpticMode OpticMode { get; }
        /// <summary>Camera field of view at full ADS for an 80 degree hip view (ADR-102 convention).</summary>
        public float AdsFovDegrees { get; }
        /// <summary>Approximate magnification shown in the Armory (1 for irons and the collimator).</summary>
        public float Magnification { get; }

        public SightVisualProfile(DefinitionId id, string name, string displayLabel, string summary, WeaponOpticMode opticMode,
            float adsFovDegrees, float magnification)
        {
            if (!id.IsValid) throw new ArgumentException("Sight id required.", nameof(id));
            if (opticMode == WeaponOpticMode.None) throw new ArgumentException("A sight has an optic mode.", nameof(opticMode));
            if (!(adsFovDegrees >= 5f && adsFovDegrees <= 80f)) throw new ArgumentOutOfRangeException(nameof(adsFovDegrees));
            Id = id; Name = name ?? throw new ArgumentNullException(nameof(name)); DisplayLabel = displayLabel ?? "";
            Summary = summary ?? ""; OpticMode = opticMode; AdsFovDegrees = adsFovDegrees; Magnification = magnification;
        }

        public string MeshKeyFor(string mountKind) => "sight-" + Name + "-" + mountKind;
    }

    /// <summary>ADR-107 sights the Armory offers on one primary gun (generated from the weapon definition files).</summary>
    public sealed class WeaponSightChoice
    {
        public DefinitionId WeaponId { get; }
        public string MountKind { get; }
        public DefinitionId Default { get; }
        public IReadOnlyList<DefinitionId> Allowed { get; }

        public WeaponSightChoice(DefinitionId weaponId, string mountKind, DefinitionId defaultSight, DefinitionId[] allowed)
        {
            if (!weaponId.IsValid) throw new ArgumentException("Weapon id required.", nameof(weaponId));
            if (allowed == null || allowed.Length == 0 || Array.IndexOf(allowed, defaultSight) < 0)
                throw new ArgumentException("Allowed sights must include the default.", nameof(allowed));
            WeaponId = weaponId; MountKind = mountKind ?? throw new ArgumentNullException(nameof(mountKind));
            Default = defaultSight; Allowed = (DefinitionId[])allowed.Clone();
        }

        public bool Allows(DefinitionId sight)
        {
            for (int i = 0; i < Allowed.Count; i++)
                if (Allowed[i].Equals(sight)) return true;
            return false;
        }

        /// <summary>Packaged mesh key of <paramref name="sight"/> on this gun, or null when the sight is part of the body (AK irons).</summary>
        public string? MeshKeyFor(DefinitionId sight)
        {
            if (!Allows(sight) || !WeaponSightChoices.TryGetSight(sight, out var profile)) return null;
            if (MountKind == "ak" && profile.Name == "irons") return null;
            return profile.MeshKeyFor(MountKind);
        }
    }

    /// <summary>ADR-107 sight presentation rows plus the per-gun choices from <c>WeaponSightChoices.g.cs</c>.</summary>
    public static partial class WeaponSightChoices
    {
        public static readonly DefinitionId IronsId = new DefinitionId("sights/irons");
        public static readonly DefinitionId CollimatorId = new DefinitionId("sights/collimator");

        private static readonly SightVisualProfile[] Sights =
        {
            new SightVisualProfile(IronsId, "irons", "IRON SIGHTS", "No zoom. Fast and uncluttered.", WeaponOpticMode.Irons, 52f, 1f),
            new SightVisualProfile(CollimatorId, "collimator", "COLLIMATOR", "Red dot, 1x. The gun stays in view.", WeaponOpticMode.Collimator, 56f, 1f),
            new SightVisualProfile(new DefinitionId("sights/scope2"), "scope2", "2X PRISM", "M16 prism scope. 2x zoom, mil-dot lens.", WeaponOpticMode.ScopedPip, 40f, 2f),
            new SightVisualProfile(new DefinitionId("sights/scope4"), "scope4", "4X SCOPE", "Marksman scope. 4x zoom, mil-dot lens.", WeaponOpticMode.ScopedPip, 20f, 4f),
            new SightVisualProfile(new DefinitionId("sights/scope8"), "scope8", "8X SCOPE", "Sniper scope. 8x zoom, mil-dot lens.", WeaponOpticMode.ScopedPip, 10f, 8f),
        };

        private static readonly WeaponSightChoice[] Choices = CreateGeneratedChoices();

        public static IReadOnlyList<SightVisualProfile> AllSights => Sights;
        public static IReadOnlyList<WeaponSightChoice> All => Choices;

        public static bool TryGetSight(DefinitionId id, out SightVisualProfile profile)
        {
            foreach (var sight in Sights)
                if (sight.Id.Equals(id)) { profile = sight; return true; }
            profile = default;
            return false;
        }

        public static bool TryGet(DefinitionId weaponId, out WeaponSightChoice choice)
        {
            foreach (var row in Choices)
                if (row.WeaponId.Equals(weaponId)) { choice = row; return true; }
            choice = null!;
            return false;
        }

        /// <summary>
        /// The profile ADS presents for <paramref name="weaponId"/> carrying <paramref name="sight"/>: optic mode and field
        /// of view come from the mounted sight. Guns without sight choices, or an unknown sight, keep their own profile.
        /// </summary>
        public static WeaponVisualProfile Apply(in WeaponVisualProfile profile, DefinitionId sight)
        {
            if (!TryGet(profile.DefinitionId, out var choice)) return profile;
            if (!choice.Allows(sight)) sight = choice.Default;
            return TryGetSight(sight, out var optic) ? profile.WithOptic(optic.OpticMode, optic.AdsFovDegrees) : profile;
        }
    }
}
