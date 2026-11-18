#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using static Shooter.VectorMath.VecMath;

namespace Shooter.Simulation.Pose
{
    /// <summary>
    /// Versioned mathematical hit capsules independent of visual rig LOD. ADR-112 (v2): the capsules follow the drawn
    /// soldier (Generation <c>SoldierMotion</c> with the ADR-095 shooting posture) instead of a fixed upright figure:
    /// the head sits where the rendered head is for standing still, moving and crouched bodies, offset to the
    /// shooting side and pitched with the aim; the torso spans the drawn pelvis to chest. The numbers are measured
    /// from the rig and guarded by <c>Tools/Generation.Tests/HitVolumeRigAlignmentTests</c>.
    /// </summary>
    public sealed class GameplayPoseEvaluator
    {
        public const uint Version = 2;

        /// <summary>Drawn pelvis heights on flat ground (SoldierMotion, including the still-stance foot IK correction):
        /// standing still, any locomotion, full crouch.</summary>
        public const float PelvisStill = 0.943f, PelvisMoving = 0.78f, PelvisCrouch = 0.66f;
        /// <summary>Head-sphere centre above the drawn pelvis at level aim: the head joint (0.629 m, the same at every
        /// stance) plus 0.02 m to the middle of the balaclava-and-helmet silhouette.</summary>
        public const float HeadAbovePelvis = 0.649f;
        /// <summary>Chest joint above the pelvis (SoldierSkeleton spine 3 × 0.14 m).</summary>
        public const float ChestAbovePelvis = 0.42f;
        /// <summary>Speed above which the rig uses its bent-knee locomotion pelvis (SoldierMotion: any motion).</summary>
        public const float MovingSpeed = 0.001f;
        public const float HeadRadius = 0.13f;

        public uint PoseVersion => Version;

        public void Evaluate(EntityId entityId, Float3 feet, float yaw, float pitch, Stance stance, float capsuleHeight, uint tick, List<HitVolume> destination) =>
            Evaluate(entityId, feet, yaw, pitch, stance, capsuleHeight, 0f, tick, destination);

        public void Evaluate(EntityId entityId, Float3 feet, float yaw, float pitch, Stance stance, float capsuleHeight, float planarSpeed, uint tick, List<HitVolume> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (stance == Stance.Dead) return;

            bool crouch = stance == Stance.Crouching;
            float standFactor = crouch ? (capsuleHeight / 1.2f) : (capsuleHeight / 1.8f);
            float s = (float)Math.Sin(yaw);
            float c = (float)Math.Cos(yaw);
            Float3 forward = new Float3(s, 0, c);
            Float3 right = new Float3(c, 0, -s);

            float pelvis = (crouch ? PelvisCrouch : planarSpeed > MovingSpeed ? PelvisMoving : PelvisStill) * standFactor;
            // Presentation clamps the drawn aim pitch to ±25°; the head follows the sight line (linear fit of the rig).
            float p = Math.Max(-0.436f, Math.Min(0.436f, pitch));
            float headY = pelvis + (HeadAbovePelvis - 0.163f * p * p) * standFactor;
            float headRight = (0.094f - 0.156f * p) * standFactor;
            float headForward = (-0.101f - 0.293f * p) * standFactor;
            var head = new Float3(feet.X + right.X * headRight + forward.X * headForward, feet.Y + headY,
                feet.Z + right.Z * headRight + forward.Z * headForward);

            float headR = HeadRadius * standFactor;
            float torsoR = 0.18f * standFactor;
            float armR = 0.055f * standFactor;
            float legR = 0.08f * standFactor;
            float chest = pelvis + ChestAbovePelvis * standFactor;
            float shoulder = chest + 0.07f * standFactor;
            // Sphere centres, so the torso's rounded top ends at the shoulders and stays below the head sphere.
            float torsoTop = Math.Min(shoulder + 0.02f * standFactor, headY - headR) - torsoR;

            Add(destination, entityId, 1, new Capsule(head, head, headR), tick);
            Add(destination, entityId, 2, CapsuleVertical(feet, pelvis, torsoTop, torsoR), tick);
            Add(destination, entityId, 3, Limb(feet, shoulder, right, forward, -1, armR, 0.35f * standFactor), tick);
            Add(destination, entityId, 4, Limb(feet, shoulder, right, forward, +1, armR, 0.35f * standFactor), tick);
            Add(destination, entityId, 5, CapsuleVertical(Offset(feet, right, -0.1f * standFactor), legR, pelvis - legR, legR), tick);
            Add(destination, entityId, 6, CapsuleVertical(Offset(feet, right, 0.1f * standFactor), legR, pelvis - legR, legR), tick);
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
