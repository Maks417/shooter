#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests
{
    public sealed class ProductSurfaceMaterialTests
    {
        [Test]
        public void MaskConversionPreservesSpatialMetallicAndOcclusionAndInvertsRoughness()
        {
            byte[] source = { 7, 231, 0, 255, 204, 36, 255, 255, 60, 128, 90, 255 };
            byte[] converted = ProductSurfaceMaterial.ConvertMaskMip(source);
            Assert.That(converted, Is.EqualTo(new byte[] { 7, 231, 0, 255, 204, 36, 0, 0, 60, 128, 0, 165 }));
            Assert.That(source[6], Is.EqualTo(255), "Upload conversion cannot mutate verified source bytes.");
            Assert.Throws<ArgumentException>(() => ProductSurfaceMaterial.ConvertMaskMip(new byte[3]));
        }

        [Test]
        public void LitMaterialBindsSpatialPlanesWithCompleteMipsAndSharedMaskUpload()
        {
            var owned = new List<Object>();
            try
            {
                var albedo = Plane("rgba8-srgb", new byte[] { 140, 130, 120, 255 });
                var normal = Plane("rgba8-linear", new byte[] { 153, 102, 250, 255 });
                var mask = Plane("rgba8-linear", new byte[] { 35, 202, 176, 255 });
                Material material = ProductSurfaceMaterial.Create(Shader.Find("Universal Render Pipeline/Lit"),
                    albedo, normal, mask, "Product surface test", 8f, value => owned.Add(value));
                Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                Assert.That(material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True);
                Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
                Assert.That(material.GetTexture("_MetallicGlossMap"), Is.SameAs(material.GetTexture("_OcclusionMap")));
                Assert.That(((Texture2D)material.GetTexture("_MetallicGlossMap")).mipmapCount, Is.EqualTo(2));
                Assert.That(((Texture2D)material.GetTexture("_BumpMap")).isDataSRGB, Is.False);
                Assert.That(((Texture2D)material.GetTexture("_BaseMap")).isDataSRGB, Is.True);
                Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(8, 8)));
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f), "Do not attenuate authored roughness with a first-texel scalar.");
                Assert.That(owned.Count, Is.EqualTo(4), "One material plus three planes; no duplicate mask texture.");
            }
            finally { foreach (Object value in owned) if (value != null) Object.DestroyImmediate(value); }
        }

        [Test]
        public void InvalidCompanionCannotSilentlyBecomeFlatMaterial()
        {
            var owned = new List<Object>();
            try
            {
                var albedo = Plane("rgba8-srgb", new byte[] { 140, 130, 120, 255 });
                var invalidNormal = Plane("rgba8-linear", new byte[] { 128, 128, 255, 128 });
                Assert.Throws<InvalidOperationException>(() => ProductSurfaceMaterial.Create(
                    Shader.Find("Universal Render Pipeline/Lit"), albedo, invalidNormal, null, "Invalid surface", 1,
                    value => owned.Add(value)));
                var wrongSize = new MaterialData(1, 1, "rgba8-linear", new[] { new byte[] { 0, 255, 200, 255 } });
                Assert.Throws<InvalidOperationException>(() => ProductSurfaceMaterial.Create(
                    Shader.Find("Universal Render Pipeline/Lit"), albedo, null, wrongSize, "Invalid mask", 1,
                    value => owned.Add(value)));
            }
            finally { foreach (Object value in owned) if (value != null) Object.DestroyImmediate(value); }
        }

        [Test]
        public void ReinstallAndClearReleasePreviousMaterialTextureAndMeshResources()
        {
            var hash = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("product-surface-lifecycle"));
            var visuals = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", hash));
            var boxes = new[] { new CollisionBox(1001, 2001, 1, new Float3(0, 1, 0), new Float3(2, 2, 2), true) };
            var root = new GameObject("Product surface lifetime");
            try
            {
                ProductMapView view = ProductMapView.Create(root.transform);
                view.Install(boxes, null, "training-compound", visuals);
                Assert.That(view.transform.Find("Product map solids").GetComponent<MeshRenderer>()
                    .sharedMaterial.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(.5f, .5f)),
                    "Metric map UVs must retain the numeric two-metre material scale.");
                Material previous = view.transform.Find("Product ground").GetComponent<MeshRenderer>().sharedMaterial;
                Texture previousTexture = previous.GetTexture("_BaseMap");
                Mesh previousMesh = view.transform.Find("Product ground").GetComponent<MeshFilter>().sharedMesh;
                view.Install(boxes, null, "training-compound", visuals);
                Assert.That(previous == null && previousTexture == null && previousMesh == null, Is.True,
                    "Replacing a map must release its prior GPU resources.");
                Material current = view.transform.Find("Product ground").GetComponent<MeshRenderer>().sharedMaterial;
                Texture currentTexture = current.GetTexture("_BaseMap");
                view.ClearVisuals();
                Assert.That(view.VisualsBound, Is.False);
                Assert.That(current == null && currentTexture == null, Is.True);
                Assert.That(view.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static MaterialData Plane(string format, byte[] pixel)
        {
            var first = new byte[16];
            for (int i = 0; i < 4; i++) Array.Copy(pixel, 0, first, i * 4, 4);
            return new MaterialData(2, 2, format, new[] { first, pixel });
        }
    }
}
