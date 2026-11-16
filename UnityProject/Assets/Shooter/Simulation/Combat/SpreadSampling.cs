#nullable enable
using System;
using Shooter.Contracts;
using static Shooter.VectorMath.VecMath;

namespace Shooter.Simulation.Combat
{
    /// <summary>Uniform solid-angle cone using independent u,v in [0,1) from a named shot substream.</summary>
    public static class SpreadSampling
    {
        public static Float3 SampleDirection(Float3 unitAim, float halfAngleRadians, ulong streamSampleU, ulong streamSampleV)
        {
            if (!unitAim.IsUnit) throw new ArgumentException("Aim must be unit", nameof(unitAim));
            if (halfAngleRadians <= 0f) return unitAim;
            float u = ToUnitFloat(streamSampleU);
            float v = ToUnitFloat(streamSampleV);
            double cosAlpha = Math.Cos(halfAngleRadians);
            double cosTheta = 1.0 - u * (1.0 - cosAlpha);
            double sinTheta = Math.Sqrt(Math.Max(0.0, 1.0 - cosTheta * cosTheta));
            double phi = 2.0 * Math.PI * v;
            Basis(unitAim, out Float3 right, out Float3 up);
            double x = sinTheta * Math.Cos(phi);
            double y = sinTheta * Math.Sin(phi);
            double z = cosTheta;
            return Normalize(new Float3(
                (float)(right.X * x + up.X * y + unitAim.X * z),
                (float)(right.Y * x + up.Y * y + unitAim.Y * z),
                (float)(right.Z * x + up.Z * y + unitAim.Z * z)));
        }

        public static ulong MixShot(ulong seed, uint shotIndex, byte channel)
        {
            unchecked
            {
                ulong z = seed + 0x9E3779B97F4A7C15UL * (shotIndex + 1u) + channel * 0xC2B2AE3D27D4EB4FUL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        private static float ToUnitFloat(ulong sample) => (sample >> 11) * (1f / (1UL << 53));

        private static void Basis(Float3 aim, out Float3 right, out Float3 up)
        {
            Float3 alt = Math.Abs(aim.Y) > 0.999f ? new Float3(1, 0, 0) : new Float3(0, 1, 0);
            right = Normalize(Cross(alt, aim));
            up = Cross(aim, right);
        }
    }
}
