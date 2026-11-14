#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Rendering
{
    /// <summary>
    /// Read-only product map surfaces from verified packaged collision/cover metadata.
    /// Adds MeshRenderers only; never installs colliders or mutates authority.
    /// </summary>
    public sealed class ProductMapView : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<GameObject> solids = new List<GameObject>();
        private Mesh? unitBox;
        private Material? concrete;
        private Material? cover;
        private Material? ground;
        private Material? asphalt;
        private GeneratedSky? sky;
        private GeneratedReflectionProbe? probe;
        private string mapId = "";
        private int solidCount;
        private int coverCount;

        public string MapId => mapId;
        public int SolidCount => solidCount;
        public int CoverCount => coverCount;
        public bool IsInstalled => solidCount > 0;

        public static ProductMapView Create(Transform parent)
        {
            var root = new GameObject("Product map view").AddComponent<ProductMapView>();
            root.transform.SetParent(parent, false);
            root.InitializeShared();
            return root;
        }

        private void InitializeShared()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Required locked URP Lit shader missing; no art fallback.");
            unitBox = Own(FoundationView.CreateUnitBox());
            concrete = Own(Solid(shader, "Numeric concrete", new Color(0.42f, 0.44f, 0.41f), 0f, 0.18f));
            cover = Own(Solid(shader, "Numeric cover", new Color(0.22f, 0.24f, 0.20f), 0.05f, 0.28f));
            ground = Own(Solid(shader, "Numeric soil ground", new Color(0.18f, 0.14f, 0.08f), 0f, 0.08f));
            asphalt = Own(Solid(shader, "Numeric asphalt", new Color(0.08f, 0.09f, 0.10f), 0f, 0.12f));
        }

        public void Install(IReadOnlyList<CollisionBox> boxes, byte[]? metadataBytes, string installedMapId)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (boxes.Count == 0)
                throw new InvalidOperationException("Verified map visuals require packaged solids; refusing empty/diagnostic fallback.");
            ClearVisuals();
            mapId = installedMapId ?? "";

            HashSet<ulong> coverIds = ParseCoverIds(metadataBytes);
            float half = CompoundPlan.MapHalfExtent;
            Place("Product ground", new Vector3(0f, -0.05f, 0f), new Vector3(half * 2f, 0.1f, half * 2f), ground!);

            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                bool isCover = coverIds.Contains(box.StableId) || LooksLikeCover(box);
                Material mat = isCover ? cover! : MaterialForTag(box.SurfaceTag);
                Place(
                    (isCover ? "Product cover " : "Product solid ") + box.StableId.ToString(CultureInfo.InvariantCulture),
                    new Vector3(box.Center.X, box.Center.Y, box.Center.Z),
                    new Vector3(box.Size.X, box.Size.Y, box.Size.Z),
                    mat);
                solidCount++;
                if (isCover) coverCount++;
            }

            // Explicit cover proxies from metadata (may duplicate collision tinting; still readable).
            if (metadataBytes != null && metadataBytes.Length > 0)
                PlaceMetadataCovers(metadataBytes, coverIds);

            sky = GeneratedSky.Apply(transform);
            probe = GeneratedReflectionProbe.Apply(transform);
            Debug.Log("SHOOTER_PRODUCT_MAP_VIEW map=" + mapId + " solids=" + solidCount + " covers=" + coverCount);
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
                        cover!);
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
            // Cover proxies are typically waist-high blocks distinct from thin wall slabs.
            return box.Size.Y <= 1.6f && box.Size.Y >= 0.8f
                && box.Size.X <= 4f && box.Size.Z <= 4f
                && box.Size.X >= 0.6f && box.Size.Z >= 0.6f;
        }

        private Material MaterialForTag(ushort tag)
        {
            // Planner tags: 1 = structure/prop, 2 = low obstacles (depot). Numeric only.
            if (tag == 2) return asphalt!;
            return concrete!;
        }

        private void Place(string label, Vector3 center, Vector3 size, Material material)
        {
            var item = new GameObject(label) { layer = 11 };
            item.transform.SetParent(transform, false);
            item.transform.position = center;
            item.transform.localScale = size;
            item.AddComponent<MeshFilter>().sharedMesh = unitBox;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            solids.Add(item);
        }

        private static Material Solid(Shader shader, string label, Color color, float metallic, float smoothness)
        {
            var material = new Material(shader) { name = label };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            return material;
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
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null) DestroyObject(owned[i]);
            owned.Clear();
        }
    }
}
