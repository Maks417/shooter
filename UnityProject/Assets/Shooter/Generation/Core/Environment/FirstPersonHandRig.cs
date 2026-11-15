#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Camera-only numeric hand skeleton; independent of soldier/hit anatomy.</summary>
    public sealed class FirstPersonHandRig
    {
        public const string Version = "fp.hand.v2";
        public const int JointCount = 17;
        public Float3 Palm { get; }
        public Float3 Wrist { get; }
        public Float3 Elbow { get; }
        public Float3[] BindPositions { get; }
        public SkeletonData Skeleton { get; }
        public FirstPersonHandRig(byte slot, bool support)
        {
            if (slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            float side = support ? -1f : 1f;
            float z = support && slot == 0 ? .34f : slot == 2 ? -.035f : -.015f;
            float y = support && slot == 0 ? .012f : slot == 3 ? .002f : -.026f;
            Palm = new Float3(side * .039f, y, z - .013f);
            Wrist = new Float3(side * .052f, y - .065f, z - .04f);
            Elbow = new Float3(side * (support ? .24f : .18f), -.24f, -.29f);
            BindPositions = new Float3[JointCount];
            BindPositions[1] = Elbow; BindPositions[2] = Wrist;
            for (int f = 0; f < 4; f++)
            {
                float fy = y + .029f - f * .019f;
                int j = 3 + f * 3;
                float radius = slot == 3 ? .012f : slot == 2 ? -.004f : 0;
                BindPositions[j] = new Float3(side * (.047f + radius), fy, z + .005f);
                BindPositions[j + 1] = new Float3(side * (.022f + radius), fy + .002f, z + .035f);
                BindPositions[j + 2] = new Float3(-side * .006f, fy + .004f, z + .020f);
                if (!support && f == 0 && slot < 2)
                {
                    BindPositions[j + 1] = new Float3(side * .037f, fy, z + .052f);
                    BindPositions[j + 2] = new Float3(side * .030f, fy, z + .082f);
                }
            }
            BindPositions[15] = new Float3(side * .046f, y + .034f, z - .035f);
            BindPositions[16] = new Float3(side * .016f, y + .058f, z - .020f);
            var joints = new JointDefinition[JointCount];
            for (ushort j = 0; j < JointCount; j++)
            {
                ushort parent = j == 0 ? ushort.MaxValue : j <= 2 ? (ushort)0 :
                    j == 15 ? (ushort)2 : j == 16 ? (ushort)15 : (j - 3) % 3 == 0 ? (ushort)2 : (ushort)(j - 1);
                var local = parent == ushort.MaxValue ? BindPositions[j] : Sub(BindPositions[j], BindPositions[parent]);
                joints[j] = new JointDefinition(j, parent, local, Quat.Identity, new Float3(-90,-90,-90), new Float3(90,90,90));
            }
            Skeleton = new SkeletonData(joints);
        }
        public Float3 FingerTip(int finger, bool support, byte slot)
        {
            int j = 3 + finger * 3; var c = BindPositions[j + 2];
            if (finger == 0 && !support && slot < 2) return new Float3(c.X - .004f, c.Y, c.Z + .018f);
            return new Float3(c.X + (support ? .010f : -.010f), c.Y, c.Z - .012f);
        }
        public static float OpenForReload(float phase)
        {
            if (!float.IsFinite(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (phase <= 0 || phase >= 1) return 0;
            float Ramp(float a, float b) { float t = Math.Clamp((phase-a)/(b-a),0,1); return t*t*(3-2*t); }
            return Ramp(.04f,.12f)*(1-Ramp(.16f,.22f)) + Ramp(.78f,.84f)*(1-Ramp(.92f,.98f));
        }
        public static Quat FingerRotation(int joint, bool support, float open, float trigger)
        {
            if (joint < 0 || joint >= JointCount || !float.IsFinite(open) || !float.IsFinite(trigger)) throw new ArgumentOutOfRangeException();
            if (joint < 3) return Quat.Identity;
            float degrees = Math.Clamp(open,0,1) * (joint >= 15 ? 35 : (joint-3)%3 == 0 ? 45 : (joint-3)%3 == 1 ? 55 : 25);
            if (!support && joint >= 3 && joint <= 5) degrees -= Math.Clamp(trigger,0,1) * 24;
            double radians = degrees * (support ? -1 : 1) * Math.PI / 360;
            return new Quat(0, (float)Math.Sin(radians), 0, (float)Math.Cos(radians));
        }
        private static Float3 Sub(Float3 a, Float3 b) => new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    }
}
