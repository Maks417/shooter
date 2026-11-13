using System;
using System.IO;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;
using Xunit.Abstractions;

public sealed class SoldierClearanceTests
{
    private readonly ITestOutputHelper output;
    public SoldierClearanceTests(ITestOutputHelper output){this.output=output;}
    private sealed class Ground:ISoldierGround{public bool TrySample(float x,float z,out float y){y=0;return true;}}
    [Fact]
    public void ThirdIterationHeldAndReloadClearanceAndResidualsAreMeasuredAcrossSupportedRange()
    {
        var motion=new SoldierMotion(WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"FeatureRecipes","rifle.json"))));
        var ground=new Ground();float minimum=float.MaxValue,maxGrip=0,maxEye=0,maxStock=0;string worst="";
        foreach(float crouch in new[]{0f,1f})foreach(float yaw in new[]{-SoldierMotion.MaxAimYawRadians,0,SoldierMotion.MaxAimYawRadians})
        foreach(float pitch in new[]{-SoldierMotion.MaxAimPitchRadians,0,SoldierMotion.MaxAimPitchRadians})
        for(int phase=-1;phase<=100;phase++)
        {
            var pose=motion.Evaluate(new SoldierMotionInput(new Float3(0,0,0),0,0,0,crouch,yaw,pitch,phase<0?-1:phase/100f),ground);
            float clearance=ArmBodyClearance.Minimum(pose);
            if(clearance<minimum){minimum=clearance;worst="crouch="+crouch+" yaw="+yaw+" pitch="+pitch+" phase="+phase;}
            maxGrip=Math.Max(maxGrip,Math.Max(pose.LeftGripError,pose.RightGripError));maxEye=Math.Max(maxEye,pose.EyeSightLineErrorMeters);maxStock=Math.Max(maxStock,pose.RightStockErrorMeters);
        }
        output.WriteLine("Conservative proxy only:120mm source-derived shoulder attachment exclusion and6mm torso skin margin; actual skinned attachment surfaces and visual acceptance remain separate.");
        output.WriteLine("Third iteration minArmBodyClearance="+minimum+" worst="+worst+" maxGrip="+maxGrip+" maxStock="+maxStock+" maxEyeSightLine="+maxEye);
        Assert.InRange(maxGrip,0,.02f);Assert.InRange(maxStock,0,1e-5f);Assert.True(minimum>=0,"Real tube-versus-body clearance fails: "+minimum+" "+worst);
    }
}
