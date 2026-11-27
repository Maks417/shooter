#nullable enable
using System;

namespace Shooter.Presentation.View
{
    /// <summary>Clip roles an authored soldier body plays (ADR-124). Indices are stable mixer inputs.</summary>
    public enum SoldierClipRole
    {
        Idle = 0,
        WalkForward, WalkBackward, WalkLeft, WalkRight,
        RunForward, RunBackward, RunLeft, RunRight,
        CrouchIdle, CrouchForward, CrouchBackward, CrouchLeft, CrouchRight,
        Airborne,
        Count
    }

    /// <summary>
    /// ADR-124 pure blend for an authored soldier: weights over the locomotion clip roles from the authoritative
    /// travel speed and heading (relative to facing), the crouch blend and the airborne blend, plus one shared gait
    /// phase so every directional clip keeps its feet in step. The four-way split eases over
    /// <see cref="DirectionSeconds"/> so a strafe reversal crossfades, and the phase rate follows the actual speed
    /// against the playing clips' nominal speed so a slow strafe on a run clip does not skate (ADR-129). Presentation only.
    /// </summary>
    public sealed class AuthoredSoldierBlend
    {
        public const float WalkSpeed = 1.6f, RunSpeed = 4.2f, CrouchSpeed = 1.4f;
        /// <summary>Time constant of the directional crossfade.</summary>
        public const float DirectionSeconds = .12f;
        /// <summary>Bounds of the stride-rate scale (speed over the playing clips' nominal speed).</summary>
        public const float MinRate = .6f, MaxRate = 1.4f;
        private readonly float[] weights = new float[(int)SoldierClipRole.Count];
        private float sf = 1f, sb, sl, sr;
        private bool hasDirection;
        public float Phase { get; private set; }
        public ReadOnlySpan<float> Weights => weights;

        /// <summary>
        /// <paramref name="localHeadingRadians"/> is the travel direction relative to facing: 0 forward, +π/2 right,
        /// ±π backward. <paramref name="cycleSeconds"/> returns a role's clip length (≤ 0 when missing).
        /// </summary>
        public void Step(float dtSeconds, float speed, float localHeadingRadians, float crouch, float airborne, Func<SoldierClipRole, float> cycleSeconds)
        {
            if (cycleSeconds == null) throw new ArgumentNullException(nameof(cycleSeconds));
            Array.Clear(weights, 0, weights.Length);
            float dt = float.IsFinite(dtSeconds) && dtSeconds > 0f ? dtSeconds : 0f;
            speed = float.IsFinite(speed) ? Math.Max(0f, speed) : 0f;
            crouch = Clamp01(crouch);
            airborne = Clamp01(airborne);
            float heading = float.IsFinite(localHeadingRadians) ? localHeadingRadians : 0f;

            // Four-way directional split: forward/right/backward/left by the heading's projection.
            float fwd = (float)Math.Cos(heading), side = (float)Math.Sin(heading);
            float wf = Math.Max(0f, fwd), wb = Math.Max(0f, -fwd), wr = Math.Max(0f, side), wl = Math.Max(0f, -side);
            float sum = wf + wb + wr + wl;
            if (sum < 1e-5f) { wf = 1f; sum = 1f; }
            wf /= sum; wb /= sum; wr /= sum; wl /= sum;
            if (!hasDirection) { sf = wf; sb = wb; sl = wl; sr = wr; hasDirection = true; }
            else
            {
                float k = 1f - (float)Math.Exp(-dt / DirectionSeconds);
                sf += (wf - sf) * k; sb += (wb - sb) * k; sl += (wl - sl) * k; sr += (wr - sr) * k;
                float total = sf + sb + sl + sr;
                if (!(total > 1e-5f)) { sf = wf; sb = wb; sl = wl; sr = wr; total = 1f; }
                sf /= total; sb /= total; sl /= total; sr /= total;
            }
            wf = sf; wb = sb; wl = sl; wr = sr;

            // Stand: idle → walk → run by speed. Crouch: idle → crouch walk.
            float move = Clamp01(speed / WalkSpeed);
            float run = Clamp01((speed - WalkSpeed) / (RunSpeed - WalkSpeed));
            float stand = (1f - crouch) * (1f - airborne), low = crouch * (1f - airborne);
            weights[(int)SoldierClipRole.Idle] = stand * (1f - move);
            Spread(SoldierClipRole.WalkForward, stand * move * (1f - run), wf, wb, wl, wr);
            Spread(SoldierClipRole.RunForward, stand * move * run, wf, wb, wl, wr);
            float crouchMove = Clamp01(speed / CrouchSpeed);
            weights[(int)SoldierClipRole.CrouchIdle] = low * (1f - crouchMove);
            Spread(SoldierClipRole.CrouchForward, low * crouchMove, wf, wb, wl, wr);
            weights[(int)SoldierClipRole.Airborne] = airborne;

            // Missing roles hand their weight down their fallback chain to the first present clip.
            for (int r = 0; r < weights.Length; r++)
            {
                if (weights[r] <= 0f || cycleSeconds((SoldierClipRole)r) > 0f) continue;
                float moved = weights[r]; weights[r] = 0f;
                foreach (var fallback in Fallbacks((SoldierClipRole)r))
                    if (cycleSeconds(fallback) > 0f) { weights[(int)fallback] += moved; moved = 0f; break; }
                if (moved > 0f) weights[(int)SoldierClipRole.Idle] += moved;
            }

            // Shared phase: the weighted cycle length of the moving roles drives one normalized clock, scaled by the
            // speed over the nominal speed of the clips actually playing (after fallback).
            float cycle = 0f, moving = 0f, nominal = 0f;
            for (int r = (int)SoldierClipRole.WalkForward; r < (int)SoldierClipRole.Count; r++)
            {
                if (r == (int)SoldierClipRole.CrouchIdle || r == (int)SoldierClipRole.Airborne || weights[r] <= 0f) continue;
                float length = cycleSeconds((SoldierClipRole)r);
                if (length > 0f) { cycle += weights[r] * length; moving += weights[r]; nominal += weights[r] * NominalSpeed((SoldierClipRole)r); }
            }
            float period = moving > 1e-4f ? cycle / moving : Math.Max(0.5f, cycleSeconds(SoldierClipRole.Idle));
            if (!(period > 1e-3f)) period = 1f;
            float rate = moving > 1e-4f ? Math.Max(MinRate, Math.Min(MaxRate, speed / (nominal / moving))) : 1f;
            Rate = rate;
            Phase = (Phase + dt * rate / period) % 1f;
        }

