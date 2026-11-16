#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>ADR-078: the release store attaches the shared weapon package to every verified visual load.</summary>
    public sealed class VerifiedWeaponPackageStoreTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "shooter-weapon-store-" + Guid.NewGuid().ToString("N"));
        private readonly ContentId toolchain = ContentIdentity.Hash(Encoding.ASCII.GetBytes("weapon-store-tests"));

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private VerifiedMapPackageStore Publish(out ContentId lowRoot)
        {
            var published = MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound" }, new[] { "gameplay", "low" }, false, CancellationToken.None);
            Assert.Equal(ErrorCode.None, published.Error);
            var gameplay = MapPackageBuilder.Build("training-compound", "gameplay", toolchain).PackageRootHash;
            lowRoot = MapPackageBuilder.Build("training-compound", "low", toolchain).PackageRootHash;
            var roots = new Dictionary<string, ContentId> { ["training-compound"] = gameplay };
            var low = new Dictionary<string, ContentId> { ["training-compound"] = lowRoot };
            return new VerifiedMapPackageStore(root, toolchain, roots, low, null, null);
        }

        [Fact]
        public void VisualLoadServesWeaponMeshesFromTheWeaponPackage()
        {
            var store = Publish(out var lowRoot);
            Assert.True(store.TryLoadVisual(new MapId("training-compound"), "low", lowRoot, out var visuals, out string diagnostic), diagnostic);
            Assert.True(visuals.HasWeapons);
            Assert.Equal(WeaponPackage.Build(toolchain).PackageRootHash, visuals.WeaponPackageRootHash);
            Assert.True(MapVisualClosure.DecodeWeaponLod(visuals, "rifle-carbine", 3).Positions.Count > 0);
            Assert.NotNull(MapVisualClosure.DecodeWeaponMagazine(visuals, "rifle"));
            Assert.Null(MapVisualClosure.DecodeWeaponMagazine(visuals, "knife"));
        }

        [Fact]
        public void MissingWeaponIndexFailsTheVisualLoad()
        {
            var store = Publish(out var lowRoot);
            File.Delete(WeaponPackage.IndexPath(root));
            Assert.False(store.TryLoadVisual(new MapId("training-compound"), "low", lowRoot, out _, out string diagnostic));
            Assert.Equal("weapon-index-missing", diagnostic);
        }
    }
}
