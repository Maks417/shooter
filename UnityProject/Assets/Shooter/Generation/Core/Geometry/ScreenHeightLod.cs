#nullable enable
using System;

namespace Shooter.Generation
{
    /// <summary>Presentation-only four-level selector. Thresholds are fractions of viewport height.</summary>
    public static class ScreenHeightLod
    {
        public const float Hysteresis = .10f;
        private static readonly float[] Thresholds = { .50f, .20f, .08f };
        public static int Select(float height, int previous = -1, bool firstPerson = false)
        {
            if (float.IsNaN(height) || float.IsInfinity(height) || height < 0 || previous < -1 || previous > 3)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (firstPerson) return 0;
            if (previous < 0)
            {
                int initial = 0;
                while (initial < 3 && height < Thresholds[initial]) initial++;
                return initial;
            }
            int lod = previous;
            while (lod < 3 && height < Thresholds[lod] * (1 - Hysteresis)) lod++;
            while (lod > 0 && height > Thresholds[lod - 1] * (1 + Hysteresis)) lod--;
            return lod;
        }
        public static float PerspectiveHeight(float diameterMeters, float forwardDepthMeters, float verticalFieldOfViewDegrees)
        {
            if (!Finite(diameterMeters) || diameterMeters <= 0 || !Finite(forwardDepthMeters) ||
                !Finite(verticalFieldOfViewDegrees) || verticalFieldOfViewDegrees <= 0 || verticalFieldOfViewDegrees >= 180)
                throw new ArgumentOutOfRangeException(nameof(diameterMeters));
            // Intersecting/behind the eye uses near detail, without changing visibility or authority.
            if (forwardDepthMeters <= diameterMeters * .5f) return float.MaxValue;
            return (float)(diameterMeters / (2 * forwardDepthMeters * Math.Tan(verticalFieldOfViewDegrees * Math.PI / 360)));
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
