#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    public enum WeaponOpticMode : byte
    {
        None = 0,
        ScopedPip = 1,
        Irons = 2
    }

    public enum WeaponVisualFamily : byte
    {
        PrimaryGun = 0,
        Sidearm = 1,
        Melee = 2,
        Throwable = 3
    }

    /// <summary>
    /// Presentation row keyed by DefinitionId (ADR-072). Mesh/optic/audio/handling resolve here — not from slot index.
    /// </summary>
    public readonly struct WeaponVisualProfile
    {
        public DefinitionId DefinitionId { get; }
        public WeaponVisualFamily Family { get; }
        public string MeshAssetKey { get; }
        public WeaponOpticMode OpticMode { get; }
        public bool HasMagazineCycle { get; }
        public string AudioShotKey { get; }
        public float NearFieldScale { get; }
        public Float3 NearFieldHipBias { get; }
        public Float3 HipRotationDegrees { get; }
        public float CycleTravelMeters { get; }
        public string DisplayLabel { get; }

        public WeaponVisualProfile(
            DefinitionId definitionId,
            WeaponVisualFamily family,
            string meshAssetKey,
            WeaponOpticMode opticMode,
            bool hasMagazineCycle,
            string audioShotKey,
            float nearFieldScale,
            Float3 nearFieldHipBias,
            Float3 hipRotationDegrees,
            float cycleTravelMeters,
            string displayLabel)
        {
            if (!definitionId.IsValid) throw new ArgumentException("Definition id required.", nameof(definitionId));
            if (string.IsNullOrEmpty(meshAssetKey)) throw new ArgumentException("Mesh asset key required.", nameof(meshAssetKey));
            DefinitionId = definitionId;
            Family = family;
            MeshAssetKey = meshAssetKey;
            OpticMode = opticMode;
            HasMagazineCycle = hasMagazineCycle;
            AudioShotKey = audioShotKey ?? string.Empty;
            NearFieldScale = nearFieldScale;
            NearFieldHipBias = nearFieldHipBias;
            HipRotationDegrees = hipRotationDegrees;
            CycleTravelMeters = cycleTravelMeters;
            DisplayLabel = displayLabel ?? string.Empty;
        }
    }

    /// <summary>Frozen presentation catalog parallel to Simulation WeaponCatalog ids.</summary>
    public static class WeaponVisualProfiles
    {
        public const string MeshKeyRifle = "rifle";
        public const string MeshKeyRifleCarbine = "rifle-carbine";
        public const string MeshKeyPistol = "pistol";
        public const string MeshKeyKnife = "knife";
        public const string MeshKeyGrenade = "grenade";

        private static readonly Dictionary<string, WeaponVisualProfile> ById;
        private static readonly WeaponVisualProfile[] AllProfiles;

        static WeaponVisualProfiles()
        {
            AllProfiles = new[]
            {
                new WeaponVisualProfile(
                    new DefinitionId("weapons/rifle"), WeaponVisualFamily.PrimaryGun, MeshKeyRifle,
                    WeaponOpticMode.ScopedPip, true, "weapons/rifle",
                    1.12f, new Float3(0.08f, 0.02f, -0.10f), new Float3(0, -18f, -5f), 0.070f, "Rifle"),
                new WeaponVisualProfile(
                    new DefinitionId("weapons/rifle-carbine"), WeaponVisualFamily.PrimaryGun, MeshKeyRifleCarbine,
                    WeaponOpticMode.ScopedPip, true, "weapons/rifle-carbine",
                    1.10f, new Float3(0.08f, 0.02f, -0.09f), new Float3(0, -16f, -4f), 0.060f, "Carbine"),
                new WeaponVisualProfile(
                    new DefinitionId("weapons/pistol"), WeaponVisualFamily.Sidearm, MeshKeyPistol,
                    WeaponOpticMode.Irons, true, "weapons/pistol",
                    1.15f, new Float3(0.08f, -0.085f, 0.11f), new Float3(0, -3f, -2f), 0.038f, "Sidearm"),
                new WeaponVisualProfile(
                    new DefinitionId("weapons/knife"), WeaponVisualFamily.Melee, MeshKeyKnife,
                    WeaponOpticMode.None, false, "weapons/knife",
                    1.08f, new Float3(0.10f, -0.10f, 0.16f), new Float3(0, -3f, -2f), 0f, "Knife"),
                new WeaponVisualProfile(
                    new DefinitionId("weapons/grenade"), WeaponVisualFamily.Throwable, MeshKeyGrenade,
                    WeaponOpticMode.None, false, "weapons/grenade",
                    1.08f, new Float3(0.09f, -0.09f, 0.14f), new Float3(0, -3f, -2f), 0f, "Grenade")
            };
            ById = new Dictionary<string, WeaponVisualProfile>(AllProfiles.Length, StringComparer.Ordinal);
            for (int i = 0; i < AllProfiles.Length; i++)
                ById.Add(AllProfiles[i].DefinitionId.ToString(), AllProfiles[i]);
        }

        public static IReadOnlyList<WeaponVisualProfile> All => AllProfiles;

        public static bool TryGet(DefinitionId id, out WeaponVisualProfile profile)
        {
            profile = default;
            if (!id.IsValid) return false;
            return ById.TryGetValue(id.ToString(), out profile);
        }

        public static WeaponVisualProfile Require(DefinitionId id)
        {
            if (!TryGet(id, out var profile))
                throw new ArgumentException("Unknown weapon visual profile", nameof(id));
            return profile;
        }

        /// <summary>Resolve by DefinitionId when present; otherwise legacy slot→default-id mapping.</summary>
        public static WeaponVisualProfile Resolve(DefinitionId id, byte loadoutSlot)
        {
            if (TryGet(id, out var profile)) return profile;
            return Require(DefaultIdForSlot(loadoutSlot));
        }

        public static DefinitionId DefaultIdForSlot(byte slot) => slot switch
        {
            ProductAuthorityActionView.PistolSlot => new DefinitionId("weapons/pistol"),
            ProductAuthorityActionView.KnifeSlot => new DefinitionId("weapons/knife"),
            ProductAuthorityActionView.GrenadeSlot => new DefinitionId("weapons/grenade"),
            _ => new DefinitionId("weapons/rifle")
        };

        public static bool IsGun(in WeaponVisualProfile profile) =>
            profile.Family == WeaponVisualFamily.PrimaryGun || profile.Family == WeaponVisualFamily.Sidearm;
    }
}
