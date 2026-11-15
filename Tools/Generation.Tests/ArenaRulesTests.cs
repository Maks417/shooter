#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Xunit;
using Xunit.Abstractions;

namespace Shooter.Generation.Tests
{
    public sealed class ArenaRulesTests
    {
        private readonly ITestOutputHelper output;
        public ArenaRulesTests(ITestOutputHelper output) { this.output = output; }

        public static IEnumerable<object[]> ProductMaps()
        {
            yield return new object[] { "training-compound" };
            yield return new object[] { "depot-yard" };
        }

        [Theory]
        [MemberData(nameof(ProductMaps))]
        public void Product_maps_pass_every_arena_rule_and_topology(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            Assert.NotNull(plan.Arena);
            var sw = Stopwatch.StartNew();
            var report = ArenaRules.Validate(plan);
            output.WriteLine(mapId + " (" + sw.ElapsedMilliseconds + " ms)\n" + report.ToText());
            Assert.True(report.Passed, report.ToText());
            Assert.Equal(13, report.Rules.Count);
            Assert.True(sw.ElapsedMilliseconds < 4000, "validation took " + sw.ElapsedMilliseconds + " ms");
            var topology = MapTopologyValidation.Validate(mapId, plan);
            Assert.True(topology.Passed, topology.Diagnostic);
            Assert.True(topology.Team1StagingExits >= 3 && topology.Team2StagingExits >= 3);
        }

        [Theory]
        [MemberData(nameof(ProductMaps))]
        public void Generation_is_deterministic_and_hashes_follow_boxes(string mapId)
        {
            var a = ProductMapPlanner.Generate(mapId);
            var b = ProductMapPlanner.Generate(mapId);
            Assert.Equal(a.CollisionBoxes.Count, b.CollisionBoxes.Count);
            for (int i = 0; i < a.CollisionBoxes.Count; i++)
            {
                var x = a.CollisionBoxes[i];
                var y = b.CollisionBoxes[i];
                Assert.Equal(x.StableId, y.StableId);
                Assert.Equal(x.Center, y.Center);
                Assert.Equal(x.Size, y.Size);
            }
            Assert.Equal(a.CollisionHash, b.CollisionHash);
            Assert.Equal(a.NavHash, b.NavHash);
            Assert.NotEqual(a.CollisionHash, a.NavHash);

            // Identity is derived from the box list, not the seed alone.
            var boxes = new List<CollisionBox>(a.CollisionBoxes);
            boxes.RemoveAt(boxes.Count - 1);
            var altered = ArenaGeometry.HashGameplay(
                mapId == "depot-yard" ? DepotArenaPlanner.GeneratorId : CompoundArenaPlanner.GeneratorId,
                mapId == "depot-yard" ? DepotArenaPlanner.GeneratorVersion : CompoundArenaPlanner.GeneratorVersion,
                a.Seed, boxes, "collision");
            Assert.NotEqual(a.CollisionHash, altered);
            Assert.Equal(MapRecipe.ApprovedSeed(mapId), a.Seed);
        }

        [Theory]
        [MemberData(nameof(ProductMaps))]
        public void Stable_ids_are_sequential_from_the_map_base(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            ulong first = mapId == "depot-yard" ? DepotArenaPlanner.StableIdBase : CompoundArenaPlanner.StableIdBase;
            var seen = new HashSet<ulong>();
            foreach (var box in plan.CollisionBoxes) Assert.True(box.StableId >= first && seen.Add(box.StableId));
            foreach (var cover in plan.Covers) Assert.True(cover.StableId >= first && seen.Add(cover.StableId));
        }

        [Theory]
        [MemberData(nameof(ProductMaps))]
        public void Gameplay_boxes_are_mirror_symmetric_across_z0(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            foreach (var a in plan.CollisionBoxes)
            {
                if (!a.Gameplay) continue;
                bool twin = false;
                foreach (var b in plan.CollisionBoxes)
                {
                    if (!b.Gameplay) continue;
                    if (Math.Abs(a.Center.X - b.Center.X) < 0.001f && Math.Abs(a.Center.Y - b.Center.Y) < 0.001f &&
                        Math.Abs(a.Center.Z + b.Center.Z) < 0.001f && Math.Abs(a.Size.X - b.Size.X) < 0.001f &&
                        Math.Abs(a.Size.Z - b.Size.Z) < 0.001f && Math.Abs(a.Size.Y - b.Size.Y) < 0.001f)
                    { twin = true; break; }
                }
                Assert.True(twin, "no mirror twin for box " + a.StableId);
            }
            Assert.Equal(plan.PlayerSpawn.Z, -plan.ObjectiveCenter.Z);
            Assert.Equal(plan.PlayerSpawn.X, plan.ObjectiveCenter.X);
        }

