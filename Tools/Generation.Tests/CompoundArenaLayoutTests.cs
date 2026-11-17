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
    /// <summary>ADR-074 garrison-square layout for <c>training-compound</c> (64 x 80 m, point-symmetric).</summary>
    public sealed class CompoundArenaLayoutTests
    {
        private readonly ITestOutputHelper output;
        public CompoundArenaLayoutTests(ITestOutputHelper output) { this.output = output; }

        private static CompoundPlan Plan() => ProductMapPlanner.Generate("training-compound");

        private static bool Los(List<CollisionBox> eyeBoxes, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            int steps = Math.Max(2, (int)MathF.Ceiling(dist / 0.25f));
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                float x = ax + dx * t, z = az + dz * t;
                for (int j = 0; j < eyeBoxes.Count; j++)
                {
                    var box = eyeBoxes[j];
                    if (Math.Abs(x - box.Center.X) <= box.Size.X * 0.5f && Math.Abs(z - box.Center.Z) <= box.Size.Z * 0.5f) return false;
                }
            }
            return true;
        }

        private static List<CollisionBox> EyeBoxes(CompoundPlan plan)
        {
            var list = new List<CollisionBox>();
            foreach (var b in plan.CollisionBoxes)
            {
                if (!b.Gameplay) continue;
                if (1.65f < b.Center.Y - b.Size.Y * 0.5f || 1.65f > b.Center.Y + b.Size.Y * 0.5f) continue;
                list.Add(b);
            }
            return list;
        }

        [Fact]
        public void Spawn_protection_budget_at_most_four_own_slots_visible_from_outside_staging()
        {
            var plan = Plan();
            var arena = plan.Arena!;
            var eye = EyeBoxes(plan);
            var slots = MapSpawnLayout.BuildBothTeams(plan);
            float clearance = CompoundConnectivity.RequiredClearance;
            int worst = 0; float wx = 0, wz = 0; int worstTeam = 0; int points = 0;
            for (int team = 1; team <= 2; team++)
            {
                var own = slots.FindAll(s => s.Team == team);
                for (float z = -39f; z <= 39f; z += 2f)
                for (float x = -31f; x <= 31f; x += 2f)
                {
                    if (arena.StagingContains(team, x, z)) continue;
                    if (CompoundConnectivity.IsBlocked(plan.CollisionBoxes, x, z, clearance)) continue;
                    points++;
                    int seen = 0;
                    foreach (var s in own)
                    {
                        float dx = s.Position.X - x, dz = s.Position.Z - z;
                        if (dx * dx + dz * dz > 40f * 40f) continue;
                        if (Los(eye, x, z, s.Position.X, s.Position.Z)) seen++;
                    }
                    if (seen > worst) { worst = seen; wx = x; wz = z; worstTeam = team; }
                }
            }
            output.WriteLine("spawn visibility worst case: " + worst + " of 15 own slots (team " + worstTeam + ") from (" + wx + "," + wz + "); " + points + " observer points");
            Assert.True(worst <= 4, "worst " + worst + " slots visible from (" + wx + "," + wz + ")");
        }

        [Fact]
        public void All_arena_rules_pass_with_measured_values()
        {
            var plan = Plan();
            var report = ArenaRules.Validate(plan);
            output.WriteLine(report.ToText());
            output.WriteLine("boxes=" + report.GameplayBoxCount + " covers=" + report.CoverProxyCount);
            Assert.True(report.Passed, report.ToText());
            Assert.True(report.GameplayBoxCount <= 250);
            Assert.True(report.CoverProxyCount <= 64);
            Assert.InRange(report.PathAnchorToCenterTeam1, ArenaRules.MinPathToCenter, ArenaRules.MaxPathToCenter);
            Assert.Equal(64f, report.PlayableSizeX);
            Assert.Equal(80f, report.PlayableSizeZ);
            var topology = MapTopologyValidation.Validate("training-compound", plan);
            Assert.True(topology.Passed, topology.Diagnostic);
        }

        private static bool Reachable(CompoundPlan plan, Float3 a, Float3 b)
            => CompoundConnectivity.TryFindPath(plan.CollisionBoxes, a, b, new List<Float3>(), CompoundConnectivity.RequiredClearance);

        [Fact]
        public void Parade_square_centre_has_no_building_and_is_reachable_from_every_entrance()
        {
            var plan = Plan();
            // No wall or building inside the square: every tall box there must be a feature object (trailer/booth).
            foreach (var b in plan.CollisionBoxes)
            {
                if (!b.Gameplay || b.Center.Y + b.Size.Y * 0.5f < 2f) continue;
                if (Math.Abs(b.Center.X) > 9f || Math.Abs(b.Center.Z) > 8f) continue;
                Assert.Fail("tall box " + b.StableId + " inside the parade square at (" + b.Center.X + "," + b.Center.Z + ")");
            }
            // The plinth is low (half cover) and centred on the origin; a flag feature stands on it.
            Assert.Contains(plan.Features, f => f.Kind == ArenaFeatureKind.Flagpole && f.Center.X == 0f && f.Center.Z == 0f);
            var square = new Float3(0f, 0f, -4f);
            var entrances = new[]
            {
                new Float3(-1.75f, 0f, -15f),          // arch between the guard huts
                new Float3(-12f, 0f, -11f),            // barracks east door
                new Float3(15.5f, 0f, -7.5f),          // office north door
                new Float3(-26f, 0f, 0f), new Float3(26f, 0f, 0f) // both lane points
            };
            foreach (var e in entrances)
            {
                Assert.True(Reachable(plan, e, square), "entrance " + e.X + "," + e.Z);
                var twin = CompoundArenaPlanner.Layout.Twin(e);
                Assert.True(Reachable(plan, twin, ArenaLayout.Rotate180(square)), "rotated entrance " + twin.X + "," + twin.Z);
            }
        }

        [Fact]
        public void Layout_is_point_symmetric_not_mirrored()
        {
            var plan = Plan();
            Assert.Equal(ArenaSymmetry.Rotate180, plan.Arena!.Symmetry);
            Assert.Equal(new Float3(0f, 0f, 34f), plan.Arena.Team2Anchor);
            int mirrorMisses = 0;
            foreach (var a in plan.CollisionBoxes)
            {
                if (!a.Gameplay) continue;
                bool rot = false, mir = false;
                foreach (var b in plan.CollisionBoxes)
                {
                    if (!b.Gameplay || Math.Abs(a.Size.X - b.Size.X) > 0.001f || Math.Abs(a.Size.Y - b.Size.Y) > 0.001f ||
                        Math.Abs(a.Size.Z - b.Size.Z) > 0.001f || Math.Abs(a.Center.Y - b.Center.Y) > 0.001f) continue;
                    if (Math.Abs(a.Center.X + b.Center.X) < 0.001f && Math.Abs(a.Center.Z + b.Center.Z) < 0.001f) rot = true;
                    if (Math.Abs(a.Center.X - b.Center.X) < 0.001f && Math.Abs(a.Center.Z + b.Center.Z) < 0.001f) mir = true;
                }
                Assert.True(rot, "no 180-degree twin for box " + a.StableId);
                if (!mir) mirrorMisses++;
            }
            // The pinwheel is genuinely not a z-mirror: many boxes have no mirror image.
            Assert.True(mirrorMisses > 20, "only " + mirrorMisses + " boxes lack a z-mirror twin");
        }

        private static bool Solid(CompoundPlan plan, float x, float z)
        {
            foreach (var b in plan.CollisionBoxes)
            {
                if (!b.Gameplay || b.Center.Y + b.Size.Y * 0.5f < 2f) continue;
                if (Math.Abs(x - b.Center.X) <= b.Size.X * 0.5f && Math.Abs(z - b.Center.Z) <= b.Size.Z * 0.5f) return true;
            }
            return false;
        }

        /// <summary>Counts distinct openings along the four wall lines of a room rectangle (walls lie on the edges).</summary>
        private static int CountExits(CompoundPlan plan, float x0, float x1, float z0, float z1, out float narrowest)
        {
            int exits = 0; float narrow = float.MaxValue;
            void Scan(bool alongX, float fixedCoord, float from, float to)
            {
                float run = 0f;
                for (float t = from; t <= to + 1e-3f; t += 0.1f)
                {
                    bool solid = alongX ? Solid(plan, t, fixedCoord) : Solid(plan, fixedCoord, t);
                    if (!solid) run += 0.1f;
                    else if (run > 0f) { if (run >= 1.0f) { exits++; narrow = Math.Min(narrow, run); } run = 0f; }
                }
                if (run >= 1.0f) { exits++; narrow = Math.Min(narrow, run); }
            }
            Scan(true, z0, x0, x1);
            Scan(true, z1, x0, x1);
            Scan(false, x0, z0 + 0.3f, z1 - 0.3f);
            Scan(false, x1, z0 + 0.3f, z1 - 0.3f);
            narrowest = narrow;
            return exits;
        }

        [Fact]
        public void Every_room_has_at_least_two_exits_and_doors_are_wide_enough()
        {
            var plan = Plan();
            // Team-1 rooms (x0, x1, z0, z1): barracks south/north rooms, HQ office, motor-pool garage.
            float[][] rooms =
            {
                new[] { -27f, -15f, -22f, -15f }, new[] { -27f, -15f, -15f, -8f },
                new[] { 11f, 20f, -19f, -9f }, new[] { -7f, 7f, -30.3f, -23.7f }
            };
            foreach (int sign in new[] { 1, -1 })
            foreach (var r in rooms)
            {
                // Rotated twin: (x, z) -> (-x, -z).
                float x0 = sign > 0 ? r[0] : -r[1], x1 = sign > 0 ? r[1] : -r[0];
                float z0 = sign > 0 ? r[2] : -r[3], z1 = sign > 0 ? r[3] : -r[2];
                int exits = CountExits(plan, x0 + 0.3f, x1 - 0.3f, z0, z1, out float narrow);
                output.WriteLine("room x[" + x0 + "," + x1 + "] z[" + z0 + "," + z1 + "] exits=" + exits + " narrowest=" + narrow);
                Assert.True(exits >= 2, "room x[" + x0 + "," + x1 + "] z[" + z0 + "," + z1 + "] has " + exits + " exits");
                Assert.True(narrow >= 2.2f, "door narrower than 2 m: " + narrow);
            }
        }

        private static float Dist(Float3 a, Float3 b) { float dx = a.X - b.X, dz = a.Z - b.Z; return MathF.Sqrt(dx * dx + dz * dz); }

        private static Float3 NearestOpen(IReadOnlyList<CollisionBox> boxes, Float3 p, float clr)
        {
            if (!CompoundConnectivity.IsBlocked(boxes, p.X, p.Z, clr)) return p;
            for (int r = 1; r <= 6; r++)
            for (int a = 0; a < 16; a++)
            {
                double ang = a * Math.PI / 8;
                var q = new Float3(p.X + (float)Math.Cos(ang) * r, 0f, p.Z + (float)Math.Sin(ang) * r);
                if (!CompoundConnectivity.IsBlocked(boxes, q.X, q.Z, clr)) return q;
            }
            return p;
        }

        private static Float3 Sample(List<Float3> path, float t)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Dist(path[i - 1], path[i]);
            float target = t * total, acc = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Dist(path[i - 1], path[i]);
                if (acc + seg >= target || i == path.Count - 1)
                {
                    float u = seg < 1e-4f ? 0f : (target - acc) / seg;
                    return new Float3(path[i - 1].X + (path[i].X - path[i - 1].X) * u, 0f, path[i - 1].Z + (path[i].Z - path[i - 1].Z) * u);
                }
                acc += seg;
            }
            return path[path.Count - 1];
        }

        private static float Similarity(List<Float3> a, List<Float3> b)
        {
            int hits = 0;
            for (int i = 0; i < 8; i++)
            {
                float t = (i + 1) / 9f;
                var pa = Sample(a, t); var pb = Sample(b, t);
                float dx = pa.X - pb.X, dz = pa.Z - pb.Z;
                if (dx * dx + dz * dz <= 18f * 18f) hits++;
            }
            return hits / 8f;
        }

        [Fact]
        public void Three_lane_routes_are_distinct_pairwise_and_one_is_covered()
        {
            var plan = Plan();
            var arena = plan.Arena!;
            var boxes = plan.CollisionBoxes;
            float clr = CompoundConnectivity.RequiredClearance;
            var routes = new List<List<Float3>>();
            foreach (var lane in arena.LanePoints)
            {
                // The centre lane point sits on the flag plinth; use the nearest open cell like MapTopologyValidation.
                var via = NearestOpen(boxes, lane, clr);
                Assert.True(Dist(via, lane) <= 3f, "lane point " + lane.X + " has no open cell nearby");
                var a = new List<Float3>(); var c = new List<Float3>();
                Assert.True(CompoundConnectivity.TryFindPath(boxes, arena.Team1Anchor, via, a, clr));
                Assert.True(CompoundConnectivity.TryFindPath(boxes, via, arena.Team2Anchor, c, clr));
                a.AddRange(c);
                routes.Add(a);
            }
            for (int i = 0; i < routes.Count; i++)
            for (int j = i + 1; j < routes.Count; j++)
            {
                float sim = Similarity(routes[i], routes[j]);
                output.WriteLine("route similarity " + i + "-" + j + " = " + sim);
                Assert.True(sim <= 0.72f, "routes " + i + "," + j + " similarity " + sim);
            }
            Assert.Contains(routes[0], p => p.X <= -24f);
            Assert.DoesNotContain(routes[0], p => p.X >= 24f);
            Assert.Contains(routes[2], p => p.X >= 24f);
            Assert.DoesNotContain(routes[2], p => p.X <= -24f);
            var topology = MapTopologyValidation.Validate("training-compound", plan);
            Assert.True(topology.DistinctRoutes >= 3 && topology.HasCoveredRoute);
        }

        [Fact]
        public void Generation_is_deterministic()
        {
            var a = Plan(); var b = Plan();
            Assert.Equal(a.CollisionBoxes.Count, b.CollisionBoxes.Count);
            for (int i = 0; i < a.CollisionBoxes.Count; i++)
            {
                Assert.Equal(a.CollisionBoxes[i].StableId, b.CollisionBoxes[i].StableId);
                Assert.Equal(a.CollisionBoxes[i].Center, b.CollisionBoxes[i].Center);
                Assert.Equal(a.CollisionBoxes[i].Size, b.CollisionBoxes[i].Size);
            }
            Assert.Equal(a.CollisionHash, b.CollisionHash);
            Assert.Equal(a.NavHash, b.NavHash);
            Assert.Equal(a.Covers.Count, b.Covers.Count);
        }

        [Fact]
        public void Dump_topdown_map_and_measurements()
        {
            var plan = Plan();
            var report = ArenaRules.Validate(plan);
            output.WriteLine(report.ToText());
            var map = TopDown(plan);
            output.WriteLine(map);
            string? root = FindRoot();
            if (root != null)
            {
                string dir = Path.Combine(root, "Artifacts", "maps");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "training-compound-topdown.txt"), map);
            }
        }

        private static string? FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "UnityProject")) && Directory.Exists(Path.Combine(dir.FullName, "Tools"))) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>1 char = 2 m. '#' wall/block (>= 2.0 m), '+' half cover, '.' open, ',' open staging, '*' team slot.</summary>
        private static string TopDown(CompoundPlan plan)
        {
            var arena = plan.Arena!;
            var slots = MapSpawnLayout.BuildBothTeams(plan);
            var sb = new StringBuilder();
            sb.AppendLine("training-compound top-down, x -32..32 left to right, z +40 (team 2) at top to -40 (team 1) at bottom, 1 char = 2 m");
            for (int row = 0; row < 40; row++)
            {
                float z1 = 40f - row * 2f, z0 = z1 - 2f;
                for (int col = 0; col < 32; col++)
                {
                    float x0 = -32f + col * 2f, x1 = x0 + 2f;
                    char c = arena.StagingContains(1, (x0 + x1) / 2, (z0 + z1) / 2) || arena.StagingContains(2, (x0 + x1) / 2, (z0 + z1) / 2) ? ',' : '.';
                    float tallest = 0f;
                    foreach (var box in plan.CollisionBoxes)
                    {
                        if (!box.Gameplay) continue;
                        float ox = Math.Min(x1, box.Center.X + box.Size.X / 2) - Math.Max(x0, box.Center.X - box.Size.X / 2);
                        float oz = Math.Min(z1, box.Center.Z + box.Size.Z / 2) - Math.Max(z0, box.Center.Z - box.Size.Z / 2);
                        if (ox <= 0f || oz <= 0f || ox * oz < 0.2f) continue;
                        tallest = Math.Max(tallest, box.Center.Y + box.Size.Y / 2);
                    }
                    if (tallest >= 2f) c = '#'; else if (tallest > 0f) c = '+';
                    foreach (var s in slots)
                        if (s.Position.X >= x0 && s.Position.X < x1 && s.Position.Z >= z0 && s.Position.Z < z1) c = '*';
                    sb.Append(c);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
