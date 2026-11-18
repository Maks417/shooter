#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.World;
using Shooter.World.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.Tests
{
    /// <summary>
    /// ADR-114: the published native bake of every product map installs into Unity NavMesh whole. Before, only a few
    /// hundred triangles survived the install, so every runtime query missed and bots ran on the packaged fallback.
    /// </summary>
    public sealed class ProductNavMeshInstallTests
    {
        public static IEnumerable<string> ProductMaps() => MapRegistry.MapIds;

        [TestCaseSource(nameof(ProductMaps))]
        public void PublishedBakeInstallsWholeAndAnswersQueriesOnItsPolygons(string mapId)
        {
            var bake = LoadBake(mapId);
            NavMesh.RemoveAllNavMeshData();
            using var install = UnityNavMeshBakeInstaller.Install(bake, NavSettings.SliceDefault);
            var tri = NavMesh.CalculateTriangulation();
            int probes = 0, hits = 0;
            string firstMiss = "none";
            for (int p = 0; p < bake.Polygons.Count; p += 7)
            {
                var centre = Centre(bake, p);
                probes++;
                if (install.TrySamplePosition(centre, 0.5f, out _)) hits++;
                else if (firstMiss == "none") firstMiss = centre.ToString();
            }
            string summary = mapId + " bakePolygons=" + bake.Polygons.Count + " installedTriangles=" + tri.indices.Length / 3 +
                             " samples=" + hits + "/" + probes + " firstMiss=" + firstMiss;
            TestContext.WriteLine("SHOOTER_NAV_INSTALL " + summary);
            Assert.That(hits, Is.GreaterThanOrEqualTo(probes * 95 / 100), summary);
        }

        private static Float3 Centre(UnityNavMeshBakeCodec.Bake bake, int p)
        {
            var poly = bake.Polygons[p].Indices;
            float x = 0, y = 0, z = 0;
            foreach (uint i in poly) { x += bake.Vertices[(int)i].X; y += bake.Vertices[(int)i].Y; z += bake.Vertices[(int)i].Z; }
            return new Float3(x / poly.Length, y / poly.Length, z / poly.Length);
        }

        private static UnityNavMeshBakeCodec.Bake LoadBake(string mapId)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string lockPath = Path.Combine(root, "toolchain-lock.json");
            string cache = Path.Combine(root, "Artifacts", "map-packages-native");
            if (!File.Exists(Path.Combine(cache, "catalog.json"))) cache = Path.Combine(root, "Artifacts", "map-packages");
            if (!File.Exists(lockPath) || !Directory.Exists(cache)) Assert.Inconclusive("no map package cache");
            var toolchain = ContentIdentity.Hash(File.ReadAllBytes(lockPath));
            Assert.That(StaticMapCatalog.TryLoadPublished(cache, toolchain, out var catalog, out string diag), Is.True, diag);
            var store = new VerifiedMapPackageStore(cache, toolchain, catalog.GameplayRootHashes,
                catalog.LowVisualRootHashes, catalog.BalancedVisualRootHashes, catalog.HighVisualRootHashes);
            var key = new MapId(mapId);
            Assert.That(catalog.TryGet(key, out var entry), Is.True);
            Assert.That(store.TryLoadGameplay(key, entry.GameplayRootHash, out var published, out string loadDiag), Is.True, loadDiag);
            var fingerprint = GeneratorFingerprint.Compute(MapRecipe.GeneratorIdFor(mapId), MapRecipe.GeneratorVersion);
            var packageToolchain = new ContentId(JsonValue.Parse(published.PackageRootBytes).Object["toolchainHash"].String);
            var navigation = GeometryCodec.DecodeNavigation(ArtifactCodec.Decode(published.NavigationSgaf, ContentKind.Navigation, fingerprint, packageToolchain));
            return UnityNavMeshBakeCodec.Decode(navigation.Blob);
        }
    }
}
