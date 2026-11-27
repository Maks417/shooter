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
                var meshes = weapon.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                    .Concat(weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.sharedMesh)).ToArray();
                // ADR-099: the hidden "FP fresh magazine" child reuses the packaged magazine mesh (no new geometry).
                // ADR-107: plus the "FP sight" child that carries the mounted sight mesh.
                Assert.That(meshes.Length, Is.EqualTo(7), "Weapon, magazine, fresh magazine, cycle, sight and both packaged hands must be present.");
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
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("depot-yard", "low", hash)),
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("oil-depot", "low", hash))
            };
            var root = new GameObject("Menu staging runtime lifetime");
            try
            {
                var scene = root.AddComponent<ProductMenuScene>();
                scene.BindVerified(visuals);
                yield return null; // Activate the real MonoBehaviour lifecycle before suspension.
                yield return null;
                yield return null; // Static overview capture follows URP startup.
                foreach (string map in new[] { "training-compound", "depot-yard", "oil-depot" })
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
                resources.Add((RenderTexture)scene.MapCard("oil-depot")!);
                root.SetActive(false); // Same shell transition as starting a match.
                Object.Destroy(root);
                yield return null;
                yield return null; // OnDestroy schedules owned resources for deferred destruction.
                Assert.That(resources.Where(r => r != null).Select(r => r.name).ToArray(), Is.Empty,
                    "Suspended menu destruction must release all owned GPU resources.");
            }
            finally { if (root != null) Object.Destroy(root); }
        }

        /// <summary>
        /// The shell changes the menu focus from OnGUI, which URP invokes inside its camera loop. Staging a vista there
        /// must not render the stage camera re-entrantly (URP throws "UniversalCameraData has already been created");
        /// the new vista renders on the next LateUpdate.
        /// </summary>
        [UnityTest]
        public IEnumerator FocusChangeInsideACameraRenderDefersTheVistaRender()
        {
            var hash = ContentIdentity.Hash(Encoding.ASCII.GetBytes("menu-staging-reentrant-focus"));
            var visuals = new[]
            {
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", hash)),
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("depot-yard", "low", hash)),
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("oil-depot", "low", hash))
            };
            var root = new GameObject("Menu staging re-entrant focus");
            var target = new RenderTexture(64, 36, 24) { name = "Re-entrant focus probe target" };
            var probe = new GameObject("Re-entrant focus probe").AddComponent<Camera>();
            probe.enabled = false;
            probe.targetTexture = target;
            // OnRenderObject runs inside URP's render graph while the camera's UniversalCameraData is alive, the same
            // window in which the player invokes the shell's OnGUI.
            var hook = probe.gameObject.AddComponent<FocusDuringRender>();
            try
            {
                var scene = root.AddComponent<ProductMenuScene>();
                hook.Scene = scene;
                scene.BindVerified(visuals);
                for (int i = 0; i < 6 && scene.StagedMap.Length == 0; i++) yield return null; // cards first, then the vista
                Assert.That(scene.StagedMap, Is.Not.Empty, "The vista is staged after the operation cards.");
                foreach (string map in new[] { "depot-yard", "oil-depot", "training-compound" })
                {
                    hook.Map = map;
                    hook.Armed = true;
                    probe.Render(); // SetFocusedMap runs inside this render, as it does from the shell's OnGUI.
                    hook.Armed = false;
                    Assert.That(hook.Calls, Is.GreaterThan(0), "The focus change must happen inside the probe render.");
                    Assert.That(scene.StagedMap, Is.EqualTo(map), "Staging itself stays synchronous.");
                    yield return null; // The deferred stage render runs in LateUpdate, outside any camera loop.
                    yield return null;
                }
            }
            finally
            {
                Object.Destroy(probe.gameObject);
                target.Release();
                Object.Destroy(target);
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// ADR-123: each vista renders under its own map's fog colour and trilight ambient (from the generated sky
        /// palette), and the global RenderSettings come back unchanged after the stage render.
        /// </summary>
        [UnityTest]
        public IEnumerator StageRendersUnderTheFocusedMapsFogAndAmbient()
        {
            var hash = ContentIdentity.Hash(Encoding.ASCII.GetBytes("menu-staging-atmosphere"));
            string[] maps = { "training-compound", "depot-yard", "oil-depot" };
            var visuals = maps.Select(map => PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build(map, "low", hash))).ToArray();
            Color fog0 = RenderSettings.fogColor, sky0 = RenderSettings.ambientSkyColor,
                equator0 = RenderSettings.ambientEquatorColor, ground0 = RenderSettings.ambientGroundColor;
            Color sentinel = new Color(1f, 0f, 1f, 1f);
            RenderSettings.fogColor = RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = sentinel;
            var root = new GameObject("Menu staging atmosphere");
            ProductMenuScene? scene = null;
            Camera? stage = null;
            var seen = new System.Collections.Generic.Dictionary<string, (Color Fog, Color Sky, Color Equator, Color Ground)>();
            void Record(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
            {
                if (camera == stage && scene != null && scene.StagedMap.Length > 0)
                    seen[scene.StagedMap] = (RenderSettings.fogColor, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor);
            }
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += Record;
            try
            {
                scene = root.AddComponent<ProductMenuScene>();
                scene.BindVerified(visuals);
                for (int i = 0; i < 6 && scene.StagedMap.Length == 0; i++) yield return null;
                Assert.That(scene.StagedMap, Is.Not.Empty);
                stage = root.GetComponentsInChildren<Camera>(true).Single(c => c.targetTexture != null && c.targetTexture == scene.Frame);
                // The first map was staged (and rendered) before the stage camera was known: focus the others first
                // so every focus is a change, then wait in real time for that vista's render.
                foreach (string map in maps.Where(m => m != scene.StagedMap).Append(scene.StagedMap).ToArray())
                {
                    scene.SetFocusedMap(map);
                    float until = Time.realtimeSinceStartup + 3f;
                    while (!seen.ContainsKey(map) && Time.realtimeSinceStartup < until) yield return null;
                }
                foreach (string map in maps)
                {
                    Assert.That(seen.ContainsKey(map), Is.True, map + " vista was rendered.");
                    Shooter.Presentation.GeneratedSkyMath.ResolveProductPalette(map, out var zenith, out var horizon, out var ground, out _, out _, out _);
                    Color Rgb(Shooter.Presentation.GeneratedSkyMath.Rgb c) => new Color(c.R, c.G, c.B, 1f);
                    var (fog, sky, equator, groundColor) = seen[map];
                    AssertColor(fog, Rgb(horizon), map + " fog colour");
                    AssertColor(sky, Rgb(zenith), map + " ambient sky");
                    AssertColor(equator, Rgb(horizon) * .8f, map + " ambient equator");
                    AssertColor(groundColor, Rgb(ground), map + " ambient ground");
                }
                Assert.That(RenderSettings.fogColor, Is.EqualTo(sentinel), "The stage restores the global fog colour.");
                Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sentinel), "The stage restores the global ambient.");
            }
            finally
            {
                UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= Record;
                RenderSettings.fogColor = fog0; RenderSettings.ambientSkyColor = sky0;
                RenderSettings.ambientEquatorColor = equator0; RenderSettings.ambientGroundColor = ground0;
                Object.Destroy(root);
            }
        }

        private static void AssertColor(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-3f), what + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-3f), what + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-3f), what + " (b)");
        }

        private sealed class FocusDuringRender : MonoBehaviour
        {
            public ProductMenuScene? Scene;
            public string Map = "";
            public bool Armed;
            public int Calls;

            private void OnRenderObject()
            {
                if (!Armed || Scene == null || Map.Length == 0) return;
                Calls++;
                Scene.SetFocusedMap(Map);
            }
        }
    }
}
