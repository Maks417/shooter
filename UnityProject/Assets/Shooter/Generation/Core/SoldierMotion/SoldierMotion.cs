#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using static Shooter.Generation.GeometryMath;
using static Shooter.Generation.MotionMath;

namespace Shooter.Generation
{
    /// <summary>Cosmetic ground sampling. The caller owns authoritative root motion; this port never moves it.</summary>
    public interface ISoldierGround
    {
        bool TrySample(float worldX, float worldZ, out float heightMeters);
    }

    public readonly struct SoldierMotionInput
    {
        public Float3 Root { get; }
        public float YawRadians { get; }
        public double DistanceMeters { get; }
        public float SpeedMetersPerSecond { get; }
        public float Crouch { get; }
        public float AimYawRadians { get; }
        public float AimPitchRadians { get; }
        public float ReloadPhase { get; }
        public bool Reset { get; }
        public SoldierMotionInput(Float3 root, float yawRadians, double distanceMeters, float speedMetersPerSecond,
            float crouch = 0, float aimYawRadians = 0, float aimPitchRadians = 0, float reloadPhase = -1, bool reset = false)
        {
            if (!Finite(yawRadians) || Math.Abs(yawRadians) > 1000000 || double.IsNaN(distanceMeters) || double.IsInfinity(distanceMeters) || distanceMeters < 0 || distanceMeters > 10000000 ||
                !Finite(speedMetersPerSecond) || speedMetersPerSecond < 0 || speedMetersPerSecond > 5.5f || !Finite(crouch) || crouch < 0 || crouch > 1 ||
                !Finite(aimYawRadians) || Math.Abs(aimYawRadians) > SoldierMotion.MaxAimYawRadians + 1e-6f ||
                !Finite(aimPitchRadians) || Math.Abs(aimPitchRadians) > SoldierMotion.MaxAimPitchRadians + 1e-6f ||
                !Finite(reloadPhase) || (reloadPhase != -1 && (reloadPhase < 0 || reloadPhase > 1)))
                throw new ArgumentOutOfRangeException(nameof(distanceMeters), "Unsupported motion input; aim is bounded to +/-45 yaw and +/-25 pitch degrees.");
            Root = root; YawRadians = yawRadians; DistanceMeters = distanceMeters; SpeedMetersPerSecond = speedMetersPerSecond;
            Crouch = crouch; AimYawRadians = aimYawRadians; AimPitchRadians = aimPitchRadians; ReloadPhase = reloadPhase; Reset = reset;
        }
    }

    /// <summary>A per-rig reused view, valid until its next Evaluate call. Copy when retaining samples.</summary>
    public sealed class SoldierPose
    {
        internal readonly Float3[] Positions = new Float3[SoldierSkeleton.JointCount];
        internal readonly Quat[] Rotations = new Quat[SoldierSkeleton.JointCount];
        public IReadOnlyList<Float3> WorldPositions { get; }
        public IReadOnlyList<Quat> WorldRotations { get; }
        public Float3 WeaponPosition { get; internal set; }
        public Quat WeaponRotation { get; internal set; }
        public Float3 LeftHandTarget { get; internal set; }
        public Float3 RightHandTarget { get; internal set; }
        public float LeftGripError { get; internal set; }
        public float RightGripError { get; internal set; }
        public float PlantSlide { get; internal set; }
        public float PlantTargetError { get; internal set; }
        public float PelvisCorrection { get; internal set; }
        public bool LeftPlanted { get; internal set; }
        public bool RightPlanted { get; internal set; }
        public bool ContactsReset { get; internal set; }
        public int GroundQueries { get; internal set; }
        public float LeftElbowFlexion { get; internal set; }
        public float RightElbowFlexion { get; internal set; }
        public float LeftKneeFlexion { get; internal set; }
        public float RightKneeFlexion { get; internal set; }
        public float LeftWristOrientationErrorRadians { get; internal set; }
        public float RightWristOrientationErrorRadians { get; internal set; }
        internal SoldierPose() { WorldPositions = Array.AsReadOnly(Positions); WorldRotations = Array.AsReadOnly(Rotations); }
    }

