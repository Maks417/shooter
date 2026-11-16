#nullable enable
using System;
using Shooter.Contracts;
using Shooter.VectorMath;

namespace Shooter.Simulation.Motor
{
    public static class MotorMath
    {
        public const float TickSeconds = 1f / 60f;

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
            float d = VecMath.Dot(vector, unitNormal);
            return VecMath.Sub(vector, VecMath.Scale(unitNormal, d));
        }
    }
}
