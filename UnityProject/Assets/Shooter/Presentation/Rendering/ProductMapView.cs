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
        /// <summary>Presentation-only shrink so metal cover reads as crates instead of coplanar slabs.</summary>
        public const float CoverHorizontalInset = 0.94f;
        public const float CoverVerticalInset = 0.97f;
        /// <summary>Lift cover overlays a few centimetres to avoid z-fighting with baked solids.</summary>
        public const float CoverLiftMeters = 0.035f;
        /// <summary>Inner yard pad as a fraction of map half-extent (apron occupies the outer ring).</summary>
        public const float InnerYardExtentFraction = 0.78f;

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<GameObject> solids = new List<GameObject>();
        private Shader? litShader;
        private Shader? mapShader;
        private static readonly int AoVolumeId = Shader.PropertyToID("_ShooterAoVolume");
        private static readonly int AoVolumeMinId = Shader.PropertyToID("_ShooterAoVolumeMin");
        private static readonly int AoVolumeInvSizeId = Shader.PropertyToID("_ShooterAoVolumeInvSize");
        private Mesh? unitBox;
        private Mesh? mapSolids;
        private MeshData? solidsMeshData;
        private Material? concrete;
        private Material? trim;
        private Material? cover;
        private Material? ground;
        private Material? asphalt;
        private Material? apron;
        private Material[] architecturalAccents = Array.Empty<Material>();
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
            // ADR-081 map surfaces: URP Lit shading plus the baked ambient-occlusion volume.
            mapShader = Shader.Find("Shooter/MapSurface");
            if (mapShader == null || !mapShader.isSupported)
                throw new InvalidOperationException("Required Shooter/MapSurface shader missing; no art fallback.");
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
            float full = half * 2f;
            // Outer apron ring (darker asphalt response) + inner yard pad for lane readability.
            // Presentation-only unit boxes; no colliders.
            Place("Product apron", new Vector3(0f, -0.06f, 0f), new Vector3(full, 0.1f, full), apron!, unitBox!);
            float inner = full * InnerYardExtentFraction;
            Material yardSurface = PreferDepotYardAsphalt(mapId) ? asphalt! : ground!;
            Place("Product ground", new Vector3(0f, -0.02f, 0f), new Vector3(inner, 0.1f, inner), yardSurface, unitBox!);

            // Packaged combined solids mesh encodes gameplay + visual boxes with MaterialSlot roles
            // (concrete / metal cover / plaster trim). Discrete metal overlays still inset cover props.
            var solidsGo = new GameObject("Product map solids") { layer = 11 };
            solidsGo.transform.SetParent(transform, false);
            solidsGo.AddComponent<MeshFilter>().sharedMesh = mapSolids;
            var solidsRenderer = solidsGo.AddComponent<MeshRenderer>();
            AssignSolidsMaterials(solidsRenderer, solidsMeshData!, concrete!, cover!, trim!, architecturalAccents);
            solids.Add(solidsGo);
            solidCount = boxes.Count;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (coverIds.Contains(boxes[i].StableId) || LooksLikeCover(boxes[i]))
                    coverCount++;
            }

            // Place discrete metal cover props; skip set tracks overlays already placed (not coverIds).
            var placedMetalCovers = new HashSet<ulong>();
            if (metadataBytes != null && metadataBytes.Length > 0)
                PlaceMetadataCovers(metadataBytes, coverIds, placedMetalCovers);
            PlaceCoverOverlaysFromBoxes(boxes, coverIds, placedMetalCovers);

            // Atmosphere after materials/meshes so Trilight + one-shot probe sample the installed surfaces.
            GeneratedSkyMath.ResolveProductPalette(mapId, out var zenith, out var horizon, out var groundRgb, out float fogStart, out float fogEnd, out float fogDensity);
            sky = GeneratedSky.Apply(transform, zenith, horizon, groundRgb, true, fogStart, fogEnd, fogDensity,
                GeneratedSky.ProductAmbientIntensity);
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

                solidsMeshData = MapVisualClosure.DecodeMapMesh(visuals, MapVisualClosure.MapSolidsMeshPath);
                mapSolids = Own(UploadMesh(solidsMeshData, "Product map solids"));
                unitBox = Own(UploadMesh(MapVisualClosure.DecodeMapMesh(visuals, MapVisualClosure.UnitBoxMeshPath), "Product unit box", true));
                // Combined map solids retain metric UVs, so .5 repeats/m matches the two-metre recipe.
                // Scaled unit boxes receive metric UVs too: all families repeat every two metres.
                // Scalar metallic/smoothness only apply when packaged mask planes are absent; T075 masks own response.
                concrete = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[0], "Numeric concrete",
                    ProductSurfaceResponse.Concrete, .5f);
                // Trim reuses packaged concrete planes with a warmer plaster response (no extra material path).
                trim = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[0], "Numeric trim plaster",
                    ProductSurfaceResponse.TrimPlaster, .5f);
                cover = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[1], "Numeric cover",
                    ProductSurfaceResponse.CoverMetal, .5f);
                architecturalAccents = ProductSurfaceMaterial.ArchitecturalAccents(concrete, cover, value => owned.Add(value));
                ground = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[2], "Numeric soil ground",
                    ProductSurfaceResponse.Soil, .5f);
                asphalt = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[3], "Numeric asphalt",
                    ProductSurfaceResponse.Asphalt, .5f);
                // Apron reuses asphalt albedo/normal/mask planes with a darker BaseColor response.
                apron = FromPackaged(visuals, MapVisualClosure.MapMaterialPaths[3], "Numeric apron asphalt",
                    ProductSurfaceResponse.Apron, .5f);
                BindAmbientOcclusion(visuals);
                visualsBound = true;
            }
            catch (Exception error)
            {
                ReleaseBoundVisuals();
                throw new InvalidOperationException("Required packaged map visuals missing/corrupt; refusing generation fallback: " + error.Message, error);
            }
        }

        /// <summary>Upload the packaged ADR-081 volume as a Texture3D (R8, trilinear, clamped) and publish its transform.</summary>
        private void BindAmbientOcclusion(PackagedVisualPayloads visuals)
        {
            byte[] volume = MapVisualClosure.DecodeAmbientOcclusionVolume(visuals);
            var texture = new Texture3D(MapAmbientOcclusionVolume.SizeX, MapAmbientOcclusionVolume.SizeY, MapAmbientOcclusionVolume.SizeZ,
                TextureFormat.R8, false)
            {
                name = "Product baked AO volume " + visuals.MapId,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixelData(volume, 0);
            texture.Apply(false, true);
            owned.Add(texture);
            Shader.SetGlobalTexture(AoVolumeId, texture);
            Shader.SetGlobalVector(AoVolumeMinId, new Vector4(
                (float)MapAmbientOcclusionVolume.MinX, (float)MapAmbientOcclusionVolume.MinY, (float)MapAmbientOcclusionVolume.MinZ, 0f));
            Shader.SetGlobalVector(AoVolumeInvSizeId, new Vector4(
                (float)(1.0 / (MapAmbientOcclusionVolume.SizeX * MapAmbientOcclusionVolume.CellXZ)),
                (float)(1.0 / (MapAmbientOcclusionVolume.SizeY * MapAmbientOcclusionVolume.CellY)),
                (float)(1.0 / (MapAmbientOcclusionVolume.SizeZ * MapAmbientOcclusionVolume.CellXZ)), 0f));
            ProductPostProcessing.AoVolumeBound = true;
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

        private void PlaceMetadataCovers(byte[] metadataBytes, HashSet<ulong> countedCoverIds, HashSet<ulong> alreadyPlaced)
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
                    if (alreadyPlaced.Contains(id)) continue;
                    var center = c["centerMeters"].Array;
                    var size = c["sizeMeters"].Array;
                    PlaceCover(
                        "Product cover-meta " + id.ToString(CultureInfo.InvariantCulture),
                        new Vector3((float)center[0].Number, (float)center[1].Number, (float)center[2].Number),
                        new Vector3((float)size[0].Number, (float)size[1].Number, (float)size[2].Number));
                    alreadyPlaced.Add(id);
                    // Overlays do not change SolidCount. CoverCount for ids already tallied from boxes stays put.
                    if (!countedCoverIds.Contains(id))
                        coverCount++;
                }
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Map cover metadata unreadable for product visuals: " + error.Message);
            }
        }

        private void PlaceCoverOverlaysFromBoxes(
            IReadOnlyList<CollisionBox> boxes,
            HashSet<ulong> coverIds,
            HashSet<ulong> alreadyPlaced)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                CollisionBox box = boxes[i];
                if (!(coverIds.Contains(box.StableId) || LooksLikeCover(box))) continue;
                if (alreadyPlaced.Contains(box.StableId)) continue;
                PlaceCover(
                    "Product cover " + box.StableId.ToString(CultureInfo.InvariantCulture),
                    new Vector3(box.Center.X, box.Center.Y, box.Center.Z),
                    new Vector3(box.Size.X, box.Size.Y, box.Size.Z));
                alreadyPlaced.Add(box.StableId);
            }
        }

        private void PlaceCover(string label, Vector3 center, Vector3 size)
        {
            // Inset + lift only; collision/authority boxes stay untouched.
            var inset = new Vector3(
                size.x * CoverHorizontalInset,
                size.y * CoverVerticalInset,
                size.z * CoverHorizontalInset);
            var lifted = new Vector3(center.x, center.y + CoverLiftMeters, center.z);
            Place(label, lifted, inset, cover!, unitBox!);
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

        private static bool LooksLikeCover(CollisionBox box) => MapSolidsMesh.LooksLikeMetalCover(box);

        /// <summary>
        /// Maps each Unity submesh to concrete / metal / plaster using MeshData MaterialSlot roles.
        /// Submesh array order follows MeshAssembly's ascending slot sort; empty roles are omitted.
        /// </summary>
        public static void AssignSolidsMaterials(
            MeshRenderer renderer, MeshData meshData, Material concreteMaterial, Material coverMaterial, Material trimMaterial,
            params Material[] accents)
        {
            if (meshData.Submeshes.Count <= 1)
            {
                renderer.sharedMaterial = MaterialForSlot(meshData.Submeshes[0].MaterialSlot, concreteMaterial, coverMaterial, trimMaterial, accents);
                return;
            }
            var materials = new Material[meshData.Submeshes.Count];
            for (int i = 0; i < meshData.Submeshes.Count; i++)
                materials[i] = MaterialForSlot(meshData.Submeshes[i].MaterialSlot, concreteMaterial, coverMaterial, trimMaterial, accents);
            renderer.sharedMaterials = materials;
        }

        private static Material MaterialForSlot(
            ushort slot, Material concreteMaterial, Material coverMaterial, Material trimMaterial, Material[] accents)
        {
            if (slot == (ushort)MapSolidsMaterialRole.MetalCover) return coverMaterial;
            if (slot == (ushort)MapSolidsMaterialRole.Trim) return trimMaterial;
            if (slot == (ushort)MapSolidsMaterialRole.StructureConcrete) return concreteMaterial;
            int accent = slot - (ushort)MapSolidsMaterialRole.DepotBlue;
            if (accent >= 0 && accent < accents.Length) return accents[accent];
            throw new InvalidOperationException("Packaged architectural material role is unsupported: " + slot);
        }

        private void Place(string label, Vector3 center, Vector3 size, Material material, Mesh mesh)
        {
            var item = new GameObject(label) { layer = 11 };
            item.transform.SetParent(transform, false);
            item.transform.position = center;
            item.transform.localScale = size;
            item.AddComponent<MeshFilter>().sharedMesh = ProductSurfaceMaterial.MetricBoxUv(mesh, size, value => owned.Add(value));
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            solids.Add(item);
        }

        private Material FromPackaged(
            PackagedVisualPayloads visuals,
            string path,
            string label,
            ProductSurfaceResponse response,
            float tileRepeats)
        {
            var albedo = MapVisualClosure.DecodeMapMaterial(visuals, path);
            MaterialData? normal = DecodeCompanionPlane(visuals, path, "normal");
            MaterialData? mask = DecodeCompanionPlane(visuals, path, "mask");
            return ProductSurfaceMaterial.Create(mapShader!, albedo, normal, mask, label, tileRepeats,
                value => owned.Add(value), response);
        }

        private static bool PreferDepotYardAsphalt(string installedMapId) =>
            string.Equals(installedMapId, "depot-yard", StringComparison.Ordinal)
            || string.Equals(installedMapId, "oil-depot", StringComparison.Ordinal);

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

        private Mesh UploadMesh(MeshData data, string label, bool keepReadable = false)
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
            mesh.UploadMeshData(!keepReadable);
            return mesh;
        }

        private void ReleaseBoundVisuals()
        {
            if (ProductPostProcessing.AoVolumeBound) ProductPostProcessing.AoVolumeBound = false;
            visualsBound = false;
            mapSolids = null;
            solidsMeshData = null;
            unitBox = null;
            concrete = null;
            trim = null;
            cover = null;
            ground = null;
            asphalt = null;
            apron = null;
            architecturalAccents = Array.Empty<Material>();
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
