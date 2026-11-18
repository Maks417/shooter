#nullable enable
using UnityEngine;

namespace Shooter.Presentation.Rendering
{
    /// <summary>
    /// Presentation-only URP Lit response knobs for packaged map surfaces.
    /// Does not rewrite verified albedo/normal/mask bytes; multiplies BaseColor and scales bump/occlusion.
    /// </summary>
    public readonly struct ProductSurfaceResponse
    {
        public readonly Color BaseColor;
        public readonly float BumpScale;
        public readonly float OcclusionStrength;
        public readonly float MetallicFallback;
        public readonly float SmoothnessFallback;

        public ProductSurfaceResponse(
            Color baseColor,
            float bumpScale,
            float occlusionStrength,
            float metallicFallback,
            float smoothnessFallback)
        {
            BaseColor = baseColor;
            BumpScale = bumpScale;
            OcclusionStrength = occlusionStrength;
            MetallicFallback = metallicFallback;
            SmoothnessFallback = smoothnessFallback;
        }

        // Cool plaster/concrete walls vs warm yard soil and darker asphalt lanes. ADR-106 lowered every bump scale
        // about a quarter: the structured 1.1.0 surfaces carry real relief, so strong bumps only read as noise.
        // ADR-116: material.noise 1.2.0 bakes realistic albedo, so the tints sit near one and only nudge hue; they no
        // longer darken every surface toward grey.
        public static readonly ProductSurfaceResponse Concrete = new ProductSurfaceResponse(
            new Color(1.00f, 0.99f, 0.97f, 1f), 1.05f, 1f, 0.02f, 0.18f);

        /// <summary>Warmer plaster for door/window frames and curb trim (reuses packaged concrete planes).</summary>
        public static readonly ProductSurfaceResponse TrimPlaster = new ProductSurfaceResponse(
            new Color(1.04f, 1.00f, 0.92f, 1f), 0.95f, 0.94f, 0.01f, 0.22f);

        public static readonly ProductSurfaceResponse CoverMetal = new ProductSurfaceResponse(
            new Color(0.95f, 0.96f, 0.96f, 1f), 1.00f, 0.82f, 0.72f, 0.50f);

        public static readonly ProductSurfaceResponse Soil = new ProductSurfaceResponse(
            new Color(1.02f, 0.95f, 0.84f, 1f), 1.15f, 1f, 0f, 0.045f);

        public static readonly ProductSurfaceResponse Asphalt = new ProductSurfaceResponse(
            new Color(1.00f, 1.00f, 1.02f, 1f), 1.00f, 1f, 0.03f, 0.11f);

        /// <summary>Darker outer ring so the playable apron reads against the inner yard pad.</summary>
        public static readonly ProductSurfaceResponse Apron = new ProductSurfaceResponse(
            new Color(0.80f, 0.80f, 0.82f, 1f), 0.95f, 1f, 0.04f, 0.09f);
    }
}
