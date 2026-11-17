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

        private static Shooter.Generation.Environment.MapGrade mapGrade=new Shooter.Generation.Environment.MapGrade();
        private static Texture2D? mapLookup;
        public static void SetMapGrade(string? mapId)
        {
            mapGrade=mapId!=null?Shooter.Generation.Environment.MapRegistry.Require(mapId).Grade:new Shooter.Generation.Environment.MapGrade();
            if(mapLookup!=null) { if(Application.isPlaying)Object.Destroy(mapLookup);else Object.DestroyImmediate(mapLookup); }
            var data=Shooter.Generation.Environment.AtmosphereBaker.Grade(mapGrade);
            mapLookup=new Texture2D(data.Width,data.Height,TextureFormat.RGBA32,false,true){name="Numeric map grade",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var bytes=new byte[data.MipBytes[0].Count];for(int i=0;i<bytes.Length;i++)bytes[i]=data.MipBytes[0][i];mapLookup.LoadRawTextureData(bytes);mapLookup.Apply(false,true);
            if(profile!=null)Configure(profile,PostProcessingTier.For(Mathf.Max(0,appliedTier)));
        }
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

        public static PostProcessingTier CurrentTier =>
            PostProcessingTier.For(appliedTier >= 0 ? appliedTier : Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, PostProcessingTier.TierCount - 1));

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
            GeneratedSky.ApplyFogScale(tier.FogDistanceScale);
            Cameras.RemoveAll(c => c == null);
            foreach (var camera in Cameras)
            {
                camera.allowHDR = tier.Hdr;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                // ADR-103: MSAA follows the tier (the old one-shot allowMSAA at camera creation missed the saved tier);
                // Low gets texture-free FXAA instead. SMAA stays off (ADR-080 lookup-texture audit).
                camera.allowMSAA = tier.MsaaSamples > 1;
                data.antialiasing = tier.Fxaa ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
                data.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
            }
        }

        private static Volume? damageVolume;
        private static Vignette? damageVignette;
        private static ColorAdjustments? damageColor;
        private static readonly Color DamageRed = new Color(0.55f, 0.02f, 0.02f, 1f);

        /// <summary>
        /// ADR-103 low-health/hit grade: a second code-built global volume (priority 20) that blends a red vignette,
        /// desaturation and a slight exposure dip over the tier grade. Weight 0 removes it entirely (no cost).
        /// </summary>
        public static void SetDamageGrade(Shooter.Presentation.Hud.CombatHudVisual.LowHealthGrade grade)
        {
            if (grade.Weight <= 0f)
            {
                if (damageVolume != null) { damageVolume.weight = 0f; damageVolume.enabled = false; }
                return;
            }
            if (damageVolume == null)
            {
                var host = new GameObject("Product damage volume");
                Object.DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.DontSave;
                damageVolume = host.AddComponent<Volume>();
                damageVolume.isGlobal = true;
                damageVolume.priority = 20f;
                var damageProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                damageProfile.name = "Product damage grade (code-built)";
                damageProfile.hideFlags = HideFlags.DontSave;
                damageVolume.sharedProfile = damageProfile;
                damageVignette = Get<Vignette>(damageProfile);
                damageColor = Get<ColorAdjustments>(damageProfile);
            }
            damageVolume.enabled = true;
            damageVolume.weight = Mathf.Clamp01(grade.Weight);
            damageVignette!.color.Override(DamageRed);
            damageVignette.intensity.Override(Mathf.Max(PostProcessingTier.For(Mathf.Max(0, appliedTier)).VignetteIntensity, Mathf.Clamp01(grade.Vignette)));
            damageVignette.smoothness.Override(0.55f);
            damageVignette.rounded.Override(false);
            damageColor!.saturation.Override(BaseSaturation() + grade.Saturation);
            damageColor.contrast.Override(BaseContrast() + grade.Contrast);
            damageColor.postExposure.Override(PostProcessingTier.For(Mathf.Max(0, appliedTier)).PostExposure + grade.Exposure);
        }

        // The damage volume overrides the same parameters as the tier volume, so it carries the tier baseline forward.
        private static float BaseSaturation()
        {
            var tier = PostProcessingTier.For(Mathf.Max(0, appliedTier));
            return tier.Saturation + (tier.Tier == 0 ? (mapGrade.Saturation - 1000) / 10f : 0f);
        }

        private static float BaseContrast()
        {
            var tier = PostProcessingTier.For(Mathf.Max(0, appliedTier));
            return tier.Contrast + (tier.Tier == 0 ? (mapGrade.Contrast - 1000) / 10f : 0f);
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
            var lookup=Get<ColorLookup>(target);lookup.active=tier.Tier>0 && mapLookup!=null;
            lookup.texture.Override(mapLookup);lookup.contribution.Override(lookup.active?1f:0f);
            color.colorFilter.Override(tier.Tier==0?new Color(mapGrade.Red/1000f,mapGrade.Green/1000f,mapGrade.Blue/1000f):Color.white);
            if(tier.Tier==0){color.contrast.Override(tier.Contrast+(mapGrade.Contrast-1000)/10f);color.saturation.Override(tier.Saturation+(mapGrade.Saturation-1000)/10f);}

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
