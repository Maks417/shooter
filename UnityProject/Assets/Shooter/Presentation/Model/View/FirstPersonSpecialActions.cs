#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Knife slash/stab and grenade pin-pull/cook/throw choreography as numeric key tables (ADR-064, ADR-099).
    /// Cosmetic only — never changes aim, ammo, fuse or ballistic origins.
    /// </summary>
    public static class FirstPersonSpecialActions
    {
        /// <summary>Fallback swing length when the knife definition has no fire interval.</summary>
        public const float MeleeDurationSeconds = 0.4f;
        /// <summary>Overhand release and follow-through after the authoritative ProjectileSpawned.</summary>
        public const float ThrowDurationSeconds = 0.30f;
        /// <summary>Throw phase at which the grenade leaves the hand (held prop hides).</summary>
        public const float ThrowReleasePhase = 0.12f;
        /// <summary>Seconds for the pin pull at the start of a cook.</summary>
        public const float PinPullSeconds = 0.42f;
        /// <summary>Seconds to raise the grenade into the cocked cook pose.</summary>
        public const float CookRaiseSeconds = 0.26f;

        // ADR-111 knife slash, authored for a right-to-left cut (direction +1); direction -1 mirrors X, yaw and roll.
        // Short cocked wind-up up and to the right, a fast diagonal cut across the screen (right-high to left-low),
        // a heavy follow-through below the view centre, then recovery to the low ready.
        private static readonly MotionCurve SlashPosition = new MotionCurve(
            new MotionKey(0f, 0f, 0f, 0f), new MotionKey(.10f, .05f, .09f, -.04f), new MotionKey(.20f, -.06f, .05f, .10f),
            new MotionKey(.32f, -.17f, -.03f, .08f), new MotionKey(.50f, -.20f, -.10f, .02f), new MotionKey(.75f, -.07f, -.05f, 0f),
            new MotionKey(1f, 0f, 0f, 0f));
        private static readonly MotionCurve SlashRotation = new MotionCurve(
            new MotionKey(0f, 0f, 0f, 0f), new MotionKey(.10f, -25f, 30f, -30f), new MotionKey(.20f, 0f, -35f, 40f),
            new MotionKey(.32f, 18f, -70f, 60f), new MotionKey(.50f, 25f, -78f, 55f), new MotionKey(.75f, 10f, -30f, 20f),
            new MotionKey(1f, 0f, 0f, 0f));
        // Stab (the swing that connected): pull back, drive the point forward, hold in the target (hit-stop), withdraw.
        private static readonly MotionCurve StabPosition = new MotionCurve(
            new MotionKey(0f, 0f, 0f, 0f), new MotionKey(.12f, .02f, .03f, -.08f), new MotionKey(.26f, -.03f, .01f, .20f),
            new MotionKey(.40f, -.03f, .005f, .19f), new MotionKey(.72f, -.01f, -.01f, .04f), new MotionKey(1f, 0f, 0f, 0f));
        private static readonly MotionCurve StabRotation = new MotionCurve(
            new MotionKey(0f, 0f, 0f, 0f), new MotionKey(.12f, -10f, 5f, 0f), new MotionKey(.26f, -8f, -12f, -5f),
            new MotionKey(.40f, -7f, -12f, -5f), new MotionKey(.72f, -2f, -3f, -1f), new MotionKey(1f, 0f, 0f, 0f));

        /// <summary>Phase window in which the blade trail is drawn (the cutting part of the swing).</summary>
        public const float TrailStartPhase = .10f;
        public const float TrailEndPhase = .52f;

        /// <summary>Blade-trail opacity for a melee phase: rises through the cut and fades in the follow-through.</summary>
        public static float BladeTrailStrength(float phase01, bool stab)
        {
            if (!float.IsFinite(phase01) || phase01 <= TrailStartPhase || phase01 >= TrailEndPhase) return 0f;
            float strength = MotionCurve.Ramp(phase01, TrailStartPhase, .18f) * (1f - MotionCurve.Ramp(phase01, .34f, TrailEndPhase));
            return stab ? strength * .45f : strength;
        }

        /// <summary>
        /// ADR-111: the knife and the grenade are one-handed; the support hand is shown only for the guns.
        /// The grenade pin pull is carried by its sound and the thumb tug of the throwing hand.
        /// </summary>
        public static bool SupportHandVisible(byte slot, bool cooking, float cookSeconds) =>
            ProductAuthorityActionView.IsGunSlot(slot);

        // Grenade cocked over the shoulder while cooking.
        // ADR-111: an overhand wind-up, raised up and back beside the right shoulder.
        private static readonly Float3 CookPosition = new Float3(0.02f, 0.03f, -0.04f);
        private static readonly Float3 CookRotation = new Float3(-6f, 4f, 0f);

        // Support hand during the pin pull (seconds): reach to the ring, grip, yank out, lower away.
        private static readonly MotionCurve PinHand = new MotionCurve(
            new MotionKey(0f, 0f, 0f, 0f), new MotionKey(.12f, .05f, .05f, .01f), new MotionKey(.16f, .05f, .05f, .01f),
            new MotionKey(.24f, -.06f, .02f, -.04f), new MotionKey(.42f, -.07f, -.12f, .02f));
        private static readonly MotionCurve PinOpen = new MotionCurve(
            new MotionKey(0f, 0f, 0f, 0f), new MotionKey(.08f, .9f, 0f, 0f), new MotionKey(.13f, 0f, 0f, 0f),
            new MotionKey(.28f, 0f, 0f, 0f), new MotionKey(.34f, .8f, 0f, 0f), new MotionKey(.42f, .5f, 0f, 0f));

        // Throw targets after release, from rest; the pose at release fades out over the first beat (no per-frame allocation).
        private static readonly MotionCurve ThrowPositionTargets = new MotionCurve(
            new MotionKey(0f, 0f, 0f, 0f), new MotionKey(.18f, 0f, .10f, .20f), new MotionKey(.42f, -.03f, -.10f, .16f),
            new MotionKey(1f, -.02f, -.30f, .05f));
        private static readonly MotionCurve ThrowRotationTargets = new MotionCurve(
            new MotionKey(0f, 0f, 0f, 0f), new MotionKey(.18f, -60f, -5f, 5f), new MotionKey(.42f, -75f, -10f, 0f),
            new MotionKey(1f, -40f, 0f, 0f));

        public static Float3 MeleePositionOffset(float phase01, bool stab, bool reducedMotion) =>
            MeleePositionOffset(phase01, stab, 1, reducedMotion);

        public static Float3 MeleeRotationDegrees(float phase01, bool stab, bool reducedMotion) =>
            MeleeRotationDegrees(phase01, stab, 1, reducedMotion);

        /// <param name="direction">+1 right-to-left cut, -1 left-to-right; swings alternate (ADR-099).</param>
        public static Float3 MeleePositionOffset(float phase01, bool stab, int direction, bool reducedMotion)
        {
            if (!float.IsFinite(phase01)) throw new ArgumentOutOfRangeException(nameof(phase01));
            if (phase01 <= 0f || phase01 >= 1f) return default;
            float amp = reducedMotion ? 0.45f : 1f;
            Float3 v = stab ? StabPosition.Sample(phase01) : SlashPosition.Sample(phase01);
            float side = stab || direction >= 0 ? 1f : -1f;
            return new Float3(v.X * side * amp, v.Y * amp, v.Z * amp);
        }

        public static Float3 MeleeRotationDegrees(float phase01, bool stab, int direction, bool reducedMotion)
        {
            if (!float.IsFinite(phase01)) throw new ArgumentOutOfRangeException(nameof(phase01));
            if (phase01 <= 0f || phase01 >= 1f) return default;
            float amp = reducedMotion ? 0.45f : 1f;
            Float3 v = stab ? StabRotation.Sample(phase01) : SlashRotation.Sample(phase01);
            float side = stab || direction >= 0 ? 1f : -1f;
            return new Float3(v.X * amp, v.Y * side * amp, v.Z * side * amp);
        }

        /// <summary>Raise into the cocked pose with a pin-pull jerk and a fuse tremble that grows as the fuse runs down.</summary>
        public static Float3 GrenadeCookPosition(float cookSeconds, float fuseSeconds, bool reducedMotion)
        {
            if (!float.IsFinite(cookSeconds) || cookSeconds < 0f) return default;
            float amp = reducedMotion ? 0.55f : 1f;
            float raise = MotionCurve.Ramp(cookSeconds, 0f, CookRaiseSeconds);
            // The pin comes out at ~0.2 s: a small sympathetic tug of the grenade hand.
            float tug = MotionCurve.Window(cookSeconds, .17f, .22f, .22f, .32f);
            float tremble = reducedMotion ? 0f : Tremble(cookSeconds, fuseSeconds);
            return new Float3(
                (CookPosition.X + 0.008f * tug) * raise * amp + tremble * 0.0012f,
                CookPosition.Y * raise * amp + (float)Math.Sin(cookSeconds * 13.1d) * tremble * 0.0010f,
                (CookPosition.Z - 0.01f * tug) * raise * amp);
        }

        public static Float3 GrenadeCookRotation(float cookSeconds, float fuseSeconds, bool reducedMotion)
        {
            if (!float.IsFinite(cookSeconds) || cookSeconds < 0f) return default;
            float amp = reducedMotion ? 0.55f : 1f;
            float raise = MotionCurve.Ramp(cookSeconds, 0f, CookRaiseSeconds);
            float tug = MotionCurve.Window(cookSeconds, .17f, .22f, .22f, .32f);
            float tremble = reducedMotion ? 0f : Tremble(cookSeconds, fuseSeconds);
            return new Float3(
                (CookRotation.X - 4f * tug) * raise * amp + tremble * 0.6f,
                (CookRotation.Y - 6f * tug) * raise * amp,
                CookRotation.Z * raise * amp + (float)Math.Sin(cookSeconds * 11.3d) * tremble * 0.5f);
        }

        /// <summary>Legacy binary cook pose (fully raised).</summary>
        public static Float3 GrenadeCookPosition(bool cooking, bool reducedMotion) =>
            cooking ? GrenadeCookPosition(CookRaiseSeconds, 0f, reducedMotion) : default;

        public static Float3 GrenadeCookRotation(bool cooking, bool reducedMotion) =>
            cooking ? GrenadeCookRotation(CookRaiseSeconds, 0f, reducedMotion) : default;

        /// <summary>Support-hand wrist offset during the pin pull (hand-root local, meters).</summary>
        public static Float3 PinPullWrist(float cookSeconds) =>
            !float.IsFinite(cookSeconds) || cookSeconds <= 0f ? default : PinHand.Sample(cookSeconds);

        /// <summary>Support-hand finger openness during the pin pull.</summary>
        public static float PinPullOpen(float cookSeconds) =>
            !float.IsFinite(cookSeconds) || cookSeconds <= 0f ? 0f : Math.Clamp(PinOpen.Sample(cookSeconds).X, 0f, 1f);

        public static Float3 GrenadeThrowPosition(float phase01, bool reducedMotion) =>
            GrenadeThrowPosition(phase01, GrenadeCookPosition(true, reducedMotion), reducedMotion);

        public static Float3 GrenadeThrowRotation(float phase01, bool reducedMotion) =>
            GrenadeThrowRotation(phase01, GrenadeCookRotation(true, reducedMotion), reducedMotion);

        /// <summary>Overhand release from <paramref name="start"/> (the pose at release), follow-through, then down out of view.</summary>
        public static Float3 GrenadeThrowPosition(float phase01, Float3 start, bool reducedMotion) =>
            ThrowSample(phase01, start, ThrowPositionTargets, reducedMotion ? 0.5f : 1f);

        public static Float3 GrenadeThrowRotation(float phase01, Float3 start, bool reducedMotion) =>
            ThrowSample(phase01, start, ThrowRotationTargets, reducedMotion ? 0.5f : 1f);

        private static Float3 ThrowSample(float phase01, Float3 start, MotionCurve targets, float amp)
        {
            if (!float.IsFinite(phase01)) throw new ArgumentOutOfRangeException(nameof(phase01));
            if (phase01 <= 0f) return start;
            float p = Math.Min(phase01, 1f);
            float hold = 1f - MotionCurve.Ramp(p, 0f, .18f);
            return MotionCurve.Add(MotionCurve.Scale(targets.Sample(p), amp), MotionCurve.Scale(start, hold));
        }

        // Grows with the square of the elapsed fuse share; zero until the pin is out.
        private static float Tremble(float cookSeconds, float fuseSeconds)
        {
            if (fuseSeconds <= 0f || cookSeconds <= PinPullSeconds) return 0f;
            float share = Math.Clamp(cookSeconds / fuseSeconds, 0f, 1f);
            return (float)Math.Sin(cookSeconds * 9.3d) * share * share;
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
