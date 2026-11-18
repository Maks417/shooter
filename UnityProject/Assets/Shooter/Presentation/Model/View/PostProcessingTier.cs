#nullable enable
using System;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// ADR-080 per-quality-tier post-processing numbers (presentation only; never read by Simulation). Low stays
    /// LDR with colour adjustments and vignette only, so the 720p Low reference gate keeps its budget. Balanced and High
    /// render HDR with neutral tonemapping and bloom; their SSAO lives in the per-tier renderer assets.
    /// ADR-116: every tier now adds +10 saturation (was -4 Low, -6 Balanced/High), which more than cancels the map grade's
    /// desaturation, so maps keep their surface colour.
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
        /// <summary>ADR-103 camera MSAA sample count (1 = off). Must match the per-tier URP asset.</summary>
        public int MsaaSamples { get; }
        /// <summary>ADR-103 FXAA post pass (texture-free; Low only, since higher tiers resolve edges with MSAA).</summary>
        public bool Fxaa { get; }
        /// <summary>ADR-103 anisotropic level applied to generated surface/decal textures.</summary>
        public int AnisoLevel { get; }
        /// <summary>ADR-103 multiplier on each map's fog start/end so distant (scoped) views keep contrast.</summary>
        public float FogDistanceScale { get; }
        /// <summary>ADR-103 optical scope lens render-target edge in pixels (before resolution scaling).</summary>
        public int ScopeLensTextureSize { get; }

        private PostProcessingTier(int tier, bool hdr, bool neutralTonemapping, float postExposure, float contrast, float saturation,
            bool bloom, float bloomThreshold, float bloomIntensity, float bloomScatter, bool bloomHighQualityFiltering,
            float vignetteIntensity, float vignetteSmoothness, bool ssao, float aoVolumeStrength,
            int msaaSamples, bool fxaa, int anisoLevel, float fogDistanceScale, int scopeLensTextureSize)
        {
            MsaaSamples = msaaSamples; Fxaa = fxaa; AnisoLevel = anisoLevel; FogDistanceScale = fogDistanceScale;
            ScopeLensTextureSize = scopeLensTextureSize;
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
                0 => new PostProcessingTier(0, hdr: false, neutralTonemapping: false, postExposure: 0f, contrast: 6f, saturation: 10f,
                    bloom: false, bloomThreshold: 0f, bloomIntensity: 0f, bloomScatter: 0f, bloomHighQualityFiltering: false,
                    vignetteIntensity: 0.16f, vignetteSmoothness: 0.4f, ssao: false, aoVolumeStrength: 1.0f,
                    msaaSamples: 1, fxaa: true, anisoLevel: 2, fogDistanceScale: 1.0f, scopeLensTextureSize: 512),
                1 => new PostProcessingTier(1, hdr: true, neutralTonemapping: true, postExposure: 0.25f, contrast: 10f, saturation: 10f,
                    bloom: true, bloomThreshold: 1.0f, bloomIntensity: 0.25f, bloomScatter: 0.55f, bloomHighQualityFiltering: false,
                    vignetteIntensity: 0.2f, vignetteSmoothness: 0.4f, ssao: true, aoVolumeStrength: 0.8f,
                    msaaSamples: 2, fxaa: false, anisoLevel: 8, fogDistanceScale: 1.8f, scopeLensTextureSize: 768),
                _ => new PostProcessingTier(2, hdr: true, neutralTonemapping: true, postExposure: 0.25f, contrast: 10f, saturation: 10f,
                    bloom: true, bloomThreshold: 1.0f, bloomIntensity: 0.35f, bloomScatter: 0.65f, bloomHighQualityFiltering: true,
                    vignetteIntensity: 0.2f, vignetteSmoothness: 0.4f, ssao: true, aoVolumeStrength: 0.8f,
                    msaaSamples: 4, fxaa: false, anisoLevel: 16, fogDistanceScale: 2.6f, scopeLensTextureSize: 1024)
            };
        }
    }
}
