#nullable enable
using System;
using Shooter.Contracts;
using static Shooter.Generation.GeometryMath;
using static Shooter.Generation.MotionMath;

namespace Shooter.Generation
{
    /// <summary>Conservative code-derived torso/plate/pack/head clearance proxy for the fixed M1 anatomy.
    /// It is a cosmetic IK constraint, never a gameplay collision or hit volume.</summary>
    public static class ArmBodyClearance
    {
        public const float ShoulderAttachmentBoundMeters = .12f;
        public static float Minimum(SoldierPose pose)
        {
            if(pose==null)throw new ArgumentNullException(nameof(pose));
            var context=new Context(pose);
            return Math.Min(context.Arm(pose.WorldPositions[8],pose.WorldPositions[9],pose.WorldPositions[10]),
                context.Arm(pose.WorldPositions[12],pose.WorldPositions[13],pose.WorldPositions[14]));
        }
        public static float Minimum(SoldierPose pose,bool left)
        {
            if(pose==null)throw new ArgumentNullException(nameof(pose));var context=new Context(pose);int i=left?8:12;
            return context.Arm(pose.WorldPositions[i],pose.WorldPositions[i+1],pose.WorldPositions[i+2]);
        }
        public static float PointDistance(SoldierPose pose,Float3 point)=>new Context(pose).Point(point);
        internal sealed class Context
        {
            private Float3 chest,pelvis,head;
            private readonly Float3[] centers=new Float3[9],axesX=new Float3[9],axesZ=new Float3[9];
            private readonly float[] widths=new float[9],depths=new float[9];
            private Quat chestInverse,pelvisInverse,headInverse;
            public Context(){}
            public Context(SoldierPose pose){Update(pose);}
            public void Update(SoldierPose pose)
            {
                chest=pose.WorldPositions[4];pelvis=pose.WorldPositions[1];head=pose.WorldPositions[6];
                chestInverse=Inverse(pose.WorldRotations[4]);pelvisInverse=Inverse(pose.WorldRotations[1]);headInverse=Inverse(pose.WorldRotations[6]);
                for(int i=0;i<9;i++)
                {
                    float y=.88f+.60f*i/8,t=i/8f;int a,b;float blend;
                    if(y<=.96f){a=b=1;blend=0;}
                    else if(y<1.1f){a=1;b=2;blend=(y-.96f)/.14f;}
                    else if(y<1.24f){a=2;b=3;blend=(y-1.1f)/.14f;}
                    else{a=3;b=4;blend=Math.Min(1,(y-1.24f)/.14f);}
                    var bind=new Float3(0,y,0);
                    var ca=Add(pose.WorldPositions[a],Rotate(Sub(bind,SoldierSkeleton.BindPositions[a]),pose.WorldRotations[a]));
                    var cb=Add(pose.WorldPositions[b],Rotate(Sub(bind,SoldierSkeleton.BindPositions[b]),pose.WorldRotations[b]));
                    centers[i]=Lerp(ca,cb,blend);
                    axesX[i]=Lerp(Rotate(new Float3(1,0,0),pose.WorldRotations[a]),Rotate(new Float3(1,0,0),pose.WorldRotations[b]),blend);
                    axesZ[i]=Lerp(Rotate(new Float3(0,0,1),pose.WorldRotations[a]),Rotate(new Float3(0,0,1),pose.WorldRotations[b]),blend);
                    widths[i]=t<.35f?.156f:t<.8f?.156f+.060f*(t-.35f)/.45f:.216f-.096f*(t-.8f)/.2f;
                    depths[i]=t<.8f?.106f+.020f*t/.8f:.126f-.036f*(t-.8f)/.2f;
                }
            }
            public float Arm(Float3 shoulder,Float3 elbow,Float3 hand)
            {
                float result=float.MaxValue;
                // The shoulder attachment uses blended surfaces, not a rigid tube.
                // Start the tube proxy beyond that blend plus its radial footprint (39.5mm blend reach plus at most80mm radial footprint, rounded up to120mm);
                // actual skinned attachment surfaces require the separate engine/visual review.
                // Interior sleeve and complete forearm must clear the body; no limb is removed/masked.
                for(int i=0;i<=8;i++)
                {
                    float first=ShoulderAttachmentBoundMeters/.29f;float t=first+(1-first)*i/8;
                    result=Math.Min(result,Point(Lerp(shoulder,elbow,t))-(.080f-.026f*t));
                }
                for(int i=0;i<=8;i++)
                {float t=i/8f;result=Math.Min(result,Point(Lerp(elbow,hand,t))-(.054f-.015f*t));}
                return result;
            }
            public float Point(Float3 point)=>Distances(point,out _,out _,out _,out _,out _,out _);
            private float Distances(Float3 point,out float torso,out float plate,out float straps,out float pack,out float hips,out float helmet)
            {
                var local=Rotate(Sub(point,chest),chestInverse);
                torso=Torso(point)-.006f;
                plate=Box(Sub(local,new Float3(0,-.125f,.153f)),new Float3(.155f,.15f,.0225f));
                straps=Math.Min(Box(Sub(local,new Float3(-.125f,-.125f,.181f)),new Float3(.013f,.15f,.0125f)),
                    Box(Sub(local,new Float3(.125f,-.125f,.181f)),new Float3(.013f,.15f,.0125f)));
                pack=Box(Sub(local,new Float3(0,-.135f,-.198f)),new Float3(.14f,.17f,.065f));
                var hip=Sub(Rotate(Sub(point,pelvis),pelvisInverse),new Float3(0,-.05f,0));
                hips=Ellipsoid(hip,new Float3(.176f,.132f,.126f));
                var skull=Rotate(Sub(point,head),headInverse);
                helmet=Ellipsoid(skull,new Float3(.106f,.145f,.116f));
                return Math.Min(Math.Min(Math.Min(torso,plate),Math.Min(straps,pack)),Math.Min(hips,helmet));
            }
            private float Torso(Float3 point)
            {
                int best=0;float bestDistance=float.MaxValue,bestT=0;
                for(int i=0;i<8;i++)
                {
                    var segment=Sub(centers[i+1],centers[i]);float t=Clamp(Dot(Sub(point,centers[i]),segment)/(float)segment.LengthSquared,0,1);
                    var delta=Sub(point,Lerp(centers[i],centers[i+1],t));float d=(float)delta.LengthSquared;
                    if(d<bestDistance){best=i;bestDistance=d;bestT=t;}
                }
                var center=Lerp(centers[best],centers[best+1],bestT);
                var xAxis=Lerp(axesX[best],axesX[best+1],bestT);var zAxis=Lerp(axesZ[best],axesZ[best+1],bestT);
                var q=Sub(point,center);float xx=Dot(xAxis,xAxis),zz=Dot(zAxis,zAxis),xz=Dot(xAxis,zAxis),det=xx*zz-xz*xz;
                float dx=Dot(q,xAxis),dz=Dot(q,zAxis),x=(dx*zz-dz*xz)/det,z=(dz*xx-dx*xz)/det;
                float width=widths[best]+(widths[best+1]-widths[best])*bestT,depth=depths[best]+(depths[best+1]-depths[best])*bestT;
                float radial=((float)Math.Sqrt(x*x/(width*width)+z*z/(depth*depth))-1)*Math.Min(width,depth);
                float axial=-1;
                if(best==0&&bestT==0)axial=-Dot(q,Normalize(Cross(zAxis,xAxis)));
                else if(best==7&&bestT==1)axial=Dot(q,Normalize(Cross(zAxis,xAxis)));
                return Combine(radial,axial);
            }
            private static float Ellipsoid(Float3 p,Float3 r)
                =>((float)Math.Sqrt(p.X*p.X/(r.X*r.X)+p.Y*p.Y/(r.Y*r.Y)+p.Z*p.Z/(r.Z*r.Z))-1)*Math.Min(r.X,Math.Min(r.Y,r.Z));
            private static float Box(Float3 p,Float3 half)
            {
                float x=Math.Abs(p.X)-half.X,y=Math.Abs(p.Y)-half.Y,z=Math.Abs(p.Z)-half.Z;
                return (float)Math.Sqrt(Math.Max(x,0)*Math.Max(x,0)+Math.Max(y,0)*Math.Max(y,0)+Math.Max(z,0)*Math.Max(z,0))+Math.Min(Math.Max(x,Math.Max(y,z)),0);
            }
            private static float Combine(float a,float b)
                =>(float)Math.Sqrt(Math.Max(a,0)*Math.Max(a,0)+Math.Max(b,0)*Math.Max(b,0))+Math.Min(Math.Max(a,b),0);
        }
    }
}
