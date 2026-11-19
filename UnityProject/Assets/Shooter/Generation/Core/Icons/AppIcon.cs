#nullable enable
using System;

namespace Shooter.Generation.Icons
{
    /// <summary>
    /// Executable, shortcut and installer icon (ADR-123). Analytic only (ADR-002/085, no imported art): a charcoal
    /// rounded tile in the UI surface tones with a hairline rim, carrying the amber <see cref="BrandEmblem"/> (reticle
    /// ring, four ticks, chevron) shaded from bright to deep amber. Every size is rendered natively, not downsampled,
    /// and small sizes thicken the strokes. Output is straight-alpha RGBA32, row 0 = bottom (Unity texture order).
    /// Uses IEEE add/sub/mul/div/sqrt only, so the bytes are identical in every lane.
    /// </summary>
    public static class AppIcon
    {
        public const int DefaultSize = 256;

        /// <summary>The sizes baked into the player icon slots and the installer .ico.</summary>
        public static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

        // Icon units: 1000 × 1000, x right, y down (as IconShape).
        private static readonly IconShape Tile = IconShape.Box(30, 30, 940, 940, 150);

        public static byte[] Rgba(int size = DefaultSize) => Render(size, rowZeroIsTop: false);

        /// <summary>Same pixels with row 0 at the top (file formats such as ICO/PNG writers that flip themselves).</summary>
        public static byte[] RgbaTopDown(int size = DefaultSize) => Render(size, rowZeroIsTop: true);

        private static byte[] Render(int size, bool rowZeroIsTop)
        {
            if (size < 16 || size > 1024) throw new ArgumentOutOfRangeException(nameof(size));
            var pixels = new byte[size * size * 4];
            var emblem = BrandEmblem.Shapes(size);
            double unitsPerPixel = 1000.0 / size;
            double aa = unitsPerPixel; // one-pixel coverage ramp
            double bevel = Math.Max(14.0, unitsPerPixel * 1.2);
            var c = new double[3];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    double x = (px + 0.5) * unitsPerPixel;
                    double y = (rowZeroIsTop ? py + 0.5 : size - 1 - py + 0.5) * unitsPerPixel;
                    double tileDistance = Tile.Distance(x, y);
                    double tile = Coverage(tileDistance, aa);
                    if (tile <= 0.0) continue;

                    double v = y / 1000.0;
                    for (int k = 0; k < 3; k++) c[k] = BrandEmblem.TileTop[k] + (BrandEmblem.TileBottom[k] - BrandEmblem.TileTop[k]) * v;
                    // Hairline rim just inside the tile edge, brighter at the top like the UI frames.
                    double rim = 1.0 - Coverage(-tileDistance - bevel, aa);
                    Blend(c, BrandEmblem.TileEdge, rim * (0.95 - 0.5 * v));

                    double mark = 0.0;
                    for (int i = 0; i < emblem.Length; i++) mark = Math.Max(mark, Coverage(emblem[i].Distance(x, y), aa));
                    if (mark > 0.0)
                    {
                        double shade = (y - 140.0) / 720.0;
                        shade = shade < 0.0 ? 0.0 : shade > 1.0 ? 1.0 : shade;
                        var ink = new double[3];
                        for (int k = 0; k < 3; k++)
                        {
                            ink[k] = shade < 0.5
                                ? BrandEmblem.AccentBright[k] + (BrandEmblem.Accent[k] - BrandEmblem.AccentBright[k]) * (shade * 2.0)
                                : BrandEmblem.Accent[k] + (BrandEmblem.AccentDeep[k] - BrandEmblem.Accent[k]) * ((shade - 0.5) * 2.0);
                        }
                        Blend(c, ink, mark);
                    }

                    int o = (py * size + px) * 4;
                    pixels[o] = ToByte(c[0]);
                    pixels[o + 1] = ToByte(c[1]);
                    pixels[o + 2] = ToByte(c[2]);
                    pixels[o + 3] = ToByte(tile);
                }
            }
            return pixels;
        }

        internal static double Coverage(double distance, double aa)
        {
            double u = 0.5 - distance / aa;
            return u <= 0.0 ? 0.0 : u >= 1.0 ? 1.0 : u;
        }

        internal static void Blend(double[] c, double[] over, double alpha)
        {
            if (alpha <= 0.0) return;
            for (int k = 0; k < 3; k++) c[k] += (over[k] - c[k]) * alpha;
        }

        internal static byte ToByte(double value)
        {
            double v = value * 255.0 + 0.5;
            return v <= 0.0 ? (byte)0 : v >= 255.0 ? (byte)255 : (byte)v;
        }
    }
}
