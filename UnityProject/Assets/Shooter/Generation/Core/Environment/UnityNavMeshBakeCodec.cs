#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>ADR-035 shipping navigation adapter blob: unity.navmesh.bake.v1 / NVB1 triangulation.</summary>
    public static class UnityNavMeshBakeCodec
    {
        public const string AdapterFormat = "unity.navmesh.bake.v1";
        public const uint AdapterVersion = 1;
        public const uint Magic = 0x3142564E; // 'NVB1' little-endian

        public readonly struct Polygon
        {
            public readonly uint[] Indices;
            public readonly ushort Area;
            public readonly byte Flags;
            public Polygon(uint[] indices, ushort area, byte flags)
            {
                if (indices == null || indices.Length < 3 || indices.Length > 6) throw new ArgumentOutOfRangeException(nameof(indices));
                Indices = indices; Area = area; Flags = flags;
            }
        }

        public sealed class Bake
        {
            public IReadOnlyList<Float3> Vertices { get; }
            public IReadOnlyList<Polygon> Polygons { get; }
            public Float3 BoundsMin { get; }
            public Float3 BoundsMax { get; }
            public Bake(IReadOnlyList<Float3> vertices, IReadOnlyList<Polygon> polygons, Float3 boundsMin, Float3 boundsMax)
            {
                Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
                Polygons = polygons ?? throw new ArgumentNullException(nameof(polygons));
                BoundsMin = boundsMin; BoundsMax = boundsMax;
            }
        }

        public static byte[] Encode(Bake bake)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (bake.Vertices.Count == 0 || bake.Polygons.Count == 0) throw new ArgumentException("Bake requires vertices and polygons.");
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            w.Write(Magic);
            w.Write((uint)bake.Vertices.Count);
            for (int i = 0; i < bake.Vertices.Count; i++)
            {
                var v = bake.Vertices[i];
                RequireFinite(v);
                w.Write(v.X); w.Write(v.Y); w.Write(v.Z);
            }
            w.Write((uint)bake.Polygons.Count);
            for (int i = 0; i < bake.Polygons.Count; i++)
            {
                var poly = bake.Polygons[i];
                w.Write((byte)poly.Indices.Length);
                for (int j = 0; j < poly.Indices.Length; j++)
                {
                    if (poly.Indices[j] >= (uint)bake.Vertices.Count) throw new ArgumentOutOfRangeException(nameof(bake), "Polygon index out of range.");
                    w.Write(poly.Indices[j]);
                }
                w.Write(poly.Area);
                w.Write(poly.Flags);
            }
            RequireFinite(bake.BoundsMin); RequireFinite(bake.BoundsMax);
            w.Write(bake.BoundsMin.X); w.Write(bake.BoundsMin.Y); w.Write(bake.BoundsMin.Z);
            w.Write(bake.BoundsMax.X); w.Write(bake.BoundsMax.Y); w.Write(bake.BoundsMax.Z);
            byte[] bytes = ms.ToArray();
            Decode(bytes);
            return bytes;
        }

        public static Bake Decode(ReadOnlySpan<byte> blob)
        {
            if (blob.Length < 16) throw new FormatException("NVB1 blob truncated.");
            var r = new BinaryReader(new MemoryStream(blob.ToArray()), Encoding.UTF8, true);
            if (r.ReadUInt32() != Magic) throw new FormatException("Expected NVB1 magic.");
            int vertexCount = checked((int)r.ReadUInt32());
            if (vertexCount <= 0 || vertexCount > 1_000_000) throw new FormatException("Invalid vertex count.");
            var vertices = new Float3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                var v = new Float3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                RequireFinite(v);
                vertices[i] = v;
            }
            int polyCount = checked((int)r.ReadUInt32());
            if (polyCount <= 0 || polyCount > 1_000_000) throw new FormatException("Invalid polygon count.");
            var polys = new Polygon[polyCount];
            for (int i = 0; i < polyCount; i++)
            {
                int indexCount = r.ReadByte();
                if (indexCount < 3 || indexCount > 6) throw new FormatException("Polygon index count must be 3..6.");
                var indices = new uint[indexCount];
                for (int j = 0; j < indexCount; j++)
                {
                    indices[j] = r.ReadUInt32();
                    if (indices[j] >= (uint)vertexCount) throw new FormatException("Polygon index out of range.");
                }
                ushort area = r.ReadUInt16();
                byte flags = r.ReadByte();
                polys[i] = new Polygon(indices, area, flags);
            }
            var min = new Float3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var max = new Float3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            RequireFinite(min); RequireFinite(max);
            if (r.BaseStream.Position != r.BaseStream.Length) throw new FormatException("NVB1 blob has trailing bytes.");
            return new Bake(vertices, polys, min, max);
        }

        /// <summary>
        /// Software triangulation over free cells for pure-test / non-Unity package publication only.
        /// Must never be labeled as a completed native Unity bake in shipping provenance.
        /// </summary>
        public static Bake BuildWalkableGrid(IReadOnlyList<CollisionBox> boxes, float halfExtent, float cellSize, float clearance)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (halfExtent <= 0 || cellSize < 1f || clearance <= 0) throw new ArgumentOutOfRangeException(nameof(halfExtent));
            var vertices = new List<Float3>();
            var polys = new List<Polygon>();
            int cells = (int)MathF.Floor((halfExtent * 2f) / cellSize);
            if (cells < 2) cells = 2;
            float origin = -halfExtent;
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    float cx = origin + (x + 0.5f) * cellSize;
                    float cz = origin + (z + 0.5f) * cellSize;
                    if (CompoundConnectivity.IsBlocked(boxes, cx, cz, clearance)) continue;
                    float y = 0f;
                    float x0 = origin + x * cellSize, x1 = x0 + cellSize;
                    float z0 = origin + z * cellSize, z1 = z0 + cellSize;
                    uint baseIndex = (uint)vertices.Count;
                    vertices.Add(new Float3(x0, y, z0));
                    vertices.Add(new Float3(x1, y, z0));
                    vertices.Add(new Float3(x1, y, z1));
                    vertices.Add(new Float3(x0, y, z1));
                    polys.Add(new Polygon(new[] { baseIndex, baseIndex + 1, baseIndex + 2, baseIndex + 3 }, 1, 0));
                }
            }
            if (vertices.Count == 0) throw new InvalidOperationException("No walkable cells for nav bake.");
            var min = new Float3(-halfExtent, -1f, -halfExtent);
            var max = new Float3(halfExtent, 4f, halfExtent);
            return new Bake(vertices, polys, min, max);
        }

        /// <summary>True when the XZ point lies inside any bake polygon (Y ignored for containment).</summary>
        public static bool ContainsPointXZ(Bake bake, float x, float z)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            for (int i = 0; i < bake.Polygons.Count; i++)
            {
                if (PointInPolygonXZ(bake, bake.Polygons[i], x, z)) return true;
            }
            return false;
        }

        /// <summary>
        /// Polygon-graph path over an installed NVB1 bake. Used by pure tests and as the runtime solver
        /// when querying packaged triangulation corners without re-baking against collision.
        /// </summary>
        public static bool TryFindPath(Bake bake, Float3 start, Float3 goal, List<Float3> corners)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (corners == null) throw new ArgumentNullException(nameof(corners));
            corners.Clear();
            if (!TryNearestPolygon(bake, start.X, start.Z, out int startPoly, out Float3 startPoint)) return false;
            if (!TryNearestPolygon(bake, goal.X, goal.Z, out int goalPoly, out Float3 goalPoint)) return false;
            if (startPoly == goalPoly)
            {
                corners.Add(startPoint);
                if (DistanceXZ(startPoint, goalPoint) > 1e-4f) corners.Add(goalPoint);
                return true;
            }

            int polyCount = bake.Polygons.Count;
            var adjacency = BuildAdjacency(bake);
            var came = new int[polyCount];
            var gScore = new float[polyCount];
            var open = new List<int>(polyCount);
            var inOpen = new bool[polyCount];
            for (int i = 0; i < polyCount; i++)
            {
                came[i] = -1;
                gScore[i] = float.PositiveInfinity;
            }
            gScore[startPoly] = 0f;
            open.Add(startPoly);
            inOpen[startPoly] = true;
            Float3 goalCenter = PolygonCenter(bake, bake.Polygons[goalPoly]);

            bool found = false;
            int guard = 0;
            while (open.Count > 0)
            {
                if (++guard > polyCount * polyCount + 8) return false;
                int best = 0;
                float bestF = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    int p = open[i];
                    float f = gScore[p] + DistanceXZ(PolygonCenter(bake, bake.Polygons[p]), goalCenter);
                    if (f < bestF) { bestF = f; best = i; }
                }
                int current = open[best];
                open.RemoveAt(best);
                inOpen[current] = false;
                if (current == goalPoly) { found = true; break; }
                var neighbors = adjacency[current];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int next = neighbors[i];
                    float step = DistanceXZ(PolygonCenter(bake, bake.Polygons[current]), PolygonCenter(bake, bake.Polygons[next]));
                    float tentative = gScore[current] + Math.Max(step, 1e-4f);
                    if (tentative >= gScore[next]) continue;
                    came[next] = current;
                    gScore[next] = tentative;
                    if (!inOpen[next])
                    {
                        open.Add(next);
                        inOpen[next] = true;
                    }
                }
            }
            if (!found) return false;

            var chain = new List<int>();
            for (int p = goalPoly; p >= 0; p = came[p])
            {
                chain.Add(p);
                if (p == startPoly) break;
            }
            if (chain.Count == 0 || chain[chain.Count - 1] != startPoly) return false;
            chain.Reverse();

            corners.Add(startPoint);
            for (int i = 1; i < chain.Count - 1; i++)
                corners.Add(PolygonCenter(bake, bake.Polygons[chain[i]]));
            if (DistanceXZ(corners[corners.Count - 1], goalPoint) > 1e-4f)
                corners.Add(goalPoint);
            return corners.Count >= 1;
        }

        private static List<int>[] BuildAdjacency(Bake bake)
        {
            int n = bake.Polygons.Count;
            var adj = new List<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new List<int>(4);
            // Index-shared edges (native Unity triangulation) plus geometric edges (software grid cells).
            var indexEdges = new Dictionary<(uint, uint), int>();
            var geoEdges = new Dictionary<(long, long, long, long), int>();
            for (int p = 0; p < n; p++)
            {
                var poly = bake.Polygons[p];
                int count = poly.Indices.Length;
                for (int i = 0; i < count; i++)
                {
                    uint ia = poly.Indices[i];
                    uint ib = poly.Indices[(i + 1) % count];
                    var indexKey = ia < ib ? (ia, ib) : (ib, ia);
                    if (indexEdges.TryGetValue(indexKey, out int indexOther))
                        Link(adj, p, indexOther);
                    else indexEdges[indexKey] = p;

                    var va = bake.Vertices[(int)ia];
                    var vb = bake.Vertices[(int)ib];
                    var geoKey = EdgeKey(va, vb);
                    if (geoEdges.TryGetValue(geoKey, out int geoOther))
                        Link(adj, p, geoOther);
                    else geoEdges[geoKey] = p;
                }
            }
            return adj;
        }

        private static void Link(List<int>[] adj, int a, int b)
        {
            if (a == b) return;
            if (!adj[a].Contains(b)) adj[a].Add(b);
            if (!adj[b].Contains(a)) adj[b].Add(a);
        }

        private static (long, long, long, long) EdgeKey(Float3 a, Float3 b)
        {
            long ax = Quantize(a.X), az = Quantize(a.Z), bx = Quantize(b.X), bz = Quantize(b.Z);
            if (ax < bx || (ax == bx && az <= bz)) return (ax, az, bx, bz);
            return (bx, bz, ax, az);
        }

        private static long Quantize(float v) => (long)MathF.Round(v * 1000f);

        private static bool TryNearestPolygon(Bake bake, float x, float z, out int polyIndex, out Float3 point)
        {
            polyIndex = -1;
            point = default;
            float best = float.PositiveInfinity;
            for (int i = 0; i < bake.Polygons.Count; i++)
            {
                var poly = bake.Polygons[i];
                if (PointInPolygonXZ(bake, poly, x, z))
                {
                    Float3 c = PolygonCenter(bake, poly);
                    polyIndex = i;
                    point = new Float3(x, c.Y, z);
                    return true;
                }
                Float3 closest = ClosestPointOnPolygonXZ(bake, poly, x, z);
                float d = DistanceXZ(closest, new Float3(x, 0f, z));
                if (d < best)
                {
                    best = d;
                    polyIndex = i;
                    point = closest;
                }
            }
            // Snap onto the bake surface; keep holes larger than this threshold blocked.
            return polyIndex >= 0 && best <= 8.1f;
        }

        private static Float3 ClosestPointOnPolygonXZ(Bake bake, Polygon poly, float x, float z)
        {
            Float3 center = PolygonCenter(bake, poly);
            float best = DistanceXZ(center, new Float3(x, 0f, z));
            Float3 bestPoint = center;
            int count = poly.Indices.Length;
            for (int i = 0; i < count; i++)
            {
                var a = bake.Vertices[(int)poly.Indices[i]];
                var b = bake.Vertices[(int)poly.Indices[(i + 1) % count]];
                Float3 c = ClosestPointOnSegmentXZ(a, b, x, z);
                float d = DistanceXZ(c, new Float3(x, 0f, z));
                if (d < best)
                {
                    best = d;
                    bestPoint = c;
                }
            }
            return bestPoint;
        }

        private static Float3 ClosestPointOnSegmentXZ(Float3 a, Float3 b, float x, float z)
        {
            float abx = b.X - a.X, abz = b.Z - a.Z;
            float len2 = abx * abx + abz * abz;
            if (len2 < 1e-10f) return a;
            float t = ((x - a.X) * abx + (z - a.Z) * abz) / len2;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return new Float3(a.X + abx * t, a.Y + (b.Y - a.Y) * t, a.Z + abz * t);
        }

        private static bool PointInPolygonXZ(Bake bake, Polygon poly, float x, float z)
        {
            bool inside = false;
            int count = poly.Indices.Length;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var vi = bake.Vertices[(int)poly.Indices[i]];
                var vj = bake.Vertices[(int)poly.Indices[j]];
                if (((vi.Z > z) != (vj.Z > z)) &&
                    (x < (vj.X - vi.X) * (z - vi.Z) / (vj.Z - vi.Z + 1e-12f) + vi.X))
                    inside = !inside;
            }
            return inside;
        }

        private static Float3 PolygonCenter(Bake bake, Polygon poly)
        {
            float x = 0f, y = 0f, z = 0f;
            for (int i = 0; i < poly.Indices.Length; i++)
            {
                var v = bake.Vertices[(int)poly.Indices[i]];
                x += v.X; y += v.Y; z += v.Z;
            }
            float inv = 1f / poly.Indices.Length;
            return new Float3(x * inv, y * inv, z * inv);
        }

        private static float DistanceXZ(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        private static void RequireFinite(Float3 v)
        {
            if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z) ||
                float.IsInfinity(v.X) || float.IsInfinity(v.Y) || float.IsInfinity(v.Z))
                throw new FormatException("Non-finite navigation vertex.");
        }
    }
}
