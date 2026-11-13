using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation
{
    /// <summary>B01-materials only: verified seven-family surfaces, original foundation floor/light/camera recipe.
    /// The central plaster replaces the foundation cube visual; gameplay colliders remain unchanged.</summary>
    public sealed class MaterialBenchmarkView : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private static readonly string[] families = { "metal", "polymer", "concrete", "plaster", "asphalt", "soil", "fabric" };
        private readonly List<GameObject> cubes = new List<GameObject>();
        private GeneratedSky sky;
        private GeneratedReflectionProbe reflectionProbe;
        public Camera ViewCamera { get; private set; }
        public string ContentManifestJson { get; private set; }
        public string ContentManifestHash { get; private set; }
        public long TextureBytes { get; private set; }
        public const int RenderInstances = 8, Triangles = 96, MaterialCubes = 7;
        public static MaterialBenchmarkView Create(string root, ContentId toolchain, Shader shader)
        {
            var view = new GameObject("B01 materials presentation").AddComponent<MaterialBenchmarkView>();
            try { view.Initialize(root, toolchain, shader); return view; }
            catch { DestroyObject(view.gameObject); throw; }
        }
        private void Initialize(string root, ContentId toolchain, Shader shader)
        {
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Required B01 procedural shader unavailable; no fallback.");
            var files = new CacheFiles(root, toolchain);
            string planPath = Path.Combine(files.Root, "gallery.json"); files.AssertSafePath(planPath);
            if (new FileInfo(planPath).Length > CanonicalJson.MaxBytes) throw new InvalidDataException("Material source plan exceeds limit.");
            var plan = JsonValue.Parse(File.ReadAllBytes(planPath)).Object;
            if (plan["schema"].String != "shooter.gallery.v1" || plan["profile"].String != "Low" ||
                new ContentId(plan["toolchainHash"].String) != toolchain || plan["materials"].Array.Count != 7)
                throw new InvalidDataException("B01 requires verified Low material source plan.");
            Mesh box = Own(FoundationView.CreateUnitBox()); box.RecalculateTangents();
            var identities = new List<string>();
            using (var store = new FileContentStore(files))
            for (int i = 0; i < 7; i++)
            {
                var item = plan["materials"].Array[i].Object;
                if (item["family"].String != families[i]) throw new InvalidDataException("B01 material family order mismatch.");
                var id = new ContentId(item["contentId"].String); var manifest = files.ReadManifest(id);
                int tileMm = item["tileMm"].Number;
                if (manifest.RecipeId != "materials/" + families[i] || manifest.VisualProfile != "Low" || manifest.BuildPlatform != "windows64" ||
                    manifest.GeneratorId != "material.surface" || manifest.GeneratorVersion != "1.0.1" || manifest.Seed != 460072030UL ||
                    manifest.MaterialLayerIds.Count != 1 || manifest.MaterialLayerIds[0] != families[i] || manifest.Artifacts.Count != 3 ||
                    tileMm != (i == 0 ? 1000 : i == 1 ? 500 : i == 6 ? 250 : 2000))
                    throw new InvalidDataException("B01 material identity/physical scale mismatch.");
                string source = "{\"schemaId\":\"shooter.recipe.material.v1\",\"recipeId\":\"materials/" + families[i] +
                    "\",\"generatorId\":\"material.surface\",\"generatorVersion\":\"1.0.1\",\"seed\":\"460072030\",\"parameters\":{\"family\":\"" + families[i] +
                    "\",\"resolution\":256,\"tileMm\":" + tileMm.ToString(CultureInfo.InvariantCulture) + "},\"dependencies\":[]}";
                if (MaterialRecipe.Parse(Encoding.ASCII.GetBytes(source)).SourceHash != manifest.SourceHash)
                    throw new InvalidDataException("B01 material source recipe mismatch.");
                var material = Own(new Material(shader) { name = "B01 generated " + families[i] });
                foreach (string plane in new[] { "albedo", "normal", "mask" })
                {
                    var result = store.AcquireVariantAsync(id, ContentKind.Material, plane, CancellationToken.None).GetAwaiter().GetResult();
                    if (result.Error != ErrorCode.None || result.Lease == null) throw new InvalidDataException("Required B01 verified material unavailable: " + result.Diagnostic);
                    byte[] payload;
                    using (result.Lease) { payload = new byte[result.Lease.Length]; result.Lease.CopyTo(payload); }
                    var data = GeometryCodec.DecodeMaterial(ArtifactCodec.Decode(payload, ContentKind.Material, manifest.GeneratorHash, toolchain));
                    if (data.Width != 256 || data.Height != 256 || data.ChannelFormat != (plane == "albedo" ? "rgba8-srgb" : "rgba8-linear"))
                        throw new InvalidDataException("B01 material dimensions/channel format mismatch.");
                    var texture = Upload(data, families[i] + " " + plane);
                    material.SetTexture(plane == "albedo" ? "_BaseMap" : plane == "normal" ? "_NormalMap" : "_MaskMap", texture);
                }
                material.EnableKeyword("_NORMALMAP"); material.SetFloat("_Tiling", 1000f / tileMm);
                cubes.Add(Place("B01 " + families[i], box, material, new Vector3(-6 + i * 2, .5f, 0), Vector3.one));
                identities.Add("{\"contentId\":\"" + id + "\",\"family\":\"" + families[i] + "\",\"manifestSha256\":\"" + ContentIdentity.Hash(manifest.Encode()) + "\",\"tileMm\":" + tileMm.ToString(CultureInfo.InvariantCulture) + "}");
            }
            var floorShader = Shader.Find("Universal Render Pipeline/Lit");
            if (floorShader == null) throw new InvalidOperationException("Locked foundation Lit shader unavailable.");
            var floor = Own(new Material(floorShader) { name = "Numeric matte concrete" });
            floor.SetColor("_BaseColor", new Color(.42f, .46f, .48f, 1)); floor.SetFloat("_Smoothness", .12f); floor.SetFloat("_Metallic", 0);
            Place("Floor visual", box, floor, new Vector3(0, -.05f, 0), new Vector3(10, .1f, 10));
            ViewCamera = new GameObject("B01 materials camera").AddComponent<Camera>(); ViewCamera.transform.SetParent(transform, false);
            ViewCamera.fieldOfView = 60; ViewCamera.nearClipPlane = .1f; ViewCamera.farClipPlane = 200;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor; ViewCamera.backgroundColor = new Color(.11f, .14f, .18f, 1);
            ViewCamera.allowHDR = false; ViewCamera.allowMSAA = false; SetView(0);
            var sun = new GameObject("Foundation sun").AddComponent<Light>(); sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(45, 135, 0); sun.intensity = 1; sun.shadows = LightShadows.Hard;
            // Fog off: B01 framing keeps camera/content inventory semantics; sky still supplies Trilight ambient.
            sky = GeneratedSky.Apply(transform, enableFog: false);
            reflectionProbe = GeneratedReflectionProbe.Apply(transform);
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            if (Application.isPlaying) Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            string json = "{\"floor\":\"foundation-matte-10m-v1\",\"materialCubes\":7,\"materials\":[" + string.Join(",", identities) +
                "],\"renderInstances\":8,\"schema\":\"shooter.b01-materials-content.v1\",\"textureFormat\":\"RGBA32\",\"toolchainHash\":\"" + toolchain + "\",\"triangles\":96,\"workloadId\":\"B01-materials-v1\"}";
            byte[] bytes = CanonicalJson.Encode(Encoding.ASCII.GetBytes(json)); ContentManifestJson = Encoding.ASCII.GetString(bytes);
            ContentManifestHash = ContentIdentity.Hash(bytes).ToString();
            Debug.Log("SHOOTER_MATERIAL_BENCHMARK_READY content=" + ContentManifestHash + " textureBytes=" + TextureBytes + " bakeRan=" + reflectionProbe.BakeRan);
        }
        public void SetView(int index)
        {
            if (index < 0 || index >= 7) throw new ArgumentOutOfRangeException(nameof(index));
            ViewCamera.transform.SetPositionAndRotation(new Vector3(-6 + 2 * index, 1.65f, -6), Quaternion.identity);
        }
        public void ValidateView(int index)
        {
            if (ViewCamera == null || Vector3.Distance(ViewCamera.transform.position, new Vector3(-6 + 2 * index, 1.65f, -6)) > .0001f ||
                Quaternion.Angle(ViewCamera.transform.rotation, Quaternion.identity) > .0001f || ViewCamera.fieldOfView != 60 ||
                GetComponentsInChildren<MeshRenderer>().Length != 8 || cubes.Count != 7 ||
                UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length != 8 ||
                UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("B01 materials view/instance configuration mismatch.");
        }
        private Texture2D Upload(MaterialData data, string name)
        {
            var texture = Own(new Texture2D(data.Width, data.Height, TextureFormat.RGBA32, true, data.ChannelFormat != "rgba8-srgb")
                { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 });
            for (int mip = 0; mip < data.MipBytes.Count; mip++)
            {
                var source = data.MipBytes[mip]; byte[] bytes = new byte[source.Count];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = source[i];
                texture.SetPixelData(bytes, mip); TextureBytes += bytes.Length;
            }
            texture.Apply(false, true); return texture;
        }
        private GameObject Place(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var item = new GameObject(name) { layer = 11 }; item.transform.SetParent(transform, false);
            item.transform.localPosition = position; item.transform.localScale = scale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh; item.AddComponent<MeshRenderer>().sharedMaterial = material; return item;
        }
        private T Own<T>(T item) where T : UnityEngine.Object { owned.Add(item); return item; }
        private static void DestroyObject(UnityEngine.Object item) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        private void OnDestroy()
        {
            reflectionProbe?.Dispose(); reflectionProbe = null;
            sky?.Dispose(); sky = null;
            foreach (var item in owned) if (item != null) DestroyObject(item);
        }
    }
}
