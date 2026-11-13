#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Generation-local C1 spike baseline. IDs/offsets are the documented 1800 mm T bind, not C0 changes.</summary>
    public static class SoldierSkeleton
    {
        public const string Version = "soldier.semantic23.v1";
        public const int JointCount = 23;
        public static FrozenList<string> Names { get; } = new FrozenList<string>(new[] {
            "root", "pelvis", "spine0", "spine1", "chest", "neck", "head",
            "leftClavicle", "leftUpperArm", "leftForearm", "leftHand",
            "rightClavicle", "rightUpperArm", "rightForearm", "rightHand",
            "leftThigh", "leftCalf", "leftFoot", "leftToe", "rightThigh", "rightCalf", "rightFoot", "rightToe" }, JointCount);
        public static SkeletonData Definition { get; } = Create();
        public static FrozenList<Float3> BindPositions { get; } = Positions();
        public static FrozenList<SoldierSegmentFrame> Frames { get; } = SegmentFrames();

        private static SkeletonData Create()
        {
            ushort[] parents = { ushort.MaxValue, 0, 1, 2, 3, 4, 5, 4, 7, 8, 9, 4, 11, 12, 13, 1, 15, 16, 17, 1, 19, 20, 21 };
            int[,] mm = { {0,0,0}, {0,960,0}, {0,140,0}, {0,140,0}, {0,140,0}, {0,160,0}, {0,120,0},
                {-80,70,0}, {-130,0,0}, {-290,0,0}, {-260,0,0}, {80,70,0}, {130,0,0}, {290,0,0}, {260,0,0},
                {-100,-40,0}, {0,-450,0}, {0,-390,20}, {0,-50,170}, {100,-40,0}, {0,-450,0}, {0,-390,20}, {0,-50,170} };
            var joints = new JointDefinition[JointCount];
            for (ushort i = 0; i < JointCount; i++)
            {
                float swing = i == 8 || i == 12 ? 90 : i == 15 || i == 19 ? 75 : 45;
                var min = Radians(-swing, -45, -swing); var max = Radians(swing, 45, swing);
                // Euler envelopes are descriptive; anatomical cone/hinge constraints use Frames below.
                if (i == 9 || i == 13) { min = Radians(0, -45, 0); max = Radians(145, 45, 0); }
                if (i == 16 || i == 20) { min = Radians(0, -30, 0); max = Radians(150, 30, 0); }
                if (i == 10 || i == 14) { min = Radians(-45, -30, 0); max = Radians(45, 30, 0); }
                if (i == 17 || i == 21) { min = Radians(-35, 0, -20); max = Radians(25, 0, 20); }
                if (i == 2 || i == 3 || i == 4) { min = Radians(-25f/3, -15, 0); max = Radians(25f/3, 15, 0); }
                if (i == 0) { min = new Float3(0,0,0); max = min; }
                joints[i] = new JointDefinition(i, parents[i], new Float3(mm[i,0]/1000f, mm[i,1]/1000f, mm[i,2]/1000f), Quat.Identity, min, max);
            }
            return new SkeletonData(joints);
        }
        private static Float3 Radians(float x, float y, float z) => new Float3(x*(float)Math.PI/180, y*(float)Math.PI/180, z*(float)Math.PI/180);
        private static FrozenList<Float3> Positions()
        {
            var result = new Float3[JointCount];
            foreach (var joint in Definition.Joints)
                result[joint.Id] = joint.ParentId == ushort.MaxValue ? joint.LocalBindPosition : GeometryMath.Add(result[joint.ParentId], joint.LocalBindPosition);
            return new FrozenList<Float3>(result, JointCount);
        }
        private static FrozenList<SoldierSegmentFrame> SegmentFrames()
        {
            // Primary child per semantic segment; leaf frames inherit their incoming segment direction.
            int[] child = { 1,2,3,4,5,6,-1,8,9,10,-1,12,13,14,-1,16,17,18,-1,20,21,22,-1 };
            var frames = new SoldierSegmentFrame[JointCount];
            for (int i = 0; i < JointCount; i++)
            {
                int parent = Definition.Joints[i].ParentId;
                Float3 primary = child[i] >= 0 ? GeometryMath.Sub(BindPositions[child[i]], BindPositions[i]) : GeometryMath.Sub(BindPositions[i], BindPositions[parent]);
                primary = GeometryMath.Normalize(primary);
                Float3 secondary = new Float3(0,0,1);
                secondary = GeometryMath.Sub(secondary, GeometryMath.Scale(primary, GeometryMath.Dot(primary, secondary)));
                if (secondary.LengthSquared < 1e-12) secondary = new Float3(1,0,0);
                secondary = GeometryMath.Normalize(secondary);
                Float3 x = GeometryMath.Normalize(new Float3(primary.Y*secondary.Z-primary.Z*secondary.Y, primary.Z*secondary.X-primary.X*secondary.Z, primary.X*secondary.Y-primary.Y*secondary.X));
                var pole = new Float3(0,0, i >= 15 ? 1 : -1);
                frames[i] = new SoldierSegmentFrame(x, primary, secondary, pole);
            }
            return new FrozenList<SoldierSegmentFrame>(frames, JointCount);
        }
    }

    /// <summary>Right-handed segment basis; hinge bends about X, independent of identity local bind rotations.</summary>
    public readonly struct SoldierSegmentFrame
    {
        public Float3 X { get; }
        public Float3 Y { get; }
        public Float3 Z { get; }
        public Float3 Pole { get; }
        public SoldierSegmentFrame(Float3 x, Float3 y, Float3 z, Float3 pole) { X=x; Y=y; Z=z; Pole=pole; }
    }
}
