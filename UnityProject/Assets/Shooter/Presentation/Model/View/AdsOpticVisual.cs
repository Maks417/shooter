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
            float ratio = UsesScopedOptic(profile) ? (42f / 80f)
                : UsesIronSightOptic(profile) ? (52f / 80f)
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
