#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using UnityEngine;

namespace Shooter.Presentation.Rendering
{
    /// <summary>Loading-time adaptation of verified SGAF planes to the locked URP Lit channel layout.</summary>
    public static class ProductSurfaceMaterial
    {
        /// <summary>
        /// Numeric painted-steel / concrete variants share verified PBR planes and texture residency. ADR-116: each tint is the
        /// target paint colour divided by the source plane's mean linear albedo, so "tank white" reads white and "safety
        /// yellow" reads yellow whatever dark role the map's cover slot bakes (the old tints multiplied dark steel into teal
        /// and olive).
        /// </summary>
        public static Material[] ArchitecturalAccents(Material concrete, Material metal, Action<UnityEngine.Object> own)
        {
            Material Variant(Material source, string name, Color target)
            {
                var material = new Material(source) { name = name };
                own(material);
                material.SetColor("_BaseColor", AccentTint(target, MeanAlbedo(source)));
                return material;
            }
            return new[]
            {
                Variant(metal, "Weathered depot blue", AccentTargets[0]),
                Variant(metal, "Weathered oxide paint", AccentTargets[1]),
                Variant(concrete, "Concrete footing and joints", AccentTargets[2]),
                Variant(metal, "Weathered tank enamel", AccentTargets[3]),
                Variant(metal, "Safety yellow paint", AccentTargets[4])
            };
        }

        /// <summary>ADR-116 rendered linear albedo targets: depot blue, oxide red, footing grey, tank white, safety yellow.</summary>
        public static readonly Color[] AccentTargets =
        {
            new Color(.10f, .22f, .34f), new Color(.30f, .10f, .05f), new Color(.28f, .27f, .25f),
            new Color(.70f, .70f, .66f), new Color(.62f, .42f, .04f)
        };

        /// <summary>Tint that maps a plane's mean linear albedo onto <paramref name="target"/>, clamped to the validated 0..8 range.</summary>
        public static Color AccentTint(Color target, Color meanAlbedo)
        {
            float Channel(float t, float mean) => Mathf.Clamp(t / Mathf.Max(mean, .02f), 0f, 8f);
            return new Color(Channel(target.r, meanAlbedo.r), Channel(target.g, meanAlbedo.g), Channel(target.b, meanAlbedo.b), 1f);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Material, System.Runtime.CompilerServices.StrongBox<Color>> meanAlbedo =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Material, System.Runtime.CompilerServices.StrongBox<Color>>();

        /// <summary>Mean linear albedo recorded when <see cref="Create(Shader, MaterialData, MaterialData?, MaterialData?, string, float, Action{UnityEngine.Object}, ProductSurfaceResponse)"/> uploaded the plane; mid grey otherwise.</summary>
        public static Color MeanAlbedo(Material material) =>
            material != null && meanAlbedo.TryGetValue(material, out var box) ? box.Value : new Color(.3f, .3f, .3f);

        /// <summary>Linear mean of an sRGB albedo plane, read from its smallest mip (already the box-filtered average).</summary>
        public static Color MeanLinearAlbedo(MaterialData albedo)
        {
            var mip = albedo.MipBytes[albedo.MipBytes.Count - 1];
            double r = 0, g = 0, b = 0; int texels = mip.Count / 4;
            static double Linear(byte s) { double c = s / 255.0; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
            for (int i = 0; i + 3 < mip.Count; i += 4) { r += Linear(mip[i]); g += Linear(mip[i + 1]); b += Linear(mip[i + 2]); }
            texels = Math.Max(1, texels);
            return new Color((float)(r / texels), (float)(g / texels), (float)(b / texels), 1f);
        }

        /// <summary>Semantic palette shared by first-person, world and source diagnostics.</summary>
        public static Material[] WeaponFinishes(Material coated, Material polymer, Material steel, Material recess, Material enamel)
            => new[] { coated, polymer, steel, recess, enamel };

        /// <summary>
        /// ADR-102 palette: the five verified finishes plus slot 5 oiled wood and slot 6 polished gold. Both are numeric
        /// tints of the verified polymer and steel planes (no new textures), like <see cref="ArchitecturalAccents"/>.
        /// ADR-111 appends the knife and grenade finishes: slot 7 brushed blade steel, slot 8 polished edge grind, slot 9 olive
        /// grenade enamel (verified planes of their own) and slot 10 the yellow HE marking band, a tint of the enamel.
        /// </summary>
        public static Material[] WeaponFinishes(Material coated, Material polymer, Material steel, Material recess, Material enamel,
            Material blade, Material bladeEdge, Material grenadePaint, Action<UnityEngine.Object> own)
        {
            if (own == null) throw new ArgumentNullException(nameof(own));
            Material Tint(Material source, string name, Color tint, float smoothness)
            {
                var material = new Material(source) { name = name };
                own(material);
                material.SetColor("_BaseColor", tint);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
                return material;
            }
            // The polymer albedo is near-black, so the walnut tint is above one to lift it into warm brown.
            var wood = Tint(polymer, "Oiled walnut furniture", new Color(3.1f, 1.65f, .82f), .42f);
            var gold = Tint(steel, "Polished gold plating", new Color(1.95f, 1.45f, .52f), .82f);
            // The olive enamel albedo is dark green, so the band tint lifts red and suppresses blue into marking yellow.
            var band = Tint(grenadePaint, "Yellow HE marking band", new Color(7.2f, 4.0f, .7f), .45f);
            return new[] { coated, polymer, steel, recess, enamel, wood, gold, blade, bladeEdge, grenadePaint, band, SightGlass(own) };
        }

        /// <summary>ADR-115 slot 11: the clear, faintly coated reflex-sight pane (transparent, no texture planes).</summary>
        public static Material SightGlass(Action<UnityEngine.Object> own)
        {
            if (own == null) throw new ArgumentNullException(nameof(own));
            var shader = Shader.Find("Shooter/SightGlass") ?? throw new InvalidOperationException("Sight glass shader missing.");
            var material = new Material(shader) { name = "Clear reflex sight glass", renderQueue = 3000 };
            own(material);
            return material;
        }

        /// <summary>Retains verified box geometry; adapts face UVs to metres after nonuniform scaling.</summary>
        public static Mesh MetricBoxUv(Mesh source, Vector3 size, Action<UnityEngine.Object> own)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (own == null) throw new ArgumentNullException(nameof(own));
            if (!Finite(size.x) || !Finite(size.y) || !Finite(size.z) || size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));
            var uv = source.uv;
            var normals = source.normals;
            var tangents = source.tangents;
            if (uv.Length != source.vertexCount || normals.Length != uv.Length || tangents.Length != uv.Length)
                throw new InvalidOperationException("Verified box requires complete UV and tangent frames.");
            for (int i = 0; i < uv.Length; i++)
            {
                var u = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                var v = Vector3.Cross(normals[i], u);
                uv[i] = Vector2.Scale(uv[i], new Vector2(Vector3.Scale(u, size).magnitude, Vector3.Scale(v, size).magnitude));
            }
            var mesh = UnityEngine.Object.Instantiate(source);
            own(mesh);
            mesh.name = source.name + " metric UV";
            mesh.uv = uv;
            return mesh;
        }

