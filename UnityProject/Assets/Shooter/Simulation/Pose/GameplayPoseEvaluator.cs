#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using static Shooter.VectorMath.VecMath;

namespace Shooter.Simulation.Pose
{
    /// <summary>Versioned mathematical hit capsules independent of visual rig LOD.</summary>
    public sealed class GameplayPoseEvaluator
    {
        public const uint Version = 1;

        public uint PoseVersion => Version;

        public void Evaluate(EntityId entityId, Float3 feet, float yaw, float pitch, Stance stance, float capsuleHeight, uint tick, List<HitVolume> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (stance == Stance.Dead) return;

            float standFactor = stance == Stance.Crouching ? (capsuleHeight / 1.2f) : (capsuleHeight / 1.8f);
            float s = (float)Math.Sin(yaw);
            float c = (float)Math.Cos(yaw);
            Float3 forward = new Float3(s, 0, c);
            Float3 right = new Float3(c, 0, -s);

            // Approximate body proportions from soldier baseline (meters).
            float headR = 0.12f * standFactor;
            float torsoR = 0.18f * standFactor;
            float armR = 0.055f * standFactor;
            float legR = 0.08f * standFactor;
            float eye = stance == Stance.Crouching ? 1.05f * standFactor : 1.65f * standFactor;
            float hip = stance == Stance.Crouching ? 0.55f * standFactor : 0.9f * standFactor;
            float shoulder = eye - 0.25f * standFactor;

            Add(destination, entityId, 1, Sphere(feet, eye, headR), tick);
            Add(destination, entityId, 2, CapsuleVertical(feet, hip, shoulder, torsoR), tick);
            Add(destination, entityId, 3, Limb(feet, shoulder, right, forward, -1, armR, 0.35f * standFactor), tick);
            Add(destination, entityId, 4, Limb(feet, shoulder, right, forward, +1, armR, 0.35f * standFactor), tick);
            Add(destination, entityId, 5, CapsuleVertical(feet, legR, hip - legR, legR), tick);
            Add(destination, entityId, 6, CapsuleVertical(Offset(feet, right, 0.12f * standFactor), legR, hip - legR, legR), tick);

            // Pitch currently reserved for aim-driven upper body; v1 keeps upright capsules for stability.
            _ = pitch;
        }

        public Float3 EyePosition(Float3 feet, Stance stance, float capsuleHeight)
        {
            float standFactor = stance == Stance.Crouching ? (capsuleHeight / 1.2f) : (capsuleHeight / 1.8f);
            float eye = stance == Stance.Crouching ? 1.05f * standFactor : 1.65f * standFactor;
            return new Float3(feet.X, feet.Y + eye, feet.Z);
        }

        /// <summary>Canonical gameplay muzzle: eye-relative (+0.18,-0.15,+0.45)m in yaw/pitch aim frame.</summary>
        public Float3 CanonicalMuzzle(Float3 eye, float yaw, float pitch)
        {
            float cy = (float)Math.Cos(yaw), sy = (float)Math.Sin(yaw);
            float cp = (float)Math.Cos(pitch), sp = (float)Math.Sin(pitch);
            // Aim frame: +Z forward, +Y up, +X right.
            Float3 forward = new Float3(sy * cp, -sp, cy * cp);
            Float3 right = new Float3(cy, 0, -sy);
            Float3 up = Cross(forward, right);
            const float mx = 0.18f, my = -0.15f, mz = 0.45f;
            return new Float3(
                eye.X + right.X * mx + up.X * my + forward.X * mz,
                eye.Y + right.Y * mx + up.Y * my + forward.Y * mz,
                eye.Z + right.Z * mx + up.Z * my + forward.Z * mz);
        }

        public Float3 AimDirection(float yaw, float pitch)
        {
            float cp = (float)Math.Cos(pitch), sp = (float)Math.Sin(pitch);
            float cy = (float)Math.Cos(yaw), sy = (float)Math.Sin(yaw);
            return new Float3(sy * cp, -sp, cy * cp);
        }

        private static void Add(List<HitVolume> list, EntityId id, ushort region, Capsule capsule, uint tick) =>
            list.Add(new HitVolume(id, region, capsule, tick));

        private static Capsule Sphere(Float3 feet, float y, float r)
        {
            var c = new Float3(feet.X, feet.Y + y, feet.Z);
            return new Capsule(c, c, r);
        }

        private static Capsule CapsuleVertical(Float3 feet, float y0, float y1, float r)
        {
            float lo = Math.Min(y0, y1), hi = Math.Max(y0, y1);
            if (hi - lo < 1e-4f) hi = lo + 1e-3f;
            return new Capsule(new Float3(feet.X, feet.Y + lo, feet.Z), new Float3(feet.X, feet.Y + hi, feet.Z), r);
        }

        private static Capsule Limb(Float3 feet, float shoulderY, Float3 right, Float3 forward, int side, float r, float length)
        {
            var shoulder = new Float3(feet.X + right.X * (0.22f * side), feet.Y + shoulderY, feet.Z + right.Z * (0.22f * side));
            var end = new Float3(shoulder.X + forward.X * length * 0.2f + right.X * (0.05f * side), shoulder.Y - length, shoulder.Z + forward.Z * length * 0.2f + right.Z * (0.05f * side));
            return new Capsule(shoulder, end, r);
        }

        private static Float3 Offset(Float3 feet, Float3 right, float amount) =>
            new Float3(feet.X + right.X * amount, feet.Y, feet.Z + right.Z * amount);
    }
}
