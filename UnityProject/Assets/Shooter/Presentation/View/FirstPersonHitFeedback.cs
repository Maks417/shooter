#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Pure combat hit-feedback timing for HUD/vignette. Reads authority events only; never invents damage.
    /// </summary>
    public static class FirstPersonHitFeedback
    {
        public const float HitMarkerSeconds = 0.20f;
        public const float DamageFlashSeconds = 0.32f;
        public const float HitMarkerFadeSeconds = 0.12f;

        public static bool IsDamageTaken(CombatEventKind kind, EntityId target, EntityId localEntity) =>
            kind == CombatEventKind.DamageApplied && localEntity.IsValid && target.Equals(localEntity);

        public static bool IsDamageDealt(CombatEventKind kind, EntityId instigator, EntityId localEntity) =>
            kind == CombatEventKind.DamageApplied && localEntity.IsValid && instigator.Equals(localEntity);

        /// <summary>
        /// Local first-person muzzle overlay / shot subtitle only — remote bot ShotAccepted must not flash the screen.
        /// World muzzle mesh proxies stay driven separately from every ShotAccepted.
        /// </summary>
        public static bool IsLocalShot(CombatEventKind kind, EntityId instigator, EntityId localEntity) =>
            kind == CombatEventKind.ShotAccepted && localEntity.IsValid && instigator.Equals(localEntity);

        /// <summary>Opacity for a center hit marker given remaining display time.</summary>
        public static float HitMarkerAlpha(float remainingSeconds, float fadeSeconds = HitMarkerFadeSeconds)
        {
            if (remainingSeconds <= 0f) return 0f;
            if (fadeSeconds <= 0f) return 1f;
            if (remainingSeconds >= fadeSeconds) return 1f;
            return remainingSeconds / fadeSeconds;
        }

        /// <summary>
        /// Brief coral plate flash strength when the local player takes damage.
        /// Reduced flash keeps a quieter static cue for the same remaining window.
        /// </summary>
        public static float DamageFlashStrength(float remainingSeconds, bool reducedFlash)
        {
            if (remainingSeconds <= 0f) return 0f;
            float t = remainingSeconds / DamageFlashSeconds;
            if (t > 1f) t = 1f;
            float peak = reducedFlash ? 0.10f : 0.22f;
            return peak * t * t;
        }

        /// <summary>Optional micro shake amplitude (meters) for damage taken; zero under reduced motion/flash.</summary>
        public static float DamageShakeMeters(float remainingSeconds, bool reducedMotion, bool reducedFlash)
        {
            if (reducedMotion || reducedFlash || remainingSeconds <= 0f) return 0f;
            float t = remainingSeconds / DamageFlashSeconds;
            if (t > 1f) t = 1f;
            return 0.0045f * t;
        }
    }
}
