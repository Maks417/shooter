using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ADR-080: code-built URP post-processing data and per-tier renderers. Every texture slot is filled with generated
/// lookup data (no stock blue-noise, film-grain or SMAA images), so the player art audit stays clean. Low keeps the
/// original renderer without SSAO; Balanced and High get their own renderer assets with a configured SSAO feature.
/// </summary>
public static class ShooterPostProcessingBuild
{
    private const string Settings = "Assets/Shooter/Settings";
    public const string PostProcessDataPath = Settings + "/ProductPostProcessData.asset";
    public const string MapSurfaceShaderPath = "Assets/Shooter/Shaders/MapSurface.shader";
    public const string MapSurfaceVariantPath = Settings + "/ProductMapSurfaceNormalMask.mat";
    public static readonly string[] RendererPaths =
    {
        Settings + "/FoundationRenderer.asset",
        Settings + "/FoundationRendererBalanced.asset",
        Settings + "/FoundationRendererHigh.asset"
    };
    private static readonly string[] PipelinePaths = { Settings + "/Low.asset", Settings + "/Balanced.asset", Settings + "/High.asset" };

    // AO method 1 = InterleavedGradient (no noise texture); samples 2 = Low (4), 1 = Medium (8); blur 1 = Gaussian, 0 = bilateral.
    private struct SsaoTier { public bool Enabled, Downsample; public int Samples, Blur; public float Intensity, Radius, DirectLighting; }
    private static readonly SsaoTier[] Ssao =
    {
        new SsaoTier { Enabled = false },
        new SsaoTier { Enabled = true, Downsample = true, Samples = 2, Blur = 1, Intensity = 1.2f, Radius = 0.3f, DirectLighting = 0.25f },
        new SsaoTier { Enabled = true, Downsample = false, Samples = 1, Blur = 0, Intensity = 1.4f, Radius = 0.4f, DirectLighting = 0.25f }
    };
    private static readonly bool[] Hdr = { false, true, true };

    /// <summary>Batch entry: configure post-processing assets, then refresh settings provenance.</summary>
    public static void Configure()
    {
        try
        {
            ShooterBuild.CheckToolchain();
            ConfigureAssets();
            AssetDatabase.SaveAssets();
            ShooterBuild.WriteSettingsProvenance();
            Debug.Log("SHOOTER_POST_PROCESSING_CONFIGURED");
        }
        catch (Exception error)
        {
            Debug.LogError("SHOOTER_FAILURE: " + error);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Idempotent: existing assets keep their GUIDs and are updated in place.</summary>
    public static void ConfigureAssets()
    {
        var dither16 = ShooterBuild.BakeOrderedDither(Settings + "/PostDither16.asset", 16);
        var zero = ShooterBuild.BakeOrderedDither(Settings + "/UnusedPostLookup.asset", 1);

        var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<PostProcessData>();
            AssetDatabase.CreateAsset(data, PostProcessDataPath);
        }
        data.shaders = GraphicsSettings.GetRenderPipelineSettings<PostProcessData.ShaderResources>()
            ?? throw new InvalidOperationException("Locked URP post-process shader resources missing");
        // Generated lookups only. Film grain and SMAA are never enabled; their slots hold a one-pixel zero lookup so the
        // editor never repopulates them with stock images.
        data.textures = new PostProcessData.TextureResources
        {
            blueNoise16LTex = new[] { dither16 },
            filmGrainTex = Enumerable.Repeat(zero, 10).ToArray(),
            smaaAreaTex = zero,
            smaaSearchTex = zero
        };
        EditorUtility.SetDirty(data);

        var low = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPaths[0])
            ?? throw new InvalidOperationException("Missing foundation renderer; run ShooterBuild.InitializeFoundation");
        low.postProcessData = data;
        EditorUtility.SetDirty(low);
        var renderers = new UniversalRendererData[3];
        renderers[0] = low;
        for (int tier = 1; tier < 3; tier++)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPaths[tier]);
            if (renderer == null)
            {
                renderer = UnityEngine.Object.Instantiate(low);
                renderer.rendererFeatures.Clear();
                AssetDatabase.CreateAsset(renderer, RendererPaths[tier]);
            }
            renderer.postProcessData = data;
            ConfigureSsao(renderer, Ssao[tier]);
            renderers[tier] = renderer;
        }

        for (int tier = 0; tier < 3; tier++)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePaths[tier])
                ?? throw new InvalidOperationException("Missing pipeline " + PipelinePaths[tier]);
            pipeline.supportsHDR = Hdr[tier];
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderers[tier];
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        // ADR-081: keep the one Shooter/MapSurface keyword set map materials use (normal + mask) in players.
        var mapShader = AssetDatabase.LoadAssetAtPath<Shader>(MapSurfaceShaderPath)
            ?? throw new InvalidOperationException("Missing " + MapSurfaceShaderPath);
        var variant = AssetDatabase.LoadAssetAtPath<Material>(MapSurfaceVariantPath);
        if (variant == null) { variant = new Material(mapShader); AssetDatabase.CreateAsset(variant, MapSurfaceVariantPath); }
        variant.shader = mapShader;
        variant.shaderKeywords = new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" };
        EditorUtility.SetDirty(variant);

        // SSAO uses InterleavedGradient, so URP's ScreenSpaceAmbientOcclusionDynamicResourcesStripper removes its stock
        // blue-noise images from players; the build's imported-art audit fails if that ever stops being true.
    }

    private static void ConfigureSsao(UniversalRendererData renderer, SsaoTier tier)
    {
        var ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
        if (ssao == null)
        {
            ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "Product SSAO";
            AssetDatabase.AddObjectToAsset(ssao, renderer);
            renderer.rendererFeatures.Add(ssao);
            AssetDatabase.SaveAssets();
            var map = new SerializedObject(renderer);
            var ids = map.FindProperty("m_RendererFeatureMap");
            ids.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out _, out long localId))
                    throw new InvalidOperationException("Renderer feature has no local file id");
                ids.GetArrayElementAtIndex(i).longValue = localId;
            }
            map.ApplyModifiedPropertiesWithoutUndo();
        }
        ssao.SetActive(tier.Enabled);
        var settings = new SerializedObject(ssao);
        settings.FindProperty("m_Settings.AOMethod").enumValueIndex = 1;
        settings.FindProperty("m_Settings.Downsample").boolValue = tier.Downsample;
        settings.FindProperty("m_Settings.AfterOpaque").boolValue = false;
        settings.FindProperty("m_Settings.Source").enumValueIndex = 1;
        settings.FindProperty("m_Settings.Intensity").floatValue = tier.Intensity;
        settings.FindProperty("m_Settings.Radius").floatValue = tier.Radius;
        settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = tier.DirectLighting;
        settings.FindProperty("m_Settings.Samples").enumValueIndex = tier.Samples;
        settings.FindProperty("m_Settings.BlurQuality").enumValueIndex = tier.Blur;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ssao);
        EditorUtility.SetDirty(renderer);
    }
}
