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

        var skeleton = MapVisualClosure.DecodeSoldierSkeleton(payloads);
        Assert.Equal(SoldierSkeleton.JointCount, skeleton.Joints.Count);
        Assert.True(skeleton.Joints.Count > 1);

        Assert.True(MapVisualClosure.DecodeSoldierLod(payloads, 0).Positions.Count > 0);
        Assert.True(MapVisualClosure.DecodeRifleLod(payloads, 0).Positions.Count > 0);
        Assert.True(MapVisualClosure.DecodePistolLod(payloads, 0).Positions.Count > 0);

        var shader = MapJsonValue.Parse(payloads.Bytes(MapVisualClosure.ShaderPath("low"))).Object;
        Assert.Equal("procedural-urp-lit-v1", shader["shaderException"].String);
        Assert.Equal("Universal Render Pipeline/Lit", shader["unityShader"].String);
        Assert.True(shader["retainedByPlayer"].Boolean);
        var font = MapJsonValue.Parse(payloads.Bytes(MapVisualClosure.FontPath)).Object;
        Assert.Equal("engine-allowlist-v1", font["fontException"].String);
        Assert.True(font["retainedByPlayer"].Boolean);
    }

    [Fact]
    public void VisualPackageCorruptDependencyFailsClosed()
    {
        Assert.Equal(ErrorCode.None, MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound" }, new[] { "low" }, false, CancellationToken.None).Error);
        var package = MapPackageBuilder.Build("training-compound", "low", toolchain);
        string dir = MapPackagePublisher.PackageDirectory(root, package.PackageRootHash);
        string soldier = Path.Combine(dir, MapVisualClosure.SoldierLodPaths[0]);
        byte[] corrupt = { 9, 9, 9, 9 };
        File.WriteAllBytes(soldier, corrupt);
        Assert.False(MapVisualClosure.TryLoadFromPackageDirectory(dir, package.PackageRootHash, toolchain, out _, out string diagnostic));
        Assert.Contains("hash", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(corrupt, File.ReadAllBytes(soldier));
    }
}
