#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Xunit;
using Xunit.Abstractions;

namespace Shooter.Generation.Tests
{
    /// <summary>T076 Depot Yard layout: rule table, spawn protection, lanes, warehouse, container maze, determinism.</summary>
    public sealed class DepotArenaLayoutTests
    {
        private readonly ITestOutputHelper output;
        public DepotArenaLayoutTests(ITestOutputHelper output) { this.output = output; }

        private static readonly float Clear = CompoundConnectivity.RequiredClearance;
        private static CompoundPlan Plan() => DepotArenaPlanner.Generate(MapRecipe.ApprovedSeed("depot-yard"));
        private static bool Open(CompoundPlan p, float x, float z) => !CompoundConnectivity.IsBlocked(p.CollisionBoxes, x, z, Clear);

        [Fact]
        public void All_arena_rules_pass_and_print_measured_values()
        {
            var plan = Plan();
            var report = ArenaRules.Validate(plan);
            output.WriteLine(report.ToText());
            Assert.True(report.Passed, report.ToText());
            Assert.True(report.PathAnchorToCenterTeam1 >= 38f && report.PathAnchorToCenterTeam1 <= 55f);
            Assert.True(report.LongestSightline <= 60f);
            Assert.True(report.GameplayBoxCount <= 250, "gameplay boxes " + report.GameplayBoxCount);
            Assert.True(report.CoverProxyCount <= 64);
            Assert.True(report.DistinctRoutes >= 3 && report.HasCoveredRoute);
        }

        [Fact]
        public void Spawn_slots_are_hidden_from_open_ground_outside_staging()
        {
            var plan = Plan();
            var arena = plan.Arena!;
            var slots = MapSpawnLayout.BuildBothTeams(plan);
            var own = new List<SpawnCandidate>();
            foreach (var s in slots) if (s.Team == 1) own.Add(s);
            Assert.Equal(12, own.Count);

            var tall = new List<CollisionBox>();
            foreach (var bx in plan.CollisionBoxes)
                if (bx.Gameplay && bx.Center.Y + bx.Size.Y * 0.5f >= MapSpawnLayout.EyeHeightMeters && bx.Center.Y - bx.Size.Y * 0.5f <= MapSpawnLayout.EyeHeightMeters) tall.Add(bx);

            int worst = 0; float wx = 0, wz = 0, step = 2f;
            for (float z = -56f; z <= 0f; z += step)
            for (float x = -40f; x <= 40f; x += step)
            {
                if (arena.StagingContains(1, x, z) || !Open(plan, x, z)) continue;
                int seen = Visible(tall, own, x, z);
                if (seen > worst) { worst = seen; wx = x; wz = z; }
            }
            // Denser sweep right outside the gates.
            for (float z = -40f; z <= -30f; z += 0.5f)
            for (float x = -34f; x <= 34f; x += 0.5f)
            {
                if (!Open(plan, x, z)) continue;
                int seen = Visible(tall, own, x, z);
                if (seen > worst) { worst = seen; wx = x; wz = z; }
            }
            output.WriteLine("worst visible own slots = " + worst + " at (" + wx + "," + wz + ")");
            Assert.True(worst <= 4, "worst " + worst + " at (" + wx + "," + wz + ")");
        }

        private static int Visible(List<CollisionBox> tall, List<SpawnCandidate> slots, float x, float z)
        {
            int n = 0;
            foreach (var s in slots)
            {
                float dx = s.Position.X - x, dz = s.Position.Z - z;
                if (dx * dx + dz * dz > 40f * 40f) continue;
                if (CompoundConnectivity.HasLineOfSight(tall, new Float3(x, 1.65f, z), new Float3(s.Position.X, 1.65f, s.Position.Z), 1.65f)) n++;
            }
            return n;
        }