    /// <summary>Initial semantic23 view-only gait/IK spike. No clips, physics mutation or frame-time accumulation.</summary>
    public sealed class SoldierMotion
    {
        public const string Version = "soldier.motion.spike.v1";
        public const float MaxAimYawRadians = (float)Math.PI / 4;
        public const float MaxAimPitchRadians = 25 * (float)Math.PI / 180;
        private const float Radians = (float)Math.PI / 180;
        private readonly SoldierPose pose = new SoldierPose();
        private readonly FootState left = new FootState(), right = new FootState();
        private readonly Float3 grip, support, magazine;
        private bool initialized, running;
        private double previousDistance;
        private Float3 previousRoot;
        private static readonly Float3 Up = new Float3(0, 1, 0), Forward = new Float3(0, 0, 1);

        public SoldierMotion(WeaponRecipe rifle)
        {
            if (rifle == null || rifle.Kind != "rifle") throw new ArgumentException("The M1 motion spike requires the validated baseline rifle.", nameof(rifle));
            foreach (var socket in WeaponGeometry.Sockets(rifle))
            {
                if (socket.Id == "right-hand-grip") grip = socket.Position;
                else if (socket.Id == "left-hand-grip") support = socket.Position;
                else if (socket.Id == "magazine-grip") magazine = socket.Position;
            }
        }

        public SoldierPose Evaluate(SoldierMotionInput input, ISoldierGround ground)
        {
            try { return EvaluateCore(input, ground); }
            catch { initialized = false; throw; } // A failed ground dependency cannot leave half-initialized contacts reusable.
        }
        private SoldierPose EvaluateCore(SoldierMotionInput input, ISoldierGround ground)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            bool run = input.SpeedMetersPerSecond > 3.5f;
            double travel = input.DistanceMeters - previousDistance;
            bool reset = !initialized || input.Reset || run != running || travel < 0 || travel > .5 || Sub(input.Root, previousRoot).LengthSquared > .25;
            initialized = true; running = run; previousDistance = input.DistanceMeters; previousRoot = input.Root;
            pose.ContactsReset = reset; pose.GroundQueries = 0;
            Quat yaw = AxisAngle(Up, input.YawRadians);
            float cycle = run ? 2.2f : 1.4f, duty = run ? .45f : .62f, clearance = run ? .12f : .08f;
            double phase = input.DistanceMeters / cycle; phase -= Math.Floor(phase);
            bool moving = input.SpeedMetersPerSecond > .001f;
            var leftTarget = FootTarget(left, -.1f, phase, cycle, duty, clearance, moving, reset, input.Root, yaw, ground);
            double rightPhase = phase + .5; rightPhase -= Math.Floor(rightPhase);
            var rightTarget = FootTarget(right, .1f, rightPhase, cycle, duty, clearance, moving, reset, input.Root, yaw, ground);
            float pelvisY = input.Root.Y + .96f - input.Crouch * .30f;
            float desired = Math.Min(RequiredPelvis(leftTarget, -.1f, input.Root, yaw), RequiredPelvis(rightTarget, .1f, input.Root, yaw));
            pose.PelvisCorrection = Clamp(desired - pelvisY, -.08f, 0);
            pelvisY += pose.PelvisCorrection;
            var p = pose.Positions; var r = pose.Rotations;
            p[0] = input.Root; r[0] = yaw; p[1] = new Float3(input.Root.X, pelvisY, input.Root.Z); r[1] = yaw;
            // Distribute the documented torso45deg yaw/25deg pitch over the three spine joints.
            for (int i = 2; i <= 4; i++)
            {
                p[i] = Add(p[i - 1], Rotate(SoldierSkeleton.Definition.Joints[i].LocalBindPosition, r[i - 1]));
                r[i] = Multiply(yaw, Multiply(AxisAngle(Up, input.AimYawRadians * (i - 1) / 3), AxisAngle(new Float3(1, 0, 0), -input.AimPitchRadians * (i - 1) / 3)));
            }
            SetChild(5, r[4]); SetChild(6, r[4]); SetChild(7, r[4]); SetChild(8, r[4]); SetChild(11, r[4]); SetChild(12, r[4]);
            SetChild(15, yaw); SetChild(19, yaw);
            var leftLeg = Leg(15, 16, 17, 18, leftTarget, yaw); var rightLeg = Leg(19, 20, 21, 22, rightTarget, yaw);
            pose.LeftKneeFlexion = leftLeg.FlexionRadians; pose.RightKneeFlexion = rightLeg.FlexionRadians;
            pose.LeftPlanted = left.Stance; pose.RightPlanted = right.Stance;
            pose.PlantTargetError = Math.Max(left.Stance ? leftLeg.TargetError : 0, right.Stance ? rightLeg.TargetError : 0);
            float leftSlide = TrackPlant(left, p[17]), rightSlide = TrackPlant(right, p[21]);
            pose.PlantSlide = Math.Max(leftSlide, rightSlide);
            pose.WeaponRotation = r[4];
            pose.WeaponPosition = Add(p[4], Rotate(new Float3(0, .16f, .10f), r[4]));
            pose.RightHandTarget = Add(pose.WeaponPosition, Rotate(grip, pose.WeaponRotation));
            var heldSupport = Add(pose.WeaponPosition, Rotate(support, pose.WeaponRotation));
            var mag = Add(pose.WeaponPosition, Rotate(magazine, pose.WeaponRotation));
            var belt = Add(p[1], Rotate(new Float3(-.15f, -.05f, .1f), yaw));
            pose.LeftHandTarget = input.ReloadPhase < 0 ? heldSupport : ReloadTarget(input.ReloadPhase, heldSupport, mag, belt);
            var leftArm = Arm(8, 9, 10, pose.LeftHandTarget, r[4], -1);
            var rightArm = Arm(12, 13, 14, pose.RightHandTarget, r[4], 1);
            pose.LeftGripError = leftArm.TargetError; pose.RightGripError = rightArm.TargetError;
            pose.LeftElbowFlexion = leftArm.FlexionRadians; pose.RightElbowFlexion = rightArm.FlexionRadians;
            pose.LeftWristOrientationErrorRadians = AngularError(r[10], pose.WeaponRotation);
            pose.RightWristOrientationErrorRadians = AngularError(r[14], pose.WeaponRotation);
            return pose;
        }

