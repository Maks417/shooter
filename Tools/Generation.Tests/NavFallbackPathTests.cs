using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Xunit;

/// <summary>ADR-114: the packaged-graph fallback never pulls a far point across a wall nor thins a corner through one.</summary>
public sealed class NavFallbackPathTests
{
    [Fact]
    public void Runtime_snap_rejects_points_far_from_the_surface_that_the_grid_snap_accepts()
    {
        var walls = new[] { new CollisionBox(1, 1, 1, new Float3(0f, 1.5f, 0f), new Float3(6f, 3f, 6f), true) };
        var bake = CleanNavBake.Build(walls, 10f, 0.5f);
        var inside = new Float3(0f, 0f, 0f);         // 3.5 m from the nearest walkable cell
        var outside = new Float3(-8f, 0f, -8f);
        var corners = new List<Float3>();
        Assert.True(UnityNavMeshBakeCodec.TryFindPath(bake, outside, inside, corners, UnityNavMeshBakeCodec.GridMaxSnapMeters));
        Assert.False(UnityNavMeshBakeCodec.TryFindPath(bake, outside, inside, corners, UnityNavMeshBakeCodec.RuntimeMaxSnapMeters));
        Assert.Empty(corners);
        var nearEdge = new Float3(-3.9f, 0f, 0f);    // 0.5 m outside the cut: still snaps
        Assert.True(UnityNavMeshBakeCodec.TryFindPath(bake, outside, nearEdge, corners, UnityNavMeshBakeCodec.RuntimeMaxSnapMeters));
    }

    [Fact]
    public void Thinning_keeps_a_corner_whose_shortcut_crosses_a_wall()
    {
        // L-shaped route around a wall corner; the a->c shortcut is blocked, b->d is not.
        var a = new Float3(0, 0, 0); var b = new Float3(5, 0, 0); var c = new Float3(5, 0, 5); var d = new Float3(5, 0, 10);
        var corners = new List<Float3> { a, b, c, d };
        UnityNavMeshBakeCodec.ThinVisibleCorners(corners, 32, (p, q) => !(p.Equals(a) && (q.Equals(c) || q.Equals(d))));
        Assert.Equal(new[] { a, b, d }, corners);
    }

    [Fact]
    public void Thinning_without_a_clearance_query_skips_nothing_and_truncates_past_the_limit()
    {
        var corners = new List<Float3>();
        for (int i = 0; i < 10; i++) corners.Add(new Float3(i, 0, i % 2));
        UnityNavMeshBakeCodec.ThinVisibleCorners(corners, 4, null);
        Assert.Equal(4, corners.Count);
        Assert.Equal(new Float3(3, 0, 1), corners[3]);
    }
}
