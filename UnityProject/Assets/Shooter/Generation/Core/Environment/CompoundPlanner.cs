#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>W01 compound planner: 8×8×32 m chunks, B02 shells/props, gameplay cover, four-seed connectivity.</summary>
    public static class CompoundPlanner
    {
        public static readonly ulong[] CanonicalSeeds =
        {
            460072030UL,
            1UL,
            42UL,
            18446744073709551615UL
        };

        private static readonly Float3[] BotCandidates =
        {
            new Float3(-48, 0, -32),
            new Float3(0, 0, -48),
            new Float3(48, 0, -16),
            new Float3(-32, 0, 32),
            new Float3(16, 0, 32),
            new Float3(64, 0, 48),
            new Float3(48, 0, 96),
            new Float3(96, 0, 64)
        };

        private static readonly float[] ShellCentersX = { -72, -24, 24, 72 };
        private static readonly float[] ShellCentersZ = { -64, 0, 64 };

        public static CompoundPlan Generate(ulong seed)
        {
            var buildings = BuildShells();
            var props = BuildProps(buildings);
            var boxes = new List<CollisionBox>();
            ulong nextId = 1000;
            // Ground plane proxy (thin) is not a motor blocker for grid pathing; walls/props/cover are.
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                // Four walls with two opposing 2 m doors on ±Z.
                float hx = b.SizeX * 0.5f, hz = b.SizeZ * 0.5f;
                float wallT = 0.3f, doorHalf = 1f;
                float segment = hx - doorHalf; // 5 m each side for 12 m footprint
                float leftX = b.Center.X - doorHalf - segment * 0.5f;
                float rightX = b.Center.X + doorHalf + segment * 0.5f;
                // North (+Z) and south (-Z) walls split around door gap [cx-1, cx+1]
                AddWall(boxes, ref nextId, leftX, b.Height * 0.5f, b.Center.Z + hz, segment, b.Height, wallT);
                AddWall(boxes, ref nextId, rightX, b.Height * 0.5f, b.Center.Z + hz, segment, b.Height, wallT);
                AddWall(boxes, ref nextId, leftX, b.Height * 0.5f, b.Center.Z - hz, segment, b.Height, wallT);
                AddWall(boxes, ref nextId, rightX, b.Height * 0.5f, b.Center.Z - hz, segment, b.Height, wallT);
                // East / West solid walls (inset so they do not seal the door corners)
                AddWall(boxes, ref nextId, b.Center.X + hx, b.Height * 0.5f, b.Center.Z, wallT, b.Height, b.SizeZ - wallT);
                AddWall(boxes, ref nextId, b.Center.X - hx, b.Height * 0.5f, b.Center.Z, wallT, b.Height, b.SizeZ - wallT);
                if (b.HasRoof)
                {
                    ulong sid = nextId++;
                    boxes.Add(new CollisionBox(sid, nextId++, 1, new Float3(b.Center.X, b.Height + 0.15f, b.Center.Z),
                        new Float3(b.SizeX, 0.3f, b.SizeZ), true));
                }
            }

            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                boxes.Add(new CollisionBox(nextId++, nextId, p.SurfaceTag, p.Center,
                    new Float3(p.SizeX, p.SizeY, p.SizeZ), true));
            }

            var playerSpawn = new Float3(-104, TerrainHeight.Sample(seed, -104, -104), -104);
            var objective = new Float3(96, TerrainHeight.Sample(seed, 96, 96), 96);
            var covers = new List<CoverProxy>();
            EnsureSpawnCover(boxes, covers, ref nextId, playerSpawn, BotCandidates);

            var botSpawns = new List<Float3>(8);
            for (int i = 0; i < BotCandidates.Length; i++)
            {
                if (!TryResolveSpawn(boxes, seed, BotCandidates[i], out Float3 resolved))
                    throw new InvalidOperationException("Seed rejected: bot spawn " + i.ToString(CultureInfo.InvariantCulture) + " unreachable.");
                botSpawns.Add(resolved);
                float eye = playerSpawn.Y + 1.65f;
                var botEye = new Float3(resolved.X, resolved.Y + 1.65f, resolved.Z);
                var playerEye = new Float3(playerSpawn.X, eye, playerSpawn.Z);
                if (CompoundConnectivity.HasLineOfSight(boxes, playerEye, botEye, eye))
                    throw new InvalidOperationException("Seed rejected: bot " + i + " has spawn LOS.");
            }

            if (!TryResolveSpawn(boxes, seed, playerSpawn, out Float3 safePlayer))
                throw new InvalidOperationException("Seed rejected: player spawn invalid.");
            playerSpawn = safePlayer;

            var route = new List<Float3>();
            float clearance = CompoundConnectivity.RequiredClearance;
            if (!CompoundConnectivity.TryFindPath(boxes, playerSpawn, objective, route, clearance))
                throw new InvalidOperationException("Seed rejected: no spawn→objective route.");
            for (int i = 0; i < botSpawns.Count; i++)
            {
                var tmp = new List<Float3>();
                if (!CompoundConnectivity.TryFindPath(boxes, playerSpawn, botSpawns[i], tmp, clearance))
                    throw new InvalidOperationException("Seed rejected: bot " + i + " unreachable from spawn.");
            }

            // Reserved 4 m B03 corridor around perimeter waypoints.
            Float3[] perimeter =
            {
                new Float3(-104, 0, -104), new Float3(104, 0, -104),
                new Float3(104, 0, 104), new Float3(-104, 0, 104)
            };
            for (int i = 0; i < perimeter.Length; i++)
            {
                var a = perimeter[i];
                var b = perimeter[(i + 1) % perimeter.Length];
                EnsureCorridorClear(boxes, a, b, 2f);
            }

            float[,] heights = TerrainHeight.BuildGrid(seed, 257);
            ContentId collisionHash = HashGameplay(seed, boxes, "collision");
            ContentId navHash = HashGameplay(seed, boxes, "navigation");
            // Cosmetic profile must not alter gameplay hashes (validated in tests via same boxes).
            return new CompoundPlan(
                seed,
                collisionRevision: 1,
                navRevision: 1,
                collisionHash,
                navHash,
                playerSpawn,
                objective,
                objectiveRadius: 3f,
                botSpawns,
                buildings,
                props,
                covers,
                boxes,
                route,
                heights);
        }

        public static CompoundPlan GenerateCanonical(int index) => Generate(CanonicalSeeds[index]);

        private static List<BuildingFootprint> BuildShells()
        {
            var list = new List<BuildingFootprint>(12);
            int slot = 0;
            for (int zi = 0; zi < ShellCentersZ.Length; zi++)
            for (int xi = 0; xi < ShellCentersX.Length; xi++)
            {
                bool roof = (slot % 2) == 0;
                list.Add(new BuildingFootprint(slot, new Float3(ShellCentersX[xi], 0, ShellCentersZ[zi]), 12f, 10f, 6f, roof));
                slot++;
            }
            return list;
        }

        private static List<PropPlacement> BuildProps(IReadOnlyList<BuildingFootprint> buildings)
        {
            var props = new List<PropPlacement>(120);
            for (int index = 0; index < 120; index++)
            {
                int row = index / 10;
                int col = index % 10;
                // docs: rows 10×12 → X=−110+20×column, Z=−110+22×row — use 12 columns × 10 rows for 120
                // "rows10×12: X=−110+20×column, Z=−110+22×row" with indexed 0..119
                int column = index % 12;
                row = index / 12;
                float x = -110f + 20f * column;
                float z = -110f + 22f * row;
                var center = new Float3(x, 0.5f, z);
                if (IntersectsMandatory(buildings, center, 1f, 1f))
                {
                    if (!TryShiftProp(buildings, ref center))
                        throw new InvalidOperationException("Prop " + index + " cannot leave corridor.");
                }
                props.Add(new PropPlacement(index, center, 1f, 1f, 1f, 1));
            }
            return props;
        }

        private static bool IntersectsMandatory(IReadOnlyList<BuildingFootprint> buildings, Float3 center, float sx, float sz)
        {
            // Mandatory corridors: door axes ±Z through each shell and 4 m perimeter strip.
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                // Door approach corridors 2 m wide along ±Z through building center
                if (Math.Abs(center.X - b.Center.X) < 1.2f + sx * 0.5f &&
                    Math.Abs(center.Z - b.Center.Z) < b.SizeZ * 0.5f + 4f)
                    return true;
            }
            // Perimeter corridor band near |x| or |z| ~= 104
            if (Math.Abs(Math.Abs(center.X) - 104f) < 2f + sx || Math.Abs(Math.Abs(center.Z) - 104f) < 2f + sz)
                return true;
            return false;
        }

        private static bool TryShiftProp(IReadOnlyList<BuildingFootprint> buildings, ref Float3 center)
        {
            for (float r = 0.5f; r <= 4f + 1e-4f; r += 0.5f)
            for (int i = 0; i < 16; i++)
            {
                float ang = i * (float)(Math.PI / 8);
                var c = new Float3(center.X + MathF.Cos(ang) * r, center.Y, center.Z + MathF.Sin(ang) * r);
                if (Math.Abs(c.X) > CompoundPlan.MapHalfExtent - 1 || Math.Abs(c.Z) > CompoundPlan.MapHalfExtent - 1) continue;
                if (!IntersectsMandatory(buildings, c, 1f, 1f))
                {
                    // Deterministic tie-break: prefer lower X then Z vs original; accept first in ring order.
                    center = c;
                    return true;
                }
            }
            return false;
        }

        private static void AddWall(List<CollisionBox> boxes, ref ulong nextId, float x, float y, float z, float sx, float sy, float sz)
        {
            if (sx <= 0.05f || sy <= 0 || sz <= 0.05f) return;
            boxes.Add(new CollisionBox(nextId++, nextId, 1, new Float3(x, y, z), new Float3(sx, sy, sz), true));
        }

        private static void EnsureSpawnCover(
            List<CollisionBox> boxes,
            List<CoverProxy> covers,
            ref ulong nextId,
            Float3 playerSpawn,
            Float3[] bots)
        {
            for (int i = 0; i < bots.Length; i++)
            {
                float eye = playerSpawn.Y + 1.65f;
                var playerEye = new Float3(playerSpawn.X, eye, playerSpawn.Z);
                var botEye = new Float3(bots[i].X, bots[i].Y + 1.65f, bots[i].Z);
                if (!CompoundConnectivity.HasLineOfSight(boxes, playerEye, botEye, eye)) continue;
                // Place a hard cover barrier at 35% along the spawn→bot segment.
                float x = playerSpawn.X + (bots[i].X - playerSpawn.X) * 0.35f;
                float z = playerSpawn.Z + (bots[i].Z - playerSpawn.Z) * 0.35f;
                // Nudge off player spawn if too close
                if (Math.Abs(x - playerSpawn.X) < 3f && Math.Abs(z - playerSpawn.Z) < 3f)
                {
                    x = playerSpawn.X + Math.Sign(bots[i].X - playerSpawn.X) * 6f;
                    z = playerSpawn.Z + Math.Sign(bots[i].Z - playerSpawn.Z) * 6f;
                }
                var center = new Float3(x, 1f, z);
                var size = new Float3(2.4f, 2f, 0.6f);
                // Orient longer axis perpendicular to LOS in XZ by swapping if needed
                float dx = bots[i].X - playerSpawn.X, dz = bots[i].Z - playerSpawn.Z;
                if (Math.Abs(dx) > Math.Abs(dz)) size = new Float3(0.6f, 2f, 2.4f);
                ulong id = nextId++;
                boxes.Add(new CollisionBox(id, nextId++, 1, center, size, true));
                var peek = new Float3(center.X + (dx >= 0 ? -1.5f : 1.5f), 0, center.Z + (dz >= 0 ? -1.5f : 1.5f));
                covers.Add(new CoverProxy(id, center, size.X, size.Y, size.Z, peek, new Float3(peek.X, 0, peek.Z), true));
            }
        }

        private static bool TryResolveSpawn(IReadOnlyList<CollisionBox> boxes, ulong seed, Float3 candidate, out Float3 resolved)
        {
            float clearance = CompoundConnectivity.RequiredClearance;
            for (float r = 0; r <= 8f + 1e-4f; r += 0.5f)
            {
                int rings = r < 1e-4f ? 1 : 16;
                for (int i = 0; i < rings; i++)
                {
                    float ang = i * (float)(2 * Math.PI / rings);
                    float x = candidate.X + (r < 1e-4f ? 0 : MathF.Cos(ang) * r);
                    float z = candidate.Z + (r < 1e-4f ? 0 : MathF.Sin(ang) * r);
                    if (Math.Abs(x) > CompoundPlan.MapHalfExtent - 1 || Math.Abs(z) > CompoundPlan.MapHalfExtent - 1) continue;
                    if (CompoundConnectivity.IsBlocked(boxes, x, z, clearance)) continue;
                    float y = TerrainHeight.Sample(seed, x, z);
                    resolved = new Float3(x, y, z);
                    return true;
                }
            }
            // Deterministic tie-break search exhausted
            resolved = candidate;
            return false;
        }

        private static void EnsureCorridorClear(IReadOnlyList<CollisionBox> boxes, Float3 a, Float3 b, float halfWidth)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float len = MathF.Sqrt(dx * dx + dz * dz);
            int steps = Math.Max(2, (int)MathF.Ceiling(len / 1f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = a.X + dx * t, z = a.Z + dz * t;
                if (CompoundConnectivity.IsBlocked(boxes, x, z, halfWidth))
                    throw new InvalidOperationException("Perimeter corridor blocked at " + x.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static ContentId HashGameplay(ulong seed, IReadOnlyList<CollisionBox> boxes, string kind)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            w.Write(CompoundPlan.GeneratorId);
            w.Write(CompoundPlan.GeneratorVersion);
            w.Write(kind);
            w.Write(seed);
            w.Write(boxes.Count);
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.Gameplay) continue;
                w.Write(b.StableId); w.Write(b.SurfaceId); w.Write(b.SurfaceTag);
                w.Write(b.Center.X); w.Write(b.Center.Y); w.Write(b.Center.Z);
                w.Write(b.Size.X); w.Write(b.Size.Y); w.Write(b.Size.Z);
            }
            return ContentIdentity.Hash(ms.ToArray());
        }
    }
}
