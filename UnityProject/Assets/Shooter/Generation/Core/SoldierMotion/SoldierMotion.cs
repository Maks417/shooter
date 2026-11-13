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
        public const string Version = "soldier.motion.spike.v3";
        public const float MaxAimYawRadians = (float)Math.PI / 4;
        public const float MaxAimPitchRadians = 25 * (float)Math.PI / 180;
        private const float Radians = (float)Math.PI / 180;
        public const float BodyBladeRadians = 35 * Radians;
        public const float LegHeadingOffsetRadians = 5 * Radians;
        public static readonly Float3 PouchContactChestLocal = new Float3(-.24f,-.28f,.20f);
        private readonly SoldierPose pose = new SoldierPose();
        private readonly ArmBodyClearance.Context armBody = new ArmBodyClearance.Context();
        private readonly FootState left = new FootState(), right = new FootState();
        private readonly Float3 grip, support, magazine, stock, sight;
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

        public SoldierMotion(WeaponRecipe rifle)
        {
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

        public SoldierPose Evaluate(SoldierMotionInput input, ISoldierGround ground)
        {
            try { return EvaluateCore(input, ground); }
            catch { initialized = false; throw; } // A failed ground dependency cannot leave half-initialized contacts reusable.
        }
        private SoldierPose EvaluateCore(SoldierMotionInput input, ISoldierGround ground)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            bool run = input.SpeedMetersPerSecond > 3.5f;
            bool moving = input.SpeedMetersPerSecond > .001f;
            double travel = input.DistanceMeters - previousDistance;
            bool reset = !initialized || input.Reset || run != running || moving != wasMoving || travel < 0 || travel > .5 || Sub(input.Root, previousRoot).LengthSquared > .25;
            initialized = true; running = run; wasMoving = moving; previousDistance = input.DistanceMeters; previousRoot = input.Root;
            pose.ContactsReset = reset; pose.GroundQueries = 0;
            Quat yaw = AxisAngle(Up, input.YawRadians);
            // Pelvis stance is cosmetic; root position/rotation and requested weapon aim remain the input.
            Quat bodyYaw = Multiply(yaw, AxisAngle(Up, BodyBladeRadians));
            Quat legYaw = Multiply(yaw, AxisAngle(Up, LegHeadingOffsetRadians));
            float cycle = run ? 2.2f : 1.4f, duty = run ? .35f : .50f, clearance = run ? .12f : .08f;
            double phase = input.DistanceMeters / cycle; phase -= Math.Floor(phase);
            var leftTarget = FootTarget(left, -.1f, phase, cycle, duty, clearance, moving, reset, input.Root, yaw, bodyYaw, ground);
            double rightPhase = phase + .5; rightPhase -= Math.Floor(rightPhase);
            var rightTarget = FootTarget(right, .1f, rightPhase, cycle, duty, clearance, moving, reset, input.Root, yaw, bodyYaw, ground);
            // Version2 uses a bent-knee locomotion posture. Full crouch remains the original.66m;
            // locomotion lowering is interpolated, never stacked onto the crouch displacement.
            float nominalHeight = moving ? (run ? .76f : .78f) : .96f;
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
            var belt = Add(p[4], Rotate(PouchContactChestLocal, r[4]));
            pose.LeftHandTarget = input.ReloadPhase < 0 ? heldSupport : ReloadTask(input.ReloadPhase, heldSupport, mag, belt,r[4],pose.WeaponRotation);
            // Clavicle protraction follows the equipment target's reach requirement, not a joint-keyframe phase.
            float protraction=SupportProtraction(pose.LeftHandTarget);
            SetChild(7,Multiply(r[4],AxisAngle(Up,protraction)));SetChild(8,r[4]);
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
