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
                anisoLevel = 2
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
