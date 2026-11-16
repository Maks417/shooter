#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Generation.Icons
{
    /// <summary>
    /// ADR-085 analytic icon shape in icon units: one cell is 1000 × 1000, x right, y down.
    /// All distances use IEEE add/sub/mul/div/sqrt only (no trigonometry), so .NET and Unity lanes agree.
    /// </summary>
    public abstract class IconShape
    {
        public abstract double Distance(double x, double y);

        public static IconShape Circle(int cx, int cy, int r) => new CircleShape(cx, cy, r);
        public static IconShape Ring(int cx, int cy, int r, int thickness) => new RingShape(cx, cy, r, thickness);
        public static IconShape Box(int x, int y, int w, int h, int radius = 0) => new BoxShape(x, y, w, h, radius);
        public static IconShape Capsule(int x0, int y0, int x1, int y1, int width) => new CapsuleShape(x0, y0, x1, y1, width);

        /// <summary>Closed polygon from flat x,y pairs (any winding, may be concave).</summary>
        public static IconShape Polygon(params int[] xy) => new PolygonShape(xy);

        /// <summary>Polyline stroke of <paramref name="width"/> through flat x,y pairs.</summary>
        public static IconShape Stroke(int width, params int[] xy) => new StrokeShape(width, xy);

        private sealed class CircleShape : IconShape
        {
            private readonly double cx, cy, r;
            public CircleShape(int cx, int cy, int r) { if (r <= 0) throw new ArgumentOutOfRangeException(nameof(r)); this.cx = cx; this.cy = cy; this.r = r; }
            public override double Distance(double x, double y) { double dx = x - cx, dy = y - cy; return Math.Sqrt(dx * dx + dy * dy) - r; }
        }

        private sealed class RingShape : IconShape
        {
            private readonly double cx, cy, r, half;
            public RingShape(int cx, int cy, int r, int t)
            {
                if (r <= 0 || t <= 0 || t > 2 * r) throw new ArgumentOutOfRangeException(nameof(t));
                this.cx = cx; this.cy = cy; this.r = r; half = t * 0.5;
            }
            public override double Distance(double x, double y) { double dx = x - cx, dy = y - cy; return Math.Abs(Math.Sqrt(dx * dx + dy * dy) - r) - half; }
        }

        private sealed class BoxShape : IconShape
        {
            private readonly double cx, cy, hx, hy, r;
            public BoxShape(int x, int y, int w, int h, int radius)
            {
                if (w <= 0 || h <= 0 || radius < 0) throw new ArgumentOutOfRangeException(nameof(w));
                cx = x + w * 0.5; cy = y + h * 0.5; hx = w * 0.5; hy = h * 0.5;
                r = Math.Min(radius, Math.Min(hx, hy));
            }
            public override double Distance(double x, double y)
            {
                double qx = Math.Abs(x - cx) - hx + r, qy = Math.Abs(y - cy) - hy + r;
                double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
                return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - r;
            }
        }

        internal static double SegmentDistance(double x, double y, double ax, double ay, double bx, double by)
        {
            double px = x - ax, py = y - ay, ex = bx - ax, ey = by - ay;
            double len2 = ex * ex + ey * ey;
            double t = len2 <= 0 ? 0 : Math.Max(0, Math.Min(1, (px * ex + py * ey) / len2));
            double dx = px - ex * t, dy = py - ey * t;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private sealed class CapsuleShape : IconShape
        {
            private readonly double x0, y0, x1, y1, half;
            public CapsuleShape(int x0, int y0, int x1, int y1, int width)
            {
                if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
                this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; half = width * 0.5;
            }
            public override double Distance(double x, double y) => SegmentDistance(x, y, x0, y0, x1, y1) - half;
        }

        private sealed class StrokeShape : IconShape
        {
            private readonly int[] xy;
            private readonly double half;
            public StrokeShape(int width, int[] xy)
            {
                if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
                if (xy == null || xy.Length < 4 || xy.Length % 2 != 0) throw new ArgumentException("Stroke needs two or more points.", nameof(xy));
                this.xy = (int[])xy.Clone(); half = width * 0.5;
            }
            public override double Distance(double x, double y)
            {
                double best = double.MaxValue;
                for (int i = 0; i + 3 < xy.Length; i += 2)
                    best = Math.Min(best, SegmentDistance(x, y, xy[i], xy[i + 1], xy[i + 2], xy[i + 3]));
                return best - half;
            }
        }

        private sealed class PolygonShape : IconShape
        {
            private readonly int[] xy;
            public PolygonShape(int[] xy)
            {
                if (xy == null || xy.Length < 6 || xy.Length % 2 != 0) throw new ArgumentException("Polygon needs three or more points.", nameof(xy));
                this.xy = (int[])xy.Clone();
            }
            public override double Distance(double x, double y)
            {
                int n = xy.Length / 2;
                double best = double.MaxValue;
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double ax = xy[j * 2], ay = xy[j * 2 + 1], bx = xy[i * 2], by = xy[i * 2 + 1];
                    best = Math.Min(best, SegmentDistance(x, y, ax, ay, bx, by));
                    // Even-odd crossing test on a half-open interval (deterministic on vertices).
                    if ((by > y) != (ay > y) && x < (ax - bx) * (y - by) / (ay - by) + bx) inside = !inside;
                }
                return inside ? -best : best;
            }
        }
    }

    /// <summary>Union/subtract list of <see cref="IconShape"/> rendered into a span of square atlas cells.</summary>
    public sealed class IconRecipe
    {
        private readonly List<(bool Add, IconShape Shape)> ops = new List<(bool, IconShape)>();

        public IconRecipe(string name, int spanCells = 1)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Icon name required.", nameof(name));
            if (spanCells < 1 || spanCells > 8) throw new ArgumentOutOfRangeException(nameof(spanCells));
            Name = name;
            SpanCells = spanCells;
        }

        public string Name { get; }
        public int SpanCells { get; }
        public int OperationCount => ops.Count;

        public IconRecipe Add(IconShape shape) { ops.Add((true, shape ?? throw new ArgumentNullException(nameof(shape)))); return this; }
        public IconRecipe Cut(IconShape shape) { ops.Add((false, shape ?? throw new ArgumentNullException(nameof(shape)))); return this; }

        /// <summary>Signed distance in icon units (negative inside).</summary>
        public double Distance(double x, double y)
        {
            if (ops.Count == 0) throw new InvalidOperationException("Empty icon recipe: " + Name);
            double d = double.MaxValue;
            foreach (var op in ops)
            {
                double s = op.Shape.Distance(x, y);
                d = op.Add ? Math.Min(d, s) : Math.Max(d, -s);
            }
            return d;
        }
    }
}