        [Theory]
        [MemberData(nameof(ProductMaps))]
        public void Spawn_slots_sit_inside_own_staging_and_face_the_enemy(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            var arena = plan.Arena!;
            var slots = MapSpawnLayout.BuildBothTeams(plan);
            Assert.Equal(24, slots.Count);
            Assert.True(MapSpawnLayout.PositionsUnique(slots));
            int t1 = 0, t2 = 0;
            foreach (var s in slots)
            {
                Assert.True(arena.StagingContains(s.Team, s.Position.X, s.Position.Z), "slot " + s.SlotId + " outside staging");
                Assert.False(arena.StagingContains(s.Team == 1 ? 2 : 1, s.Position.X, s.Position.Z));
                Assert.False(CompoundConnectivity.IsBlocked(plan.CollisionBoxes, s.Position.X, s.Position.Z, CompoundConnectivity.RequiredClearance));
                // Yaw 0 looks along +Z: team 1 faces +Z (|yaw| < 90 deg), team 2 faces -Z.
                if (s.Team == 1) { t1++; Assert.True(Math.Cos(s.Yaw) > 0); }
                else { t2++; Assert.True(Math.Cos(s.Yaw) < 0); }
            }
            Assert.Equal(12, t1);
            Assert.Equal(12, t2);
            Assert.Equal(arena.Team1Anchor, plan.PlayerSpawn);
            Assert.Equal(arena.Team2Anchor, plan.ObjectiveCenter);
        }

        [Fact]
        public void Layout_helpers_mirror_and_contain()
        {
            var layout = CompoundArenaPlanner.Layout;
            Assert.Equal(new Float3(1f, 2f, 3f), ArenaLayout.MirrorZ(new Float3(1f, 2f, -3f)));
            Assert.True(layout.StagingContains(1, 0f, -46f));
            Assert.False(layout.StagingContains(1, 0f, 46f));
            Assert.True(layout.StagingContains(2, 0f, 46f));
            Assert.Equal(3, layout.Team2Exits.Count);
            Assert.Equal(-layout.Team1Exits[0].Z, layout.Team2Exits[0].Z);
        }

        [Fact]
        public void Legacy_plans_have_no_arena_and_keep_legacy_hashes()
        {
            var legacy = CompoundPlanner.Generate(460072030UL);
            Assert.Null(legacy.Arena);
            Assert.Null(DepotPlanner.Generate(460072031UL).Arena);
            Assert.False(ArenaRules.Validate(legacy).Passed);
        }

        // ---- negative tests ----

        private static CompoundPlan Rebuild(CompoundPlan plan, IEnumerable<CollisionBox> boxes)
            => new CompoundPlan(plan.Seed, 1, 1, plan.CollisionHash, plan.NavHash, plan.PlayerSpawn, plan.ObjectiveCenter, plan.ObjectiveRadius,
                plan.BotSpawns, plan.Buildings, plan.Props, plan.Covers, new List<CollisionBox>(boxes), plan.RouteCorners, plan.HeightSamples, plan.Arena);

        [Fact]
        public void Validator_catches_missing_perimeter_wall()
        {
            var plan = ProductMapPlanner.Generate("training-compound");
            var arena = plan.Arena!;
            var kept = new List<CollisionBox>();
            foreach (var b in plan.CollisionBoxes)
            {
                // Drop the east wall (centre beyond +X).
                if (b.Center.X > arena.HalfExtentX) continue;
                kept.Add(b);
            }
            var report = ArenaRules.Validate(Rebuild(plan, kept));
            Assert.False(report.Passed);
            Assert.False(report.Rule(ArenaRules.RulePerimeter)!.Passed);
            Assert.False(report.PerimeterClosed);
        }

        [Fact]
        public void Validator_catches_short_perimeter_wall()
        {
            var plan = ProductMapPlanner.Generate("depot-yard");
            var arena = plan.Arena!;
            var boxes = new List<CollisionBox>();
            foreach (var b in plan.CollisionBoxes)
                boxes.Add(ArenaRules.IsPerimeterBox(b, arena)
                    ? new CollisionBox(b.StableId, b.SurfaceId, b.SurfaceTag, new Float3(b.Center.X, 1.5f, b.Center.Z), new Float3(b.Size.X, 3f, b.Size.Z), true)
                    : b);
            var report = ArenaRules.Validate(Rebuild(plan, boxes));
            Assert.False(report.Rule(ArenaRules.RulePerimeter)!.Passed);
        }