        /// <summary>Stride-rate scale applied on the last step (1 when nothing moves).</summary>
        public float Rate { get; private set; } = 1f;

        /// <summary>The speed a role's clip is authored for: walk, run or crouch walk.</summary>
        public static float NominalSpeed(SoldierClipRole role)
        {
            if (role >= SoldierClipRole.WalkForward && role <= SoldierClipRole.WalkRight) return WalkSpeed;
            if (role >= SoldierClipRole.RunForward && role <= SoldierClipRole.RunRight) return RunSpeed;
            if (role >= SoldierClipRole.CrouchForward && role <= SoldierClipRole.CrouchRight) return CrouchSpeed;
            return WalkSpeed;
        }

        private void Spread(SoldierClipRole forward, float amount, float wf, float wb, float wl, float wr)
        {
            int f = (int)forward;
            weights[f] += amount * wf; weights[f + 1] += amount * wb; weights[f + 2] += amount * wl; weights[f + 3] += amount * wr;
        }

        /// <summary>
        /// Fallback chain for a missing role: same direction in the other gait first, then forward, then idle.
        /// Crouch roles never fall to a standing gait sideways; the body steers the legs instead.
        /// </summary>
        public static SoldierClipRole[] Fallbacks(SoldierClipRole role)
        {
            int r = (int)role;
            if (r >= (int)SoldierClipRole.WalkForward && r <= (int)SoldierClipRole.WalkRight)
                return new[] { (SoldierClipRole)(r + 4), SoldierClipRole.WalkForward, SoldierClipRole.RunForward, SoldierClipRole.Idle };
            if (r >= (int)SoldierClipRole.RunForward && r <= (int)SoldierClipRole.RunRight)
                return new[] { (SoldierClipRole)(r - 4), SoldierClipRole.RunForward, SoldierClipRole.WalkForward, SoldierClipRole.Idle };
            if (r >= (int)SoldierClipRole.CrouchForward && r <= (int)SoldierClipRole.CrouchRight)
                return new[] { SoldierClipRole.CrouchForward, SoldierClipRole.WalkForward, SoldierClipRole.Idle };
            return new[] { SoldierClipRole.Idle };
        }

        private static float Clamp01(float v) => float.IsFinite(v) ? Math.Max(0f, Math.Min(1f, v)) : 0f;
    }

    /// <summary>ADR-124 deterministic per-actor variant pick within a team's eligible variants (presentation only).</summary>
    public static class SoldierVariantPicker
    {
        public static int Pick(ulong entityId, byte team, int variantCount)
        {
            if (variantCount <= 0) throw new ArgumentOutOfRangeException(nameof(variantCount));
            ulong z = entityId * 0x9E3779B97F4A7C15UL + team;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z % (ulong)variantCount);
        }
    }
}
