#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Shooter.Bootstrap.Release;
using Shooter.Generation.Environment;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>ADR-083: the release policy lists exactly the registered maps, so a new map cannot ship half-wired.</summary>
    public sealed class MapRegistryReleasePolicyTests
    {
        [Fact]
        public void ReleasePoliciesListEveryRegisteredMapInOrder()
        {
            Assert.Equal(MapRegistry.MapIds, ReleasePackagePolicy.RequiredMaps);
            string root = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "Tools", "release-package-policy.json")))
                root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Repository root not found.");
            using var policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Tools", "release-package-policy.json")));
            var listed = policy.RootElement.GetProperty("requiredMaps").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
            Assert.Equal(MapRegistry.MapIds, listed);
        }
    }
}
