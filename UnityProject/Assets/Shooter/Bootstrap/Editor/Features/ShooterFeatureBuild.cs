using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

public static class ShooterFeatureBuild
{
    [Serializable] public sealed class Item { public string recipeId, profile, contentId, manifestSha256, sourceHash, seed; public int vertices, triangles, bytes; public bool reused; }
    [Serializable] public sealed class Socket { public string id; public float[] position, rotation; }
    [Serializable] public sealed class Weapon { public string kind; public int[] lodTriangles; public Socket[] sockets; }
    [Serializable] public sealed class CompileProof
    {
        public string result = "Verified", scope = "M1 P03/P04 diagnostic content; no soldier, combat or hardware gate", profile, toolchainHash, galleryPlanSha256;
        public Item[] content; public Weapon[] weapons; public double elapsedMilliseconds;
        public ShooterBuild.FileProof[] sources;
    }
    [Serializable] public sealed class CaptureProof
    {
        public string result = "Verified", workload = "M1-content-gallery-v1", scope = "Editor visual diagnostic only; not B01-materials-v1 performance acceptance";
        public string[] captures; public long textureBytes, meshBytes; public int trianglesAllLods; public ulong shaderVariants;
        public bool compressedTextures = false, shaderVariantCountAvailable = false;
        public int declaredKeywordCombinations = 25;
    }
    public static void Compile() => Guard(() =>
    {
        ShooterBuild.CheckToolchain(); string root = CacheRoot(), profile = ShooterBuild.Argument("-shooterProfile", "Low");
        if (profile != "Low" && profile != "Balanced" && profile != "High") throw new ArgumentException("Unknown profile.");
        bool force = bool.Parse(ShooterBuild.Argument("-shooterForce", "false"));
        var hash = new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath)); var files = new CacheFiles(root, hash);
        var nodes = new List<CompilationNode>(); var weaponRecipes = new List<WeaponRecipe>();
        foreach (string kind in new[] { "rifle", "pistol" })
        {
            var recipe = WeaponRecipe.Parse(Source(kind)); weaponRecipes.Add(recipe);
            var validated = new ValidatedRecipe(recipe.Document, recipe.CanonicalSource, new WeaponRecipeValidator());
            nodes.Add(new CompilationNode(validated, "windows64", profile, hash, new WeaponAssetGenerator()));
            nodes.Add(new CompilationNode(validated, "windows64", "gameplay", hash, new WeaponAssetGenerator()));
        }
        var materialRecipes = new List<MaterialRecipe>();
        foreach (string family in GeneratedGallery.Families)
        {
            var recipe = MaterialRecipe.Parse(Source(family)); materialRecipes.Add(recipe);
            nodes.Add(new CompilationNode(new ValidatedRecipe(recipe.Document, recipe.CanonicalSource, new MaterialRecipeValidator()), "windows64", profile, hash, new MaterialAssetGenerator(), new[] { family }));
        }
        var timer = Stopwatch.StartNew();
        var result = new ContentCompiler(files).CompileAsync(nodes, force, CancellationToken.None).GetAwaiter().GetResult();
        if (result.Error != ErrorCode.None) throw new InvalidOperationException(result.Error + ": " + result.Diagnostic);
        Item[] items = result.Entries.Select(e => new Item { recipeId = e.Manifest.RecipeId, profile = e.Manifest.VisualProfile, contentId = e.Manifest.ContentId.ToString(), manifestSha256 = ContentIdentity.Hash(e.Manifest.Encode()).ToString(), sourceHash = e.Manifest.SourceHash.ToString(), seed = e.Manifest.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), vertices = e.Manifest.VertexCount, triangles = e.Manifest.TriangleCount, bytes = e.Manifest.Artifacts.Sum(a => a.Bytes), reused = e.Reused }).ToArray();
        string Id(string recipe, string tier) => items.Single(e => e.recipeId == recipe && e.profile == tier).contentId;
        string materials = string.Join(",", materialRecipes.Select(r => "{\"family\":\"" + r.Family + "\",\"tileMm\":" + r.TileMm + ",\"contentId\":\"" + Id(r.Document.RecipeId.ToString(), profile) + "\"}"));
        string plan = "{\"schema\":\"shooter.gallery.v1\",\"toolchainHash\":\"" + hash + "\",\"profile\":\"" + profile + "\",\"materials\":[" + materials + "],\"rifleVisual\":\"" + Id("weapons/rifle", profile) + "\",\"pistolVisual\":\"" + Id("weapons/pistol", profile) + "\",\"rifleGameplay\":\"" + Id("weapons/rifle", "gameplay") + "\",\"pistolGameplay\":\"" + Id("weapons/pistol", "gameplay") + "\"}";
        byte[] planBytes = CanonicalJson.Encode(Encoding.UTF8.GetBytes(plan));
        string target = Path.Combine(root, "gallery.json"), temporary = Path.Combine(root, ".gallery-" + Guid.NewGuid().ToString("N") + ".tmp");
        files.AssertSafePath(temporary); files.AssertSafePath(target);
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(planBytes, 0, planBytes.Length); file.Flush(true); }
        if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
        timer.Stop();
        var weapons = weaponRecipes.Select(r => new Weapon { kind = r.Kind, lodTriangles = WeaponGeometry.Build(r).Lods.Select(m => m.Indices.Count / 3).ToArray(), sockets = WeaponGeometry.Sockets(r).Select(s => new Socket { id = s.Id, position = new[] { s.Position.X, s.Position.Y, s.Position.Z }, rotation = new[] { s.Rotation.X, s.Rotation.Y, s.Rotation.Z, s.Rotation.W } }).ToArray() }).ToArray();
        var sources = Directory.GetFiles(Path.Combine(Application.dataPath, "Shooter"), "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs", StringComparison.Ordinal) || p.EndsWith(".shader", StringComparison.Ordinal) || p.Replace('\\', '/').Contains("/FeatureRecipes/") && p.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => new ShooterBuild.FileProof { path = "UnityProject/" + p.Substring(Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Length + 1).Replace('\\', '/'), sha256 = ShooterBuild.HashFile(p) }).ToArray();
        ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), new CompileProof { profile = profile, toolchainHash = hash.ToString(), content = items, weapons = weapons, elapsedMilliseconds = timer.Elapsed.TotalMilliseconds, galleryPlanSha256 = ContentIdentity.Hash(planBytes).ToString(), sources = sources });
        Debug.Log("SHOOTER_FEATURE_CONTENT_VERIFIED content=" + items.Length);
    });
    public static void Capture() => Guard(() =>
    {
        ShooterBuild.CheckToolchain(); string root = CacheRoot(), output = Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot"));
        RequireArtifacts(output); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        QualitySettings.SetQualityLevel(0, true);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shooter/Shaders/ProceduralSurface.shader");
        if (shader == null || ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error")) throw new InvalidOperationException("Procedural shader compile errors.");
        var gallery = GeneratedGallery.Create(root, new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath)), shader);
        try
        {
            var paths = new List<string>();
            void Shot(string name)
            {
                string file = Path.Combine(output, name + ".png"); Render(gallery.ViewCamera, file); paths.Add(file);
            }
            Shot("gallery");
            for (int material = 0; material < 7; material++) { gallery.FocusMaterial(material); Shot("material-" + GeneratedGallery.Families[material]); }
            for (int weapon = 0; weapon < 2; weapon++)
            {
                string kind = weapon == 0 ? "rifle" : "pistol";
                foreach (string angle in new[] { "front", "side", "three-quarter", "ads" }) { gallery.FocusWeapon(weapon, 0, angle); Shot(kind + "-" + angle); }
                for (int lod = 1; lod < 4; lod++) { gallery.FocusWeapon(weapon, lod, "side"); Shot(kind + "-lod" + lod); }
            }
            if (ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error")) throw new InvalidOperationException("Procedural shader failed during rendering.");
            ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), new CaptureProof { captures = paths.ToArray(), textureBytes = gallery.TextureBytes, meshBytes = gallery.MeshBytes, trianglesAllLods = gallery.Triangles, shaderVariants = 0 });
        }
        finally { UnityEngine.Object.DestroyImmediate(gallery.gameObject); }
    });
    // Offline source visual diagnostic with the same URP material adapter as the product.
    // Captures are not first-person gameplay, packaged-player or performance acceptance.
    public static void CaptureArsenal() => Guard(() =>
    {
        ShooterBuild.CheckToolchain();
        string output = Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot"));
        RequireArtifacts(output); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        QualitySettings.SetQualityLevel(0, true);
        var owned = new List<UnityEngine.Object>();
        try
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Required URP Lit unavailable.");
            Material Surface(int family)
            {
                var bake = Shooter.Generation.Environment.FirstPersonSurfaceBaker.Bake(family);
                return Shooter.Presentation.Rendering.ProductSurfaceMaterial.Create(shader, bake.Albedo, bake.Normal, bake.Mask,
                    "Arsenal source finish " + family, 10, value => owned.Add(value));
            }
            var palette = Shooter.Presentation.Rendering.ProductSurfaceMaterial.WeaponFinishes(Surface(0), Surface(1), Surface(4), Surface(5), Surface(6));
            var cameraObject = new GameObject("Arsenal capture camera"); owned.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .09f, .11f);
            camera.nearClipPlane = .005f; camera.farClipPlane = 10; camera.fieldOfView = 32;
            var lightObject = new GameObject("Arsenal key"); owned.Add(lightObject);
            var key = lightObject.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 2.0f;
            key.transform.rotation = Quaternion.Euler(35, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.32f, .36f, .42f);
            var paths = new List<string>();
            foreach (string kind in new[] { "rifle", "pistol", "knife", "grenade" })
            {
                var root = new GameObject(kind); owned.Add(root);
                var parts = new List<MeshData>();
                if (kind == "rifle" || kind == "pistol")
                {
                    var build = WeaponGeometry.Build(WeaponRecipe.Parse(Source(kind)));
                    parts.Add(build.Lods[0]); parts.Add(build.Magazine); parts.Add(build.CyclePart);
                }
                else parts.Add(kind == "knife" ? LoadoutPropGeometry.BuildKnife(0) : LoadoutPropGeometry.BuildGrenade(0));
                foreach (var data in parts)
                {
                    var mesh = new Mesh(); owned.Add(mesh);
                    mesh.vertices = data.Positions.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
                    mesh.normals = data.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
                    mesh.tangents = data.Tangents.Select(v => new Vector4(v.X, v.Y, v.Z, v.W)).ToArray();
                    mesh.uv = data.Uv.Select(v => new Vector2(v.X, v.Y)).ToArray();
                    mesh.subMeshCount = data.Submeshes.Count;
                    for (int i = 0; i < data.Submeshes.Count; i++)
                    {
                        var sub = data.Submeshes[i];
                        mesh.SetIndices(data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v => (int)v).ToArray(), MeshTopology.Triangles, i);
                    }
                    mesh.RecalculateBounds();
                    var child = new GameObject("Source part"); child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    child.AddComponent<MeshRenderer>().sharedMaterials = data.Submeshes.Select(sub => palette[sub.MaterialSlot]).ToArray();
                }
                var renderers = root.GetComponentsInChildren<MeshRenderer>(); var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float distance = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * 2.2f;
                foreach (string angle in new[] { "side", "three-quarter" })
                {
                    camera.transform.position = bounds.center + (angle == "side" ? Vector3.right : new Vector3(.8f, .35f, -.7f).normalized) * distance;
                    camera.transform.LookAt(bounds.center);
                    string path = Path.Combine(output, kind + "-" + angle + ".png"); Render(camera, path); paths.Add(path);
                }
                root.SetActive(false);
            }
            File.WriteAllText(Path.Combine(output, "scope.txt"), "Offline source LOD0 arsenal diagnostic, URP Lit, 1280x720. Not gameplay/visual acceptance. geometry.weapon " + WeaponRecipe.GeneratorVersion + " source " + ShooterBuild.HashFile(Path.Combine(Application.dataPath, "Shooter/Generation/Core/Weapons/WeaponGeometry.cs")));
            Debug.Log("SHOOTER_ARSENAL_CAPTURE files=" + paths.Count);
        }
        finally { foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value); }
    });

    internal static void Render(Camera camera, string path)
    {
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create();
        var prior = RenderTexture.active; Texture2D pixels = null;
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request); RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target; pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32(); int pink = 0;
            foreach (var c in colors) if (c.r > 220 && c.b > 220 && c.g < 40) pink++;
            if (pink > colors.Length / 100) throw new InvalidOperationException("Magenta shader error detected in capture.");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = prior; if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
    private static byte[] Source(string name) => File.ReadAllBytes(Path.Combine(Application.dataPath, "Shooter/Generation/FeatureRecipes/" + name + ".json"));
    private static string CacheRoot() { string path = Path.GetFullPath(ShooterBuild.Argument("-shooterCacheRoot")); RequireArtifacts(path); return path; }
    private static void RequireArtifacts(string path)
    { if (!path.StartsWith(Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output must stay under repository Artifacts."); }
    private static void Guard(Action body) { try { body(); } catch (Exception error) { Debug.LogError("SHOOTER_FAILURE: " + error); EditorApplication.Exit(1); } }
}
