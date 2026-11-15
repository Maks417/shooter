#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Rendering
{
    /// <summary>
    /// Read-only product map surfaces from verified packaged visual payloads + cover metadata.
    /// Adds MeshRenderers only; never installs colliders, mutates authority, or regenerates geometry.
    /// </summary>
    public sealed class ProductMapView : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<GameObject> solids = new List<GameObject>();
        private Shader? litShader;
        private Mesh? unitBox;
        private Mesh? mapSolids;
        private Material? concrete;
        private Material? cover;
        private Material? ground;
        private Material? asphalt;
        private GeneratedSky? sky;
        private GeneratedReflectionProbe? probe;
        private string mapId = "";
        private int solidCount;
        private int coverCount;
        private bool visualsBound;

        public string MapId => mapId;
        public int SolidCount => solidCount;
        public int CoverCount => coverCount;
        public bool IsInstalled => solidCount > 0;
        public bool VisualsBound => visualsBound;

        public static ProductMapView Create(Transform parent)
        {
            var root = new GameObject("Product map view").AddComponent<ProductMapView>();
            root.transform.SetParent(parent, false);
            root.InitializeShared();
            return root;
        }

        private void InitializeShared()
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null || !litShader.isSupported)
                throw new InvalidOperationException("Required locked URP Lit shader missing; no art fallback.");
        }

        public void Install(IReadOnlyList<CollisionBox> boxes, byte[]? metadataBytes, string installedMapId, PackagedVisualPayloads visuals)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (visuals == null) throw new ArgumentNullException(nameof(visuals));
            if (boxes.Count == 0)
                throw new InvalidOperationException("Verified map visuals require packaged solids; refusing empty/diagnostic fallback.");
            if (string.IsNullOrEmpty(visuals.MapId) || visuals.MapId == "unknown")
                throw new InvalidOperationException("Packaged visual map identity missing.");
            if (!string.IsNullOrEmpty(installedMapId) && visuals.MapId != installedMapId)
                throw new InvalidOperationException("Visual package mapId mismatch.");

            ClearVisuals();
            BindVisuals(visuals);
            mapId = installedMapId ?? visuals.MapId;

            HashSet<ulong> coverIds = ParseCoverIds(metadataBytes);
            float half = CompoundPlan.MapHalfExtent;
            // Collision-faithful ground plane: packaged soil/asphalt (depot), not flat tint. Prefer packaged materials.
            Material groundSurface = PreferDepotYardAsphalt(mapId) ? asphalt! : ground!;
            Place("Product ground", new Vector3(0f, -0.05f, 0f), new Vector3(half * 2f, 0.1f, half * 2f), groundSurface, unitBox!);

            // Packaged combined solids mesh already encodes gameplay boxes; one renderer, identity transform.
            var solidsGo = new GameObject("Product map solids") { layer = 11 };
            solidsGo.transform.SetParent(transform, false);
            solidsGo.AddComponent<MeshFilter>().sharedMesh = mapSolids;
            solidsGo.AddComponent<MeshRenderer>().sharedMaterial = concrete;
            solids.Add(solidsGo);
            solidCount = boxes.Count;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (coverIds.Contains(boxes[i].StableId) || LooksLikeCover(boxes[i]))
                    coverCount++;
            }

            if (metadataBytes != null && metadataBytes.Length > 0)
                PlaceMetadataCovers(metadataBytes, coverIds);

            GeneratedSkyMath.ResolveProductPalette(mapId, out var zenith, out var horizon, out var groundRgb, out float fogStart, out float fogEnd, out float fogDensity);
            sky = GeneratedSky.Apply(transform, zenith, horizon, groundRgb, true, fogStart, fogEnd, fogDensity);
            probe = GeneratedReflectionProbe.Apply(
                transform,
                GeneratedReflectionProbe.ProductMapBoxSizeMeters,
                GeneratedReflectionProbe.ProductMapIntensity,
                GeneratedReflectionProbe.DefaultResolution);
            Debug.Log("SHOOTER_PRODUCT_MAP_VIEW map=" + mapId + " solids=" + solidCount + " covers=" + coverCount + " packaged=1");
        }

        private void BindVisuals(PackagedVisualPayloads visuals)
        {
            ReleaseBoundVisuals();
            try
            {
                var shaderRoot = MapJsonValue.Parse(visuals.Bytes(MapVisualClosure.ShaderPath(visuals.Profile))).Object;
                if (shaderRoot["unityShader"].String != "Universal Render Pipeline/Lit" ||
                    shaderRoot["shaderException"].String != "procedural-urp-lit-v1")
                    throw new InvalidOperationException("Packaged shader provenance rejected.");

                mapSolids = Own(UploadMesh(MapVisualClosure.DecodeMapMesh(visuals, MapVisualClosure.MapSolidsMeshPath), "Product map solids"));
                unitBox = Own(UploadMesh(MapVisualClosure.DecodeMapMesh(visuals, MapVisualClosure.UnitBoxMeshPath), "Product unit box"));
                // Tile repeats: solids/cover use modest face density (shared mat across box sizes);
                // ground/asphalt target ~2 m tiles on the 256 m product ground plane (UV 0..1).
                concrete = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[0], "Numeric concrete", 0f, 0.18f, 4f);
                cover = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[1], "Numeric cover", 0.05f, 0.28f, 4f);
                ground = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[2], "Numeric soil ground", 0f, 0.08f, CompoundPlan.MapHalfExtent);
                asphalt = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[3], "Numeric asphalt", 0f, 0.12f, CompoundPlan.MapHalfExtent);
                visualsBound = true;
            }
            catch (Exception error)
            {
                ReleaseBoundVisuals();
                throw new InvalidOperationException("Required packaged map visuals missing/corrupt; refusing generation fallback: " + error.Message, error);
            }
        }

        public void ClearVisuals()
        {
            for (int i = 0; i < solids.Count; i++)
                if (solids[i] != null) DestroyObject(solids[i]);
            solids.Clear();
            solidCount = 0;
            coverCount = 0;
            mapId = "";
            probe?.Dispose();
            probe = null;
            sky?.Dispose();
            sky = null;
            ReleaseBoundVisuals();
        }

        private void PlaceMetadataCovers(byte[] metadataBytes, HashSet<ulong> already)
        {
            try
            {
                var root = MapJsonValue.Parse(metadataBytes).Object;
                if (!root.ContainsKey("covers")) return;
                var covers = root["covers"].Array;
                for (int i = 0; i < covers.Count; i++)
                {
                    var c = covers[i].Object;
                    ulong id = ulong.Parse(c["stableId"].String, CultureInfo.InvariantCulture);
                    if (already.Contains(id)) continue;
                    var center = c["centerMeters"].Array;
                    var size = c["sizeMeters"].Array;
                    Place(
                        "Product cover-meta " + id.ToString(CultureInfo.InvariantCulture),
                        new Vector3((float)center[0].Number, (float)center[1].Number, (float)center[2].Number),
                        new Vector3((float)size[0].Number, (float)size[1].Number, (float)size[2].Number),
                        cover!,
                        unitBox!);
                    solidCount++;
                    coverCount++;
                }
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Map cover metadata unreadable for product visuals: " + error.Message);
            }
        }

        private static HashSet<ulong> ParseCoverIds(byte[]? metadataBytes)
        {
            var ids = new HashSet<ulong>();
            if (metadataBytes == null || metadataBytes.Length == 0) return ids;
            try
            {
                var root = MapJsonValue.Parse(metadataBytes).Object;
                if (!root.ContainsKey("covers")) return ids;
                var covers = root["covers"].Array;
                for (int i = 0; i < covers.Count; i++)
                    ids.Add(ulong.Parse(covers[i].Object["stableId"].String, CultureInfo.InvariantCulture));
            }
            catch
            {
                // Caller Install will fail loudly on cover placement if metadata is required-corrupt.
            }
            return ids;
        }

        private static bool LooksLikeCover(CollisionBox box)
        {
            return box.Size.Y <= 1.6f && box.Size.Y >= 0.8f
                && box.Size.X <= 4f && box.Size.Z <= 4f
                && box.Size.X >= 0.6f && box.Size.Z >= 0.6f;
        }

        private void Place(string label, Vector3 center, Vector3 size, Material material, Mesh mesh)
        {
            var item = new GameObject(label) { layer = 11 };
            item.transform.SetParent(transform, false);
            item.transform.position = center;
            item.transform.localScale = size;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            solids.Add(item);
        }

        private Material FromPackaged(
            PackagedVisualPayloads visuals,
            string path,
            string label,
            float metallic,
            float smoothness,
            float tileRepeats)
        {
            var albedo = MapVisualClosure.DecodeMapMaterial(visuals, path);
            MaterialData? normal = DecodeCompanionPlane(visuals, path, "normal");
            MaterialData? mask = DecodeCompanionPlane(visuals, path, "mask");
            return ProductSurfaceMaterial.Create(litShader!, albedo, normal, mask, label, tileRepeats,
                value => owned.Add(value), metallic, smoothness);
        }

        private static bool PreferDepotYardAsphalt(string installedMapId) =>
            string.Equals(installedMapId, "depot-yard", StringComparison.Ordinal);

        private static MaterialData? DecodeCompanionPlane(PackagedVisualPayloads visuals, string albedoPath, string plane)
        {
            string companion = CompanionPlanePath(albedoPath, plane);
            // Older one-texel packages may omit companions. A present but invalid plane
            // is a package failure, never permission to silently substitute a flat material.
            return visuals.Has(companion) ? MapVisualClosure.DecodeMapMaterial(visuals, companion) : null;
        }

        private static string CompanionPlanePath(string albedoPath, string plane)
        {
            const string suffix = ".bin";
            if (albedoPath.EndsWith(suffix, StringComparison.Ordinal))
                return albedoPath.Substring(0, albedoPath.Length - suffix.Length) + "-" + plane + suffix;
            return albedoPath + "-" + plane;
        }

        private Mesh UploadMesh(MeshData data, string label)
        {
            var mesh = new Mesh
            {
                name = label,
                indexFormat = data.Positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = data.Positions.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
            mesh.normals = data.Normals.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
            mesh.tangents = data.Tangents.Select(t => new Vector4(t.X, t.Y, t.Z, t.W)).ToArray();
            mesh.uv = data.Uv.Select(t => new Vector2(t.X, t.Y)).ToArray();
            mesh.subMeshCount = data.Submeshes.Count;
            for (int i = 0; i < data.Submeshes.Count; i++)
            {
                var sub = data.Submeshes[i];
                mesh.SetIndices(
                    data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v => (int)v).ToArray(),
                    MeshTopology.Triangles,
                    i);
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private void ReleaseBoundVisuals()
        {
            visualsBound = false;
            mapSolids = null;
            unitBox = null;
            concrete = null;
            cover = null;
            ground = null;
            asphalt = null;
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null) DestroyObject(owned[i]);
            owned.Clear();
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            ClearVisuals();
        }
    }
}
