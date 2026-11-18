#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>One corner of a closed 2D outline and the fillet radius rounding it (metres, 0 = sharp).</summary>
    public readonly struct ProfileCorner
    {
        public float A { get; }
        public float B { get; }
        public float Radius { get; }
        public ProfileCorner(float a, float b, float radius = 0) { A = a; B = b; Radius = radius; }
    }

    /// <summary>
    /// ADR-106 hard-surface and organic modelling kernel. Every builder returns one closed, outward-wound solid with
    /// analytic or grid-derived smooth normals, so parts read as machined/moulded shapes rather than stacked boxes.
    /// Pure and deterministic: no randomness, no authored vertex arrays (only numeric corner/station recipes).
    /// </summary>
    public static class ShapeKit
    {
        // ------------------------------------------------------------------ outlines

        /// <summary>Fillets every corner of a closed polygon and returns a counter-clockwise polyline in (A, B).</summary>
        public static List<Float2> RoundedOutline(IReadOnlyList<ProfileCorner> corners, int arcSegments)
        {
            if (corners == null || corners.Count < 3) throw new ArgumentException("Outline needs at least three corners.");
            if (arcSegments < 1 || arcSegments > 32) throw new ArgumentException("Arc segments must be 1..32.");
            int n = corners.Count;
            var result = new List<Float2>();
            for (int i = 0; i < n; i++)
            {
                var prev = corners[(i + n - 1) % n]; var c = corners[i]; var next = corners[(i + 1) % n];
                double ux = prev.A - c.A, uy = prev.B - c.B, vx = next.A - c.A, vy = next.B - c.B;
                double lu = Math.Sqrt(ux * ux + uy * uy), lv = Math.Sqrt(vx * vx + vy * vy);
                if (lu < 1e-6 || lv < 1e-6) throw new ArgumentException("Outline has coincident corners.");
                ux /= lu; uy /= lu; vx /= lv; vy /= lv;
                double cos = Math.Max(-1, Math.Min(1, ux * vx + uy * vy));
                double cross = ux * vy - uy * vx;
                if (Math.Abs(cross) < 1e-6 && cos < 0) continue; // straight-through corner adds nothing
                double half = Math.Acos(cos) / 2;
                if (c.Radius <= 0 || half < 1e-4) { result.Add(new Float2(c.A, c.B)); continue; }
                double t = c.Radius / Math.Tan(half);
                double limit = .48 * Math.Min(lu, lv);
                double r = c.Radius;
                if (t > limit) { t = limit; r = t * Math.Tan(half); }
                double bx = ux + vx, by = uy + vy, lb = Math.Sqrt(bx * bx + by * by);
                bx /= lb; by /= lb;
                double d = r / Math.Sin(half);
                double cx = c.A + bx * d, cy = c.B + by * d;
                double t1x = c.A + ux * t, t1y = c.B + uy * t, t2x = c.A + vx * t, t2y = c.B + vy * t;
                double a1 = Math.Atan2(t1y - cy, t1x - cx), a2 = Math.Atan2(t2y - cy, t2x - cx);
                double delta = a2 - a1;
                while (delta > Math.PI) delta -= 2 * Math.PI;
                while (delta <= -Math.PI) delta += 2 * Math.PI;
                int steps = Math.Max(1, (int)Math.Ceiling(arcSegments * Math.Abs(delta) / (Math.PI / 2) - 1e-9));
                for (int s = 0; s <= steps; s++)
                {
                    double a = a1 + delta * s / steps;
                    result.Add(new Float2((float)(cx + r * Math.Cos(a)), (float)(cy + r * Math.Sin(a))));
                }
            }
            return Clean(result);
        }

        private static List<Float2> Clean(List<Float2> points)
        {
            var clean = new List<Float2>();
            foreach (var p in points)
                if (clean.Count == 0 || Dist(clean[clean.Count - 1], p) > 2e-5) clean.Add(p);
            while (clean.Count > 1 && Dist(clean[0], clean[clean.Count - 1]) <= 2e-5) clean.RemoveAt(clean.Count - 1);
            // Drop exactly collinear vertices so flat caps never need zero-area triangles.
            bool changed = true;
            while (changed && clean.Count > 3)
            {
                changed = false;
                for (int i = 0; i < clean.Count && clean.Count > 3; i++)
                {
                    var a = clean[(i + clean.Count - 1) % clean.Count]; var b = clean[i]; var c = clean[(i + 1) % clean.Count];
                    double cross = ((double)b.X - a.X) * ((double)c.Y - b.Y) - ((double)b.Y - a.Y) * ((double)c.X - b.X);
                    double scale = Dist(a, b) * Dist(b, c);
                    if (Math.Abs(cross) <= 1e-7 * scale) { clean.RemoveAt(i); changed = true; i--; }
                }
            }
            if (clean.Count < 3) throw new ArgumentException("Outline collapsed.");
            if (SignedArea(clean) < 0) clean.Reverse();
            return clean;
        }

        private static double Dist(Float2 a, Float2 b) { double x = (double)a.X - b.X, y = (double)a.Y - b.Y; return Math.Sqrt(x * x + y * y); }
        private static double SignedArea(IReadOnlyList<Float2> p)
        {
            double area = 0;
            for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; area += (double)a.X * b.Y - (double)b.X * a.Y; }
            return area * .5;
        }

        // ------------------------------------------------------------------ profile extrusion

        /// <summary>
        /// Extrudes a side-view outline (A = +Z forward, B = +Y up) symmetrically along X with quarter-round edge
        /// rolls of <paramref name="edgeRadius"/> between the side faces and the wall. <paramref name="edgeSegments"/>
        /// 0 gives sharp edges. Outline corners must be filleted at least as much as the edge radius where concave.
        /// </summary>
        public static MeshData Extrude(IReadOnlyList<Float2> outline, float width, float edgeRadius, int edgeSegments)
        {
            // Edge rolls wider than a corner fillet fold the inset cap; halve the roll until the cap is simple.
            for (int attempt = 0; ; attempt++)
            {
                try { return ExtrudeOnce(outline, width, edgeRadius, edgeSegments); }
                catch (ArgumentException) when (attempt < 6 && edgeSegments > 0 && edgeRadius > .0002f) { edgeRadius *= .5f; }
            }
        }

        /// <summary>Rounds every corner at least as much as the edge roll, then extrudes: the safe default for parts.</summary>
        public static MeshData Slab(IReadOnlyList<ProfileCorner> corners, float width, float edgeRadius, int arcSegments, int edgeSegments)
        {
            var rounded = new ProfileCorner[corners.Count];
            float minimum = edgeSegments > 0 ? Math.Min(edgeRadius, width * .45f) * 1.05f : 0;
            for (int i = 0; i < rounded.Length; i++) rounded[i] = new ProfileCorner(corners[i].A, corners[i].B, Math.Max(corners[i].Radius, minimum));
            return Extrude(RoundedOutline(rounded, arcSegments), width, edgeRadius, edgeSegments);
        }

        private static MeshData ExtrudeOnce(IReadOnlyList<Float2> outline, float width, float edgeRadius, int edgeSegments)
        {
            if (outline == null || outline.Count < 3) throw new ArgumentException("Extrusion outline.");
            GeometryMath.Dimension(width);
            if (edgeSegments < 0 || edgeSegments > 12 || !GeometryMath.Finite(edgeRadius) || edgeRadius < 0) throw new ArgumentException("Extrusion edge.");
            var o = new List<Float2>(outline);
            if (SignedArea(o) < 0) o.Reverse();
            int n = o.Count;
            // Outward vertex normals with miter compensation, in (A, B).
            var na = new double[n]; var nb = new double[n]; var miter = new double[n];
            for (int i = 0; i < n; i++)
            {
                var p0 = o[(i + n - 1) % n]; var p1 = o[i]; var p2 = o[(i + 1) % n];
                double e0a = (double)p1.X - p0.X, e0b = (double)p1.Y - p0.Y, l0 = Math.Sqrt(e0a * e0a + e0b * e0b);
                double e1a = (double)p2.X - p1.X, e1b = (double)p2.Y - p1.Y, l1 = Math.Sqrt(e1a * e1a + e1b * e1b);
                double n0a = e0b / l0, n0b = -e0a / l0, n1a = e1b / l1, n1b = -e1a / l1;
                double sa = n0a + n1a, sb = n0b + n1b, ls = Math.Sqrt(sa * sa + sb * sb);
                if (ls < 1e-9) { sa = n1a; sb = n1b; ls = 1; }
                na[i] = sa / ls; nb[i] = sb / ls;
                miter[i] = 1 / Math.Max(.55, na[i] * n1a + nb[i] * n1b);
            }
            float r = edgeSegments == 0 ? 0 : Math.Min(edgeRadius, width * .45f);
            int segs = r > 0 ? edgeSegments : 0;
            // Cross-section rows from the -X side face to the +X side face.
            var rowX = new List<double>(); var rowOffset = new List<double>(); var rowSin = new List<double>(); var rowCos = new List<double>();
            double half = width * .5;
            if (segs == 0)
            {
                rowX.Add(-half); rowOffset.Add(0); rowSin.Add(0); rowCos.Add(1);
                rowX.Add(half); rowOffset.Add(0); rowSin.Add(0); rowCos.Add(1);
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int k = 0; k <= segs; k++)
                    {
                        double phi = side < 0 ? -Math.PI / 2 + Math.PI / 2 * k / segs : Math.PI / 2 * k / segs;
                        double s = Math.Sin(phi), c = Math.Cos(phi);
                        if (side < 0 && k == 0) { s = -1; c = 0; }
                        if (side > 0 && k == segs) { s = 1; c = 0; }
                        rowX.Add(side * (half - r) + r * s); rowOffset.Add(r * c - r); rowSin.Add(s); rowCos.Add(c);
                    }
            }
            int rows = rowX.Count;
            var mesh = new ShapeBuffer();
            // Arc-length U along the outline.
            var u = new double[n + 1];
            for (int i = 1; i <= n; i++) u[i] = u[i - 1] + Dist(o[i - 1], o[i % n]);
            var grid = new int[rows, n + 1];
            for (int k = 0; k < rows; k++)
            {
                double v = rowX[k] + half;
                for (int i = 0; i <= n; i++)
                {
                    int j = i % n;
                    double off = rowOffset[k] * miter[j];
                    var position = new Float3((float)rowX[k], (float)(o[j].Y + off * nb[j]), (float)(o[j].X + off * na[j]));
                    Float3 normal;
                    if (segs == 0) normal = Unit(0, nb[j], na[j]);
                    else normal = Unit(rowSin[k], rowCos[k] * nb[j], rowCos[k] * na[j]);
                    // Tangent follows the outline direction (+U).
                    var q0 = o[(j + n - 1) % n]; var q2 = o[(j + 1) % n];
                    var tangent = Tangent(normal, 0, (double)q2.Y - q0.Y, (double)q2.X - q0.X, 1, 0, 0);
                    grid[k, i] = mesh.Positions.Count;
                    mesh.Vertex(position, normal, tangent, new Float2((float)u[i], (float)v));
                }
            }
            for (int k = 0; k + 1 < rows; k++)
                for (int i = 0; i < n; i++)
                    Quad(mesh, grid[k, i], grid[k, i + 1], grid[k + 1, i], grid[k + 1, i + 1]);
            // Flat side caps on the inset outline (row 0 and the last row).
            var inset = new List<Float2>(n);
            double capOffset = rowOffset[0];
            for (int i = 0; i < n; i++)
            {
                double off = capOffset * miter[i];
                inset.Add(new Float2((float)(o[i].X + off * na[i]), (float)(o[i].Y + off * nb[i])));
            }
            var triangles = EarClip(inset);
            for (int side = -1; side <= 1; side += 2)
            {
                int first = mesh.Positions.Count;
                var normal = new Float3(side, 0, 0);
                var tangent = new Float4(0, 0, 1, side < 0 ? 1 : -1);
                for (int i = 0; i < n; i++)
                {
                    var p = inset[i];
                    int k = side < 0 ? 0 : rows - 1;
                    var source = grid[k, i];
                    mesh.Vertex(mesh.Positions[source], normal, tangent, new Float2(p.X, p.Y));
                }
                for (int t = 0; t < triangles.Count; t += 3)
                {
                    int a = first + triangles[t], b = first + triangles[t + 1], c = first + triangles[t + 2];
                    // CCW in (A, B) faces -X once mapped to (x, y = B, z = A).
                    if (side < 0) mesh.Triangle(a, b, c); else mesh.Triangle(a, c, b);
                }
            }
            return mesh.Build();
        }

        // ------------------------------------------------------------------ lathe

        /// <summary>
        /// Revolves a (radius, z) profile around +Z. Trace the profile from the rear axis side to the front axis side over
        /// the outside surface; the rear end is closed with a -Z disc and the front end with a +Z disc. Turns sharper than
        /// <paramref name="creaseDegrees"/> become hard edges. All radii must be positive.
        /// </summary>
        public static MeshData Lathe(IReadOnlyList<Float2> profile, int segments, float creaseDegrees = 38)
        {
            if (profile == null || profile.Count < 2) throw new ArgumentException("Lathe profile.");
            if (segments < 3 || segments > 128) throw new ArgumentException("Lathe segments.");
            int m = profile.Count;
            foreach (var p in profile) if (!(p.X > .0002f) || !GeometryMath.Finite(p.Y)) throw new ArgumentException("Lathe radii must be positive.");
            var cos = new double[segments + 1]; var sin = new double[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double a = 2 * Math.PI * (i % segments) / segments;
                cos[i] = i % segments == 0 ? 1 : Math.Cos(a); sin[i] = i % segments == 0 ? 0 : Math.Sin(a);
            }
            // Segment outward normals in (r, z): (dz, -dr).
            var segR = new double[m - 1]; var segZ = new double[m - 1]; var segLen = new double[m - 1];
            for (int s = 0; s < m - 1; s++)
            {
                double dr = (double)profile[s + 1].X - profile[s].X, dz = (double)profile[s + 1].Y - profile[s].Y;
                double l = Math.Sqrt(dr * dr + dz * dz);
                if (l < 1e-6) throw new ArgumentException("Lathe profile has coincident points.");
                segR[s] = dz / l; segZ[s] = -dr / l; segLen[s] = l;
            }
            double crease = Math.Cos(creaseDegrees * Math.PI / 180);
            var mesh = new ShapeBuffer();
            double rMax = 0; foreach (var p in profile) rMax = Math.Max(rMax, p.X);
            double v = 0;
            int[]? previous = null;
            for (int s = 0; s < m - 1; s++)
            {
                // Each segment owns two rings unless it shares a smooth vertex with its neighbour.
                bool smoothStart = s > 0 && segR[s - 1] * segR[s] + segZ[s - 1] * segZ[s] >= crease;
                int[] start = smoothStart && previous != null ? previous
                    : Ring(mesh, profile[s], (segR[s], segZ[s]), cos, sin, segments, v, rMax);
                v += segLen[s];
                bool smoothEnd = s + 1 < m - 1 && segR[s] * segR[s + 1] + segZ[s] * segZ[s + 1] >= crease;
                var endNormal = smoothEnd ? Average(segR[s], segZ[s], segR[s + 1], segZ[s + 1]) : (segR[s], segZ[s]);
                int[] end = Ring(mesh, profile[s + 1], endNormal, cos, sin, segments, v, rMax);
                for (int i = 0; i < segments; i++) Quad(mesh, start[i], start[i + 1], end[i], end[i + 1]);
                previous = end;
            }
            Disc(mesh, profile[0], -1, cos, sin, segments);
            Disc(mesh, profile[m - 1], 1, cos, sin, segments);
            return mesh.Build();
        }

        private static (double, double) Average(double r0, double z0, double r1, double z1)
        {
            double r = r0 + r1, z = z0 + z1, l = Math.Sqrt(r * r + z * z);
            return l < 1e-9 ? (r1, z1) : (r / l, z / l);
        }

        private static int[] Ring(ShapeBuffer mesh, Float2 point, (double r, double z) normal, double[] cos, double[] sin, int segments, double v, double rMax)
        {
            var ring = new int[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                var n = Unit(normal.r * cos[i], normal.r * sin[i], normal.z);
                var tangent = Tangent(n, -sin[i], cos[i], 0, 0, 0, 1);
                ring[i] = mesh.Positions.Count;
                mesh.Vertex(new Float3((float)(point.X * cos[i]), (float)(point.X * sin[i]), point.Y), n, tangent,
                    new Float2((float)(2 * Math.PI * rMax * i / segments), (float)v));
            }
            return ring;
        }

        private static void Disc(ShapeBuffer mesh, Float2 point, int side, double[] cos, double[] sin, int segments)
        {
            var normal = new Float3(0, 0, side); var tangent = new Float4(1, 0, 0, side);
            int center = mesh.Positions.Count;
            mesh.Vertex(new Float3(0, 0, point.Y), normal, tangent, new Float2(0, 0));
            int first = mesh.Positions.Count;
            for (int i = 0; i < segments; i++)
                mesh.Vertex(new Float3((float)(point.X * cos[i]), (float)(point.X * sin[i]), point.Y), normal, tangent,
                    new Float2((float)(point.X * cos[i]), (float)(point.X * sin[i])));
            for (int i = 0; i < segments; i++)
            {
                int a = first + i, b = first + (i + 1) % segments;
                if (side > 0) mesh.Triangle(center, a, b); else mesh.Triangle(center, b, a);
            }
        }

        // ------------------------------------------------------------------ section loft

        /// <summary>One superellipse section of a <see cref="Loft"/>: centre height, half extents and squareness (2 = ellipse).</summary>
        public readonly struct Section
        {
            public float Z { get; }
            public float Y { get; }
            public float HalfWidth { get; }
            public float HalfHeight { get; }
            public float Squareness { get; }
            public Section(float z, float y, float halfWidth, float halfHeight, float squareness = 4)
            { Z = z; Y = y; HalfWidth = halfWidth; HalfHeight = halfHeight; Squareness = squareness; }
        }

        /// <summary>Smooth loft through superellipse sections along +Z with flat end discs. Normals come from the grid.</summary>
        public static MeshData Loft(IReadOnlyList<Section> sections, int radial)
        {
            if (sections == null || sections.Count < 2) throw new ArgumentException("Loft sections.");
            if (radial < 6 || radial > 96) throw new ArgumentException("Loft radial segments.");
            int m = sections.Count;
            for (int i = 0; i < m; i++)
            {
                var s = sections[i];
                if (!(s.HalfWidth > .0003f) || !(s.HalfHeight > .0003f) || !(s.Squareness >= 2 && s.Squareness <= 12) || i > 0 && !(s.Z > sections[i - 1].Z))
                    throw new ArgumentException("Loft section " + i + " is invalid.");
            }
            var pos = new double[m, radial, 3];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < radial; j++)
                {
                    double a = 2 * Math.PI * j / radial, c = j == 0 ? 1 : Math.Cos(a), s = j == 0 ? 0 : Math.Sin(a);
                    double e = 2 / sections[i].Squareness;
                    pos[i, j, 0] = sections[i].HalfWidth * Math.Sign(c) * Math.Pow(Math.Abs(c), e);
                    pos[i, j, 1] = sections[i].Y + sections[i].HalfHeight * Math.Sign(s) * Math.Pow(Math.Abs(s), e);
                    pos[i, j, 2] = sections[i].Z;
                }
            var mesh = new ShapeBuffer();
            var grid = new int[m, radial + 1];
            double vAcc = 0;
            for (int i = 0; i < m; i++)
            {
                if (i > 0) vAcc += sections[i].Z - sections[i - 1].Z;
                double uAcc = 0;
                for (int j = 0; j <= radial; j++)
                {
                    int jj = j % radial, jp = (jj + 1) % radial, jm = (jj + radial - 1) % radial;
                    int ip = Math.Min(m - 1, i + 1), im = Math.Max(0, i - 1);
                    double tx = pos[i, jp, 0] - pos[i, jm, 0], ty = pos[i, jp, 1] - pos[i, jm, 1], tz = pos[i, jp, 2] - pos[i, jm, 2];
                    double ax = pos[ip, jj, 0] - pos[im, jj, 0], ay = pos[ip, jj, 1] - pos[im, jj, 1], az = pos[ip, jj, 2] - pos[im, jj, 2];
                    // Outward = along x around; the ring runs counter-clockwise seen from +Z, so around x along points out.
                    double nx = ty * az - tz * ay, ny = tz * ax - tx * az, nz = tx * ay - ty * ax;
                    double cx = pos[i, jj, 0], cy = pos[i, jj, 1] - sections[i].Y;
                    if (nx * cx + ny * cy < 0) { nx = -nx; ny = -ny; nz = -nz; }
                    var normal = Unit(nx, ny, nz);
                    var tangent = Tangent(normal, tx, ty, tz, ax, ay, az);
                    if (j > 0)
                    {
                        int jb = (j - 1) % radial;
                        double dx = pos[i, jj, 0] - pos[i, jb, 0], dy = pos[i, jj, 1] - pos[i, jb, 1];
                        uAcc += Math.Sqrt(dx * dx + dy * dy);
                    }
                    grid[i, j] = mesh.Positions.Count;
                    mesh.Vertex(new Float3((float)pos[i, jj, 0], (float)pos[i, jj, 1], (float)pos[i, jj, 2]), normal, tangent, new Float2((float)uAcc, (float)vAcc));
                }
            }
            for (int i = 0; i + 1 < m; i++)
                for (int j = 0; j < radial; j++) Quad(mesh, grid[i, j], grid[i, j + 1], grid[i + 1, j], grid[i + 1, j + 1]);
            for (int end = 0; end < 2; end++)
            {
                int i = end == 0 ? 0 : m - 1; int side = end == 0 ? -1 : 1;
                var normal = new Float3(0, 0, side); var tangent = new Float4(1, 0, 0, side);
                int center = mesh.Positions.Count;
                mesh.Vertex(new Float3(0, sections[i].Y, sections[i].Z), normal, tangent, new Float2(0, sections[i].Y));
                int first = mesh.Positions.Count;
                for (int j = 0; j < radial; j++)
                    mesh.Vertex(new Float3((float)pos[i, j, 0], (float)pos[i, j, 1], (float)pos[i, j, 2]), normal, tangent, new Float2((float)pos[i, j, 0], (float)pos[i, j, 1]));
                for (int j = 0; j < radial; j++)
                {
                    int a = first + j, b = first + (j + 1) % radial;
                    if (side > 0) mesh.Triangle(center, a, b); else mesh.Triangle(center, b, a);
                }
            }
            return mesh.Build();
        }

        // ------------------------------------------------------------------ swept tube

        /// <summary>Uniform Catmull-Rom samples through <paramref name="points"/> (end points repeated as tangents).</summary>
        public static List<Float3> Spline(IReadOnlyList<Float3> points, int samplesPerSegment)
        {
            if (points == null || points.Count < 2 || samplesPerSegment < 1) throw new ArgumentException("Spline points.");
            var result = new List<Float3>();
            int n = points.Count;
            for (int i = 0; i + 1 < n; i++)
            {
                var p0 = points[Math.Max(0, i - 1)]; var p1 = points[i]; var p2 = points[i + 1]; var p3 = points[Math.Min(n - 1, i + 2)];
                for (int k = 0; k < samplesPerSegment; k++)
                {
                    double t = (double)k / samplesPerSegment, t2 = t * t, t3 = t2 * t;
                    double a = -.5 * t3 + t2 - .5 * t, b = 1.5 * t3 - 2.5 * t2 + 1, c = -1.5 * t3 + 2 * t2 + .5 * t, d = .5 * t3 - .5 * t2;
                    result.Add(new Float3((float)(a * p0.X + b * p1.X + c * p2.X + d * p3.X), (float)(a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y),
                        (float)(a * p0.Z + b * p1.Z + c * p2.Z + d * p3.Z)));
                }
            }
            result.Add(points[n - 1]);
            return result;
        }

        /// <summary>
        /// Sweeps an elliptical section (radii[i].X along the frame normal, radii[i].Y along the binormal) along
        /// <paramref name="path"/> with parallel-transport frames seeded from <paramref name="up"/>. The start is closed with a
        /// flat disc; the end with a domed cap of <paramref name="domeRings"/> rings and a pole (0 = flat disc).
        /// </summary>
        public static MeshData Tube(IReadOnlyList<Float3> path, IReadOnlyList<Float2> radii, Float3 up, int radial, int domeRings = 3)
        {
            if (path == null || radii == null || path.Count < 2 || radii.Count != path.Count) throw new ArgumentException("Tube path/radii.");
            if (radial < 6 || radial > 64 || domeRings < 0 || domeRings > 8) throw new ArgumentException("Tube resolution.");
            int m = path.Count;
            var P = new double[m, 3]; for (int i = 0; i < m; i++) { P[i, 0] = path[i].X; P[i, 1] = path[i].Y; P[i, 2] = path[i].Z; }
            var T = new double[m, 3]; var N = new double[m, 3]; var B = new double[m, 3];
            for (int i = 0; i < m; i++)
            {
                int i0 = Math.Max(0, i - 1), i1 = Math.Min(m - 1, i + 1);
                double tx = P[i1, 0] - P[i0, 0], ty = P[i1, 1] - P[i0, 1], tz = P[i1, 2] - P[i0, 2], l = Math.Sqrt(tx * tx + ty * ty + tz * tz);
                if (l < 1e-7) throw new ArgumentException("Tube path has coincident samples.");
                T[i, 0] = tx / l; T[i, 1] = ty / l; T[i, 2] = tz / l;
                double nx, ny, nz;
                if (i == 0) { nx = up.X; ny = up.Y; nz = up.Z; } else { nx = N[i - 1, 0]; ny = N[i - 1, 1]; nz = N[i - 1, 2]; }
                double d = nx * T[i, 0] + ny * T[i, 1] + nz * T[i, 2];
                nx -= d * T[i, 0]; ny -= d * T[i, 1]; nz -= d * T[i, 2];
                double ln = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (ln < 1e-6) throw new ArgumentException("Tube up vector is parallel to the path.");
                N[i, 0] = nx / ln; N[i, 1] = ny / ln; N[i, 2] = nz / ln;
                B[i, 0] = T[i, 1] * N[i, 2] - T[i, 2] * N[i, 1]; B[i, 1] = T[i, 2] * N[i, 0] - T[i, 0] * N[i, 2]; B[i, 2] = T[i, 0] * N[i, 1] - T[i, 1] * N[i, 0];
            }
            // Ring centres and radii, including the dome rings beyond the last sample.
            var centres = new List<double[]>(); var rx = new List<double>(); var ry = new List<double>(); var frame = new List<int>();
            for (int i = 0; i < m; i++) { centres.Add(new[] { P[i, 0], P[i, 1], P[i, 2] }); rx.Add(radii[i].X); ry.Add(radii[i].Y); frame.Add(i); }
            double endR = Math.Min(radii[m - 1].X, radii[m - 1].Y);
            for (int k = 1; k <= domeRings; k++)
            {
                double phi = Math.PI / 2 * k / (domeRings + 1), along = endR * Math.Sin(phi), scale = Math.Cos(phi);
                centres.Add(new[] { P[m - 1, 0] + T[m - 1, 0] * along, P[m - 1, 1] + T[m - 1, 1] * along, P[m - 1, 2] + T[m - 1, 2] * along });
                rx.Add(radii[m - 1].X * scale); ry.Add(radii[m - 1].Y * scale); frame.Add(m - 1);
            }
            int rings = centres.Count;
            var pos = new double[rings, radial, 3];
            for (int r = 0; r < rings; r++)
            {
                int f = frame[r];
                for (int j = 0; j < radial; j++)
                {
                    double a = 2 * Math.PI * j / radial, c = j == 0 ? 1 : Math.Cos(a), sn = j == 0 ? 0 : Math.Sin(a);
                    for (int e = 0; e < 3; e++) pos[r, j, e] = centres[r][e] + rx[r] * c * N[f, e] + ry[r] * sn * B[f, e];
                }
            }
            var mesh = new ShapeBuffer();
            var grid = new int[rings, radial + 1];
            double v = 0;
            for (int r = 0; r < rings; r++)
            {
                if (r > 0) v += Math.Sqrt(Sq(centres[r][0] - centres[r - 1][0]) + Sq(centres[r][1] - centres[r - 1][1]) + Sq(centres[r][2] - centres[r - 1][2]));
                int rp = Math.Min(rings - 1, r + 1), rm = Math.Max(0, r - 1);
                for (int j = 0; j <= radial; j++)
                {
                    int jj = j % radial, jp = (jj + 1) % radial, jm = (jj + radial - 1) % radial;
                    double ax = pos[r, jp, 0] - pos[r, jm, 0], ay = pos[r, jp, 1] - pos[r, jm, 1], az = pos[r, jp, 2] - pos[r, jm, 2];
                    double bx = pos[rp, jj, 0] - pos[rm, jj, 0], by = pos[rp, jj, 1] - pos[rm, jj, 1], bz = pos[rp, jj, 2] - pos[rm, jj, 2];
                    double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
                    double ox = pos[r, jj, 0] - centres[r][0], oy = pos[r, jj, 1] - centres[r][1], oz = pos[r, jj, 2] - centres[r][2];
                    if (nx * ox + ny * oy + nz * oz < 0) { nx = -nx; ny = -ny; nz = -nz; }
                    var normal = Unit(nx, ny, nz);
                    var tangent = Tangent(normal, ax, ay, az, bx, by, bz);
                    grid[r, j] = mesh.Positions.Count;
                    mesh.Vertex(new Float3((float)pos[r, jj, 0], (float)pos[r, jj, 1], (float)pos[r, jj, 2]), normal, tangent,
                        new Float2((float)(2 * Math.PI * Math.Max(rx[0], ry[0]) * j / radial), (float)v));
                }
            }
            for (int r = 0; r + 1 < rings; r++)
                for (int j = 0; j < radial; j++) Quad(mesh, grid[r, j], grid[r, j + 1], grid[r + 1, j], grid[r + 1, j + 1]);
            // Start disc facing back along the path.
            {
                var normal = Unit(-T[0, 0], -T[0, 1], -T[0, 2]);
                var tangent = Tangent(normal, N[0, 0], N[0, 1], N[0, 2], B[0, 0], B[0, 1], B[0, 2]);
                int centre = mesh.Positions.Count;
                mesh.Vertex(new Float3((float)centres[0][0], (float)centres[0][1], (float)centres[0][2]), normal, tangent, new Float2(0, 0));
                int first = mesh.Positions.Count;
                for (int j = 0; j < radial; j++)
                    mesh.Vertex(new Float3((float)pos[0, j, 0], (float)pos[0, j, 1], (float)pos[0, j, 2]), normal, tangent, new Float2(j, 0));
                for (int j = 0; j < radial; j++) Fan(mesh, centre, first + j, first + (j + 1) % radial, normal);
            }
            // End: the dome pole, or a flat disc.
            {
                int last = rings - 1;
                var normal = Unit(T[m - 1, 0], T[m - 1, 1], T[m - 1, 2]);
                var tangent = Tangent(normal, N[m - 1, 0], N[m - 1, 1], N[m - 1, 2], B[m - 1, 0], B[m - 1, 1], B[m - 1, 2]);
                if (domeRings > 0)
                {
                    int pole = mesh.Positions.Count;
                    mesh.Vertex(new Float3((float)(P[m - 1, 0] + T[m - 1, 0] * endR), (float)(P[m - 1, 1] + T[m - 1, 1] * endR), (float)(P[m - 1, 2] + T[m - 1, 2] * endR)),
                        normal, tangent, new Float2(0, (float)(v + endR * .3)));
                    for (int j = 0; j < radial; j++) Fan(mesh, pole, grid[last, j], grid[last, j + 1], normal);
                }
                else
                {
                    int centre = mesh.Positions.Count;
                    mesh.Vertex(new Float3((float)centres[last][0], (float)centres[last][1], (float)centres[last][2]), normal, tangent, new Float2(0, 0));
                    int first = mesh.Positions.Count;
                    for (int j = 0; j < radial; j++)
                        mesh.Vertex(new Float3((float)pos[last, j, 0], (float)pos[last, j, 1], (float)pos[last, j, 2]), normal, tangent, new Float2(j, 0));
                    for (int j = 0; j < radial; j++) Fan(mesh, centre, first + j, first + (j + 1) % radial, normal);
                }
            }
            return mesh.Build();
        }

        private static double Sq(double x) => x * x;

        /// <summary>Triangle (centre, a, b) wound to face <paramref name="facing"/>.</summary>
        private static void Fan(ShapeBuffer mesh, int centre, int a, int b, Float3 facing)
        {
            var pc = mesh.Positions[centre]; var pa = mesh.Positions[a]; var pb = mesh.Positions[b];
            double ux = (double)pa.X - pc.X, uy = (double)pa.Y - pc.Y, uz = (double)pa.Z - pc.Z;
            double vx = (double)pb.X - pc.X, vy = (double)pb.Y - pc.Y, vz = (double)pb.Z - pc.Z;
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            if (cx * facing.X + cy * facing.Y + cz * facing.Z >= 0) Tri(mesh, centre, a, b); else Tri(mesh, centre, b, a);
        }

        // ------------------------------------------------------------------ shared helpers

        /// <summary>Adds the quad a-b / c-d (a,b on one row; c,d on the next) wound to agree with its vertex normals.</summary>
        private static void Quad(ShapeBuffer mesh, int a, int b, int c, int d)
        {
            double agreement = Agreement(mesh, a, b, c) + Agreement(mesh, b, d, c);
            if (agreement >= 0) { Tri(mesh, a, b, c); Tri(mesh, b, d, c); }
            else { Tri(mesh, a, c, b); Tri(mesh, b, c, d); }
        }

        private static void Tri(ShapeBuffer mesh, int a, int b, int c)
        {
            var pa = mesh.Positions[a]; var pb = mesh.Positions[b]; var pc = mesh.Positions[c];
            double ux = (double)pb.X - pa.X, uy = (double)pb.Y - pa.Y, uz = (double)pb.Z - pa.Z;
            double vx = (double)pc.X - pa.X, vy = (double)pc.Y - pa.Y, vz = (double)pc.Z - pa.Z;
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            if (cx * cx + cy * cy + cz * cz <= 4e-20) return; // collapsed pole/sliver: its neighbours already close the surface
            mesh.Triangle(a, b, c);
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

        internal static Float3 Unit(double x, double y, double z)
        {
            double l = Math.Sqrt(x * x + y * y + z * z);
            if (l < 1e-12) throw new ArgumentException("Zero-length normal.");
            return new Float3((float)(x / l), (float)(y / l), (float)(z / l));
        }

        /// <summary>Gram-Schmidt tangent from a preferred U direction; handedness from the V direction.</summary>
        internal static Float4 Tangent(Float3 normal, double ux, double uy, double uz, double vx, double vy, double vz)
        {
            double d = ux * normal.X + uy * normal.Y + uz * normal.Z;
            double tx = ux - normal.X * d, ty = uy - normal.Y * d, tz = uz - normal.Z * d;
            double l = Math.Sqrt(tx * tx + ty * ty + tz * tz);
            if (l < 1e-9)
            {
                // Fall back to any axis perpendicular to the normal.
                double ax = Math.Abs(normal.X) < .9 ? 1 : 0, ay = ax == 1 ? 0 : 1;
                d = ax * normal.X + ay * normal.Y;
                tx = ax - normal.X * d; ty = ay - normal.Y * d; tz = -normal.Z * d;
                l = Math.Sqrt(tx * tx + ty * ty + tz * tz);
            }
            tx /= l; ty /= l; tz /= l;
            // Re-orthogonalise in float space so the codec's 1e-4 frame check always holds.
            var t = Unit(tx, ty, tz);
            double bx = normal.Y * t.Z - normal.Z * t.Y, by = normal.Z * t.X - normal.X * t.Z, bz = normal.X * t.Y - normal.Y * t.X;
            float w = bx * vx + by * vy + bz * vz >= 0 ? 1 : -1;
            return new Float4(t.X, t.Y, t.Z, w);
        }

        /// <summary>Deterministic ear clipping of a simple counter-clockwise polygon. Returns index triples.</summary>
        internal static List<int> EarClip(IReadOnlyList<Float2> polygon)
        {
            int n = polygon.Count;
            var remaining = new List<int>(n); for (int i = 0; i < n; i++) remaining.Add(i);
            var result = new List<int>((n - 2) * 3);
            int guard = 0;
            while (remaining.Count > 3)
            {
                bool clipped = false;
                int count = remaining.Count;
                // Prefer the ear with the best (largest) minimum angle proxy for less sliver-prone caps.
                int best = -1; double bestScore = double.NegativeInfinity;
                for (int k = 0; k < count; k++)
                {
                    int ia = remaining[(k + count - 1) % count], ib = remaining[k], ic = remaining[(k + 1) % count];
                    var a = polygon[ia]; var b = polygon[ib]; var c = polygon[ic];
                    double cross = ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
                    if (cross <= 2e-10) continue; // the mesh codec rejects |cross| <= 1e-10 m^2
                    bool inside = false;
                    for (int q = 0; q < count && !inside; q++)
                    {
                        int iq = remaining[q];
                        if (iq == ia || iq == ib || iq == ic) continue;
                        inside = InTriangle(polygon[iq], a, b, c);
                    }
                    if (inside) continue;
                    double ab = Dist(a, b), bc = Dist(b, c), ca = Dist(c, a);
                    double score = cross / Math.Max(1e-18, ab * ab + bc * bc + ca * ca);
                    if (score > bestScore) { bestScore = score; best = k; }
                }
                if (best >= 0)
                {
                    int ia = remaining[(best + count - 1) % count], ib = remaining[best], ic = remaining[(best + 1) % count];
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    remaining.RemoveAt(best); clipped = true;
                }
                if (!clipped || ++guard > 100000) throw new ArgumentException("Cap outline is not a simple polygon.");
            }
            result.Add(remaining[0]); result.Add(remaining[1]); result.Add(remaining[2]);
            return result;
        }

        private static bool InTriangle(Float2 p, Float2 a, Float2 b, Float2 c)
        {
            double d1 = Side(p, a, b), d2 = Side(p, b, c), d3 = Side(p, c, a);
            return d1 >= -1e-14 && d2 >= -1e-14 && d3 >= -1e-14;
        }
        private static double Side(Float2 p, Float2 a, Float2 b) => ((double)b.X - a.X) * ((double)p.Y - a.Y) - ((double)b.Y - a.Y) * ((double)p.X - a.X);
    }

    /// <summary>Vertex/triangle accumulator for <see cref="ShapeKit"/> with read access to normals for winding checks.</summary>
    internal sealed class ShapeBuffer
    {
        public readonly List<Float3> Positions = new List<Float3>();
        private readonly List<Float3> normals = new List<Float3>();
        private readonly List<Float4> tangents = new List<Float4>();
        private readonly List<Float2> uv = new List<Float2>();
        private readonly List<uint> indices = new List<uint>();
        public Float3 Normal(int i) => normals[i];
        public void Vertex(Float3 p, Float3 n, Float4 t, Float2 tex) { Positions.Add(p); normals.Add(n); tangents.Add(t); uv.Add(tex); }
        public void Triangle(int a, int b, int c) { indices.Add((uint)a); indices.Add((uint)b); indices.Add((uint)c); }
        public MeshData Build()
        {
            var mesh = new MeshData(Positions, normals, tangents, uv, indices, new[] { new Submesh(0, (uint)indices.Count, 0) });
            MeshValidation.Check(mesh, true); // every kernel part is a closed, outward solid
            return mesh;
        }
    }
}
