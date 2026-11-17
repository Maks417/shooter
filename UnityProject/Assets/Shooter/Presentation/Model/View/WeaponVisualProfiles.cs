#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Hud;

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
        /// <summary>Bank voice played for this weapon's ShotAccepted events (ADR-077).</summary>
        public SynthVoiceKind ShotVoice { get; }
        /// <summary>Kill-feed silhouette for kills made with this weapon (ADR-077).</summary>
        public KillFeedWeapon KillFeedGlyph { get; }
        public float NearFieldScale { get; }
        public Float3 NearFieldHipBias { get; }
        public Float3 HipRotationDegrees { get; }
        public float CycleTravelMeters { get; }
        public string DisplayLabel { get; }
        public string MuzzleFx { get; }
        /// <summary>Authoritative per-shot muzzle climb (degrees) copied from the weapon definition file.</summary>
        public float RecoilPitchDegrees { get; }
        /// <summary>Authoritative per-shot horizontal half-range (degrees) copied from the weapon definition file.</summary>
        public float RecoilYawHalfDegrees { get; }
        /// <summary>Authoritative draw window (ticks) copied from the definition file; times the switch choreography (ADR-099).</summary>
        public uint EquipTicks { get; }
        /// <summary>Authoritative ticks between shots/swings; times the bolt cycle and knife swing (ADR-099).</summary>
        public uint FireIntervalTicks { get; }
        /// <summary>Authoritative reload window (ticks); the reload choreography is phase-normalized to it (ADR-099).</summary>
        public uint ReloadTicks { get; }
        /// <summary>Authoritative grenade fuse (ticks); drives the cook tremble (ADR-099).</summary>
        public uint FuseTicks { get; }
        /// <summary>Seconds for the cosmetic ADS raise (ADR-099). Optional visual key, default 0.15.</summary>
        public float AdsSeconds { get; }

        public float EquipSeconds => EquipTicks / ProductAuthorityActionView.SimulationHz;
        public float FireIntervalSeconds => FireIntervalTicks / ProductAuthorityActionView.SimulationHz;
        public float ReloadSeconds => ReloadTicks / ProductAuthorityActionView.SimulationHz;
        public float FuseSeconds => FuseTicks / ProductAuthorityActionView.SimulationHz;

        public WeaponVisualProfile(
            DefinitionId definitionId,
            WeaponVisualFamily family,
            string meshAssetKey,
            WeaponOpticMode opticMode,
            bool hasMagazineCycle,
            SynthVoiceKind shotVoice,
            KillFeedWeapon killFeedGlyph,
            float nearFieldScale,
            Float3 nearFieldHipBias,
            Float3 hipRotationDegrees,
            float cycleTravelMeters,
            string displayLabel, string muzzleFx = "rifle",
            float recoilPitchDegrees = 0f, float recoilYawHalfDegrees = 0f,
            uint equipTicks = 0, uint fireIntervalTicks = 0, uint reloadTicks = 0, uint fuseTicks = 0,
            float adsSeconds = DefaultAdsSeconds)
        {
            if (!definitionId.IsValid) throw new ArgumentException("Definition id required.", nameof(definitionId));
            if (string.IsNullOrEmpty(meshAssetKey)) throw new ArgumentException("Mesh asset key required.", nameof(meshAssetKey));
            DefinitionId = definitionId;
            Family = family;
            MeshAssetKey = meshAssetKey;
            OpticMode = opticMode;
            HasMagazineCycle = hasMagazineCycle;
            if (killFeedGlyph == KillFeedWeapon.None) throw new ArgumentException("Kill-feed glyph required.", nameof(killFeedGlyph));
            ShotVoice = shotVoice;
            KillFeedGlyph = killFeedGlyph;
            NearFieldScale = nearFieldScale;
            NearFieldHipBias = nearFieldHipBias;
            HipRotationDegrees = hipRotationDegrees;
            CycleTravelMeters = cycleTravelMeters;
            DisplayLabel = displayLabel ?? string.Empty;
            if(muzzleFx!="rifle"&&muzzleFx!="pistol"&&muzzleFx!="carbine"&&muzzleFx!="none")throw new ArgumentException("Unknown muzzle FX profile.");
            MuzzleFx=muzzleFx;
            if (!(recoilPitchDegrees >= 0f && recoilPitchDegrees <= 45f)) throw new ArgumentOutOfRangeException(nameof(recoilPitchDegrees));
            if (!(recoilYawHalfDegrees >= 0f && recoilYawHalfDegrees <= 45f)) throw new ArgumentOutOfRangeException(nameof(recoilYawHalfDegrees));
            RecoilPitchDegrees = recoilPitchDegrees;
            RecoilYawHalfDegrees = recoilYawHalfDegrees;
            if (!(adsSeconds >= 0.05f && adsSeconds <= 1f)) throw new ArgumentOutOfRangeException(nameof(adsSeconds));
            EquipTicks = equipTicks;
            FireIntervalTicks = fireIntervalTicks;
            ReloadTicks = reloadTicks;
            FuseTicks = fuseTicks;
            AdsSeconds = adsSeconds;
        }

        public const float DefaultAdsSeconds = 0.15f;
    }

    /// <summary>Frozen presentation catalog parallel to Simulation WeaponCatalog ids; generated from the same definition files.</summary>
    public static partial class WeaponVisualProfiles
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
            // Rows come from Content/Weapons/*.weapon.json via Tools/generate_weapon_catalog.py (ADR-077).
            AllProfiles = CreateGeneratedProfiles();
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