        private void SetChild(int id, Quat rotation)
        {
            int parent = SoldierSkeleton.Definition.Joints[id].ParentId;
            pose.Positions[id] = Add(pose.Positions[parent], Rotate(SoldierSkeleton.Definition.Joints[id].LocalBindPosition, pose.Rotations[parent]));
            pose.Rotations[id] = rotation;
        }
        private TwoBoneResult Leg(int thigh, int calf, int foot, int toe, Float3 target, Quat yaw)
        {
            var p = pose.Positions; var r = pose.Rotations;
            float lower = (float)Math.Sqrt(SoldierSkeleton.Definition.Joints[foot].LocalBindPosition.LengthSquared);
            var result = TwoBoneIk.Solve(p[thigh], target, Rotate(Forward, yaw), .45f, lower, 150 * Radians, .98f, Rotate(new Float3(0, -1, 0), yaw), 75 * Radians);
            p[calf] = result.Joint; p[foot] = result.End;
            r[thigh] = SegmentRotation(thigh, calf, yaw); r[calf] = SegmentRotation(calf, foot, yaw);
            // Ankle pitch clamp relative to the lower-leg frame. Ground-normal orientation is a later adapter refinement.
            var direction = Rotate(Sub(p[foot], p[calf]), AxisAngle(Up, -(float)(2 * Math.Atan2(yaw.Y, yaw.W))));
            float calfPitch = (float)Math.Atan2(-direction.Z, -direction.Y) - (float)Math.Atan2(-.02f, .39f);
            float footPitch = calfPitch + Clamp(-calfPitch, -35 * Radians, 25 * Radians);
            r[foot] = Multiply(yaw, AxisAngle(new Float3(1, 0, 0), footPitch));
            p[toe] = Add(p[foot], Rotate(SoldierSkeleton.Definition.Joints[toe].LocalBindPosition, r[foot])); r[toe] = r[foot];
            return result;
        }
        private TwoBoneResult Arm(int upper, int forearm, int hand, Float3 target, Quat chest, float side)
        {
            var p = pose.Positions; var r = pose.Rotations;
            var result = TwoBoneIk.Solve(p[upper], target, Rotate(new Float3(0, 0, -1), chest), .29f, .26f, 145 * Radians, .98f,
                Rotate(new Float3(side, 0, 0), chest), 90 * Radians);
            p[forearm] = result.Joint; p[hand] = result.End;
            r[upper] = SegmentRotation(upper, forearm, chest); r[forearm] = SegmentRotation(forearm, hand, chest);
            // The baseline has no authored palm frame. Respect a conservative45deg swing/30deg twist
            // envelope and report the remaining weapon-frame orientation discrepancy for visual review.
            r[hand] = LimitSwingTwist(pose.WeaponRotation, r[forearm], new Float3(side, 0, 0), 45 * Radians, 30 * Radians);
            return result;
        }
        private Quat SegmentRotation(int parent, int child, Quat reference)
        {
            var bind = Sub(SoldierSkeleton.BindPositions[child], SoldierSkeleton.BindPositions[parent]);
            return Multiply(FromTo(Rotate(bind, reference), Sub(pose.Positions[child], pose.Positions[parent])), reference);
        }
        private float RequiredPelvis(Float3 target, float lateral, Float3 root, Quat yaw)
        {
            var hip = Add(root, Rotate(new Float3(lateral, 0, 0), yaw));
            float dx = target.X - hip.X, dz = target.Z - hip.Z;
            double max = (.45 + Math.Sqrt(.39 * .39 + .02 * .02)) * .98;
            return target.Y + .04f + (float)Math.Sqrt(Math.Max(0, max * max - dx * dx - dz * dz));
        }
        private Float3 FootTarget(FootState foot, float lateral, double phase, float cycle, float duty, float clearance,
            bool moving, bool reset, Float3 root, Quat yaw, ISoldierGround ground)
        {
            bool stance = !moving || phase < duty;
            if (reset || moving != foot.Moving)
            {
                foot.Stance = stance; foot.Moving = moving; foot.NewPlant = true;
                if (stance)
                {
                    float ahead = moving ? (duty * .5f - (float)phase) * cycle : 0;
                    foot.Anchor = Sample(Add(root, Rotate(new Float3(lateral, .08f, .02f + ahead), yaw)), ground);
                    foot.Start = foot.Anchor; foot.End = foot.Anchor;
                }
                else
                {
                    foot.Start = Add(root, Rotate(new Float3(lateral, .08f, .02f - duty * cycle * .5f), yaw));
                    foot.End = Sample(Add(root, Rotate(new Float3(lateral, .08f, .02f + (1 - (float)phase + duty * .5f) * cycle), yaw)), ground);
                }
            }
            else if (stance && !foot.Stance)
            { foot.Anchor = foot.End; foot.NewPlant = true; }
            else if (!stance && foot.Stance)
            {
                foot.Start = foot.Anchor;
                foot.End = Sample(Add(root, Rotate(new Float3(lateral, .08f, .02f + (1 - (float)phase + duty * .5f) * cycle), yaw)), ground);
            }
            foot.Stance = stance; foot.Moving = moving;
            if (stance) return foot.Anchor;
            float t = (float)((phase - duty) / (1 - duty));
            var target = Lerp(foot.Start, foot.End, Smooth(t));
            return Add(target, new Float3(0, clearance * (float)Math.Sin(Math.PI * t), 0));
        }
        private Float3 Sample(Float3 point, ISoldierGround ground)
        {
            pose.GroundQueries++;
            if (pose.GroundQueries > 2) throw new InvalidOperationException("Per-rig foot query budget exceeded.");
            if (!ground.TrySample(point.X, point.Z, out float height) || !Finite(height)) throw new InvalidOperationException("Required cosmetic foot ground sample unavailable.");
            return new Float3(point.X, height + .08f, point.Z);
        }
        private static float TrackPlant(FootState foot, Float3 actual)
        {
            if (!foot.Stance) return 0;
            if (foot.NewPlant) { foot.ActualStart = actual; foot.NewPlant = false; }
            return (float)Math.Sqrt(Sub(actual, foot.ActualStart).LengthSquared);
        }
        public static Float3 ReloadTarget(float phase, Float3 support, Float3 magazine, Float3 belt)
        {
            if (!Finite(phase) || phase < 0 || phase > 1) throw new ArgumentOutOfRangeException(nameof(phase));
            var withdrawn = Add(magazine, new Float3(0, -.18f, 0));
            if (phase < .2f) return Lerp(support, magazine, Smooth(phase / .2f));
            if (phase < .45f) return Lerp(magazine, withdrawn, Smooth((phase - .2f) / .25f));
            if (phase < .7f) return Lerp(withdrawn, belt, Smooth((phase - .45f) / .25f));
            if (phase < .9f) return Lerp(belt, magazine, Smooth((phase - .7f) / .2f));
            return Lerp(magazine, support, Smooth((phase - .9f) / .1f));
        }
        private sealed class FootState
        {
            public bool Stance, Moving, NewPlant;
            public Float3 Anchor, Start, End, ActualStart;
        }
    }
}
