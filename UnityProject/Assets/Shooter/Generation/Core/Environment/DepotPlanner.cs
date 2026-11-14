#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// M10 T045 / T058 depot-yard grammar: linear loading docks + parallel warehouse bays
    /// (distinct from compound shells). Approved catalog seed 460072031 (ADR-035).
    /// </summary>
    public static class DepotPlanner
    {
        public const string GeneratorId = "environment.depot";
        public const string GeneratorVersion = "1.0.0";

        public static CompoundPlan Generate(ulong seed)
        {
            var boxes = new List<CollisionBox>();
            ulong nextId = 5000;
            // Parallel warehouse bays along X; open yard corridors along Z.
            float[] bayX = { -90, -54, -18, 18, 54, 90 };
            for (int i = 0; i < bayX.Length; i++)
            {
                float x = bayX[i];
                boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(x, 4f, -40f), new Float3(24f, 8f, 12f), true));
                boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(x, 4f, 40f), new Float3(24f, 8f, 12f), true));
                // Loading dock lip
                boxes.Add(new CollisionBox(nextId++, nextId, 2, new Float3(x, 0.6f, 0f), new Float3(10f, 1.2f, 4f), true));
            }

            // Covered flank corridor along +X: two parallel walls with a walkable channel (covered route).
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(108f, 2f, 0f), new Float3(2f, 4f, 180f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(116f, 2f, 0f), new Float3(2f, 4f, 180f), true));
            // Partial roof strips so mid-route samples lose open-center LOS.
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(112f, 3.2f, -40f), new Float3(6f, 0.4f, 50f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(112f, 3.2f, 40f), new Float3(6f, 0.4f, 50f), true));

            // Diagonal LOS blockers between opposite staging corners (no initial spawn shot).
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(-40f, 1.5f, -40f), new Float3(18f, 3f, 1.2f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(40f, 1.5f, 40f), new Float3(18f, 3f, 1.2f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(-20f, 1.5f, 20f), new Float3(1.2f, 3f, 24f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(20f, 1.5f, -20f), new Float3(1.2f, 3f, 24f), true));

            // Perimeter fence segments with two gate gaps at ±Z and ±X (staging exits).
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(-120f, 1.5f, 0f), new Float3(1f, 3f, 220f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(120f, 1.5f, 0f), new Float3(1f, 3f, 220f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(-60f, 1.5f, -120f), new Float3(100f, 3f, 1f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(60f, 1.5f, -120f), new Float3(100f, 3f, 1f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(-60f, 1.5f, 120f), new Float3(100f, 3f, 1f), true));
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(60f, 1.5f, 120f), new Float3(100f, 3f, 1f), true));

            float clearance = CompoundConnectivity.RequiredClearance;
            var playerSpawn = ResolveOpen(boxes, new Float3(-100f, 0f, -100f), clearance);
            var objective = ResolveOpen(boxes, new Float3(100f, 0f, 100f), clearance);

            // Ensure opposite staging anchors do not share eye LOS.
            EnsureNoLos(boxes, ref nextId, playerSpawn, objective);

            var bots = new List<Float3>();
            Float3[] candidates =
            {
                new Float3(-80, 0, -20), new Float3(-40, 0, 20), new Float3(0, 0, -60), new Float3(40, 0, 60),
                new Float3(80, 0, -20), new Float3(20, 0, 80), new Float3(-20, 0, 80), new Float3(60, 0, -80),
                new Float3(-70, 0, -70), new Float3(70, 0, 70), new Float3(-90, 0, -60), new Float3(90, 0, 60)
            };
            for (int i = 0; i < candidates.Length; i++)
                bots.Add(ResolveOpen(boxes, candidates[i], clearance));
            while (bots.Count < 8) bots.Add(objective);

            var covers = new List<CoverProxy>();
            for (int i = 0; i < bayX.Length; i++)
            {
                var center = new Float3(bayX[i], 0.6f, 8f);
                covers.Add(new CoverProxy(
                    nextId++,
                    center,
                    4f,
                    1.2f,
                    2f,
                    new Float3(center.X, center.Y + 1.2f, center.Z + 2f),
                    new Float3(center.X, center.Y + 0.6f, center.Z + 2f),
                    true));
            }

            var route = new List<Float3>();
            CompoundConnectivity.TryFindPath(boxes, playerSpawn, objective, route, CompoundConnectivity.RequiredClearance);

            using var sha = System.Security.Cryptography.SHA256.Create();
            // Identity remains seed-keyed (ADR-035 catalog seed 460072031). Topology revisions keep GeneratorVersion.
            var collisionHash = new ContentId(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(GeneratorId + ":" + GeneratorVersion + ":" + seed + ":col")));
            var navHash = new ContentId(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(GeneratorId + ":" + GeneratorVersion + ":" + seed + ":nav")));
            var height = new float[2, 2];
            return new CompoundPlan(
                seed, 1, 1, collisionHash, navHash,
                playerSpawn, objective, 8f, bots,
                Array.Empty<BuildingFootprint>(), Array.Empty<PropPlacement>(), covers, boxes, route, height);
        }

        private static void EnsureNoLos(List<CollisionBox> boxes, ref ulong nextId, Float3 a, Float3 b)
        {
            if (!MapSpawnLayout.HasSpawnLos(boxes, a, b)) return;
            float x = (a.X + b.X) * 0.5f;
            float z = (a.Z + b.Z) * 0.5f;
            float dx = b.X - a.X, dz = b.Z - a.Z;
            var size = Math.Abs(dx) > Math.Abs(dz)
                ? new Float3(0.8f, 3f, 12f)
                : new Float3(12f, 3f, 0.8f);
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(x, 1.5f, z), size, true));
        }

        private static Float3 ResolveOpen(IReadOnlyList<CollisionBox> boxes, Float3 start, float clearance)
        {
            if (!CompoundConnectivity.IsBlocked(boxes, start.X, start.Z, clearance)) return start;
            for (int r = 1; r <= 12; r++)
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
    }
}
