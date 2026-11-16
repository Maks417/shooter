#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Analytic equipment-relative magazine path for reload presentation.
    /// Two-phase remove/insert arcs; empty mags drop farther. Never mutates authority ammo.
    /// </summary>
    public static class FirstPersonMagazineTravel
    {
        public const float RemoveEnd = 0.42f;
        public const float InsertStart = 0.42f;

        /// <summary>
        /// Camera-local offset for the support hand / mag proxy during reload.
        /// Phase in (0,1]; non-positive phase returns zero (no travel).
        /// </summary>
        public static Float3 Offset(float reloadPhase, bool magazineEmpty, bool reducedMotion)
        {
            if (reloadPhase <= 0f) return default;
            float t = Math.Clamp(reloadPhase, 0f, 1f);
            float amp = reducedMotion ? 0.55f : 1f;
            float empty = magazineEmpty ? 1.35f : 1f;

            // Let fingers reach and close before removal; finish seating before they return to the fore-end.
            if (t <= .22f || t >= .78f) return default;
            if (t <= RemoveEnd)
            {
                // Pull mag down and out of the well (readable remove).
                float u = (t-.22f) / (RemoveEnd-.22f);
                float ease = u * u * (3f - 2f * u);
                return new Float3(
                    0.028f * ease * amp,
                    -(0.095f + 0.035f * (empty - 1f)) * ease * amp,
                    -0.055f * ease * amp);
            }

            // Seat a fresh mag: rise into the well then settle.
            float v = (t - InsertStart) / (.78f - InsertStart);
            float settle = 1f - v * v * (3f - 2f * v);
            return new Float3(
                0.028f * settle * amp,
                -(0.095f + 0.035f * (empty - 1f)) * settle * amp,
                -0.055f * settle * amp);
        }

        /// <summary>Peak remove depth (more negative for empty). Used by pure tests.</summary>
        public static float PeakRemoveDepth(bool magazineEmpty, bool reducedMotion)
        {
            Float3 peak = Offset(RemoveEnd, magazineEmpty, reducedMotion);
            return peak.Y;
        }
    }
}
