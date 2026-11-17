#nullable enable
using System;

namespace Shooter.Generation.Icons
{
    /// <summary>
    /// Executable / shortcut icon (bug fix after 0.3.18: the player shipped the default Unity icon).
    /// Analytic only (ADR-002/085, no imported art): a dark olive rounded tile with a sand scope reticle
    /// (ring, four ticks, centre dot) and a red aim pip. Output is straight-alpha RGBA32, row 0 = bottom
    /// (Unity texture order). Uses IEEE add/sub/mul/div/sqrt only, so the bytes are identical in every lane.
    /// </summary>
    public static class AppIcon
    {
        public const int DefaultSize = 256;

        // Icon units: 1000 × 1000, x right, y down (as IconShape).
        private static readonly IconShape Tile = IconShape.Box(40, 40, 920, 920, 190);
        private static readonly IconShape Ring = IconShape.Ring(500, 500, 300, 64);
        private static readonly IconShape[] Ticks =
        {
            IconShape.Box(472, 120, 56, 250, 20),
            IconShape.Box(472, 630, 56, 250, 20),
            IconShape.Box(120, 472, 250, 56, 20),
            IconShape.Box(630, 472, 250, 56, 20)
        };
        private static readonly IconShape Pip = IconShape.Circle(500, 500, 46);

        private static readonly double[] TileTop = { 0.26, 0.29, 0.20 };
        private static readonly double[] TileBottom = { 0.11, 0.13, 0.09 };
        private static readonly double[] Edge = { 0.55, 0.50, 0.36 };
        private static readonly double[] Sand = { 0.93, 0.84, 0.62 };
        private static readonly double[] Red = { 0.90, 0.22, 0.16 };

        public static byte[] Rgba(int size = DefaultSize)
        {
            if (size < 16 || size > 1024) throw new ArgumentOutOfRangeException(nameof(size));
            var pixels = new byte[size * size * 4];
            double unitsPerPixel = 1000.0 / size;
            double aa = unitsPerPixel; // one-pixel coverage ramp
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    double x = (px + 0.5) * unitsPerPixel;
                    double y = (size - 1 - py + 0.5) * unitsPerPixel; // texture row 0 is the bottom
                    double tile = Coverage(Tile.Distance(x, y), aa);
                    if (tile <= 0.0) continue;

                    double v = y / 1000.0;
                    var c = new double[3];
                    for (int k = 0; k < 3; k++) c[k] = TileTop[k] + (TileBottom[k] - TileTop[k]) * v;
                    // Thin bevel just inside the tile edge.
                    double rim = Coverage(-Tile.Distance(x, y) - 22.0, aa);
                    Blend(c, Edge, (1.0 - rim) * 0.85);

                    double mark = Coverage(Ring.Distance(x, y), aa);
                    for (int t = 0; t < Ticks.Length; t++) mark = Math.Max(mark, Coverage(Ticks[t].Distance(x, y), aa));
                    Blend(c, Sand, mark);
                    Blend(c, Red, Coverage(Pip.Distance(x, y), aa));

                    int o = (py * size + px) * 4;
                    pixels[o] = ToByte(c[0]);
                    pixels[o + 1] = ToByte(c[1]);
                    pixels[o + 2] = ToByte(c[2]);
                    pixels[o + 3] = ToByte(tile);
                }
            }
            return pixels;
        }

        private static double Coverage(double distance, double aa)
        {
            double u = 0.5 - distance / aa;
            return u <= 0.0 ? 0.0 : u >= 1.0 ? 1.0 : u;
        }

        private static void Blend(double[] c, double[] over, double alpha)
        {
            if (alpha <= 0.0) return;
            for (int k = 0; k < 3; k++) c[k] += (over[k] - c[k]) * alpha;
        }

        private static byte ToByte(double value)
        {
            double v = value * 255.0 + 0.5;
            return v <= 0.0 ? (byte)0 : v >= 255.0 ? (byte)255 : (byte)v;
        }
    }
}
