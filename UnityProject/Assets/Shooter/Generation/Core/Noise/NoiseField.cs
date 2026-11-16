#nullable enable
using System;
using Shooter.Generation.Noise.ThirdParty;

namespace Shooter.Generation.Noise
{
    /// <summary>Recipe-named noise bases backed by vendored FastNoiseLite v1.1.1, ported to double precision (ADR-075).</summary>
    public enum NoiseBasis
    {
        /// <summary>OpenSimplex2 gradient noise, FBm when octaves &gt; 1.</summary>
        Gradient,
        /// <summary>Euclidean cellular (Worley) distance to the nearest feature point.</summary>
        Cellular,
        /// <summary>Ridged OpenSimplex2 fractal: crack and vein lines.</summary>
        RidgedFbm,
        /// <summary>OpenSimplex2 FBm sampled through a progressive OpenSimplex2 domain warp.</summary>
        DomainWarpedFbm,
    }

    /// <summary>
    /// The only API recipes use for third-party noise. Seeds come from a <see cref="NamedRandomStream"/> (ADR-009) and every
    /// FastNoiseLite setting is set explicitly, so upstream default changes cannot silently move output.
    /// <see cref="SamplePeriodic"/> tiles with period 1 in u and v: it blends the four lattice-shifted copies with quintic weights
    /// (C1 across the seam, so height-derived normals show no seam line) and renormalizes by the weight energy so the
    /// tile centre keeps the same contrast as its edges.
    /// </summary>
    public sealed class NoiseField
    {
        public const int MaxPeriod = 256;
        public const int MaxOctaves = 8;
        private readonly FastNoiseLite noise;
        private readonly FastNoiseLite? warp;
        public NoiseBasis Basis { get; }
        public int Period { get; }
        public int Octaves { get; }

        private NoiseField(NoiseBasis basis, int period, int octaves, FastNoiseLite noise, FastNoiseLite? warp)
        { Basis = basis; Period = period; Octaves = octaves; this.noise = noise; this.warp = warp; }

        /// <param name="period">Feature cycles across one tile (1..256); integer so the tile edge stays on the lattice.</param>
        /// <param name="octaves">Fractal octaves (1..8); ignored by <see cref="NoiseBasis.Cellular"/>.</param>
        public static NoiseField Create(NoiseBasis basis, NamedRandomStream stream, int period, int octaves = 1)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!Enum.IsDefined(typeof(NoiseBasis), basis)) throw new ArgumentOutOfRangeException(nameof(basis));
            if (period < 1 || period > MaxPeriod) throw new ArgumentOutOfRangeException(nameof(period), "Noise period must be 1..256.");
            if (octaves < 1 || octaves > MaxOctaves) throw new ArgumentOutOfRangeException(nameof(octaves), "Noise octaves must be 1..8.");
            var generator = stream.CreateGenerator();
            int seed = unchecked((int)generator.NextUInt());
            var noise = Configure(seed);
            FastNoiseLite? warp = null;
            switch (basis)
            {
                case NoiseBasis.Gradient:
                    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                    noise.SetFractalType(octaves > 1 ? FastNoiseLite.FractalType.FBm : FastNoiseLite.FractalType.None);
                    break;
                case NoiseBasis.Cellular:
                    noise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
                    noise.SetFractalType(FastNoiseLite.FractalType.None);
                    noise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.Euclidean);
                    noise.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
                    noise.SetCellularJitter(1f);
                    break;
                case NoiseBasis.RidgedFbm:
                    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                    noise.SetFractalType(FastNoiseLite.FractalType.Ridged);
                    break;
                case NoiseBasis.DomainWarpedFbm:
                    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                    noise.SetFractalType(octaves > 1 ? FastNoiseLite.FractalType.FBm : FastNoiseLite.FractalType.None);
                    warp = Configure(unchecked((int)generator.NextUInt()));
                    warp.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
                    warp.SetDomainWarpAmp(1.5f);
                    warp.SetFractalType(FastNoiseLite.FractalType.DomainWarpProgressive);
                    warp.SetFractalOctaves(Math.Min(octaves, 3));
                    break;
            }
            noise.SetFractalOctaves(octaves);
            return new NoiseField(basis, period, octaves, noise, warp);
        }

        /// <summary>Tileable sample in [0, 1] with period 1 in both u and v.</summary>
        public double SamplePeriodic(double u, double v)
        {
            if (double.IsNaN(u) || double.IsInfinity(u) || double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException("Noise coordinates must be finite.");
            u -= Math.Floor(u); v -= Math.Floor(v);
            double su = Quintic(u), sv = Quintic(v);
            double w00 = (1 - su) * (1 - sv), w10 = su * (1 - sv), w01 = (1 - su) * sv, w11 = su * sv;
            double x = u * Period, y = v * Period;
            double sum = w00 * Centered(x, y) + w10 * Centered(x - Period, y) + w01 * Centered(x, y - Period) + w11 * Centered(x - Period, y - Period);
            double energy = Math.Sqrt(w00 * w00 + w10 * w10 + w01 * w01 + w11 * w11);
            return Math.Max(0, Math.Min(1, .5 + .5 * sum / energy));
        }

        // Energy renormalization is only mean-preserving for zero-mean input, so each basis is centred and scaled to about [-1, 1]
        // first. Cellular Euclidean distance spans roughly [-1, -0.1] (measured mean -0.563); the OpenSimplex2 bases are ~zero-mean.
        // These constants are part of the algorithm: changing them changes output and needs a version bump.
        private const double CellularCentre = -.55, CellularHalfRange = .45;
        private double Centered(double x, double y)
        {
            double raw = Raw(x, y);
            return Basis == NoiseBasis.Cellular ? (raw - CellularCentre) / CellularHalfRange : raw;
        }

        /// <summary>Unwrapped FastNoiseLite output (about [-1, 1]) at lattice coordinates; for golden tests and diagnostics.</summary>
        public double Raw(double x, double y)
        {
            if (warp != null) warp.DomainWarp(ref x, ref y);
            return noise.GetNoise(x, y);
        }

        private static FastNoiseLite Configure(int seed)
        {
            var value = new FastNoiseLite(seed);
            value.SetFrequency(1f);
            value.SetFractalLacunarity(2f);
            value.SetFractalGain(.5f);
            value.SetFractalWeightedStrength(0f);
            return value;
        }

        private static double Quintic(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    }
}
