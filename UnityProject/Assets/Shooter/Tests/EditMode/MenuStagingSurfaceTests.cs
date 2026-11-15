#nullable enable
using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Session;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests
{
    public sealed class MenuStagingSurfaceTests
    {
        private PackagedVisualPayloads[] visuals = null!;

        [OneTimeSetUp]
        public void BuildNumericFixtures()
        {
            var hash = ContentIdentity.Hash(Encoding.ASCII.GetBytes("menu-staging-surface-tests"));
            visuals = new[]
            {
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", hash)),
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("depot-yard", "low", hash))
            };
        }

        [Test]
        public void StageSharesPackagedPlanesAndPreservesRenderIsolation()
        {
            var root = new GameObject("MenuStagingSurfaceTests");
            var previous = RenderTexture.active;
            bool fog = RenderSettings.fog;
            Color fogColor = RenderSettings.fogColor;
            var ambient = RenderSettings.ambientMode;
            try
            {
                var scene = root.AddComponent<ProductMenuScene>();
                scene.BindVerified(visuals);
                var concrete = root.transform.Find("Skyline shell").GetComponent<MeshRenderer>().sharedMaterial;
                var warm = root.transform.Find("Hangar volume").GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(concrete.GetTexture("_BaseMap"), Is.Not.Null);
                Assert.That(warm.GetTexture("_BaseMap"), Is.SameAs(concrete.GetTexture("_BaseMap")),
                    "Tint variants share uploaded planes instead of duplicating texture residency.");
                var asphalt = root.transform.Find("Apron slab").GetComponent<MeshRenderer>().sharedMaterial;
                var apronTint = root.transform.Find("Apron seam").GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(asphalt.GetTexture("_BaseMap"), Is.Not.Null);
                Assert.That(apronTint.GetTexture("_BaseMap"), Is.SameAs(asphalt.GetTexture("_BaseMap")),
                    "Apron variants reuse packaged asphalt planes.");
                foreach (string map in new[] { "training-compound", "depot-yard" })
                {
                    var card = root.transform.Find("Verified map card " + map);
                    Assert.That(card.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                    var mesh = card.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(card.GetComponent<MeshRenderer>().sharedMaterials.Length, Is.EqualTo(mesh.subMeshCount),
                        "Every packaged material role must survive into the operation overview.");
                    var camera = root.GetComponentsInChildren<Camera>().Single(c => c.targetTexture == scene.MapCard(map));
                    Bounds bounds = mesh.bounds;
                    float occupancy = 0f;
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                            for (int z = -1; z <= 1; z += 2)
                            {
                                Vector3 corner = card.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                                Vector3 screen = camera.WorldToViewportPoint(corner);
                                Assert.That(screen.x, Is.InRange(.04f, .96f));
                                Assert.That(screen.y, Is.InRange(.04f, .96f));
                                Assert.That(screen.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane));
                                occupancy = Mathf.Max(occupancy, Mathf.Abs(screen.x - .5f), Mathf.Abs(screen.y - .5f));
                            }
                    Assert.That(occupancy, Is.GreaterThan(.40f), "Compact maps must fill the card, not disappear on a historical-size plate.");
                    Assert.That(root.transform.Find("Map card ground " + map), Is.Not.Null);
                    Assert.That(root.transform.Find("Map card plate " + map), Is.Not.Null);
                    Assert.That(root.transform.Find("Map card rim N " + map), Is.Not.Null);
                    Assert.That(scene.MapCard(map), Is.Not.Null);
                }
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                    Assert.That(filter.sharedMesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.Tangent), Is.EqualTo(4),
                        "Packaged tangent frames must reach the normal-mapped stage.");
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.LessThan(200));
                Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.Zero);
                Assert.That(root.GetComponentsInChildren<Rigidbody>().Length, Is.Zero);
                Assert.That(root.GetComponentsInChildren<Transform>().All(t => t.gameObject.layer == ProductMenuScene.StageLayer), Is.True);
                Assert.That(root.GetComponentsInChildren<Camera>().All(c => !c.enabled && c.cullingMask == 1 << ProductMenuScene.StageLayer), Is.True);
                Assert.That(RenderTexture.active, Is.SameAs(previous));
                Assert.That(RenderSettings.fog, Is.EqualTo(fog));
                Assert.That(RenderSettings.fogColor, Is.EqualTo(fogColor));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(ambient));
                // The shell suspends this child during loading/gameplay; all its lights/renderers
                // inherit suspension and no standalone camera can render into the gameplay view.
                root.SetActive(false);
                Assert.That(root.GetComponentsInChildren<Light>(true).All(l => !l.isActiveAndEnabled), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DepthBandsAtmosphereAndReducedMotionPolicy()
        {
            var root = new GameObject("MenuStagingDepthTests");
            try
            {
                var scene = root.AddComponent<ProductMenuScene>();
                scene.BindVerified(visuals);
                Assert.That(MenuStagingComposition.InApronBand(root.transform.Find("Apron slab").localPosition.z), Is.True);
                Assert.That(MenuStagingComposition.InSoldierBand(root.transform.Find("Hero pad A").localPosition.z), Is.True);
                Assert.That(MenuStagingComposition.InSoldierBand(root.transform.Find("Hero pad C").localPosition.z), Is.True);
                Assert.That(MenuStagingComposition.InDistantBand(root.transform.Find("Skyline shell").localPosition.z), Is.True);
                Assert.That(root.transform.Find("Distant haze shell"), Is.Not.Null);
                Assert.That(root.transform.Find("Warm key light"), Is.Not.Null);
                Assert.That(root.transform.Find("Distant shell wash"), Is.Not.Null);
                Assert.That(MenuStagingComposition.FogDensity, Is.GreaterThan(.02f));
                Assert.That(MenuStagingComposition.Lights.Length, Is.GreaterThanOrEqualTo(6));

                var dust = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "Mesh dust").ToArray();
                Assert.That(dust.Length, Is.GreaterThan(0));
                Assert.That(dust.All(t => t.gameObject.activeSelf), Is.True);
                scene.SetReducedMotion(true);
                Assert.That(dust.All(t => !t.gameObject.activeSelf), Is.True,
                    "Reduced motion must disable cosmetic dust without tearing down the stage.");
                scene.SetReducedMotion(false);
                Assert.That(dust.All(t => t.gameObject.activeSelf), Is.True);
                Assert.That(scene.IsReady, Is.True);
                Assert.That(scene.ShowcaseActorCount, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FailedLateDependencyReleasesPartialStageAndAllowsRepair()
        {
            var root = new GameObject("MenuStagingRepairTests");
            byte[] broken = visuals[0].Bytes(MapVisualClosure.WeaponMaterialPaths[0]);
            byte original = broken[0];
            try
            {
                var scene = root.AddComponent<ProductMenuScene>();
                broken[0] ^= 0xff;
                Assert.Catch<Exception>(() => scene.BindVerified(visuals));
                Assert.That(scene.IsReady, Is.False);
                Assert.That(scene.Frame, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                broken[0] = original;
                scene.BindVerified(visuals);
                Assert.That(scene.IsReady, Is.True);
                Assert.That(scene.ShowcaseActorCount, Is.EqualTo(3));
                // Normal MonoBehaviour destruction callbacks require PlayMode. Automatic GPU
                // teardown is covered by MenuStagingLifetimeTests after actual activation.
            }
            finally
            {
                broken[0] = original;
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
