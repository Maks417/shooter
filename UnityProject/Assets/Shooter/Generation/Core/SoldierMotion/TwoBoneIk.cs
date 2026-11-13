#nullable enable
using System;
using Shooter.Contracts;
using static Shooter.Generation.GeometryMath;

namespace Shooter.Generation
{
    public readonly struct TwoBoneResult
    {
        public Float3 Joint { get; }
        public Float3 End { get; }
        public float TargetError { get; }
        public float FlexionRadians { get; }
        internal TwoBoneResult(Float3 joint, Float3 end, float error, float flexion)
        { Joint = joint; End = end; TargetError = error; FlexionRadians = flexion; }
    }

    /// <summary>Bounded analytical hinge IK. The requested target is never rewritten in error diagnostics.</summary>
    public static class TwoBoneIk
    {
        public static TwoBoneResult Solve(Float3 origin, Float3 target, Float3 pole, float upperLength, float lowerLength,
            float maxFlexionRadians, float maxReachRatio = .98f)
            => Solve(origin, target, pole, upperLength, lowerLength, maxFlexionRadians, maxReachRatio, new Float3(0, -1, 0), (float)Math.PI);

        public static TwoBoneResult Solve(Float3 origin, Float3 target, Float3 pole, float upperLength, float lowerLength,
            float maxFlexionRadians, float maxReachRatio, Float3 bindDirection, float maxSwingRadians)
        {
            if (!Finite(upperLength) || !Finite(lowerLength) || upperLength < .001f || lowerLength < .001f ||
                !Finite(maxFlexionRadians) || maxFlexionRadians <= 0 || maxFlexionRadians >= Math.PI ||
                !Finite(maxReachRatio) || maxReachRatio <= 0 || maxReachRatio > 1 || !bindDirection.IsUnit ||
                !Finite(maxSwingRadians) || maxSwingRadians <= 0 || maxSwingRadians > Math.PI + 1e-6)
                throw new ArgumentOutOfRangeException(nameof(upperLength));
            var delta = Sub(target, origin); float wanted = (float)Math.Sqrt(delta.LengthSquared);
            var direction = wanted > 1e-7f ? Scale(delta, 1 / wanted) : new Float3(0, -1, 0);
            var projected = Sub(pole, Scale(direction, Dot(pole, direction)));
            if (projected.LengthSquared < 1e-10)
            {
                var fallback = Math.Abs(direction.X) < .9f ? new Float3(1, 0, 0) : new Float3(0, 0, 1);
                projected = Sub(fallback, Scale(direction, Dot(fallback, direction)));
            }
            projected = Normalize(projected);
            double a = upperLength, b = lowerLength;
            float minimum = (float)Math.Sqrt(a * a + b * b + 2 * a * b * Math.Cos(maxFlexionRadians));
            float maximum = (upperLength + lowerLength) * maxReachRatio;
            if (minimum > maximum) throw new ArgumentException("Joint flexion and reach limits have no common interval.");
            float distance = Math.Max(minimum, Math.Min(maximum, wanted));
            float along = (float)((a * a - b * b + distance * distance) / (2 * distance));
            float height = (float)Math.Sqrt(Math.Max(0, a * a - along * along));
            // Rotate around the reach axis only when the preferred pole violates the anatomical cone.
            // This keeps the target and both segment lengths unchanged whenever the circle intersects the cone.
            float threshold = upperLength * (float)Math.Cos(maxSwingRadians);
            if (Dot(Add(Scale(direction, along), Scale(projected, height)), bindDirection) < threshold - 1e-6f)
            {
                var perpendicular = MotionMath.Cross(direction, projected);
                float ca = height * Dot(projected, bindDirection), cb = height * Dot(perpendicular, bindDirection);
                float desired = threshold - along * Dot(direction, bindDirection);
                float amplitude = (float)Math.Sqrt(ca * ca + cb * cb);
                if (amplitude > 1e-7f && desired <= amplitude)
                {
                    float center = (float)Math.Atan2(cb, ca), half = (float)Math.Acos(MotionMath.Clamp(desired / amplitude, -1, 1));
                    float p = Wrap(center + half), q = Wrap(center - half), angle = Math.Abs(p) < Math.Abs(q) ? p : q;
                    projected = Add(Scale(projected, (float)Math.Cos(angle)), Scale(perpendicular, (float)Math.Sin(angle)));
                }
                else
                {
                    // No exact IK solution exists inside the cone. Preserve lengths and disclose the residual.
                    var upper = Normalize(Add(Scale(direction, along), Scale(projected, height)));
                    // At an infeasible cone boundary minimize the real target residual, rather than retain
                    // an impossible preferred-pole solution that can point the arm away from its task.
                    var tangent = Sub(delta, Scale(bindDirection, Dot(delta, bindDirection)));
                    if (tangent.LengthSquared < 1e-10) tangent = MotionMath.Cross(bindDirection, Math.Abs(bindDirection.X) < .9f ? new Float3(1, 0, 0) : new Float3(0, 0, 1));
                    upper = Add(Scale(bindDirection, (float)Math.Cos(maxSwingRadians)), Scale(Normalize(tangent), (float)Math.Sin(maxSwingRadians)));
                    var constrainedJoint = Add(origin, Scale(upper, upperLength));
                    var lower = Sub(target, constrainedJoint);
                    if (lower.LengthSquared < 1e-10) lower = upper;
                    lower = Normalize(lower);
                    float bend = (float)Math.Acos(MotionMath.Clamp(Dot(upper, lower), -1, 1));
                    float minFlexion = (float)Math.Acos(MotionMath.Clamp((maximum * maximum - upperLength * upperLength - lowerLength * lowerLength) / (2 * upperLength * lowerLength), -1, 1));
                    float limitedBend = MotionMath.Clamp(bend, minFlexion, maxFlexionRadians);
                    if (Math.Abs(limitedBend - bend) > 1e-6f)
                    {
                        var bendAxis = Sub(lower, Scale(upper, Dot(upper, lower)));
                        if (bendAxis.LengthSquared < 1e-10) bendAxis = projected;
                        bendAxis = Sub(bendAxis, Scale(upper, Dot(upper, bendAxis)));
                        if (bendAxis.LengthSquared < 1e-10) bendAxis = MotionMath.Cross(upper, Math.Abs(upper.X) < .9f ? new Float3(1, 0, 0) : new Float3(0, 0, 1));
                        lower = Add(Scale(upper, (float)Math.Cos(limitedBend)), Scale(Normalize(bendAxis), (float)Math.Sin(limitedBend)));
                        bend = limitedBend;
                    }
                    var constrainedEnd = Add(constrainedJoint, Scale(lower, lowerLength));
                    return new TwoBoneResult(constrainedJoint, constrainedEnd, (float)Math.Sqrt(Sub(constrainedEnd, target).LengthSquared), bend);
                }
            }
            var joint = Add(origin, Add(Scale(direction, along), Scale(projected, height)));
            var end = Add(origin, Scale(direction, distance));
            float flexion = (float)Math.Acos(Math.Max(-1, Math.Min(1, (distance * distance - a * a - b * b) / (2 * a * b))));
            return new TwoBoneResult(joint, end, (float)Math.Sqrt(Sub(end, target).LengthSquared), flexion);
        }
        private static float Wrap(float angle)
        { while (angle > Math.PI) angle -= 2 * (float)Math.PI; while (angle < -Math.PI) angle += 2 * (float)Math.PI; return angle; }
    }

