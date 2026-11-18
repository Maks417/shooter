using System;
using System.Collections.Generic;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

/// <summary>ADR-114: a navigation bake with walkable floor inside (or brushing) solid collision never publishes.</summary>
[Collection("NativeNavProvider")]
public sealed class NavCollisionAuditTests
{
    private const float R = CleanNavBake.Radius, H = CleanNavBake.Height, Step = CleanNavBake.Step;
    private readonly ContentId toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("nav-collision-audit-test"));

    private static CollisionBox Box(ulong id, float cx, float cz, float sx, float sz, float height = 3f, float baseY = 0f) =>
        new CollisionBox(id, 1, 1, new Float3(cx, baseY + height * 0.5f, cz), new Float3(sx, height, sz), true);

    public static IEnumerable<object[]> Maps()
    {
        foreach (string id in MapRegistry.MapIds) yield return new object[] { id };
    }

    [Fact]
    public void Floor_running_under_a_wall_leaks()
    {
        var boxes = new[] { Box(1, 0f, 0f, 0.4f, 10f) };
        var report = NavCollisionAudit.Run(CleanNavBake.LeakyFloor(20f), boxes, R, H, Step);
        Assert.False(report.Passed);
        Assert.Equal(1, report.LeakingBoxes);
        Assert.True(report.LeakAreaSquareMeters > 0.5f * 0.3f * 9.9f);
        Assert.Throws<InvalidOperationException>(() => NavCollisionAudit.ThrowIfLeaky("unit", CleanNavBake.LeakyFloor(20f), boxes, R, H, Step));
    }

    [Fact]
    public void Floor_cut_around_a_wall_by_the_agent_radius_passes()
    {
        var boxes = new[] { Box(1, 0.5f, 0.5f, 3f, 3f) };
        var report = NavCollisionAudit.Run(CleanNavBake.Build(boxes, 10f, 0.5f), boxes, R, H, Step);
        Assert.True(report.Passed, report.Summary());
        Assert.Equal(1, report.BlockingBoxes);
    }

    [Fact]
    public void Vertex_brushing_a_wall_fails_clearance_even_without_overlap()
    {
        var boxes = new[] { Box(1, 0.6f, 0.6f, 3f, 3f) };
        // A 0.01 m cut the floor too tightly: no polygon inside the wall, but vertices sit 0–0.1 m from it.
        var report = NavCollisionAudit.Run(CleanNavBake.Build(boxes, 10f, 0.5f, radius: 0.01f), boxes, R, H, Step);
        Assert.Equal(0, report.LeakingBoxes);
        Assert.True(report.ClearanceViolations > 0, report.Summary());
        Assert.False(report.Passed);
    }

    [Fact]
    public void Walkable_slabs_high_beams_and_decks_on_top_are_not_leaks()
    {
        var slab = new CollisionBox(1, 1, 1, new Float3(0f, 0.15f, 0f), new Float3(4f, 0.3f, 4f), true);   // stair tread
        var beam = Box(2, 6f, 0f, 1f, 4f, 0.5f, baseY: 2.5f);                                             // above head height
        var crate = Box(3, -6f, 0f, 2f, 2f, 1f);                                                          // deck on its top
        var boxes = new[] { slab, beam, crate };
        var deck = new List<Float3>
        {
            new Float3(-6.9f, 1.02f, -0.9f), new Float3(-6.9f, 1.02f, 0.9f), new Float3(-5.1f, 1.02f, 0.9f), new Float3(-5.1f, 1.02f, -0.9f)
        };
        var floor = CleanNavBake.Build(new[] { crate }, 10f, 0.5f);
        var vertices = new List<Float3>(floor.Vertices);
        var polys = new List<UnityNavMeshBakeCodec.Polygon>(floor.Polygons);
        uint b = (uint)vertices.Count;
        vertices.AddRange(deck);
        polys.Add(new UnityNavMeshBakeCodec.Polygon(new[] { b, b + 1, b + 2, b + 3 }, 0, 0));
        var bake = new UnityNavMeshBakeCodec.Bake(vertices, polys, floor.BoundsMin, floor.BoundsMax);
        var report = NavCollisionAudit.Run(bake, boxes, R, H, Step);
        Assert.True(report.Passed, report.Summary());
        Assert.Equal(2, report.BlockingBoxes); // beam and crate are solid; the tread is a walkable slab
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Product_map_whole_floor_bake_fails_and_wall_cut_bake_passes(string mapId)
    {
        var boxes = ProductMapPlanner.Generate(mapId).CollisionBoxes;
        var leaky = NavCollisionAudit.Run(CleanNavBake.LeakyFloor(CompoundPlan.MapHalfExtent), boxes, R, H, Step);
        Assert.False(leaky.Passed);
        Assert.True(leaky.LeakingBoxes > 0);
        var clean = NavCollisionAudit.Run(CleanNavBake.Build(boxes, CompoundPlan.MapHalfExtent), boxes, R, H, Step);
        Assert.True(clean.Passed, mapId + ": " + clean.Summary());
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Publishing_a_leaky_native_bake_throws_for_every_product_map(string mapId)
    {
        var previous = NavBakePublication.NativeProvider;
        try
        {
            NavBakePublication.NativeProvider = (boxes, half, radius, height, step, slope, margin) =>
                new NavBakePublication.NativeBake(CleanNavBake.LeakyFloor(half), NavBakePublication.NativeProvenance);
            var error = Assert.Throws<InvalidOperationException>(() => MapPackageBuilder.Build(mapId, "gameplay", toolchain));
            Assert.StartsWith("nav-collision-audit-failed:" + mapId, error.Message, StringComparison.Ordinal);

            NavBakePublication.NativeProvider = (boxes, half, radius, height, step, slope, margin) =>
                new NavBakePublication.NativeBake(CleanNavBake.Build(boxes, half), NavBakePublication.NativeProvenance);
            var package = MapPackageBuilder.Build(mapId, "gameplay", toolchain);
            Assert.Equal(NavBakePublication.NativeProvenance, package.NavBakeProvenance);
        }
        finally
        {
            NavBakePublication.NativeProvider = previous;
        }
    }
}
