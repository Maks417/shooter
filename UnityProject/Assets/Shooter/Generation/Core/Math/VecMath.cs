#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.VectorMath
{
    /// <summary>
    /// Single deterministic vector/quaternion kernel over the frozen C0 <see cref="Float3"/>/<see cref="Quat"/>.
    /// Every expression keeps the exact operation order of the helpers it replaced (MotorMath, GeometryMath,
    /// MotionMath and private copies), so content hashes and goldens stay bit-identical on Mono, .NET and IL2CPP.
    /// Do not reorder terms, fuse multiply-adds, or route through System.Numerics/Unity.Mathematics.
    /// "Double" variants accumulate or divide in double before narrowing; plain variants stay in float.
    /// </summary>
    public static class VecMath
    {
        public static Float3 Add(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 Sub(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 Scale(Float3 v, float s) => new Float3(v.X * s, v.Y * s, v.Z * s);
        /// <summary>Scales in double and narrows each component once.</summary>
        public static Float3 ScaleDouble(Float3 v, double s) => new Float3((float)(v.X * s), (float)(v.Y * s), (float)(v.Z * s));
        /// <summary>Float accumulation: (x*x' + y*y') + z*z' in float.</summary>
        public static float Dot(Float3 a, Float3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        /// <summary>Double accumulation of float products widened before multiply.</summary>
        public static double DotDouble(Float3 a, Float3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
        public static Float3 Cross(Float3 a, Float3 b) => new Float3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        /// <summary>Cross product evaluated in double, narrowed per component.</summary>
        public static Float3 CrossDouble(Float3 a, Float3 b) => new Float3(
            (float)((double)a.Y * b.Z - (double)a.Z * b.Y),
            (float)((double)a.Z * b.X - (double)a.X * b.Z),
            (float)((double)a.X * b.Y - (double)a.Y * b.X));
        /// <summary>Double sqrt of the double <see cref="Float3.LengthSquared"/>.</summary>
        public static double LengthDouble(Float3 v) => Math.Sqrt(v.LengthSquared);
        public static float Length(Float3 v) => (float)Math.Sqrt(v.LengthSquared);

        /// <summary>Divides each component by the double length; throws below 1e-12 (former GeometryMath.Normalize).</summary>
        public static Float3 Normalize(Float3 v)
        {
            double length = Math.Sqrt(v.LengthSquared);
            if (length < 1e-12) throw new ArgumentException("Zero-length direction.");
            return new Float3((float)(v.X / length), (float)(v.Y / length), (float)(v.Z / length));
        }

        /// <summary>Float reciprocal of the double length, zero below 1e-12 squared length (former MotorMath.NormalizeOrZero).</summary>
        public static Float3 NormalizeOrZero(Float3 v)
        {
            double len2 = v.LengthSquared;
            if (len2 <= 1e-12) return default;
            float inv = 1f / (float)Math.Sqrt(len2);
            return Scale(v, inv);
        }

        /// <summary>Rotates <paramref name="p"/> by unit <paramref name="q"/> via p + w*t + q x t with t = 2(q x p).</summary>
        public static Float3 Rotate(Float3 p, Quat q)
        {
            float tx = 2 * (q.Y * p.Z - q.Z * p.Y), ty = 2 * (q.Z * p.X - q.X * p.Z), tz = 2 * (q.X * p.Y - q.Y * p.X);
            return new Float3(p.X + q.W * tx + q.Y * tz - q.Z * ty, p.Y + q.W * ty + q.Z * tx - q.X * tz, p.Z + q.W * tz + q.X * ty - q.Y * tx);
        }

        /// <summary>Unit quaternion from a unit axis and angle in radians (half angle in float).</summary>
        public static Quat AxisAngle(Float3 axis, float angle)
        {
            float s = (float)Math.Sin(angle * .5f); return new Quat(axis.X * s, axis.Y * s, axis.Z * s, (float)Math.Cos(angle * .5f));
        }

        /// <summary>Hamilton product a*b, renormalised through <see cref="UnitQuat"/>.</summary>
        public static Quat Multiply(Quat a, Quat b) => UnitQuat(a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X, a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        /// <summary>Normalises raw components with a double-accumulated norm narrowed to float, then float division.</summary>
        public static Quat UnitQuat(float x, float y, float z, float w)
        {
            float n = (float)Math.Sqrt((double)x * x + (double)y * y + (double)z * z + (double)w * w);
            return new Quat(x / n, y / n, z / n, w / n);
        }

        /// <summary>Shortest-arc rotation taking <paramref name="from"/> to <paramref name="to"/> (any non-zero lengths).</summary>
        public static Quat FromTo(Float3 from, Float3 to)
        {
            from = Normalize(from); to = Normalize(to); float dot = Math.Max(-1, Math.Min(1, Dot(from, to)));
            if (dot > .999999f) return Quat.Identity;
            if (dot < -.999999f)
            {
                var axis = Cross(from, Math.Abs(from.X) < .9f ? new Float3(1, 0, 0) : new Float3(0, 0, 1));
                return AxisAngle(Normalize(axis), (float)Math.PI);
            }
            var cross = Cross(from, to); return UnitQuat(cross.X, cross.Y, cross.Z, 1 + dot);
        }

        /// <summary>Conjugate; the inverse of a unit quaternion.</summary>
        public static Quat Inverse(Quat q) => new Quat(-q.X, -q.Y, -q.Z, q.W);
    }
}
