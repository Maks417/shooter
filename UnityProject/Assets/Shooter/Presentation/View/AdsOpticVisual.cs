#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Pure ADS optic layout: rifle scope mask + mil-dot math, pistol iron-sight vignette.
    /// Presentation-only — does not change aim input, FOV settings schema, or ballistics.
    /// </summary>
    public static class AdsOpticVisual
    {
        /// <summary>Blend above which the hip reticle yields to weapon/optic aim.</summary>
        public const float HipReticleHideBlend = 0.18f;
        /// <summary>Blend above which the rifle FP mesh hides so the scope mask reads cleanly.</summary>
        public const float RifleMeshHideBlend = 0.88f;
        /// <summary>Full optic mask opacity at this ADS blend.</summary>
        public const float FullMaskBlend = 0.92f;

        /// <summary>Smoothstep ADS strength used for FOV, mask and reticle.</summary>
        public static float OpticStrength(float adsPresentationBlend)
        {
            float t = Math.Clamp(adsPresentationBlend, 0f, 1f);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        public static bool UsesScopedOptic(byte weaponSlot) =>
            ProductAuthorityActionView.IsRifleSlot(weaponSlot);

        public static bool UsesIronSightOptic(byte weaponSlot) =>
            weaponSlot == ProductAuthorityActionView.PistolSlot;

        public static bool HideHipReticle(float adsPresentationBlend) =>
            OpticStrength(adsPresentationBlend) >= HipReticleHideBlend;

        public static bool HideRifleViewModel(byte weaponSlot, float adsPresentationBlend) =>
            UsesScopedOptic(weaponSlot) && OpticStrength(adsPresentationBlend) >= RifleMeshHideBlend;

        /// <summary>Mask opacity for the black outside-lens fill (0..1).</summary>
        public static float ScopeMaskOpacity(float adsPresentationBlend)
        {
            float s = OpticStrength(adsPresentationBlend);
            if (s < 0.35f) return 0f;
            float u = (s - 0.35f) / (FullMaskBlend - 0.35f);
            if (u > 1f) u = 1f;
            return u * u;
        }

        /// <summary>Pistol ADS edge vignette only — no circular scope tube.</summary>
        public static float IronVignetteOpacity(float adsPresentationBlend)
        {
            float s = OpticStrength(adsPresentationBlend);
            if (s < 0.25f) return 0f;
            return Math.Min(1f, (s - 0.25f) / 0.55f) * 0.55f;
        }

        /// <summary>
        /// Cosmetic ADS FOV from hip degrees. Rifle scopes tighter than pistol irons;
        /// reduced-camera-motion callers should keep hip FOV instead.
        /// </summary>
        public static float AdsFieldOfViewDegrees(float hipFovDegrees, byte weaponSlot)
        {
            if (!float.IsFinite(hipFovDegrees) || hipFovDegrees < 1f)
                throw new ArgumentOutOfRangeException(nameof(hipFovDegrees));
            float ratio = UsesScopedOptic(weaponSlot) ? (42f / 80f)
                : UsesIronSightOptic(weaponSlot) ? (52f / 80f)
                : (58f / 80f);
            return hipFovDegrees * ratio;
        }

        public static float BlendedFieldOfViewDegrees(float hipFovDegrees, float adsFovDegrees, float adsPresentationBlend)
        {
            if (!float.IsFinite(hipFovDegrees) || !float.IsFinite(adsFovDegrees))
                throw new ArgumentOutOfRangeException(nameof(hipFovDegrees));
            float s = OpticStrength(adsPresentationBlend);
            return hipFovDegrees + (adsFovDegrees - hipFovDegrees) * s;
        }

        /// <summary>Lens radius in logical HUD pixels; keeps a readable outside housing band.</summary>
        public static float LensRadiusPixels(float width, float height)
        {
            if (width < 1f || height < 1f) throw new ArgumentOutOfRangeException(nameof(width));
            return Math.Min(width, height) * 0.42f;
        }

        /// <summary>Mil-dot spacing along one reticle arm (pixels).</summary>
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
