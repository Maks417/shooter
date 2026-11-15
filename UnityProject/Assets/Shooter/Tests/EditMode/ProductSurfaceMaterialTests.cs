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
        public void NonuniformBoxRetainsTwoMetreTexelScaleOnEveryFaceWithoutChangingSource()
        {
            var source = new Mesh();
            Mesh? result = null;
            try
            {
                // One UV edge on each of the XY, XZ and YZ faces.
                source.vertices = new Vector3[6];
                source.normals = new[] { Vector3.forward, Vector3.forward, Vector3.up, Vector3.up, Vector3.right, Vector3.right };
                source.tangents = new[] { new Vector4(1,0,0,1), new Vector4(1,0,0,1), new Vector4(1,0,0,-1), new Vector4(1,0,0,-1), new Vector4(0,1,0,1), new Vector4(0,1,0,1) };
                source.uv = new[] { Vector2.zero, Vector2.one, Vector2.zero, Vector2.one, Vector2.zero, Vector2.one };
                result = ProductSurfaceMaterial.MetricBoxUv(source, new Vector3(12, 3, 8), _ => { });
                Assert.That(result.uv[1], Is.EqualTo(new Vector2(12, 3)));
                Assert.That(result.uv[3], Is.EqualTo(new Vector2(12, 8)));
                Assert.That(result.uv[5], Is.EqualTo(new Vector2(3, 8)));
                Assert.That(source.uv[1], Is.EqualTo(Vector2.one));
                Assert.That(result.vertices, Is.EqualTo(source.vertices));
                Assert.That(result.tangents, Is.EqualTo(source.tangents));
            }
            finally { Object.DestroyImmediate(result); Object.DestroyImmediate(source); }
        }

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
        public void SurfaceResponseProfilesDriveBaseColorBumpAndOcclusionWithoutMutatingMaskScalars()
        {
            var owned = new List<Object>();
            try
            {
                var albedo = Plane("rgba8-srgb", new byte[] { 140, 130, 120, 255 });
                var normal = Plane("rgba8-linear", new byte[] { 153, 102, 250, 255 });
                var mask = Plane("rgba8-linear", new byte[] { 35, 202, 176, 255 });
                Material concrete = ProductSurfaceMaterial.Create(Shader.Find("Universal Render Pipeline/Lit"),
                    albedo, normal, mask, "Concrete response", 2f, value => owned.Add(value), ProductSurfaceResponse.Concrete);
                Material cover = ProductSurfaceMaterial.Create(Shader.Find("Universal Render Pipeline/Lit"),
                    albedo, normal, mask, "Cover response", 2f, value => owned.Add(value), ProductSurfaceResponse.CoverMetal);
                Assert.That(concrete.GetColor("_BaseColor").b, Is.GreaterThan(cover.GetColor("_BaseColor").b - 0.01f),
                    "Concrete stays cooler/brighter than steel cover.");
                Assert.That(cover.GetColor("_BaseColor").r, Is.LessThan(concrete.GetColor("_BaseColor").r));
                Assert.That(concrete.GetFloat("_BumpScale"), Is.GreaterThan(cover.GetFloat("_BumpScale")));
                Assert.That(cover.GetFloat("_OcclusionStrength"), Is.LessThan(concrete.GetFloat("_OcclusionStrength")));
                Assert.That(concrete.GetFloat("_Smoothness"), Is.EqualTo(1f));
                Assert.That(cover.GetFloat("_Metallic"), Is.EqualTo(1f), "Mask path still drives metallic via map, not fallback scalar.");
                Assert.That(ProductSurfaceResponse.Apron.BaseColor.r, Is.LessThan(ProductSurfaceResponse.Asphalt.BaseColor.r),
                    "Apron ring must read darker than inner asphalt/yard pad.");
            }
            finally { foreach (Object value in owned) if (value != null) Object.DestroyImmediate(value); }
        }

        [TestCase("training-compound")]
        [TestCase("depot-yard")]
        public void ProductMapView_InstallsApronInnerYardAndInsetCoverOverlays(string mapId)
        {
            var hash = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("product-surface-apron-cover"));
            var visuals = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build(mapId, "low", hash));
            var boxes = new[]
            {
                new CollisionBox(1001, 2001, 1, new Float3(0, 1, 0), new Float3(2, 2, 2), true),
                new CollisionBox(1002, 2002, 2, new Float3(4, 0.6f, 0), new Float3(1.2f, 1.2f, 1.2f), true)
            };
            var root = new GameObject("Product surface apron cover");
            try
            {
                ProductMapView view = ProductMapView.Create(root.transform);
                view.Install(boxes, null, mapId, visuals);
                Assert.That(view.CoverCount, Is.EqualTo(1), "Heuristic cover box must count as metal overlay.");
                Transform apron = view.transform.Find("Product apron");
                Transform ground = view.transform.Find("Product ground");
                Assert.That(apron, Is.Not.Null);
                Assert.That(ground, Is.Not.Null);
                Assert.That(apron!.localScale.x, Is.GreaterThan(ground!.localScale.x));
                Assert.That(apron.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor").r,
                    Is.LessThan(ground.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor").r));
                Transform cover = view.transform.Find("Product cover 1002");
                Assert.That(cover, Is.Not.Null);
                Assert.That(cover!.localScale.x, Is.EqualTo(1.2f * ProductMapView.CoverHorizontalInset).Within(0.0001f));
                Assert.That(cover.localScale.y, Is.EqualTo(1.2f * ProductMapView.CoverVerticalInset).Within(0.0001f));
                Assert.That(cover.position.y, Is.EqualTo(0.6f + ProductMapView.CoverLiftMeters).Within(0.0001f));
                Assert.That(cover.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_BumpScale"),
                    Is.EqualTo(ProductSurfaceResponse.CoverMetal.BumpScale).Within(0.0001f));
                Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.Zero,
                    "Apron/cover overlays must stay presentation-only.");
                MeshRenderer solidsRenderer = view.transform.Find("Product map solids").GetComponent<MeshRenderer>();
                Material[] solidsMats = solidsRenderer.sharedMaterials;
                Assert.That(solidsMats.Length, Is.EqualTo(solidsRenderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount));
                if (mapId == "depot-yard")
                {
                    Assert.That(solidsMats.Length, Is.EqualTo(6));
                    Assert.That(solidsMats[3].GetTexture("_BaseMap"), Is.SameAs(solidsMats[1].GetTexture("_BaseMap")),
                        "Painted cargo variants share verified steel planes without extra texture residency.");
                    Assert.That(solidsMats[4].GetTexture("_BumpMap"), Is.SameAs(solidsMats[1].GetTexture("_BumpMap")));
                    Assert.That(solidsMats[3].GetColor("_BaseColor").b, Is.GreaterThan(solidsMats[3].GetColor("_BaseColor").r));
                    Assert.That(solidsMats[4].GetColor("_BaseColor").r, Is.GreaterThan(solidsMats[4].GetColor("_BaseColor").b));
                }
                Assert.That(solidsMats.Length, Is.GreaterThanOrEqualTo(2), "Packaged solids must expose multi-material submeshes.");
                Assert.That(solidsMats[0].GetColor("_BaseColor"), Is.EqualTo(ProductSurfaceResponse.Concrete.BaseColor));
                bool sawMetal = false, sawTrim = false;
                for (int i = 0; i < solidsMats.Length; i++)
                {
                    Color c = solidsMats[i].GetColor("_BaseColor");
                    if (Approximately(c, ProductSurfaceResponse.CoverMetal.BaseColor)) sawMetal = true;
                    if (Approximately(c, ProductSurfaceResponse.TrimPlaster.BaseColor)) sawTrim = true;
                }
                Assert.That(sawMetal, Is.True, "Metal cover submesh material missing on packaged solids.");
                Assert.That(sawTrim, Is.True, "Trim plaster submesh material missing on packaged solids.");
                view.ClearVisuals();
                Assert.That(view.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.0001f && Mathf.Abs(a.g - b.g) < 0.0001f &&
            Mathf.Abs(a.b - b.b) < 0.0001f && Mathf.Abs(a.a - b.a) < 0.0001f;

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
