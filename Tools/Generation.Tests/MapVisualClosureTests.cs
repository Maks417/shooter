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
    [Theory]
    [InlineData((byte)0,false)] [InlineData((byte)0,true)]
    [InlineData((byte)1,false)] [InlineData((byte)1,true)]
    [InlineData((byte)2,false)] [InlineData((byte)2,true)]
    [InlineData((byte)3,false)] [InlineData((byte)3,true)]
    public void ArticulatedHandsAreDeterministicAndKeepElbowAndWristSkinEndpoints(byte slot,bool support)
    {
        var rig=new FirstPersonHandRig(slot,support);
        var mesh=FirstPersonHandGeometry.Build(slot,support);
        Assert.Equal(FirstPersonHandRig.JointCount,rig.Skeleton.Joints.Count);
        Assert.Equal(GeometryCodec.EncodeMesh(mesh),GeometryCodec.EncodeMesh(FirstPersonHandGeometry.Build(slot,support)));
        Assert.Equal(GeometryCodec.EncodeSkeleton(rig.Skeleton),GeometryCodec.EncodeSkeleton(new FirstPersonHandRig(slot,support).Skeleton));
        MeshValidation.Check(mesh,false);
        Assert.NotNull(mesh.Joints); Assert.NotNull(mesh.Weights);
        Assert.Equal((ushort)1,mesh.Joints![0].X);
        Assert.Equal(1,mesh.Weights![0].X);
        Assert.Contains(mesh.Joints,j=>j.X==16);
        Assert.All(mesh.Weights,w=>Assert.InRange(w.X+w.Y+w.Z+w.W,.99999f,1.00001f));
        Assert.All(mesh.Joints,j=>Assert.InRange(j.X,(ushort)0,(ushort)16));
        Assert.All(mesh.Positions,p=>Assert.True(float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)));
    }
    [Fact]
    public void ReloadFingerReleaseHasContinuousReachAndReseatWindows()
    {
        Assert.Equal(0,FirstPersonHandRig.OpenForReload(-1));
        Assert.Equal(0,FirstPersonHandRig.OpenForReload(0));
        Assert.Equal(0,FirstPersonHandRig.OpenForReload(1));
        Assert.True(FirstPersonHandRig.OpenForReload(.14f)>.9f);
        Assert.Equal(0,FirstPersonHandRig.OpenForReload(.5f));
        Assert.True(FirstPersonHandRig.OpenForReload(.86f)>.9f);
        float previous=0;
        for(int i=0;i<=1000;i++)
        { float next=FirstPersonHandRig.OpenForReload(i/1000f); Assert.InRange(next,0,1); Assert.InRange(Math.Abs(next-previous),0,.03f); previous=next; }
        Assert.Throws<ArgumentOutOfRangeException>(()=>new FirstPersonHandRig(4,false));
        Assert.Throws<ArgumentOutOfRangeException>(()=>FirstPersonHandRig.OpenForReload(float.NaN));
    }
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
        Assert.True(payloads.HasWeapons);
        foreach (var source in WeaponMeshCatalog.All)
        {
            Assert.True(MapVisualClosure.DecodeWeaponLod(payloads, source.MeshKey, 0).Positions.Count > 0);
            Assert.Equal(source.HasMagazineCycle, MapVisualClosure.DecodeWeaponMagazine(payloads, source.MeshKey) != null);
            Assert.Equal(source.HasMagazineCycle, MapVisualClosure.DecodeWeaponCycle(payloads, source.MeshKey) != null);
        }
        for (byte slot = 0; slot < 4; slot++)
            foreach (bool support in new[] { false, true })
            {
                var hand = MapVisualClosure.DecodeFirstPersonHand(payloads, slot, support);
                Assert.InRange(hand.Positions.Count, 100, FirstPersonHandGeometry.MaxVerticesPerHand);
                Assert.Equal(3, hand.Submeshes.Count);
                Assert.NotNull(hand.Joints); // Independent camera rig; authoritative soldier anatomy is unchanged.
                var handSkeleton=MapVisualClosure.DecodeFirstPersonHandSkeleton(payloads,slot,support);
                Assert.Equal(FirstPersonHandRig.JointCount,handSkeleton.Joints.Count);
                Assert.All(hand.Joints!,j=>Assert.InRange(j.X,(ushort)0,(ushort)(FirstPersonHandRig.JointCount-1)));
                Assert.All(hand.Positions, p =>
                {
                    Assert.InRange(p.X, -.31f, .31f);
                    Assert.InRange(p.Y, -.31f, .095f); // Sight-eye is .100m pistol / .115m rifle.
                    Assert.InRange(p.Z, -.36f, .42f);
                });
                Assert.Equal(GeometryCodec.EncodeMesh(hand), GeometryCodec.EncodeMesh(FirstPersonHandGeometry.Build(slot, support)));
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
