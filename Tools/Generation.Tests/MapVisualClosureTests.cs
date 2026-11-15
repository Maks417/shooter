using System;
using System.IO;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

[Collection("NativeNavProvider")]
public sealed class MapVisualClosureTests : IDisposable
{
    [Fact]
    public void FoldedSleeveIsAClosedSurfaceWithoutInternalCapEdges()
    {
        foreach (float length in new[] { .28f, .58f })
        {
            var mesh = FirstPersonHandGeometry.BuildSleeve(length);
            var welded = new System.Collections.Generic.Dictionary<(long, long, long), int>();
            var ids = new int[mesh.Positions.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                var p = mesh.Positions[i];
                var key = ((long)Math.Round(p.X * 1000000), (long)Math.Round(p.Y * 1000000), (long)Math.Round(p.Z * 1000000));
                if (!welded.TryGetValue(key, out int id)) { id = welded.Count; welded.Add(key, id); }
                ids[i] = id;
            }
            var edges = new System.Collections.Generic.Dictionary<(int, int), int>();
            void Edge(int a, int b)
            {
                Assert.NotEqual(a, b);
                var key = a < b ? (a, b) : (b, a);
                edges.TryGetValue(key, out int count); edges[key] = count + 1;
            }
            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                int a = ids[mesh.Indices[i]], b = ids[mesh.Indices[i + 1]], c = ids[mesh.Indices[i + 2]];
                Edge(a, b); Edge(b, c); Edge(c, a);
            }
            Assert.All(edges.Values, count => Assert.Equal(2, count));
        }
    }

    private readonly string root = Path.Combine(Path.GetTempPath(), "shooter-map-visual-" + Guid.NewGuid().ToString("N"));
    private readonly ContentId toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("map-visual-closure-test-toolchain"));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public void VisualPackageIncludesSoldierWeaponLodsAndProvenance()
    {
        var package = MapPackageBuilder.Build("depot-yard", "low", toolchain);
        Assert.Equal(460072031UL, package.Seed);
        foreach (string role in new[] { "collision", "navigation", "metadata", "mesh", "material", "skeleton", "shader", "font" })
            Assert.Contains(package.Dependencies, d => d.Role == role);

        var payloads = PackagedVisualPayloads.FromBuild(package);
        Assert.Equal("depot-yard", payloads.MapId);
        Assert.Equal("low", payloads.Profile);
        foreach (string path in MapVisualClosure.RequiredPaths("low"))
            Assert.True(payloads.Has(path), path);
        foreach (string path in MapVisualClosure.MapMaterialPaths)
        {
            Assert.Equal(128, MapVisualClosure.DecodeMapMaterial(payloads, path).Width);
            Assert.True(payloads.Has(MapVisualClosure.MaterialCompanionPath(path, "normal")));
            Assert.True(payloads.Has(MapVisualClosure.MaterialCompanionPath(path, "mask")));
        }

        var skeleton = MapVisualClosure.DecodeSoldierSkeleton(payloads);
        Assert.Equal(SoldierSkeleton.JointCount, skeleton.Joints.Count);
        Assert.True(skeleton.Joints.Count > 1);

        Assert.True(MapVisualClosure.DecodeSoldierLod(payloads, 0).Positions.Count > 0);
        Assert.True(MapVisualClosure.DecodeRifleLod(payloads, 0).Positions.Count > 0);
        Assert.True(MapVisualClosure.DecodePistolLod(payloads, 0).Positions.Count > 0);
        Assert.True(MapVisualClosure.DecodeRifleMagazine(payloads).Positions.Count > 0);
        Assert.True(MapVisualClosure.DecodeRifleCycle(payloads).Positions.Count > 0);
        Assert.True(MapVisualClosure.DecodePistolMagazine(payloads).Positions.Count > 0);
        Assert.True(MapVisualClosure.DecodePistolCycle(payloads).Positions.Count > 0);
        for (byte slot = 0; slot < 2; slot++)
            foreach (bool support in new[] { false, true })
            {
                var hand = MapVisualClosure.DecodeFirstPersonHand(payloads, slot, support);
                Assert.InRange(hand.Positions.Count, 100, FirstPersonHandGeometry.MaxVerticesPerHand);
                Assert.Equal(3, hand.Submeshes.Count);
                Assert.Null(hand.Joints); // Camera props cannot inherit or modify authoritative soldier skin/hit poses.
                Assert.All(hand.Positions, p =>
                {
                    Assert.InRange(p.X, -.31f, .31f);
                    Assert.InRange(p.Y, -.31f, .095f); // Sight-eye is .100m pistol / .115m rifle.
                    Assert.InRange(p.Z, -.36f, .42f);
                });
                Assert.Equal(GeometryCodec.EncodeMesh(hand), GeometryCodec.EncodeMesh(FirstPersonHandGeometry.Build(slot == 1, support)));
            }

        var shader = MapJsonValue.Parse(payloads.Bytes(MapVisualClosure.ShaderPath("low"))).Object;
        Assert.Equal("procedural-urp-lit-v1", shader["shaderException"].String);
        Assert.Equal("Universal Render Pipeline/Lit", shader["unityShader"].String);
        Assert.True(shader["retainedByPlayer"].Boolean);
        var font = MapJsonValue.Parse(payloads.Bytes(MapVisualClosure.FontPath)).Object;
        Assert.Equal("ui-typeface-bank-v1", font["fontException"].String);
        Assert.True(font["retainedByPlayer"].Boolean);
    }

    [Theory]
    [InlineData("mesh-soldier-lod0.bin")]
    [InlineData("mesh-fp-rifle-trigger-hand.bin")]
    public void VisualPackageCorruptDependencyFailsClosed(string dependencyPath)
    {
        Assert.Equal(ErrorCode.None, MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound" }, new[] { "low" }, false, CancellationToken.None).Error);
        var package = MapPackageBuilder.Build("training-compound", "low", toolchain);
        string dir = MapPackagePublisher.PackageDirectory(root, package.PackageRootHash);
        string soldier = Path.Combine(dir, dependencyPath);
        byte[] corrupt = { 9, 9, 9, 9 };
        File.WriteAllBytes(soldier, corrupt);
        Assert.False(MapVisualClosure.TryLoadFromPackageDirectory(dir, package.PackageRootHash, toolchain, out _, out string diagnostic));
        Assert.Contains("hash", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(corrupt, File.ReadAllBytes(soldier));
    }
}
