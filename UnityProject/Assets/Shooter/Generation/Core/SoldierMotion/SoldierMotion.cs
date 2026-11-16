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
        /// <summary>-1 living; 0..1 analytic death collapse (cosmetic only).</summary>
        public float DeathPhase { get; }
        /// <summary>0..1 cosmetic slide dive (motor still owns root translation).</summary>
        public float Slide { get; }
        public SoldierMotionInput(Float3 root, float yawRadians, double distanceMeters, float speedMetersPerSecond,
            float crouch = 0, float aimYawRadians = 0, float aimPitchRadians = 0, float reloadPhase = -1, bool reset = false,
            float deathPhase = -1, float slide = 0)
        {
            if (!Finite(yawRadians) || Math.Abs(yawRadians) > 1000000 || double.IsNaN(distanceMeters) || double.IsInfinity(distanceMeters) || distanceMeters < 0 || distanceMeters > 10000000 ||
                !Finite(speedMetersPerSecond) || speedMetersPerSecond < 0 || speedMetersPerSecond > 5.5f || !Finite(crouch) || crouch < 0 || crouch > 1 ||
                !Finite(aimYawRadians) || Math.Abs(aimYawRadians) > SoldierMotion.MaxAimYawRadians + 1e-6f ||
                !Finite(aimPitchRadians) || Math.Abs(aimPitchRadians) > SoldierMotion.MaxAimPitchRadians + 1e-6f ||
                !Finite(reloadPhase) || (reloadPhase != -1 && (reloadPhase < 0 || reloadPhase > 1)) ||
                !Finite(deathPhase) || (deathPhase != -1 && (deathPhase < 0 || deathPhase > 1)) ||
                !Finite(slide) || slide < 0 || slide > 1)
                throw new ArgumentOutOfRangeException(nameof(distanceMeters), "Unsupported motion input; aim is bounded to +/-45 yaw and +/-25 pitch degrees.");
            Root = root; YawRadians = yawRadians; DistanceMeters = distanceMeters; SpeedMetersPerSecond = speedMetersPerSecond;
            Crouch = crouch; AimYawRadians = aimYawRadians; AimPitchRadians = aimPitchRadians; ReloadPhase = reloadPhase; Reset = reset;
            DeathPhase = deathPhase; Slide = slide;
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
        public bool UpperBodyCacheHit { get; internal set; }
        public float LeftElbowFlexion { get; internal set; }
        public float RightElbowFlexion { get; internal set; }
        public float LeftKneeFlexion { get; internal set; }
        public float RightKneeFlexion { get; internal set; }
        public float LeftWristOrientationErrorRadians { get; internal set; }
        public float RightWristOrientationErrorRadians { get; internal set; }
        public float RightStockErrorMeters { get; internal set; }
        public float EyeSightLineErrorMeters { get; internal set; }
        public Float3 RightEyePosition { get; internal set; }
        public float ArmBodyClearanceMeters { get; internal set; }
        internal SoldierPose() { WorldPositions = Array.AsReadOnly(Positions); WorldRotations = Array.AsReadOnly(Rotations); }
    }

    /// <summary>Initial semantic23 view-only gait/IK spike. No clips, physics mutation or frame-time accumulation.</summary>
    public sealed class SoldierMotion
    {
        public const string Version = "soldier.motion.spike.v5";
        public const float MaxAimYawRadians = (float)Math.PI / 4;
        public const float MaxAimPitchRadians = 25 * (float)Math.PI / 180;
        /// <summary>Presentation advance duration for DeathPhase 0→1 (seconds).</summary>
        public const float DeathCollapseDurationSeconds = 0.75f;
        /// <summary>Walk→run enter / run→walk leave speeds (hysteresis avoids frame-speed chatter resets).</summary>
        public const float RunEnterSpeed = 3.8f;
        public const float RunLeaveSpeed = 3.2f;
        private const float Radians = (float)Math.PI / 180;
        public const float BodyBladeRadians = 35 * Radians;
        public const float LegHeadingOffsetRadians = 5 * Radians;
        public static readonly Float3 PouchContactChestLocal = new Float3(-.24f,-.28f,.20f);
        private readonly SoldierPose pose = new SoldierPose();
        private readonly ArmBodyClearance.Context armBody = new ArmBodyClearance.Context();
        private readonly FootState left = new FootState(), right = new FootState();
        private Float3 grip, support, magazine, stock, sight;
        private readonly bool upgraded;
        private bool initialized, running, wasMoving;
        private double previousDistance;
        private Float3 previousRoot;
        private readonly Float3[] cachedUpperPositions=new Float3[10];
        private readonly Quat[] cachedUpperRotations=new Quat[10];
        private readonly SoldierPose cachedUpper=new SoldierPose();
        private bool upperCacheValid;
        private float cachedYaw,cachedAimYaw,cachedPitch,cachedReload;
        private Float3 cachedChest;
        private static readonly Float3 Up = new Float3(0, 1, 0), Forward = new Float3(0, 0, 1);

        public SoldierMotion(WeaponRecipe rifle, bool upgraded = false)
        {
            this.upgraded=upgraded;
            if (rifle == null || rifle.Kind != "rifle") throw new ArgumentException("The M1 motion spike requires the validated baseline rifle.", nameof(rifle));
            foreach (var socket in WeaponGeometry.Sockets(rifle))
            {
                if (socket.Id == "right-hand-grip") grip = socket.Position;
                else if (socket.Id == "left-hand-grip") support = socket.Position;
                else if (socket.Id == "magazine-grip") magazine = socket.Position;
                else if (socket.Id == "shoulder") stock = socket.Position;
                else if (socket.Id == "sight-eye") sight = socket.Position;
            }
        }

        public void SetVisualWeapon(WeaponRecipe recipe)
        {
            foreach(var socket in WeaponGeometry.Sockets(recipe))
            {
                if(socket.Id=="right-hand-grip")grip=socket.Position;
                else if(socket.Id=="left-hand-grip")support=socket.Position;
                else if(socket.Id=="magazine-grip")magazine=socket.Position;
                else if(socket.Id=="shoulder")stock=socket.Position;
                else if(socket.Id=="sight-eye")sight=socket.Position;
            }
            upperCacheValid=false;
        }
        private void AlignHeadToSight(SoldierMotionInput input)
        {
            // Bounded two-segment neck look-at; root/chest, weapon and all hit poses stay untouched.
            var p=pose.Positions;var r=pose.Rotations;
            var aim=Multiply(AxisAngle(Up,input.YawRadians+input.AimYawRadians),AxisAngle(new Float3(1,0,0),-input.AimPitchRadians));
            var axis=Rotate(Forward,pose.WeaponRotation);
            var leftArm=TwoBoneIk.Solve(p[8],pose.LeftHandTarget,Rotate(new Float3(-1,-.65f,.35f),r[4]),.29f,.26f,145*Radians,.98f);
            var rightArm=TwoBoneIk.Solve(p[12],pose.RightHandTarget,Rotate(new Float3(1,-.65f,.35f),r[4]),.29f,.26f,145*Radians,.98f);
            Float3 target=p[6];Quat headAim=aim;float best=float.MinValue;
            const float reach=.272f;
            for(int yaw=-1;yaw<=1;yaw++)for(int pitch=-1;pitch<=1;pitch++)for(int side=-1;side<=1;side+=2)
            {
                var orientation=Multiply(aim,Multiply(AxisAngle(Up,yaw*20*Radians),AxisAngle(new Float3(1,0,0),pitch*15*Radians)));
                var offset=Rotate(new Float3(.035f,-.005f,.103f),orientation);
                var line=Sub(Add(pose.WeaponPosition,Rotate(sight,pose.WeaponRotation)),offset);
                var delta=Sub(line,p[4]);var perpendicular=Sub(delta,Scale(axis,Dot(delta,axis)));
                float r2=reach*reach-(float)perpendicular.LengthSquared;if(r2<0)continue;
                var center=Add(p[4],Add(perpendicular,Scale(axis,side*(float)Math.Sqrt(r2))));
                var inverse=Inverse(orientation);float score=float.MaxValue;
                for(int arm=0;arm<2;arm++)for(int k=0;k<=6;k++)
                {
                    var elbow=arm==0?leftArm.Joint:rightArm.Joint;var hand=arm==0?leftArm.End:rightArm.End;
                    var q=Rotate(Sub(Lerp(elbow,hand,k/6f),center),inverse);
                    float distance=((float)Math.Sqrt(q.X*q.X/(.106f*.106f)+q.Y*q.Y/(.145f*.145f)+q.Z*q.Z/(.116f*.116f))-1)*.106f-(.054f-.015f*k/6f);
                    score=Math.Min(score,distance);
                }
                score-=.00002f*(Math.Abs(yaw)+Math.Abs(pitch));
                if(score>best){best=score;target=center;headAim=orientation;}
            }

            var neck=TwoBoneIk.Solve(p[4],target,Rotate(new Float3(0,0,-1),r[4]),.16f,.12f,75*Radians,.98f);
            p[5]=neck.Joint;p[6]=neck.End;
            r[5]=SegmentRotation(5,6,r[4]);r[6]=headAim;
        }

        public SoldierPose Evaluate(SoldierMotionInput input, ISoldierGround ground)
        {
            try { return EvaluateCore(input, ground); }
            catch { initialized = false; throw; } // A failed ground dependency cannot leave half-initialized contacts reusable.
        }
        private SoldierPose EvaluateCore(SoldierMotionInput input, ISoldierGround ground)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (input.DeathPhase >= 0)
                return EvaluateDeathCollapse(input, ground);
            if (input.Slide > .01f)
                return EvaluateSlide(input, ground);
            // Hysteresis: enter run above RunEnterSpeed, leave below RunLeaveSpeed (avoids ContactsReset thrash).
            bool run = !initialized ? input.SpeedMetersPerSecond > 3.5f
                : running ? input.SpeedMetersPerSecond >= RunLeaveSpeed
                : input.SpeedMetersPerSecond > RunEnterSpeed;
            bool moving = input.SpeedMetersPerSecond > .001f;
            double travel = input.DistanceMeters - previousDistance;
            bool reset = !initialized || input.Reset || run != running || moving != wasMoving || travel < 0 || travel > .5 || Sub(input.Root, previousRoot).LengthSquared > .25;
            initialized = true; running = run; wasMoving = moving; previousDistance = input.DistanceMeters; previousRoot = input.Root;
            pose.ContactsReset = reset; pose.GroundQueries = 0;
            Quat yaw = AxisAngle(Up, input.YawRadians);
            // Pelvis stance is cosmetic; root position/rotation and requested weapon aim remain the input.
            Quat bodyYaw = Multiply(yaw, AxisAngle(Up, BodyBladeRadians));
            Quat legYaw = Multiply(yaw, AxisAngle(Up, LegHeadingOffsetRadians));
            // v5 run: slightly longer plant duty and clearance so sprint feet read more clearly.
            float cycle = run ? 2.2f : 1.4f, duty = run ? .38f : .50f, clearance = run ? .14f : .08f;
            double phase = input.DistanceMeters / cycle; phase -= Math.Floor(phase);
            var leftTarget = FootTarget(left, -.1f, phase, cycle, duty, clearance, moving, reset, input.Root, yaw, bodyYaw, ground);
            double rightPhase = phase + .5; rightPhase -= Math.Floor(rightPhase);
            var rightTarget = FootTarget(right, .1f, rightPhase, cycle, duty, clearance, moving, reset, input.Root, yaw, bodyYaw, ground);
            // Version2 uses a bent-knee locomotion posture. Full crouch remains the original.66m;
            // locomotion lowering is interpolated, never stacked onto the crouch displacement.
            float nominalHeight = moving ? .78f : .96f;
            float pelvisY = input.Root.Y + nominalHeight + (.66f - nominalHeight) * input.Crouch;
            float desired = Math.Min(RequiredPelvis(leftTarget, -.1f, input.Root, bodyYaw), RequiredPelvis(rightTarget, .1f, input.Root, bodyYaw));
            pose.PelvisCorrection = Clamp(desired - pelvisY, -.08f, 0);
            pelvisY += pose.PelvisCorrection;
            var p = pose.Positions; var r = pose.Rotations;
            p[0] = input.Root; r[0] = yaw; p[1] = new Float3(input.Root.X, pelvisY, input.Root.Z); r[1] = bodyYaw;
            // Distribute the documented torso45deg yaw/25deg pitch over the three spine joints.
            for (int i = 2; i <= 4; i++)
            {
                p[i] = Add(p[i - 1], Rotate(SoldierSkeleton.Definition.Joints[i].LocalBindPosition, r[i - 1]));
                r[i] = Multiply(bodyYaw, Multiply(AxisAngle(Up, input.AimYawRadians * (i - 1) / 3), AxisAngle(new Float3(1, 0, 0), -input.AimPitchRadians * (i - 1) / 3)));
            }
            SetChild(5, Multiply(r[4], Multiply(AxisAngle(new Float3(0,0,1), -30*Radians), AxisAngle(new Float3(1,0,0),30*Radians))));
            SetChild(6, Multiply(r[5],AxisAngle(Up,-35*Radians)));
            SetChild(7, Multiply(r[4],AxisAngle(Up,45*Radians))); SetChild(8,r[4]);
            SetChild(11, Multiply(r[4],AxisAngle(new Float3(0,0,1),40*Radians))); SetChild(12,r[4]);
            SetChild(15, bodyYaw); SetChild(19, bodyYaw);
            var leftLeg = Leg(15, 16, 17, 18, leftTarget, legYaw); var rightLeg = Leg(19, 20, 21, 22, rightTarget, legYaw);
            pose.LeftKneeFlexion = leftLeg.FlexionRadians; pose.RightKneeFlexion = rightLeg.FlexionRadians;
            pose.LeftPlanted = left.Stance; pose.RightPlanted = right.Stance;
            pose.PlantTargetError = Math.Max(left.Stance ? leftLeg.TargetError : 0, right.Stance ? rightLeg.TargetError : 0);
            float leftSlide = TrackPlant(left, p[17]), rightSlide = TrackPlant(right, p[21]);
            pose.PlantSlide = Math.Max(leftSlide, rightSlide);
            if(TryRestoreUpper(input))return pose;
            pose.UpperBodyCacheHit=false;
            pose.WeaponRotation = Multiply(yaw,Multiply(AxisAngle(Up,input.AimYawRadians),AxisAngle(new Float3(1,0,0),-input.AimPitchRadians)));
            if(input.ReloadPhase>=0)
            {
                float envelope=(float)Math.Sin(Math.PI*input.ReloadPhase);envelope*=envelope;
                pose.WeaponRotation=Multiply(pose.WeaponRotation,AxisAngle(Forward,-45*Radians*envelope));
            }
            pose.WeaponPosition = Sub(p[12], Rotate(stock,pose.WeaponRotation));
            pose.RightHandTarget = Add(pose.WeaponPosition, Rotate(grip, pose.WeaponRotation));
            var heldSupport = Add(pose.WeaponPosition, Rotate(support, pose.WeaponRotation));
            var mag = Add(pose.WeaponPosition, Rotate(magazine, pose.WeaponRotation));
            var belt = Add(p[4], Rotate(upgraded?new Float3(-.24f,-.28f,.22f):PouchContactChestLocal, r[4]));
            pose.LeftHandTarget = input.ReloadPhase < 0 ? heldSupport : ReloadTask(input.ReloadPhase, heldSupport, mag, belt,r[4],pose.WeaponRotation);
            // Clavicle protraction follows the equipment target's reach requirement, not a joint-keyframe phase.
            float protraction=SupportProtraction(pose.LeftHandTarget);
            SetChild(7,Multiply(r[4],AxisAngle(Up,protraction)));SetChild(8,r[4]);
            if(upgraded)AlignHeadToSight(input);
            armBody.Update(pose);
            var leftArm = Arm(8, 9, 10, pose.LeftHandTarget, r[7], -1);
            var rightArm = Arm(12, 13, 14, pose.RightHandTarget, r[11], 1);
            pose.LeftGripError = leftArm.TargetError; pose.RightGripError = rightArm.TargetError;
            pose.LeftElbowFlexion = leftArm.FlexionRadians; pose.RightElbowFlexion = rightArm.FlexionRadians;
            pose.LeftWristOrientationErrorRadians = AngularError(r[10], Multiply(pose.WeaponRotation,AxisAngle(Up,90*Radians)));
            pose.RightWristOrientationErrorRadians = AngularError(r[14], Multiply(pose.WeaponRotation,AxisAngle(new Float3(0,0,1),90*Radians)));
            pose.RightStockErrorMeters=(float)Math.Sqrt(Sub(Add(pose.WeaponPosition,Rotate(stock,pose.WeaponRotation)),p[12]).LengthSquared);
            pose.RightEyePosition=Add(p[6],Rotate(new Float3(.035f,-.005f,.103f),r[6]));
            var eyeDelta=Sub(pose.RightEyePosition,Add(pose.WeaponPosition,Rotate(sight,pose.WeaponRotation)));
            var axis=Rotate(Forward,pose.WeaponRotation);
            pose.EyeSightLineErrorMeters=(float)Math.Sqrt(Sub(eyeDelta,Scale(axis,Dot(eyeDelta,axis))).LengthSquared);
            pose.ArmBodyClearanceMeters=Math.Min(armBody.Arm(p[8],p[9],p[10]),armBody.Arm(p[12],p[13],p[14]));
            StoreUpper(input);
            return pose;
        }

        /// <summary>
        /// Cosmetic slide dive while the motor owns root translation (ADR-039 capsule crouch unchanged).
        /// Lead/trail feet track with the moving root; torso pitches forward; weapon braced low.
        /// </summary>
        private SoldierPose EvaluateSlide(SoldierMotionInput input, ISoldierGround ground)
        {
            float s = Clamp(input.Slide, 0, 1);
            initialized = true;
            running = false;
            wasMoving = false;
            previousDistance = input.DistanceMeters;
            previousRoot = input.Root;
            upperCacheValid = false;
            left.Stance = right.Stance = false;
            pose.ContactsReset = true;
            pose.GroundQueries = 0;
            pose.UpperBodyCacheHit = false;
            pose.PlantSlide = 0;
            pose.PlantTargetError = 0;
            pose.PelvisCorrection = 0;
            pose.LeftPlanted = pose.RightPlanted = false;

            // Two foot ground samples (budget 2); pelvis uses the same flat height as the root.
            pose.GroundQueries = 0;
            Quat yaw = AxisAngle(Up, input.YawRadians);
            Quat bodyYaw = Multiply(yaw, AxisAngle(Up, BodyBladeRadians));
            Quat legYaw = Multiply(yaw, AxisAngle(Up, LegHeadingOffsetRadians));
            Float3 forward = Rotate(Forward, yaw);
            Float3 rightAxis = Rotate(new Float3(1, 0, 0), yaw);
            Quat pitchDown = AxisAngle(new Float3(1, 0, 0), 58f * Radians * s);
            Quat bodyRot = Multiply(bodyYaw, pitchDown);
            Quat slideLegYaw = Multiply(legYaw, pitchDown);

            float groundY = input.Root.Y;
            float pelvisY = groundY + Lerp(.66f, .50f, s);
            var p = pose.Positions; var r = pose.Rotations;
            p[0] = new Float3(input.Root.X, groundY, input.Root.Z); r[0] = yaw;
            p[1] = new Float3(input.Root.X, pelvisY, input.Root.Z); r[1] = bodyRot;

            float spineFold = 12f * Radians * s;
            for (int i = 2; i <= 4; i++)
            {
                p[i] = Add(p[i - 1], Rotate(SoldierSkeleton.Definition.Joints[i].LocalBindPosition, r[i - 1]));
                r[i] = Multiply(bodyRot, Multiply(
                    AxisAngle(Up, input.AimYawRadians * (i - 1) / 3f),
                    AxisAngle(new Float3(1, 0, 0), -input.AimPitchRadians * (i - 1) / 3f - spineFold * (i - 1) / 3f)));
            }
            SetChild(5, Multiply(r[4], Multiply(AxisAngle(new Float3(0, 0, 1), -30 * Radians), AxisAngle(new Float3(1, 0, 0), 20 * Radians))));
            SetChild(6, Multiply(r[5], AxisAngle(Up, -35 * Radians)));
            SetChild(7, Multiply(r[4], AxisAngle(Up, 25 * Radians))); SetChild(8, r[4]);
            SetChild(11, Multiply(r[4], AxisAngle(new Float3(0, 0, 1), 30 * Radians))); SetChild(12, r[4]);
            SetChild(15, bodyRot); SetChild(19, bodyRot);

            // Lead foot forward, trail foot aft — both near ground, tracking the moving root (no idle plant).
            Float3 trail = Add(input.Root, Add(Scale(forward, -.42f * s), Scale(rightAxis, -.10f)));
            Float3 lead = Add(input.Root, Add(Scale(forward, .55f * s), Scale(rightAxis, .10f)));
            trail = Sample(trail, ground);
            lead = Sample(lead, ground);
            var leftLeg = Leg(15, 16, 17, 18, trail, slideLegYaw);
            var rightLeg = Leg(19, 20, 21, 22, lead, slideLegYaw);
            pose.LeftKneeFlexion = leftLeg.FlexionRadians; pose.RightKneeFlexion = rightLeg.FlexionRadians;
            pose.PlantTargetError = Math.Max(leftLeg.TargetError, rightLeg.TargetError);

            pose.WeaponRotation = Multiply(yaw, Multiply(
                AxisAngle(Up, input.AimYawRadians),
                AxisAngle(new Float3(1, 0, 0), -input.AimPitchRadians - 25f * Radians * s)));
            pose.WeaponPosition = Sub(p[12], Rotate(stock, pose.WeaponRotation));
            pose.RightHandTarget = Add(pose.WeaponPosition, Rotate(grip, pose.WeaponRotation));
            pose.LeftHandTarget = Add(pose.WeaponPosition, Rotate(support, pose.WeaponRotation));
            float protraction = SupportProtraction(pose.LeftHandTarget);
            SetChild(7, Multiply(r[4], AxisAngle(Up, protraction))); SetChild(8, r[4]);
            if(upgraded)AlignHeadToSight(input);
            armBody.Update(pose);
            var leftArm = Arm(8, 9, 10, pose.LeftHandTarget, r[7], -1);
            var rightArm = Arm(12, 13, 14, pose.RightHandTarget, r[11], 1);
            pose.LeftGripError = leftArm.TargetError; pose.RightGripError = rightArm.TargetError;
            pose.LeftElbowFlexion = leftArm.FlexionRadians; pose.RightElbowFlexion = rightArm.FlexionRadians;
            pose.LeftWristOrientationErrorRadians = AngularError(r[10], Multiply(pose.WeaponRotation, AxisAngle(Up, 90 * Radians)));
            pose.RightWristOrientationErrorRadians = AngularError(r[14], Multiply(pose.WeaponRotation, AxisAngle(new Float3(0, 0, 1), 90 * Radians)));
            pose.RightStockErrorMeters = (float)Math.Sqrt(Sub(Add(pose.WeaponPosition, Rotate(stock, pose.WeaponRotation)), p[12]).LengthSquared);
            pose.RightEyePosition = Add(p[6], Rotate(new Float3(.035f, -.005f, .103f), r[6]));
            var eyeDelta = Sub(pose.RightEyePosition, Add(pose.WeaponPosition, Rotate(sight, pose.WeaponRotation)));
            var axis = Rotate(Forward, pose.WeaponRotation);
            pose.EyeSightLineErrorMeters = (float)Math.Sqrt(Sub(eyeDelta, Scale(axis, Dot(eyeDelta, axis))).LengthSquared);
            pose.ArmBodyClearanceMeters = Math.Min(armBody.Arm(p[8], p[9], p[10]), armBody.Arm(p[12], p[13], p[14]));
            return pose;
        }

        /// <summary>
        /// Analytic joint-constrained collapse (T032). Root stays at authoritative feet; limbs ease
        /// into a side-lying ground pose and freeze at DeathPhase=1. Cosmetic only — no hit volumes.
        /// </summary>
        private SoldierPose EvaluateDeathCollapse(SoldierMotionInput input, ISoldierGround ground)
        {
            float t = Smooth(Clamp(input.DeathPhase, 0, 1));
            initialized = false;
            upperCacheValid = false;
            left.Stance = right.Stance = false;
            pose.ContactsReset = true;
            pose.GroundQueries = 0;
            pose.UpperBodyCacheHit = false;
            pose.PlantSlide = 0;
            pose.PlantTargetError = 0;
            pose.PelvisCorrection = 0;
            pose.LeftPlanted = pose.RightPlanted = false;
            previousRoot = input.Root;
            previousDistance = input.DistanceMeters;

            float groundY = input.Root.Y;
            if (ground.TrySample(input.Root.X, input.Root.Z, out float sampled) && Finite(sampled))
                groundY = sampled;
            pose.GroundQueries = 1;

            Quat yaw = AxisAngle(Up, input.YawRadians);
            Quat bodyYaw = Multiply(yaw, AxisAngle(Up, BodyBladeRadians));
            Quat legYaw = Multiply(yaw, AxisAngle(Up, LegHeadingOffsetRadians));
            Float3 forward = Rotate(Forward, yaw);
            Float3 rightAxis = Rotate(new Float3(1, 0, 0), yaw);

            float standPelvisY = groundY + .96f;
            float liePelvisY = groundY + .18f;
            float pelvisY = Lerp(standPelvisY, liePelvisY, t);
            Quat roll = AxisAngle(forward, 88f * Radians * t);
            Quat bodyRot = Multiply(roll, bodyYaw);
            Quat lieLegYaw = Multiply(roll, legYaw);

            var p = pose.Positions; var r = pose.Rotations;
            p[0] = new Float3(input.Root.X, groundY, input.Root.Z); r[0] = yaw;
            p[1] = new Float3(input.Root.X, pelvisY, input.Root.Z); r[1] = bodyRot;

            float spineFold = 18f * Radians * t;
            for (int i = 2; i <= 4; i++)
            {
                p[i] = Add(p[i - 1], Rotate(SoldierSkeleton.Definition.Joints[i].LocalBindPosition, r[i - 1]));
                r[i] = Multiply(bodyRot, AxisAngle(new Float3(1, 0, 0), -spineFold * (i - 1) / 3f));
            }
            SetChild(5, Multiply(r[4], Multiply(AxisAngle(new Float3(0, 0, 1), -30 * Radians), AxisAngle(new Float3(1, 0, 0), 30 * Radians * (1f - .4f * t)))));
            SetChild(6, Multiply(r[5], AxisAngle(Up, -35 * Radians * (1f - t))));
            SetChild(7, Multiply(r[4], AxisAngle(Up, 20 * Radians * t))); SetChild(8, r[4]);
            SetChild(11, Multiply(r[4], AxisAngle(new Float3(0, 0, 1), 40 * Radians * (1f - .5f * t)))); SetChild(12, r[4]);
            SetChild(15, bodyRot); SetChild(19, bodyRot);

            // Standing foot anchors → crumpled side-lying targets (right-side fall).
            Float3 leftStand = Add(p[0], Add(Rotate(new Float3(-.1f, .08f, .02f), bodyYaw), new Float3(0, 0, 0)));
            Float3 rightStand = Add(p[0], Rotate(new Float3(.1f, .08f, .02f), bodyYaw));
            leftStand = new Float3(leftStand.X, groundY + .08f, leftStand.Z);
            rightStand = new Float3(rightStand.X, groundY + .08f, rightStand.Z);
            Float3 leftLie = Add(p[1], Add(Scale(forward, -.15f), Add(Scale(rightAxis, -.05f), new Float3(0, .08f - (pelvisY - groundY), .35f))));
            Float3 rightLie = Add(p[1], Add(Scale(forward, .45f), Add(Scale(rightAxis, .12f), new Float3(0, .08f - (pelvisY - groundY), -.05f))));
            leftLie = new Float3(leftLie.X, Math.Max(groundY + .06f, leftLie.Y), leftLie.Z);
            rightLie = new Float3(rightLie.X, Math.Max(groundY + .06f, rightLie.Y), rightLie.Z);
            Float3 leftFoot = Lerp(leftStand, leftLie, t);
            Float3 rightFoot = Lerp(rightStand, rightLie, t);
            var leftLeg = Leg(15, 16, 17, 18, leftFoot, lieLegYaw);
            var rightLeg = Leg(19, 20, 21, 22, rightFoot, lieLegYaw);
            pose.LeftKneeFlexion = leftLeg.FlexionRadians; pose.RightKneeFlexion = rightLeg.FlexionRadians;

            Float3 leftHandStand = Add(p[4], Rotate(new Float3(-.35f, -.2f, .25f), r[4]));
            Float3 rightHandStand = Add(p[4], Rotate(new Float3(.2f, -.15f, .35f), r[4]));
            Float3 leftHandLie = Add(p[4], Add(Scale(forward, .1f), Add(Scale(rightAxis, -.35f), new Float3(0, groundY + .1f - p[4].Y, 0))));
            Float3 rightHandLie = Add(p[4], Add(Scale(forward, -.05f), Add(Scale(rightAxis, .45f), new Float3(0, groundY + .12f - p[4].Y, 0))));
            leftHandLie = new Float3(leftHandLie.X, Math.Max(groundY + .05f, leftHandLie.Y), leftHandLie.Z);
            rightHandLie = new Float3(rightHandLie.X, Math.Max(groundY + .05f, rightHandLie.Y), rightHandLie.Z);
            pose.LeftHandTarget = Lerp(leftHandStand, leftHandLie, t);
            pose.RightHandTarget = Lerp(rightHandStand, rightHandLie, t);
            pose.WeaponRotation = Multiply(yaw, AxisAngle(new Float3(1, 0, 0), -70f * Radians * t));
            pose.WeaponPosition = Add(p[4], Rotate(new Float3(.15f, -.1f, .2f), r[4]));
            if(upgraded)AlignHeadToSight(input);
            armBody.Update(pose);
            var leftArm = Arm(8, 9, 10, pose.LeftHandTarget, r[7], -1);
            var rightArm = Arm(12, 13, 14, pose.RightHandTarget, r[11], 1);
            pose.LeftGripError = leftArm.TargetError; pose.RightGripError = rightArm.TargetError;
            pose.LeftElbowFlexion = leftArm.FlexionRadians; pose.RightElbowFlexion = rightArm.FlexionRadians;
            pose.LeftWristOrientationErrorRadians = 0; pose.RightWristOrientationErrorRadians = 0;
            pose.RightStockErrorMeters = 0; pose.EyeSightLineErrorMeters = 0;
            pose.RightEyePosition = Add(p[6], Rotate(new Float3(.035f, -.005f, .103f), r[6]));
            pose.ArmBodyClearanceMeters = Math.Min(armBody.Arm(p[8], p[9], p[10]), armBody.Arm(p[12], p[13], p[14]));
            ClampJointAboveGround(groundY);
            return pose;
        }

        private void ClampJointAboveGround(float groundY)
        {
            float minY = groundY + .02f;
            var p = pose.Positions;
            for (int i = 1; i < SoldierSkeleton.JointCount; i++)
            {
                if (p[i].Y < minY)
                    p[i] = new Float3(p[i].X, minY, p[i].Z);
            }
        }

        // Exact-key reuse of a rigidly translated upper-body solve. Stance and gait only translate
        // this fixed upper anatomy; any yaw/aim/action change recomputes the constraints immediately.
        private bool TryRestoreUpper(SoldierMotionInput input)
        {
            if(!upperCacheValid||input.YawRadians!=cachedYaw||input.AimYawRadians!=cachedAimYaw||
                input.AimPitchRadians!=cachedPitch||input.ReloadPhase!=cachedReload)return false;
            var delta=Sub(pose.Positions[4],cachedChest);
            for(int i=0;i<10;i++){pose.Positions[i+5]=Add(cachedUpperPositions[i],delta);pose.Rotations[i+5]=cachedUpperRotations[i];}
            CopyUpperMetrics(cachedUpper,pose);
            pose.WeaponPosition=Add(cachedUpper.WeaponPosition,delta);pose.RightEyePosition=Add(cachedUpper.RightEyePosition,delta);
            pose.LeftHandTarget=Add(cachedUpper.LeftHandTarget,delta);pose.RightHandTarget=Add(cachedUpper.RightHandTarget,delta);
            pose.UpperBodyCacheHit=true;return true;
        }
        private void StoreUpper(SoldierMotionInput input)
        {
            cachedYaw=input.YawRadians;cachedAimYaw=input.AimYawRadians;cachedPitch=input.AimPitchRadians;cachedReload=input.ReloadPhase;cachedChest=pose.Positions[4];
            for(int i=0;i<10;i++){cachedUpperPositions[i]=pose.Positions[i+5];cachedUpperRotations[i]=pose.Rotations[i+5];}
            CopyUpperMetrics(pose,cachedUpper);upperCacheValid=true;
        }
        private static void CopyUpperMetrics(SoldierPose a,SoldierPose b)
        {
            b.WeaponPosition=a.WeaponPosition;b.WeaponRotation=a.WeaponRotation;b.RightEyePosition=a.RightEyePosition;
            b.LeftHandTarget=a.LeftHandTarget;b.RightHandTarget=a.RightHandTarget;b.LeftGripError=a.LeftGripError;b.RightGripError=a.RightGripError;
            b.LeftElbowFlexion=a.LeftElbowFlexion;b.RightElbowFlexion=a.RightElbowFlexion;
            b.LeftWristOrientationErrorRadians=a.LeftWristOrientationErrorRadians;b.RightWristOrientationErrorRadians=a.RightWristOrientationErrorRadians;
            b.RightStockErrorMeters=a.RightStockErrorMeters;b.EyeSightLineErrorMeters=a.EyeSightLineErrorMeters;b.ArmBodyClearanceMeters=a.ArmBodyClearanceMeters;
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
        private float SupportProtraction(Float3 target)
        {
            var local=Rotate(Sub(target,pose.Positions[7]),Inverse(pose.Rotations[4]));
            float DistanceAt(float angle)
            {
                float x=local.X+.13f*(float)Math.Cos(angle),z=local.Z-.13f*(float)Math.Sin(angle);
                return x*x+local.Y*local.Y+z*z;
            }
            const float comfortable=.51f*.51f;
            if(DistanceAt(0)<=comfortable)return 0;
            float best=0,bestDistance=DistanceAt(0);
            for(int i=1;i<=8;i++)
            {
                float angle=i*45*Radians/8,distance=DistanceAt(angle);
                if(distance<bestDistance){best=angle;bestDistance=distance;}
                if(distance<=comfortable)
                {
                    float low=(i-1)*45*Radians/8,high=angle;
                    for(int j=0;j<6;j++){float mid=(low+high)*.5f;if(DistanceAt(mid)>comfortable)low=mid;else high=mid;}
                    return high;
                }
            }
            return best;
        }
        private TwoBoneResult Arm(int upper, int forearm, int hand, Float3 target, Quat taskFrame, float side)
        {
            var p=pose.Positions;var r=pose.Rotations;
            // The fixed palm primitive contains its semantic wrist origin; that point is the
            // numeric socket contact. Bone orientation is derived separately from its mirrored bind-X frame.
            var desiredHand=Multiply(pose.WeaponRotation,side>0?AxisAngle(new Float3(0,0,1),90*Radians):AxisAngle(Up,90*Radians));
            var result=ClearArm(p[upper],target,taskFrame,side,armBody);
            p[forearm]=result.Joint;p[hand]=result.End;
            r[upper]=SegmentRotation(upper,forearm,pose.Rotations[4]);r[forearm]=SegmentRotation(forearm,hand,pose.Rotations[4]);
            r[hand]=LimitSwingTwist(desiredHand,r[forearm],new Float3(side,0,0),45*Radians,30*Radians);
            return result;
        }
        private TwoBoneResult ClearArm(Float3 origin,Float3 target,Quat taskFrame,float side,ArmBodyClearance.Context context)
        {
            var down=Rotate(new Float3(0,-1,0),taskFrame);
            var preferred=Rotate(new Float3(side,-.65f,.35f),pose.Rotations[4]);
            var best=TwoBoneIk.Solve(origin,target,preferred,.29f,.26f,145*Radians,.98f,down,90*Radians);
            float clearance=context.Arm(origin,best.Joint,best.End);
            if(clearance>=.003f)return best;
            float bestScore=Math.Min(.003f,clearance)-100*best.TargetError,bestAngle=0;
            var delta=Sub(target,origin);
            if(delta.LengthSquared<1e-10)return best;
            var direction=Normalize(delta);
            var basis=Sub(preferred,Scale(direction,Dot(preferred,direction)));
            if(basis.LengthSquared<1e-10)basis=Cross(direction,new Float3(1,0,0));
            if(basis.LengthSquared<1e-10)basis=Cross(direction,new Float3(0,0,1));
            basis=Normalize(basis);var other=Cross(direction,basis);
            for(int i=1;i<24;i++)
            {
                float angle=i*2*(float)Math.PI/24;
                var pole=Add(Scale(basis,(float)Math.Cos(angle)),Scale(other,(float)Math.Sin(angle)));
                var candidate=TwoBoneIk.Solve(origin,target,pole,.29f,.26f,145*Radians,.98f,down,90*Radians);
                float candidateClearance=context.Arm(origin,candidate.Joint,candidate.End);
                float score=Math.Min(.003f,candidateClearance)-100*candidate.TargetError-.00001f*(1-(float)Math.Cos(angle));
                if(score>bestScore){best=candidate;bestScore=score;bestAngle=angle;}
            }
            float step=2*(float)Math.PI/24;
            for(int refine=0;refine<5;refine++)
            {
                step*=.5f;float center=bestAngle;
                for(int sideIndex=-1;sideIndex<=1;sideIndex+=2)
                {
                    float angle=center+sideIndex*step;
                    var pole=Add(Scale(basis,(float)Math.Cos(angle)),Scale(other,(float)Math.Sin(angle)));
                    var candidate=TwoBoneIk.Solve(origin,target,pole,.29f,.26f,145*Radians,.98f,down,90*Radians);
                    float score=Math.Min(.003f,context.Arm(origin,candidate.Joint,candidate.End))-100*candidate.TargetError-.00001f*(1-(float)Math.Cos(angle));
                    if(score>bestScore){best=candidate;bestScore=score;bestAngle=angle;}
                }
            }
            return best;
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
            bool moving, bool reset, Float3 root, Quat yaw, Quat bodyYaw, ISoldierGround ground)
        {
            bool stance = !moving || phase < duty;
            if (reset || moving != foot.Moving)
            {
                foot.Stance = stance; foot.Moving = moving; foot.NewPlant = true;
                if (stance)
                {
                    float ahead = moving ? (duty * .5f - (float)phase) * cycle : 0;
                    foot.Anchor = Sample(FootPoint(root,yaw,bodyYaw,lateral,.02f+ahead), ground);
                    foot.Start = foot.Anchor; foot.End = foot.Anchor;
                }
                else
                {
                    foot.Start = FootPoint(root,yaw,bodyYaw,lateral,.02f-duty*cycle*.5f);
                    foot.End = Sample(FootPoint(root,yaw,bodyYaw,lateral,.02f+(1-(float)phase+duty*.5f)*cycle), ground);
                }
            }
            else if (stance && !foot.Stance)
            { foot.Anchor = foot.End; foot.NewPlant = true; }
            else if (!stance && foot.Stance)
            {
                foot.Start = foot.Anchor;
                foot.End = Sample(FootPoint(root,yaw,bodyYaw,lateral,.02f+(1-(float)phase+duty*.5f)*cycle), ground);
            }
            foot.Stance = stance; foot.Moving = moving;
            if (stance) return foot.Anchor;
            float t = (float)((phase - duty) / (1 - duty));
            var target = Lerp(foot.Start, foot.End, Smooth(t));
            return Add(target, new Float3(0, clearance * (float)Math.Sin(Math.PI * t), 0));
        }
        private static Float3 FootPoint(Float3 root,Quat heading,Quat body,float lateral,float ahead)
            =>Add(root,Add(Rotate(new Float3(lateral,0,0),body),Rotate(new Float3(0,.08f,ahead),heading)));
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
        private static Float3 ReloadTask(float phase,Float3 support,Float3 magazine,Float3 belt,Quat body,Quat weapon)
        {
            var target=ReloadTarget(phase,support,magazine,belt);
            var withdrawn=Add(magazine,Rotate(new Float3(0,-.18f,0),weapon));
            if(phase>=.2f&&phase<.45f)target=Lerp(magazine,withdrawn,Smooth((phase-.2f)/.25f));
            else if(phase>=.45f&&phase<.7f)target=Lerp(withdrawn,belt,Smooth((phase-.45f)/.25f));
            if(phase>=.45f&&phase<.9f)
            {
                float t=phase<.7f?(phase-.45f)/.25f:(phase-.7f)/.2f;
                float arc=16*t*t*(1-t)*(1-t);
                target=Add(target,Rotate(new Float3(-.14f*arc,0,.06f*arc),body));
            }
            return target;
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
