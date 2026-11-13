#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.Motor
{
    internal static class MotorMath
    {
        public const float TickSeconds = 1f / 60f;

        public static Float3 Add(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 Sub(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 Scale(Float3 a, float s) => new Float3(a.X * s, a.Y * s, a.Z * s);
        public static float Dot(Float3 a, Float3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static float Length(Float3 a) => (float)Math.Sqrt(a.LengthSquared);
        public static Float3 NormalizeOrZero(Float3 a)
        {
            double len2 = a.LengthSquared;
            if (len2 <= 1e-12) return default;
            float inv = 1f / (float)Math.Sqrt(len2);
            return Scale(a, inv);
        }

        public static Float3 Horizontal(Float3 v) => new Float3(v.X, 0, v.Z);

        public static Float3 YawForward(float yawRadians)
        {
            float s = (float)Math.Sin(yawRadians);
            float c = (float)Math.Cos(yawRadians);
            return new Float3(s, 0, c);
        }

        public static Float3 YawRight(float yawRadians)
        {
            float s = (float)Math.Sin(yawRadians);
            float c = (float)Math.Cos(yawRadians);
            return new Float3(c, 0, -s);
        }

        public static Capsule BodyCapsule(Float3 feet, float height, float radius)
        {
            if (!(height > 2f * radius)) throw new ArgumentException("capsule height", nameof(height));
            var bottom = new Float3(feet.X, feet.Y + radius, feet.Z);
            var top = new Float3(feet.X, feet.Y + height - radius, feet.Z);
            return new Capsule(bottom, top, radius);
        }

        public static Float3 ProjectOnPlane(Float3 vector, Float3 unitNormal)
        {
            float d = Dot(vector, unitNormal);
            return Sub(vector, Scale(unitNormal, d));
        }
    }
}
