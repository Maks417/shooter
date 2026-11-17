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
    private sealed class Ground : ISoldierGround
    {
        private readonly int mode; public int Queries;
        public Ground(int mode = 0) { this.mode = mode; }
        public float Height(float z) => mode == 1 ? z * (float)Math.Tan(Math.PI / 12) : mode == 3 ? -z * (float)Math.Tan(Math.PI / 12)
            : mode == 2 && z >= 1 ? .2f : mode == 4 && z >= 1 ? -.2f : 0;
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
        Assert.True(pose.WorldPositions[13].Y<pose.WorldPositions[12].Y-.05f, "Held rifle task keeps the trigger elbow below the shoulder instead of raising it beside the face.");
        Assert.True(pose.WorldPositions[9].Y<pose.WorldPositions[6].Y-.06f, "Support elbow stays below the head center in the neutral held pose.");
        output.WriteLine("Neutral held trigger elbow height relative to shoulder="+(pose.WorldPositions[13].Y-pose.WorldPositions[12].Y)+"m");
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
        output.WriteLine("Supported aim cone held grip worst position residual="+worst+"m; wrist weapon orientation residual remains explicit.");
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
            AssertShoulderTaskCones(pose);
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
    public void InitialUprightWalkBaselineReachConflictRemainsRecordedAsAnExplicitCalculation()
    {
        double oldVertical=.96-.04-.08-.08, oldHalfContact=1.4*.62/2;
        double oldRequired=Math.Sqrt(oldVertical*oldVertical+oldHalfContact*oldHalfContact);
        double maximum=(.45+Math.Sqrt(.39*.39+.02*.02))*.98;
        output.WriteLine("Version1 unmodified upright1.4m/.62duty lower bound reach residual="+(oldRequired-maximum));
        Assert.True(oldRequired-maximum>.05); // Version2 posture/duty refinement is explicit; old failures remain evidence.
    }
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void VersionTwoPlantGatePassesSixteenPhasesWalkRunAndBothStanceExtremes(int terrain)
    {
        var rifle=Rifle();float maxError=0,maxSlide=0;
        foreach(float speed in new[]{3.5f,5.5f})foreach(float crouch in new[]{0f,1f})
        for(int phase=0;phase<16;phase++)
        {
            var motion=new SoldierMotion(rifle);var ground=new Ground(terrain);
            double initial=(speed>3.5f?2.2:1.4)*phase/16;
            for(int tick=0;tick<600;tick++)
            {
                float z=tick*speed/60;var root=new Float3(0,ground.Height(z),z);
                var pose=motion.Evaluate(new SoldierMotionInput(root,0,initial+z,speed,crouch),ground);
                maxError=Math.Max(maxError,pose.PlantTargetError);maxSlide=Math.Max(maxSlide,pose.PlantSlide);
                Assert.InRange(pose.PlantTargetError,0,.05f);Assert.InRange(pose.PlantSlide,0,.05f);
                Assert.InRange(pose.PelvisCorrection,-.080001f,0);Assert.InRange(pose.GroundQueries,0,2);
                Assert.Equal(root.Y,pose.WorldPositions[0].Y);
            }
        }
        output.WriteLine("motion="+SoldierMotion.Version+" terrain="+terrain+" poses=38400 maxPlantTargetError="+maxError+" maxPlantSlide="+maxSlide+"; numerical contact gate only, not visual/hardware acceptance");
    }
    [Fact]
    public void VersionTwoReloadTaskGatePassesNineAimBoundariesAndAllHundredOnePhases()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();float maxError=0;
        foreach(float crouch in new[]{0f,1f})
        foreach(float yaw in new[]{-SoldierMotion.MaxAimYawRadians,0,SoldierMotion.MaxAimYawRadians})
        foreach(float pitch in new[]{-SoldierMotion.MaxAimPitchRadians,0,SoldierMotion.MaxAimPitchRadians})
        for(int phase=0;phase<=100;phase++)
        {
            var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0,crouch,yaw,pitch,phase/100f),ground);
            maxError=Math.Max(maxError,Math.Max(pose.LeftGripError,pose.RightGripError));
            Assert.InRange(pose.LeftGripError,0,.02f);Assert.InRange(pose.RightGripError,0,.02f);
            AssertShoulderTaskCones(pose);
        }
        output.WriteLine("motion="+SoldierMotion.Version+" reload poses=1818 full supported aim cone maximumTaskError="+maxError+"m; wrist orientation discrepancy reported separately");
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
        var motion=new SoldierMotion(Rifle());
        Assert.Throws<InvalidOperationException>(()=>motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),new MissingGround()));
        var recovered=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),new Ground());
        Assert.True(recovered.ContactsReset);Assert.Equal(2,recovered.GroundQueries);
    }
    [Fact]
    public void ReusedPoseEvaluationDoesNotAllocateAfterWarmup()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();var input=new SoldierMotionInput(new Float3(0,0,0),0,0,0);
        var pose=motion.Evaluate(input,ground);for(int i=0;i<100;i++)motion.Evaluate(input,ground);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)motion.Evaluate(input,ground);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Same(pose,motion.Evaluate(input,ground));Assert.Equal(0,bytes);
        before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)
        {
            float z=i*3.5f/60;
            motion.Evaluate(new SoldierMotionInput(new Float3(0,0,z),0,z,3.5f),ground);
        }
        bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,bytes);
    }
    [Fact]
    public void UpperBodyReuseIsExactRigidTranslationAndInvalidatesOnAimOrActionChanges()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();
        var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),ground);
        Assert.False(pose.UpperBodyCacheHit);
        var chest=pose.WorldPositions[4];var hand=pose.WorldPositions[14];var weapon=pose.WeaponPosition;
        pose=motion.Evaluate(new SoldierMotionInput(new Float3(.1f,0,.1f),0,0,0,1,reset:true),ground);
        Assert.True(pose.UpperBodyCacheHit);
        var delta=new Float3(pose.WorldPositions[4].X-chest.X,pose.WorldPositions[4].Y-chest.Y,pose.WorldPositions[4].Z-chest.Z);
        Assert.InRange(Distance(pose.WorldPositions[14],new Float3(hand.X+delta.X,hand.Y+delta.Y,hand.Z+delta.Z)),0,1e-6);
        Assert.InRange(Distance(pose.WeaponPosition,new Float3(weapon.X+delta.X,weapon.Y+delta.Y,weapon.Z+delta.Z)),0,1e-6);
        pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0,aimYawRadians:.1f),ground);Assert.False(pose.UpperBodyCacheHit);
        pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0,aimYawRadians:.1f,reloadPhase:.5f),ground);Assert.False(pose.UpperBodyCacheHit);
    }
    [Fact]
    public void DeathCollapseLowersPelvisAndHeadTowardGroundAndStaysFinite()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();
        var standing=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),ground);
        float standPelvis=standing.WorldPositions[1].Y; float standHead=standing.WorldPositions[6].Y;
        float midPelvis=0, midHead=0, endPelvis=0, endHead=0;
        for (int i = 0; i <= 20; i++)
        {
            float phase = i / 20f;
            var pose = motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0,deathPhase:phase),ground);
            Assert.False(pose.UpperBodyCacheHit);
            Assert.InRange(pose.GroundQueries,0,2);
            for (int j = 0; j < SoldierSkeleton.JointCount; j++)
            {
                Assert.True(float.IsFinite(pose.WorldPositions[j].X) && float.IsFinite(pose.WorldPositions[j].Y) && float.IsFinite(pose.WorldPositions[j].Z));
                Assert.True(pose.WorldPositions[j].Y >= -1e-3f);
            }
            if (i == 10) { midPelvis = pose.WorldPositions[1].Y; midHead = pose.WorldPositions[6].Y; }
            if (i == 20) { endPelvis = pose.WorldPositions[1].Y; endHead = pose.WorldPositions[6].Y; }
        }
        Assert.True(midPelvis < standPelvis - .15f);
        Assert.True(endPelvis < midPelvis);
        Assert.True(endHead < standHead - .4f);
        Assert.InRange(endPelvis, 0f, .35f);
        output.WriteLine("motion="+SoldierMotion.Version+" deathCollapse standPelvis="+standPelvis+" mid="+midPelvis+" end="+endPelvis+" endHead="+endHead);
    }
    [Fact]
    public void DeathCollapsePhaseOneIsDeterministicAndIdempotent()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();
        var first=motion.Evaluate(new SoldierMotionInput(new Float3(1,.5f,-2),.3f,0,0,deathPhase:1f),ground);
        var snapshot=new Float3[SoldierSkeleton.JointCount];
        var rotX=new float[SoldierSkeleton.JointCount];var rotY=new float[SoldierSkeleton.JointCount];
        var rotZ=new float[SoldierSkeleton.JointCount];var rotW=new float[SoldierSkeleton.JointCount];
        for (int j = 0; j < SoldierSkeleton.JointCount; j++)
        {
            snapshot[j]=first.WorldPositions[j];
            rotX[j]=first.WorldRotations[j].X; rotY[j]=first.WorldRotations[j].Y;
            rotZ[j]=first.WorldRotations[j].Z; rotW[j]=first.WorldRotations[j].W;
        }
        var second=motion.Evaluate(new SoldierMotionInput(new Float3(1,.5f,-2),.3f,0,0,deathPhase:1f),ground);
        Assert.Same(first, second);
        for (int j = 0; j < SoldierSkeleton.JointCount; j++)
        {
            Assert.InRange(Distance(snapshot[j], second.WorldPositions[j]), 0, 1e-6);
            Assert.InRange(Math.Abs(rotX[j] - second.WorldRotations[j].X), 0, 1e-6);
            Assert.InRange(Math.Abs(rotY[j] - second.WorldRotations[j].Y), 0, 1e-6);
            Assert.InRange(Math.Abs(rotZ[j] - second.WorldRotations[j].Z), 0, 1e-6);
            Assert.InRange(Math.Abs(rotW[j] - second.WorldRotations[j].W), 0, 1e-6);
        }
        Assert.Equal(0f, second.WorldPositions[0].Y); // Ground mode0 samples height 0, root clamps to ground.
        Assert.True(second.WorldPositions[1].Y < .35f);
    }
    [Fact]
    public void LivingDeathPhaseNegativeMatchesBaselineStandingSample()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();
        var living=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),ground);
        var pelvis=living.WorldPositions[1]; var head=living.WorldPositions[6]; var weapon=living.WeaponPosition;
        var explicitLiving=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0,deathPhase:-1f),ground);
        Assert.InRange(Distance(pelvis, explicitLiving.WorldPositions[1]), 0, 1e-6);
        Assert.InRange(Distance(head, explicitLiving.WorldPositions[6]), 0, 1e-6);
        Assert.InRange(Distance(weapon, explicitLiving.WeaponPosition), 0, 1e-6);
    }
    [Fact]
    public void UnsupportedDeathPhaseRejects()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,deathPhase:-.5f));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,deathPhase:1.1f));
    }
    [Fact]
    public void SlidePoseLowersPelvisPitchesTorsoAndKeepsFeetNearGroundWhileRootTranslates()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();
        var standing=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),ground);
        float standPelvis=standing.WorldPositions[1].Y;
        Float3 prevRoot=default;
        for(int i=0;i<30;i++)
        {
            float z=i*.09f;
            var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,z),0,z,5.5f,slide:1f),ground);
            Assert.InRange(pose.GroundQueries,0,2);
            Assert.True(pose.WorldPositions[1].Y < standPelvis - .2f, "slide pelvis should sit below standing");
            Assert.InRange(pose.WorldPositions[1].Y, .35f, .70f);
            Assert.True(pose.WorldPositions[17].Y < .25f && pose.WorldPositions[21].Y < .25f, "slide feet stay near ground");
            for(int j=0;j<SoldierSkeleton.JointCount;j++)
                Assert.True(float.IsFinite(pose.WorldPositions[j].X)&&float.IsFinite(pose.WorldPositions[j].Y)&&float.IsFinite(pose.WorldPositions[j].Z));
            if(i>0) Assert.True(pose.WorldPositions[0].Z > prevRoot.Z - 1e-4f);
            prevRoot=pose.WorldPositions[0];
        }
        output.WriteLine("motion="+SoldierMotion.Version+" slidePose pelvis lowered and feet tracked with translating root");
    }
    [Fact]
    public void RunHysteresisDoesNotResetContactsWhenSpeedChattersAroundWalkRunThreshold()
    {
        var motion=new SoldierMotion(Rifle());var ground=new Ground();
        double dist=0;
        // Enter run, then chatter between 3.3 and 3.7 (inside hysteresis band) without ContactsReset.
        for(int i=0;i<10;i++){dist+=.1;motion.Evaluate(new SoldierMotionInput(new Float3(0,0,(float)dist),0,dist,5.0f),ground);}
        int resets=0;
        for(int i=0;i<40;i++)
        {
            dist+=.06;
            float speed=i%2==0?3.7f:3.3f;
            var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,(float)dist),0,dist,speed),ground);
            if(pose.ContactsReset)resets++;
        }
        Assert.Equal(0,resets);
        output.WriteLine("motion="+SoldierMotion.Version+" runHysteresis chatterResets="+resets);
    }
    [Fact]
    public void UnsupportedSlideRejects()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,slide:-.1f));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,slide:1.1f));
    }

    // ADR-100 directional gait: drive a root along a world travel heading while the aim stays at yaw 0.
    private static (float maxError,float maxSlide,float maxReach,int resets) Travel(SoldierMotion motion,ISoldierGround ground,
        ref Float3 root,ref double distance,float heading,float speed,int ticks,bool directional,Action<SoldierPose>? each=null)
    {
        float maxError=0,maxSlide=0,maxReach=0;int resets=0;
        float dx=(float)Math.Sin(heading)*speed/60,dz=(float)Math.Cos(heading)*speed/60;
        for(int i=0;i<ticks;i++)
        {
            root=new Float3(root.X+dx,0,root.Z+dz);distance+=speed/60;
            var pose=motion.Evaluate(new SoldierMotionInput(root,0,distance,speed,moveHeadingRadians:directional?heading:float.NaN),ground);
            maxError=Math.Max(maxError,pose.PlantTargetError);maxSlide=Math.Max(maxSlide,pose.PlantSlide);
            foreach(int foot in new[]{17,21})
            {
                var f=pose.WorldPositions[foot];
                maxReach=Math.Max(maxReach,(float)Math.Sqrt((f.X-root.X)*(f.X-root.X)+(f.Z-root.Z)*(f.Z-root.Z)));
            }
            if(pose.ContactsReset)resets++;
            Assert.InRange(pose.GroundQueries,0,2);
            each?.Invoke(pose);
        }
        return (maxError,maxSlide,maxReach,resets);
    }
    [Fact]
    public void StrafeSideStepsKeepPlantedFeetUnderTheBodyInsteadOfTrailing()
    {
        float right=(float)Math.PI/2;
        var root=new Float3(0,0,0);double distance=0;
        var strafe=Travel(new SoldierMotion(Rifle(),new ShootingPosture()),new Ground(),ref root,ref distance,right,3.0f,240,true);
        root=new Float3(0,0,0);distance=0;
        var legacy=Travel(new SoldierMotion(Rifle(),new ShootingPosture()),new Ground(),ref root,ref distance,right,3.0f,240,false);
        output.WriteLine("ADR-100 strafe maxReach="+strafe.maxReach+" plantError="+strafe.maxError+" plantSlide="+strafe.maxSlide+
            " | aim-heading feet maxReach="+legacy.maxReach+" plantError="+legacy.maxError);
        Assert.InRange(strafe.maxError,0,.05f);Assert.InRange(strafe.maxSlide,0,.05f);
        Assert.InRange(strafe.maxReach,0,.7f);
        Assert.True(legacy.maxReach>strafe.maxReach+.3f,"the old aim-heading stride trails the strafing root");
        Assert.Equal(1,strafe.resets);
    }
    [Fact]
    public void BackpedalStepsBackwardWithinReach()
    {
        var root=new Float3(0,0,0);double distance=0;float minZ=float.MaxValue;
        var back=Travel(new SoldierMotion(Rifle(),new ShootingPosture()),new Ground(),ref root,ref distance,(float)Math.PI,2.5f,240,true,
            pose=>minZ=Math.Min(minZ,Math.Min(pose.WorldPositions[17].Z,pose.WorldPositions[21].Z)));
        Assert.InRange(back.maxError,0,.05f);Assert.InRange(back.maxReach,0,.7f);
        Assert.True(minZ<root.Z+.3f,"feet follow the backward travel");
    }
    [Fact]
    public void StrafeReversalReplansTheSwingWithoutResetOrPlantError()
    {
        var motion=new SoldierMotion(Rifle(),new ShootingPosture());var ground=new Ground();
        var root=new Float3(0,0,0);double distance=0;
        Travel(motion,ground,ref root,ref distance,(float)Math.PI/2,3f,90,true);
        float maxError=0,maxReach=0;int resets=0;
        for(int flip=0;flip<6;flip++)
        {
            float heading=flip%2==0?-(float)Math.PI/2:(float)Math.PI/2;
            var leg=Travel(motion,ground,ref root,ref distance,heading,3f,40+flip*7,true);
            maxError=Math.Max(maxError,leg.maxError);maxReach=Math.Max(maxReach,leg.maxReach);resets+=leg.resets;
        }
        output.WriteLine("ADR-100 strafe reversal maxError="+maxError+" maxReach="+maxReach+" resets="+resets);
        Assert.Equal(0,resets);Assert.InRange(maxError,0,.05f);Assert.InRange(maxReach,0,.8f);
    }
    [Fact]
    public void AirbornePoseTucksLegsWithoutGroundSamplesAndRelandsWithADip()
    {
        var motion=new SoldierMotion(Rifle(),new ShootingPosture());var missing=new MissingGround();var ground=new Ground();
        var standing=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0),ground);
        float standKnee=Math.Max(standing.LeftKneeFlexion,standing.RightKneeFlexion);float standPelvis=standing.WorldPositions[1].Y;
        var air=new Float3(0,.5f,.3f);
        var rising=motion.Evaluate(new SoldierMotionInput(air,0,.3,4,moveHeadingRadians:0,airborne:1,verticalSpeedMetersPerSecond:4),missing);
        Assert.Equal(0,rising.GroundQueries);Assert.True(rising.ContactsReset);
        float risingFeet=Math.Min(rising.WorldPositions[17].Y,rising.WorldPositions[21].Y)-air.Y;
        float risingKnee=Math.Min(rising.LeftKneeFlexion,rising.RightKneeFlexion);
        var falling=motion.Evaluate(new SoldierMotionInput(air,0,.35,4,moveHeadingRadians:0,airborne:1,verticalSpeedMetersPerSecond:-6),missing);
        float fallingFeet=Math.Min(falling.WorldPositions[17].Y,falling.WorldPositions[21].Y)-air.Y;
        Assert.True(risingKnee>standKnee+.5f,"rising tuck bends the knees: "+risingKnee+" vs "+standKnee);
        Assert.True(risingFeet>.25f&&fallingFeet<risingFeet-.1f,"feet tuck up rising and reach down falling: "+risingFeet+"/"+fallingFeet);
        for(int j=0;j<SoldierSkeleton.JointCount;j++)Assert.True(falling.WorldRotations[j].IsValid);
        Assert.InRange(rising.RightGripError,0,.02f);
        // Landing re-plants the gait contacts and dips the pelvis at mid phase.
        var landed=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,.4f),0,.4,0,land:.5f),ground);
        Assert.True(landed.ContactsReset);
        Assert.InRange(landed.WorldPositions[1].Y,standPelvis-SoldierMotion.LandDipMeters-.001f,standPelvis-SoldierMotion.LandDipMeters+.001f);
        output.WriteLine("ADR-100 airborne knee="+risingKnee+" stand="+standKnee+" risingFeet="+risingFeet+" fallingFeet="+fallingFeet);
    }
    [Fact]
    public void SlideDivesAlongTravelWhileTheChestAndWeaponStayOnAim()
    {
        var motion=new SoldierMotion(Rifle(),new ShootingPosture());var ground=new Ground();
        float heading=40*(float)Math.PI/180;
        var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,5,moveHeadingRadians:heading,slide:1),ground);
        var lead=pose.WorldPositions[21];var trail=pose.WorldPositions[17];
        float dx=lead.X-trail.X,dz=lead.Z-trail.Z,length=(float)Math.Sqrt(dx*dx+dz*dz);
        float alongTravel=(dx*(float)Math.Sin(heading)+dz*(float)Math.Cos(heading))/length;
        Assert.True(alongTravel>.95f,"feet split along the slide travel: "+alongTravel);
        var weaponForward=Transform(new Float3(0,0,0),pose.WeaponRotation,new Float3(0,0,1));
        Assert.InRange(weaponForward.X,-.02f,.02f);
        var aimOnly=new SoldierMotion(Rifle(),new ShootingPosture()).Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,5,slide:1),ground);
        var legacy=aimOnly.WorldPositions[21];
        Assert.InRange(legacy.X-aimOnly.WorldPositions[17].X,-.25f,.25f); // no heading: unchanged aim-aligned dive
    }
    [Fact]
    public void UnsupportedLocomotionInputsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,airborne:1.1f));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,verticalSpeedMetersPerSecond:31));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,land:-.5f));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SoldierMotionInput(new Float3(0,0,0),0,0,0,moveHeadingRadians:float.PositiveInfinity));
        Assert.False(new SoldierMotionInput(new Float3(0,0,0),0,0,0).HasMoveHeading);
    }
    private static Float3 Transform(Float3 origin,Quat q,Float3 p)
    {
        float tx=2*(q.Y*p.Z-q.Z*p.Y),ty=2*(q.Z*p.X-q.X*p.Z),tz=2*(q.X*p.Y-q.Y*p.X);
        return new Float3(origin.X+p.X+q.W*tx+q.Y*tz-q.Z*ty,origin.Y+p.Y+q.W*ty+q.Z*tx-q.X*tz,origin.Z+p.Z+q.W*tz+q.X*ty-q.Y*tx);
    }
    private static void AssertShoulderTaskCones(SoldierPose pose)
    {
        foreach(int shoulder in new[]{8,12})
        {
            var down=Transform(new Float3(0,0,0),pose.WorldRotations[shoulder-1],new Float3(0,-1,0));
            var a=pose.WorldPositions[shoulder];var b=pose.WorldPositions[shoulder+1];
            double projection=(b.X-a.X)*down.X+(b.Y-a.Y)*down.Y+(b.Z-a.Z)*down.Z;
            Assert.True(projection>=-1e-5,"Upper arm stays within the unchanged90-degree cone around anatomical task-neutral down; T-bind axes remain immutable.");
        }
    }
}