        [Fact]
        public void Validator_catches_spawn_to_spawn_line_of_sight()
        {
            var plan = ProductMapPlanner.Generate("training-compound");
            // Remove everything that is not perimeter: the two staging areas now see each other.
            var arena = plan.Arena!;
            var kept = new List<CollisionBox>();
            foreach (var b in plan.CollisionBoxes) if (ArenaRules.IsPerimeterBox(b, arena)) kept.Add(b);
            var report = ArenaRules.Validate(Rebuild(plan, kept));
            Assert.False(report.Rule(ArenaRules.RuleSpawnLos)!.Passed);
            Assert.True(report.SpawnLosPairs > 0);
        }

        [Fact]
        public void Validator_catches_too_long_sightline_and_missing_cover()
        {
            var plan = ProductMapPlanner.Generate("depot-yard");
            var arena = plan.Arena!;
            var kept = new List<CollisionBox>();
            foreach (var b in plan.CollisionBoxes) if (ArenaRules.IsPerimeterBox(b, arena)) kept.Add(b);
            var report = ArenaRules.Validate(Rebuild(plan, kept));
            Assert.False(report.Rule(ArenaRules.RuleSightline)!.Passed);
            Assert.True(report.LongestSightline > ArenaRules.MaxSightlineMeters);
            Assert.False(report.Rule(ArenaRules.RuleCover)!.Passed);
            Assert.True(report.UncoveredSamples > 0);
        }

        [Fact]
        public void Validator_catches_mirror_asymmetry()
        {
            var plan = ProductMapPlanner.Generate("training-compound");
            var boxes = new List<CollisionBox>(plan.CollisionBoxes);
            boxes.Add(new CollisionBox(999999, 999999, 1, new Float3(10f, 1f, -10f), new Float3(1f, 2f, 1f), true));
            var report = ArenaRules.Validate(Rebuild(plan, boxes));
            Assert.False(report.Rule(ArenaRules.RuleMirror)!.Passed);
            Assert.Equal(1, report.MirrorMismatches);
        }

        [Fact]
        public void Validator_catches_box_ceiling()
        {
            var plan = ProductMapPlanner.Generate("training-compound");
            var boxes = new List<CollisionBox>(plan.CollisionBoxes);
            for (int i = 0; i < 401; i++)
                boxes.Add(new CollisionBox((ulong)(800000 + i), (ulong)(800000 + i), 1, new Float3(0f, 5f, 0f), new Float3(0.1f, 0.1f, 0.1f), true));
            Assert.False(ArenaRules.Validate(Rebuild(plan, boxes)).Rule(ArenaRules.RuleCeilings)!.Passed);
        }

        [Fact]
        public void Builder_mirrors_with_sequential_ids_and_gap_walls()
        {
            var b = new ArenaBoxBuilder(500);
            b.WallAlongX(-10f, -20f, 20f, 0.4f, 3f, new[] { new ArenaGap(0f, 4f) });
            Assert.Equal(4, b.Boxes.Count); // two segments plus mirrors
            for (int i = 0; i < b.Boxes.Count; i++) Assert.Equal(500UL + (ulong)i, b.Boxes[i].StableId);
            Assert.Equal(-b.Boxes[0].Center.Z, b.Boxes[1].Center.Z);
            b.AddGrounded(0f, 0f, 2f, 2f, 1f);
            Assert.Equal(5, b.Boxes.Count); // z = 0 box has no twin
            b.AddCover(5f, -10f, 2f, 1.2f, 1f, new Float3(0f, 0f, 0f));
            Assert.Equal(2, b.Covers.Count);
            // Threat at the origin is toward +Z of the -10 cover, so the defender stands at more negative z.
            Assert.True(b.Covers[0].PeekStanding.Z < b.Covers[0].Center.Z);
            Assert.True(b.Covers[1].PeekStanding.Z > b.Covers[1].Center.Z);
        }

        [Fact]
        public void Shell_leaves_door_openings()
        {
            var b = new ArenaBoxBuilder(900);
            b.Shell(0f, 20f, 16f, 12f, 0.4f, 3.5f, new[] { new ArenaDoor(ArenaSide.South, 0f, 2.4f), new ArenaDoor(ArenaSide.North, 0f, 2.4f) }, roof: true, mirror: false);
            Assert.False(CompoundConnectivity.IsBlocked(b.Boxes, 0f, 14.2f, 0.37f));
            Assert.True(CompoundConnectivity.IsBlocked(b.Boxes, 5f, 14.2f, 0.37f));
            Assert.Contains(b.Boxes, x => !x.Gameplay); // roof
        }
    }
}
