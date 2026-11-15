#nullable enable
using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Session;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests
{
    public sealed class MenuStagingLifetimeTests
    {
        [UnityTest]
        public IEnumerator DestroyingPresentationReleasesCameraOwnedHandsAndWeaponRoot()
        {
            var root = new GameObject("First person lifetime runtime");
            var camera = new GameObject("Viewmodel camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            var hash = ContentIdentity.Hash(Encoding.ASCII.GetBytes("first-person-lifetime"));
            var visuals = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", hash));
            try
            {
                var presentation = Shooter.Presentation.Rig.ProductActorPresentation.Create(root.transform, camera);
                presentation.BindPackagedVisuals(visuals);
                yield return null; // Exercise the actual MonoBehaviour destruction lifecycle.
                var weapon = camera.transform.GetChild(0);
                var meshes = weapon.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
                Assert.That(meshes.Length, Is.EqualTo(3), "Weapon plus both packaged hands must be present.");
                Object.Destroy(presentation.gameObject);
                yield return null;
                yield return null;
                Assert.That(camera.transform.childCount, Is.Zero);
                Assert.That(meshes.All(mesh => mesh == null), Is.True);
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator DestroyingSuspendedStageReleasesMaterialsMeshesTexturesAndTargets()
        {
            var hash = ContentIdentity.Hash(Encoding.ASCII.GetBytes("menu-staging-lifetime"));
            var visuals = new[]
            {
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", hash)),
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("depot-yard", "low", hash))
            };
            var root = new GameObject("Menu staging runtime lifetime");
            try
            {
                var scene = root.AddComponent<ProductMenuScene>();
                scene.BindVerified(visuals);
                yield return null; // Activate the real MonoBehaviour lifecycle before suspension.
                yield return null;
                yield return null; // Static overview capture follows URP startup.
                foreach (string map in new[] { "training-compound", "depot-yard" })
                {
                    var target = (RenderTexture)scene.MapCard(map)!;
                    var previous = RenderTexture.active;
                    var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                    try
                    {
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        pixels.Apply();
                        Assert.That(pixels.GetPixels32().Select(p => (p.r / 8, p.g / 8, p.b / 8)).Distinct().Count(),
                            Is.GreaterThan(4), "The " + map + " card must contain a rendered scene, not only a clear color.");
                    }
                    finally { RenderTexture.active = previous; Object.Destroy(pixels); }
                }
                var materials = root.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
                var resources = materials.Cast<Object>().ToList();
                foreach (var material in materials)
                    foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                        if (material.HasProperty(property) && material.GetTexture(property) != null)
                            resources.Add(material.GetTexture(property));
                resources.AddRange(root.GetComponentsInChildren<MeshFilter>(true).Select(m => (Object)m.sharedMesh));
                resources.AddRange(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(m => (Object)m.sharedMesh));
                resources.Add((RenderTexture)scene.Frame!);
                resources.Add((RenderTexture)scene.MapCard("training-compound")!);
                resources.Add((RenderTexture)scene.MapCard("depot-yard")!);
                root.SetActive(false); // Same shell transition as starting a match.
                Object.Destroy(root);
                yield return null;
                yield return null; // OnDestroy schedules owned resources for deferred destruction.
                Assert.That(resources.Where(r => r != null).Select(r => r.name).ToArray(), Is.Empty,
                    "Suspended menu destruction must release all owned GPU resources.");
            }
            finally { if (root != null) Object.Destroy(root); }
        }
    }
}
