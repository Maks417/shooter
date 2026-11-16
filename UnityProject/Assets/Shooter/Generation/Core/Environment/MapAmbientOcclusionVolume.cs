#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-081 baked ambient-occlusion (sky-visibility) volume for a compact arena. Pure and deterministic: every
    /// computation is IEEE double add/sub/mul/div/sqrt on integer-derived inputs (no trig), so the .NET tools lane and
    /// Unity's Mono produce identical bytes. Cells are 0.5 m in X/Z and 1 m in Y over a fixed 80 × 16 × 96 m box.
    /// Each cell centre casts 17 rays (the integer lattice directions with y ≥ 0) of at most
    /// <see cref="RayLengthMeters"/> against the plan's axis-aligned collision boxes; upward rays weigh more.
    /// </summary>
    public static class MapAmbientOcclusionVolume
    {
        public const string Path = "lighting-ambient-occlusion.bin";
        public const int SizeX = 160, SizeY = 16, SizeZ = 192;
        public const double CellXZ = 0.5, CellY = 1.0;
        public const double MinX = -40.0, MinY = 0.0, MinZ = -48.0;
        public const double RayLengthMeters = 6.0;
        public const int LayersPerTexel = 4;
        private const double BinMeters = 4.0;
        private const int DilationPasses = 3;
        private const byte SolidMarker = 0;

        private static readonly double[] DirX, DirY, DirZ, Weight;
        private static readonly double WeightSum;
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, byte[]> Cache = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        static MapAmbientOcclusionVolume()
        {
            var x = new List<double>(); var y = new List<double>(); var z = new List<double>(); var w = new List<double>();
            for (int j = 0; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                    for (int k = -1; k <= 1; k++)
                    {
                        if (i == 0 && j == 0 && k == 0) continue;
                        double length = Math.Sqrt(i * i + j * j + k * k);
                        x.Add(i / length); y.Add(j / length); z.Add(k / length);
                        w.Add(0.5 + 0.5 * (j / length));
                    }
            DirX = x.ToArray(); DirY = y.ToArray(); DirZ = z.ToArray(); Weight = w.ToArray();
            double sum = 0;
            for (int d = 0; d < Weight.Length; d++) sum += Weight[d];
            WeightSum = sum;
        }

        public static int RayCount => DirX.Length;
        public static int CellCount => SizeX * SizeY * SizeZ;

        /// <summary>Volume index matching a Unity Texture3D (width X, height Y, depth Z): x fastest, then y, then z.</summary>
        public static int Index(int x, int y, int z) => x + SizeX * (y + SizeY * z);

        public static double CellCenterX(int x) => MinX + (x + 0.5) * CellXZ;
        public static double CellCenterY(int y) => MinY + (y + 0.5) * CellY;
        public static double CellCenterZ(int z) => MinZ + (z + 0.5) * CellXZ;

        /// <summary>Bake (cached per map and collision identity). Bytes: 255 = open sky, 0 = fully occluded.</summary>
        public static byte[] Bake(string cacheKey, IReadOnlyList<CollisionBox> boxes)
        {
            if (string.IsNullOrEmpty(cacheKey)) throw new ArgumentException("Cache key required.", nameof(cacheKey));
            lock (CacheLock)
            {
                if (Cache.TryGetValue(cacheKey, out var cached)) return cached;
            }
            byte[] volume = BakeUncached(boxes);
            lock (CacheLock)
            {
                Cache[cacheKey] = volume;
            }
            return volume;
        }

        public static byte[] BakeUncached(IReadOnlyList<CollisionBox> boxes)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            int count = boxes.Count;
            var min = new double[count * 3];
            var max = new double[count * 3];
            for (int b = 0; b < count; b++)
            {
                var box = boxes[b];
                double cx = box.Center.X, cy = box.Center.Y, cz = box.Center.Z;
                double hx = box.Size.X * 0.5, hy = box.Size.Y * 0.5, hz = box.Size.Z * 0.5;
                min[b * 3] = cx - hx; min[b * 3 + 1] = cy - hy; min[b * 3 + 2] = cz - hz;
                max[b * 3] = cx + hx; max[b * 3 + 1] = cy + hy; max[b * 3 + 2] = cz + hz;
            }

            int binsX = (int)Math.Ceiling(SizeX * CellXZ / BinMeters), binsZ = (int)Math.Ceiling(SizeZ * CellXZ / BinMeters);
            var bins = new List<int>[binsX * binsZ];
            for (int i = 0; i < bins.Length; i++) bins[i] = new List<int>();
            for (int b = 0; b < count; b++)
            {
                int x0 = Bin(min[b * 3] - MinX, binsX), x1 = Bin(max[b * 3] - MinX, binsX);
                int z0 = Bin(min[b * 3 + 2] - MinZ, binsZ), z1 = Bin(max[b * 3 + 2] - MinZ, binsZ);
                for (int bz = z0; bz <= z1; bz++)
                    for (int bx = x0; bx <= x1; bx++)
                        bins[bx + binsX * bz].Add(b);
            }

            var volume = new byte[CellCount];
            var solid = new bool[CellCount];
            var stamp = new int[count];
            int stampValue = 0;
            var candidates = new List<int>(64);
            for (int z = 0; z < SizeZ; z++)
            {
                double pz = CellCenterZ(z);
                for (int x = 0; x < SizeX; x++)
                {
                    double px = CellCenterX(x);
                    // Candidate boxes for this column: every bin within the ray length, de-duplicated in bin order.
                    stampValue++;
                    candidates.Clear();
                    int bx0 = Bin(px - RayLengthMeters - MinX, binsX), bx1 = Bin(px + RayLengthMeters - MinX, binsX);
                    int bz0 = Bin(pz - RayLengthMeters - MinZ, binsZ), bz1 = Bin(pz + RayLengthMeters - MinZ, binsZ);
                    for (int bz = bz0; bz <= bz1; bz++)
                        for (int bx = bx0; bx <= bx1; bx++)
                            foreach (int b in bins[bx + binsX * bz])
                            {
                                if (stamp[b] == stampValue) continue;
                                stamp[b] = stampValue;
                                candidates.Add(b);
                            }

                    for (int y = 0; y < SizeY; y++)
                    {
                        double py = CellCenterY(y);
                        int index = Index(x, y, z);
                        if (Inside(candidates, min, max, px, py, pz))
                        {
                            solid[index] = true;
                            volume[index] = SolidMarker;
                            continue;
                        }
                        double open = 0;
                        for (int d = 0; d < DirX.Length; d++)
                            if (!Blocked(candidates, min, max, px, py, pz, DirX[d], DirY[d], DirZ[d]))
                                open += Weight[d];
                        volume[index] = Quantize(open / WeightSum);
                    }
                }
            }

            Dilate(volume, solid);
            return volume;
        }

        /// <summary>Pack four Y layers per RGBA texel: width X, height Z × (SizeY / 4). Channel c of slab s is layer 4s + c.</summary>
        public static MaterialData ToAtlas(byte[] volume)
        {
            if (volume == null || volume.Length != CellCount) throw new ArgumentException("Volume size mismatch.", nameof(volume));
            int slabs = SizeY / LayersPerTexel;
            var pixels = new byte[SizeX * SizeZ * slabs * 4];
            for (int s = 0; s < slabs; s++)
                for (int z = 0; z < SizeZ; z++)
                    for (int x = 0; x < SizeX; x++)
                    {
                        int texel = (x + SizeX * (z + SizeZ * s)) * 4;
                        for (int c = 0; c < LayersPerTexel; c++) pixels[texel + c] = volume[Index(x, s * LayersPerTexel + c, z)];
                    }
            return new MaterialData((ushort)SizeX, (ushort)(SizeZ * slabs), "rgba8-linear", MipChain(pixels, SizeX, SizeZ * slabs));
        }

        /// <summary>The frozen material codec requires a full mip chain; lower levels are 2x2 box filters and unused at runtime.</summary>
        private static List<byte[]> MipChain(byte[] level0, int width, int height)
        {
            var mips = new List<byte[]> { level0 };
            byte[] current = level0;
            while (width > 1 || height > 1)
            {
                int w = Math.Max(1, width / 2), h = Math.Max(1, height / 2);
                var next = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        for (int c = 0; c < 4; c++)
                        {
                            int sum = 0, n = 0;
                            for (int dy = 0; dy < 2; dy++)
                                for (int dx = 0; dx < 2; dx++)
                                {
                                    int sx = Math.Min(width - 1, x * 2 + dx), sy = Math.Min(height - 1, y * 2 + dy);
                                    sum += current[(sx + width * sy) * 4 + c]; n++;
                                }
                            next[(x + w * y) * 4 + c] = (byte)((sum + n / 2) / n);
                        }
                mips.Add(next);
                current = next; width = w; height = h;
            }
            return mips;
        }

        public static byte[] FromAtlas(MaterialData atlas)
        {
            if (atlas == null) throw new ArgumentNullException(nameof(atlas));
            int slabs = SizeY / LayersPerTexel;
            if (atlas.Width != SizeX || atlas.Height != SizeZ * slabs || atlas.ChannelFormat != "rgba8-linear" || atlas.MipBytes.Count < 1)
                throw new FormatException("Ambient-occlusion atlas has unexpected dimensions or format.");
            var pixels = atlas.MipBytes[0];
            var volume = new byte[CellCount];
            for (int s = 0; s < slabs; s++)
                for (int z = 0; z < SizeZ; z++)
                    for (int x = 0; x < SizeX; x++)
                    {
                        int texel = (x + SizeX * (z + SizeZ * s)) * 4;
                        for (int c = 0; c < LayersPerTexel; c++) volume[Index(x, s * LayersPerTexel + c, z)] = pixels[texel + c];
                    }
            return volume;
        }

        private static int Bin(double offsetMeters, int bins)
        {
            int bin = (int)Math.Floor(offsetMeters / BinMeters);
            return bin < 0 ? 0 : bin >= bins ? bins - 1 : bin;
        }

        private static byte Quantize(double visibility)
        {
            double scaled = Math.Floor(visibility * 255.0 + 0.5);
            return (byte)(scaled < 1 ? 1 : scaled > 255 ? 255 : scaled);
        }

        private static bool Inside(List<int> candidates, double[] min, double[] max, double px, double py, double pz)
        {
            foreach (int b in candidates)
            {
                int o = b * 3;
                if (px > min[o] && px < max[o] && py > min[o + 1] && py < max[o + 1] && pz > min[o + 2] && pz < max[o + 2]) return true;
            }
            return false;
        }

        private static bool Blocked(List<int> candidates, double[] min, double[] max,
            double px, double py, double pz, double dx, double dy, double dz)
        {
            foreach (int b in candidates)
            {
                int o = b * 3;
                double near = 1e-6, far = RayLengthMeters;
                if (!Slab(px, dx, min[o], max[o], ref near, ref far)) continue;
                if (!Slab(py, dy, min[o + 1], max[o + 1], ref near, ref far)) continue;
                if (!Slab(pz, dz, min[o + 2], max[o + 2], ref near, ref far)) continue;
                return true;
            }
            return false;
        }

        private static bool Slab(double p, double d, double lo, double hi, ref double near, ref double far)
        {
            if (d == 0) return p >= lo && p <= hi;
            double t1 = (lo - p) / d, t2 = (hi - p) / d;
            if (t1 > t2) { double t = t1; t1 = t2; t2 = t; }
            if (t1 > near) near = t1;
            if (t2 < far) far = t2;
            return near <= far;
        }

        /// <summary>Solid cells take the mean of their non-solid face neighbours so trilinear sampling never bleeds black.</summary>
        private static void Dilate(byte[] volume, bool[] solid)
        {
            var filled = (bool[])solid.Clone();
            for (int i = 0; i < filled.Length; i++) filled[i] = !solid[i];
            for (int pass = 0; pass < DilationPasses; pass++)
            {
                var next = (bool[])filled.Clone();
                var values = (byte[])volume.Clone();
                for (int z = 0; z < SizeZ; z++)
                    for (int y = 0; y < SizeY; y++)
                        for (int x = 0; x < SizeX; x++)
                        {
                            int index = Index(x, y, z);
                            if (filled[index]) continue;
                            int sum = 0, n = 0;
                            void Take(int nx, int ny, int nz)
                            {
                                if (nx < 0 || ny < 0 || nz < 0 || nx >= SizeX || ny >= SizeY || nz >= SizeZ) return;
                                int ni = Index(nx, ny, nz);
                                if (!filled[ni]) return;
                                sum += volume[ni]; n++;
                            }
                            Take(x - 1, y, z); Take(x + 1, y, z); Take(x, y - 1, z); Take(x, y + 1, z); Take(x, y, z - 1); Take(x, y, z + 1);
                            if (n == 0) continue;
                            values[index] = (byte)((sum + n / 2) / n);
                            next[index] = true;
                        }
                Array.Copy(values, volume, volume.Length);
                filled = next;
            }
        }
    }
}