        public static Material Create(Shader shader, MaterialData albedo, MaterialData? normal, MaterialData? mask,
            string label, float tileRepeats, Action<UnityEngine.Object> own, float metallic = 0f, float smoothness = .2f)
        {
            return Create(shader, albedo, normal, mask, label, tileRepeats, own,
                new ProductSurfaceResponse(Color.white, 1.05f, 1f, metallic, smoothness));
        }

        public static Material Create(Shader shader, MaterialData albedo, MaterialData? normal, MaterialData? mask,
            string label, float tileRepeats, Action<UnityEngine.Object> own, ProductSurfaceResponse response)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (own == null) throw new ArgumentNullException(nameof(own));
            if (!float.IsFinite(tileRepeats) || tileRepeats <= 0f) throw new ArgumentOutOfRangeException(nameof(tileRepeats));
            ValidateResponse(response);
            ValidatePlane(albedo, "rgba8-srgb");
            if (normal != null) ValidateCompanion(albedo, normal);
            if (mask != null) ValidateCompanion(albedo, mask);
            var material = new Material(shader) { name = label };
            own(material);
            material.SetColor("_BaseColor", response.BaseColor);
            material.SetTexture("_BaseMap", Upload(albedo, label + " albedo", false, own));
            meanAlbedo.AddOrUpdate(material, new System.Runtime.CompilerServices.StrongBox<Color>(MeanLinearAlbedo(albedo)));
            float repeats = albedo.Width <= 1 && albedo.Height <= 1 ? 1f : tileRepeats;
            material.SetTextureScale("_BaseMap", new Vector2(repeats, repeats));
            if (normal != null)
            {
                // The frozen normal plane is XYZ with A=255. URP's desktop RGorAG decoder
                // reads X=R*A and Y=G, so preserve XYZ and opaque alpha (also valid for RGB decoding).
                material.SetTexture("_BumpMap", Upload(normal, label + " normal", false, own, true));
                material.SetFloat("_BumpScale", response.BumpScale);
                material.EnableKeyword("_NORMALMAP");
            }
            if (mask != null)
            {
                Texture2D texture = Upload(mask, label + " metallic smoothness occlusion", true, own);
                // Lit samples metallic from R, smoothness from A, and occlusion from G.
                // Both properties share one linear upload; every mip retains spatial detail.
                material.SetTexture("_MetallicGlossMap", texture);
                material.SetTexture("_OcclusionMap", texture);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_OcclusionStrength", response.OcclusionStrength);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            else
            {
                material.SetFloat("_Metallic", response.MetallicFallback);
                material.SetFloat("_Smoothness", response.SmoothnessFallback);
            }
            return material;
        }

