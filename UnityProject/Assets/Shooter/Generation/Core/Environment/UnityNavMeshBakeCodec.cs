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
        /// Software triangulation over free cells for package publication when Unity NavMesh bake is unavailable.
        /// FIX04 native player/nav install remains Pending until editor bake evidence lands.
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

        private static void RequireFinite(Float3 v)
        {
            if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z) ||
                float.IsInfinity(v.X) || float.IsInfinity(v.Y) || float.IsInfinity(v.Z))
                throw new FormatException("Non-finite navigation vertex.");
        }
    }
}
