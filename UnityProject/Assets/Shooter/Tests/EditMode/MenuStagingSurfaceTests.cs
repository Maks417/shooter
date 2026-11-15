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
                foreach (string map in new[] { "training-compound", "depot-yard" })
                {
                    var card = root.transform.Find("Verified map card " + map);
                    Assert.That(card.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                    Assert.That(root.transform.Find("Map card ground " + map), Is.Not.Null);
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
                var resources = root.GetComponentsInChildren<MeshRenderer>()
                    .SelectMany(r => r.sharedMaterials).Distinct().Cast<Object>().ToList();
                var frame = (RenderTexture)scene.Frame!;
                resources.Add(frame);
                resources.Add((RenderTexture)scene.MapCard("training-compound")!);
                resources.Add((RenderTexture)scene.MapCard("depot-yard")!);
                Object.DestroyImmediate(root);
                Assert.That(resources.All(r => r == null), Is.True,
                    "Destroying the shell child must release its material/target ownership.");
            }
            finally
            {
                broken[0] = original;
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
