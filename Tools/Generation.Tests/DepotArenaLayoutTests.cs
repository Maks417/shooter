#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Xunit;
using Xunit.Abstractions;

namespace Shooter.Generation.Tests
{
    /// <summary>ADR-074 Depot Yard "rail crossing": rule table, spawn protection, train crossings, stack yard, loading shed, determinism.</summary>
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
            Assert.InRange(report.PathAnchorToCenterTeam1, ArenaRules.MinPathToCenter, ArenaRules.MaxPathToCenter);
            Assert.True(report.LongestSightline <= ArenaRules.MaxSightlineMeters);
            Assert.Equal(64f, report.PlayableSizeX);
            Assert.Equal(80f, report.PlayableSizeZ);
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
            Assert.Equal(15, own.Count);

            var tall = new List<CollisionBox>();
            foreach (var bx in plan.CollisionBoxes)
                if (bx.Gameplay && bx.Center.Y + bx.Size.Y * 0.5f >= MapSpawnLayout.EyeHeightMeters && bx.Center.Y - bx.Size.Y * 0.5f <= MapSpawnLayout.EyeHeightMeters) tall.Add(bx);

            int worst = 0; float wx = 0, wz = 0, step = 2f;
            for (float z = -40f; z <= 0f; z += step)
            for (float x = -32f; x <= 32f; x += step)
            {
                if (arena.StagingContains(1, x, z) || !Open(plan, x, z)) continue;
                int seen = Visible(tall, own, x, z);
                if (seen > worst) { worst = seen; wx = x; wz = z; }
            }
            // Denser sweep right outside the gates.
            for (float z = -29f; z <= -22f; z += 0.5f)
            for (float x = -31.5f; x <= 31.5f; x += 0.5f)
            {
                if (arena.StagingContains(1, x, z) || !Open(plan, x, z)) continue;
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
        public void Train_closes_the_middle_except_three_crossings()
        {
            var plan = Plan();
            // Walk the track line: only the two wall crossings and the centre crossing are passable.
            var open = new List<float>();
            for (float x = -31.5f; x <= 31.5f; x += 0.5f) if (Open(plan, x, 0f)) open.Add(x);
            output.WriteLine("open track cells: " + string.Join(",", open));
            Assert.NotEmpty(open);
            foreach (float x in open)
                Assert.True(x <= -28f || Math.Abs(x) <= 3f || x >= 28f, "unexpected crossing at x=" + x);
            Assert.Contains(open, x => x <= -28f);
            Assert.Contains(open, x => Math.Abs(x) <= 1f);
            Assert.Contains(open, x => x >= 28f);
            // Each crossing joins the two halves.
            var path = new List<Float3>();
            foreach (float x in new[] { -30f, 0f, 30f })
                Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, new Float3(x, 0f, -5f), new Float3(x, 0f, 5f), path, Clear), "crossing x=" + x);
            // The flatcar is low cover you can shoot over (not climb) except where the tarped load stands.
            Assert.Contains(plan.Features, f => f.Kind == ArenaFeatureKind.Flatcar && f.Size.Y < 1.65f);
            Assert.Contains(plan.Features, f => f.Kind == ArenaFeatureKind.Cargo && f.Center.Y - f.Size.Y * 0.5f > 1f);
            Assert.Equal(3, plan.Features.Count(f => f.Kind == ArenaFeatureKind.Boxcar));
        }

        [Fact]
        public void Loading_shed_is_reachable_through_every_door()
        {
            var plan = Plan();
            var inside = new Float3(20f, 0f, -15f);
            Assert.True(Open(plan, inside.X, inside.Z));
            // Approaches outside the south, north, west and east doors.
            var outside = new[] { new Float3(18f, 0f, -24f), new Float3(22f, 0f, -6f), new Float3(11f, 0f, -16f), new Float3(28.5f, 0f, -17f) };
            var path = new List<Float3>();
            foreach (var o in outside)
            {
                Assert.True(Open(plan, o.X, o.Z), "door approach blocked " + o.X + "," + o.Z);
                Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, o, inside, path, Clear), "no path from " + o.X + "," + o.Z);
            }
        }

        [Fact]
        public void Stack_yard_alleys_have_no_dead_ends_and_short_sightlines()
        {
            var plan = Plan();
            const float x0 = -32f, x1 = -12f, z0 = -27f, z1 = -2f;
            // Core cells: every 8-neighbour open (>= 3 m wide). Leaf-prune the whole-map core graph; no yard cell may be pruned.
            var core = new HashSet<(int, int)>();
            for (int x = -32; x <= 32; x++)
            for (int z = -40; z <= 40; z++)
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
            var yardDead = pruned.FindAll(c => c.Item1 > x0 && c.Item1 < x1 && c.Item2 > z0 && c.Item2 < z1);
            output.WriteLine("stack yard dead-end core cells: " + yardDead.Count + " " + string.Join(" ", yardDead));
            Assert.Empty(yardDead);

            // Every open yard cell reaches the side gate, so there is no sealed pocket.
            var gate = new Float3(-19.5f, 0f, -29f);
            var path = new List<Float3>();
            for (int x = -31; x < -12; x++)
            for (int z = -27; z <= -2; z++)
            {
                if (!Open(plan, x, z)) continue;
                Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, new Float3(x, 0f, z), gate, path, Clear), "unreachable yard cell " + x + "," + z);
            }

            // Eye-height sightlines inside the yard box (rays clipped at the yard boundary).
            float longest = 0f; (float, float, int) lo = (0, 0, 0);
            for (float sz = z0 + 0.5f; sz < z1; sz += 1f)
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
                        if (x < x0 || x > x1 || z < z0 || z > z1) break;
                        if (EyeBlocked(plan, x, z)) break;
                    }
                    if (t > longest) { longest = t; lo = (sx, sz, d); }
                }
            }
            output.WriteLine("stack yard longest sightline = " + longest + " from " + lo);
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

            // Lane-to-lane rotation on the own half stays short (no detour through staging).
            var path = new List<Float3>();
            Assert.True(CompoundConnectivity.TryFindPath(plan.CollisionBoxes, new Float3(-28f, 0f, -10f), new Float3(28f, 0f, -10f), path, Clear));
            output.WriteLine("lane to lane path corners=" + path.Count);
            Assert.True(path.Count < 110, "lane to lane path " + path.Count);
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
            Assert.NotEqual(compound.Arena!.Symmetry, a.Arena!.Symmetry);

            // Depot-specific features: boxcars on the z = 0 track, 6 m containers (single and double stacked), a 4.5 m loading shed.
            int boxcars = 0, containers = 0, doubles = 0, shedWalls = 0;
            foreach (var bx in a.CollisionBoxes)
            {
                if (!bx.Gameplay) continue;
                if (Near(bx.Size.Z, 3f) && Near(bx.Size.Y, 3.4f) && Near(bx.Center.Z, 0f)) boxcars++;
                if (Near(bx.Size.X, 6f) && Near(bx.Size.Z, 2.5f) && Near(bx.Size.Y, 2.6f)) containers++;
                if (Near(bx.Size.X, 6f) && Near(bx.Size.Z, 2.5f) && Near(bx.Size.Y, 5.2f)) doubles++;
                if (Near(bx.Size.Y, 4.5f) && bx.Center.X >= 12f && bx.Center.X <= 28f) shedWalls++;
            }
            Assert.Equal(3, boxcars);
            Assert.True(containers >= 8 && doubles >= 4, containers + "/" + doubles);
            Assert.True(shedWalls >= 10, "shed walls " + shedWalls);
            // No central warehouse: nothing stands on the centre crossing.
            Assert.DoesNotContain(a.CollisionBoxes, bx => bx.Gameplay && Math.Abs(bx.Center.X) < 3f && Math.Abs(bx.Center.Z) < 3f);
            // Roof is emitted as a non-gameplay box.
            Assert.Contains(a.CollisionBoxes, bx => !bx.Gameplay && Near(bx.Size.X, 14f) && Near(bx.Size.Z, 14f));
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.01f;

        [Fact]
        public void Write_topdown_map_artifact()
        {
            var plan = Plan();
            var sb = new StringBuilder();
            sb.AppendLine("Depot Yard top-down (1 char = 2 m, x -32..32 left to right, z +40 top to -40 bottom; # solid >=1.65, + cover 1.0-1.65, . open, S slot t1, s slot t2)");
            var slots = MapSpawnLayout.BuildBothTeams(plan);
            for (float z = 39f; z > -40f; z -= 2f)
            {
                for (float x = -31f; x < 32f; x += 2f)
                {
                    char c = '.';
                    foreach (var s in slots)
                        if (Math.Abs(s.Position.X - x) < 1.5f && Math.Abs(s.Position.Z - z) < 1.5f) c = s.Team == 1 ? 'S' : 's';
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
