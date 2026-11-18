using System;
using System.Linq;
using System.Text;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;
using Xunit.Abstractions;

/// <summary>ADR-106 soldier.loft 1.1.0: kernel-built kit on the 1.0.3 anatomy, product soldier version.</summary>
public sealed class SoldierProfileTests
{
    private readonly ITestOutputHelper output;
    public SoldierProfileTests(ITestOutputHelper output) { this.output = output; }

    private static SoldierRecipe Recipe() => SoldierRecipe.Parse(Encoding.UTF8.GetBytes(
        "{\"schemaId\":\"shooter.soldier.v1\",\"recipeId\":\"soldiers/baseline\",\"generatorId\":\"soldier.loft\",\"generatorVersion\":\"" + SoldierRecipe.ProfileVersion +
        "\",\"seed\":\"460072030\",\"parameters\":" + SoldierRecipe.ParametersJson + ",\"dependencies\":[]}"));

    [Fact]
    public void Profile_lods_are_closed_skinned_ordered_and_inside_the_bind_bounds()
    {
        var build = SoldierGeometry.Build(Recipe());
        int[] budget = { 26000, 11000, 4000 };
        int previous = int.MaxValue;
        for (int lod = 0; lod < 3; lod++)
        {
            var mesh = build.Lods[lod]; int triangles = mesh.Indices.Count / 3;
            output.WriteLine("lod" + lod + ": " + triangles + " triangles, " + mesh.Positions.Count + " vertices");
            Assert.InRange(triangles, 1, budget[lod]); Assert.True(triangles < previous); previous = triangles;
            GeometryCodec.DecodeMesh(GeometryCodec.EncodeMesh(mesh));
            MeshValidation.Check(mesh, true);
            Assert.All(mesh.Submeshes, s => Assert.True(s.MaterialSlot <= 5 && s.MaterialSlot != 2)); // covered faces: no skin slot
            for (int v = 0; v < mesh.Positions.Count; v++)
            {
                var w = mesh.Weights![v]; var j = mesh.Joints![v]; var p = mesh.Positions[v];
                Assert.InRange(Math.Abs(w.X + w.Y + w.Z + w.W - 1), 0, .0001f);
                Assert.InRange(j.X, (ushort)0, (ushort)22); Assert.InRange(j.Y, (ushort)0, (ushort)22);
                Assert.InRange(p.X, build.Bounds.Min.X, build.Bounds.Max.X); Assert.InRange(p.Y, build.Bounds.Min.Y, build.Bounds.Max.Y); Assert.InRange(p.Z, build.Bounds.Min.Z, build.Bounds.Max.Z);
            }
        }
    }

    [Fact]
    public void Profile_soldier_is_deterministic_and_keeps_the_legacy_versions()
    {
        Assert.Equal(GeometryCodec.EncodeMesh(SoldierGeometry.Build(Recipe()).Lods[0]), GeometryCodec.EncodeMesh(SoldierGeometry.Build(Recipe()).Lods[0]));
        Assert.True(SoldierRecipe.Supports("1.0.2") && SoldierRecipe.Supports("1.0.3") && SoldierRecipe.Supports(SoldierRecipe.ProfileVersion));
    }
}
