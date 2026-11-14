using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

[Collection("NativeNavProvider")]
public sealed class NavBakePublicationTests
{
    private readonly ContentId toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("nav-bake-publication-test"));

    [Fact]
    public void PurePackageBuildKeepsSoftwarePendingProvenance()
    {
        var package = MapPackageBuilder.Build("training-compound", "gameplay", toolchain);
        Assert.Equal(NavBakePublication.SoftwarePendingProvenance, package.NavBakeProvenance);
        Assert.StartsWith("software-walkable-grid", package.NavBakeProvenance);
        Assert.Contains("unity-native-bake-pending", package.NavBakeProvenance, StringComparison.Ordinal);
        Assert.False(UnityNavMeshBakePublisher.IsNativeProviderRegistered);
        Assert.False(string.IsNullOrEmpty(UnityNavMeshBakePublisher.PendingNativeBakeReason));
    }

    [Fact]
    public void SoftwareGridPathCornersStayOnBakeAndNativeProviderRejectsSoftwareLabel()
    {
        var plan = CompoundPlanner.Generate(460072030UL);
        float clearance = CompoundConnectivity.RequiredClearance;
        var bake = UnityNavMeshBakeCodec.BuildWalkableGrid(plan.CollisionBoxes, CompoundPlan.MapHalfExtent, 8f, clearance);
        var corners = new List<Float3>();
        Assert.True(UnityNavMeshBakeCodec.TryFindPath(bake, plan.PlayerSpawn, plan.ObjectiveCenter, corners));
        Assert.True(corners.Count >= 1);
        Assert.True(UnityNavMeshBakeCodec.ContainsPointXZ(bake, corners[0].X, corners[0].Z) ||
                    UnityNavMeshBakeCodec.ContainsPointXZ(bake, corners[corners.Count - 1].X, corners[corners.Count - 1].Z));

        Assert.Throws<ArgumentException>(() =>
            new NavBakePublication.NativeBake(bake, "software-walkable-grid;fake-native"));
    }

    [Fact]
    public void InjectedNativeProviderChangesProvenanceWithoutSoftwareClaim()
    {
        var previous = NavBakePublication.NativeProvider;
        try
        {
            NavBakePublication.NativeProvider = (boxes, half, radius, height, step, slope, margin) =>
            {
                var bake = UnityNavMeshBakeCodec.BuildWalkableGrid(boxes, half, 8f, radius + margin);
                return new NavBakePublication.NativeBake(bake, NavBakePublication.NativeProvenance);
            };
            var package = MapPackageBuilder.Build("depot-yard", "gameplay", toolchain);
            Assert.Equal(NavBakePublication.NativeProvenance, package.NavBakeProvenance);
            Assert.DoesNotContain("software-walkable-grid", package.NavBakeProvenance, StringComparison.Ordinal);
            Assert.True(UnityNavMeshBakePublisher.IsNativeProviderRegistered);
        }
        finally
        {
            NavBakePublication.NativeProvider = previous;
        }
    }

    [Fact]
    public void PublisherSoftwareFallbackMatchesCodecGrid()
    {
        var boxes = new[]
        {
            new CollisionBox(1, 1, 1, new Float3(0f, 1f, 0f), new Float3(2f, 2f, 2f), true)
        };
        var a = UnityNavMeshBakePublisher.BuildSoftwareFallback(boxes, 16f, 8f, 0.37f);
        var b = UnityNavMeshBakeCodec.BuildWalkableGrid(boxes, 16f, 8f, 0.37f);
        Assert.Equal(b.Polygons.Count, a.Polygons.Count);
        Assert.Equal(b.Vertices.Count, a.Vertices.Count);
    }
}
