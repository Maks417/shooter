#nullable enable
using System;

namespace Shooter.Presentation.Hud
{
    /// <summary>Analytic shape the ADR-084 UI SDF shader evaluates.</summary>
    public enum UiShapeKind : byte
    {
        /// <summary>Rounded rectangle (radius 0 = sharp rectangle), filled or stroked.</summary>
        RoundedRect = 0,
        /// <summary>Circle or ring inscribed in the rect.</summary>
        Circle = 1,
        /// <summary>Inverse edge falloff over the whole rect (vignette).</summary>
        EdgeFade = 2,
        /// <summary>Ring arc around the rect centre, between two angles.</summary>
        Arc = 3,
        /// <summary>Upward triangle inscribed in the rect.</summary>
        Triangle = 4,
        /// <summary>Diamond (rotated square) inscribed in the rect.</summary>
        Diamond = 5,
        /// <summary>ADR-085 icon from the generated SDF atlas, stretched over the rect.</summary>
        Glyph = 6,
    }

    /// <summary>
    /// ADR-084 brand shape tokens (logical pixels at 100% UI scale) and their pure helper math.
    /// The shader works in physical pixels; <see cref="AntiAliasPixels"/> keeps edges one device pixel soft.
    /// </summary>
    public static class UiShapeParams
    {
        public const float PlateRadius = 4f;
        public const float ButtonRadius = 3f;
        public const float CardRadius = 6f;
        public const float ChipRadius = 2f;
        public const float HudPlateRadius = 3f;

        public const float ShadowOffset = 3f;
        public const float ShadowSoftness = 10f;
        public const float ShadowAlpha = 0.38f;

        public const float FocusGlowWidth = 8f;
        public const float FocusGlowAlpha = 0.28f;

        /// <summary>Hairline width used for plate edges and dividers.</summary>
        public const float Hairline = 1f;

        /// <summary>Anti-alias width in logical px so edges stay ~1 device pixel at any UI scale.</summary>
        public static float AntiAliasPixels(float uiScale) => uiScale <= 0f ? 1f : 1f / uiScale;

        /// <summary>
        /// Quad padding needed around a shape so its shadow/glow is not cut off (logical px).
        /// </summary>
        public static float Padding(float shadowSoftness, float shadowOffset, float glowWidth) =>
            (float)Math.Ceiling(Math.Max(0f, shadowSoftness) + Math.Abs(shadowOffset) + Math.Max(0f, glowWidth) + 1f);

        /// <summary>Clamp a corner radius to what fits inside a rect.</summary>
        public static float FitRadius(float radius, float width, float height)
        {
            float max = Math.Max(0f, Math.Min(width, height) * 0.5f);
            return radius < 0f ? 0f : radius > max ? max : radius;
        }

        /// <summary>
        /// Signed distance from point (px,py) to a rounded box centred at the origin with half extents (hx,hy)
        /// and radius r. Negative inside. Mirrors <c>UiSdf.shader</c> so tests pin the shader's geometry.
        /// </summary>
        public static float RoundedBoxDistance(float px, float py, float hx, float hy, float r)
        {
            r = FitRadius(r, hx * 2f, hy * 2f);
            float qx = Math.Abs(px) - hx + r;
            float qy = Math.Abs(py) - hy + r;
            float ox = Math.Max(qx, 0f), oy = Math.Max(qy, 0f);
            float outside = (float)Math.Sqrt(ox * ox + oy * oy);
            float inside = Math.Min(Math.Max(qx, qy), 0f);
            return outside + inside - r;
        }

        /// <summary>Coverage (0..1) from a signed distance with a one-pixel anti-alias ramp.</summary>
        public static float Coverage(float distance, float antiAlias)
        {
            float aa = antiAlias <= 0f ? 1f : antiAlias;
            float t = 0.5f - distance / aa;
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        /// <summary>
        /// Edge-fade (vignette) alpha at distance <paramref name="inside"/> from the nearest screen edge.
        /// Quadratic falloff to zero at <paramref name="band"/>.
        /// </summary>
        public static float EdgeFade(float inside, float band, float strength)
        {
            if (band <= 0f || strength <= 0f) return 0f;
            float u = inside / band;
            if (u >= 1f) return 0f;
            if (u <= 0f) return strength;
            float k = 1f - u;
            return strength * k * k;
        }

        /// <summary>Normalises an arc span to (start, sweep) radians with sweep in (0, 2π].</summary>
        public static (float Start, float Sweep) NormaliseArc(float startRadians, float endRadians)
        {
            const float Tau = (float)(Math.PI * 2.0);
            float sweep = endRadians - startRadians;
            if (sweep <= 0f) sweep += Tau * (float)Math.Ceiling(-sweep / Tau + 1e-6f);
            if (sweep > Tau) sweep = Tau;
            float start = startRadians % Tau;
            if (start < 0f) start += Tau;
            return (start, sweep);
        }
    }
}
