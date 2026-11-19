#nullable enable
using System;

namespace Shooter.Generation.Icons
{
    /// <summary>
    /// ADR-123 brand emblem: one numeric recipe for the app icon, the installer art and the in-game brand mark.
    /// A reticle ring crossed by four ticks with an upward chevron inside. Units are 1000 × 1000, x right, y down.
    /// The colours mirror the UI tokens (<c>Presentation/Model/Style/UiTokens.cs</c>); <c>BrandEmblemTokenTests</c>
    /// keeps the two equal.
    /// </summary>
    public static class BrandEmblem
    {
        public readonly struct Segment
        {
            public Segment(double x0, double y0, double x1, double y1)
            {
                X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
            }

            public double X0 { get; }
            public double Y0 { get; }
            public double X1 { get; }
            public double Y1 { get; }
        }

        public readonly struct Point
        {
            public Point(double x, double y)
            {
                X = x; Y = y;
            }

            public double X { get; }
            public double Y { get; }
        }

        // Geometry in icon units (integers so the rasterizer shapes stay exact).
        public const int Centre = 500;
        public const int RingRadiusUnits = 330;
        public const int RingStrokeUnits = 58;
        public const int TickWidthUnits = 56;
        public const int TickOuterUnits = 390;
        public const int TickInnerUnits = 250;
        public const int ChevronStrokeUnits = 84;
        private static readonly int[] ChevronUnits = { 330, 610, 500, 440, 670, 610 };

        // Normalized (0..1) views for the SDF brand mark.
        public const double RingRadius = RingRadiusUnits / 1000.0;
        public const double RingStroke = RingStrokeUnits / 1000.0;
        public const double TickWidth = TickWidthUnits / 1000.0;
        public const double ChevronStroke = ChevronStrokeUnits / 1000.0;

        public static readonly Segment[] Ticks = BuildTicks();
        public static readonly Point[] Chevron = BuildChevron();

        // Colours (sRGB 0..1): the UI token values for Accent, AccentBright, AccentDeep, PanelTop, Shade and Edge.
        public static readonly double[] Accent = Rgb(230, 197, 106);
        public static readonly double[] AccentBright = Rgb(255, 230, 158);
        public static readonly double[] AccentDeep = Rgb(196, 160, 72);
        public static readonly double[] TileTop = Rgb(35, 44, 49);
        public static readonly double[] TileBottom = Rgb(5, 8, 9);
        public static readonly double[] TileEdge = Rgb(54, 66, 72);

        /// <summary>
        /// Rasterizer shapes for the emblem at <paramref name="pixels"/> size. Below 48 px the strokes thicken so the
        /// mark keeps at least about one device pixel per stroke instead of being downsampled to mush.
        /// </summary>
        public static IconShape[] Shapes(int pixels)
        {
            double boost = pixels >= 48 ? 1.0 : Math.Min(1.8, 48.0 / pixels);
            int W(int units) => (int)Math.Round(units * boost);
            var shapes = new IconShape[6];
            shapes[0] = IconShape.Ring(Centre, Centre, RingRadiusUnits, W(RingStrokeUnits));
            int t = 0;
            foreach (var tick in TickUnits())
                shapes[1 + t++] = IconShape.Capsule(tick[0], tick[1], tick[2], tick[3], W(TickWidthUnits));
            shapes[5] = IconShape.Stroke(W(ChevronStrokeUnits), ChevronUnits);
            return shapes;
        }

        private static int[][] TickUnits() => new[]
        {
            new[] { Centre, Centre - TickOuterUnits, Centre, Centre - TickInnerUnits },
            new[] { Centre, Centre + TickInnerUnits, Centre, Centre + TickOuterUnits },
            new[] { Centre - TickOuterUnits, Centre, Centre - TickInnerUnits, Centre },
            new[] { Centre + TickInnerUnits, Centre, Centre + TickOuterUnits, Centre }
        };

        private static Segment[] BuildTicks()
        {
            var units = TickUnits();
            var result = new Segment[units.Length];
            for (int i = 0; i < units.Length; i++)
                result[i] = new Segment(units[i][0] / 1000.0, units[i][1] / 1000.0, units[i][2] / 1000.0, units[i][3] / 1000.0);
            return result;
        }

        private static Point[] BuildChevron()
        {
            var result = new Point[ChevronUnits.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Point(ChevronUnits[i * 2] / 1000.0, ChevronUnits[i * 2 + 1] / 1000.0);
            return result;
        }

        private static double[] Rgb(int r, int g, int b) => new[] { r / 255.0, g / 255.0, b / 255.0 };
    }
}
