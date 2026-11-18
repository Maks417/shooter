#nullable enable
using System;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Offline analytic near-field finishes: no image inputs, lighting bakes or runtime fallback. Families follow
    /// <see cref="MapVisualClosure.FirstPersonSurfacePaths"/>: 0 coated metal, 1 polymer, 2 sleeve, 3 glove, 4 steel,
    /// 5 recess, 6 enamel, and (ADR-111) 7 brushed blade steel, 8 polished edge grind, 9 olive grenade enamel.
    /// </summary>
    public static class FirstPersonSurfaceBaker
    {
        public const int Resolution = 128;
        public const int FamilyCount = 10;
        public const int BladeSteel = 7, BladeEdge = 8, GrenadePaint = 9;
        public const float TileMeters = .1f;
        private static readonly Lazy<MaterialBake[]> bakes = new Lazy<MaterialBake[]>(
            () => { var all = new MaterialBake[FamilyCount]; for (int i = 0; i < all.Length; i++) all[i] = Build(i); return all; }, true);

        public static MaterialBake Bake(int family)
        {
            if (family < 0 || family >= FamilyCount) throw new ArgumentOutOfRangeException(nameof(family));
            return bakes.Value[family];
        }

        private static MaterialBake Build(int family)
        {
            int n = Resolution;
            var albedo = new byte[n * n * 4];
            var normal = new byte[albedo.Length];
            var mask = new byte[albedo.Length];
            var heights = new double[n * n];
            int[][] colors = { new[] { 61, 67, 71 }, new[] { 31, 34, 30 }, new[] { 92, 108, 85 }, new[] { 70, 63, 48 }, new[] { 108, 115, 122 }, new[] { 18, 23, 28 }, new[] { 51, 66, 28 },
                new[] { 150, 153, 158 }, new[] { 196, 199, 204 }, new[] { 78, 86, 50 } };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double u = (double)x / n, v = (double)y / n;
                    double grain = Noise(x, y, family);
                    double warp = Math.Sin(2 * Math.PI * 16 * u), weft = Math.Sin(2 * Math.PI * 16 * v);
                    double weave = warp * weft;
                    double brush = Math.Sin(2 * Math.PI * (29 * u + v));
                    double roughness, variation, height;
                    byte metallic = family == 0 ? (byte)190 : family == 4 ? (byte)240 : family == 6 ? (byte)30 : (byte)0;
                    int[] color = colors[family];
                    if (family == BladeSteel)
                    {
                        // Directional brushed grain: per-row streaks running along U (the blade length) with long, slow
                        // modulation, plus fine micro-scratch noise.
                        double streak = .6 * Noise(0, y, family) + .4 * Smooth(x, y, 4, Resolution, family, 1);
                        variation = .95 + .05 * streak + .02 * grain;
                        height = .000006 * streak + .000004 * grain;
                        roughness = .24 + .05 * streak + .02 * grain;
                        metallic = 235;
                    }
                    else if (family == BladeEdge)
                    {
                        // Polished edge grind: brighter and smoother, with sharpening lines running across the bevel (along V).
                        double streak = .6 * Noise(x, 0, family) + .4 * Smooth(x, y, Resolution, 4, family, 1);
                        variation = .97 + .035 * streak + .015 * grain;
                        height = .000004 * streak + .000003 * grain;
                        roughness = .14 + .03 * streak + .015 * grain;
                        metallic = 245;
                    }
                    else if (family == GrenadePaint)
                    {
                        // Olive enamel: orange-peel relief, broad mottling, rubbed thin paint around sparse chips of bare steel.
                        double peel = .6 * Smooth(x, y, 64, 64, family, 2) + .4 * Smooth(x, y, 32, 32, family, 3);
                        double mottle = Smooth(x, y, 4, 4, family, 4);
                        double chipField = Smooth(x, y, 16, 16, family, 5) + .35 * Smooth(x, y, 64, 64, family, 6);
                        bool chip = chipField > .86;
                        double wear = Math.Max(0, Math.Min(1, (chipField - .58) / .28));
                        if (chip)
                        {
                            color = new[] { 118, 116, 110 };
                            variation = .92 + .08 * grain;
                            height = -.00004 + .000004 * grain;
                            roughness = .42 + .04 * grain;
                            metallic = 200;
                        }
                        else
                        {
                            variation = .96 + .04 * mottle + .02 * grain + .12 * wear;
                            height = .000010 * peel;
                            roughness = .55 + .05 * peel + .03 * mottle - .08 * wear;
                        }
                    }
                    else if (family == 0)
                    {
                        variation = .95 + .055 * grain + .018 * brush;
                        height = .000012 * brush + .000006 * grain;
                        roughness = .53 + .08 * grain + .03 * brush;
                    }
                    else if (family == 1)
                    {
                        double stipple = Math.Max(0, grain - .25);
                        variation = .97 + .06 * grain;
                        height = .000085 * stipple;
                        roughness = .86 + .045 * grain;
                    }
                    else if (family == 2)
                    {
                        variation = .97 + .065 * weave + .035 * grain;
                        height = .000075 * weave + .000012 * grain;
                        roughness = .91 + .035 * weave;
                    }
                    else if (family == 4)
                    {
                        variation = .97 + .025 * grain + .018 * brush;
                        height = .000008 * brush + .000003 * grain;
                        roughness = .31 + .045 * grain + .025 * brush;
                    }
                    else if (family == 5)
                    {
                        variation = .94 + .12 * grain;
                        height = .000012 * grain;
                        roughness = .92 + .025 * grain;
                    }
                    else if (family == 6)
                    {
                        variation = .97 + .045 * grain;
                        height = .000021 * grain;
                        roughness = .64 + .055 * grain;
                    }
                    else
                    {
                        double crease = Math.Pow(.5 + .5 * Math.Sin(2 * Math.PI * (3 * u + 2 * v)), 12);
                        variation = .97 + .045 * grain - .06 * crease;
                        height = .00005 * grain - .00009 * crease;
                        roughness = .78 + .055 * grain + .04 * crease;
                    }
                    heights[y * n + x] = height;
                    int pixel = (y * n + x) * 4;
                    for (int c = 0; c < 3; c++) albedo[pixel + c] = Byte(color[c] / 255d * variation);
                    albedo[pixel + 3] = 255;
                    mask[pixel] = metallic;
                    mask[pixel + 1] = Byte(.96 - .03 * Math.Abs(grain));
                    mask[pixel + 2] = Byte(roughness);
                    mask[pixel + 3] = 255;
                }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double du = (heights[y * n + (x + 1) % n] - heights[y * n + (x + n - 1) % n]) / (2 * TileMeters / n);
                    double dv = (heights[((y + 1) % n) * n + x] - heights[((y + n - 1) % n) * n + x]) / (2 * TileMeters / n);
                    var tangent = MaterialBaker.TangentNormal(du, dv);
                    int pixel = (y * n + x) * 4;
                    normal[pixel] = Byte(tangent.X * .5 + .5);
                    normal[pixel + 1] = Byte(tangent.Y * .5 + .5);
                    normal[pixel + 2] = Byte(tangent.Z * .5 + .5);
                    normal[pixel + 3] = 255;
                }
            return new MaterialBake(MaterialBaker.BuildPlane(n, albedo, "albedo"),
                MaterialBaker.BuildPlane(n, normal, "normal"), MaterialBaker.BuildPlane(n, mask, "mask"));
        }

        private static double Noise(int x, int y, int family)
        {
            unchecked
            {
                uint bits = (uint)(x + y * Resolution + family * 7919) + 0x9e3779b9u;
                bits = (bits ^ (bits >> 16)) * 0x85ebca6bu;
                bits = (bits ^ (bits >> 13)) * 0xc2b2ae35u;
                bits ^= bits >> 16;
                return (bits & 65535u) / 32767.5 - 1d;
            }
        }
        /// <summary>Tileable smoothstep value noise in [-1, 1] on a <paramref name="cellsX"/> x <paramref name="cellsY"/> lattice.</summary>
        private static double Smooth(int x, int y, int cellsX, int cellsY, int family, int salt)
        {
            double fx = (double)x * cellsX / Resolution, fy = (double)y * cellsY / Resolution;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
            double Lattice(int i, int j) => Noise(((i % cellsX) + cellsX) % cellsX + 1000 * salt, ((j % cellsY) + cellsY) % cellsY, family);
            double a = Lattice(x0, y0), b = Lattice(x0 + 1, y0), c = Lattice(x0, y0 + 1), d = Lattice(x0 + 1, y0 + 1);
            return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty;
        }

        private static byte Byte(double value) => (byte)Math.Round(Math.Max(0, Math.Min(1, value)) * 255, MidpointRounding.AwayFromZero);
    }
}
