#nullable enable
using System;
using System.IO;

namespace Shooter.Generation.Icons
{
    /// <summary>
    /// ADR-123 installer art from the brand recipe, no imported art: the multi-size setup icon (.ico with PNG-free
    /// 32-bit BMP entries at every <see cref="AppIcon.Sizes"/> size) and the Inno Setup wizard bitmaps (24-bit BMP).
    /// The wizard panel is the UI's charcoal gradient with analytic contour lines, an amber rail and the emblem.
    /// Deterministic bytes: integer/IEEE maths only.
    /// </summary>
    public static class InstallerArt
    {
        public const int WizardWidth = 164;
        public const int WizardHeight = 314;
        public const int SmallSize = 55;

        /// <summary>Windows .ico holding every icon size as a 32-bit BGRA DIB with an AND mask.</summary>
        public static byte[] Ico()
        {
            int[] sizes = AppIcon.Sizes;
            var images = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++) images[i] = IcoDib(sizes[i]);
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }
            for (int i = 0; i < images.Length; i++) w.Write(images[i]);
            w.Flush();
            return stream.ToArray();
        }

        private static byte[] IcoDib(int size)
        {
            byte[] rgba = AppIcon.Rgba(size); // row 0 = bottom, as DIBs expect
            int maskStride = ((size + 31) / 32) * 4;
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write(40);
            w.Write(size);
            w.Write(size * 2); // XOR + AND masks
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(0);
            w.Write(size * size * 4 + maskStride * size);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            for (int i = 0; i < size * size; i++)
            {
                w.Write(rgba[i * 4 + 2]);
                w.Write(rgba[i * 4 + 1]);
                w.Write(rgba[i * 4]);
                w.Write(rgba[i * 4 + 3]);
            }
            for (int y = 0; y < size; y++)
            {
                var row = new byte[maskStride];
                for (int x = 0; x < size; x++)
                    if (rgba[(y * size + x) * 4 + 3] == 0) row[x >> 3] |= (byte)(0x80 >> (x & 7));
                w.Write(row);
            }
            w.Flush();
            return stream.ToArray();
        }

        /// <summary>Large wizard panel (Inno Setup <c>WizardImageFile</c>), 164 × 314, 24-bit BMP.</summary>
        public static byte[] WizardImage() => Bmp24(WizardWidth, WizardHeight, WizardPixel);

        /// <summary>Small wizard header image (Inno Setup <c>WizardSmallImageFile</c>), 55 × 55, 24-bit BMP.</summary>
        public static byte[] WizardSmallImage()
        {
            byte[] icon = AppIcon.RgbaTopDown(64);
            return Bmp24(SmallSize, SmallSize, (x, y, c) =>
            {
                // White page background (the wizard header is white); the icon is centred and composited over it.
                c[0] = c[1] = c[2] = 1.0;
                int sx = x + (64 - SmallSize) / 2, sy = y + (64 - SmallSize) / 2;
                int o = (sy * 64 + sx) * 4;
                double a = icon[o + 3] / 255.0;
                for (int k = 0; k < 3; k++) c[k] = c[k] * (1 - a) + icon[o + k] / 255.0 * a;
            });
        }

        private static void WizardPixel(int x, int y, double[] c)
        {
            // Units: y down, 0..1 over the panel height.
            double v = (y + 0.5) / WizardHeight, u = (x + 0.5) / WizardWidth;
            for (int k = 0; k < 3; k++) c[k] = BrandEmblem.TileTop[k] + (BrandEmblem.TileBottom[k] - BrandEmblem.TileTop[k]) * v;
            // Contour lines (same family as the shell backdrop): thin bands of a sine terrain field.
            double field = (v * 9.0) + 0.6 * Math.Sin(u * 6.0 + v * 2.0) + 0.25 * Math.Sin(u * 13.0 - v * 5.0);
            double frac = field - Math.Floor(field);
            double line = frac < 0.06 ? 1.0 - frac / 0.06 : 0.0;
            AppIcon.Blend(c, BrandEmblem.TileEdge, line * 0.7 * (0.35 + 0.65 * v));
            // Amber rail on the left edge.
            if (x < 4) AppIcon.Blend(c, BrandEmblem.Accent, 1.0);
            // Emblem near the top, on the tile-less panel.
            const int iconSize = 96;
            double ex = (WizardWidth - iconSize) * 0.5, ey = 42;
            double ix = (x + 0.5 - ex) / iconSize * 1000.0, iy = (y + 0.5 - ey) / iconSize * 1000.0;
            if (ix > 0 && iy > 0 && ix < 1000 && iy < 1000)
            {
                double aa = 1000.0 / iconSize, mark = 0.0;
                foreach (var shape in Emblem) mark = Math.Max(mark, AppIcon.Coverage(shape.Distance(ix, iy), aa));
                AppIcon.Blend(c, BrandEmblem.Accent, mark);
            }
            // Amber hairline under the emblem.
            if (y == 160 && x >= 54 && x < 110) AppIcon.Blend(c, BrandEmblem.Accent, 1.0);
        }

        private static readonly IconShape[] Emblem = BrandEmblem.Shapes(96);

        private delegate void PixelFn(int x, int y, double[] rgb);

        private static byte[] Bmp24(int width, int height, PixelFn pixel)
        {
            int stride = ((width * 3 + 3) / 4) * 4;
            int dataSize = stride * height;
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write((byte)'B');
            w.Write((byte)'M');
            w.Write(54 + dataSize);
            w.Write(0);
            w.Write(54);
            w.Write(40);
            w.Write(width);
            w.Write(height);
            w.Write((ushort)1);
            w.Write((ushort)24);
            w.Write(0);
            w.Write(dataSize);
            w.Write(2835);
            w.Write(2835);
            w.Write(0);
            w.Write(0);
            var c = new double[3];
            var row = new byte[stride];
            for (int by = 0; by < height; by++)
            {
                int y = height - 1 - by; // BMP rows are bottom-up
                Array.Clear(row, 0, row.Length);
                for (int x = 0; x < width; x++)
                {
                    pixel(x, y, c);
                    row[x * 3] = AppIcon.ToByte(c[2]);
                    row[x * 3 + 1] = AppIcon.ToByte(c[1]);
                    row[x * 3 + 2] = AppIcon.ToByte(c[0]);
                }
                w.Write(row);
            }
            w.Flush();
            return stream.ToArray();
        }
    }
}
