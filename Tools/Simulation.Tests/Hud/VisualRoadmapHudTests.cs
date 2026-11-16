#nullable enable
using Shooter.Generation;using Shooter.Presentation.Menus;using Shooter.Presentation.View;using Shooter.Generation.Environment;using Shooter.Contracts;using Xunit;
namespace Shooter.Simulation.Tests.Hud
{
 public sealed class VisualRoadmapHudTests
 {
  [Fact] public void Insertion_tips_rotate_but_reduced_motion_is_steady()
  {Assert.Equal(InsertionScreenCopy.TipAt(0,true),InsertionScreenCopy.TipAt(100,true));Assert.NotEqual(InsertionScreenCopy.TipAt(0,false),InsertionScreenCopy.TipAt(7,false));Assert.Equal(InsertionScreenCopy.TipAt(0,false),InsertionScreenCopy.TipAt(-7,false));}
  [Fact] public void Cosmetic_ground_samples_topmost_installed_support_in_range()
  {
   var boxes=new[]{new CollisionBox(1,1,1,new Float3(0,1,0),new Float3(2,.2f,2),true)};var ground=new CollisionSoldierGround(boxes){ReferenceHeight=1.1f};
   Assert.True(ground.TrySample(0,0,out float y));Assert.Equal(1.1f,y,4);Assert.False(ground.TrySample(20,0,out _));ground.ReferenceHeight=0;Assert.True(ground.TrySample(20,0,out y));Assert.Equal(0,y);
  }
 }
}
