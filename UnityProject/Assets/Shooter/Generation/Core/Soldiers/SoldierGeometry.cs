#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class SoldierBuild
    {
        public SkeletonData Skeleton => SoldierSkeleton.Definition;
        public FrozenList<MeshData> Lods { get; }
        public Aabb Bounds { get; }
        internal SoldierBuild(MeshData[] lods, Aabb bounds) { Lods = new FrozenList<MeshData>(lods, 3); Bounds = bounds; }
    }

    /// <summary>Code-derived ring anatomy and rigid equipment. Closed components overlap at shoulders/hips; no authored vertex arrays.</summary>
    public static class SoldierGeometry
    {
        public static Aabb BindBounds { get; } = new Aabb(new Float3(-.85f,0,-.27f), new Float3(.85f,1.81f,.27f));
        public static SoldierBuild Build(SoldierRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            SoldierRecipe.Validate(recipe.Document);
            var lods = new MeshData[3];
            for (int lod=0; lod<3; lod++) lods[lod] = BuildLod(lod);
            return new SoldierBuild(lods, BindBounds);
        }
        private static MeshData BuildLod(int lod)
        {
            var b = new SkinBuilder(lod); int torso = new[]{16,12,8}[lod], radial = new[]{12,8,6}[lod];
            // Ring stations derive from semantic pelvis/waist/chest/neck measures; no stored surface vertices.
            b.Loft(new Float3(0,.88f,0), new Float3(0,1.48f,0), new Float3(1,0,0), torso, 8,
                t => { float width = t < .35f ? Lerp(.156f,.156f,t/.35f) : t < .8f ? Lerp(.156f,.216f,(t-.35f)/.45f) : Lerp(.216f,.12f,(t-.8f)/.2f); return new Float2(width, t<.8f ? Lerp(.106f,.126f,t/.8f) : Lerp(.126f,.09f,(t-.8f)/.2f)); },
                t => TorsoSkin(Lerp(.88f,1.48f,t)), 0);
            // A rounded pelvis shell bridges jacket, belt and trouser roots, including during thigh rotation.
            Ellipsoid(b,new Float3(0,.91f,0),new Float3(.176f,.132f,.126f),torso,8,1,0);
            b.Loft(new Float3(0,1.48f,0), new Float3(0,1.59f,0), new Float3(1,0,0), radial, 3,
                t => new Float2(.064f,.069f), t => new Skin(5,6,t*.3f), 0);
            Ellipsoid(b,new Float3(0,1.66f,0),new Float3(.09f,.12f,.10f),torso,new[]{10,8,6}[lod],6,2);
            // Covered anatomy is the bounded first-slice policy: a generated cloth hood under the helmet.
            Ellipsoid(b,new Float3(0,1.652f,0),new Float3(.096f,.123f,.108f),torso,8,6,0);
            // Lower rim protects side silhouette instead of leaving an exposed upper-head block.
            b.Loft(new Float3(0,1.665f,0),new Float3(0,1.805f,0),new Float3(1,0,0),torso,6,
                t => {float r=(float)Math.Sqrt(Math.Max(.0064,1-t*t)); return new Float2(.106f*r,.116f*r);},t=>new Skin(6),1);
            b.Box(new Float3(.21f,.014f,.065f),new Float3(0,1.681f,.096f),6,1,.005f); // numeric helmet brim
            b.Box(new Float3(.165f,.042f,.025f),new Float3(0,1.655f,.103f),6,3,.008f); // grouped goggles
            b.Box(new Float3(.11f,.075f,.035f),new Float3(0,1.60f,.098f),6,0,.012f); // covered jaw
            foreach (int side in new[]{-1,1})
            {
                ushort upper=(ushort)(side<0?8:12), lower=(ushort)(upper+1), hand=(ushort)(upper+2);
                // Sleeve root lies inside the torso; its first rings inherit the chest before transitioning to humerus.
                b.Loft(new Float3(side*.145f,1.45f,0),new Float3(side*.76f,1.45f,0),new Float3(0,1,0),radial,10,
                    t=>new Float2(ArmRadius(t),(ArmRadius(t))*.94f),
                    t=>t<.17f?new Skin(4,upper,Smooth(t/.17f)):LimbSkin(t,.355f/.615f,upper,lower,hand),0,-1,
                    new[]{0f,.065f/.615f,.17f,.30f,.45f,.355f/.615f,.67f,.78f,.88f,.95f,1f});
                b.Box(new Float3(.09f,.07f,.045f),new Float3(side*.797f,1.45f,.008f),hand,1,.008f); // covered palm/grouped fingers
                ushort thigh=(ushort)(side<0?15:19),calf=(ushort)(thigh+1),foot=(ushort)(thigh+2);
                b.Loft(new Float3(side*.10f,.98f,0),new Float3(side*.10f,.08f,.02f),new Float3(1,0,0),radial,10,
                    t=>new Float2(LegRadius(t),LegRadius(t)*.96f),
                    t=>t<.16f?new Skin(1,thigh,Smooth(t/.16f)):LimbSkin(t,.51f/.90f,thigh,calf,foot),0,-1,
                    new[]{0f,.06f/.90f,.16f,.29f,.43f,.51f/.90f,.68f,.78f,.88f,.95f,1f});
                // Rounded toe/heel and a tall cuff overlap trouser ends instead of a detached cuboid foot.
                b.Box(new Float3(.125f,.10f,.285f),new Float3(side*.10f,.05f,.10f),foot,1,.024f);
                b.Loft(new Float3(side*.10f,.045f,.015f),new Float3(side*.10f,.185f,.015f),new Float3(1,0,0),radial,4,
                    t=>new Float2(Lerp(.059f,.052f,t),Lerp(.075f,.063f,t)),t=>new Skin(foot),1);
            }
            b.Box(new Float3(.31f,.30f,.045f),new Float3(0,1.255f,.153f),4,1,.012f); // plate +12mm clearance
            b.Box(new Float3(.28f,.34f,.13f),new Float3(0,1.245f,-.198f),4,0,.018f); // pack
            b.Box(new Float3(.32f,.06f,.232f),new Float3(0,.96f,0),1,1); // belt
            if(lod<2) foreach(int side in new[]{-1,1})
                b.Box(new Float3(.026f,.30f,.025f),new Float3(side*.125f,1.255f,.181f),4,0); // straps
            MeshData mesh=b.Build(); GeometryCodec.EncodeMesh(mesh);
            foreach(var p in mesh.Positions)
                if(p.X<BindBounds.Min.X || p.X>BindBounds.Max.X || p.Y<BindBounds.Min.Y || p.Y>BindBounds.Max.Y || p.Z<BindBounds.Min.Z || p.Z>BindBounds.Max.Z)
                    throw new InvalidOperationException("Soldier escaped bind bounds.");
            return mesh;
        }
        private static float ArmRadius(float t)
        {
            const float elbow=.355f/.615f;
            float radius=t<elbow?Lerp(.080f,.054f,t/elbow):Lerp(.054f,.039f,(t-elbow)/(1-elbow));
            return radius+.006f*(float)Math.Sin(Math.PI*Math.Min(1,t/elbow));
        }
        private static float LegRadius(float t)
        {
            const float knee=.51f/.90f;
            float radius=t<knee?Lerp(.108f,.073f,t/knee):Lerp(.073f,.054f,(t-knee)/(1-knee));
            // Broad analytic cloth fullness, not image displacement or authored folds.
            return radius+.010f*(float)Math.Sin(Math.PI*Math.Min(1,t/knee));
        }
        private static float Smooth(float t) {t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        private static float Lerp(float a,float c,float t)=>a+(c-a)*t;
        private static Skin TorsoSkin(float height)
        {
            if(height<=.96f)return new Skin(1);
            if(height<1.1f)return new Skin(1,2,(height-.96f)/.14f);
            if(height<1.24f)return new Skin(2,3,(height-1.1f)/.14f);
            return new Skin(3,4,Math.Min(1,(height-1.24f)/.14f));
        }
        private static Skin LimbSkin(float t,float joint,ushort first,ushort second,ushort end)
        {
            const float blend=.16f;
            if(t<joint-blend)return new Skin(first);
            if(t<joint+blend)return new Skin(first,second,(t-joint+blend)/(2*blend));
            if(t<.88f)return new Skin(second);
            return new Skin(second,end,(t-.88f)/.12f*.4f);
        }
        private static void Ellipsoid(SkinBuilder b,Float3 center,Float3 radii,int radial,int rings,ushort joint,ushort slot)
        {
            // Tiny circular end caps avoid zero-area pole triangles at all LODs.
            b.Loft(new Float3(center.X,center.Y-radii.Y,center.Z),new Float3(center.X,center.Y+radii.Y,center.Z),new Float3(1,0,0),radial,rings,
                t=>{float y=2*t-1; float f=(float)Math.Sqrt(Math.Max(.0025,1-y*y));return new Float2(radii.X*f,radii.Z*f);},t=>new Skin(joint),slot);
        }
        private readonly struct Skin
        {
            internal readonly ushort A,B; internal readonly float Blend;
            internal Skin(ushort a) {A=a;B=a;Blend=0;}
            internal Skin(ushort a,ushort b,float blend) {A=a;B=b;Blend=blend;}
        }
        private sealed class SkinBuilder
        {
            private readonly int lod;
            private readonly List<Float3> positions=new List<Float3>(),normals=new List<Float3>();
            private readonly List<Float4> tangents=new List<Float4>(),weights=new List<Float4>();
            private readonly List<Float2> uv=new List<Float2>();
            private readonly List<UInt16x4> joints=new List<UInt16x4>();
            private readonly SortedDictionary<ushort,List<uint>> groups=new SortedDictionary<ushort,List<uint>>();
            internal SkinBuilder(int lod){this.lod=lod;}
            private ushort Remap(ushort id)
            {
                if(lod<2)return id;
                // nearest retained semantic parent: remove spine0,spine1,neck,clavicles,toes =>16 influencing joints.
                while(id==2 || id==3 || id==5 || id==7 || id==11 || id==18 || id==22)id=SoldierSkeleton.Definition.Joints[id].ParentId;
                return id;
            }
            private uint Vertex(Float3 p,Float3 n,Float3 tangent,Float2 tex,Skin skin)
            {
                uint index=(uint)positions.Count; positions.Add(p);normals.Add(n);tangents.Add(new Float4(tangent.X,tangent.Y,tangent.Z,1));uv.Add(tex);
                ushort a=Remap(skin.A),c=Remap(skin.B); float blend=Math.Max(0,Math.Min(1,skin.Blend));
                joints.Add(new UInt16x4(a,c,0,0)); weights.Add(new Float4(1-blend,blend,0,0)); return index;
            }
            private void Triangle(ushort slot,uint a,uint c,uint d)
            {if(!groups.TryGetValue(slot,out var g)){g=new List<uint>();groups.Add(slot,g);}g.Add(a);g.Add(c);g.Add(d);}
            internal void Box(Float3 size,Float3 center,ushort joint,ushort slot,float bevel=0)
            {
                MeshData box=PrimitiveGeometry.Box(size,bevel);uint offset=(uint)positions.Count;
                for(int i=0;i<box.Positions.Count;i++){var t=box.Tangents[i]; uint id=Vertex(GeometryMath.Add(center,box.Positions[i]),box.Normals[i],new Float3(t.X,t.Y,t.Z),box.Uv[i],new Skin(joint));tangents[(int)id]=t;}
                for(int i=0;i<box.Indices.Count;i+=3)Triangle(slot,offset+box.Indices[i],offset+box.Indices[i+1],offset+box.Indices[i+2]);
            }
            internal void Loft(Float3 start,Float3 end,Float3 u,int radial,int rings,Func<float,Float2> radius,Func<float,Skin> skin,ushort slot,float seam=-1,float[]? stations=null)
            {
                Float3 delta=GeometryMath.Sub(end,start);float length=(float)Math.Sqrt(delta.LengthSquared); Float3 axis=GeometryMath.Normalize(delta);
                u=GeometryMath.Normalize(GeometryMath.Sub(u,GeometryMath.Scale(axis,GeometryMath.Dot(u,axis))));
                Float3 v=new Float3(u.Y*axis.Z-u.Z*axis.Y,u.Z*axis.X-u.X*axis.Z,u.X*axis.Y-u.Y*axis.X);
                uint first=(uint)positions.Count;
                for(int ring=0;ring<=rings;ring++)
                {
                    float t=stations!=null?stations[ring]:seam<0?ring/(float)rings:ring<=rings/2?seam*ring/(rings/2):seam+(1-seam)*(ring-rings/2)/(rings-rings/2);var r=radius(t);float lo=Math.Max(0,t-.001f),hi=Math.Min(1,t+.001f);var ra=radius(lo);var rb=radius(hi);
                    float dx=(rb.X-ra.X)/((hi-lo)*length),dz=(rb.Y-ra.Y)/((hi-lo)*length);
                    var center=GeometryMath.Add(start,GeometryMath.Scale(delta,t));
                    for(int i=0;i<=radial;i++)
                    {
                        double angle=i==radial?0:2*Math.PI*i/radial;float cos=(float)Math.Cos(angle),sin=(float)Math.Sin(angle);
                        Float3 normal=GeometryMath.Add(GeometryMath.Scale(u,cos/r.X),GeometryMath.Scale(v,sin/r.Y));
                        normal=GeometryMath.Normalize(GeometryMath.Sub(normal,GeometryMath.Scale(axis,dx*cos*cos/r.X+dz*sin*sin/r.Y)));
                        Float3 tangent=GeometryMath.Add(GeometryMath.Scale(u,-r.X*sin),GeometryMath.Scale(v,r.Y*cos));
                        tangent=GeometryMath.Normalize(GeometryMath.Sub(tangent,GeometryMath.Scale(normal,GeometryMath.Dot(tangent,normal))));
                        Float3 p=GeometryMath.Add(center,GeometryMath.Add(GeometryMath.Scale(u,r.X*cos),GeometryMath.Scale(v,r.Y*sin)));
                        Vertex(p,normal,tangent,new Float2((float)(2*Math.PI*i/radial*Math.Sqrt((r.X*r.X+r.Y*r.Y)*.5)),t*length),skin(t));
                    }
                }
                for(int ring=0;ring<rings;ring++)for(int i=0;i<radial;i++)
                {uint a=first+(uint)(ring*(radial+1)+i),c=a+(uint)(radial+1);Triangle(slot,a,c,a+1);Triangle(slot,a+1,c,c+1);}
                for(int cap=0;cap<2;cap++)
                {
                    float t=cap; var center=cap==0?start:end; var r=radius(t);var n=GeometryMath.Scale(axis,cap==0?-1:1);
                    uint mid=Vertex(center,n,u,new Float2(0,0),skin(t));uint rim=(uint)positions.Count;
                    for(int i=0;i<=radial;i++)
                    {double a=i==radial?0:2*Math.PI*i/radial;float x=r.X*(float)Math.Cos(a),z=r.Y*(float)Math.Sin(a);Vertex(GeometryMath.Add(center,GeometryMath.Add(GeometryMath.Scale(u,x),GeometryMath.Scale(v,z))),n,u,new Float2(x,z),skin(t));}
                    for(int i=0;i<radial;i++)if(cap==0)Triangle(slot,mid,rim+(uint)i,rim+(uint)i+1);else Triangle(slot,mid,rim+(uint)i+1,rim+(uint)i);
                }
            }
            internal MeshData Build()
            {
                var indices=new List<uint>();var subs=new List<Submesh>();
                foreach(var group in groups){subs.Add(new Submesh((uint)indices.Count,(uint)group.Value.Count,group.Key));indices.AddRange(group.Value);}
                return new MeshData(positions,normals,tangents,uv,indices,subs,joints,weights);
            }
        }
    }
}
