#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Icons
{
    /// <summary>
    /// ADR-085 side-view silhouette of packaged weapon meshes (+Z → right, +Y → up) as a signed distance field.
    /// Triangles are rasterised into a supersampled coverage mask, then an exact Euclidean distance transform
    /// gives the signed distance. Double precision with IEEE operations only, so lanes agree.
    /// </summary>
    public static class MeshSilhouette
    {
        public const int Supersample = 4;

        /// <summary>
        /// Signed distance (output pixels, negative inside) for a <paramref name="width"/> × <paramref name="height"/>
        /// tile, row 0 at the top. The projected silhouette is fitted uniformly with <paramref name="marginPx"/> on all sides.
        /// </summary>
        public static double[] SignedDistance(IReadOnlyList<MeshData> meshes, int width, int height, int marginPx)
        {
            if (meshes == null || meshes.Count == 0) throw new ArgumentException("Meshes required.", nameof(meshes));
            if (width < 8 || height < 8 || width > 2048 || height > 2048) throw new ArgumentOutOfRangeException(nameof(width));
            if (marginPx < 0 || marginPx * 2 >= Math.Min(width, height)) throw new ArgumentOutOfRangeException(nameof(marginPx));

            double minZ = double.MaxValue, maxZ = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (var mesh in meshes)
                foreach (var p in mesh.Positions)
                {
                    minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                }
            double spanZ = maxZ - minZ, spanY = maxY - minY;
            if (!(spanZ > 0) || !(spanY > 0)) throw new InvalidOperationException("Degenerate silhouette bounds.");

            int sw = width * Supersample, sh = height * Supersample;
            double availW = (width - 2 * marginPx) * Supersample, availH = (height - 2 * marginPx) * Supersample;
            double scale = Math.Min(availW / spanZ, availH / spanY);
            double offX = (sw - spanZ * scale) * 0.5, offY = (sh - spanY * scale) * 0.5;

            var inside = new bool[sw * sh];
            foreach (var mesh in meshes)
            {
                var pos = mesh.Positions;
                var idx = mesh.Indices;
                for (int t = 0; t + 2 < idx.Count; t += 3)
                {
                    var a = pos[(int)idx[t]]; var b = pos[(int)idx[t + 1]]; var c = pos[(int)idx[t + 2]];
                    double ax = offX + (a.Z - minZ) * scale, ay = offY + (maxY - a.Y) * scale;
                    double bx = offX + (b.Z - minZ) * scale, by = offY + (maxY - b.Y) * scale;
                    double cx = offX + (c.Z - minZ) * scale, cy = offY + (maxY - c.Y) * scale;
                    double area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                    if (area == 0) continue;
                    int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
                    int x1 = Math.Min(sw - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                    int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
                    int y1 = Math.Min(sh - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                    for (int y = y0; y <= y1; y++)
                    {
                        double py = y + 0.5;
                        for (int x = x0; x <= x1; x++)
                        {
                            double px = x + 0.5;
                            double w0 = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
                            double w1 = (cx - bx) * (py - by) - (cy - by) * (px - bx);
                            double w2 = (ax - cx) * (py - cy) - (ay - cy) * (px - cx);
                            if ((w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0))
                                inside[y * sw + x] = true;
                        }
                    }
                }
            }

            double[] toInside = DistanceTransform(inside, sw, sh, wantInside: true);
            double[] toOutside = DistanceTransform(inside, sw, sh, wantInside: false);
            var result = new double[width * height];
            int mid = Supersample / 2;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int s = (y * Supersample + mid) * sw + x * Supersample + mid;
                    double d = inside[s] ? -(toOutside[s] - 0.5) : toInside[s] - 0.5;
                    result[y * width + x] = d / Supersample;
                }
            return result;
        }

        /// <summary>Exact Euclidean distance (samples) from every sample to the nearest sample whose inside flag equals <paramref name="wantInside"/>.</summary>
        private static double[] DistanceTransform(bool[] inside, int w, int h, bool wantInside)
        {
            const double Inf = 1e20;
            var f = new double[w * h];
            for (int i = 0; i < f.Length; i++) f[i] = inside[i] == wantInside ? 0 : Inf;
            var buffer = new double[Math.Max(w, h)];
            var output = new double[Math.Max(w, h)];
            var v = new int[Math.Max(w, h)];
            var z = new double[Math.Max(w, h) + 1];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) buffer[y] = f[y * w + x];
                Transform1D(buffer, h, output, v, z);
                for (int y = 0; y < h; y++) f[y * w + x] = output[y];
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) buffer[x] = f[y * w + x];
                Transform1D(buffer, w, output, v, z);
                for (int x = 0; x < w; x++) f[y * w + x] = Math.Sqrt(output[x]);
            }
            return f;
        }

        private static double Intersect(double[] f, int q, int p) =>
            ((f[q] + (double)q * q) - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);

        // Felzenszwalb & Huttenlocher squared-distance lower envelope.
        private static void Transform1D(double[] f, int n, double[] d, int[] v, double[] z)
        {
            const double Inf = 1e20;
            int k = 0;
            v[0] = 0; z[0] = -Inf; z[1] = Inf;
            for (int q = 1; q < n; q++)
            {
                double s = Intersect(f, q, v[k]);
                // z[0] is -Inf, so this always stops at k = 0.
                while (s <= z[k]) { k--; s = Intersect(f, q, v[k]); }
                k++;
                v[k] = q; z[k] = s; z[k + 1] = Inf;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                double dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }
    }
}
