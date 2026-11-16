#nullable enable
using System;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// ADR-080 per-quality-tier post-processing numbers (presentation only; never read by Simulation). Low stays
    /// LDR with colour adjustments and vignette only, so the 720p Low reference gate keeps its budget. Balanced and High
    /// render HDR with neutral tonemapping and bloom; their SSAO lives in the per-tier renderer assets.
    /// </summary>
    public readonly struct PostProcessingTier
    {
        public int Tier { get; }
        public bool Hdr { get; }
        public bool NeutralTonemapping { get; }
        public float PostExposure { get; }
        public float Contrast { get; }
        public float Saturation { get; }
        public bool Bloom { get; }
        public float BloomThreshold { get; }
        public float BloomIntensity { get; }
        public float BloomScatter { get; }
        public bool BloomHighQualityFiltering { get; }
        public float VignetteIntensity { get; }
        public float VignetteSmoothness { get; }
        public bool Ssao { get; }
        /// <summary>ADR-081 baked ambient-occlusion volume weight on indirect light (SSAO adds contact detail on top).</summary>
        public float AoVolumeStrength { get; }

        private PostProcessingTier(int tier, bool hdr, bool neutralTonemapping, float postExposure, float contrast, float saturation,
            bool bloom, float bloomThreshold, float bloomIntensity, float bloomScatter, bool bloomHighQualityFiltering,
            float vignetteIntensity, float vignetteSmoothness, bool ssao, float aoVolumeStrength)
        {
            Tier = tier; Hdr = hdr; NeutralTonemapping = neutralTonemapping; PostExposure = postExposure;
            Contrast = contrast; Saturation = saturation; Bloom = bloom; BloomThreshold = bloomThreshold;
            BloomIntensity = bloomIntensity; BloomScatter = bloomScatter; BloomHighQualityFiltering = bloomHighQualityFiltering;
            VignetteIntensity = vignetteIntensity; VignetteSmoothness = vignetteSmoothness; Ssao = ssao;
            AoVolumeStrength = aoVolumeStrength;
        }

        public const int TierCount = 3;

        public static PostProcessingTier For(int qualityTier)
        {
            if (qualityTier < 0 || qualityTier >= TierCount) throw new ArgumentOutOfRangeException(nameof(qualityTier));
            return qualityTier switch
            {
                0 => new PostProcessingTier(0, hdr: false, neutralTonemapping: false, postExposure: 0f, contrast: 6f, saturation: -4f,
                    bloom: false, bloomThreshold: 0f, bloomIntensity: 0f, bloomScatter: 0f, bloomHighQualityFiltering: false,
                    vignetteIntensity: 0.16f, vignetteSmoothness: 0.4f, ssao: false, aoVolumeStrength: 1.0f),
                1 => new PostProcessingTier(1, hdr: true, neutralTonemapping: true, postExposure: 0.25f, contrast: 10f, saturation: -6f,
                    bloom: true, bloomThreshold: 1.0f, bloomIntensity: 0.25f, bloomScatter: 0.55f, bloomHighQualityFiltering: false,
                    vignetteIntensity: 0.2f, vignetteSmoothness: 0.4f, ssao: true, aoVolumeStrength: 0.8f),
                _ => new PostProcessingTier(2, hdr: true, neutralTonemapping: true, postExposure: 0.25f, contrast: 10f, saturation: -6f,
                    bloom: true, bloomThreshold: 1.0f, bloomIntensity: 0.35f, bloomScatter: 0.65f, bloomHighQualityFiltering: true,
                    vignetteIntensity: 0.2f, vignetteSmoothness: 0.4f, ssao: true, aoVolumeStrength: 0.8f)
            };
        }
    }
}
