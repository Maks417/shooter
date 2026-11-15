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
