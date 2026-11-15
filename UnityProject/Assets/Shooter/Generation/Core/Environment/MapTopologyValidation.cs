#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>FIX07 / MAP02 topology checks: staging exits, three routes (one covered), spawn LOS, standing clearance.</summary>
    public sealed class MapTopologyReport
    {
        public string MapId { get; }
        public ulong Seed { get; }
        public bool Passed { get; }
        public int Team1Slots { get; }
        public int Team2Slots { get; }
        public bool UniqueSlots { get; }
        public bool StandingClearance { get; }
        public bool InitialSpawnLosBlocked { get; }
        public int Team1StagingExits { get; }
        public int Team2StagingExits { get; }
        public int DistinctRoutes { get; }
        public bool HasCoveredRoute { get; }
        public string Diagnostic { get; }

        public MapTopologyReport(
            string mapId,
            ulong seed,
            bool passed,
            int team1Slots,
            int team2Slots,
            bool uniqueSlots,
            bool standingClearance,
            bool initialSpawnLosBlocked,
            int team1StagingExits,
            int team2StagingExits,
            int distinctRoutes,
            bool hasCoveredRoute,
            string diagnostic)
        {
            MapId = mapId;
            Seed = seed;
            Passed = passed;
            Team1Slots = team1Slots;
            Team2Slots = team2Slots;
            UniqueSlots = uniqueSlots;
            StandingClearance = standingClearance;
            InitialSpawnLosBlocked = initialSpawnLosBlocked;
            Team1StagingExits = team1StagingExits;
            Team2StagingExits = team2StagingExits;
            DistinctRoutes = distinctRoutes;
            HasCoveredRoute = hasCoveredRoute;
            Diagnostic = diagnostic ?? string.Empty;
        }
    }

    public static class MapTopologyValidation
    {
        public const float StagingHalfExtentMeters = 18f;

        public static MapTopologyReport Validate(string mapId, CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var slots = MapSpawnLayout.BuildBothTeams(plan);
            int t1 = 0, t2 = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Team == 1) t1++;
                else t2++;
            }

            bool unique = MapSpawnLayout.PositionsUnique(slots);
            bool clearance = true;
            float required = CompoundConnectivity.RequiredClearance;
            for (int i = 0; i < slots.Count; i++)
            {
                if (CompoundConnectivity.IsBlocked(plan.CollisionBoxes, slots[i].Position.X, slots[i].Position.Z, required))
                {
                    clearance = false;
                    break;
                }
            }

            bool losBlocked = true;
            for (int i = 0; i < slots.Count && losBlocked; i++)
            {
                if (slots[i].Team != 1) continue;
                for (int j = 0; j < slots.Count; j++)
                {
                    if (slots[j].Team != 2) continue;
                    if (MapSpawnLayout.HasSpawnLos(plan.CollisionBoxes, slots[i].Position, slots[j].Position))
                    {
                        losBlocked = false;
                        break;
                    }
                }
            }

            int exits1 = plan.Arena != null ? CountArenaExits(plan, plan.Arena, 1) : CountStagingExits(plan, plan.PlayerSpawn);
            int exits2 = plan.Arena != null ? CountArenaExits(plan, plan.Arena, 2) : CountStagingExits(plan, plan.ObjectiveCenter);
            int requiredExits = plan.Arena != null ? 3 : 2;
            var routes = BuildDistinctRoutes(plan);
            bool covered = false;
            for (int i = 0; i < routes.Count; i++)
            {
                if (IsCoveredRoute(plan, routes[i]))
                {
                    covered = true;
                    break;
                }
            }

            bool passed =
                t1 >= MapSpawnLayout.SlotsPerTeam &&
                t2 >= MapSpawnLayout.SlotsPerTeam &&
                unique &&
                clearance &&
                losBlocked &&
                exits1 >= requiredExits &&
                exits2 >= requiredExits &&
                routes.Count >= 3 &&
                covered;

            string diag = passed
                ? "ok"
                : "t1=" + t1 + " t2=" + t2 +
                  " unique=" + unique +
                  " clearance=" + clearance +
                  " losBlocked=" + losBlocked +
                  " exits1=" + exits1.ToString(CultureInfo.InvariantCulture) +
                  " exits2=" + exits2.ToString(CultureInfo.InvariantCulture) +
                  " routes=" + routes.Count.ToString(CultureInfo.InvariantCulture) +
                  " covered=" + covered;

            return new MapTopologyReport(
                mapId,
                plan.Seed,
                passed,
                t1,
                t2,
                unique,
                clearance,
                losBlocked,
                exits1,
                exits2,
                routes.Count,
                covered,
                diag);
        }

        /// <summary>Arena plans: an exit counts when the anchor reaches the exit point and the exit reaches the (open) map centre.</summary>
        private static int CountArenaExits(CompoundPlan plan, ArenaLayout arena, int team)
        {
            float clearance = CompoundConnectivity.RequiredClearance;
            var anchor = arena.Anchor(team);
            var combat = ResolveOpen(plan.CollisionBoxes, arena.Center, clearance);
            var exits = arena.Exits(team);
            int count = 0;
            var path = new List<Float3>();
            for (int i = 0; i < exits.Count; i++)
            {
                var gate = exits[i];
                if (CompoundConnectivity.IsBlocked(plan.CollisionBoxes, gate.X, gate.Z, clearance)) continue;
                if (!CompoundConnectivity.TryFindPath(plan.CollisionBoxes, anchor, gate, path, clearance)) continue;
                if (!CompoundConnectivity.TryFindPath(plan.CollisionBoxes, gate, combat, path, clearance)) continue;
                count++;
            }
            return count;
        }

        private static int CountStagingExits(CompoundPlan plan, Float3 stagingCenter)
        {
            // Two exits = two navigable staging-boundary cells that reach the map center combat belt.
            Float3 combat = new Float3(0f, 0f, 0f);
            float half = StagingHalfExtentMeters;
            Float3[] gates =
            {
                new Float3(stagingCenter.X + half, 0f, stagingCenter.Z),
                new Float3(stagingCenter.X - half, 0f, stagingCenter.Z),
                new Float3(stagingCenter.X, 0f, stagingCenter.Z + half),
                new Float3(stagingCenter.X, 0f, stagingCenter.Z - half)
            };
            int exits = 0;
            var path = new List<Float3>();
            float clearance = CompoundConnectivity.RequiredClearance;
            for (int i = 0; i < gates.Length; i++)
            {
                var gate = ResolveOpen(plan.CollisionBoxes, gates[i], clearance);
                if (CompoundConnectivity.IsBlocked(plan.CollisionBoxes, gate.X, gate.Z, clearance)) continue;
                if (!CompoundConnectivity.TryFindPath(plan.CollisionBoxes, stagingCenter, gate, path, clearance)) continue;
                if (!CompoundConnectivity.TryFindPath(plan.CollisionBoxes, gate, combat, path, clearance)) continue;
                exits++;
            }
            return exits;
        }

        private static List<List<Float3>> BuildDistinctRoutes(CompoundPlan plan)
        {
            float clearance = CompoundConnectivity.RequiredClearance;
            Float3 start = plan.PlayerSpawn;
            Float3 goal = plan.ObjectiveCenter;
            Float3[] via =
            {
                new Float3(0f, 0f, 0f),
                new Float3(-96f, 0f, 0f),
                new Float3(96f, 0f, 0f),
                new Float3(0f, 0f, -96f),
                new Float3(0f, 0f, 96f),
                new Float3(-80f, 0f, 80f),
                new Float3(80f, 0f, -80f)
            };

            if (plan.Arena != null)
            {
                var lanes = plan.Arena.LanePoints;
                via = new Float3[lanes.Count];
                for (int i = 0; i < lanes.Count; i++) via[i] = lanes[i];
            }

            var routes = new List<List<Float3>>();
            var scratchA = new List<Float3>();
            var scratchB = new List<Float3>();
            for (int i = 0; i < via.Length && routes.Count < 4; i++)
            {
                var waypoint = ResolveOpen(plan.CollisionBoxes, via[i], clearance);
                if (!CompoundConnectivity.TryFindPath(plan.CollisionBoxes, start, waypoint, scratchA, clearance)) continue;
                if (!CompoundConnectivity.TryFindPath(plan.CollisionBoxes, waypoint, goal, scratchB, clearance)) continue;
                var merged = new List<Float3>(scratchA.Count + scratchB.Count);
                merged.AddRange(scratchA);
                if (scratchB.Count > 0) merged.AddRange(scratchB);
                if (!IsDistinctRoute(routes, merged)) continue;
                routes.Add(merged);
            }

            // Direct path as fallback candidate.
            if (CompoundConnectivity.TryFindPath(plan.CollisionBoxes, start, goal, scratchA, clearance) &&
                IsDistinctRoute(routes, scratchA))
            {
                routes.Add(new List<Float3>(scratchA));
            }
            return routes;
        }

        private static bool IsDistinctRoute(List<List<Float3>> existing, List<Float3> candidate)
        {
            if (candidate.Count < 2) return false;
            for (int i = 0; i < existing.Count; i++)
            {
                if (RouteSimilarity(existing[i], candidate) > 0.72f) return false;
            }
            return true;
        }

        private static float RouteSimilarity(List<Float3> a, List<Float3> b)
        {
            int samples = 8;
            int hits = 0;
            for (int i = 0; i < samples; i++)
            {
                float t = (i + 1) / (float)(samples + 1);
                var pa = SamplePolyline(a, t);
                var pb = SamplePolyline(b, t);
                float dx = pa.X - pb.X, dz = pa.Z - pb.Z;
                if (dx * dx + dz * dz <= 18f * 18f) hits++;
            }
            return hits / (float)samples;
        }

        private static Float3 SamplePolyline(List<Float3> path, float t)
        {
            if (path.Count == 1) return path[0];
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
                total += Dist(path[i - 1], path[i]);
            if (total < 1e-3f) return path[0];
            float target = t * total;
            float acc = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Dist(path[i - 1], path[i]);
                if (acc + seg >= target || i == path.Count - 1)
                {
                    float u = seg < 1e-4f ? 0f : (target - acc) / seg;
                    return new Float3(
                        path[i - 1].X + (path[i].X - path[i - 1].X) * u,
                        path[i - 1].Y,
                        path[i - 1].Z + (path[i].Z - path[i - 1].Z) * u);
                }
                acc += seg;
            }
            return path[path.Count - 1];
        }

        private static bool IsCoveredRoute(CompoundPlan plan, List<Float3> route)
        {
            // Covered = majority of mid-route samples lack open skyline LOS to the map center combat belt.
            if (route.Count < 3) return false;
            int covered = 0, samples = 0;
            var openRef = new Float3(0f, 0f, 0f);
            for (int i = 1; i < route.Count - 1; i++)
            {
                samples++;
                if (!MapSpawnLayout.HasSpawnLos(plan.CollisionBoxes, route[i], openRef)) covered++;
            }
            return samples > 0 && covered * 2 >= samples;
        }

        private static Float3 ResolveOpen(IReadOnlyList<CollisionBox> boxes, Float3 start, float clearance)
        {
            if (!CompoundConnectivity.IsBlocked(boxes, start.X, start.Z, clearance)) return start;
            for (int r = 1; r <= 16; r++)
            {
                for (int a = 0; a < 8; a++)
                {
                    double ang = a * Math.PI / 4;
                    var p = new Float3(start.X + (float)Math.Cos(ang) * r, 0f, start.Z + (float)Math.Sin(ang) * r);
                    if (!CompoundConnectivity.IsBlocked(boxes, p.X, p.Z, clearance)) return p;
                }
            }
            return start;
        }

        private static float Dist(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }
    }
}
