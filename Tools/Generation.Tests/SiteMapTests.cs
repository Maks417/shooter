#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Shooter.Generation.Environment;
using Xunit;
using Xunit.Abstractions;

namespace Shooter.Generation.Tests
{
    /// <summary>ADR-109 site-planned product maps: arena rules, site rules, determinism and the top-down review render.</summary>
    public sealed class SiteMapTests
    {
        private readonly ITestOutputHelper output;
        public SiteMapTests(ITestOutputHelper output) { this.output = output; }

        public static IEnumerable<object[]> SiteMaps()
        {
            yield return new object[] { "training-compound" };
            yield return new object[] { "depot-yard" };
            yield return new object[] { "oil-depot" };
        }

        internal static CompoundPlan Plan(string mapId) => mapId switch
        {
            "training-compound" => OldTownSitePlanner.Generate(460072030),
            "depot-yard" => HarbourSitePlanner.Generate(460072031),
            "oil-depot" => OilDepotSitePlanner.Generate(460072032),
            _ => throw new ArgumentOutOfRangeException(nameof(mapId))
        };

        [Theory]
        [MemberData(nameof(SiteMaps))]
        public void Site_maps_pass_arena_and_site_rules(string mapId)
        {
            var plan = Plan(mapId);
            Assert.NotNull(plan.Site);
            var sw = Stopwatch.StartNew();
            var arena = ArenaRules.Validate(plan);
            var site = SiteRules.Validate(plan);
            long ms = sw.ElapsedMilliseconds;
            List<SpawnCandidate>? slots = null;
            try { slots = MapSpawnLayout.BuildBothTeams(plan); } catch (InvalidOperationException) { }
            WriteReview(mapId, plan, slots, site, arena);
            output.WriteLine(mapId + " (" + ms + " ms)\n" + site.ToText() + arena.ToText());
            Assert.True(site.Passed, site.ToText());
            Assert.True(arena.Passed, arena.ToText());
            var topology = MapTopologyValidation.Validate(mapId, plan);
            Assert.True(topology.Passed, topology.Diagnostic);
        }

        [Theory]
        [MemberData(nameof(SiteMaps))]
        public void Site_generation_is_deterministic(string mapId)
        {
            var a = Plan(mapId);
            var b = Plan(mapId);
            Assert.Equal(a.CollisionBoxes.Count, b.CollisionBoxes.Count);
            for (int i = 0; i < a.CollisionBoxes.Count; i++)
            {
                Assert.Equal(a.CollisionBoxes[i].StableId, b.CollisionBoxes[i].StableId);
                Assert.Equal(a.CollisionBoxes[i].Center, b.CollisionBoxes[i].Center);
                Assert.Equal(a.CollisionBoxes[i].Size, b.CollisionBoxes[i].Size);
            }
            Assert.Equal(a.CollisionHash, b.CollisionHash);
        }

        private static void WriteReview(string mapId, CompoundPlan plan, IReadOnlyList<SpawnCandidate>? slots, SiteRulesReport site, ArenaRulesReport arena)
        {
            string? root = FindRoot();
            if (root == null) return;
            string dir = Path.Combine(root, "Artifacts", "maps");
            Directory.CreateDirectory(dir);
            string title = plan.Site!.Name + " (" + mapId + ")";
            File.WriteAllText(Path.Combine(dir, mapId + "-site.svg"), SiteMapSvg.Render(plan, title, slots, site, arena));
            File.WriteAllText(Path.Combine(dir, mapId + "-site.txt"), site.ToText() + arena.ToText());
        }

        private static string? FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "toolchain-lock.json"))) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