    internal static class MotionMath
    {
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        public static Float3 Lerp(Float3 a, Float3 b, float t) => Add(a, Scale(Sub(b, a), t));
        public static float Smooth(float t) { t = Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
        public static Float3 Cross(Float3 a, Float3 b) => new Float3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static Quat AxisAngle(Float3 axis, float angle)
        {
            float s = (float)Math.Sin(angle * .5f); return new Quat(axis.X * s, axis.Y * s, axis.Z * s, (float)Math.Cos(angle * .5f));
        }
        public static Quat Multiply(Quat a, Quat b) => Unit(a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X, a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        private static Quat Unit(float x, float y, float z, float w)
        {
            float n = (float)Math.Sqrt((double)x * x + (double)y * y + (double)z * z + (double)w * w);
            return new Quat(x / n, y / n, z / n, w / n);
        }
        public static Quat FromTo(Float3 from, Float3 to)
        {
            from = Normalize(from); to = Normalize(to); float dot = Clamp(Dot(from, to), -1, 1);
            if (dot > .999999f) return Quat.Identity;
            if (dot < -.999999f)
            {
                var axis = Cross(from, Math.Abs(from.X) < .9f ? new Float3(1, 0, 0) : new Float3(0, 0, 1));
                return AxisAngle(Normalize(axis), (float)Math.PI);
            }
            var cross = Cross(from, to); return Unit(cross.X, cross.Y, cross.Z, 1 + dot);
        }
        public static Quat Inverse(Quat q) => new Quat(-q.X, -q.Y, -q.Z, q.W);
        public static float AngularError(Quat a, Quat b)
        { var delta = Multiply(Inverse(a), b); return 2 * (float)Math.Acos(Clamp(Math.Abs(delta.W), 0, 1)); }
        public static Quat LimitSwingTwist(Quat desired, Quat parent, Float3 axis, float maxSwing, float maxTwist)
        {
            var local = Multiply(Inverse(parent), desired);
            float projected = local.X * axis.X + local.Y * axis.Y + local.Z * axis.Z;
            Quat twist = projected * projected + local.W * local.W < 1e-10f ? Quat.Identity : Unit(axis.X * projected, axis.Y * projected, axis.Z * projected, local.W);
            var swing = Multiply(local, Inverse(twist));
            float angle = 2 * (float)Math.Atan2(twist.X * axis.X + twist.Y * axis.Y + twist.Z * axis.Z, twist.W);
            while (angle > Math.PI) angle -= 2 * (float)Math.PI; while (angle < -Math.PI) angle += 2 * (float)Math.PI;
            twist = AxisAngle(axis, Clamp(angle, -maxTwist, maxTwist));
            if (swing.W < 0) swing = new Quat(-swing.X, -swing.Y, -swing.Z, -swing.W);
            float swingAngle = 2 * (float)Math.Acos(Clamp(swing.W, -1, 1));
            if (swingAngle > maxSwing)
            {
                var direction = new Float3(swing.X, swing.Y, swing.Z);
                swing = AxisAngle(Normalize(direction), maxSwing);
            }
            return Multiply(parent, Multiply(swing, twist));
        }
    }
}
