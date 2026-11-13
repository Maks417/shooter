using System;
using System.IO;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class SoldierSkinPoseTests
{
    private sealed class Ground : ISoldierGround
    { public bool TrySample(float x,float z,out float height){height=0;return true;} }
    [Fact]
    public void ThousandPosesDeformEveryLodWithFiniteNoncollapsedTrianglesInsideConservativeBounds()
    {
        var soldier=SoldierGeometry.Build(SoldierRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"FeatureRecipes","soldier.json"))));
        var motion=new SoldierMotion(WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"FeatureRecipes","rifle.json"))));var ground=new Ground();
        for(int sample=0;sample<1000;sample++)
        {
            float distance=sample*3.5f/60;
            var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,distance),0,distance,3.5f,(sample%101)/100f,
                (sample%91-45)*(float)Math.PI/180,(sample%51-25)*(float)Math.PI/180,sample%3==0?(sample%101)/100f:-1),ground);
            foreach(var mesh in soldier.Lods)
            {
                var p=new Float3[mesh.Positions.Count];
                for(int v=0;v<p.Length;v++)
                {
                    var j=mesh.Joints![v];var w=mesh.Weights![v];
                    p[v]=Add(Add(Skin(mesh.Positions[v],j.X,w.X,pose),Skin(mesh.Positions[v],j.Y,w.Y,pose)),
                        Add(Skin(mesh.Positions[v],j.Z,w.Z,pose),Skin(mesh.Positions[v],j.W,w.W,pose)));
                    Assert.InRange(p[v].X,-2,2);Assert.InRange(p[v].Y,-1,3);Assert.InRange(p[v].Z-distance,-2,2);
                }
                for(int i=0;i<mesh.Indices.Count;i+=3)
                {
                    var a=Sub(p[mesh.Indices[i+1]],p[mesh.Indices[i]]);var b=Sub(p[mesh.Indices[i+2]],p[mesh.Indices[i]]);
                    double x=(double)a.Y*b.Z-(double)a.Z*b.Y,y=(double)a.Z*b.X-(double)a.X*b.Z,z=(double)a.X*b.Y-(double)a.Y*b.X;
                    Assert.True(x*x+y*y+z*z>1e-20,"Skinned triangle collapsed at pose "+sample);
                }
            }
        }
    }
    private static Float3 Skin(Float3 p,int j,float weight,SoldierPose pose)
    {
        if(weight==0)return new Float3(0,0,0);
        p=Sub(p,SoldierSkeleton.BindPositions[j]);var q=pose.WorldRotations[j];
        float x=2*(q.Y*p.Z-q.Z*p.Y),y=2*(q.Z*p.X-q.X*p.Z),z=2*(q.X*p.Y-q.Y*p.X);
        var origin=pose.WorldPositions[j];return new Float3((origin.X+p.X+q.W*x+q.Y*z-q.Z*y)*weight,
            (origin.Y+p.Y+q.W*y+q.Z*x-q.X*z)*weight,(origin.Z+p.Z+q.W*z+q.X*y-q.Y*x)*weight);
    }
    private static Float3 Add(Float3 a,Float3 b)=>new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    private static Float3 Sub(Float3 a,Float3 b)=>new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
}
