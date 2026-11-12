#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Closed revolved solid with a shallow blind bore, not a painted or transparent muzzle hole.</summary>
    internal static class BoredMuzzle
    {
        public static MeshData Build(float outer, float inner, float length, float floorDepth, int segments)
        {
            var p = new List<Float3>(); var n = new List<Float3>(); var t = new List<Float4>(); var uv = new List<Float2>(); var indices = new List<uint>();
            int Vertex(float x, float y, float z, Float3 normal, Float4 tangent, float u, float v)
            { int index = p.Count; p.Add(new Float3(x, y, z)); n.Add(normal); t.Add(tangent); uv.Add(new Float2(u, v)); return index; }
            void Triangle(int a, int b, int c, bool reverse)
            { indices.Add((uint)a); indices.Add((uint)(reverse ? c : b)); indices.Add((uint)(reverse ? b : c)); }
            void Wall(float radius, float z0, float z1, bool inward)
            {
                int start = p.Count;
                for (int i = 0; i <= segments; i++)
                {
                    double angle = i * Math.PI * 2 / segments; float x = (float)Math.Cos(angle), y = (float)Math.Sin(angle);
                    float sign = inward ? -1 : 1;
                    var normal = new Float3(sign * x, sign * y, 0); var tangent = new Float4(-y, x, 0, inward ? -1 : 1);
                    float uMeters = (float)(i * Math.PI * 2 * radius / segments);
                    Vertex(radius * x, radius * y, z0, normal, tangent, uMeters, z0);
                    Vertex(radius * x, radius * y, z1, normal, tangent, uMeters, z1);
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i * 2;
                    Triangle(a, a + 2, a + 1, inward); Triangle(a + 2, a + 3, a + 1, inward);
                }
            }
            void Cap(float outerRadius, float innerRadius, float z, bool reverse)
            {
                var normal = new Float3(0, 0, reverse ? -1 : 1); var tangent = new Float4(1, 0, 0, reverse ? -1 : 1);
                int center = innerRadius == 0 ? Vertex(0, 0, z, normal, tangent, 0, 0) : -1;
                int start = p.Count;
                for (int i = 0; i <= segments; i++)
                {
                    double angle = i * Math.PI * 2 / segments; float x = (float)Math.Cos(angle), y = (float)Math.Sin(angle);
                    Vertex(outerRadius * x, outerRadius * y, z, normal, tangent, outerRadius * x, outerRadius * y);
                    if (innerRadius != 0)
                        Vertex(innerRadius * x, innerRadius * y, z, normal, tangent, innerRadius * x, innerRadius * y);
                }
                for (int i = 0; i < segments; i++)
                {
                    if (center >= 0) Triangle(center, start + i, start + i + 1, reverse);
                    else
                    {
                        int a = start + i * 2;
                        Triangle(a, a + 2, a + 1, reverse); Triangle(a + 2, a + 3, a + 1, reverse);
                    }
                }
            }
            Wall(outer, 0, length, false); Wall(inner, floorDepth, length, true);
            Cap(outer, inner, length, false); Cap(inner, 0, floorDepth, false); Cap(outer, 0, 0, true);
            var mesh = new MeshData(p, n, t, uv, indices, new[] { new Submesh(0, (uint)indices.Count, 0) });
            MeshValidation.Check(mesh, true);
            return mesh;
        }
    }
}
