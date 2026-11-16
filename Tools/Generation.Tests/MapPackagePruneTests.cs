using System;
using System.IO;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

/// <summary>ADR-076 addendum: a successful publish removes the packages it superseded, and nothing else.</summary>
[Collection("NativeNavProvider")]
public sealed class MapPackagePruneTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "shooter-prune-" + Guid.NewGuid().ToString("N"));
    private static ContentId Toolchain(string name) => ContentIdentity.Hash(Encoding.ASCII.GetBytes(name));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private string Dir(ContentId hash) => MapPackagePublisher.PackageDirectory(root, hash);

    private MapPackagePublishResult Publish(string mapId, string profile, ContentId toolchain)
    {
        var result = MapPackagePublisher.Publish(root, toolchain, new[] { mapId }, new[] { profile }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, result.Error);
        return result;
    }

    [Fact]
    public void RepublishRemovesSupersededPackagesOfTheSameMapProfileAndWeapons()
    {
        var a = Toolchain("prune-a");
        var b = Toolchain("prune-b");
        Publish("depot-yard", "low", a);
        var oldMap = MapPackageBuilder.Build("depot-yard", "low", a).PackageRootHash;
        var oldWeapons = WeaponPackage.Build(a).PackageRootHash;
        Assert.True(Directory.Exists(Dir(oldMap)));

        var result = Publish("depot-yard", "low", b);
        Assert.Equal(2, result.PrunedPackages);
        Assert.False(Directory.Exists(Dir(oldMap)));
        Assert.False(Directory.Exists(Dir(oldWeapons)));
        Assert.True(Directory.Exists(Dir(MapPackageBuilder.Build("depot-yard", "low", b).PackageRootHash)));
        Assert.True(WeaponPackage.TryLoad(root, b, out _, out string diagnostic), diagnostic);
    }

    [Fact]
    public void OtherMapsProfilesStagingAndUnreadableDirectoriesAreKept()
    {
        var a = Toolchain("prune-c");
        var b = Toolchain("prune-d");
        Publish("depot-yard", "gameplay", a);
        var otherProfile = MapPackageBuilder.Build("depot-yard", "gameplay", a).PackageRootHash;
        Publish("oil-depot", "gameplay", a);
        var otherMap = MapPackageBuilder.Build("oil-depot", "gameplay", a).PackageRootHash;
        string unreadable = Path.Combine(root, "ab", new string('a', 64));
        Directory.CreateDirectory(unreadable);
        string staging = Path.Combine(root, "cd", ".deadbeef.tmp");
        Directory.CreateDirectory(staging);

        // Gameplay-only publish of depot-yard with a new toolchain: only depot-yard/gameplay is superseded.
        var result = Publish("depot-yard", "gameplay", b);
        Assert.Equal(1, result.PrunedPackages);
        Assert.False(Directory.Exists(Dir(otherProfile)));
        Assert.True(Directory.Exists(Dir(otherMap)));
        Assert.True(Directory.Exists(unreadable));
        Assert.True(Directory.Exists(staging));

        // A different profile of the same map is not touched by a low-only publish.
        Publish("oil-depot", "low", b);
        Assert.True(Directory.Exists(Dir(otherMap)));
    }

    [Fact]
    public void ReusedPublishPrunesNothing()
    {
        var a = Toolchain("prune-e");
        Publish("depot-yard", "low", a);
        Assert.Equal(0, Publish("depot-yard", "low", a).PrunedPackages);
    }
}
