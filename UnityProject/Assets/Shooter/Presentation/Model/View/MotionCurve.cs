#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>One numeric pose key: normalized time and a camera/weapon-local value (ADR-099).</summary>
    public readonly struct MotionKey
    {
        public float Time { get; }
        public Float3 Value { get; }
        public MotionKey(float time, float x, float y, float z)
        {
            Time = time;
            Value = new Float3(x, y, z);
        }
    }

    /// <summary>
    /// Pose-to-pose keyframe track evaluated with a C1 cubic Hermite (Catmull-Rom tangents, zero tangents at both ends).
    /// First-person choreography is authored as these numeric tables — no imported or recorded clips (AGENTS.md, ADR-099).
    /// </summary>
    public sealed class MotionCurve
    {
        private readonly MotionKey[] keys;
        private readonly Float3[] tangents;

        public MotionCurve(params MotionKey[] keys)
        {
            if (keys == null || keys.Length == 0) throw new ArgumentException("At least one key required.", nameof(keys));
            for (int i = 0; i < keys.Length; i++)
            {
                if (!float.IsFinite(keys[i].Time)) throw new ArgumentOutOfRangeException(nameof(keys));
                if (i > 0 && !(keys[i].Time > keys[i - 1].Time))
                    throw new ArgumentException("Key times must strictly increase.", nameof(keys));
            }
            this.keys = keys;
            tangents = new Float3[keys.Length];
            for (int i = 1; i < keys.Length - 1; i++)
            {
                float span = keys[i + 1].Time - keys[i - 1].Time;
                Float3 a = keys[i - 1].Value, b = keys[i + 1].Value;
                tangents[i] = new Float3((b.X - a.X) / span, (b.Y - a.Y) / span, (b.Z - a.Z) / span);
                // A key that is a local extreme on an axis holds still there, so poses read and nothing overshoots.
                tangents[i] = new Float3(
                    Extreme(a.X, keys[i].Value.X, b.X) ? 0f : tangents[i].X,
                    Extreme(a.Y, keys[i].Value.Y, b.Y) ? 0f : tangents[i].Y,
                    Extreme(a.Z, keys[i].Value.Z, b.Z) ? 0f : tangents[i].Z);
            }
        }

        public float StartTime => keys[0].Time;
        public float EndTime => keys[keys.Length - 1].Time;
        public Float3 First => keys[0].Value;
        public Float3 Last => keys[keys.Length - 1].Value;

        public Float3 Sample(float time)
        {
            if (!float.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
            if (time <= keys[0].Time) return keys[0].Value;
            int last = keys.Length - 1;
            if (time >= keys[last].Time) return keys[last].Value;
            int i = 0;
            while (time > keys[i + 1].Time) i++;
            float h = keys[i + 1].Time - keys[i].Time;
            float u = (time - keys[i].Time) / h;
            float u2 = u * u, u3 = u2 * u;
            float h00 = 2f * u3 - 3f * u2 + 1f;
            float h10 = u3 - 2f * u2 + u;
            float h01 = -2f * u3 + 3f * u2;
            float h11 = u3 - u2;
            Float3 p0 = keys[i].Value, p1 = keys[i + 1].Value, m0 = tangents[i], m1 = tangents[i + 1];
            return new Float3(
                h00 * p0.X + h10 * h * m0.X + h01 * p1.X + h11 * h * m1.X,
                h00 * p0.Y + h10 * h * m0.Y + h01 * p1.Y + h11 * h * m1.Y,
                h00 * p0.Z + h10 * h * m0.Z + h01 * p1.Z + h11 * h * m1.Z);
        }

        /// <summary>Smoothstep ramp from 0 at <paramref name="start"/> to 1 at <paramref name="end"/>.</summary>
        public static float Ramp(float t, float start, float end)
        {
            if (end <= start) return t >= end ? 1f : 0f;
            float u = Math.Clamp((t - start) / (end - start), 0f, 1f);
            return u * u * (3f - 2f * u);
        }

        /// <summary>Rises over [a,b], holds, falls over [c,d]; 0 outside.</summary>
        public static float Window(float t, float a, float b, float c, float d) => Ramp(t, a, b) * (1f - Ramp(t, c, d));

        public static Float3 Scale(Float3 v, float s) => new Float3(v.X * s, v.Y * s, v.Z * s);
        public static Float3 Add(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 Sub(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 Lerp(Float3 a, Float3 b, float t) =>
            new Float3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        private static bool Extreme(float a, float v, float b) => (v >= a && v >= b) || (v <= a && v <= b);
    }
}
