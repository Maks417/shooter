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

        // Cool plaster/concrete walls vs warm yard soil and darker asphalt lanes (Bodycam/BFV place craft).
        public static readonly ProductSurfaceResponse Concrete = new ProductSurfaceResponse(
            new Color(0.90f, 0.91f, 0.93f, 1f), 1.38f, 1f, 0.02f, 0.18f);

        public static readonly ProductSurfaceResponse CoverMetal = new ProductSurfaceResponse(
            new Color(0.70f, 0.74f, 0.78f, 1f), 1.18f, 0.82f, 0.72f, 0.50f);

        /// <summary>Painted/edge trim: darker than wall concrete, less metal than cover crates.</summary>
        public static readonly ProductSurfaceResponse Trim = new ProductSurfaceResponse(
            new Color(0.62f, 0.64f, 0.66f, 1f), 1.28f, 0.92f, 0.18f, 0.28f);

        public static readonly ProductSurfaceResponse Soil = new ProductSurfaceResponse(
            new Color(0.78f, 0.72f, 0.58f, 1f), 1.48f, 1f, 0f, 0.045f);

        public static readonly ProductSurfaceResponse Asphalt = new ProductSurfaceResponse(
            new Color(0.52f, 0.53f, 0.55f, 1f), 1.22f, 1f, 0.03f, 0.11f);

        /// <summary>Darker outer ring so the playable apron reads against the inner yard pad.</summary>
        public static readonly ProductSurfaceResponse Apron = new ProductSurfaceResponse(
            new Color(0.38f, 0.39f, 0.40f, 1f), 1.15f, 1f, 0.04f, 0.09f);
    }
}
