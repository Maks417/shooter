using System;
using System.IO;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;
using Xunit.Abstractions;

public sealed class SoldierMotionTests
{
    private readonly ITestOutputHelper output;
    public SoldierMotionTests(ITestOutputHelper output) { this.output = output; }
    private static WeaponRecipe Rifle() => WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureRecipes", "rifle.json")));
    private static double Distance(Float3 a, Float3 b) => Math.Sqrt((double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y) + (double)(a.Z - b.Z) * (a.Z - b.Z));
    private static Float3 Sub(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static double Dot(Float3 a, Float3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
    private sealed class Ground : ISoldierGround
    {
        private readonly int mode; public int Queries;
        public Ground(int mode = 0) { this.mode = mode; }
        public float Height(float z) => mode == 1 ? z * (float)Math.Tan(Math.PI / 12) : mode == 2 && z >= 1 ? .2f : 0;
        public bool TrySample(float x, float z, out float height) { Queries++; height = Height(z); return true; }
    }
    private sealed class MissingGround : ISoldierGround
    { public bool TrySample(float x, float z, out float height) { height = 0; return false; } }

    [Fact]
    public void IkRetainsLengthsHingeLimitAndOriginalTargetResidualInDegenerateCases()
    {
        foreach (var target in new[] {new Float3(0,0,0),new Float3(0,-.5f,0),new Float3(0,-10,0),new Float3(1,.2f,-.3f)})
        {
            var result = TwoBoneIk.Solve(new Float3(0,0,0),target,new Float3(0,-1,0),.45f,.39f,150*(float)Math.PI/180);
            Assert.InRange(Distance(new Float3(0,0,0),result.Joint),.44999,.45001);
            Assert.InRange(Distance(result.Joint,result.End),.38999,.39001);
            Assert.InRange(result.FlexionRadians,0,150*(float)Math.PI/180+1e-5f);
            Assert.InRange(Math.Abs(result.TargetError-Distance(result.End,target)),0,1e-5);
            Assert.True(result.End.LengthSquared <= Math.Pow(.84*.98+1e-5,2));
        }
    }
    [Fact]
    public void IkSwingConeRetainsLengthsAndReportsImpossibleTargets()
    {
        var random = new Pcg32(460072030,42); int residuals = 0;
        for(int i=0;i<1000;i++)
        {
            var target = new Float3(random.NextUnitFloat()*2-1,random.NextUnitFloat()*2-1,random.NextUnitFloat()*2-1);
            var result = TwoBoneIk.Solve(new Float3(0,0,0),target,new Float3(0,0,-1),.29f,.26f,145*(float)Math.PI/180,.98f,new Float3(1,0,0),(float)Math.PI/2);
            Assert.InRange(result.Joint.X,-1e-5f,1);
            Assert.InRange(Distance(new Float3(0,0,0),result.Joint),.28999,.29001);
            Assert.InRange(Distance(result.Joint,result.End),.25999,.26001);
            Assert.InRange(result.FlexionRadians,0,145*(float)Math.PI/180+1e-5f);
            Assert.True(result.End.LengthSquared <= Math.Pow(.55*.98+1e-5,2));
            if(result.TargetError>.02f) residuals++;
        }
        Assert.True(residuals>0);
    }
    [Fact]
    public void HeldRifleTargetsAreActualSocketsAndStandingGripIsWithinTwoCentimeters()
    {
        var rifle = Rifle(); var motion = new SoldierMotion(rifle);
        var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),new Ground());
        foreach(var socket in WeaponGeometry.Sockets(rifle))
        {
            if(socket.Id=="right-hand-grip") Assert.InRange(Distance(pose.RightHandTarget,Transform(pose.WeaponPosition,pose.WeaponRotation,socket.Position)),0,1e-6);
            if(socket.Id=="left-hand-grip") Assert.InRange(Distance(pose.LeftHandTarget,Transform(pose.WeaponPosition,pose.WeaponRotation,socket.Position)),0,1e-6);
        }
        Assert.InRange(pose.RightGripError,0,.02f); Assert.InRange(pose.LeftGripError,0,.02f);
    }
    [Fact]
    public void HeldGripsRemainWithinTwoCentimetersAcrossTheSupportedAimCone()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();float worst=0;
        foreach(float yaw in new[]{-SoldierMotion.MaxAimYawRadians,0,SoldierMotion.MaxAimYawRadians})
        foreach(float pitch in new[]{-SoldierMotion.MaxAimPitchRadians,0,SoldierMotion.MaxAimPitchRadians})
        foreach(float crouch in new[]{0f,1f})
        {
            var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0,crouch,yaw,pitch),ground);
            worst=Math.Max(worst,Math.Max(pose.LeftGripError,pose.RightGripError));
            Assert.InRange(pose.LeftGripError,0,.02f);Assert.InRange(pose.RightGripError,0,.02f);
        }
        output.WriteLine("Supportedaimcone heldgrip worstpositionresidual="+worst+"m; wristweaponorientation residual remains explicit.");
    }
    [Fact]
    public void AnalyticReloadFollowsDocumentedEquipmentTaskBoundaries()
    {
        var support=new Float3(0,1,.4f);var magazine=new Float3(0,.9f,.1f);var belt=new Float3(-.15f,.8f,.1f);
        Assert.InRange(Distance(support,SoldierMotion.ReloadTarget(0,support,magazine,belt)),0,1e-6);
        Assert.InRange(Distance(magazine,SoldierMotion.ReloadTarget(.2f,support,magazine,belt)),0,1e-6);
        Assert.InRange(Distance(new Float3(0,.72f,.1f),SoldierMotion.ReloadTarget(.45f,support,magazine,belt)),0,1e-6);
        Assert.InRange(Distance(belt,SoldierMotion.ReloadTarget(.7f,support,magazine,belt)),0,1e-6);
        Assert.InRange(Distance(magazine,SoldierMotion.ReloadTarget(.9f,support,magazine,belt)),0,1e-6);
        Assert.InRange(Distance(support,SoldierMotion.ReloadTarget(1,support,magazine,belt)),0,1e-6);
    }
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)]
    public void ThousandPoseSweepPreservesFiniteFullSkeletonLengthsAndQueryBound(int terrain)
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground(terrain);var random=new Pcg32(460072030,42);
        float maxGrip=0,maxPlant=0,maxSlide=0; double travelled=0;
        for(int i=0;i<1000;i++)
        {
            float speed=i<500?3.5f:5.5f; travelled+=speed/60;
            float z=(float)travelled;var root=new Float3(0,ground.Height(z),z);
            var input=new SoldierMotionInput(root,0,travelled,speed,(i%240)/239f,
                (random.NextUnitFloat()*2-1)*SoldierMotion.MaxAimYawRadians,(random.NextUnitFloat()*2-1)*SoldierMotion.MaxAimPitchRadians,
                i%200<100?-1:(i%100)/99f);
            var pose=motion.Evaluate(input,ground);
            Assert.Equal(23,pose.WorldPositions.Count);Assert.Equal(23,pose.WorldRotations.Count);
            Assert.Equal(root.X,pose.WorldPositions[0].X);Assert.Equal(root.Y,pose.WorldPositions[0].Y);Assert.Equal(root.Z,pose.WorldPositions[0].Z);
            Assert.InRange(pose.PelvisCorrection,-.080001f,0);Assert.InRange(pose.GroundQueries,0,2);
            Assert.InRange(pose.LeftElbowFlexion,0,145*(float)Math.PI/180+1e-5f);Assert.InRange(pose.RightElbowFlexion,0,145*(float)Math.PI/180+1e-5f);
            Assert.InRange(pose.LeftKneeFlexion,0,150*(float)Math.PI/180+1e-5f);Assert.InRange(pose.RightKneeFlexion,0,150*(float)Math.PI/180+1e-5f);
            for(int j=0;j<23;j++)
            {
                Assert.True(pose.WorldRotations[j].IsValid);
                if(j<2)continue;
                int parent=SoldierSkeleton.Definition.Joints[j].ParentId;
                double expected=Math.Sqrt(SoldierSkeleton.Definition.Joints[j].LocalBindPosition.LengthSquared);
                Assert.InRange(Math.Abs(Distance(pose.WorldPositions[j],pose.WorldPositions[parent])-expected),0,2e-5);
            }
            maxGrip=Math.Max(maxGrip,Math.Max(pose.LeftGripError,pose.RightGripError));maxPlant=Math.Max(maxPlant,pose.PlantTargetError);maxSlide=Math.Max(maxSlide,pose.PlantSlide);
        }
        output.WriteLine("terrain="+terrain+" poses=1000 maximumTaskResidualMeters="+maxGrip+" plantTargetErrorMeters="+maxPlant+" plantSlideMeters="+maxSlide+"; numeric finite/length check only, not visual/hardware acceptance");
    }
    [Fact]
    public void StationaryPlantedFeetDoNotSlideAcrossCrouchAndAimChanges()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();float slide=0;
        for(int i=0;i<60;i++)
        {
            var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0,i/59f,0,.2f),ground);
            slide=Math.Max(slide,pose.PlantSlide);Assert.InRange(pose.PlantTargetError,0,1e-5f);
        }
        Assert.InRange(slide,0,.00001f);
    }
    [Fact]
    public void BaselineWalkReachConflictIsExposedWithoutMovingRootOrChangingBoneLengths()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();float maxError=0,maxSlide=0;
        for(int i=0;i<180;i++)
        {
            float z=i*3.5f/60;
            var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,z),0,z,3.5f),ground);
            maxError=Math.Max(maxError,pose.PlantTargetError);maxSlide=Math.Max(maxSlide,pose.PlantSlide);
        }
        output.WriteLine("Unmodified upright1.4m/.62duty walk: maxContactError="+maxError+" maxSlide="+maxSlide);
        Assert.True(maxError>.02f); // A disclosed infeasible baseline is not silently counted as a V05 pass.
    }
    [Fact]
    public void ResetAndDiscontinuousCorrectionReplanContactsWithinTwoQueries()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();
        motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),ground);
        var pose=motion.Evaluate(new SoldierMotionInput(new Float3(10,0,20),1,20,3.5f,reset:true),ground);
        Assert.True(pose.ContactsReset);Assert.InRange(pose.GroundQueries,0,2);Assert.InRange(pose.PlantSlide,0,1e-6f);
        Assert.Equal(10,pose.WorldPositions[0].X);Assert.Equal(20,pose.WorldPositions[0].Z);
    }
    [Fact]
    public void MissingGroundAndUnsupportedInputsRejectInsteadOfInventingFootTargets()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,6));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,aimYawRadians:1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,reloadPhase:-.5f));
        Assert.Throws<InvalidOperationException>(()=>new SoldierMotion(Rifle()).Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),new MissingGround()));
    }
    [Fact]
    public void ReusedPoseEvaluationDoesNotAllocateAfterWarmup()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();var input=new SoldierMotionInput(new Float3(0,0,0),0,0,0);
        var pose=motion.Evaluate(input,ground);for(int i=0;i<100;i++)motion.Evaluate(input,ground);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)motion.Evaluate(input,ground);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Same(pose,motion.Evaluate(input,ground));Assert.Equal(0,bytes);
    }
    private static Float3 Transform(Float3 origin,Quat q,Float3 p)
    {
        float tx=2*(q.Y*p.Z-q.Z*p.Y),ty=2*(q.Z*p.X-q.X*p.Z),tz=2*(q.X*p.Y-q.Y*p.X);
        return new Float3(origin.X+p.X+q.W*tx+q.Y*tz-q.Z*ty,origin.Y+p.Y+q.W*ty+q.Z*tx-q.X*tz,origin.Z+p.Z+q.W*tz+q.X*ty-q.Y*tx);
    }
}
