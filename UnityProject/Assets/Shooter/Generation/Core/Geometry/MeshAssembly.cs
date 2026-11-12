#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Rigid component composition, not boolean union. Material slots sort deterministically.</summary>
    public sealed class MeshAssembly
    {
        private readonly List<Float3> positions = new List<Float3>(), normals = new List<Float3>();
        private readonly List<Float4> tangents = new List<Float4>();
        private readonly List<Float2> uv = new List<Float2>();
        private readonly SortedDictionary<ushort, List<uint>> groups = new SortedDictionary<ushort, List<uint>>();
        private int indexCount;
        private Float3 min, max;
        public int VertexCount => positions.Count;
        public int TriangleCount => indexCount / 3;
        public Aabb Bounds => positions.Count == 0 ? throw new InvalidOperationException("Empty assembly.") : new Aabb(min, max);
        public void Add(MeshData mesh, Float3 translation, Quat rotation, ushort materialSlot = 0)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (!rotation.IsValid || mesh.Joints != null) throw new ArgumentException("Rigid unskinned composition requires a unit quaternion.");
            long vertices = (long)positions.Count + mesh.Positions.Count, indices = (long)indexCount + mesh.Indices.Count;
            if (vertices > 2000000 || indices > 6000000 || vertices * 48 + indices * 4 + (groups.Count + 1L) * 10 > ArtifactCodec.MaxBodyBytes - 16)
                throw new ArgumentException("Assembly byte/count limit.");
            if (!groups.ContainsKey(materialSlot) && groups.Count == 256) throw new ArgumentException("Assembly material slot limit.");
            // Validate all transformed values before mutating the builder.
            var ps = new Float3[mesh.Positions.Count]; var ns = new Float3[ps.Length]; var ts = new Float4[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                ps[i] = GeometryMath.Add(GeometryMath.Rotate(mesh.Positions[i], rotation), translation);
                ns[i] = GeometryMath.Normalize(GeometryMath.Rotate(mesh.Normals[i], rotation));
                Float4 t = mesh.Tangents[i]; Float3 v = GeometryMath.Normalize(GeometryMath.Rotate(new Float3(t.X, t.Y, t.Z), rotation));
                ts[i] = new Float4(v.X, v.Y, v.Z, t.W);
            }
            uint offset = (uint)positions.Count;
            if (!groups.TryGetValue(materialSlot, out var group)) { group = new List<uint>(); groups.Add(materialSlot, group); }
            for (int i = 0; i < ps.Length; i++)
            {
                Float3 p = ps[i];
                if (positions.Count == 0) { min = p; max = p; }
                else { min = new Float3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z)); max = new Float3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z)); }
                positions.Add(p); normals.Add(ns[i]); tangents.Add(ts[i]); uv.Add(mesh.Uv[i]);
            }
            foreach (uint index in mesh.Indices) group.Add(offset + index);
            indexCount += mesh.Indices.Count;
        }
        public MeshData Build()
        {
            if (positions.Count == 0) throw new InvalidOperationException("Cannot build an empty assembly.");
            var indices = new List<uint>(indexCount); var subs = new List<Submesh>(groups.Count);
            foreach (var group in groups)
            { subs.Add(new Submesh((uint)indices.Count, (uint)group.Value.Count, group.Key)); indices.AddRange(group.Value); }
            var mesh = new MeshData(positions, normals, tangents, uv, indices, subs);
            GeometryCodec.EncodeMesh(mesh); return mesh;
        }
    }
}
