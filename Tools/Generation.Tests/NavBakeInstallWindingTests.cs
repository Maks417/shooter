using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Xunit;

public sealed class NavBakeInstallWindingTests
{
    private static float CrossY(Float3 a, Float3 b, Float3 c) => (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z);

    private static UnityNavMeshBakeCodec.Bake QuadBake(bool clockwiseFromAbove)
    {
        // Counter-clockwise listing in (X right, Z up) is how the software grid is stored; clockwise matches native triangulation.
        var verts = new[] { new Float3(0, 0, 0), new Float3(4, 0, 0), new Float3(4, 0, 4), new Float3(0, 0, 4) };
        uint[] order = clockwiseFromAbove ? new uint[] { 0, 3, 2, 1 } : new uint[] { 0, 1, 2, 3 };
        return new UnityNavMeshBakeCodec.Bake(verts, new[] { new UnityNavMeshBakeCodec.Polygon(order, 0, 0) }, new Float3(0, -1, 0), new Float3(4, 2, 4));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryTriangleFacesUpRegardlessOfStoredWinding(bool clockwise)
    {
        var bake = QuadBake(clockwise);
        int[] idx = UnityNavMeshBakeCodec.BuildUpFacingTriangleIndices(bake);
        Assert.Equal(6, idx.Length);
        for (int i = 0; i < idx.Length; i += 3)
            Assert.True(CrossY(bake.Vertices[idx[i]], bake.Vertices[idx[i + 1]], bake.Vertices[idx[i + 2]]) > 0f);
    }

    [Fact]
    public void DegenerateTrianglesAreSkipped()
    {
        var verts = new[] { new Float3(0, 0, 0), new Float3(1, 0, 0), new Float3(2, 0, 0) };
        var bake = new UnityNavMeshBakeCodec.Bake(verts, new[] { new UnityNavMeshBakeCodec.Polygon(new uint[] { 0, 1, 2 }, 0, 0) }, new Float3(0, 0, 0), new Float3(2, 1, 0));
        Assert.Empty(UnityNavMeshBakeCodec.BuildUpFacingTriangleIndices(bake));
    }

    [Fact]
    public void DecimateKeepsEndpointsAndCap()
    {
        var corners = new List<Float3>();
        for (int i = 0; i < 300; i++) corners.Add(new Float3(i, 0, 0));
        UnityNavMeshBakeCodec.DecimateCorners(corners, 100);
        Assert.Equal(100, corners.Count);
        Assert.Equal(0f, corners[0].X);
        Assert.Equal(299f, corners[99].X);
        var small = new List<Float3> { new Float3(1, 0, 0), new Float3(2, 0, 0) };
        UnityNavMeshBakeCodec.DecimateCorners(small, 100);
        Assert.Equal(2, small.Count);
    }

    [Fact]
    public void FallbackPathWorksOnClockwiseBakeAndRepeatedQueriesAgree()
    {
        var bake = QuadBake(true);
        var a = new List<Float3>();
        var b = new List<Float3>();
        Assert.True(UnityNavMeshBakeCodec.TryFindPath(bake, new Float3(0.5f, 0, 0.5f), new Float3(3.5f, 0, 3.5f), a));
        Assert.True(UnityNavMeshBakeCodec.TryFindPath(bake, new Float3(0.5f, 0, 0.5f), new Float3(3.5f, 0, 3.5f), b));
        Assert.Equal(a.Count, b.Count);
    }
}
