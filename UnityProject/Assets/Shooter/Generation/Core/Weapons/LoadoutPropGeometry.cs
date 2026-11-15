#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Numeric equipment profiles. No model, image or authored vertex input.</summary>
    public static class LoadoutPropGeometry
    {
        public static MeshData BuildKnife(int lod)
        {
            CheckLod(lod);
            var assembly = new MeshAssembly();
            // Diamond-ground blade: thick spine, distinct bevel planes and a tapered point.
            assembly.Add(WeaponProfiles.Loft(new[] { -.004f, .018f, .108f, .143f, .159f },
                new[] { .003f, .003f, .0026f, .0016f, .0003f },
                new[] { .010f, .015f, .014f, .007f, .0003f }, 4),
                new Float3(0, .006f, 0), Quat.Identity, 2);
            assembly.Add(WeaponProfiles.Loft(new[] { -.090f, -.080f, -.016f, -.006f },
                new[] { .009f, .012f, .012f, .009f }, new[] { .014f, .018f, .017f, .014f }, 8),
                new Float3(0, 0, 0), Quat.Identity, 1);
            void Box(float z, float sx, float sy, float sz, ushort slot)
                => assembly.Add(PrimitiveGeometry.Box(new Float3(sx, sy, sz), lod < 2 ? .0008f : 0),
                    new Float3(0, 0, z), Quat.Identity, slot);
            Box(-.006f, .024f, .052f, .006f, 0); // metal finger guard
            Box(-.089f, .023f, .033f, .008f, 0); // exposed tang / pommel
            if (lod < 2)
            {
                for (int i = 0; i < (lod == 0 ? 6 : 3); i++)
                {
                    float z = -.075f + i * (lod == 0 ? .010f : .020f);
                    assembly.Add(WeaponProfiles.Loft(new[] { z, z + .0018f },
                        new[] { .0124f, .0124f }, new[] { .0175f, .0175f }, 8), new Float3(0, 0, 0), Quat.Identity, 3);
                }
                foreach (float z in new[] { -.068f, -.028f })
                    for (int side = -1; side <= 1; side += 2)
                        WeaponProfiles.Fastener(assembly, side * .012f, 0, z, lod, .0025f);
            }
            return assembly.Build();
        }

        public static MeshData BuildGrenade(int lod)
        {
            CheckLod(lod);
            var assembly = new MeshAssembly();
            int radial = new[] { 24, 16, 10, 6 }[lod];
            var upright = WeaponGeometry.Pitch(-90);
            // Upright revolved ovoid; the old capped cylinder faced the firing axis.
            assembly.Add(WeaponProfiles.Loft(new[] { -.045f, -.040f, -.024f, .002f, .024f, .038f, .042f },
                new[] { .012f, .022f, .031f, .034f, .028f, .017f, .011f },
                new[] { .012f, .022f, .031f, .034f, .028f, .017f, .011f }, radial),
                new Float3(0, 0, 0), upright, 4);
            assembly.Add(PrimitiveGeometry.Cylinder(.012f, .019f, radial), new Float3(0, .047f, 0), upright, 0);
            void Box(float x, float y, float sx, float sy, float sz, float angle = 0)
                => assembly.Add(PrimitiveGeometry.Box(new Float3(sx, sy, sz), lod < 2 ? .0006f : 0),
                    new Float3(x, y, 0), new Quat(0, 0, (float)Math.Sin(angle * Math.PI / 360), (float)Math.Cos(angle * Math.PI / 360)), 0);
            Box(.011f, .059f, .042f, .004f, .015f);
            Box(.030f, .027f, .006f, .067f, .015f, 12); // bent safety spoon follows shell
            if (lod < 2)
            {
                assembly.Add(PrimitiveGeometry.Cylinder(.004f, .022f, 10), new Float3(-.008f, .052f, 0), Quat.Identity, 2);
                // Closed pull ring made from bounded tube segments; no transparent decal.
                int count = lod == 0 ? 20 : 12;
                for (int i = 0; i < count; i++)
                {
                    double a = i * Math.PI * 2 / count, b = (i + 1) * Math.PI * 2 / count;
                    WeaponProfiles.Rod(assembly,
                        new Float3(-.021f + .013f * (float)Math.Cos(a), .047f + .013f * (float)Math.Sin(a), .010f),
                        new Float3(-.021f + .013f * (float)Math.Cos(b), .047f + .013f * (float)Math.Sin(b), .010f), .0012f, 2);
                }
                // Pressed shell equator and collar, both opaque geometry.
                assembly.Add(PrimitiveGeometry.Cylinder(.0345f, .002f, radial), new Float3(0, .002f, 0), upright, 4);
                assembly.Add(PrimitiveGeometry.Cylinder(.018f, .003f, radial), new Float3(0, .037f, 0), upright, 4);
            }
            return assembly.Build();
        }
        private static void CheckLod(int lod)
        { if (lod < 0 || lod > 3) throw new ArgumentOutOfRangeException(nameof(lod)); }
    }

    /// <summary>Closed lofts from cross-section radii, flat machined facets and metric UVs.</summary>
    internal static class WeaponProfiles
    {
        public static MeshData Loft(float[] z, float[] width, float[] height, int radial)
        {
            if (z.Length < 2 || width.Length != z.Length || height.Length != z.Length || radial < 4)
                throw new ArgumentException("Invalid weapon profile.");
            var mesh = new GeometryBuffer();
            Float3 Point(int ring, int side)
            {
                if (radial == 8)
                {
                    // Rectangular section with narrow machined corner flats, rather than a diamond tube.
                    float[] xs = { 1, .72f, -.72f, -1, -1, -.72f, .72f, 1 };
                    float[] ys = { .72f, 1, 1, .72f, -.72f, -1, -1, -.72f };
                    return new Float3(width[ring] * xs[side % radial], height[ring] * ys[side % radial], z[ring]);
                }
                double angle = (side % radial) * Math.PI * 2 / radial;
                return new Float3(width[ring] * (float)Math.Cos(angle), height[ring] * (float)Math.Sin(angle), z[ring]);
            }
            void Face(Float3 a, Float3 b, Float3 c, bool curved = false)
            {
                var u = GeometryMath.Sub(b, a); var v = GeometryMath.Sub(c, a);
                var normal = GeometryMath.Normalize(new Float3(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X));
                var tangent = GeometryMath.Normalize(u);
                var bitangent = new Float3(normal.Y * tangent.Z - normal.Z * tangent.Y,
                    normal.Z * tangent.X - normal.X * tangent.Z, normal.X * tangent.Y - normal.Y * tangent.X);
                int first = mesh.Positions.Count;
                foreach (var p in new[] { a, b, c })
                {
                    var n = normal; var t = tangent;
                    if (curved)
                    {
                        int station = Array.IndexOf(z, p.Z);
                        int lo = Math.Max(0, station - 1), hi = Math.Min(z.Length - 1, station + 1);
                        float dw = (width[hi] - width[lo]) / (z[hi] - z[lo]);
                        float dh = (height[hi] - height[lo]) / (z[hi] - z[lo]);
                        float w = width[station], h = height[station];
                        n = GeometryMath.Normalize(new Float3(p.X / (w * w), p.Y / (h * h),
                            -p.X * p.X * dw / (w * w * w) - p.Y * p.Y * dh / (h * h * h)));
                        t = GeometryMath.Normalize(GeometryMath.Sub(tangent, GeometryMath.Scale(n, GeometryMath.Dot(n, tangent))));
                    }
                    mesh.Vertex(p, n, new Float4(t.X, t.Y, t.Z, 1),
                        new Float2(GeometryMath.Dot(p, tangent), GeometryMath.Dot(p, bitangent)));
                }
                mesh.Triangle(first, first + 1, first + 2);
            }
            for (int ring = 0; ring < z.Length; ring++)
            {
                if (width[ring] <= 0 || height[ring] <= 0 || ring > 0 && z[ring] <= z[ring - 1])
                    throw new ArgumentException("Weapon profile requires positive radii and increasing stations.");
                for (int side = 0; side < radial; side++)
                {
                    if (ring + 1 < z.Length)
                    {
                        var a = Point(ring, side); var b = Point(ring, side + 1);
                        var c = Point(ring + 1, side); var d = Point(ring + 1, side + 1);
                        Face(a, b, c, radial > 8); Face(b, d, c, radial > 8);
                    }
                    if (ring == 0) Face(new Float3(0, 0, z[ring]), Point(ring, side + 1), Point(ring, side));
                    if (ring == z.Length - 1) Face(new Float3(0, 0, z[ring]), Point(ring, side), Point(ring, side + 1));
                }
            }
            return mesh.Build();
        }
        public static void Fastener(MeshAssembly mesh, float x, float y, float z, int lod, float radius = .0038f)
        {
            var rotation = new Quat(0, .70710677f, 0, .70710677f);
            mesh.Add(PrimitiveGeometry.Cylinder(radius, .002f, lod == 0 ? 10 : 6), new Float3(x, y, z), rotation, 2);
            float face = x + (x < 0 ? -.0011f : .0011f);
            mesh.Add(PrimitiveGeometry.Box(new Float3(.001f, Math.Max(.001f, radius * .30f), radius * 1.30f)), new Float3(face, y, z), Quat.Identity, 3);
        }
        public static void Rod(MeshAssembly mesh, Float3 a, Float3 b, float radius, ushort slot)
        {
            var delta = GeometryMath.Sub(b, a); float length = (float)Math.Sqrt(delta.LengthSquared);
            var direction = GeometryMath.Normalize(delta);
            // Ring rods are in XY: shortest arc from +Z is stable (dot == 0).
            var rotation = new Quat(-direction.Y * .70710677f, direction.X * .70710677f, 0, .70710677f);
            mesh.Add(PrimitiveGeometry.Cylinder(radius, length, 6), GeometryMath.Scale(GeometryMath.Add(a, b), .5f), rotation, slot);
        }
    }
}
