#nullable enable
using System;

namespace Shooter.Presentation.Hud
{
    /// <summary>
    /// ADR-084 pure UI motion: easing curves, a critically damped follower, stamp/slide envelopes and the
    /// reduced-motion collapse. Cosmetic only; never read by gameplay. All inputs are seconds or 0..1 progress.
    /// </summary>
    public static class UiMotion
    {
        /// <summary>Default screen/plate transition length.</summary>
        public const float TransitionSeconds = 0.22f;
        /// <summary>Badge/medal stamp length (scale overshoot settles inside this window).</summary>
        public const float StampSeconds = 0.32f;
        /// <summary>Starting scale of a badge stamp before it settles to 1.</summary>
        public const float StampStartScale = 1.45f;
        /// <summary>Slide distance (logical px) used by entering rows and plates.</summary>
        public const float SlideDistance = 24f;
        /// <summary>Delay between consecutive staggered items.</summary>
        public const float StaggerSeconds = 0.045f;
        /// <summary>Focus-bar follow speed (1/s) for the critically damped follower.</summary>
        public const float FocusFollowRate = 18f;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public static float Progress(float elapsed, float duration) =>
            duration <= 0f ? 1f : Clamp01(elapsed / duration);

        public static float OutCubic(float t)
        {
            t = Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float InOutCubic(float t)
        {
            t = Clamp01(t);
            if (t < 0.5f) return 4f * t * t * t;
            float u = -2f * t + 2f;
            return 1f - u * u * u * 0.5f;
        }

        /// <summary>Back-out easing: overshoots past 1 then settles; exactly 0 at t=0 and 1 at t=1.</summary>
        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Clamp01(t);
            float c3 = overshoot + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }

        /// <summary>Linear interpolation helper (unclamped).</summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// Critically damped follower step (frame-rate independent). Returns the new value moving
        /// from <paramref name="current"/> toward <paramref name="target"/>; snaps when reduced motion is on.
        /// </summary>
        public static float Follow(float current, float target, float rate, float deltaSeconds, bool reducedMotion)
        {
            if (reducedMotion || rate <= 0f) return target;
            if (deltaSeconds <= 0f) return current;
            float k = 1f - (float)Math.Exp(-rate * deltaSeconds);
            float next = current + (target - current) * k;
            return Math.Abs(target - next) < 0.01f ? target : next;
        }

        /// <summary>
        /// Badge stamp scale: starts large and settles to 1 with a small undershoot.
        /// Reduced motion keeps scale 1 (fade only).
        /// </summary>
        public static float StampScale(float elapsed, bool reducedMotion)
        {
            if (reducedMotion) return 1f;
            float t = Progress(elapsed, StampSeconds);
            return Lerp(StampStartScale, 1f, OutBack(t, 2.2f));
        }

        /// <summary>Entering alpha for the stamp/slide envelopes (both motion modes).</summary>
        public static float EnterAlpha(float elapsed, float duration = TransitionSeconds) =>
            OutCubic(Progress(elapsed, duration * 0.7f));

        /// <summary>
        /// Horizontal slide offset (logical px) for entering rows/plates; 0 when settled or under reduced motion.
        /// Positive <paramref name="direction"/> enters from the right.
        /// </summary>
        public static float SlideOffset(float elapsed, float direction, bool reducedMotion, float duration = TransitionSeconds)
        {
            if (reducedMotion) return 0f;
            float t = OutCubic(Progress(elapsed, duration));
            return (1f - t) * SlideDistance * Math.Sign(direction);
        }

        /// <summary>Elapsed time seen by item <paramref name="index"/> in a staggered list.</summary>
        public static float Staggered(float elapsed, int index, bool reducedMotion) =>
            reducedMotion ? elapsed : elapsed - Math.Max(0, index) * StaggerSeconds;

        /// <summary>
        /// Glint sweep position 0..1 across a badge after it lands (one pass), or -1 when hidden.
        /// Reduced flash disables the glint entirely.
        /// </summary>
        public static float GlintPosition(float elapsed, bool reducedFlash)
        {
            if (reducedFlash) return -1f;
            const float start = StampSeconds * 0.8f, length = 0.45f;
            if (elapsed < start || elapsed > start + length) return -1f;
            return InOutCubic((elapsed - start) / length);
        }

        /// <summary>Integer count-up for score/stat readouts; reduced motion shows the final value.</summary>
        public static int CountUp(int from, int to, float elapsed, float duration, bool reducedMotion)
        {
            if (reducedMotion || duration <= 0f) return to;
            float t = OutCubic(Progress(elapsed, duration));
            return (int)Math.Round(Lerp(from, to, t), MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Slow pulse 0..1 (low-health, objective attention). Reduced flash holds a steady mid value.
        /// </summary>
        public static float Pulse(float timeSeconds, float periodSeconds, bool reducedFlash)
        {
            if (reducedFlash || periodSeconds <= 0f) return 0.5f;
            double phase = timeSeconds / periodSeconds * 2.0 * Math.PI;
            return (float)(0.5 - 0.5 * Math.Cos(phase));
        }
    }
}
