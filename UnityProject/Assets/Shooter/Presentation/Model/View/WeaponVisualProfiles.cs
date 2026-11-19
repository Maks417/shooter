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
        Irons = 2,
        /// <summary>ADR-107 red-dot collimator (1.2x since ADR-117): the gun stays in view and a lit dot marks the aim point.</summary>
        Collimator = 3
    }

    /// <summary>
    /// ADR-102 first-person handling: which reload, charging and cycle choreography a gun uses. Auto follows the family
    /// (primary guns handle like the AR rifle, sidearms like the pistol).
    /// </summary>
    public enum WeaponHandlingStyle : byte
    {
        Auto = 0,
        Rifle = 1,
        Ak = 2,
        Bolt = 3,
        Pistol = 4
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
        /// <summary>Resolved choreography style (ADR-102); never <see cref="WeaponHandlingStyle.Auto"/>.</summary>
        public WeaponHandlingStyle HandlingStyle { get; }
        /// <summary>Camera field of view at full ADS for an 80 degree hip view; 0 keeps the optic-mode default (ADR-102).</summary>
        public float AdsFovDegrees { get; }
        /// <summary>Muzzle flash size relative to the classic rifle flash (ADR-102).</summary>
        public float MuzzleFlashScale { get; }
        /// <summary>Muzzle brake: two extra flame jets vent sideways.</summary>
        public bool MuzzleFlashSideJets { get; }

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
            float adsSeconds = DefaultAdsSeconds, WeaponHandlingStyle handlingStyle = WeaponHandlingStyle.Auto,
            float adsFovDegrees = 0f, float muzzleFlashScale = -1f, bool muzzleFlashSideJets = false)
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
            HandlingStyle = handlingStyle != WeaponHandlingStyle.Auto ? handlingStyle
                : family == WeaponVisualFamily.Sidearm ? WeaponHandlingStyle.Pistol : WeaponHandlingStyle.Rifle;
            if (!(adsFovDegrees == 0f || adsFovDegrees >= 5f && adsFovDegrees <= 80f)) throw new ArgumentOutOfRangeException(nameof(adsFovDegrees));
            AdsFovDegrees = adsFovDegrees;
            // Legacy profiles size the flash from the muzzleFx name (presenter start sizes .65 / .42 / .54 m).
            if (muzzleFlashScale < 0f) muzzleFlashScale = muzzleFx == "pistol" ? .65f : muzzleFx == "carbine" ? .83f : muzzleFx == "none" ? 0f : 1f;
            if (!(muzzleFlashScale >= 0f && muzzleFlashScale <= 2.5f)) throw new ArgumentOutOfRangeException(nameof(muzzleFlashScale));
            MuzzleFlashScale = muzzleFlashScale;
            MuzzleFlashSideJets = muzzleFlashSideJets;
        }

        /// <summary>ADR-107: the same gun presenting through a different mounted sight (optic mode and ADS field of view).</summary>
        public WeaponVisualProfile WithOptic(WeaponOpticMode opticMode, float adsFovDegrees) =>
            new WeaponVisualProfile(DefinitionId, Family, MeshAssetKey, opticMode, HasMagazineCycle, ShotVoice, KillFeedGlyph,
                NearFieldScale, NearFieldHipBias, HipRotationDegrees, CycleTravelMeters, DisplayLabel, MuzzleFx,
                RecoilPitchDegrees, RecoilYawHalfDegrees, EquipTicks, FireIntervalTicks, ReloadTicks, FuseTicks, AdsSeconds,
                HandlingStyle, adsFovDegrees, MuzzleFlashScale, MuzzleFlashSideJets);

        /// <summary>
        /// ADR-108: the same gun with the local player's career handling buffs, so camera recoil and the reload/equip
        /// choreography match the authority (same per-mille rounding as the simulation). Knives and grenades are unchanged.
        /// </summary>
        public WeaponVisualProfile WithHandling(WeaponHandlingScale handling)
        {
            if (handling.IsIdentity || (Family != WeaponVisualFamily.PrimaryGun && Family != WeaponVisualFamily.Sidearm)) return this;
            return new WeaponVisualProfile(DefinitionId, Family, MeshAssetKey, OpticMode, HasMagazineCycle, ShotVoice, KillFeedGlyph,
                NearFieldScale, NearFieldHipBias, HipRotationDegrees, CycleTravelMeters, DisplayLabel, MuzzleFx,
                RecoilPitchDegrees * handling.RecoilPerMille / 1000f, RecoilYawHalfDegrees * handling.RecoilPerMille / 1000f,
                WeaponHandlingScale.ScaleTicks(EquipTicks, handling.EquipPerMille), FireIntervalTicks,
                WeaponHandlingScale.ScaleTicks(ReloadTicks, handling.ReloadPerMille), FuseTicks, AdsSeconds,
                HandlingStyle, AdsFovDegrees, MuzzleFlashScale, MuzzleFlashSideJets);
        }

        /// <summary>Pool variant byte carrying the flash size in hundredths (10..250); 0 means no flash.</summary>
        public byte MuzzleFlashVariant => MuzzleFx == "none" || MuzzleFlashScale <= 0f ? (byte)0
            : (byte)Math.Max(10, Math.Min(250, (int)Math.Round(MuzzleFlashScale * 100f)));

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
