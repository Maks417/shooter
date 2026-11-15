#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Analytic knife slash/stab and grenade cook/throw offsets. Cosmetic only —
    /// never changes aim, ammo, fuse or ballistic origins.
    /// </summary>
    public static class FirstPersonSpecialActions
    {
        public const float MeleeDurationSeconds = 0.22f;
        public const float ThrowDurationSeconds = 0.28f;

        public static Float3 MeleePositionOffset(float phase01, bool stab, bool reducedMotion)
        {
            float t = Envelope(phase01);
            if (t <= 0f) return default;
            float amp = reducedMotion ? 0.45f : 1f;
            if (stab)
                return new Float3(0.01f * t * amp, -0.02f * t * amp, 0.11f * t * amp);
            // Slash: outward then across.
            float swing = (float)Math.Sin(Math.PI * Math.Clamp(phase01, 0f, 1f));
            return new Float3(0.09f * swing * amp, -0.03f * t * amp, 0.04f * t * amp);
        }

        public static Float3 MeleeRotationDegrees(float phase01, bool stab, bool reducedMotion)
        {
            float t = Envelope(phase01);
            if (t <= 0f) return default;
            float amp = reducedMotion ? 0.45f : 1f;
            if (stab)
                return new Float3(-28f * t * amp, 8f * t * amp, -12f * t * amp);
            float swing = (float)Math.Sin(Math.PI * Math.Clamp(phase01, 0f, 1f));
            return new Float3(-18f * t * amp, 42f * swing * amp, -55f * swing * amp);
        }

        public static Float3 GrenadeCookPosition(bool cooking, bool reducedMotion)
        {
            if (!cooking) return default;
            float amp = reducedMotion ? 0.55f : 1f;
            return new Float3(0.02f * amp, 0.06f * amp, -0.02f * amp);
        }

        public static Float3 GrenadeCookRotation(bool cooking, bool reducedMotion)
        {
            if (!cooking) return default;
            float amp = reducedMotion ? 0.55f : 1f;
            return new Float3(-22f * amp, 12f * amp, 18f * amp);
        }

        public static Float3 GrenadeThrowPosition(float phase01, bool reducedMotion)
        {
            float t = Envelope(phase01);
            if (t <= 0f) return default;
            float amp = reducedMotion ? 0.5f : 1f;
            float p = Math.Clamp(phase01, 0f, 1f);
            // Wind-up down/back, then overhand release forward/up.
            float wind = p < 0.35f ? (p / 0.35f) : Math.Max(0f, 1f - (p - 0.35f) / 0.65f);
            float release = p < 0.35f ? 0f : (p - 0.35f) / 0.65f;
            return new Float3(
                0.04f * wind * amp,
                (-0.05f * wind + 0.12f * release) * amp,
                (-0.06f * wind + 0.18f * release) * amp);
        }

        public static Float3 GrenadeThrowRotation(float phase01, bool reducedMotion)
        {
            float t = Envelope(phase01);
            if (t <= 0f) return default;
            float amp = reducedMotion ? 0.5f : 1f;
            float p = Math.Clamp(phase01, 0f, 1f);
            float wind = p < 0.35f ? (p / 0.35f) : Math.Max(0f, 1f - (p - 0.35f) / 0.65f);
            float release = p < 0.35f ? 0f : (p - 0.35f) / 0.65f;
            return new Float3(
                (25f * wind - 55f * release) * amp,
                10f * wind * amp,
                (-20f * wind + 8f * release) * amp);
        }

        /// <summary>Peaks mid-phase and returns to 0 at endpoints.</summary>
        public static float Envelope(float phase01)
        {
            float p = Math.Clamp(phase01, 0f, 1f);
            if (p <= 0f || p >= 1f) return 0f;
            float mid = 1f - Math.Abs(2f * p - 1f);
            return mid * mid;
        }
    }
}
