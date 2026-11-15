#nullable enable
using System;

namespace Shooter.Generation.Environment
{
    /// <summary>Offline analytic near-field finishes: no image inputs, lighting bakes or runtime fallback.</summary>
    public static class FirstPersonSurfaceBaker
    {
        public const int Resolution = 128;
        public const float TileMeters = .1f;
        private static readonly Lazy<MaterialBake[]> bakes = new Lazy<MaterialBake[]>(
            () => new[] { Build(0), Build(1), Build(2), Build(3) }, true);

        public static MaterialBake Bake(int family)
        {
            if (family < 0 || family > 3) throw new ArgumentOutOfRangeException(nameof(family));
            return bakes.Value[family];
        }

        private static MaterialBake Build(int family)
        {
            int n = Resolution;
            var albedo = new byte[n * n * 4];
            var normal = new byte[albedo.Length];
            var mask = new byte[albedo.Length];
            var heights = new double[n * n];
            int[][] colors = { new[] { 61, 67, 71 }, new[] { 31, 34, 30 }, new[] { 92, 108, 85 }, new[] { 70, 63, 48 } };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double u = (double)x / n, v = (double)y / n;
                    double grain = Noise(x, y, family);
                    double warp = Math.Sin(2 * Math.PI * 16 * u), weft = Math.Sin(2 * Math.PI * 16 * v);
                    double weave = warp * weft;
                    double brush = Math.Sin(2 * Math.PI * (29 * u + v));
                    double roughness, variation, height;
                    if (family == 0)
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
                    else
                    {
                        double crease = Math.Pow(.5 + .5 * Math.Sin(2 * Math.PI * (3 * u + 2 * v)), 12);
                        variation = .97 + .045 * grain - .06 * crease;
                        height = .00005 * grain - .00009 * crease;
                        roughness = .78 + .055 * grain + .04 * crease;
                    }
                    heights[y * n + x] = height;
                    int pixel = (y * n + x) * 4;
                    for (int c = 0; c < 3; c++) albedo[pixel + c] = Byte(colors[family][c] / 255d * variation);
                    albedo[pixel + 3] = 255;
                    mask[pixel] = family == 0 ? (byte)190 : (byte)0;
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
        private static byte Byte(double value) => (byte)Math.Round(Math.Max(0, Math.Min(1, value)) * 255, MidpointRounding.AwayFromZero);
    }
}
