#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Grid pathfinding over gameplay collision for spawn/objective connectivity.</summary>
    public static class CompoundConnectivity
    {
        public const float CellSize = 1f;
        public const float CapsuleRadius = 0.32f;
        public const float ClearanceMargin = 0.05f;
        public static float RequiredClearance => CapsuleRadius + ClearanceMargin;

        public static bool IsBlocked(IReadOnlyList<CollisionBox> boxes, float x, float z, float clearance)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.Gameplay) continue;
                float hx = b.Size.X * 0.5f + clearance;
                float hz = b.Size.Z * 0.5f + clearance;
                if (Math.Abs(x - b.Center.X) <= hx && Math.Abs(z - b.Center.Z) <= hz) return true;
            }
            return false;
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
            int min = (int)Math.Floor(-CompoundPlan.MapHalfExtent / CellSize);
            int max = (int)Math.Floor((CompoundPlan.MapHalfExtent - CellSize * 0.5f) / CellSize);
            int ToCell(float v) => (int)Math.Round(v / CellSize);
            float ToWorld(int c) => c * CellSize;

            int sx = ToCell(start.X), sz = ToCell(start.Z);
            int gx = ToCell(goal.X), gz = ToCell(goal.Z);
            sx = Math.Clamp(sx, min, max); sz = Math.Clamp(sz, min, max);
            gx = Math.Clamp(gx, min, max); gz = Math.Clamp(gz, min, max);

            if (IsBlocked(boxes, ToWorld(sx), ToWorld(sz), clearance) || IsBlocked(boxes, ToWorld(gx), ToWorld(gz), clearance))
                return false;

            var open = new List<(int x, int z, int f)>();
            var came = new Dictionary<(int, int), (int, int)>();
            var gScore = new Dictionary<(int, int), int>();
            var startKey = (sx, sz);
            gScore[startKey] = 0;
            open.Add((sx, sz, Heuristic(sx, sz, gx, gz)));

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
                    open.Add((nx, nz, tentative + Heuristic(nx, nz, gx, gz)));
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

        private static int Heuristic(int x, int z, int gx, int gz)
            => Math.Abs(x - gx) + Math.Abs(z - gz);

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
