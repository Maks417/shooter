#nullable enable
using System;using System.IO;using System.Linq;using System.Text;using System.Text.Json.Nodes;using Shooter.Generation;using Shooter.Generation.Environment;using Shooter.Generation.Fx;using Xunit;
namespace Shooter.Generation.Tests
{
 public sealed class VisualRoadmapBehaviorTests
 {
  [Fact] public void Decal_operations_change_only_visual_placements_not_gameplay()
  {
   string root=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(root,"Content","Maps")))root=Directory.GetParent(root)?.FullName??throw new Exception("Repository not found");
   foreach(string file in Directory.GetFiles(Path.Combine(root,"Content","Maps"),"*.map.json"))
   {
    var map=JsonNode.Parse(File.ReadAllText(file))!;if(map["layout"]==null)continue;var layout=map["layout"]!;
    var painted=ArenaLayoutDocument.Parse(layout.ToJsonString());
    var ops=(JsonArray)layout["ops"]!;for(int i=ops.Count-1;i>=0;i--)if(ops[i]!["op"]!.GetValue<string>()=="decal")ops.RemoveAt(i);
    layout.AsObject().Remove("documentVersion");var original=ArenaLayoutDocument.Parse(layout.ToJsonString());
    Assert.Equal(original.Generate(1).CollisionHash,painted.Generate(1).CollisionHash);Assert.NotEmpty(painted.Decals);Assert.Empty(original.Decals);
   }
  }
  [Fact] public void Atlas_planes_roundtrip_complete_mips_and_have_transparent_gutters()
  {
   var p=ProceduralDecals.Bake();var round=GeometryCodec.DecodeMaterial(GeometryCodec.EncodeMaterial(p));Assert.Equal(9,round.MipBytes.Count);
   for(int tile=0;tile<8;tile++){int start=((tile/4*64)*256+tile%4*64)*4;Assert.Equal(0,p.MipBytes[0][start+3]);}
   Assert.Throws<ArgumentOutOfRangeException>(()=>FxFlipbook.Bake(FlipbookKind.Dust,16));
   Assert.Throws<ArgumentOutOfRangeException>(()=>new MapGrade(red:2000));
   var identity=AtmosphereBaker.Grade(new MapGrade());int at=(15*1024+16*32+14)*4;Assert.InRange(identity.MipBytes[0][at],114,116);
  }
 }
}
