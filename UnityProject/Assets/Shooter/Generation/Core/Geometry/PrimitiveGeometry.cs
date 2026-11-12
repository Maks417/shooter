#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Closed numeric primitives. Box axes are XYZ; revolved shapes use +Z.</summary>
    public static class PrimitiveGeometry
    {
        public static MeshData Box(Float3 size, float bevelMeters = 0, int bevelSegments = 1)
        {
            GeometryMath.Dimension(size.X); GeometryMath.Dimension(size.Y); GeometryMath.Dimension(size.Z);
            if (!GeometryMath.Finite(bevelMeters) || bevelMeters < 0 || bevelSegments < 1 || bevelSegments > 8)
                throw new ArgumentException("Invalid bevel or segment count.");
            float minimum = Math.Min(size.X, Math.Min(size.Y, size.Z));
            if (bevelMeters >= minimum * .5f || bevelMeters > 0 && bevelMeters / bevelSegments < .000025f)
                throw new ArgumentException("Bevel must preserve a positive face core and measurable cells.");
            var mesh = new GeometryBuffer();
                        float[] half = { size.X * .5f, size.Y * .5f, size.Z * .5f };
            float[] inner = { half[0] - bevelMeters, half[1] - bevelMeters, half[2] - bevelMeters };
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                float[] us = Coordinates(half[u], inner[u], bevelMeters, bevelSegments);
                float[] vs = Coordinates(half[v], inner[v], bevelMeters, bevelSegments);
                for (int side = -1; side <= 1; side += 2)
                {
                    int first = mesh.Positions.Count;
                    for (int j = 0; j < vs.Length; j++) for (int i = 0; i < us.Length; i++)
                    {
                        float[] p = new float[3]; p[axis] = side * half[axis]; p[u] = us[i]; p[v] = vs[j];
                        Float3 position, normal;
                        if (bevelMeters == 0)
                        { position = GeometryMath.Vector(p); normal = GeometryMath.Axis(axis, side); }
                        else
                        {
                            var core = new Float3(Clamp(p[0], -inner[0], inner[0]), Clamp(p[1], -inner[1], inner[1]), Clamp(p[2], -inner[2], inner[2]));
                            normal = GeometryMath.Normalize(GeometryMath.Sub(GeometryMath.Vector(p), core));
                            position = GeometryMath.Add(core, GeometryMath.Scale(normal, bevelMeters));
                        }
                        Float3 basis = GeometryMath.Axis(u, 1);
                        Float3 tangent = GeometryMath.Normalize(GeometryMath.Sub(basis, GeometryMath.Scale(normal, GeometryMath.Dot(normal, basis))));
                        mesh.Vertex(position, normal, new Float4(tangent.X, tangent.Y, tangent.Z, side), new Float2(us[i] + half[u], vs[j] + half[v]));
                    }
                    for (int j = 0; j + 1 < vs.Length; j++) for (int i = 0; i + 1 < us.Length; i++)
                    {
                        int a = first + j * us.Length + i, b = a + 1, c = a + us.Length, d = c + 1;
                        if (side > 0) { mesh.Triangle(a, b, c); mesh.Triangle(b, d, c); }
                        else { mesh.Triangle(a, c, b); mesh.Triangle(b, c, d); }
                    }
                }
            }
            return mesh.Build();
        }

        public static MeshData Cylinder(float radius, float length, int radialSegments = 16)
            => Cone(radius, radius, length, radialSegments);

        public static MeshData Cone(float bottomRadius, float topRadius, float length, int radialSegments = 16)
        {
            GeometryMath.Dimension(length);
            if (!GeometryMath.Finite(bottomRadius) || !GeometryMath.Finite(topRadius) || bottomRadius < 0 || topRadius < 0 ||
                bottomRadius > 500 || topRadius > 500 || Math.Max(bottomRadius, topRadius) < .0005f ||
                bottomRadius > 0 && bottomRadius < .0005f || topRadius > 0 && topRadius < .0005f || radialSegments < 3 || radialSegments > 128)
                throw new ArgumentException("Invalid radial dimensions/segments.");
            var mesh = new GeometryBuffer(); float half = length * .5f;
            for (int i = 0; i < radialSegments; i++)
            {
                double a = 2 * Math.PI * i / radialSegments, b = 2 * Math.PI * (i + 1) / radialSegments;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a), cb = (float)Math.Cos(b), sb = (float)Math.Sin(b);
                // Exact seam endpoints prevent tiny sin(2pi) drift in positions and cap welding.
                if (i + 1 == radialSegments) { cb = 1; sb = 0; }
                Float3 na = GeometryMath.Normalize(new Float3(ca, sa, (bottomRadius - topRadius) / length));
                Float3 nb = GeometryMath.Normalize(new Float3(cb, sb, (bottomRadius - topRadius) / length));
                Float4 ta = new Float4(-sa, ca, 0, 1), tb = new Float4(-sb, cb, 0, 1);
                int start = mesh.Positions.Count;
                float circumference = (float)(2 * Math.PI * Math.Max(bottomRadius, topRadius));
                float u0 = circumference * i / radialSegments, u1 = circumference * (i + 1) / radialSegments;
                if (bottomRadius == 0)
                {
                    var mid = GeometryMath.Normalize(GeometryMath.Add(na, nb));
                    Float3 tangent = GeometryMath.Normalize(new Float3(-(sa + sb), ca + cb, 0));
                    mesh.Vertex(new Float3(0, 0, -half), mid, new Float4(tangent.X, tangent.Y, tangent.Z, 1), new Float2((u0 + u1) / 2, 0));
                    mesh.Vertex(new Float3(topRadius * cb, topRadius * sb, half), nb, tb, new Float2(u1, length));
                    mesh.Vertex(new Float3(topRadius * ca, topRadius * sa, half), na, ta, new Float2(u0, length));
                    mesh.Triangle(start, start + 1, start + 2);
                }
                else if (topRadius == 0)
                {
                    var mid = GeometryMath.Normalize(GeometryMath.Add(na, nb));
                    Float3 tangent = GeometryMath.Normalize(new Float3(-(sa + sb), ca + cb, 0));
                    mesh.Vertex(new Float3(bottomRadius * ca, bottomRadius * sa, -half), na, ta, new Float2(u0, 0));
                    mesh.Vertex(new Float3(bottomRadius * cb, bottomRadius * sb, -half), nb, tb, new Float2(u1, 0));
                    mesh.Vertex(new Float3(0, 0, half), mid, new Float4(tangent.X, tangent.Y, tangent.Z, 1), new Float2((u0 + u1) / 2, length));
                    mesh.Triangle(start, start + 1, start + 2);
                }
                else
                {
                    mesh.Vertex(new Float3(bottomRadius * ca, bottomRadius * sa, -half), na, ta, new Float2(u0, 0));
                    mesh.Vertex(new Float3(bottomRadius * cb, bottomRadius * sb, -half), nb, tb, new Float2(u1, 0));
                    mesh.Vertex(new Float3(topRadius * ca, topRadius * sa, half), na, ta, new Float2(u0, length));
                    mesh.Vertex(new Float3(topRadius * cb, topRadius * sb, half), nb, tb, new Float2(u1, length));
                    mesh.Triangle(start, start + 1, start + 2); mesh.Triangle(start + 1, start + 3, start + 2);
                }
                if (bottomRadius > 0) Cap(mesh, bottomRadius, -half, -1, ca, sa, cb, sb);
                if (topRadius > 0) Cap(mesh, topRadius, half, 1, ca, sa, cb, sb);
            }
            return mesh.Build();
        }

        private static void Cap(GeometryBuffer mesh, float radius, float z, int side, float ca, float sa, float cb, float sb)
        {
            int start = mesh.Positions.Count; var normal = new Float3(0, 0, side); var tangent = new Float4(1, 0, 0, side);
            mesh.Vertex(new Float3(0, 0, z), normal, tangent, new Float2(radius, radius));
            mesh.Vertex(new Float3(radius * ca, radius * sa, z), normal, tangent, new Float2(radius * ca + radius, radius * sa + radius));
            mesh.Vertex(new Float3(radius * cb, radius * sb, z), normal, tangent, new Float2(radius * cb + radius, radius * sb + radius));
            if (side > 0) mesh.Triangle(start, start + 1, start + 2); else mesh.Triangle(start, start + 2, start + 1);
        }
        private static float Clamp(float x, float lo, float hi) => Math.Min(hi, Math.Max(lo, x));
        private static float[] Coordinates(float half, float inner, float bevel, int segments)
        {
            if (bevel == 0) return new[] { -half, half };
            var result = new float[2 * segments + 2];
            for (int i = 0; i <= segments; i++) result[i] = -half + bevel * i / segments;
            for (int i = 0; i <= segments; i++) result[segments + 1 + i] = inner + bevel * i / segments;
            result[0] = -half; result[segments] = -inner; result[segments + 1] = inner; result[result.Length - 1] = half;
            return result;
        }
    }

    internal sealed class GeometryBuffer
    {
        public readonly List<Float3> Positions = new List<Float3>();
        private readonly List<Float3> normals = new List<Float3>();
        private readonly List<Float4> tangents = new List<Float4>();
        private readonly List<Float2> uv = new List<Float2>();
        private readonly List<uint> indices = new List<uint>();
        public void Vertex(Float3 p, Float3 n, Float4 t, Float2 tex) { Positions.Add(p); normals.Add(n); tangents.Add(t); uv.Add(tex); }
        public void Triangle(int a, int b, int c) { indices.Add((uint)a); indices.Add((uint)b); indices.Add((uint)c); }
        public MeshData Build()
        {
            var mesh = new MeshData(Positions, normals, tangents, uv, indices, new[] { new Submesh(0, (uint)indices.Count, 0) });
            GeometryCodec.EncodeMesh(mesh); return mesh;
        }
    }
    internal static class GeometryMath
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static void Dimension(float value) { if (!Finite(value) || value < .001f || value > 1000) throw new ArgumentOutOfRangeException(nameof(value), "Dimension must be 1 mm..1000 m."); }
        public static Float3 Vector(float[] v) => new Float3(v[0], v[1], v[2]);
        public static Float3 Axis(int axis, float sign) => new Float3(axis == 0 ? sign : 0, axis == 1 ? sign : 0, axis == 2 ? sign : 0);
        public static Float3 Add(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 Sub(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 Scale(Float3 v, float scale) => new Float3(v.X * scale, v.Y * scale, v.Z * scale);
        public static float Dot(Float3 a, Float3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Float3 Normalize(Float3 v)
        {
            double length = Math.Sqrt(v.LengthSquared);
            if (length < 1e-12) throw new ArgumentException("Zero-length direction.");
            return new Float3((float)(v.X / length), (float)(v.Y / length), (float)(v.Z / length));
        }
        public static Float3 Rotate(Float3 p, Quat q)
        {
            float tx = 2 * (q.Y * p.Z - q.Z * p.Y), ty = 2 * (q.Z * p.X - q.X * p.Z), tz = 2 * (q.X * p.Y - q.Y * p.X);
            return new Float3(p.X + q.W * tx + q.Y * tz - q.Z * ty, p.Y + q.W * ty + q.Z * tx - q.X * tz, p.Z + q.W * tz + q.X * ty - q.Y * tx);
        }
    }
}
