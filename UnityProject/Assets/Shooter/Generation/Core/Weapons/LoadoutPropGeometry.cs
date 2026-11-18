#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>
    /// ADR-111 geometry.loadout-prop 1.1.0: the combat knife and M67-style frag grenade built from the ADR-106
    /// <see cref="ShapeKit"/> kernel (filleted <see cref="ShapeKit.Slab"/> profiles, <see cref="ShapeKit.Lathe"/> turned parts,
    /// superellipse <see cref="ShapeKit.Loft"/> mouldings and swept <see cref="ShapeKit.Tube"/>s). Numeric recipes only: no
    /// model, image or authored vertex input. Knife space: handle along −Z centred on the grip, blade +Z, edge down (−Y).
    /// Grenade space: body centred on the origin, fuse up (+Y), spoon on +X, pull ring on −X/+Z.
    /// Material slots follow <c>ProductSurfaceMaterial.WeaponFinishes</c>: 0 coated metal, 1 polymer, 2 machined steel,
    /// 3 recess, 7 brushed blade steel, 8 polished edge grind, 9 olive grenade enamel, 10 yellow HE marking band.
    /// </summary>
    public static class LoadoutPropGeometry
    {
        public const ushort CoatedSlot = 0, PolymerSlot = 1, SteelSlot = 2, RecessSlot = 3,
            BladeSteelSlot = 7, BladeEdgeSlot = 8, GrenadePaintSlot = 9, MarkingBandSlot = 10;

        private static readonly Quat ZToY = new Quat(-.70710677f, 0, 0, .70710677f);
        private static readonly Quat ZToX = new Quat(0, .70710677f, 0, .70710677f);

        /// <summary>The ADR-106 LOD tables (arc, edge roll, radial, loft radial) shared with the profile arsenal.</summary>
        private readonly struct Detail
        {
            public readonly int Lod, Arc, Edge, Radial, LoftRadial;
            public Detail(int lod)
            {
                if (lod < 0 || lod > 3) throw new ArgumentOutOfRangeException(nameof(lod));
                Lod = lod;
                Arc = new[] { 4, 2, 1, 1 }[lod];
                Edge = new[] { 2, 1, 0, 0 }[lod];
                Radial = new[] { 32, 16, 10, 6 }[lod];
                LoftRadial = new[] { 28, 16, 10, 8 }[lod];
            }
            public bool Fine => Lod == 0;
            public bool Near => Lod < 2;
            public int Pick(int lod0, int lod1, int lod2, int lod3) => Lod == 0 ? lod0 : Lod == 1 ? lod1 : Lod == 2 ? lod2 : lod3;
        }

        // ------------------------------------------------------------------ knife

        // Clip-point blade side profile (metres): spine, concave clip, belly and the edge-grind line.
        private const float BladeThickness = .0040f, BladeRoll = .0005f, BladeRear = -.004f;
        private const float Spine = .0185f, EdgeLine = -.0125f, TipZ = .160f, TipY = .004f, ClipZ = .104f, BellyZ = .098f;
        private const float PlungeZ = .014f, GrindHeight = .0068f, BandEndZ = TipZ - .0025f;
        private const float FullerY = .0115f, FullerHalf = .0021f, FullerZ0 = .020f, FullerZ1 = .098f, FullerDepth = .0007f, Overlap = .0012f;

        private static float EdgeAt(float z)
        {
            if (z <= BellyZ) return EdgeLine;
            float s = (z - BellyZ) / (TipZ - BellyZ);
            return EdgeLine + (TipY - EdgeLine) * s * s;
        }

        private static float GrindAt(float z)
        {
            float s = Math.Max(0, (z - PlungeZ) / (TipZ - PlungeZ));
            return EdgeAt(z) + GrindHeight * (1 - s * s * s);
        }

        private static float ClipAt(float z)
            => z <= ClipZ ? Spine : TipY + (Spine - TipY) * (float)Math.Pow(Math.Max(0, 1 - (z - ClipZ) / (TipZ - ClipZ)), 1.6);

        private static float ClipZAt(float y)
            => ClipZ + (TipZ - ClipZ) * (1 - (float)Math.Pow((y - TipY) / (Spine - TipY), 1 / 1.6));

        public static MeshData BuildKnife(int lod)
        {
            var d = new Detail(lod);
            var a = new MeshAssembly();
            int clipSamples = d.Pick(8, 5, 3, 2), grindSamples = d.Pick(16, 9, 5, 3);

            // Blade flats: an upper and a lower filleted Slab that overlap on the centre line and part around the fuller,
            // so the fuller is a real groove with rolled walls. The overlap faces are coplanar with identical frames and UVs.
            var upper = new List<ProfileCorner>
            {
                new ProfileCorner(BladeRear, FullerY - Overlap),
                new ProfileCorner(FullerZ0, FullerY - Overlap),
                new ProfileCorner(FullerZ0, FullerY + FullerHalf, FullerHalf),
                new ProfileCorner(FullerZ1, FullerY + FullerHalf, FullerHalf),
                new ProfileCorner(FullerZ1, FullerY - Overlap)
            };
            float upperClip = ClipZAt(FullerY - Overlap);
            upper.Add(new ProfileCorner(upperClip, FullerY - Overlap));
            for (int k = clipSamples; k >= 1; k--)
            {
                float z = ClipZ + (upperClip - ClipZ) * k / (clipSamples + 1);
                upper.Add(new ProfileCorner(z, ClipAt(z)));
            }
            upper.Add(new ProfileCorner(ClipZ, Spine, .006f));
            if (d.Near)
            {
                // Spine jimping: V-notches cut into the spine ahead of the guard.
                const float j0 = .010f, j1 = .032f, depth = .0009f;
                int teeth = d.Fine ? 8 : 5;
                float pitch = (j1 - j0) / teeth;
                for (int k = teeth; k >= 1; k--)
                {
                    upper.Add(new ProfileCorner(j0 + pitch * k, Spine));
                    upper.Add(new ProfileCorner(j0 + pitch * (k - .5f), Spine - depth));
                }
                upper.Add(new ProfileCorner(j0, Spine));
            }
            upper.Add(new ProfileCorner(BladeRear, Spine));
            a.Add(ShapeKit.Slab(upper, BladeThickness, BladeRoll, d.Arc, d.Edge), new Float3(0, 0, 0), Quat.Identity, BladeSteelSlot);

            var lower = new List<ProfileCorner>
            {
                new ProfileCorner(BladeRear, FullerY + Overlap),
                new ProfileCorner(FullerZ0, FullerY + Overlap),
                new ProfileCorner(FullerZ0, FullerY - FullerHalf, FullerHalf),
                new ProfileCorner(FullerZ1, FullerY - FullerHalf, FullerHalf),
                new ProfileCorner(FullerZ1, FullerY + Overlap)
            };
            float lowerClip = ClipZAt(FullerY + Overlap);
            lower.Add(new ProfileCorner(lowerClip, FullerY + Overlap));
            for (int k = 1; k <= clipSamples; k++)
            {
                float z = lowerClip + (TipZ - lowerClip) * k / (clipSamples + 1);
                lower.Add(new ProfileCorner(z, ClipAt(z)));
            }
            lower.Add(new ProfileCorner(TipZ, TipY, .0004f));
            for (int k = 1; k <= grindSamples; k++)
            {
                // Denser toward the tip, where the grind line turns up through the belly.
                double t = (double)k / (grindSamples + 1);
                float z = (float)(TipZ - (TipZ - PlungeZ) * Math.Pow(t, 1.35));
                lower.Add(new ProfileCorner(z, GrindAt(z)));
            }
            lower.Add(new ProfileCorner(PlungeZ + .0005f, GrindAt(PlungeZ), .0022f)); // curved plunge
            lower.Add(new ProfileCorner(PlungeZ + .0005f, EdgeLine, .0004f));
            lower.Add(new ProfileCorner(BladeRear, EdgeLine));
            a.Add(ShapeKit.Slab(lower, BladeThickness, BladeRoll, d.Arc, d.Edge), new Float3(0, 0, 0), Quat.Identity, BladeSteelSlot);

            // Fuller floor: a thinner stadium plate behind the two flats.
            float floorRadius = FullerHalf + .0008f;
            a.Add(ShapeKit.Slab(new[]
            {
                new ProfileCorner(FullerZ0 - .001f, FullerY - floorRadius, floorRadius * .9f),
                new ProfileCorner(FullerZ1 + .001f, FullerY - floorRadius, floorRadius * .9f),
                new ProfileCorner(FullerZ1 + .001f, FullerY + floorRadius, floorRadius * .9f),
                new ProfileCorner(FullerZ0 - .001f, FullerY + floorRadius, floorRadius * .9f)
            }, BladeThickness - 2 * FullerDepth, .0003f, d.Arc, d.Edge), new Float3(0, 0, 0), Quat.Identity, BladeSteelSlot);

            // Polished edge-grind bevel: its own swept solid and material slot.
            a.Add(GrindBand(d.Pick(28, 16, 8, 5)), new Float3(0, 0, 0), Quat.Identity, BladeEdgeSlot);

            // Crossguard: filleted side profile with a forward-hooked lower quillon, rolled hard on every edge.
            a.Add(ShapeKit.Slab(new[]
            {
                new ProfileCorner(-.0065f, .026f, .0028f),
                new ProfileCorner(-.0005f, .026f, .0028f),
                new ProfileCorner(-.0005f, -.019f, .004f),
                new ProfileCorner(.0035f, -.027f, .002f),
                new ProfileCorner(.0015f, -.031f, .002f),
                new ProfileCorner(-.0065f, -.029f, .004f)
            }, .0225f, .0028f, d.Arc, d.Edge), new Float3(0, 0, 0), Quat.Identity, CoatedSlot);

            a.Add(Handle(d), new Float3(0, 0, 0), Quat.Identity, PolymerSlot);

            // Turned pommel cap, wider than the grip, with a cross-drilled lanyard hole.
            a.Add(ShapeKit.Lathe(new[]
            {
                new Float2(.0040f, -.1075f), new Float2(.0090f, -.1068f), new Float2(.0122f, -.1048f), new Float2(.0134f, -.1020f),
                new Float2(.0136f, -.0985f), new Float2(.0134f, -.0950f), new Float2(.0128f, -.0925f), new Float2(.0122f, -.0900f),
                new Float2(.0118f, -.0885f)
            }, d.Radial), new Float3(0, 0, 0), Quat.Identity, CoatedSlot);
            if (d.Near)
                a.Add(ShapeKit.Lathe(new[] { new Float2(.0021f, -.0141f), new Float2(.0021f, .0141f) }, d.Pick(12, 8, 6, 6)),
                    new Float3(0, 0, -.0985f), ZToX, RecessSlot);
            return a.Build();
        }

        /// <summary>
        /// Moulded grip loft from the pommel to the guard: palm swell on the spine side, four finger grooves on the edge
        /// side and a flare into the guard.
        /// </summary>
        private static MeshData Handle(Detail d)
        {
            const float rear = -.0915f, front = -.005f;
            int stations = d.Pick(28, 16, 8, 6);
            bool grooves = d.Lod < 3;
            var sections = new List<ShapeKit.Section>();
            for (int i = 0; i <= stations; i++)
            {
                double t = (double)i / stations;
                double swell = Math.Exp(-Math.Pow((t - .45) / .25, 2));
                double neck = t < .12 ? (1 - t / .12) : 0; // narrows into the pommel collar
                double flare = t > .9 ? (t - .9) / .1 : 0;
                double groove = grooves && t > .16 && t < .92 ? .0016 * (1 - Math.Cos(2 * Math.PI * 4 * (t - .16) / .76)) / 2 : 0;
                double top = .0145 + .0012 * swell - .0028 * neck + .0008 * flare;
                double bottom = -(.0150 + .0010 * swell - .0030 * neck + .0010 * flare) + groove;
                double halfWidth = .0098 + .0016 * swell - .0006 * neck + .0002 * flare;
                sections.Add(new ShapeKit.Section((float)(rear + (front - rear) * t), (float)((top + bottom) / 2), (float)halfWidth,
                    (float)((top - bottom) / 2), 2.6f));
            }
            return ShapeKit.Loft(sections, d.Pick(24, 14, 10, 8));
        }

        /// <summary>
        /// The edge bevel as one closed solid swept along the blade: a trapezoid section from inside the flats at the grind
        /// line down to a narrow edge land, with crease-split faces (bevels, edge land, hidden top) and capped ends.
        /// </summary>
        private static MeshData GrindBand(int stations)
        {
            var corners = new Float3[stations, 4];
            for (int i = 0; i < stations; i++)
            {
                double t = (double)i / (stations - 1);
                float z = (float)(PlungeZ + (BandEndZ - PlungeZ) * (1 - Math.Pow(1 - t, 1.4)));
                float bottom = EdgeAt(z), grind = GrindAt(z);
                float top = grind + Math.Min(.0010f, .45f * (ClipAt(z) - grind));
                const float topHalf = BladeThickness * .46f, edgeHalf = .00022f;
                corners[i, 0] = new Float3(topHalf, top, z);
                corners[i, 1] = new Float3(edgeHalf, bottom, z);
                corners[i, 2] = new Float3(-edgeHalf, bottom, z);
                corners[i, 3] = new Float3(-topHalf, top, z);
            }
            var mesh = new ShapeBuffer();
            for (int face = 0; face < 4; face++)
            {
                int c0 = face, c1 = (face + 1) % 4;
                var strip = new int[stations, 2];
                for (int i = 0; i < stations; i++)
                {
                    int ip = Math.Min(stations - 1, i + 1), im = Math.Max(0, i - 1);
                    var p0 = corners[i, c0]; var p1 = corners[i, c1];
                    double ax = (corners[ip, c0].X + corners[ip, c1].X - corners[im, c0].X - corners[im, c1].X) * .5;
                    double ay = (corners[ip, c0].Y + corners[ip, c1].Y - corners[im, c0].Y - corners[im, c1].Y) * .5;
                    double az = (corners[ip, c0].Z + corners[ip, c1].Z - corners[im, c0].Z - corners[im, c1].Z) * .5;
                    double cx = (double)p1.X - p0.X, cy = (double)p1.Y - p0.Y, cz = (double)p1.Z - p0.Z;
                    double nx = ay * cz - az * cy, ny = az * cx - ax * cz, nz = ax * cy - ay * cx;
                    double mx = (p0.X + p1.X) * .5, my = (p0.Y + p1.Y) * .5;
                    double ox = mx, oy = my - (corners[i, 0].Y + corners[i, 1].Y) * .5;
                    if (nx * ox + ny * oy < 0) { nx = -nx; ny = -ny; nz = -nz; }
                    var normal = ShapeKit.Unit(nx, ny, nz);
                    var tangent = ShapeKit.Tangent(normal, ax, ay, az, cx, cy, cz);
                    float across = (float)Math.Sqrt(cx * cx + cy * cy + cz * cz);
                    strip[i, 0] = mesh.Positions.Count;
                    mesh.Vertex(p0, normal, tangent, new Float2(p0.Z, 0));
                    strip[i, 1] = mesh.Positions.Count;
                    mesh.Vertex(p1, normal, tangent, new Float2(p1.Z, across));
                }
                for (int i = 0; i + 1 < stations; i++) Quad(mesh, strip[i, 0], strip[i, 1], strip[i + 1, 0], strip[i + 1, 1]);
            }
            for (int end = 0; end < 2; end++)
            {
                int i = end == 0 ? 0 : stations - 1;
                var normal = new Float3(0, 0, end == 0 ? -1 : 1);
                var tangent = new Float4(1, 0, 0, end == 0 ? -1 : 1);
                int first = mesh.Positions.Count;
                for (int c = 0; c < 4; c++) mesh.Vertex(corners[i, c], normal, tangent, new Float2(corners[i, c].X, corners[i, c].Y));
                Facing(mesh, first, first + 1, first + 2, normal);
                Facing(mesh, first, first + 2, first + 3, normal);
            }
            return mesh.Build();
        }

        // ------------------------------------------------------------------ grenade

        private const float BodyRadius = .0318f, BodyBottom = .0322f, BodyTop = .0336f;

        /// <summary>M67-style egg body: slightly taller above the equator than below (θ in degrees, 0 = equator).</summary>
        private static Float2 BodyPoint(double degrees, double offset = 0)
        {
            double t = degrees * Math.PI / 180, c = Math.Cos(t), s = Math.Sin(t);
            double rz = degrees < 0 ? BodyBottom : BodyTop;
            double nr = c / BodyRadius, nz = s / rz, l = Math.Sqrt(nr * nr + nz * nz);
            return new Float2((float)(BodyRadius * c + offset * nr / l), (float)(rz * s + offset * nz / l));
        }

        public static MeshData BuildGrenade(int lod)
        {
            var d = new Detail(lod);
            var a = new MeshAssembly();
            var origin = new Float3(0, 0, 0);
            int segments = d.Radial;

            // Body: egg profile from the small flat base to the fuse collar; the lathe creases where the neck turns in.
            double neck = Math.Acos(.0125 / BodyRadius) * 180 / Math.PI;
            int samples = d.Pick(18, 12, 8, 5);
            var body = new List<Float2>();
            for (int k = 0; k <= samples; k++) body.Add(BodyPoint(-82 + (neck + 82) * k / samples));
            float neckZ = body[body.Count - 1].Y;
            body.Add(new Float2(.0118f, neckZ + .0004f));
            body.Add(new Float2(.0110f, neckZ + .0010f));
            a.Add(ShapeKit.Lathe(body, segments), origin, ZToY, GrenadePaintSlot);

            // Painted yellow HE marking band on the upper shoulder: a 0.22 mm skin whose end discs sit inside the body.
            {
                int steps = d.Pick(5, 4, 2, 2);
                var band = new List<Float2> { Inset(BodyPoint(40), .0012f) };
                for (int k = 0; k <= steps; k++) band.Add(BodyPoint(40 + 12.0 * k / steps, .00022));
                band.Add(Inset(BodyPoint(52), .0012f));
                a.Add(ShapeKit.Lathe(band, segments), origin, ZToY, MarkingBandSlot);
            }
            if (d.Near)
            {
                // Rolled body seam just below the equator: hard-edged by the lathe crease angle.
                const float seam = -.0015f;
                a.Add(ShapeKit.Lathe(new[]
                {
                    new Float2(BodyRadius - .0010f, seam - .0009f), new Float2(BodyRadius + .0004f, seam - .0006f),
                    new Float2(BodyRadius + .0004f, seam + .0006f), new Float2(BodyRadius - .0010f, seam + .0009f)
                }, segments), origin, ZToY, GrenadePaintSlot);
            }

            // M204-style fuse: threaded collar, shoulder, striker body and domed head in one turned profile.
            a.Add(ShapeKit.Lathe(new[]
            {
                new Float2(.0090f, .0255f), new Float2(.0126f, .0262f), new Float2(.0128f, .0335f), new Float2(.0112f, .0342f),
                new Float2(.0102f, .0350f), new Float2(.0102f, .0520f), new Float2(.0094f, .0532f), new Float2(.0094f, .0575f),
                new Float2(.0080f, .0588f), new Float2(.0062f, .0600f), new Float2(.0040f, .0604f)
            }, segments), origin, ZToY, CoatedSlot);

            // Curved safety lever: hooked over the fuse head, then following the body down past the equator.
            {
                var points = new List<Float3>
                {
                    new Float3(-.0112f, .0545f, 0), new Float3(-.0106f, .0598f, 0), new Float3(-.0058f, .0630f, 0),
                    new Float3(.0030f, .0636f, 0), new Float3(.0112f, .0610f, 0), new Float3(.0142f, .0540f, 0), new Float3(.0158f, .0440f, 0)
                };
                int bodyPoints = d.Pick(7, 5, 4, 3);
                for (int k = 0; k <= bodyPoints; k++)
                {
                    double theta = 60 - 96.0 * k / bodyPoints;
                    double gap = .0042 - .0022 * k / bodyPoints;
                    var p = BodyPoint(theta, gap);
                    points.Add(new Float3(p.X, p.Y, 0));
                }
                var path = ShapeKit.Spline(points, d.Pick(3, 2, 2, 1));
                var radii = new Float2[path.Count];
                for (int i = 0; i < radii.Length; i++)
                {
                    double t = (double)i / (radii.Length - 1);
                    double width = t < .3 ? .0058 + .0010 * t / .3 : .0068 - .0006 * (t - .3) / .7;
                    radii[i] = new Float2((float)width, .0008f);
                }
                a.Add(ShapeKit.Tube(path, radii, new Float3(0, 0, 1), d.Pick(14, 10, 8, 6), d.Near ? 2 : 0), origin, Quat.Identity, GrenadePaintSlot);
            }

            if (d.Near)
            {
                // Lever hinge: two lugs either side of the spoon and the pivot pin between them.
                foreach (float side in new[] { -.0080f, .0080f })
                    a.Add(ShapeKit.Slab(new[]
                    {
                        new ProfileCorner(.0040f, .0498f, .0010f), new ProfileCorner(.0132f, .0505f, .0012f),
                        new ProfileCorner(.0136f, .0590f, .0015f), new ProfileCorner(.0055f, .0598f, .0012f)
                    }, .0012f, .0003f, d.Arc, d.Edge), new Float3(0, 0, side), ZToX, CoatedSlot);
                a.Add(ShapeKit.Lathe(new[] { new Float2(.0010f, -.0092f), new Float2(.0010f, .0092f) }, d.Pick(10, 8, 6, 6)),
                    new Float3(.0105f, .0555f, 0), Quat.Identity, SteelSlot);
            }

            if (d.Lod < 3)
            {
                // Pull pin through the fuse head (split end bent back) and the seamless pull ring threaded on its eye.
                var pin = ShapeKit.Spline(new[]
                {
                    new Float3(.0012f, .0540f, -.0140f), new Float3(-.0026f, .0540f, -.0118f), new Float3(-.0045f, .0540f, -.0088f),
                    new Float3(-.0045f, .0540f, .0020f), new Float3(-.0045f, .0540f, .0126f)
                }, d.Pick(3, 2, 1, 1));
                var pinRadii = new Float2[pin.Count];
                for (int i = 0; i < pinRadii.Length; i++) pinRadii[i] = new Float2(.0009f, .0009f);
                a.Add(ShapeKit.Tube(pin, pinRadii, new Float3(0, 1, 0), d.Pick(10, 8, 6, 6), d.Pick(2, 1, 1, 1)), origin, Quat.Identity, SteelSlot);

                const float ringRadius = .0115f;
                var centre = new Float3(-.0150f, .0493f, .0136f);
                int count = d.Pick(28, 18, 12, 12);
                var loop = new List<Float3>(count);
                for (int i = 0; i < count; i++)
                {
                    double angle = 2 * Math.PI * i / count;
                    loop.Add(new Float3(centre.X + ringRadius * (float)Math.Cos(angle), centre.Y + ringRadius * (float)Math.Sin(angle), centre.Z));
                }
                a.Add(ShapeKit.ClosedTube(loop, .0011f, new Float3(0, 0, 1), d.Pick(10, 8, 6, 6)), origin, Quat.Identity, SteelSlot);
            }
            return a.Build();
        }

        private static Float2 Inset(Float2 point, float depth) => new Float2(point.X - depth, point.Y);

        // ------------------------------------------------------------------ winding helpers

        private static void Quad(ShapeBuffer mesh, int a, int b, int c, int d)
        {
            double agreement = Agreement(mesh, a, b, c) + Agreement(mesh, b, d, c);
            if (agreement >= 0) { mesh.Triangle(a, b, c); mesh.Triangle(b, d, c); }
            else { mesh.Triangle(a, c, b); mesh.Triangle(b, c, d); }
        }

        private static double Agreement(ShapeBuffer mesh, int a, int b, int c)
        {
            var pa = mesh.Positions[a]; var pb = mesh.Positions[b]; var pc = mesh.Positions[c];
            double ux = (double)pb.X - pa.X, uy = (double)pb.Y - pa.Y, uz = (double)pb.Z - pa.Z;
            double vx = (double)pc.X - pa.X, vy = (double)pc.Y - pa.Y, vz = (double)pc.Z - pa.Z;
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            var na = mesh.Normal(a); var nb = mesh.Normal(b); var nc = mesh.Normal(c);
            return cx * ((double)na.X + nb.X + nc.X) + cy * ((double)na.Y + nb.Y + nc.Y) + cz * ((double)na.Z + nb.Z + nc.Z);
        }

        private static void Facing(ShapeBuffer mesh, int a, int b, int c, Float3 facing)
        {
            var pa = mesh.Positions[a]; var pb = mesh.Positions[b]; var pc = mesh.Positions[c];
            double ux = (double)pb.X - pa.X, uy = (double)pb.Y - pa.Y, uz = (double)pb.Z - pa.Z;
            double vx = (double)pc.X - pa.X, vy = (double)pc.Y - pa.Y, vz = (double)pc.Z - pa.Z;
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            if (cx * facing.X + cy * facing.Y + cz * facing.Z >= 0) mesh.Triangle(a, b, c); else mesh.Triangle(a, c, b);
        }
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
