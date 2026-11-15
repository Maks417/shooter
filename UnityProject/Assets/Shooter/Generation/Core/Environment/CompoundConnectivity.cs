#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Grid pathfinding over gameplay collision for spawn/objective connectivity.
    /// Occupancy is Y-aware so thin elevated decks (catwalks/stair treads) do not seal the ground under them;
    /// layered pathfinding climbs surfaces whose tops differ by at most <see cref="MaxStepMeters"/>.
    /// Flat arenas (no walkable surfaces) use the fast XZ-only path.
    /// </summary>
    public static class CompoundConnectivity
    {
        public const float CellSize = 1f;
        public const float CapsuleRadius = 0.32f;
        public const float ClearanceMargin = 0.05f;
        public const float AgentHeightMeters = 1.8f;
        public const float MaxStepMeters = 0.25f;
        /// <summary>Gameplay boxes at or below this thickness are walkable stand surfaces (stair treads, catwalk decks).</summary>
        public const float WalkableSurfaceMaxThickness = 0.40f;
        public const float MaxStandHeightMeters = 6f;
        private const float HeightQuantize = 0.05f;

        public static float RequiredClearance => CapsuleRadius + ClearanceMargin;

        public static bool IsWalkableSurface(CollisionBox box)
            => box.Gameplay && box.Size.Y <= WalkableSurfaceMaxThickness + 1e-4f;

        public static bool HasWalkableSurfaces(IReadOnlyList<CollisionBox> boxes)
        {
            for (int i = 0; i < boxes.Count; i++)
                if (IsWalkableSurface(boxes[i])) return true;
            return false;
        }

        /// <summary>XZ blocked at ground feet (y = 0). Elevated thin decks with minY ≥ agent height do not seal the underpass.</summary>
        public static bool IsBlocked(IReadOnlyList<CollisionBox> boxes, float x, float z, float clearance)
            => IsBlockedAt(boxes, x, z, clearance, 0f, AgentHeightMeters);

        public static bool IsBlockedAt(
            IReadOnlyList<CollisionBox> boxes,
            float x,
            float z,
            float clearance,
            float feetY,
            float agentHeight)
        {
            float headY = feetY + agentHeight;
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.Gameplay) continue;
                float minY = b.Center.Y - b.Size.Y * 0.5f;
                float maxY = b.Center.Y + b.Size.Y * 0.5f;
                if (maxY <= feetY + 0.001f) continue;
                if (minY >= headY - 0.001f) continue;
                // Walkable slabs use tight XZ bounds so adjacent stair treads do not merge through capsule clearance.
                float pad = IsWalkableSurface(b) ? 0.02f : clearance;
                if (IsWalkableSurface(b) && maxY <= feetY + MaxStepMeters + 0.001f) continue;
                float hx = b.Size.X * 0.5f + pad;
                float hz = b.Size.Z * 0.5f + pad;
                if (Math.Abs(x - b.Center.X) <= hx && Math.Abs(z - b.Center.Z) <= hz) return true;
            }
            return false;
        }

        public static void CollectStandHeights(IReadOnlyList<CollisionBox> boxes, float x, float z, float clearance, List<float> heights)
        {
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            heights.Clear();
            heights.Add(0f);
            _ = clearance;
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!IsWalkableSurface(b)) continue;
                // Tight footprint: do not let capsule clearance merge neighboring treads into one column.
                float hx = b.Size.X * 0.5f + 0.02f;
                float hz = b.Size.Z * 0.5f + 0.02f;
                if (Math.Abs(x - b.Center.X) > hx || Math.Abs(z - b.Center.Z) > hz) continue;
                float top = b.Center.Y + b.Size.Y * 0.5f;
                if (top <= 0.01f || top > MaxStandHeightMeters) continue;
                bool dup = false;
                for (int h = 0; h < heights.Count; h++)
                {
                    if (Math.Abs(heights[h] - top) <= HeightQuantize * 0.5f) { dup = true; break; }
                }
                if (!dup) heights.Add(top);
            }
            heights.Sort();
        }

        public static bool TryFindPath(
            IReadOnlyList<CollisionBox> boxes,
            Float3 start,
            Float3 goal,
            List<Float3> corners,
            float clearance)
        {
            if (corners == null) throw new ArgumentNullException(nameof(corners));
            corners.Clear();
            // Ground-to-ground paths use XZ A* with Y-aware occupancy (elevated decks do not seal underpasses).
            // Layered climb is only needed when a endpoint is on a deck/stair.
            if (Math.Abs(start.Y) <= 0.01f && Math.Abs(goal.Y) <= 0.01f)
                return TryFindPathFlat(boxes, start, goal, corners, clearance);
            return TryFindPathLayered(boxes, start, goal, corners, clearance);
        }

        private static bool TryFindPathFlat(
            IReadOnlyList<CollisionBox> boxes,
            Float3 start,
            Float3 goal,
            List<Float3> corners,
            float clearance)
        {
            int min = (int)Math.Floor(-CompoundPlan.MapHalfExtent / CellSize);
            int max = (int)Math.Floor((CompoundPlan.MapHalfExtent - CellSize * 0.5f) / CellSize);
            int ToCell(float v) => (int)Math.Round(v / CellSize);
            float ToWorld(int c) => c * CellSize;

            int sx = Math.Clamp(ToCell(start.X), min, max);
            int sz = Math.Clamp(ToCell(start.Z), min, max);
            int gx = Math.Clamp(ToCell(goal.X), min, max);
            int gz = Math.Clamp(ToCell(goal.Z), min, max);

            if (IsBlocked(boxes, ToWorld(sx), ToWorld(sz), clearance) || IsBlocked(boxes, ToWorld(gx), ToWorld(gz), clearance))
                return false;

            var open = new List<(int x, int z, int f)>();
            var came = new Dictionary<(int, int), (int, int)>();
            var gScore = new Dictionary<(int, int), int>();
            var startKey = (sx, sz);
            gScore[startKey] = 0;
            open.Add((sx, sz, Heuristic2(sx, sz, gx, gz)));

            (int dx, int dz)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };
            bool found = false;
            int guard = 0;
            while (open.Count > 0)
            {
                if (++guard > 2_000_000) return false;
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (open[i].f < open[best].f) best = i;
                var current = open[best];
                open.RemoveAt(best);
                var currentKey = (current.x, current.z);
                if (current.x == gx && current.z == gz) { found = true; break; }
                if (!gScore.TryGetValue(currentKey, out int baseCost)) continue;
                for (int i = 0; i < dirs.Length; i++)
                {
                    int nx = current.x + dirs[i].dx;
                    int nz = current.z + dirs[i].dz;
                    if (nx < min || nx > max || nz < min || nz > max) continue;
                    float wx = ToWorld(nx), wz = ToWorld(nz);
                    if (IsBlocked(boxes, wx, wz, clearance)) continue;
                    var key = (nx, nz);
                    int tentative = baseCost + 1;
                    if (gScore.TryGetValue(key, out int prior) && tentative >= prior) continue;
                    came[key] = currentKey;
                    gScore[key] = tentative;
                    open.Add((nx, nz, tentative + Heuristic2(nx, nz, gx, gz)));
                }
            }
            if (!found) return false;

            var path = new List<(int x, int z)>();
            var node = (gx, gz);
            path.Add(node);
            while (!(node.Item1 == sx && node.Item2 == sz))
            {
                if (!came.TryGetValue(node, out node)) return false;
                path.Add(node);
            }
            path.Reverse();
            for (int i = 0; i < path.Count; i++)
                corners.Add(new Float3(ToWorld(path[i].x), start.Y, ToWorld(path[i].z)));
            return true;
        }

        private static bool TryFindPathLayered(
            IReadOnlyList<CollisionBox> boxes,
            Float3 start,
            Float3 goal,
            List<Float3> corners,
            float clearance)
        {
            int min = (int)Math.Floor(-CompoundPlan.MapHalfExtent / CellSize);
            int max = (int)Math.Floor((CompoundPlan.MapHalfExtent - CellSize * 0.5f) / CellSize);
            int ToCell(float v) => (int)Math.Round(v / CellSize);
            float ToWorld(int c) => c * CellSize;
            int Quantize(float y) => (int)Math.Round(y / HeightQuantize);

            int sx = Math.Clamp(ToCell(start.X), min, max);
            int sz = Math.Clamp(ToCell(start.Z), min, max);
            int gx = Math.Clamp(ToCell(goal.X), min, max);
            int gz = Math.Clamp(ToCell(goal.Z), min, max);
            float startY = Math.Max(0f, start.Y);
            float goalY = Math.Max(0f, goal.Y);

            var heightScratch = new List<float>(8);
            CollectStandHeights(boxes, ToWorld(sx), ToWorld(sz), clearance, heightScratch);
            float resolvedStart = NearestHeight(heightScratch, startY);
            CollectStandHeights(boxes, ToWorld(gx), ToWorld(gz), clearance, heightScratch);
            float resolvedGoal = NearestHeight(heightScratch, goalY);

            if (IsBlockedAt(boxes, ToWorld(sx), ToWorld(sz), clearance, resolvedStart, AgentHeightMeters)) return false;
            if (IsBlockedAt(boxes, ToWorld(gx), ToWorld(gz), clearance, resolvedGoal, AgentHeightMeters)) return false;

            int startQ = Quantize(resolvedStart);
            int goalQ = Quantize(resolvedGoal);
            var open = new List<(int x, int z, int q, int f)>();
            var came = new Dictionary<(int, int, int), (int, int, int)>();
            var gScore = new Dictionary<(int, int, int), int>();
            var startKey = (sx, sz, startQ);
            gScore[startKey] = 0;
            open.Add((sx, sz, startQ, Heuristic3(sx, sz, startQ, gx, gz, goalQ)));

            (int dx, int dz)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };
            bool found = false;
            int guard = 0;
            while (open.Count > 0)
            {
                if (++guard > 4_000_000) return false;
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (open[i].f < open[best].f) best = i;
                var current = open[best];
                open.RemoveAt(best);
                var currentKey = (current.x, current.z, current.q);
                if (current.x == gx && current.z == gz && current.q == goalQ) { found = true; break; }
                if (!gScore.TryGetValue(currentKey, out int baseCost)) continue;
                float currentY = current.q * HeightQuantize;
                for (int i = 0; i < dirs.Length; i++)
                {
                    int nx = current.x + dirs[i].dx;
                    int nz = current.z + dirs[i].dz;
                    if (nx < min || nx > max || nz < min || nz > max) continue;
                    float wx = ToWorld(nx), wz = ToWorld(nz);
                    CollectStandHeights(boxes, wx, wz, clearance, heightScratch);
                    for (int h = 0; h < heightScratch.Count; h++)
                    {
                        float ny = heightScratch[h];
                        if (Math.Abs(ny - currentY) > MaxStepMeters + 1e-3f) continue;
                        if (IsBlockedAt(boxes, wx, wz, clearance, ny, AgentHeightMeters)) continue;
                        int nq = Quantize(ny);
                        var key = (nx, nz, nq);
                        int tentative = baseCost + 1 + (nq == current.q ? 0 : 1);
                        if (gScore.TryGetValue(key, out int prior) && tentative >= prior) continue;
                        came[key] = currentKey;
                        gScore[key] = tentative;
                        open.Add((nx, nz, nq, tentative + Heuristic3(nx, nz, nq, gx, gz, goalQ)));
                    }
                }
            }
            if (!found) return false;

            var path = new List<(int x, int z, int q)>();
            var node = (gx, gz, goalQ);
            path.Add(node);
            while (!(node.Item1 == sx && node.Item2 == sz && node.Item3 == startQ))
            {
                if (!came.TryGetValue(node, out node)) return false;
                path.Add(node);
            }
            path.Reverse();
            for (int i = 0; i < path.Count; i++)
                corners.Add(new Float3(ToWorld(path[i].x), path[i].q * HeightQuantize, ToWorld(path[i].z)));
            return true;
        }

        private static float NearestHeight(List<float> heights, float prefer)
        {
            float best = heights[0];
            float bestD = Math.Abs(best - prefer);
            for (int i = 1; i < heights.Count; i++)
            {
                float d = Math.Abs(heights[i] - prefer);
                if (d < bestD) { bestD = d; best = heights[i]; }
            }
            return best;
        }

        private static int Heuristic2(int x, int z, int gx, int gz)
            => Math.Abs(x - gx) + Math.Abs(z - gz);

        private static int Heuristic3(int x, int z, int q, int gx, int gz, int gq)
            => Math.Abs(x - gx) + Math.Abs(z - gz) + Math.Abs(q - gq);

        public static bool HasLineOfSight(IReadOnlyList<CollisionBox> boxes, Float3 a, Float3 b, float eyeY)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            if (dist < 1e-4f) return true;
            int steps = Math.Max(2, (int)MathF.Ceiling(dist / 0.25f));
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                float x = a.X + dx * t;
                float z = a.Z + dz * t;
                for (int j = 0; j < boxes.Count; j++)
                {
                    var box = boxes[j];
                    if (!box.Gameplay) continue;
                    float minY = box.Center.Y - box.Size.Y * 0.5f;
                    float maxY = box.Center.Y + box.Size.Y * 0.5f;
                    if (eyeY < minY || eyeY > maxY) continue;
                    float hx = box.Size.X * 0.5f, hz = box.Size.Z * 0.5f;
                    if (Math.Abs(x - box.Center.X) <= hx && Math.Abs(z - box.Center.Z) <= hz) return false;
                }
            }
            return true;
        }
    }
}
