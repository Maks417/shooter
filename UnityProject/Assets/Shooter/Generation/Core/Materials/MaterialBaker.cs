#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public readonly struct MaterialSample
    {
        public Float3 LinearAlbedo { get; }
        public float HeightMeters { get; }
        public float Metallic { get; }
        public float Occlusion { get; }
        public float Roughness { get; }
        internal MaterialSample(Float3 albedo, double height, double metallic, double occlusion, double roughness)
        { LinearAlbedo = albedo; HeightMeters = (float)height; Metallic = (float)metallic; Occlusion = (float)occlusion; Roughness = (float)roughness; }
    }
    public sealed class MaterialBake
    {
        public MaterialData Albedo { get; }
        public MaterialData Normal { get; }
        public MaterialData Mask { get; }
        public long Bytes { get; }
        internal MaterialBake(MaterialData albedo, MaterialData normal, MaterialData mask)
        {
            Albedo = albedo; Normal = normal; Mask = mask;
            foreach (var plane in new[] { albedo, normal, mask }) foreach (var mip in plane.MipBytes) Bytes += mip.Count;
        }
    }
    /// <summary>Pure CPU periodic fields and physically scaled height normals. No baked lighting or external images.</summary>
    public static class MaterialBaker
    {
        public static MaterialBake Bake(MaterialRecipe recipe, CancellationToken cancel = default)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            cancel.ThrowIfCancellationRequested();
            // The minimum sampling grid resolves the 64-cycle weave/grain before filtering.
            // Small diagnostic outputs are lower mips, never aliased point-samples of microstructure.
            int n = Math.Max(256, recipe.Resolution);
            var albedo = new byte[n * n * 4]; var normal = new byte[albedo.Length]; var mask = new byte[albedo.Length]; var heights = new float[n * n];
            var field = new Field(recipe);
            for (int y = 0; y < n; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int x = 0; x < n; x++)
                {
                    MaterialSample value = field.Evaluate((x + .5) / n, (y + .5) / n); int p = (y * n + x) * 4;
                    albedo[p] = EncodeSrgb(value.LinearAlbedo.X); albedo[p + 1] = EncodeSrgb(value.LinearAlbedo.Y); albedo[p + 2] = EncodeSrgb(value.LinearAlbedo.Z); albedo[p + 3] = 255;
                    mask[p] = Byte(value.Metallic); mask[p + 1] = Byte(value.Occlusion); mask[p + 2] = Byte(value.Roughness); mask[p + 3] = 255;
                    heights[y * n + x] = value.HeightMeters;
                }
            }
            double distance = 2.0 * recipe.MetersPerTile / n;
            for (int y = 0; y < n; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int x = 0; x < n; x++)
                {
                    double du = (heights[y * n + (x + 1) % n] - heights[y * n + (x + n - 1) % n]) / distance;
                    double dv = (heights[((y + 1) % n) * n + x] - heights[((y + n - 1) % n) * n + x]) / distance;
                    Float3 direction = TangentNormal(du, dv); int p = (y * n + x) * 4;
                    normal[p] = Byte(direction.X * .5 + .5); normal[p + 1] = Byte(direction.Y * .5 + .5); normal[p + 2] = Byte(direction.Z * .5 + .5); normal[p + 3] = 255;
                }
            }
            cancel.ThrowIfCancellationRequested();
            var result = new MaterialBake(SelectResolution(BuildPlane(n, albedo, "albedo", cancel), recipe.Resolution),
                SelectResolution(BuildPlane(n, normal, "normal", cancel), recipe.Resolution), SelectResolution(BuildPlane(n, mask, "mask", cancel), recipe.Resolution));
            cancel.ThrowIfCancellationRequested();
            return result;
        }

        public static MaterialSample Sample(MaterialRecipe recipe, double u, double v)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (double.IsNaN(u) || double.IsInfinity(u) || double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException("Sample coordinates must be finite.");
            return new Field(recipe).Evaluate(u, v);
        }
        /// <summary>UV +U/+V correspond to tangent +X/+Y. Rising height tilts the normal toward negative gradient.</summary>
        public static Float3 TangentNormal(double heightDerivativeU, double heightDerivativeV)
        {
            if (double.IsNaN(heightDerivativeU) || double.IsInfinity(heightDerivativeU) || double.IsNaN(heightDerivativeV) || double.IsInfinity(heightDerivativeV)) throw new ArgumentException("Height derivatives must be finite.");
            double length = Math.Sqrt(heightDerivativeU * heightDerivativeU + heightDerivativeV * heightDerivativeV + 1);
            if (double.IsInfinity(length)) throw new ArgumentOutOfRangeException("Height derivatives exceed finite normal range.");
            return new Float3((float)(-heightDerivativeU / length), (float)(-heightDerivativeV / length), (float)(1 / length));
        }

        /// <summary>Bounded complete mip chain. Albedo filters in linear space; tangent normals renormalize.</summary>
        public static MaterialData BuildPlane(int resolution, byte[] rgba, string semantic, CancellationToken cancel = default)
        {
            if (resolution < 1 || resolution > 512 || (resolution & (resolution - 1)) != 0 || rgba == null || rgba.Length != resolution * resolution * 4)
                throw new ArgumentException("Expected power-of-two RGBA plane up to512.");
            if (semantic != "albedo" && semantic != "normal" && semantic != "mask") throw new ArgumentException("Unknown material plane semantic.");
            var levels = new List<byte[]> { (byte[])rgba.Clone() }; int size = resolution;
            while (size > 1)
            {
                cancel.ThrowIfCancellationRequested();
                int next = size / 2; byte[] previous = levels[levels.Count - 1], output = new byte[next * next * 4];
                for (int y = 0; y < next; y++)
                {
                    cancel.ThrowIfCancellationRequested();
                    for (int x = 0; x < next; x++)
                    {
                        int p = (y * next + x) * 4;
                        if (semantic == "normal")
                        {
                            double nx = 0, ny = 0, nz = 0;
                            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                            { int q = (((2 * y + dy) % size) * size + (2 * x + dx) % size) * 4; nx += previous[q] / 127.5 - 1; ny += previous[q + 1] / 127.5 - 1; nz += previous[q + 2] / 127.5 - 1; }
                            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                            if (length < 1e-12) { nx = 0; ny = 0; nz = 1; length = 1; }
                            output[p] = Byte(nx / length * .5 + .5); output[p + 1] = Byte(ny / length * .5 + .5); output[p + 2] = Byte(nz / length * .5 + .5); output[p + 3] = 255;
                        }
                        else
                        {
                            for (int channel = 0; channel < 3; channel++)
                            {
                                double sum = 0;
                                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                                {
                                    int q = (((2 * y + dy) % size) * size + (2 * x + dx) % size) * 4 + channel;
                                    sum += semantic == "albedo" ? DecodeSrgb(previous[q]) : previous[q] / 255.0;
                                }
                                output[p + channel] = semantic == "albedo" ? EncodeSrgb(sum / 4) : Byte(sum / 4);
                            }
                            output[p + 3] = 255;
                        }
                    }
                }
                levels.Add(output); size = next;
            }
            cancel.ThrowIfCancellationRequested();
            return new MaterialData((ushort)resolution, (ushort)resolution, semantic == "albedo" ? "rgba8-srgb" : "rgba8-linear", levels);
        }
        private static double Clamp(double x, double low = 0, double high = 1) => Math.Max(low, Math.Min(high, x));
        private static MaterialData SelectResolution(MaterialData plane, int resolution)
        {
            int offset = 0, size = plane.Width;
            while (size > resolution) { size /= 2; offset++; }
            if (offset == 0) return plane;
            var mips = new List<byte[]>();
            for (int level = offset; level < plane.MipBytes.Count; level++)
            {
                var mip = plane.MipBytes[level]; var bytes = new byte[mip.Count];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = mip[i];
                mips.Add(bytes);
            }
            return new MaterialData((ushort)resolution, (ushort)resolution, plane.ChannelFormat, mips);
        }
        private static byte Byte(double value) => (byte)Math.Floor(Clamp(value) * 255 + .5);
        private static byte EncodeSrgb(double linear) => Byte(linear <= .0031308 ? linear * 12.92 : 1.055 * Math.Pow(Clamp(linear), 1 / 2.4) - .055);
        private static double DecodeSrgb(byte srgb) { double v = srgb / 255.0; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }

        private sealed class Field
        {
            private const double Tau = Math.PI * 2;
            private readonly MaterialFamilyDefinition definition;
            private readonly uint macroSeed, grainSeed, wearSeed;
            private readonly double phase;
            public Field(MaterialRecipe recipe)
            {
                definition = MaterialFamilyDefinition.For(recipe.Family);
                macroSeed = NamedRandomStream.Derive(recipe.Document.SeedValue, recipe.Document.RecipeId.ToString(), "material/" + recipe.Family + "/macro").CreateGenerator().NextUInt();
                grainSeed = NamedRandomStream.Derive(recipe.Document.SeedValue, recipe.Document.RecipeId.ToString(), "material/" + recipe.Family + "/grain").CreateGenerator().NextUInt();
                wearSeed = NamedRandomStream.Derive(recipe.Document.SeedValue, recipe.Document.RecipeId.ToString(), "material/" + recipe.Family + "/wear").CreateGenerator().NextUInt();
                phase = wearSeed / 4294967296.0;
            }
            public MaterialSample Evaluate(double u, double v)
            {
                u -= Math.Floor(u); v -= Math.Floor(v);
                double broad = Noise(u, v, 4, macroSeed), medium = Noise(u, v, 16, macroSeed ^ 0x9e3779b9U), grain = Noise(u, v, 64, grainSeed);
                double h = .45 * (broad - .5) + .3 * (medium - .5) + .25 * (grain - .5);
                double brightness = .9 + .2 * broad, rough = definition.Roughness + .1 * (grain - .5), metal = 0, occlusion = .96 - .12 * Math.Max(0, .45 - grain);
                Float3 baseColor = definition.LinearAlbedo;
                switch (definition.Family)
                {
                    case "metal":
                        double exposed = Smooth(.62, .78, Noise(u, v, 8, wearSeed)); metal = exposed;
                        baseColor = Mix(baseColor, new Float3(.36f, .38f, .39f), exposed);
                        rough = .5 - .24 * exposed + .08 * (grain - .5);
                        h = .12 * Math.Sin(Tau * (u * 48 + phase)) + .35 * (grain - .5) + .15 * (broad - .5);
                        brightness = .96 + .08 * medium;
                        break;
                    case "polymer": h = .7 * (grain - .5) + .15 * (medium - .5); brightness = .96 + .08 * broad; break;
                    case "concrete":
                        double pores = Smooth(.7, .92, Noise(u, v, 128, grainSeed ^ 0xa511e9b3U));
                        h -= pores * .45; occlusion -= pores * .13; brightness -= pores * .1; break;
                    case "plaster": h = .55 * (broad - .5) + .25 * (medium - .5) + .1 * (grain - .5); brightness = .97 + .06 * broad; break;
                    case "asphalt":
                        double aggregate = Smooth(.48, .78, grain); h = .65 * (aggregate - .5) + .25 * (medium - .5); brightness = .72 + .55 * aggregate; rough += .06 * (aggregate - .5); break;
                    case "soil": h = .55 * (medium - .5) + .35 * (grain - .5) + .4 * (broad - .5); brightness = .82 + .36 * broad; break;
                    case "fabric":
                        double warp = Math.Sin(Tau * (u * 64 + phase)), weft = Math.Sin(Tau * (v * 64 + phase * .5));
                        double cross = Math.Cos(Tau * (u * 32 + v * 32));
                        h = .34 * warp + .34 * weft + .12 * cross; brightness = .94 + .045 * warp + .045 * weft + .04 * broad;
                        rough = .84 + .08 * (.5 + .5 * cross); occlusion = .9 + .07 * (.5 + .25 * warp + .25 * weft); break;
                }
                var albedo = new Float3((float)Clamp(baseColor.X * brightness, .005, .9), (float)Clamp(baseColor.Y * brightness, .005, .9), (float)Clamp(baseColor.Z * brightness, .005, .9));
                return new MaterialSample(albedo, h * definition.HeightMeters, Clamp(metal), Clamp(occlusion, .65, 1), Clamp(rough, .08, .98));
            }
            private static Float3 Mix(Float3 a, Float3 b, double amount) => new Float3((float)(a.X + (b.X - a.X) * amount), (float)(a.Y + (b.Y - a.Y) * amount), (float)(a.Z + (b.Z - a.Z) * amount));
            private static double Smooth(double a, double b, double value) { double x = Clamp((value - a) / (b - a)); return x * x * (3 - 2 * x); }
            private static double Noise(double u, double v, int period, uint seed)
            {
                double x = u * period, y = v * period; int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y); double fx = x - ix, fy = y - iy;
                fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10); fy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
                double a = Cell(ix, iy, period, seed), b = Cell(ix + 1, iy, period, seed), c = Cell(ix, iy + 1, period, seed), d = Cell(ix + 1, iy + 1, period, seed);
                return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
            }
            private static double Cell(int x, int y, int period, uint seed)
            {
                unchecked
                {
                    uint hash = seed ^ (uint)((x % period + period) % period) * 0x9e3779b9U ^ (uint)((y % period + period) % period) * 0x85ebca6bU;
                    hash ^= hash >> 16; hash *= 0x7feb352dU; hash ^= hash >> 15; hash *= 0x846ca68bU; hash ^= hash >> 16;
                    return (hash >> 8) / 16777216.0;
                }
            }
        }
    }
}