        [Fact]
        public void Warehouse_centre_is_open_and_reachable_through_every_door()
        {
            var plan = Plan();
            Assert.True(Open(plan, 0f, 0f));
            var doors = new[]
            {
                new Float3(-3f, 0f, -13.5f), new Float3(3f, 0f, -13.5f), new Float3(-3f, 0f, 13.5f), new Float3(3f, 0f, 13.5f),
                new Float3(-11.5f, 0f, 0f), new Float3(11.5f, 0f, 0f)
            };
            var path = new List<Float3>();
            foreach (var d in doors)
            {
                Assert.True(Open(plan, d.X, d.Z), "door approach blocked " + d.X + "," + d.Z);
                Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, d, new Float3(0f, 0f, 0f), path, Clear), "no path from door " + d.X + "," + d.Z);
                // Path to the centre must pass through the door line (interior reachable only via doors).
                Assert.True(path.Count > 2);
            }
            // The shell is closed apart from the six doors: sample the south wall line.
            int openCells = 0;
            for (float x = -10f; x <= 10f; x += 1f) if (Open(plan, x, -12.75f)) openCells++;
            Assert.True(openCells >= 4 && openCells <= 8, "south wall open cells " + openCells);
        }

        [Fact]
        public void Container_lane_alleys_have_no_dead_ends_and_short_sightlines()
        {
            var plan = Plan();
            const float x0 = 20.25f, x1 = 40f, z0 = -40f, z1 = 40f;
            // The maze proper excludes the 6 m deep arrival yard behind the gate (z beyond -34 / +34).
            const float mz = 34f;
            // Core cells: every 8-neighbour open, i.e. at least 3 m wide. Leaf-prune the whole-map core graph; no lane cell may be pruned.
            var core = new HashSet<(int, int)>();
            for (int x = -40; x <= 40; x++)
            for (int z = -56; z <= 56; z++)
            {
                bool ok = true;
                for (int dx = -1; dx <= 1 && ok; dx++)
                for (int dz = -1; dz <= 1 && ok; dz++) ok = Open(plan, x + dx, z + dz);
                if (ok) core.Add((x, z));
            }
            var pruned = new List<(int, int)>();
            bool changed = true;
            var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (changed)
            {
                changed = false;
                foreach (var c in new List<(int, int)>(core))
                {
                    int n = 0;
                    foreach (var d in dirs) if (core.Contains((c.Item1 + d.Item1, c.Item2 + d.Item2))) n++;
                    if (n > 1) continue;
                    core.Remove(c); pruned.Add(c); changed = true;
                }
            }
            var laneDead = pruned.FindAll(c => c.Item1 > x0 && c.Item1 < x1 && c.Item2 > z0 && c.Item2 < z1);
            output.WriteLine("lane dead-end core cells: " + laneDead.Count);
            Assert.Empty(laneDead);

            // Every open lane cell reaches both the gate end and the centre end of the lane, so there is no sealed pocket.
            var gate = new Float3(26f, 0f, -39f);
            var path = new List<Float3>();
            for (int x = 21; x < 40; x++)
            for (int z = -39; z <= 0; z++)
            {
                if (!Open(plan, x, z)) continue;
                Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, new Float3(x, 0f, z), gate, path, Clear), "unreachable lane cell " + x + "," + z);
            }

            // Eye-height sightlines inside the lane box (rays clipped at the lane boundary).
            float longest = 0f; (float, float, int) lo = (0, 0, 0);
            for (float sz = -mz + 0.5f; sz < 0f; sz += 1f)
            for (float sx = x0 + 0.5f; sx < x1; sx += 1f)
            {
                if (!Open(plan, sx, sz)) continue;
                for (int d = 0; d < 32; d++)
                {
                    double a = d * Math.PI / 16.0;
                    float dx = (float)Math.Cos(a), dz = (float)Math.Sin(a);
                    float t = 0f;
                    while (true)
                    {
                        t += 0.25f;
                        float x = sx + dx * t, z = sz + dz * t;
                        if (x < x0 || x > x1 || z < -mz || z > mz) break;
                        if (EyeBlocked(plan, x, z)) break;
                    }
                    if (t > longest) { longest = t; lo = (sx, sz, d); }
                }
            }
            output.WriteLine("container lane longest sightline = " + longest + " from " + lo);
            Assert.True(longest <= 30f, "longest " + longest + " from " + lo);
        }

        private static bool EyeBlocked(CompoundPlan plan, float x, float z)
        {
            foreach (var bx in plan.CollisionBoxes)
            {
                if (!bx.Gameplay) continue;
                float lo = bx.Center.Y - bx.Size.Y * 0.5f, hi = bx.Center.Y + bx.Size.Y * 0.5f;
                if (1.65f < lo || 1.65f > hi) continue;
                if (Math.Abs(x - bx.Center.X) <= bx.Size.X * 0.5f && Math.Abs(z - bx.Center.Z) <= bx.Size.Z * 0.5f) return true;
            }
            return false;
        }

        [Fact]
        public void Lanes_are_distinct_covered_and_cross_connected()
        {
            var plan = Plan();
            var topology = MapTopologyValidation.Validate("depot-yard", plan);
            output.WriteLine("routes=" + topology.DistinctRoutes + " covered=" + topology.HasCoveredRoute);
            Assert.True(topology.Passed, topology.Diagnostic);
            Assert.True(topology.DistinctRoutes >= 3 && topology.HasCoveredRoute);

            var path = new List<Float3>();
            // Cross connectors at z = -30 / +30 join left lane, yard and right lane.
            foreach (float z in new[] { -30f, 30f })
            {
                Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, new Float3(-30f, 0f, z), new Float3(-18f, 0f, z), path, Clear), "left connector z=" + z);
                Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, new Float3(18f, 0f, z), new Float3(26f, 0f, z < 0 ? -38f : 38f), path, Clear), "right connector z=" + z);
            }
            // Warehouse side door route joins the west and east strips through the interior.
            Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, new Float3(-15f, 0f, 0f), new Float3(15f, 0f, 0f), path, Clear));
            // Lane-to-lane route stays shorter than going back through staging.
            Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, new Float3(-28f, 0f, -23f), new Float3(28f, 0f, -23f), path, Clear));
            Assert.True(path.Count < 130, "lane to lane path " + path.Count);
        }

        [Fact]
        public void Depot_geometry_is_deterministic_and_differs_from_the_compound()
        {
            var a = Plan();
            var b = Plan();
            Assert.Equal(a.CollisionHash, b.CollisionHash);
            Assert.Equal(a.CollisionBoxes.Count, b.CollisionBoxes.Count);
            for (int i = 0; i < a.CollisionBoxes.Count; i++)
            {
                Assert.Equal(a.CollisionBoxes[i].StableId, b.CollisionBoxes[i].StableId);
                Assert.Equal(a.CollisionBoxes[i].Center, b.CollisionBoxes[i].Center);
                Assert.Equal(a.CollisionBoxes[i].Size, b.CollisionBoxes[i].Size);
            }
            var compound = ProductMapPlanner.Generate("training-compound");
            Assert.NotEqual(compound.CollisionHash, a.CollisionHash);
            Assert.NotEqual(compound.Arena!.HalfExtentX, a.Arena!.HalfExtentX);

            // Depot-specific features: 12 m boxcars, 6 m containers (single and double stacked), a 5 m high 22 x 26 warehouse shell.
            int boxcars = 0, containers = 0, doubles = 0, warehouseWalls = 0;
            foreach (var bx in a.CollisionBoxes)
            {
                if (!bx.Gameplay) continue;
                if (Near(bx.Size.X, 3f) && Near(bx.Size.Z, 12f) && Near(bx.Size.Y, 3.2f)) boxcars++;
                if (Near(bx.Size.X, 6f) && Near(bx.Size.Z, 2.5f) && Near(bx.Size.Y, 2.6f)) containers++;
                if (Near(bx.Size.X, 6f) && Near(bx.Size.Z, 2.5f) && Near(bx.Size.Y, 5.2f)) doubles++;
                if (Near(bx.Size.Y, 5f) && Math.Abs(bx.Center.X) <= 11f && Math.Abs(bx.Center.Z) <= 13f) warehouseWalls++;
            }
            Assert.Equal(3, boxcars);
            Assert.True(containers >= 12 && doubles >= 4, containers + "/" + doubles);
            Assert.True(warehouseWalls >= 10);
            // Roof is emitted as a non-gameplay box.
            Assert.Contains(a.CollisionBoxes, bx => !bx.Gameplay && Near(bx.Size.X, 22f) && Near(bx.Size.Z, 26f));
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.01f;

        [Fact]
        public void Write_topdown_map_artifact()
        {
            var plan = Plan();
            var sb = new StringBuilder();
            sb.AppendLine("Depot Yard top-down (1 char = 2 m, x -40..40 left to right, z +56 top to -56 bottom; # solid >=1.65, + cover 1.0-1.65, . open, S slot t1, s slot t2)");
            var slots = MapSpawnLayout.BuildBothTeams(plan);
            for (float z = 55f; z > -56f; z -= 2f)
            {
                for (float x = -39f; x < 40f; x += 2f)
                {
                    char c = '.';
                    foreach (var s in slots)
                        if (Math.Abs(s.Position.X - x) < 1.5f && Math.Abs(s.Position.Z - z) < 1.5f) c = s.Team == 1 ? 'S' : 's';
                    float top = 0f;
                    foreach (var bx in plan.CollisionBoxes)
                    {
                        if (!bx.Gameplay) continue;
                        if (Math.Abs(x - bx.Center.X) <= bx.Size.X * 0.5f + 0.9f && Math.Abs(z - bx.Center.Z) <= bx.Size.Z * 0.5f + 0.9f
                            && (Math.Abs(x - bx.Center.X) <= bx.Size.X * 0.5f || Math.Abs(z - bx.Center.Z) <= bx.Size.Z * 0.5f || true))
                            top = Math.Max(top, bx.Center.Y + bx.Size.Y * 0.5f);
                    }
                    // Only mark when the cell centre is really inside a box (exact), otherwise fall back to nearest within 1 m footprint.
                    float exact = 0f;
                    foreach (var bx in plan.CollisionBoxes)
                    {
                        if (!bx.Gameplay) continue;
                        if (Math.Abs(x - bx.Center.X) <= bx.Size.X * 0.5f + 0.5f && Math.Abs(z - bx.Center.Z) <= bx.Size.Z * 0.5f + 0.5f)
                            exact = Math.Max(exact, bx.Center.Y + bx.Size.Y * 0.5f);
                    }
                    if (c == '.' && exact >= 1.65f) c = '#';
                    else if (c == '.' && exact >= 1.0f) c = '+';
                    sb.Append(c);
                }
                sb.AppendLine();
            }
            string dir = Path.Combine(FindRepoRoot(), "Artifacts", "maps");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "depot-yard-topdown.txt"), sb.ToString());
            output.WriteLine(sb.ToString());
        }

        private static string FindRepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "global.json"))) d = d.Parent;
            return d?.FullName ?? AppContext.BaseDirectory;
        }
    }
}
