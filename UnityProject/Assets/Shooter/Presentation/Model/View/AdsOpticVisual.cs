#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Pure ADS optic layout: scoped PiP (digital zoom + blur) + iron vignette.
    /// Optic mode comes from <see cref="WeaponVisualProfiles"/> (ADR-072), not slot index alone.
    /// </summary>
    public static class AdsOpticVisual
    {
        public const float HipReticleHideBlend = 0.18f;
        public const float RifleMeshHideBlend = 0.88f;
        public const float FullMaskBlend = 0.92f;

        public static float OpticStrength(float adsPresentationBlend)
        {
            float t = Math.Clamp(adsPresentationBlend, 0f, 1f);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        public static bool UsesScopedOptic(in WeaponVisualProfile profile) =>
            profile.OpticMode == WeaponOpticMode.ScopedPip;

        public static bool UsesIronSightOptic(in WeaponVisualProfile profile) =>
            profile.OpticMode == WeaponOpticMode.Irons;

        /// <summary>ADR-107 red-dot collimator: the gun stays in view, the camera narrows slightly and a dot marks the aim point.</summary>
        public static bool UsesCollimatorOptic(in WeaponVisualProfile profile) =>
            profile.OpticMode == WeaponOpticMode.Collimator;

        /// <summary>Blend at which the collimator dot is lit (the sight has risen to the eye).</summary>
        public const float CollimatorDotBlend = 0.8f;

        /// <summary>Collimator dot opacity: off until the sight reaches the eye, then fades in quickly.</summary>
        public static float CollimatorDotOpacity(in WeaponVisualProfile profile, float adsPresentationBlend)
        {
            if (!UsesCollimatorOptic(profile)) return 0f;
            float s = OpticStrength(adsPresentationBlend);
            if (s < CollimatorDotBlend) return 0f;
            return Math.Min(1f, (s - CollimatorDotBlend) / (1f - CollimatorDotBlend) * 1.6f);
        }

        /// <summary>Collimator dot radius in pixels: one small point of fixed angular size (ADR-117: about 3 px at 1080p).</summary>
        public static float CollimatorDotRadiusPixels(float height) => Math.Max(1.5f, height * 0.0030f);

        /// <summary>ADR-117: the collimator magnifies 1.2x, so its ADS field of view is the 80° reference divided by 1.2.</summary>
        public const float CollimatorMagnification = 1.2f;
        public const float CollimatorAdsFovDegrees = 80f / CollimatorMagnification;

        public static bool UsesScopedOptic(DefinitionId weaponId, byte weaponSlot) =>
            UsesScopedOptic(WeaponVisualProfiles.Resolve(weaponId, weaponSlot));

        public static bool UsesIronSightOptic(DefinitionId weaponId, byte weaponSlot) =>
            UsesIronSightOptic(WeaponVisualProfiles.Resolve(weaponId, weaponSlot));

        /// <summary>Legacy slot-only resolve (default id for slot).</summary>
        public static bool UsesScopedOptic(byte weaponSlot) =>
            UsesScopedOptic(WeaponVisualProfiles.Resolve(default, weaponSlot));

        public static bool UsesIronSightOptic(byte weaponSlot) =>
            UsesIronSightOptic(WeaponVisualProfiles.Resolve(default, weaponSlot));

        public static bool HideHipReticle(float adsPresentationBlend) =>
            OpticStrength(adsPresentationBlend) >= HipReticleHideBlend;

        public static bool HideRifleViewModel(in WeaponVisualProfile profile, float adsPresentationBlend) =>
            UsesScopedOptic(profile) && OpticStrength(adsPresentationBlend) >= RifleMeshHideBlend;

        public static bool HideRifleViewModel(DefinitionId weaponId, byte weaponSlot, float adsPresentationBlend) =>
            HideRifleViewModel(WeaponVisualProfiles.Resolve(weaponId, weaponSlot), adsPresentationBlend);

        public static bool HideRifleViewModel(byte weaponSlot, float adsPresentationBlend) =>
            HideRifleViewModel(WeaponVisualProfiles.Resolve(default, weaponSlot), adsPresentationBlend);

        public static float ScopeMaskOpacity(float adsPresentationBlend)
        {
            float s = OpticStrength(adsPresentationBlend);
            if (s < 0.35f) return 0f;
            float u = (s - 0.35f) / (FullMaskBlend - 0.35f);
            if (u > 1f) u = 1f;
            return u * u;
        }

        public static float PipStrength(float adsPresentationBlend, bool reducedCameraMotion)
        {
            if (reducedCameraMotion) return 0f;
            return ScopeMaskOpacity(adsPresentationBlend);
        }

        public static float IronVignetteOpacity(float adsPresentationBlend)
        {
            float s = OpticStrength(adsPresentationBlend);
            if (s < 0.25f) return 0f;
            return Math.Min(1f, (s - 0.25f) / 0.55f) * 0.55f;
        }

        public static float AdsFieldOfViewDegrees(float hipFovDegrees, in WeaponVisualProfile profile)
        {
            if (!float.IsFinite(hipFovDegrees) || hipFovDegrees < 1f)
                throw new ArgumentOutOfRangeException(nameof(hipFovDegrees));
            // ADR-102: magnified optics state their own field of view (2x, 4x, 8x).
            if (profile.AdsFovDegrees > 0f) return hipFovDegrees * (profile.AdsFovDegrees / 80f);
            float ratio = UsesScopedOptic(profile) ? (42f / 80f)
                : UsesIronSightOptic(profile) ? (52f / 80f)
                : UsesCollimatorOptic(profile) ? (1f / CollimatorMagnification)
                : (58f / 80f);
            return hipFovDegrees * ratio;
        }

        public static float AdsFieldOfViewDegrees(float hipFovDegrees, DefinitionId weaponId, byte weaponSlot) =>
            AdsFieldOfViewDegrees(hipFovDegrees, WeaponVisualProfiles.Resolve(weaponId, weaponSlot));

        public static float AdsFieldOfViewDegrees(float hipFovDegrees, byte weaponSlot) =>
            AdsFieldOfViewDegrees(hipFovDegrees, WeaponVisualProfiles.Resolve(default, weaponSlot));

        public static float BlendedFieldOfViewDegrees(float hipFovDegrees, float adsFovDegrees, float adsPresentationBlend)
        {
            if (!float.IsFinite(hipFovDegrees) || !float.IsFinite(adsFovDegrees))
                throw new ArgumentOutOfRangeException(nameof(hipFovDegrees));
            float s = OpticStrength(adsPresentationBlend);
            return hipFovDegrees + (adsFovDegrees - hipFovDegrees) * s;
        }

        public static float LensUvZoom(float hipFovDegrees, in WeaponVisualProfile profile, float adsPresentationBlend)
        {
            float ads = AdsFieldOfViewDegrees(hipFovDegrees, profile);
            float full = ads / hipFovDegrees;
            float s = OpticStrength(adsPresentationBlend);
            return 1f + (full - 1f) * s;
        }

        public static float LensUvZoom(float hipFovDegrees, byte weaponSlot, float adsPresentationBlend) =>
            LensUvZoom(hipFovDegrees, WeaponVisualProfiles.Resolve(default, weaponSlot), adsPresentationBlend);

        /// <summary>
        /// ADR-103 optical lens camera: vertical FOV that shows exactly what the digital lens zoom would, so the lens
        /// renders the scene at native resolution (with narrow-FOV LOD, mips and shadows) instead of stretching pixels.
        /// The lens spans 2 * lensRadiusFraction of the screen height and shows hip-view height scaled by lensUvZoom.
        /// </summary>
        public static float LensCameraFovDegrees(float hipFovDegrees, float lensRadiusFraction, float lensUvZoom)
        {
            if (!float.IsFinite(hipFovDegrees) || hipFovDegrees <= 1f || hipFovDegrees >= 179f)
                throw new ArgumentOutOfRangeException(nameof(hipFovDegrees));
            if (!float.IsFinite(lensRadiusFraction) || lensRadiusFraction <= 0f || lensRadiusFraction > 1f)
                throw new ArgumentOutOfRangeException(nameof(lensRadiusFraction));
            if (!float.IsFinite(lensUvZoom) || lensUvZoom <= 0f || lensUvZoom > 1f)
                throw new ArgumentOutOfRangeException(nameof(lensUvZoom));
            double halfTan = Math.Tan(hipFovDegrees * Math.PI / 360.0) * 2.0 * lensRadiusFraction * lensUvZoom;
            return (float)(Math.Atan(halfTan) * 360.0 / Math.PI);
        }

        /// <summary>ADR-103: the optical lens camera is used once the near-field rifle is hidden (clean lens).</summary>
        public static bool UsesLensCamera(in WeaponVisualProfile profile, float adsPresentationBlend, bool reducedCameraMotion) =>
            !reducedCameraMotion && HideRifleViewModel(profile, adsPresentationBlend);

        public static bool KeepsHipCameraFov(in WeaponVisualProfile profile) => UsesScopedOptic(profile);

        public static bool KeepsHipCameraFov(byte weaponSlot) =>
            KeepsHipCameraFov(WeaponVisualProfiles.Resolve(default, weaponSlot));

        public const float LensRadiusFraction = 0.42f;

        public static float LensRadiusPixels(float width, float height)
        {
            if (width < 1f || height < 1f) throw new ArgumentOutOfRangeException(nameof(width));
            return Math.Min(width, height) * LensRadiusFraction;
        }

        public static float MilDotSpacingPixels(float lensRadius) =>
            Math.Max(10f, lensRadius * 0.085f);

        public static int MilDotCount(float lensRadius)
        {
            float spacing = MilDotSpacingPixels(lensRadius);
            int n = (int)((lensRadius * 0.72f) / spacing);
            if (n < 2) n = 2;
            if (n > 6) n = 6;
            return n;
        }
    }
}
