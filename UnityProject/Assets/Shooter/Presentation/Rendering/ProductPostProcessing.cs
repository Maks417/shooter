#nullable enable
using System.Collections.Generic;
using Shooter.Presentation.View;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shooter.Presentation.Rendering
{
    /// <summary>
    /// ADR-080 runtime post-processing: one global volume built from code (no profile assets) plus per-camera HDR and
    /// post flags for product cameras. Diagnostic/benchmark B01 cameras never opt in. Presentation only.
    /// </summary>
    public static class ProductPostProcessing
    {
        private static Volume? volume;
        private static VolumeProfile? profile;
        private static int appliedTier = -1;
        private static readonly List<Camera> Cameras = new List<Camera>();

        public static int AppliedTier => appliedTier;

        private static readonly int AoStrengthId = Shader.PropertyToID("_ShooterAoVolumeStrength");
        private static bool aoVolumeBound;

        /// <summary>ADR-081: set by the map view while a baked AO volume is bound; the tier sets its weight.</summary>
        public static bool AoVolumeBound
        {
            get => aoVolumeBound;
            set { aoVolumeBound = value; ApplyAoStrength(); }
        }

        private static void ApplyAoStrength()
        {
            int tier = appliedTier >= 0 ? appliedTier : Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, PostProcessingTier.TierCount - 1);
            Shader.SetGlobalFloat(AoStrengthId, aoVolumeBound ? PostProcessingTier.For(tier).AoVolumeStrength : 0f);
        }

        /// <summary>Opt a product camera into post-processing; it follows the active quality tier.</summary>
        public static void Attach(Camera camera)
        {
            if (camera == null) return;
            Cameras.RemoveAll(c => c == null);
            if (!Cameras.Contains(camera)) Cameras.Add(camera);
            Apply(QualitySettings.GetQualityLevel());
        }

        public static void Detach(Camera camera) => Cameras.Remove(camera);

        /// <summary>Rebuild the volume and camera flags for a quality tier (call after QualitySettings changes).</summary>
        public static void Apply(int qualityTier)
        {
            var tier = PostProcessingTier.For(Mathf.Clamp(qualityTier, 0, PostProcessingTier.TierCount - 1));
            EnsureVolume();
            if (tier.Tier != appliedTier)
            {
                Configure(profile!, tier);
                Debug.Log("SHOOTER_POST_PROCESSING tier=" + tier.Tier + " hdr=" + tier.Hdr + " tonemap=" + tier.NeutralTonemapping
                          + " bloom=" + tier.Bloom + " ssao=" + tier.Ssao + " pipelineHdr=" + PipelineHdr());
            }
            appliedTier = tier.Tier;
            ApplyAoStrength();
            Cameras.RemoveAll(c => c == null);
            foreach (var camera in Cameras)
            {
                camera.allowHDR = tier.Hdr;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
                data.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
            }
        }

        private static bool PipelineHdr() =>
            GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.supportsHDR;

        private static void EnsureVolume()
        {
            if (volume != null && profile != null) return;
            var host = new GameObject("Product post-processing volume");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.DontSave;
            volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Product post-processing (code-built)";
            profile.hideFlags = HideFlags.DontSave;
            volume.sharedProfile = profile;
            appliedTier = -1;
        }

        private static void Configure(VolumeProfile target, PostProcessingTier tier)
        {
            var tonemapping = Get<Tonemapping>(target);
            tonemapping.mode.Override(tier.NeutralTonemapping ? TonemappingMode.Neutral : TonemappingMode.None);

            var color = Get<ColorAdjustments>(target);
            color.postExposure.Override(tier.PostExposure);
            color.contrast.Override(tier.Contrast);
            color.saturation.Override(tier.Saturation);

            var bloom = Get<Bloom>(target);
            bloom.active = tier.Bloom;
            bloom.threshold.Override(tier.BloomThreshold);
            bloom.intensity.Override(tier.BloomIntensity);
            bloom.scatter.Override(tier.BloomScatter);
            bloom.highQualityFiltering.Override(tier.BloomHighQualityFiltering);

            var vignette = Get<Vignette>(target);
            vignette.intensity.Override(tier.VignetteIntensity);
            vignette.smoothness.Override(tier.VignetteSmoothness);
        }

        private static T Get<T>(VolumeProfile target) where T : VolumeComponent
        {
            if (!target.TryGet<T>(out var component)) component = target.Add<T>(true);
            component.active = true;
            return component;
        }
    }
}
