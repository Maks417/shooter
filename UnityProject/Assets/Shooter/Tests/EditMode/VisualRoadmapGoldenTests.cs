#nullable enable

using System;using System.IO;using System.Text;using Shooter.Generation;using Shooter.Generation.Environment;using Shooter.Generation.Fx;
using NUnit.Framework;
namespace Shooter.Tests.EditMode { public sealed class VisualRoadmapGoldenTests {
private static void Equal(string actual,string expected)=>Assert.That(actual,Is.EqualTo(expected));

        private static string Hash(Action<MemoryStream> action){using var s=new MemoryStream();action(s);var bytes=s.ToArray();return ContentIdentity.Hash(bytes).ToString();}
        private static void Add(MemoryStream s,Shooter.Contracts.MaterialData p){var bytes=GeometryCodec.EncodeMaterial(p);s.Write(bytes,0,bytes.Length);}
        [Test] public void Numeric_decal_atlas_has_the_same_bytes_in_both_lanes()
        {Equal(Hash(s=>Add(s,ProceduralDecals.Bake())),"ca70e4516a3c3a4ee65a12b444b59da8ae2efabaa2f84b59eaf51fa829d201b0");}
        [Test] public void Registered_sky_and_grade_recipes_match_both_lanes()
        {Equal(Hash(s=>{foreach(var m in MapRegistry.All){Add(s,AtmosphereBaker.Sky(m.SkyPreset));Add(s,AtmosphereBaker.Grade(m.Grade));}}),"73dd03c64a7e9983b70b4551eb995e1d715a84c5db737b7459e34421b89b02dc");}
        [Test] public void All_five_flipbooks_match_at_both_resolutions()
        {Equal(Hash(s=>{foreach(int n in new[]{32,64})foreach(FlipbookKind k in Enum.GetValues(typeof(FlipbookKind)))Add(s,FxFlipbook.Bake(k,n));}),"a48889bada1983689c991fcad4d778fcc5f9756885da17428cc09154bd104be5");}
        [Test] public void Version_103_soldier_and_insignia_match_both_lanes()
        {
            string source="{\"schemaId\":\"shooter.soldier.v1\",\"recipeId\":\"soldiers/baseline\",\"generatorId\":\"soldier.loft\",\"generatorVersion\":\"1.0.3\",\"seed\":\"460072030\",\"parameters\":"+SoldierRecipe.ParametersJson+",\"dependencies\":[]}";
            Equal(Hash(s=>{var build=SoldierGeometry.Build(SoldierRecipe.Parse(Encoding.UTF8.GetBytes(source)));foreach(var mesh in build.Lods){var b=GeometryCodec.EncodeMesh(mesh);s.Write(b,0,b.Length);}Add(s,SoldierInsignia.Bake(1));Add(s,SoldierInsignia.Bake(2));}),"a87c5bfba331b2a0213fbe820b4787457561f9700772a8cb468d4079694cb7a8");
        }
} }
