#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public static class MeshValidation
    {
        /// <summary>Checks finite frames/indices/areas, normal-facing winding and optional welded solid edges.</summary>
        public static void Check(MeshData mesh, bool requireClosed = true)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            GeometryCodec.EncodeMesh(mesh);
            var welded = new Dictionary<(long, long, long), int>(); var ids = new int[mesh.Positions.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                Float3 p = mesh.Positions[i];
                if (Math.Abs(p.X) > 1000000 || Math.Abs(p.Y) > 1000000 || Math.Abs(p.Z) > 1000000) throw new ArgumentException("Validation position range.");
                var key = (Quantize(p.X), Quantize(p.Y), Quantize(p.Z));
                if (!welded.TryGetValue(key, out int id)) { id = welded.Count; welded.Add(key, id); }
                ids[i] = id;
            }
            var edges = new Dictionary<(int, int), (int count, int orientation)>(); double volume6 = 0;
            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                int ia = (int)mesh.Indices[i], ib = (int)mesh.Indices[i + 1], ic = (int)mesh.Indices[i + 2];
                Float3 a = mesh.Positions[ia], b = mesh.Positions[ib], c = mesh.Positions[ic];
                double ux = (double)b.X - a.X, uy = (double)b.Y - a.Y, uz = (double)b.Z - a.Z;
                double vx = (double)c.X - a.X, vy = (double)c.Y - a.Y, vz = (double)c.Z - a.Z;
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                Float3 na = mesh.Normals[ia], nb = mesh.Normals[ib], nc = mesh.Normals[ic];
                double agreement = nx * ((double)na.X + nb.X + nc.X) + ny * ((double)na.Y + nb.Y + nc.Y) + nz * ((double)na.Z + nb.Z + nc.Z);
                if (agreement <= 0) throw new ArgumentException("Triangle winding disagrees with outward vertex normals.");
                volume6 += (double)a.X * ((double)b.Y * c.Z - (double)b.Z * c.Y) + (double)a.Y * ((double)b.Z * c.X - (double)b.X * c.Z) + (double)a.Z * ((double)b.X * c.Y - (double)b.Y * c.X);
                if (requireClosed) { Edge(ids[ia], ids[ib], edges); Edge(ids[ib], ids[ic], edges); Edge(ids[ic], ids[ia], edges); }
            }
            if (requireClosed)
            {
                foreach (var edge in edges.Values) if (edge.count != 2 || edge.orientation != 0) throw new ArgumentException("Solid has an open, duplicate or inconsistently oriented welded edge.");
                if (volume6 <= 0) throw new ArgumentException("Solid signed volume is not outward-positive.");
            }
        }
        private static long Quantize(float v) => (long)Math.Round(v * 1000000.0, MidpointRounding.AwayFromZero);
        private static void Edge(int a, int b, Dictionary<(int, int), (int count, int orientation)> edges)
        {
            if (a == b) throw new ArgumentException("Triangle collapses at weld tolerance.");
            var key = a < b ? (a, b) : (b, a); int direction = a < b ? 1 : -1;
            edges.TryGetValue(key, out var value); edges[key] = (value.count + 1, value.orientation + direction);
        }
    }
}
