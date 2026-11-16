using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using Shooter.Bootstrap;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

// Batch entry points intentionally use a fixed type/name rather than arbitrary type deserialization.
public static class ShooterBuild
{
    public static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
    public static string LockPath => Path.Combine(RepositoryRoot, "toolchain-lock.json");
    /// <summary>ADR-079 build-only packages and settings; not part of toolchainHash.</summary>
    public static string BuildLockPath => Path.Combine(RepositoryRoot, "build-lock.json");
    public static string HashFile(string path)
    {
        using var hash = SHA256.Create();
        using var stream = File.OpenRead(path);
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    public static string Argument(string name, string fallback = "")
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++) if (args[i] == name)
        {
            if (i + 1 == args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal)) throw new ArgumentException("Missing value for " + name);
            return args[i + 1];
        }
        return fallback;
    }
    /// <summary>Merged view of toolchain-lock.json (engine/content) and build-lock.json (build-only), ADR-079.</summary>
    [Serializable]
    public sealed class Toolchain
    {
        public int schemaVersion;
        public string editorVersion, editorRevision, urpVersion, aiNavigationVersion, graphicsApi, dotnetSdk;
        public string clientBackend, serverBackend, clientTarget, serverTarget;
        public string[] requiredModules;
        public int provenanceAllowlistVersion;
        public string transportVersion;
        public LockedPackage[] packages;
    }
    [Serializable] public sealed class LockedPackage { public string name, version; }
    [Serializable]
    public sealed class BuildLock
    {
        public int schemaVersion;
        public string lockSchema, clientBackend, serverBackend, clientTarget, serverTarget, transportVersion;
        public string[] requiredModules;
        public int provenanceAllowlistVersion;
        public LockedPackage[] packages;
    }
    [Serializable]
    public sealed class ToolchainProof
    {
        public string result = "Verified";
        public string editorVersion, editorRevision, toolchainHash;
    }
    public static Toolchain CheckToolchain()
    {
        var expected = JsonUtility.FromJson<Toolchain>(File.ReadAllText(LockPath));
        var build = JsonUtility.FromJson<BuildLock>(File.ReadAllText(BuildLockPath));
        if (build.schemaVersion != 1 || build.lockSchema != "shooter.build-lock.v1" || build.packages == null)
            throw new InvalidOperationException("Unsupported build-lock.json schema");
        expected.clientBackend = build.clientBackend; expected.serverBackend = build.serverBackend;
        expected.clientTarget = build.clientTarget; expected.serverTarget = build.serverTarget;
        expected.requiredModules = build.requiredModules; expected.provenanceAllowlistVersion = build.provenanceAllowlistVersion;
        expected.transportVersion = build.transportVersion; expected.packages = build.packages;
        string actual = FileVersionInfo.GetVersionInfo(EditorApplication.applicationPath).ProductVersion;
        if (expected.schemaVersion != 2 || Application.unityVersion != expected.editorVersion || actual != expected.editorVersion + "_" + expected.editorRevision)
            throw new InvalidOperationException("Editor does not match toolchain lock: " + actual);
        var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        void RequirePackage(string name, string version)
        {
            if (packages.SingleOrDefault(p => p.name == name)?.version != version) throw new InvalidOperationException("Package does not match toolchain lock: " + name);
        }
        RequirePackage("com.unity.render-pipelines.universal", expected.urpVersion);
        RequirePackage("com.unity.render-pipelines.core", expected.urpVersion);
        RequirePackage("com.unity.ai.navigation", expected.aiNavigationVersion);
        foreach (var locked in build.packages) RequirePackage(locked.name, locked.version);
        var core = packages.Single(p => p.name == "com.unity.render-pipelines.core");
        if (core.source.ToString() != "LocalTarball" || !core.packageId.Replace('\\', '/').EndsWith("/com.unity.render-pipelines.core-" + expected.urpVersion + "-shooter.tgz", StringComparison.Ordinal))
            throw new InvalidOperationException("Core RP must resolve the pinned immutable archive; no mutable folder or stock debug-UI fallback is permitted");
        return expected;
    }
    public static void WriteJson(string path, object report)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("A report path is required");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, JsonUtility.ToJson(report, true) + "\n");
    }
    private static void Guard(Action action)
    {
        try { action(); }
        catch (Exception error) { Debug.LogError("SHOOTER_FAILURE: " + error); EditorApplication.Exit(1); }
    }
    public static void VerifyToolchain() => Guard(() =>
    {
        var expected = CheckToolchain();
        WriteJson(Argument("-shooterReport"), new ToolchainProof { editorVersion = expected.editorVersion, editorRevision = expected.editorRevision, toolchainHash = HashFile(LockPath) });
        Debug.Log("SHOOTER_TOOLCHAIN_VERIFIED");
    });
    // Runtime-created packaged surfaces need these local shader_feature combinations in players.
    // Numeric materials retain the variants without adding source textures or disabling stripping.
    private static readonly string[] ProductSurfaceVariantPaths =
    {
        "Assets/Shooter/Settings/ProductSurfaceNormal.mat",
        "Assets/Shooter/Settings/ProductSurfaceMask.mat",
        "Assets/Shooter/Settings/ProductSurfaceNormalMask.mat"
    };
    public static void ConfigureProductSurfaceVariants() => Guard(() =>
    {
        CheckToolchain();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Locked URP Lit shader missing");
        for (int i = 0; i < ProductSurfaceVariantPaths.Length; i++)
        {
            string path = ProductSurfaceVariantPaths[i];
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shaderKeywords = i == 0 ? new[] { "_NORMALMAP" } : i == 1
                ? new[] { "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" }
                : new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" };
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        WriteSettingsProvenance();
        Debug.Log("SHOOTER_PRODUCT_SURFACE_VARIANTS_CONFIGURED");
    });
    public static void InitializeFoundation() => Guard(() =>
    {
        CheckToolchain();
        EditorSettings.serializationMode = SerializationMode.ForceText;
        VersionControlSettings.mode = "Visible Meta Files";
        PlayerSettings.companyName = "Procedural Shooter";
        PlayerSettings.productName = "Shooter";
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.enableFrameTimingStats = true;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        var timeSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
        timeSettings.FindProperty("Fixed Timestep").floatValue = 1f / 60f;
        timeSettings.ApplyModifiedPropertiesWithoutUndo();
        PlayerSettings.colorSpace = ColorSpace.Linear;
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 0;
        const string settings = "Assets/Shooter/Settings";
        Directory.CreateDirectory(settings);
        AssetDatabase.Refresh();
        string rendererPath = settings + "/FoundationRenderer.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.renderingMode = RenderingMode.Forward;
            renderer.postProcessData = null;
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }
        if (File.Exists(settings + "/FoundationPipeline.asset") && !File.Exists(settings + "/Low.asset"))
            AssetDatabase.MoveAsset(settings + "/FoundationPipeline.asset", settings + "/Low.asset");
        string[] names = { "Low", "Balanced", "High" };
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = quality.FindProperty("m_QualitySettings"); levels.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = names[i];
            levels.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms").arraySize = 0;
        }
        quality.FindProperty("m_CurrentQuality").intValue = 0; quality.ApplyModifiedPropertiesWithoutUndo();
        var pipelines = new UniversalRenderPipelineAsset[3];
        for (int i = 0; i < 3; i++)
        {
            string path = settings + "/" + names[i] + ".asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline, path); }
            pipeline.supportsHDR = false; pipeline.msaaSampleCount = i == 2 ? 2 : 1; pipeline.renderScale = 1;
            pipeline.shadowDistance = i == 0 ? 40 : i == 1 ? 60 : 100; pipeline.shadowCascadeCount = 2;
            pipeline.mainLightShadowmapResolution = i == 0 ? 1024 : 2048;
            pipeline.maxAdditionalLightsCount = i == 0 ? 2 : 4;
            var serializedPipeline = new SerializedObject(pipeline);
            serializedPipeline.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serializedPipeline.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsCameraDepthTexture = false; pipeline.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(pipeline); pipelines[i] = pipeline;
            QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0; QualitySettings.antiAliasing = 0; QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.asyncUploadTimeSlice = 2;
        }
        GraphicsSettings.defaultRenderPipeline = pipelines[0];
        var runtimeTextures = GraphicsSettings.GetRenderPipelineSettings<UniversalRenderPipelineRuntimeTextures>();
        if (runtimeTextures == null) throw new InvalidOperationException("Locked URP runtime texture settings missing");
        runtimeTextures.bayerMatrixTex = BakeOrderedDither(settings + "/OrderedDither8.asset", 8);
        // M0 has no LOD crossfade/post effects; keep all engine lookup slots procedural too.
        runtimeTextures.blueNoise64LTex = BakeOrderedDither(settings + "/OrderedDither64.asset", 64);
        runtimeTextures.debugFontTexture = BakeOrderedDither(settings + "/UnusedDebugLookup.asset", 1); // Persistent zero data; never a font/sprite atlas.
        foreach (var pipeline in pipelines)
        {
            // URP 17.3 retains serialized migration slots; they also participate in build dependencies.
            var legacy = new SerializedObject(pipeline);
            legacy.FindProperty("m_Textures.blueNoise64LTex").objectReferenceValue = runtimeTextures.blueNoise64LTex;
            legacy.FindProperty("m_Textures.bayerMatrixTex").objectReferenceValue = runtimeTextures.bayerMatrixTex;
            legacy.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline);
        }
        string materialPath = settings + "/HarnessMaterial.mat";
        var harnessMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (harnessMaterial == null)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("Locked URP Lit shader missing");
            harnessMaterial = new Material(lit); AssetDatabase.CreateAsset(harnessMaterial, materialPath);
        }
        harnessMaterial.SetColor("_BaseColor", new Color(.42f, .46f, .48f, 1)); harnessMaterial.SetFloat("_Smoothness", .12f); harnessMaterial.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(harnessMaterial);
        QualitySettings.SetQualityLevel(0, true);
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        string[] layerNames = { "WorldSolid", "ActorMotor", "ActorHit", "Cosmetic", "ObjectiveTrigger", "SensorTrigger" };
        for (int i = 0; i < layerNames.Length; i++) tags.FindProperty("layers").GetArrayElementAtIndex(8 + i).stringValue = layerNames[i];
        tags.ApplyModifiedPropertiesWithoutUndo();
        const string scenePath = "Assets/Shooter/Bootstrap/Scenes/Bootstrap.unity";
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
        var scene = File.Exists(scenePath) ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var entry = UnityEngine.Object.FindFirstObjectByType<FoundationEntry>();
        if (entry == null) { var root = GameObject.Find("Bootstrap") ?? new GameObject("Bootstrap"); root.AddComponent<FoundationEntry>(); }
        EditorSceneManager.SaveScene(scene, scenePath);
        foreach (string asset in new[] { "DefaultVolumeProfile.asset", "UniversalRenderPipelineGlobalSettings.asset" })
            if (File.Exists("Assets/" + asset) && !File.Exists(settings + "/" + asset)) AssetDatabase.MoveAsset("Assets/" + asset, settings + "/" + asset);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets();
        Time.fixedDeltaTime = 1f / 60;
        string timePath = "ProjectSettings/TimeManager.asset";
        string timeYaml = File.ReadAllText(timePath);
        timeYaml = System.Text.RegularExpressions.Regex.Replace(timeYaml, "(?m)^  Fixed Timestep: .*$", "  Fixed Timestep: 0.016666668");
        File.WriteAllText(timePath, timeYaml);
        // ADR-080: post-processing data, per-tier renderers and HDR flags are code-built on top of the base pipelines.
        ShooterPostProcessingBuild.ConfigureAssets();
        AssetDatabase.SaveAssets();
        WriteSettingsProvenance();
        VerifyToolchain();
        Debug.Log("SHOOTER_FOUNDATION_INITIALIZED");
    });

    internal static Texture2D BakeOrderedDither(string path, int size)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) { texture = new Texture2D(size, size, TextureFormat.R8, false, true) { name = "Analytic ordered dither " + size }; AssetDatabase.CreateAsset(texture, path); }
        var bytes = new byte[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            int rank = 0;
            for (int bit = 0; (1 << bit) < size; bit++) { int a = (x >> bit) & 1, b = (y >> bit) & 1; rank = (rank << 2) | ((a ^ b) << 1) | b; }
            bytes[y * size + x] = size == 1 ? (byte)0 : (byte)((rank * 256 + 128) / (size * size));
        }
        texture.LoadRawTextureData(bytes); texture.wrapMode = TextureWrapMode.Repeat; texture.filterMode = FilterMode.Point; texture.Apply(false, false);
        EditorUtility.SetDirty(texture); return texture;
    }

    [Serializable] public sealed class FileProof { public string path, sha256; }
    [Serializable] public sealed class SettingsProvenance
    {
        public int schemaVersion = 1;
        public string generatorId = "shooter.foundation.settings", generatorVersion = "1.0.0", toolchainHash;
        public FileProof[] sources, artifacts;
        public string artPolicy = "Code/numeric settings and code-owned bootstrap scene only; no imported art. Source .meta GUIDs are retained across regeneration.";
    }
    public static void WriteSettingsProvenance()
    {
        string[] sourcePaths = { "UnityProject/Assets/Shooter/Bootstrap/Editor/ShooterBuild.cs", "UnityProject/Assets/Shooter/Bootstrap/Editor/ShooterPostProcessingBuild.cs" };
        var artifacts = Directory.GetFiles("Assets/Shooter/Settings", "*.asset").Concat(Directory.GetFiles("Assets/Shooter/Settings", "*.mat")).Concat(new[] { "Assets/Shooter/Bootstrap/Scenes/Bootstrap.unity" });
        WriteJson("Assets/Shooter/Settings/provenance.json", new SettingsProvenance
        {
            toolchainHash = HashFile(LockPath), sources = sourcePaths.Select(source => new FileProof { path = source, sha256 = HashFile(Path.Combine(RepositoryRoot, source)) }).ToArray(),
            artifacts = artifacts.OrderBy(p => p, StringComparer.Ordinal).Select(p => new FileProof { path = "UnityProject/" + p.Replace('\\', '/'), sha256 = HashFile(p) }).ToArray()
        });
        AssetDatabase.ImportAsset("Assets/Shooter/Settings/provenance.json");
    }
    [Serializable] public sealed class BuildManifest
    {
        public int schemaVersion = 1;
        public string result, target, sourceCommit, sourceHash, toolchainHash, buildLockHash, contentManifestHash, editorVersion, editorRevision, backend;
        public bool sourceDirty, development;
        public string[] graphicsSourceAssets;
        public string[] sourceAssetPaths;
        public ulong totalBytes;
    }
    [Serializable] public sealed class HarnessManifest
    {
        public int schemaVersion = 1;
        public string recipeId = "foundation/box-minimal-v1", generatorVersion = "1.0.0", seed = "460072030", toolchainHash;
        public int[] floorSizeMm = { 10000, 100, 10000 }, floorCenterMm = { 0, -50, 0 }, boxSizeMm = { 1000, 1000, 1000 }, boxCenterMm = { 0, 500, 0 };
        public int[] cameraMm = { 0, 1650, -6000 }, lightEulerMilliDegrees = { 45000, 135000, 0 };
        public FileProof[] sources;
    }
    private static string Git(string arguments)
    {
        var start = new ProcessStartInfo("git", arguments) { WorkingDirectory = RepositoryRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        using var process = Process.Start(start); string result = process.StandardOutput.ReadToEnd(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Cannot determine source identity"); return result.Trim();
    }
    public static void BuildClient() => Guard(() => Build(false));
    public static void BuildServer() => Guard(() => Build(true));
    private static void Build(bool server)
    {
        var toolchain = CheckToolchain();
        // Capture dirt before BuildPlayer/SaveAssets/sibling writers can mutate the tree mid-build.
        // T064/T071 require the candidate identity to reflect the requested source, not editor side effects.
        bool sourceDirtyAtRequest = Git("status --porcelain").Length != 0;
        string sourceCommitAtRequest = Git("rev-parse HEAD");
        string output = Path.GetFullPath(Argument("-shooterOutput"));
        string artifacts = Path.Combine(RepositoryRoot, "Artifacts") + Path.DirectorySeparatorChar;
        if (!output.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Build output must stay inside repository Artifacts");
        Directory.CreateDirectory(output);
        string[] sourcePaths = Directory.GetFiles("Assets/Shooter", "*.cs", SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/Shooter", "*.asmdef", SearchOption.AllDirectories)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var sourceProofs = sourcePaths.Select(p => new FileProof { path = "UnityProject/" + p.Replace('\\', '/'), sha256 = HashFile(p) }).ToArray();
        string contentPath = Path.Combine(Path.GetDirectoryName(output), "harness-manifest.json");
        WriteJson(contentPath, new HarnessManifest { toolchainHash = HashFile(LockPath), sources = sourceProofs });
        string sourceHash;
        using (var digest = SHA256.Create()) sourceHash = BitConverter.ToString(digest.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", sourceProofs.Select(p => p.path + ":" + p.sha256))))).Replace("-", "").ToLowerInvariant();
        // Dedicated Server strips/invalidates graphics objects during its build. Retain asset paths,
        // then reload after the build instead of restoring potentially invalid native object handles.
        string[] profileNames = { "Low", "Balanced", "High" };
        if (!QualitySettings.names.SequenceEqual(profileNames)) throw new InvalidOperationException("Unexpected foundation quality registry");
        string[] qualityPaths = profileNames.Select(name => "Assets/Shooter/Settings/" + name + ".asset").ToArray();
        const string globalPath = "Assets/Shooter/Settings/UniversalRenderPipelineGlobalSettings.asset";
        foreach (string path in qualityPaths) if (AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path) == null) throw new InvalidOperationException("Missing foundation pipeline: " + path);
        if (AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(globalPath) == null) throw new InvalidOperationException("Missing foundation global settings");
        int previousQuality = QualitySettings.GetQualityLevel();
        var preloaded = PlayerSettings.GetPreloadedAssets();
        try
        {
            if (server)
            {
                GraphicsSettings.defaultRenderPipeline = null;
                for (int i = 0; i < qualityPaths.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = null; }
                UnityEditor.Rendering.EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(null); PlayerSettings.SetPreloadedAssets(Array.Empty<UnityEngine.Object>());
            }
            else
            {
                var surfaceVariants = ProductSurfaceVariantPaths.Select(path =>
                    AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new InvalidOperationException(
                        "Missing numeric surface variant material; run ShooterBuild.ConfigureProductSurfaceVariants: " + path));
                PlayerSettings.SetPreloadedAssets(preloaded.Concat(new UnityEngine.Object[]
                {
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/Shooter/Settings/HarnessMaterial.mat"),
                    AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shooter/Shaders/ProceduralSurface.shader"),
                    AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shooter/Shaders/AdsScopePip.shader")
                        ?? throw new InvalidOperationException("Missing AdsScopePip shader"),
                    AssetDatabase.LoadAssetAtPath<Shader>(ShooterPostProcessingBuild.MapSurfaceShaderPath)
                        ?? throw new InvalidOperationException("Missing MapSurface shader"),
                    AssetDatabase.LoadAssetAtPath<Material>(ShooterPostProcessingBuild.MapSurfaceVariantPath)
                        ?? throw new InvalidOperationException("Missing MapSurface variant material; run ShooterPostProcessingBuild.Configure")
                }).Concat(surfaceVariants).ToArray());
            }
            QualitySettings.SetQualityLevel(0, false);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Shooter/Bootstrap/Scenes/Bootstrap.unity" }, locationPathName = Path.Combine(output, "Shooter.exe"),
                target = BuildTarget.StandaloneWindows64, subtarget = (int)(server ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player), options = BuildOptions.None
            });
            string[] sourceAssets = report.packedAssets.SelectMany(p => p.contents).Select(p => p.sourceAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            string[] artExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".exr", ".hdr", ".psd", ".fbx", ".obj", ".blend", ".anim", ".spriteatlas", ".wav", ".mp3", ".ogg" };
            string[] importedArt = sourceAssets.Where(p => artExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())).ToArray();
            string[] graphics = sourceAssets.Where(p => p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || importedArt.Contains(p)).ToArray();
            var manifest = new BuildManifest { result = report.summary.result.ToString(), target = server ? "Server" : "Client", sourceCommit = sourceCommitAtRequest,
                sourceDirty = sourceDirtyAtRequest, sourceHash = sourceHash, toolchainHash = HashFile(LockPath), buildLockHash = HashFile(BuildLockPath), contentManifestHash = HashFile(contentPath), editorVersion = toolchain.editorVersion, editorRevision = toolchain.editorRevision,
                backend = server ? toolchain.serverBackend : toolchain.clientBackend, development = false, totalBytes = report.summary.totalSize, graphicsSourceAssets = graphics, sourceAssetPaths = sourceAssets };
            WriteJson(Argument("-shooterReport"), manifest);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build failed: " + report.summary.result);
            if (importedArt.Length != 0) throw new InvalidOperationException("Unapproved imported art in player: " + string.Join(",", importedArt));
            if (server && graphics.Length != 0) throw new InvalidOperationException("Server unexpectedly includes graphics payloads: " + string.Join(",", graphics));
            if (!server) CopyCombatAudioBankBesidePlayer(output);
            Debug.Log("SHOOTER_BUILD_SUCCEEDED " + manifest.target);
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(qualityPaths[0]);
            for (int i = 0; i < qualityPaths.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(qualityPaths[i]); }
            UnityEditor.Rendering.EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(globalPath));
            PlayerSettings.SetPreloadedAssets(preloaded); QualitySettings.SetQualityLevel(previousQuality, true); AssetDatabase.SaveAssets();
            // URP's locked build preprocessor generates shader-prefilter cache fields in these settings.
            // Record that actual engine-generated stage, rather than modifying its payload to match an old hash.
            WriteSettingsProvenance();
        }
    }
    public static void CompileContent() => Guard(ShooterContentBuild.Compile);
    public static void CompileNativeGameContent() => Guard(ShooterNativeContentBuild.Compile);

    /// <summary>
    /// T089: ship stereo combat WAV bank beside the client (not under Assets — .wav is rejected as imported art).
    /// </summary>
    private static void CopyCombatAudioBankBesidePlayer(string playerOutputDirectory)
    {
        string src = Path.Combine(RepositoryRoot, "Content", "Audio", "combat");
        string manifest = Path.Combine(src, "manifest.json");
        if (!Directory.Exists(src) || !File.Exists(manifest))
            throw new InvalidOperationException("Missing Content/Audio/combat bank; run Tools/generate_combat_audio_bank.py");
        string dst = Path.Combine(playerOutputDirectory, "Content", "Audio", "combat");
        Directory.CreateDirectory(dst);
        foreach (string file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
    }
}
