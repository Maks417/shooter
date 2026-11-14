#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Rig;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// A cosmetic staging area assembled from verified packaged meshes. No colliders, navigation,
    /// simulation, fake gameplay adapters, imported art, or generator invocation. Renders only while the shell owns it.
    /// </summary>
    public sealed class ProductMenuScene : MonoBehaviour
    {
        public const int StageLayer = 12;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Dictionary<string, RenderTexture> mapCards = new Dictionary<string, RenderTexture>();
        private Task<PackagedVisualPayloads[]>? load;
        private Camera? stageCamera;
        private ProductActorPresentation? actors;
        private RenderTexture? frame;
        private Mesh? box;
        private bool built;
        private bool failed;
        private bool reducedMotion;
        private float nextRender;
        private float sceneTime;
        private readonly List<Transform> motes = new List<Transform>();
        public Texture? Frame => built ? frame : null;
        public bool IsReady => built;
        public string Status => failed ? "Menu scenery unavailable. Repair the installed game content." : built ? "" : "Preparing operations...";
        public int ShowcaseActorCount => actors?.LiveAvatarCount ?? 0;
        public Texture? MapCard(string mapId) => mapCards.TryGetValue(mapId, out var image) ? image : null;

        public static ProductMenuScene Create(Transform parent, IProductMenuVisualSource source)
        {
            var scene = new GameObject("Cosmetic menu staging area").AddComponent<ProductMenuScene>();
            scene.transform.SetParent(parent, false);
            scene.load = Task.Run(() =>
            {
                var maps = new[] { WellKnownMaps.TrainingCompound, WellKnownMaps.DepotYard };
                var result = new PackagedVisualPayloads[maps.Length];
                for (int i = 0; i < maps.Length; i++)
                {
                    if (!source.TryLoadMenuVisuals(maps[i], out result[i], out string diagnostic))
                        throw new InvalidOperationException(maps[i].Value + ": " + diagnostic);
                }
                return result;
            });
            return scene;
        }

        public void SetReducedMotion(bool value) => reducedMotion = value;

        private void Update()
        {
            if (!built && !failed && load != null && load.IsCompleted)
            {
                try { BindVerified(load.GetAwaiter().GetResult()); }
                catch (Exception error)
                {
                    failed = true;
                    Debug.LogWarning("SHOOTER_MENU_SCENE_FAILURE " + error.Message);
                }
                finally { load = null; }
            }
        }

        /// <summary>Uploads on the Unity main thread; byte verification belongs to the package source.</summary>
        public void BindVerified(PackagedVisualPayloads[] visuals)
        {
            if (built) throw new InvalidOperationException("Menu scenery is already bound.");
            if (visuals == null || visuals.Length != 2 || visuals[0].MapId != "training-compound" || visuals[1].MapId != "depot-yard")
                throw new InvalidOperationException("Both approved menu visual packages are required.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Locked URP Lit is required.");
            box = Upload(MapVisualClosure.DecodeMapMesh(visuals[0], MapVisualClosure.UnitBoxMeshPath), "Verified menu unit box");
            Material concrete = Material(shader, "Stage concrete", new Color(.18f, .21f, .22f), .08f);
            Material steel = Material(shader, "Stage steel", new Color(.08f, .13f, .15f), .42f);
            Material ground = Material(shader, "Stage ground", new Color(.075f, .095f, .10f), .38f);
            Material light = Material(shader, "Stage lamps", new Color(.55f, .42f, .24f), .1f);
            Place("Apron", new Vector3(0, -.14f, 10), new Vector3(90, .25f, 90), ground);
            // Bounded numeric staging recipe: cargo, barriers, industrial shells and gantries.
            for (int row = 0; row < 2; row++)
            {
                float x = row == 0 ? -5.3f : 6.7f;
                Place("Cargo container", new Vector3(x, 1.25f, 9 + row * 4), new Vector3(2.5f, 2.5f, 6), steel);
                for (int rib = 0; rib < 12; rib++)
                    Place("Container rib", new Vector3(x - 1.28f, 1.25f, 6.2f + row * 4 + rib * .5f), new Vector3(.09f, 2.48f, .07f), concrete);
            }
            for (int i = 0; i < 6; i++)
                Place("Blast barrier", new Vector3(-6 + i * 2.8f, .55f, 16), new Vector3(2.2f, 1.1f, .65f), concrete);
            for (int i = 0; i < 5; i++)
            {
                float x = -19 + i * 9;
                float h = 6 + i % 3 * 2;
                Place("Industrial skyline", new Vector3(x, h * .5f, 36 + i % 2 * 8), new Vector3(7, h, 10), concrete);
                Place("Roof cap", new Vector3(x, h + .12f, 36 + i % 2 * 8), new Vector3(7.4f, .25f, 10.4f), steel);
            }
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -8 : 10;
                Place("Gantry upright", new Vector3(x, 6, 24), new Vector3(.35f, 12, .35f), steel);
                Place("Gantry crossbar", new Vector3(x + 3, 12, 24), new Vector3(6, .35f, .35f), steel);
                Place("Apron lamp", new Vector3(x + 5.5f, 11.8f, 24), new Vector3(.6f, .12f, .4f), light);
            }
            var cam = new GameObject("Menu scene camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.transform.position = new Vector3(0, 1.4f, -2.6f);
            cam.transform.LookAt(new Vector3(.45f, 1.1f, .6f));
            cam.fieldOfView = 42;
            cam.nearClipPlane = .05f;
            cam.farClipPlane = 90;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.11f, .22f, .27f);
            cam.cullingMask = 1 << StageLayer;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.enabled = false;
            stageCamera = cam;
            frame = Own(new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { name = "Live procedural menu", antiAliasing = 1 });
            frame.Create();
            cam.targetTexture = frame;
            var key = StageLight("Cool staging light", LightType.Directional, new Color(.53f, .76f, 1f), 1.6f);
            key.transform.rotation = Quaternion.Euler(28, -120, 0);
            key.shadows = LightShadows.Hard;
            var rim = StageLight("Warm rim light", LightType.Directional, new Color(1f, .62f, .30f), 1.1f);
            rim.transform.rotation = Quaternion.Euler(12, 135, 0);
            actors = ProductActorPresentation.Create(transform, cam);
            actors.BindMenuShowcase(visuals[0]);
            Material dust = Material(shader, "Analytic dust", new Color(.44f, .55f, .53f), 0);
            for (int i = 0; i < 16; i++)
            {
                var mote = Place("Mesh dust", new Vector3(-3 + (i * 7 % 17) * .4f, .3f + i % 6 * .4f, 1 + i * .55f), Vector3.one * .012f, dust);
                mote.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                motes.Add(mote.transform);
            }
            for (int i = 0; i < visuals.Length; i++) BuildMapCard(visuals[i], shader, i);
            SetLayer(transform);
            built = true;
            RenderStage();
            Debug.Log("SHOOTER_MENU_SCENE_READY actors=3 cards=2 colliders=0 authority=0");
        }

        private void BuildMapCard(PackagedVisualPayloads visuals, Shader shader, int index)
        {
            float offset = 1000 + index * 1000;
            var root = new GameObject("Verified map card " + visuals.MapId);
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(offset, 0, 0);
            root.layer = StageLayer;
            root.AddComponent<MeshFilter>().sharedMesh = Upload(MapVisualClosure.DecodeMapMesh(visuals, MapVisualClosure.MapSolidsMeshPath), "Map card " + visuals.MapId);
            root.AddComponent<MeshRenderer>().sharedMaterial = Material(shader, "Map card surfaces", new Color(.32f, .40f, .40f), .15f);
            var camera = new GameObject("Map card camera").AddComponent<Camera>();
            camera.transform.SetParent(transform, false);
            camera.transform.position = new Vector3(offset + 145, 175, -150);
            camera.transform.LookAt(new Vector3(offset, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 160;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 600;
            camera.cullingMask = 1 << StageLayer;
            camera.backgroundColor = new Color(.09f, .17f, .21f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.enabled = false;
            var target = Own(new RenderTexture(384, 216, 16, RenderTextureFormat.ARGB32) { name = "Verified " + visuals.MapId + " overview" });
            target.Create();
            camera.targetTexture = target;
            bool fog = RenderSettings.fog;
            RenderTexture previous = RenderTexture.active;
            try { RenderSettings.fog = false; camera.Render(); }
            finally { RenderSettings.fog = fog; RenderTexture.active = previous; }
            mapCards.Add(visuals.MapId, target);
            if (Application.isPlaying) Destroy(camera.gameObject); else DestroyImmediate(camera.gameObject);
            // Retain the tiny static card mesh inside this bounded scene until destruction.
        }

        private void LateUpdate()
        {
            if (!built || Time.unscaledTime < nextRender) return;
            nextRender = Time.unscaledTime + 1f / 30;
            float dt = reducedMotion ? 0 : Mathf.Min(Time.unscaledDeltaTime, .05f);
            sceneTime += dt;
            actors?.TickMenuShowcase(dt);
            for (int i = 0; i < motes.Count; i++)
            {
                var p = motes[i].localPosition;
                p.y = .3f + i % 6 * .4f + Mathf.Sin(sceneTime * .3f + i) * .08f;
                motes[i].localPosition = p;
            }
            RenderStage();
        }

        private void RenderStage()
        {
            if (stageCamera == null) return;
            bool fog = RenderSettings.fog;
            Color fogColor = RenderSettings.fogColor;
            FogMode fogMode = RenderSettings.fogMode;
            float density = RenderSettings.fogDensity;
            AmbientMode ambientMode = RenderSettings.ambientMode;
            Color ambient = RenderSettings.ambientLight;
            Color ambientSky = RenderSettings.ambientSkyColor;
            Color ambientEquator = RenderSettings.ambientEquatorColor;
            Color ambientGround = RenderSettings.ambientGroundColor;
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(.11f, .22f, .27f);
                RenderSettings.fogDensity = .025f;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.18f, .24f, .27f);
                stageCamera.Render();
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogDensity = density;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                RenderTexture.active = previous;
            }
        }

        private GameObject Place(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = new GameObject(name) { layer = StageLayer };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = box;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private Light StageLight(string name, LightType type, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.type = type; light.color = color; light.intensity = intensity;
            light.cullingMask = 1 << StageLayer; light.shadows = LightShadows.None;
            return light;
        }

        private Mesh Upload(MeshData data, string name)
        {
            var mesh = Own(new Mesh { name = name, indexFormat = IndexFormat.UInt32 });
            mesh.vertices = data.Positions.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
            mesh.normals = data.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
            mesh.uv = data.Uv.Select(v => new Vector2(v.X, v.Y)).ToArray();
            mesh.triangles = data.Indices.Select(v => (int)v).ToArray();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private Material Material(Shader shader, string name, Color color, float smoothness)
        {
            var material = Own(new Material(shader) { name = name, enableInstancing = true });
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static void SetLayer(Transform root)
        {
            root.gameObject.layer = StageLayer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i));
        }

        private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
        private void OnDestroy()
        {
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] is RenderTexture rt) rt.Release();
                if (owned[i] != null)
                {
                    if (Application.isPlaying) Destroy(owned[i]); else DestroyImmediate(owned[i]);
                }
            }
            owned.Clear();
        }
    }
}
