using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Contracts;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shooter.Tests
{
    public sealed class FoundationEditorTests
    {
        [Test] public void RunningEditorAndPackagesMatchPortableLock() { Assert.DoesNotThrow(() => ShooterBuild.CheckToolchain()); }
        [Test] public void ContractsHaveNoUnityOrTransportReferences()
        {
            Assert.That(typeof(InputCommand).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Unity", StringComparison.Ordinal) || a.Name.Contains("Transport")), Is.False);
        }
        [Test] public void ProjectPinsNativeD3D11ClientAndHeadlessBackends()
        {
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64), Is.False);
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64), Is.EqualTo(new[] { GraphicsDeviceType.Direct3D11 }));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone), Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Server), Is.EqualTo(ScriptingImplementation.Mono2x));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60).Within(.0000001));
        }
        [Test] public void ProfilesHaveExplicitBudgetSettingsAndCodeBuiltPostEffects()
        {
            Assert.That(QualitySettings.names, Is.EqualTo(new[] { "Low", "Balanced", "High" }));
            Assert.That(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline), Is.EqualTo("Assets/Shooter/Settings/Low.asset"));
            float[] distances = { 40, 60, 100 }; int[] lights = { 2, 4, 4 }; int[] atlases = { 1024, 2048, 2048 };
            bool[] hdr = { false, true, true }; // ADR-080: Low stays LDR for the reference gate.
            for (int i = 0; i < 3; i++)
            {
                var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
                Assert.That(quality.FindProperty("m_QualitySettings").GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms").arraySize, Is.Zero);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Shooter/Settings/" + QualitySettings.names[i] + ".asset");
                Assert.That(asset, Is.Not.Null); Assert.That(asset.renderScale, Is.EqualTo(1)); Assert.That(asset.shadowDistance, Is.EqualTo(distances[i]));
                Assert.That(asset.mainLightShadowmapResolution, Is.EqualTo(atlases[i])); Assert.That(asset.maxAdditionalLightsCount, Is.EqualTo(lights[i]));
                Assert.That(asset.supportsAdditionalLightShadows, Is.False); Assert.That(asset.supportsHDR, Is.EqualTo(hdr[i])); Assert.That(asset.shadowCascadeCount, Is.EqualTo(2));
                int previousTier = QualitySettings.GetQualityLevel();
                try { QualitySettings.SetQualityLevel(i, false); Assert.That(QualitySettings.renderPipeline, Is.EqualTo(asset)); }
                finally { QualitySettings.SetQualityLevel(previousTier, false); }
                var serialized = new SerializedObject(asset);
                Assert.That(AssetDatabase.GetAssetPath(serialized.FindProperty("m_Textures.blueNoise64LTex").objectReferenceValue), Is.EqualTo("Assets/Shooter/Settings/OrderedDither64.asset"));
                Assert.That(AssetDatabase.GetAssetPath(serialized.FindProperty("m_Textures.bayerMatrixTex").objectReferenceValue), Is.EqualTo("Assets/Shooter/Settings/OrderedDither8.asset"));
                var renderers = serialized.FindProperty("m_RendererDataList");
                Assert.That(renderers.arraySize, Is.EqualTo(1));
                var renderer = (UniversalRendererData)renderers.GetArrayElementAtIndex(0).objectReferenceValue;
                Assert.That(AssetDatabase.GetAssetPath(renderer), Is.EqualTo(ShooterPostProcessingBuild.RendererPaths[i]));
                Assert.That(AssetDatabase.GetAssetPath(renderer.postProcessData), Is.EqualTo(ShooterPostProcessingBuild.PostProcessDataPath));
                var ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().ToArray();
                Assert.That(ssao.Length == 1 && ssao[0].isActive, Is.EqualTo(i > 0), "SSAO only on Balanced and High");
                if (ssao.Length == 1) Assert.That(new SerializedObject(ssao[0]).FindProperty("m_Settings.AOMethod").enumValueIndex, Is.EqualTo(1), "InterleavedGradient needs no stock noise images");
            }
        }

        [Test] public void PostProcessDataReferencesOnlyGeneratedTextures()
        {
            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(ShooterPostProcessingBuild.PostProcessDataPath);
            Assert.That(data, Is.Not.Null);
            var textures = data.textures.blueNoise16LTex.Concat(data.textures.filmGrainTex)
                .Concat(new[] { data.textures.smaaAreaTex, data.textures.smaaSearchTex }).ToArray();
            Assert.That(textures, Has.None.Null);
            foreach (var texture in textures)
                Assert.That(AssetDatabase.GetAssetPath(texture), Does.StartWith("Assets/Shooter/Settings/").And.EndWith(".asset"));
            Assert.That(data.shaders.uberPostPS, Is.Not.Null);
        }
        [Test] public void ShaderLookupsAreAlgorithmicAndDebugUiResourcesAreExcluded()
        {
            var textures = GraphicsSettings.GetRenderPipelineSettings<UniversalRenderPipelineRuntimeTextures>();
            Assert.That(AssetDatabase.GetAssetPath(textures.bayerMatrixTex), Is.EqualTo("Assets/Shooter/Settings/OrderedDither8.asset"));
            Assert.That(AssetDatabase.GetAssetPath(textures.blueNoise64LTex), Is.EqualTo("Assets/Shooter/Settings/OrderedDither64.asset"));
            Assert.That(AssetDatabase.GetAssetPath(textures.debugFontTexture), Is.EqualTo("Assets/Shooter/Settings/UnusedDebugLookup.asset"));
            var core = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Single(p => p.name == "com.unity.render-pipelines.core");
            Assert.That(File.Exists(Path.Combine(core.resolvedPath, "Runtime/Debugging/Prefabs/Resources/DebugUICanvas.prefab")), Is.False);
        }
        [Test] public void GameplayLayersAndBootstrapAreCodeOwned()
        {
            Assert.That(LayerMask.NameToLayer("WorldSolid"), Is.EqualTo(8)); Assert.That(LayerMask.NameToLayer("ActorHit"), Is.EqualTo(10));
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
            Assert.That(EditorBuildSettings.scenes.Length, Is.EqualTo(1)); Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo("Assets/Shooter/Bootstrap/Scenes/Bootstrap.unity"));
            string[] prohibited = { ".png", ".jpg", ".fbx", ".obj", ".anim", ".wav", ".mp3", ".blend", ".prefab" };
            Assert.That(Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories).Any(p => prohibited.Contains(Path.GetExtension(p).ToLowerInvariant())), Is.False);
        }
    }
}
