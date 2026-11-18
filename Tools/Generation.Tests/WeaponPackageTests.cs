using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

/// <summary>ADR-078: weapon meshes live in one shared weapon package, not in every map visual package.</summary>
[Collection("NativeNavProvider")]
public sealed class WeaponPackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "shooter-weapon-package-" + Guid.NewGuid().ToString("N"));
    private readonly ContentId toolchain = ContentIdentity.Hash(Encoding.ASCII.GetBytes("weapon-package-tests"));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public void PackageHoldsEveryCatalogMeshAndDecodesUnderItsSourceFingerprint()
    {
        var build = WeaponPackage.Build(toolchain);
        Assert.Equal(WeaponMeshCatalog.RequiredPaths().OrderBy(p => p, StringComparer.Ordinal),
            build.Dependencies.Select(d => d.Path).OrderBy(p => p, StringComparer.Ordinal));
        var bytes = build.Dependencies.ToDictionary(d => d.Path, d => d.Bytes);
        foreach (var source in WeaponMeshCatalog.All)
        {
            foreach (string path in WeaponMeshCatalog.LodPaths(source.MeshKey))
                Assert.True(GeometryCodec.DecodeMesh(ArtifactCodec.Decode(bytes[path], ContentKind.Mesh, source.Fingerprint, toolchain)).Positions.Count > 0, path);
            Assert.Equal(source.HasMagazineCycle, WeaponMeshCatalog.MagazinePath(source.MeshKey) != null);
        }
        Assert.Equal(new[] { "rifle", "rifle-carbine", "pistol", "knife", "grenade", "ak47", "ak102", "m16", "mk14", "mtac338", "g17", "golden-eagle" },
            WeaponMeshCatalog.All.Where(s => !s.IsSight).Select(s => s.MeshKey));
        // ADR-107: rail-mounted sights follow the guns, one mesh set per (sight, archetype) any gun allows.
        Assert.Equal(new[] { "sight-collimator-ak", "sight-scope2-ak", "sight-irons-ar", "sight-collimator-ar", "sight-scope2-ar",
                "sight-irons-dmr", "sight-collimator-dmr", "sight-scope2-dmr", "sight-scope4-dmr",
                "sight-irons-bolt", "sight-collimator-bolt", "sight-scope2-bolt", "sight-scope8-bolt" },
            WeaponMeshCatalog.All.Where(s => s.IsSight).Select(s => s.MeshKey));
        Assert.Equal("mesh-rifle-carbine-lod2.bin", WeaponMeshCatalog.LodPath("rifle-carbine", 2));
    }

    [Fact]
    public void GunMeshesAreByteIdenticalToTheirGeometryWeaponRecipes()
    {
        var bytes = WeaponPackage.Build(toolchain).Dependencies.ToDictionary(d => d.Path, d => d.Bytes);
        foreach (var source in WeaponMeshCatalog.All.Where(s => s.IsSight))
        {
            byte[] sight = ArtifactCodec.Encode(ContentKind.Mesh, GeometryCodec.EncodeMesh(WeaponSights.BuildLods(source.MountKind!, source.Sight!)[0]),
                WeaponMeshCatalog.SightFingerprint, toolchain);
            Assert.Equal(sight, bytes[WeaponMeshCatalog.LodPath(source.MeshKey, 0)]);
        }
        foreach (var source in WeaponMeshCatalog.All.Where(s => s.Recipe != null))
        {
            var gun = WeaponGeometry.Build(source.Recipe!);
            // Meshes carry their recipe's own generator version (ADR-082; 1.2.0 arsenal recipes under ADR-102).
            byte[] expected = ArtifactCodec.Encode(ContentKind.Mesh, GeometryCodec.EncodeMesh(gun.Lods[0]),
                GeneratorFingerprint.Compute(WeaponRecipe.GeneratorId, source.Recipe!.Version), toolchain);
            Assert.Equal(expected, bytes[WeaponMeshCatalog.LodPath(source.MeshKey, 0)]);
        }
    }

    [Theory]
    [InlineData("training-compound")]
    [InlineData("oil-depot")]
    public void MapVisualPackagesNoLongerCarryWeaponMeshes(string mapId)
    {
        var weaponPaths = WeaponMeshCatalog.RequiredPaths().ToHashSet(StringComparer.Ordinal);
        foreach (string profile in new[] { "low", "high" })
        {
            var package = MapPackageBuilder.Build(mapId, profile, toolchain);
            Assert.DoesNotContain(package.Dependencies, d => weaponPaths.Contains(d.Path));
            Assert.True(MapVisualClosure.TryExtract(package.Dependencies, mapId, profile, toolchain, out var payloads, out string diagnostic), diagnostic);
            Assert.False(payloads.HasWeapons);
            Assert.Throws<InvalidOperationException>(() => MapVisualClosure.DecodeWeaponLod(payloads, "rifle", 0));
        }
    }

    [Fact]
    public void PublishWritesIndexAndLoadsVerifiedBytes()
    {
        var published = MapPackagePublisher.Publish(root, toolchain, new[] { "depot-yard" }, new[] { "low" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, published.Error);
        Assert.True(File.Exists(WeaponPackage.IndexPath(root)));
        Assert.True(WeaponPackage.TryLoad(root, toolchain, out var weapons, out string diagnostic), diagnostic);
        Assert.Equal(WeaponPackage.Build(toolchain).PackageRootHash, weapons.PackageRootHash);

        var map = MapPackageBuilder.Build("depot-yard", "low", toolchain);
        string mapDir = MapPackagePublisher.PackageDirectory(root, map.PackageRootHash);
        Assert.True(MapVisualClosure.TryLoadFromPackageDirectory(mapDir, map.PackageRootHash, toolchain, out var visuals, out diagnostic), diagnostic);
        var combined = visuals.WithWeapons(weapons);
        Assert.True(combined.HasWeapons);
        Assert.Equal(weapons.PackageRootHash, combined.WeaponPackageRootHash);
        Assert.True(MapVisualClosure.DecodeWeaponLod(combined, "pistol", 3).Positions.Count > 0);

        // Republishing the same content is idempotent.
        Assert.True(WeaponPackage.TryPublish(root, toolchain, out var again, out diagnostic), diagnostic);
        Assert.Equal(weapons.PackageRootHash, again.PackageRootHash);
    }

    [Fact]
    public void GameplayOnlyPublishDoesNotWriteTheWeaponPackage()
    {
        var published = MapPackagePublisher.Publish(root, toolchain, new[] { "depot-yard" }, new[] { "gameplay" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, published.Error);
        Assert.False(File.Exists(WeaponPackage.IndexPath(root)));
    }

    [Fact]
    public void MissingTamperedOrForeignWeaponPackageFailsClosed()
    {
        Assert.False(WeaponPackage.TryLoad(root, toolchain, out _, out string missing));
        Assert.Equal("weapon-index-missing", missing);

        Assert.True(WeaponPackage.TryPublish(root, toolchain, out var build, out string diagnostic), diagnostic);
        Assert.False(WeaponPackage.TryLoad(root, ContentIdentity.Hash(Encoding.ASCII.GetBytes("other")), out _, out string foreign));
        Assert.Equal("weapon-index-toolchain-mismatch", foreign);

        string dir = MapPackagePublisher.PackageDirectory(root, build.PackageRootHash);
        string mesh = Path.Combine(dir, WeaponMeshCatalog.LodPath("rifle", 0));
        byte[] bytes = File.ReadAllBytes(mesh);
        bytes[bytes.Length - 1] ^= 1;
        File.WriteAllBytes(mesh, bytes);
        Assert.False(WeaponPackage.TryLoad(root, toolchain, out _, out string tampered));
        Assert.StartsWith("weapon-dependency-hash-mismatch:", tampered);
        // Corrupt bytes are retained for repair, never silently replaced.
        Assert.False(WeaponPackage.TryPublish(root, toolchain, out _, out string republish));
        Assert.StartsWith("corrupt-retained:", republish);
        Assert.Equal(bytes, File.ReadAllBytes(mesh));
    }
}
