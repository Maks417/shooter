using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;
using Xunit.Abstractions;

public sealed class SoldierGeometryTests
{
    private readonly ITestOutputHelper output;
    public SoldierGeometryTests(ITestOutputHelper output){this.output=output;}
    private static byte[] Source()=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","soldier-recipe.json"));
    [Fact]
    public void Semantic23BindHierarchyAndFramesMatchDocumentedBaseline()
    {
        Assert.Equal(23,SoldierSkeleton.Definition.Joints.Count);Assert.Equal("rightToe",SoldierSkeleton.Names[22]);
        var p=SoldierSkeleton.BindPositions;
        Assert.Equal(.96f,p[1].Y);Assert.InRange(p[4].Y,1.37999f,1.38001f);Assert.InRange(p[6].Y,1.65999f,1.66001f);
        Assert.InRange(p[10].X,-.76001f,-.75999f);Assert.Equal(ushort.MaxValue,SoldierSkeleton.Definition.Joints[0].ParentId);
        Assert.Equal(17,SoldierSkeleton.Definition.Joints[18].ParentId);
        foreach(var f in SoldierSkeleton.Frames)
        {Assert.True(f.X.IsUnit && f.Y.IsUnit && f.Z.IsUnit);Assert.InRange(Dot(f.X,f.Y),-.00001,.00001);Assert.InRange(Dot(f.X,f.Z),-.00001,.00001);Assert.InRange(Dot(f.Y,f.Z),-.00001,.00001);}
        Assert.All(SoldierSkeleton.Definition.Joints,j=>Assert.Equal(1,j.LocalBindRotation.W));
        Assert.Equal(GeometryCodec.EncodeSkeleton(SoldierSkeleton.Definition),GeometryCodec.EncodeSkeleton(GeometryCodec.DecodeSkeleton(GeometryCodec.EncodeSkeleton(SoldierSkeleton.Definition))));
    }
    [Fact]
    public void NumericLoftsProduceValidSkinnedLodsWithinBudgets()
    {
        var build=SoldierGeometry.Build(SoldierRecipe.Parse(Source()));Assert.Equal(3,build.Lods.Count);
        int previous=int.MaxValue;int[] budget={24000,10000,3000};
        for(int lod=0;lod<3;lod++)
        {
            var mesh=build.Lods[lod];int triangles=mesh.Indices.Count/3;
            Assert.InRange(triangles,1,budget[lod]);Assert.True(triangles<previous);previous=triangles;
            GeometryCodec.DecodeMesh(GeometryCodec.EncodeMesh(mesh));MeshValidation.Check(mesh,true); // Component solids may intersect; this does not test anatomical self-intersection.
            Assert.All(mesh.Submeshes,s=>Assert.InRange(s.MaterialSlot,(ushort)0,(ushort)3));
            Assert.NotNull(mesh.Joints);Assert.NotNull(mesh.Weights);
            var active=new System.Collections.Generic.HashSet<ushort>();
            for(int v=0;v<mesh.Positions.Count;v++)
            {
                var j=mesh.Joints![v];var w=mesh.Weights![v];
                Assert.InRange(Math.Abs(w.X+w.Y+w.Z+w.W-1),0,.0001f);
                Assert.InRange(j.X,(ushort)0,(ushort)22);Assert.InRange(j.Y,(ushort)0,(ushort)22);
                if(w.X>0)active.Add(j.X);if(w.Y>0)active.Add(j.Y);
                var p=mesh.Positions[v];Assert.InRange(p.X,build.Bounds.Min.X,build.Bounds.Max.X);Assert.InRange(p.Y,build.Bounds.Min.Y,build.Bounds.Max.Y);Assert.InRange(p.Z,build.Bounds.Min.Z,build.Bounds.Max.Z);
            }
            Assert.InRange(active.Count,1,lod==2?20:40);
            output.WriteLine("lod"+lod+": "+triangles+" triangles "+mesh.Positions.Count+" vertices "+active.Count+" influencing joints");
        }
    }
    [Fact]
    public void ElbowAndKneeUseSharedSeamRingsWithBlendedSkin()
    {
        var build=SoldierGeometry.Build(SoldierRecipe.Parse(Source()));
        foreach(var mesh in build.Lods)
        {
            for(int side=-1;side<=1;side+=2)
            {
                ushort upper=(ushort)(side<0?8:12),lower=(ushort)(upper+1);
                int elbowVertices=0,kneeVertices=0;
                for(int v=0;v<mesh.Positions.Count;v++)
                {
                    var p=mesh.Positions[v];var j=mesh.Joints![v];var w=mesh.Weights![v];
                    if(Math.Abs(p.X-side*.50f)<.00001f && j.X==upper && j.Y==lower && w.X>.49f && w.X<.51f && w.Y>.49f && w.Y<.51f)elbowVertices++;
                    ushort thigh=(ushort)(side<0?15:19);
                    if(Math.Abs(p.Y-.47f)<.002f && j.X==thigh && j.Y==thigh+1 && w.X>.49f && w.X<.51f && w.Y>.49f && w.Y<.51f)kneeVertices++;
                }
                Assert.InRange(elbowVertices,7,13);Assert.InRange(kneeVertices,7,13);
            }
        }
    }
    [Fact]
    public void MeshesAreRepeatableAcrossSeedExtremesAndProfiles()
    {
        var initial=SoldierRecipe.Parse(Source());var baseline=SoldierGeometry.Build(initial);
        foreach(string seed in new[]{"0","18446744073709551615"})
        {
            var recipe=SoldierRecipe.Parse(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Source()).Replace("460072030",seed)));
            var other=SoldierGeometry.Build(recipe);
            Assert.NotEqual(initial.SourceHash,recipe.SourceHash);
            for(int lod=0;lod<3;lod++)Assert.Equal(GeometryCodec.EncodeMesh(baseline.Lods[lod]),GeometryCodec.EncodeMesh(other.Lods[lod]));
        }
        var generator=new SoldierAssetGenerator();var toolchain=new ContentId(new string('1',64));
        var low=generator.Generate(new GenerationRequest(initial.Document,"windows64","Low",toolchain),CancellationToken.None);
        var high=generator.Generate(new GenerationRequest(initial.Document,"windows64","High",toolchain),CancellationToken.None);
        Assert.Equal(ErrorCode.None,low.Error);Assert.Equal(4,low.Artifacts.Count);
        Assert.Equal(low.Artifacts.Select(a=>a.Sha256),high.Artifacts.Select(a=>a.Sha256));
        var gameplay=generator.Generate(new GenerationRequest(initial.Document,"windows64","gameplay",toolchain),CancellationToken.None);
        Assert.Single(gameplay.Artifacts);Assert.Equal(ContentKind.Skeleton,gameplay.Artifacts[0].Kind);
        Assert.Equal(low.Artifacts[0].Sha256,gameplay.Artifacts[0].Sha256);
    }
    [Theory]
    [InlineData("1800","1801")][InlineData("420","421")][InlineData("620000","610000")][InlineData("1.0.0","1.0.1")][InlineData("\"dependencies\": []","\"dependencies\": [\"0000000000000000000000000000000000000000000000000000000000000000\"]")]
    public void UnsupportedAnatomyVersionAndDependenciesAreRejected(string before,string after)
    {Assert.Throws<FormatException>(()=>SoldierRecipe.Parse(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Source()).Replace(before,after))));}
    [Fact]
    public void RequestErrorsAndCancellationNeverPublishPartialArtifacts()
    {
        var recipe=SoldierRecipe.Parse(Source());var generator=new SoldierAssetGenerator();
        var request=new GenerationRequest(recipe.Document,"windows64","Low",new ContentId(new string('1',64)));
        var cancelled=generator.Generate(request,new CancellationToken(true));Assert.Equal(ErrorCode.Cancelled,cancelled.Error);Assert.Empty(cancelled.Artifacts);
        var invalid=generator.Generate(new GenerationRequest(recipe.Document,"windows64","Unknown",request.ToolchainHash),CancellationToken.None);Assert.Equal(ErrorCode.InvalidInput,invalid.Error);Assert.Empty(invalid.Artifacts);
    }
    private static double Dot(Float3 a,Float3 b)=>(double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;
}
