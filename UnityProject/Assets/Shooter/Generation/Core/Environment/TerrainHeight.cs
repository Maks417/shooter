#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Low-frequency analytic height. Combat compound stays predominantly flat.</summary>
    public static class TerrainHeight
    {
        public const float SampleSpacing = 1f;

        public static float Sample(ulong seed, float x, float z)
        {
            // Bounded slope undulation; amplitude kept under 5 cm so first nav remains flat-dominant.
            double sx = x * 0.015 + (seed % 997) * 0.001;
            double sz = z * 0.017 + ((seed / 997) % 991) * 0.001;
            double h = Math.Sin(sx) * Math.Cos(sz) * 0.04;
            return (float)h;
        }

        public static float[,] BuildGrid(ulong seed, int resolution)
        {
            if (resolution < 2 || resolution > 513) throw new ArgumentOutOfRangeException(nameof(resolution));
            var grid = new float[resolution, resolution];
            float extent = CompoundPlan.MapHalfExtent;
            float step = (extent * 2f) / (resolution - 1);
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float wx = -extent + x * step;
                float wz = -extent + z * step;
                grid[x, z] = Sample(seed, wx, wz);
            }
            return grid;
        }

        public static float BorderError(float[,] a, float[,] b)
        {
            if (a == null || b == null) throw new ArgumentNullException(nameof(a));
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1))
                throw new ArgumentException("Grid size mismatch.");
            int n = a.GetLength(0);
            float max = 0;
            for (int i = 0; i < n; i++)
            {
                max = Math.Max(max, Math.Abs(a[i, 0] - b[i, 0]));
                max = Math.Max(max, Math.Abs(a[i, n - 1] - b[i, n - 1]));
                max = Math.Max(max, Math.Abs(a[0, i] - b[0, i]));
                max = Math.Max(max, Math.Abs(a[n - 1, i] - b[n - 1, i]));
            }
            return max;
        }

        /// <summary>Neighbor chunk border samples must agree within 1e-4 m for the same seed.</summary>
        public static float MaxNeighborBorderError(ulong seed)
        {
            float max = 0;
            const float half = CompoundPlan.ChunkSize * 0.5f;
            for (int cz = 0; cz < CompoundPlan.ChunkCount; cz++)
            for (int cx = 0; cx < CompoundPlan.ChunkCount; cx++)
            {
                float ox = -CompoundPlan.MapHalfExtent + cx * CompoundPlan.ChunkSize + half;
                float oz = -CompoundPlan.MapHalfExtent + cz * CompoundPlan.ChunkSize + half;
                if (cx + 1 < CompoundPlan.ChunkCount)
                {
                    float x = ox + half;
                    for (int i = 0; i <= 32; i++)
                    {
                        float z = oz - half + i;
                        float left = Sample(seed, x - 1e-5f, z);
                        float right = Sample(seed, x + 1e-5f, z);
                        max = Math.Max(max, Math.Abs(left - right));
                    }
                }
                if (cz + 1 < CompoundPlan.ChunkCount)
                {
                    float z = oz + half;
                    for (int i = 0; i <= 32; i++)
                    {
                        float x = ox - half + i;
                        float south = Sample(seed, x, z - 1e-5f);
                        float north = Sample(seed, x, z + 1e-5f);
                        max = Math.Max(max, Math.Abs(south - north));
                    }
                }
            }
            return max;
        }
    }
}