        /// <summary>SGAF R=metallic/G=occlusion/B=roughness becomes Lit R=metallic/G=occlusion/A=smoothness.</summary>
        public static byte[] ConvertMaskMip(IReadOnlyList<byte> source)
        {
            if (source == null || source.Count == 0 || source.Count % 4 != 0)
                throw new ArgumentException("A complete RGBA mask mip is required.", nameof(source));
            var bytes = new byte[source.Count];
            for (int i = 0; i < bytes.Length; i += 4)
            {
                bytes[i] = source[i];
                bytes[i + 1] = source[i + 1];
                bytes[i + 2] = 0;
                bytes[i + 3] = (byte)(255 - source[i + 2]);
            }
            return bytes;
        }

        private static void ValidateResponse(ProductSurfaceResponse response)
        {
            if (!Finite(response.BumpScale) || response.BumpScale <= 0f || response.BumpScale > 4f)
                throw new ArgumentOutOfRangeException(nameof(response), "BumpScale must be finite in (0,4].");
            if (!Finite(response.OcclusionStrength) || response.OcclusionStrength < 0f || response.OcclusionStrength > 1f)
                throw new ArgumentOutOfRangeException(nameof(response), "OcclusionStrength must be finite in [0,1].");
            if (!Finite(response.MetallicFallback) || response.MetallicFallback < 0f || response.MetallicFallback > 1f)
                throw new ArgumentOutOfRangeException(nameof(response), "MetallicFallback must be finite in [0,1].");
            if (!Finite(response.SmoothnessFallback) || response.SmoothnessFallback < 0f || response.SmoothnessFallback > 1f)
                throw new ArgumentOutOfRangeException(nameof(response), "SmoothnessFallback must be finite in [0,1].");
            Color c = response.BaseColor;
            if (!Finite(c.r) || !Finite(c.g) || !Finite(c.b) || !Finite(c.a) ||
                c.r < 0f || c.g < 0f || c.b < 0f || c.a < 0f ||
                c.r > 8f || c.g > 8f || c.b > 8f || c.a > 1f)
                throw new ArgumentOutOfRangeException(nameof(response), "BaseColor channels must be finite and in range.");
        }

        private static Texture2D Upload(MaterialData data, string label, bool mask, Action<UnityEngine.Object> own, bool normal = false)
        {
            var texture = new Texture2D(data.Width, data.Height, TextureFormat.RGBA32, data.MipBytes.Count, data.ChannelFormat != "rgba8-srgb")
            {
                name = label,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = ProductPostProcessing.CurrentTier.AnisoLevel // ADR-103
            };
            own(texture);
            for (int mip = 0; mip < data.MipBytes.Count; mip++)
            {
                var source = data.MipBytes[mip];
                byte[] bytes = mask ? ConvertMaskMip(source) : new byte[source.Count];
                if (!mask)
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = source[i];
                if (normal)
                    for (int i = 3; i < bytes.Length; i += 4)
                        if (bytes[i] != 255) throw new InvalidOperationException("Packaged XYZ normals require opaque alpha for URP decoding.");
                texture.SetPixelData(bytes, mip);
            }
            texture.Apply(false, true);
            return texture;
        }

        private static void ValidateCompanion(MaterialData albedo, MaterialData companion)
        {
            ValidatePlane(companion, "rgba8-linear");
            if (companion.Width != albedo.Width || companion.Height != albedo.Height || companion.MipBytes.Count != albedo.MipBytes.Count)
                throw new InvalidOperationException("Packaged material companion dimensions/mips do not match albedo.");
        }

        private static void ValidatePlane(MaterialData data, string format)
        {
            if (data == null || data.ChannelFormat != format)
                throw new InvalidOperationException("Packaged material channel format is incompatible with URP upload.");
            int width = data.Width, height = data.Height;
            for (int mip = 0; mip < data.MipBytes.Count; mip++)
            {
                if (data.MipBytes[mip].Count != width * height * 4)
                    throw new InvalidOperationException("Packaged material mip byte count is invalid.");
                if (mip + 1 < data.MipBytes.Count && width == 1 && height == 1)
                    throw new InvalidOperationException("Packaged material contains excess mip levels.");
                width = Math.Max(1, width / 2);
                height = Math.Max(1, height / 2);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
