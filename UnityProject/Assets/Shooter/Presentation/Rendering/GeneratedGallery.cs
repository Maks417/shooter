using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation
{
    /// <summary>Explicit diagnostic view of precompiled verified content; read-only with no authority or colliders.</summary>
    public sealed class GeneratedGallery : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<GameObject> weaponRoots = new List<GameObject>();
        private GeneratedSky sky;
        private GeneratedReflectionProbe reflectionProbe;
        public Camera ViewCamera { get; private set; }
        public long TextureBytes { get; private set; }
        public long MeshBytes { get; private set; }
        public int Triangles { get; private set; }
        public static readonly string[] Families = { "metal", "polymer", "concrete", "plaster", "asphalt", "soil", "fabric" };
        public static GeneratedGallery Create(string cacheRoot, ContentId expectedToolchain, Shader shader)
        {
            var view = new GameObject("M1 verified content diagnostic").AddComponent<GeneratedGallery>();
            try { view.Initialize(cacheRoot, expectedToolchain, shader); return view; }
            catch { DestroyObject(view.gameObject); throw; }
        }
        private void Initialize(string cacheRoot, ContentId expectedToolchain, Shader shader)
        {
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Required procedural Low shader is unavailable; no visual fallback.");
            var files = new CacheFiles(cacheRoot, expectedToolchain);
            files.AssertSafePath(Path.Combine(files.Root, "gallery.json"));
            var planPath = Path.Combine(files.Root, "gallery.json");
            if (new FileInfo(planPath).Length > CanonicalJson.MaxBytes) throw new InvalidDataException("Gallery plan exceeds limit.");
            var plan = JsonValue.Parse(File.ReadAllBytes(planPath)).Object;
            if (plan["schema"].String != "shooter.gallery.v1" || new ContentId(plan["toolchainHash"].String) != expectedToolchain || plan["materials"].Array.Count != 7)
                throw new InvalidDataException("Gallery plan identity mismatch.");
            using var store = new FileContentStore(files);
            var materials = new Material[7];
            for (int i = 0; i < 7; i++)
            {
                var item = plan["materials"].Array[i].Object;
                if (item["family"].String != Families[i]) throw new InvalidDataException("Material registry order mismatch.");
                var id = new ContentId(item["contentId"].String); var manifest = files.ReadManifest(id);
                materials[i] = Own(new Material(shader) { name = "Generated " + Families[i] });
                foreach (string plane in new[] { "albedo", "normal", "mask" })
                {
                    byte[] payload = Acquire(store, id, ContentKind.Material, plane);
                    var data = GeometryCodec.DecodeMaterial(ArtifactCodec.Decode(payload, ContentKind.Material, manifest.GeneratorHash, expectedToolchain));
                    Texture2D texture = Upload(data, Families[i] + " " + plane);
                    materials[i].SetTexture(plane == "albedo" ? "_BaseMap" : plane == "normal" ? "_NormalMap" : "_MaskMap", texture);
                }
                materials[i].EnableKeyword("_NORMALMAP");
                int tileMm = item["tileMm"].Number;
                if (tileMm < 100 || tileMm > 8000 || manifest.RecipeId != "materials/" + Families[i] || manifest.MaterialLayerIds.Count != 1 || manifest.MaterialLayerIds[0] != Families[i])
                    throw new InvalidDataException("Gallery material identity or physical scale mismatch.");
                materials[i].SetFloat("_Tiling", 1000f / tileMm);
            }
            var box = Own(FoundationView.CreateUnitBox()); box.RecalculateTangents();
            for (int i = 0; i < 7; i++) Place("Material " + Families[i], box, new[] { materials[i] }, new Vector3(-6 + 2 * i, .5f, 0), Vector3.one);
            var floor = Own(new Material(shader) { name = "Generated gallery floor" }); floor.CopyPropertiesFromMaterial(materials[2]);
            Mesh floorMesh = Own(FoundationView.CreateUnitBox());
            var floorVertices = floorMesh.vertices; var floorNormals = floorMesh.normals; var floorUv = floorMesh.uv;
            for (int i = 0; i < floorVertices.Length; i++)
            {
                floorVertices[i] = Vector3.Scale(floorVertices[i], new Vector3(16, .1f, 8));
                Vector3 p = floorVertices[i], n = floorNormals[i];
                floorUv[i] = Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) : Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
            }
            floorMesh.vertices = floorVertices; floorMesh.uv = floorUv; floorMesh.RecalculateTangents(); floorMesh.RecalculateBounds();
            Place("Gallery floor", floorMesh, new[] { floor }, new Vector3(0, -.06f, 0), Vector3.one);
            // Inventory counts include eight scene instances and two shared analytic mesh buffers.
            Triangles += 8 * 12; MeshBytes += 2 * (24 * 48 + 36 * 4);
            var steel = Own(new Material(materials[0]) { name = "Diagnostic exposed steel" });
            steel.SetColor("_BaseColor", new Color(1.6f, 1.65f, 1.7f));
            var recess = Own(new Material(materials[1]) { name = "Diagnostic recess" });
            recess.SetColor("_BaseColor", new Color(.25f, .25f, .25f));
            var weaponPalette = new[] { materials[0], materials[1], steel, recess, materials[0] };
            for (int weapon = 0; weapon < 2; weapon++)
            {
                string kind = weapon == 0 ? "rifle" : "pistol";
                var id = new ContentId(plan[kind + "Visual"].String); var manifest = files.ReadManifest(id);
                var root = new GameObject("Generated " + kind); root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3(weapon == 0 ? -1.2f : 1.2f, 1.35f, -1.8f);
                root.transform.localRotation = Quaternion.Euler(0, 80, 0); weaponRoots.Add(root);
                for (int lod = 0; lod < 4; lod++)
                {
                    var data = GeometryCodec.DecodeMesh(ArtifactCodec.Decode(Acquire(store, id, ContentKind.Mesh, "lod" + lod), ContentKind.Mesh, manifest.GeneratorHash, expectedToolchain));
                    Mesh mesh = Upload(data, kind + " LOD" + lod);
                    var mapped = data.Submeshes.Select(s => weaponPalette[s.MaterialSlot]).ToArray();
                    var child = Place(kind + " LOD" + lod, mesh, mapped, Vector3.zero, Vector3.one, root.transform);
                    foreach (string part in new[] { "magazine", "cycle" })
                    {
                        var partData = GeometryCodec.DecodeMesh(ArtifactCodec.Decode(Acquire(store, id, ContentKind.Mesh, part), ContentKind.Mesh, manifest.GeneratorHash, expectedToolchain));
                        Place(kind + " " + part, Upload(partData, kind + " " + part),
                            partData.Submeshes.Select(s => weaponPalette[s.MaterialSlot]).ToArray(), Vector3.zero, Vector3.one, child.transform);
                    }
                    child.SetActive(lod == 0);
                }
            }
            ViewCamera = new GameObject("M1 diagnostic camera").AddComponent<Camera>(); ViewCamera.transform.SetParent(transform, false);
            ViewCamera.transform.position = new Vector3(0, 2.5f, -10); ViewCamera.transform.LookAt(new Vector3(0, .7f, 0));
            ViewCamera.fieldOfView = 55; ViewCamera.nearClipPlane = .03f; ViewCamera.farClipPlane = 50;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor; ViewCamera.backgroundColor = new Color(.14f, .18f, .23f); ViewCamera.allowHDR = false; ViewCamera.allowMSAA = false;
            for (int i = 0; i < weaponRoots.Count; i++)
            {
                var lod = weaponRoots[i].AddComponent<GeneratedWeaponLod>();
                lod.ViewCamera = ViewCamera; lod.DiameterMeters = i == 0 ? 1.04f : .30f;
                lod.LocalCenter = i == 0 ? new Vector3(0,-.025f,.16f) : new Vector3(0,.01f,.025f);
            }
            var sun = new GameObject("Fixed procedural daylight").AddComponent<Light>(); sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(45, 135, 0); sun.intensity = 1; sun.shadows = LightShadows.Hard;
            sky = GeneratedSky.Apply(transform);
            reflectionProbe = GeneratedReflectionProbe.Apply(transform);
            if (RenderSettings.skybox != null) ViewCamera.clearFlags = CameraClearFlags.Skybox;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            if (Application.isPlaying) Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            Debug.Log("SHOOTER_CONTENT_GALLERY_READY textures=" + TextureBytes + " meshBytes=" + MeshBytes + " trianglesAllLods=" + Triangles + " textureFormat=RGBA32 bakeRan=" + reflectionProbe.BakeRan);
        }
        public void FocusWeapon(int index, int lod, string angle)
        {
            if (index < 0 || index >= weaponRoots.Count || lod < 0 || lod > 3) throw new ArgumentOutOfRangeException();
            for (int i = 0; i < weaponRoots.Count; i++)
            {
                var root = weaponRoots[i]; root.SetActive(i == index); root.transform.rotation = Quaternion.identity;
                root.GetComponent<GeneratedWeaponLod>().Inspect(lod);
            }
            Vector3 target = weaponRoots[index].transform.position + new Vector3(0, .02f, index == 0 ? .14f : .03f);
            if (angle == "ads")
            {
                ViewCamera.transform.position = weaponRoots[index].transform.position + new Vector3(0, index == 0 ? .115f : .1f, index == 0 ? -.16f : -.15f);
                ViewCamera.transform.rotation = Quaternion.identity; ViewCamera.nearClipPlane = .005f; return;
            }
            ViewCamera.nearClipPlane = .03f;
            float distance = index == 0 ? 1.1f : .4f;
            Vector3 offset = angle == "front" ? new Vector3(0, .02f, distance) : angle == "side" ? new Vector3(distance, .02f, 0) : new Vector3(distance * .8f, distance * .35f, -distance * .7f);
            ViewCamera.transform.position = target + offset; ViewCamera.transform.LookAt(target);
        }
        public void FocusMaterial(int index)
        {
            if (index < 0 || index >= 7) throw new ArgumentOutOfRangeException(nameof(index));
            foreach (var root in weaponRoots) root.SetActive(false);
            var center = new Vector3(-6 + 2 * index, .5f, 0);
            ViewCamera.nearClipPlane = .03f; ViewCamera.transform.position = center + new Vector3(1.1f, .65f, -1.6f); ViewCamera.transform.LookAt(center);
        }
        private static byte[] Acquire(FileContentStore store, ContentId id, ContentKind kind, string variant)
        {
            var result = store.AcquireVariantAsync(id, kind, variant, CancellationToken.None).GetAwaiter().GetResult();
            if (result.Error != ErrorCode.None || result.Lease == null) throw new InvalidDataException("Verified gallery content unavailable: " + result.Diagnostic);
            using (result.Lease) { var bytes = new byte[result.Lease.Length]; result.Lease.CopyTo(bytes); return bytes; }
        }
        private Texture2D Upload(MaterialData data, string label)
        {
            var texture = Own(new Texture2D(data.Width, data.Height, TextureFormat.RGBA32, true, data.ChannelFormat != "rgba8-srgb") { name = label, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 });
            for (int mip = 0; mip < data.MipBytes.Count; mip++)
            {
                var source = data.MipBytes[mip]; byte[] bytes = new byte[source.Count]; for (int i = 0; i < bytes.Length; i++) bytes[i] = source[i];
                TextureBytes += bytes.Length; texture.SetPixelData(bytes, mip);
            }
            texture.Apply(false, true); return texture;
        }
        private Mesh Upload(MeshData data, string label)
        {
            var mesh = Own(new Mesh { name = label, indexFormat = data.Positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 });
            mesh.vertices = data.Positions.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
            mesh.normals = data.Normals.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
            mesh.tangents = data.Tangents.Select(p => new Vector4(p.X, p.Y, p.Z, p.W)).ToArray();
            mesh.uv = data.Uv.Select(p => new Vector2(p.X, p.Y)).ToArray(); mesh.subMeshCount = data.Submeshes.Count;
            for (int part = 0; part < data.Submeshes.Count; part++)
            {
                var sub = data.Submeshes[part]; int[] indices = new int[sub.IndexCount];
                for (int i = 0; i < indices.Length; i++) indices[i] = (int)data.Indices[(int)sub.FirstIndex + i];
                mesh.SetIndices(indices, MeshTopology.Triangles, part);
            }
            mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            Triangles += data.Indices.Count / 3; MeshBytes += data.Positions.Count * 48L + data.Indices.Count * 4L;
            return mesh;
        }
        private GameObject Place(string label, Mesh mesh, Material[] materials, Vector3 position, Vector3 scale, Transform parent = null)
        {
            var item = new GameObject(label) { layer = 11 }; item.transform.SetParent(parent == null ? transform : parent, false);
            item.transform.localPosition = position; item.transform.localScale = scale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            // Primitive render seams are submeshes; material slots refer to semantic family mappings.
            if (materials.Length != mesh.subMeshCount && mesh.subMeshCount > 1)
            {
                var byPart = new Material[mesh.subMeshCount]; for (int i = 0; i < byPart.Length; i++) byPart[i] = materials[Math.Min(i, materials.Length - 1)]; renderer.sharedMaterials = byPart;
            }
            else renderer.sharedMaterials = materials;
            return item;
        }
        private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
        private static void DestroyObject(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy()
        {
            reflectionProbe?.Dispose(); reflectionProbe = null;
            sky?.Dispose(); sky = null;
            foreach (var value in owned) if (value != null) DestroyObject(value); owned.Clear();
        }
    }
}
