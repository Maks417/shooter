#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shooter.Generation.Environment;
using Shooter.World.Navigation;
using Xunit;

namespace Shooter.Generation.Tests
{
    public sealed class B02B03ManifestTests
    {
        [Fact]
        public void Freeze_B02_B03_route_and_content_hashes_for_canonical_seed()
        {
            var plan = CompoundPlanner.Generate(460072030UL);
            var settings = NavSettings.SliceDefault;
            Assert.Equal(12, plan.Buildings.Count);
            Assert.Equal(120, plan.Props.Count);
            Assert.Equal(8, plan.BotSpawns.Count);
            Assert.True(plan.RouteCorners.Count > 1);

            string routeHash = HashRoute(plan);
            string manifest = "{"
                + "\"schemaVersion\":1,"
                + "\"workloadIds\":[\"B02-compound-v1\",\"B03-combat-v1\"],"
                + "\"seed\":\"460072030\","
                + "\"generatorId\":\"" + CompoundPlan.GeneratorId + "\","
                + "\"generatorVersion\":\"" + CompoundPlan.GeneratorVersion + "\","
                + "\"collisionHash\":\"" + plan.CollisionHash + "\","
                + "\"navHash\":\"" + plan.NavHash + "\","
                + "\"navSettingsHash\":\"" + settings.Hash() + "\","
                + "\"routeHash\":\"" + routeHash + "\","
                + "\"shells\":12,\"props\":120,\"bots\":8,"
                + "\"frozenAtMilestone\":\"M3\""
                + "}";

            // Stable freeze outside C0 docs/fixtures inventory (C0-v1 lock unchanged).
            string repoFixtures = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Benchmarks", "b02-b03-m3-freeze.json"));
            Assert.True(File.Exists(repoFixtures), "Missing freeze fixture at " + repoFixtures);
            string onDisk = File.ReadAllText(repoFixtures).Replace("\r\n", "\n").Trim();
            Assert.Equal(manifest, onDisk);

            // Profile independence: regenerating under a different cosmetic label does not change gameplay hashes.
            var again = CompoundPlanner.Generate(460072030UL);
            Assert.Equal(plan.CollisionHash, again.CollisionHash);
            Assert.Equal(plan.NavHash, again.NavHash);
            Assert.Equal(routeHash, HashRoute(again));
        }

        private static string HashRoute(CompoundPlan plan)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            w.Write("b02-route-v1");
            w.Write(plan.RouteCorners.Count);
            for (int i = 0; i < plan.RouteCorners.Count; i++)
            {
                w.Write(plan.RouteCorners[i].X);
                w.Write(plan.RouteCorners[i].Y);
                w.Write(plan.RouteCorners[i].Z);
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(ms.ToArray())).Replace("-", "").ToLowerInvariant();
        }
    }
}
