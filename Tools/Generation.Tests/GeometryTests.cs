using System;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class GeometryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(.001f, 1)]
    [InlineData(.005f, 2)]
    [InlineData(.02f, 4)]
    [InlineData(.02f, 8)]
    public void BoxesAreClosedOutwardAndKeepExactConservativeDimensions(float bevel, int segments)
    {
        var mesh = PrimitiveGeometry.Box(new Float3(.1f, .2f, .3f), bevel, segments);
        MeshValidation.Check(mesh);
        var bounds = Bounds(mesh);
        AssertClose(-.05f, bounds.Min.X); AssertClose(.05f, bounds.Max.X);
        AssertClose(-.1f, bounds.Min.Y); AssertClose(.1f, bounds.Max.Y);
        AssertClose(-.15f, bounds.Min.Z); AssertClose(.15f, bounds.Max.Z);
        Assert.All(mesh.Positions, p => { Assert.InRange(p.X, -.050001f, .050001f); Assert.InRange(p.Y, -.100001f, .100001f); Assert.InRange(p.Z, -.150001f, .150001f); });
        Assert.Equal(GeometryCodec.EncodeMesh(mesh), GeometryCodec.EncodeMesh(PrimitiveGeometry.Box(new Float3(.1f, .2f, .3f), bevel, segments)));
        if (bevel == 0) Assert.Equal(12, mesh.Indices.Count / 3);
        else
        {
            Assert.Contains(mesh.Normals, n => Math.Abs(n.X) > .1f && Math.Abs(n.Y) > .1f && Math.Abs(n.Z) > .1f);
            Assert.DoesNotContain(mesh.Positions, p => Math.Abs(p.X) == .05f && Math.Abs(p.Y) == .1f && Math.Abs(p.Z) == .15f);
        }
    }

    [Theory]
    [InlineData(3)] [InlineData(4)] [InlineData(8)] [InlineData(16)] [InlineData(128)]
    public void CylindersHaveCapsStableSeamsAndMeterUv(int radialSegments)
    {
        const float radius = .015f, length = .4f;
        var mesh = PrimitiveGeometry.Cylinder(radius, length, radialSegments);
        MeshValidation.Check(mesh);
        Assert.Equal(radialSegments * 4, mesh.Indices.Count / 3);
        AssertClose(-length / 2, Bounds(mesh).Min.Z); AssertClose(length / 2, Bounds(mesh).Max.Z);
        AssertClose((float)(2 * Math.PI * radius), mesh.Uv.Max(v => v.X));
        AssertClose(length, mesh.Uv.Max(v => v.Y));
        Assert.Equal(GeometryCodec.EncodeMesh(mesh), GeometryCodec.EncodeMesh(PrimitiveGeometry.Cylinder(radius, length, radialSegments)));
    }

    [Theory]
    [InlineData(.02f, 0)] [InlineData(0, .02f)] [InlineData(.02f, .01f)]
    public void ConeTipsAndFrustaContainNoCollapsedTriangles(float bottom, float top)
    {
        var mesh = PrimitiveGeometry.Cone(bottom, top, .08f, 12);
        MeshValidation.Check(mesh);
        Assert.Equal(bottom == 0 || top == 0 ? 24 : 48, mesh.Indices.Count / 3);
        var decoded = GeometryCodec.DecodeMesh(GeometryCodec.EncodeMesh(mesh));
        Assert.Equal(mesh.Indices.Count, decoded.Indices.Count);
    }

    [Fact]
    public void BoxMeterUvScalesWithDimensionsAndConstructionRemainsBounded()
    {
        var mesh = PrimitiveGeometry.Box(new Float3(1, 2, 3));
        AssertClose(3, mesh.Uv.Max(v => v.X)); AssertClose(3, mesh.Uv.Max(v => v.Y));
        var detailed = PrimitiveGeometry.Box(new Float3(.1f, .2f, .3f), .01f, 8);
        Assert.True(detailed.Positions.Count < 2000); Assert.True(detailed.Indices.Count / 3 < 4000);
    }

    [Fact]
    public void RigidAssemblyTransformsFramesPreservesWindingAndGroupsMaterialSlots()
    {
        var builder = new MeshAssembly(); var box = PrimitiveGeometry.Box(new Float3(2, 1, 4));
        float s = (float)Math.Sqrt(.5);
        builder.Add(box, new Float3(3, 4, 5), new Quat(0, 0, s, s), 7);
        builder.Add(PrimitiveGeometry.Cylinder(.1f, .5f, 8), new Float3(-3, 0, 0), Quat.Identity, 2);
        var mesh = builder.Build(); MeshValidation.Check(mesh);
        Assert.Equal(new ushort[] { 2, 7 }, mesh.Submeshes.Select(part => part.MaterialSlot));
        Assert.Equal(0U, mesh.Submeshes[0].FirstIndex); Assert.Equal(mesh.Submeshes[0].IndexCount, mesh.Submeshes[1].FirstIndex);
        var bounds = builder.Bounds;
        AssertClose(-3.1f, bounds.Min.X); AssertClose(3.5f, bounds.Max.X); AssertClose(5, bounds.Max.Y); AssertClose(7, bounds.Max.Z);
        Assert.Equal(box.Positions.Count + 80, builder.VertexCount);
        Assert.Equal(mesh.Indices.Count / 3, builder.TriangleCount);
        Assert.Equal(GeometryCodec.EncodeMesh(mesh), GeometryCodec.EncodeMesh(builder.Build()));
    }

    [Fact]
    public void ValidationRejectsOpenOrInvertedSolidsButAllowsExplicitOpenSurfaces()
    {
        var box = PrimitiveGeometry.Box(new Float3(1, 1, 1));
        uint[] openIndices = box.Indices.Take(box.Indices.Count - 3).ToArray();
        var open = new MeshData(box.Positions, box.Normals, box.Tangents, box.Uv, openIndices, new[] { new Submesh(0, (uint)openIndices.Length, 0) });
        Assert.Throws<ArgumentException>(() => MeshValidation.Check(open));
        MeshValidation.Check(open, false);
        uint[] flipped = box.Indices.ToArray(); uint temp = flipped[0]; flipped[0] = flipped[1]; flipped[1] = temp;
        var inverted = new MeshData(box.Positions, box.Normals, box.Tangents, box.Uv, flipped, box.Submeshes);
        Assert.Throws<ArgumentException>(() => MeshValidation.Check(inverted));
    }

    [Fact]
    public void InvalidDimensionsSegmentsAndBevelsRejectWithoutMalformedMeshes()
    {
        foreach (float dimension in new[] { 0, -1, float.NaN, float.PositiveInfinity, .0001f, 1001 })
            Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Box(new Float3(dimension, 1, 1)));
        foreach (float bevel in new[] { -.01f, .5f, .51f, float.NaN })
            Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Box(new Float3(1, 1, 1), bevel));
        Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Box(new Float3(1, 1, 1), .00001f));
        foreach (int segments in new[] { 0, 9 }) Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Box(new Float3(1, 1, 1), .1f, segments));
        foreach (int segments in new[] { 0, 2, 129 }) Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Cylinder(.1f, .3f, segments));
        Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Cylinder(0, .3f));
        Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Cone(0, 0, .3f));
        Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Cone(-.1f, .2f, .3f));
        Assert.ThrowsAny<ArgumentException>(() => PrimitiveGeometry.Cylinder(.1f, float.NaN));
    }

    [Fact]
    public void AssemblyRejectsInvalidOperationsAndBuildCopiesAreIndependent()
    {
        var assembly = new MeshAssembly(); Assert.Throws<InvalidOperationException>(() => assembly.Build());
        Assert.Throws<InvalidOperationException>(() => assembly.Bounds);
        var box = PrimitiveGeometry.Box(new Float3(1, 1, 1));
        Assert.Throws<ArgumentException>(() => assembly.Add(box, new Float3(0, 0, 0), default));
        var invalidFrames = new MeshData(box.Positions, Enumerable.Repeat(new Float3(2, 0, 0), box.Positions.Count).ToArray(), box.Tangents, box.Uv, box.Indices, box.Submeshes);
        Assert.Throws<FormatException>(() => assembly.Add(invalidFrames, new Float3(0, 0, 0), Quat.Identity));
        Assert.Equal(0, assembly.VertexCount);
        assembly.Add(box, new Float3(0, 0, 0), Quat.Identity); var first = assembly.Build(); byte[] before = GeometryCodec.EncodeMesh(first);
        assembly.Add(box, new Float3(3, 0, 0), Quat.Identity);
        Assert.Equal(before, GeometryCodec.EncodeMesh(first)); Assert.Equal(first.Positions.Count * 2, assembly.Build().Positions.Count);
    }

    private static Aabb Bounds(MeshData mesh) => new Aabb(new Float3(mesh.Positions.Min(p => p.X), mesh.Positions.Min(p => p.Y), mesh.Positions.Min(p => p.Z)), new Float3(mesh.Positions.Max(p => p.X), mesh.Positions.Max(p => p.Y), mesh.Positions.Max(p => p.Z)));
    private static void AssertClose(float expected, float actual) => Assert.InRange(Math.Abs(expected - actual), 0, .000002f);
}
