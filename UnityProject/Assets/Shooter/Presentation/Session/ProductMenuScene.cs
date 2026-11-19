#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Rig;
using Shooter.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// A cosmetic staging area assembled from verified packaged meshes. No colliders, navigation,
    /// simulation, fake gameplay adapters, imported art, or generator invocation. Renders only while the shell owns it.
    /// ADR-123: the showcase squad stands on a rooftop overlook and the focused map's real packaged geometry (the same
    /// mesh the operation cards are rendered from) lies below it as a graded, fogged vista under its own sky.
    /// </summary>
    public sealed class ProductMenuScene : MonoBehaviour
    {
        public const int StageLayer = 12;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Dictionary<string, RenderTexture> mapCards = new Dictionary<string, RenderTexture>();
        private readonly List<Camera> pendingCardCameras = new List<Camera>();
        private int cardWarmupFrames;
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
        private readonly Dictionary<string, (Transform Root, Bounds Local)> vistas = new Dictionary<string, (Transform, Bounds)>();
        private string focusedMap = "";
        private string stagedMap = "";
        private RenderTexture? previousFrame;
        private float fadeStartedAt = -10f;
        private Material? skyMaterial;
        private Texture2D? skyGradient;
        private Color fogColor = MenuStagingComposition.FogColor;
        private Color ambientSky = MenuStagingComposition.AmbientSky;
        private Color ambientEquator = MenuStagingComposition.AmbientEquator;
        private Color ambientGround = MenuStagingComposition.AmbientGround;
        private float fogStart = 40f, fogEnd = 120f;
        public Texture? Frame => built ? frame : null;

        /// <summary>The vista before the last focus change while it cross-fades out; null when settled.</summary>
        public Texture? PreviousFrame => built && FadeAlpha > 0f ? previousFrame : null;

        /// <summary>Opacity of <see cref="PreviousFrame"/> over <see cref="Frame"/> (1 to 0 over the cross-fade).</summary>
        public float FadeAlpha => reducedMotion ? 0f : Mathf.Clamp01(1f - (Time.unscaledTime - fadeStartedAt) / MenuVistaShot.CrossFadeSeconds);

        /// <summary>The map whose vista is staged (empty until the operation cards are captured).</summary>
        public string StagedMap => stagedMap;

        /// <summary>Capture any pending operation cards now and stage the vista (EditMode tests and repair).</summary>
        public void CaptureCardsNow()
        {
            if (!built || pendingCardCameras.Count == 0) return;
            CaptureMapCards();
            StageVista();
        }

        /// <summary>Stage the focused map's vista (the Main card under focus or the Setup selection).</summary>
        public void SetFocusedMap(string mapId)
        {
            if (string.IsNullOrEmpty(mapId) || mapId == focusedMap) return;
            focusedMap = mapId;
            if (built && pendingCardCameras.Count == 0) StageVista();
        }
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
                // ADR-083: one map card per registered map, in catalog order.
                var ids = Shooter.Generation.Environment.MapRegistry.MapIds;
                var maps = new MapId[ids.Count];
                for (int m = 0; m < maps.Length; m++) maps[m] = new MapId(ids[m]);
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

        public void SetReducedMotion(bool value)
        {
            reducedMotion = value;
            ApplyDustMotionPolicy();
        }

        /// <summary>Dust motes stay in the hierarchy for repair/retry ownership; reduced motion only hides them.</summary>
        private void ApplyDustMotionPolicy()
        {
            for (int i = 0; i < motes.Count; i++)
            {
                if (motes[i] != null)
                    motes[i].gameObject.SetActive(!reducedMotion);
            }
        }

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
            try
            {
                BuildVerified(visuals);
                failed = false;
            }
            catch
            {
                // A corrupt late dependency may fail after earlier textures were uploaded. Release
                // the whole cosmetic transaction, allowing repair/retry without duplicate resources.
                for (int i = transform.childCount - 1; i >= 0; i--)
                    DestroyObject(transform.GetChild(i).gameObject);
                ReleaseOwned();
                actors = null;
                stageCamera = null;
                frame = null;
                box = null;
                mapCards.Clear();
                pendingCardCameras.Clear();
                motes.Clear();
                vistas.Clear();
                stagedMap = "";
                previousFrame = null;
                skyMaterial = null;
                skyGradient = null;
                built = false;
                failed = true;
                throw;
            }
        }

        private void BuildVerified(PackagedVisualPayloads[] visuals)
        {
            var registered = Shooter.Generation.Environment.MapRegistry.MapIds;
            bool complete = visuals != null && visuals.Length == registered.Count && visuals.Length > 0;
            for (int i = 0; complete && i < visuals!.Length; i++) complete = visuals[i].MapId == registered[i];
            if (!complete)
                throw new InvalidOperationException("Every registered menu visual package is required, in catalog order.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Locked URP Lit is required.");
            for (int i = 0; i < visuals.Length; i++) ValidateShader(visuals[i]);
            box = Upload(MapVisualClosure.DecodeMapMesh(visuals[0], MapVisualClosure.UnitBoxMeshPath), "Verified menu unit box", true);

            // Packaged surface families only. Variants share uploaded planes; numeric tints add no art bytes.
            Material concrete = PackagedMaterial(visuals[0], shader, MapVisualClosure.MapMaterialPaths[0], "Stage concrete", .5f);
            Material concreteWarm = Variant(concrete, "Stage concrete warm", new Color(1f, .93f, .82f));
            Material concreteCool = Variant(concrete, "Stage concrete cool", new Color(.78f, .84f, .86f));
            Material cover = PackagedMaterial(visuals[0], shader, MapVisualClosure.MapMaterialPaths[1], "Stage cover", .5f);
            Material steel = Variant(cover, "Stage steel", new Color(.56f, .68f, .73f));
            Material steelMatte = Variant(cover, "Stage steel matte", new Color(.72f, .83f, .86f));
            steelMatte.SetFloat("_Smoothness", .42f);
            Material soil = PackagedMaterial(visuals[0], shader, MapVisualClosure.MapMaterialPaths[2], "Stage soil", .5f);
            Material asphalt = PackagedMaterial(visuals[Math.Min(1, visuals.Length - 1)], shader, MapVisualClosure.MapMaterialPaths[3], "Stage asphalt", .5f);
            Material apron = Variant(asphalt, "Stage apron", new Color(.62f, .64f, .60f));
            Material apronWarm = Variant(asphalt, "Stage apron warm", new Color(.70f, .66f, .58f));
            Material contact = Material(shader, "Stage contact", new Color(.03f, .035f, .04f), .05f);
            Material light = Material(shader, "Stage lamps", new Color(.62f, .48f, .28f), .12f);
            Material sandbag = Variant(cover, "Stage sandbags", new Color(.94f, .88f, .68f));

            // ADR-123: the box diorama is gone; the squad stands on a rooftop overlook above the focused map's vista.
            PlaceApronAndSoldiersBand(asphalt, apron, apronWarm, soil, concreteWarm, steel, steelMatte, sandbag, contact);

            // Camera frames the three showcase soldiers with mid/background depth behind them.
            var cam = new GameObject("Menu scene camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.transform.position = MenuStagingComposition.CameraPosition;
            cam.transform.LookAt(MenuStagingComposition.CameraLookAt);
            cam.fieldOfView = MenuStagingComposition.CameraFieldOfView;
            // ADR-123: the nearest stage object is metres away; a 0.3 m near plane keeps depth precision across the
            // 50-200 m vista, so the map floor and its card ground plate do not z-fight (flicker).
            cam.nearClipPlane = .3f;
            cam.farClipPlane = 120;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MenuStagingComposition.CameraClear;
            cam.cullingMask = 1 << StageLayer;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.enabled = false;
            cam.farClipPlane = 600f;
            stageCamera = cam;
            // ADR-123: the tier's tonemap, bloom and anti-aliasing grade the vista like gameplay.
            // (Play mode only: the shared volume is DontDestroyOnLoad; EditMode staging tests render ungraded.)
            if (Application.isPlaying) ProductPostProcessing.Attach(cam);
            frame = Own(new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { name = "Live procedural menu", antiAliasing = 1 });
            frame.Create();
            cam.targetTexture = frame;

            ApplyStagingLights();

            actors = ProductActorPresentation.Create(transform, cam);
            actors.BindMenuShowcase(visuals[0]);

            Material dust = Material(shader, "Analytic dust", new Color(.48f, .52f, .46f), 0);
            for (int i = 0; i < 14; i++)
            {
                var mote = Place("Mesh dust", new Vector3(-3.2f + (i * 7 % 19) * .38f, .35f + i % 7 * .38f, .4f + i * .5f), Vector3.one * .011f, dust, castShadows: false);
                mote.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                mote.GetComponent<MeshRenderer>().receiveShadows = false;
                motes.Add(mote.transform);
            }
            ApplyDustMotionPolicy();
            for (int i = 0; i < visuals.Length; i++) BuildMapCard(visuals[i], shader, i);
            SetLayer(transform);
            built = true;
            RenderStage();
            Debug.Log("SHOOTER_MENU_SCENE_READY actors=3 cards=" + visuals.Length + " vistas=" + vistas.Count + " colliders=0 authority=0");
        }

        /// <summary>Rooftop overlook: roof slab, coping on the far edge, pads and props under the showcase feet.</summary>
        private void PlaceApronAndSoldiersBand(Material asphalt, Material apron, Material apronWarm, Material soil, Material concreteWarm, Material steel, Material steelMatte, Material sandbag, Material contact)
        {
            const float roofBack = -11.5f, roofEdge = MenuVistaShot.RoofEdgeZ;
            float roofDepth = roofEdge - roofBack, roofMid = (roofEdge + roofBack) * .5f;
            Place("Roof slab", new Vector3(1.2f, -.12f, roofMid), new Vector3(42, .22f, roofDepth), asphalt, castShadows: false);
            Place("Roof coping", new Vector3(1.2f, .04f, roofEdge - .2f), new Vector3(42, .12f, .4f), concreteWarm, castShadows: false);
            Place("Roof seam", new Vector3(1.2f, -.01f, roofEdge - 3.5f), new Vector3(42, .02f, .08f), apron, castShadows: false);
            Place("Roof seam C", new Vector3(1.2f, -.01f, 2.4f), new Vector3(42, .02f, .06f), apronWarm, castShadows: false);
            Place("Roof seam cross", new Vector3(-2.4f, -.01f, roofMid), new Vector3(.06f, .02f, roofDepth), apron, castShadows: false);
            Place("Roof seam cross B", new Vector3(5.1f, -.01f, roofMid), new Vector3(.06f, .02f, roofDepth), apron, castShadows: false);
            Place("Roof vent", new Vector3(-6.2f, .45f, 3.6f), new Vector3(1.6f, .9f, 1.2f), steelMatte, castShadows: true);
            Place("Roof mast", new Vector3(8.5f, 2.4f, 4.2f), new Vector3(.12f, 4.8f, .12f), steel, castShadows: true);
            Place("Apron curb", new Vector3(1.2f, .05f, -1.85f), new Vector3(18f, .12f, .22f), apronWarm, castShadows: false);
            // Soft contact discs then hard pads — staged under exact BindMenuShowcase feet.
            Place("Hero soft A", new Vector3(1.25f, .008f, .3f), new Vector3(1.55f, .02f, 1.55f), contact, castShadows: false);
            Place("Hero soft B", new Vector3(-.25f, .008f, 3.2f), new Vector3(1.55f, .02f, 1.55f), contact, castShadows: false);
            Place("Hero soft C", new Vector3(3.2f, .008f, 4.8f), new Vector3(1.55f, .02f, 1.55f), contact, castShadows: false);
            Place("Hero pad A", new Vector3(1.25f, .018f, .3f), new Vector3(1.05f, .028f, 1.05f), apron, castShadows: false);
            Place("Hero pad B", new Vector3(-.25f, .018f, 3.2f), new Vector3(1.05f, .028f, 1.05f), apron, castShadows: false);
            Place("Hero pad C", new Vector3(3.2f, .018f, 4.8f), new Vector3(1.05f, .028f, 1.05f), apron, castShadows: false);
            Place("Ammo box", new Vector3(4.1f, .22f, .6f), new Vector3(.55f, .44f, .7f), steelMatte, castShadows: true);
            Place("Ammo box contact", new Vector3(4.1f, .02f, .6f), new Vector3(.65f, .03f, .8f), contact, castShadows: false);
            Place("Shelter lean-to", new Vector3(6.4f, 1.6f, 4.2f), new Vector3(3.2f, .18f, 3.4f), steelMatte, castShadows: true);
            Place("Shelter upright", new Vector3(5.1f, .9f, 4.2f), new Vector3(.22f, 1.8f, .22f), steel, castShadows: true);
        }

        private void ApplyStagingLights()
        {
            for (int i = 0; i < MenuStagingComposition.Lights.Length; i++)
            {
                var recipe = MenuStagingComposition.Lights[i];
                var light = StageLight(recipe.Name, recipe.Type, recipe.Color, recipe.Intensity);
                if (recipe.Type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(recipe.EulerOrPosition);
                    if (recipe.HardShadows)
                    {
                        light.shadows = LightShadows.Hard;
                        light.shadowStrength = .8f;
                    }
                }
                else
                {
                    light.transform.position = recipe.EulerOrPosition;
                    light.range = recipe.Range;
                }
            }
        }

        private void BuildMapCard(PackagedVisualPayloads visuals, Shader shader, int index)
        {
            float offset = 1000 + index * 1000;
            var root = new GameObject("Verified map card " + visuals.MapId) { layer = StageLayer };
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(offset, 0, 0);
            var meshData = MapVisualClosure.DecodeMapMesh(visuals, MapVisualClosure.MapSolidsMeshPath);
            Mesh mesh = Upload(meshData, "Map card " + visuals.MapId);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            Material structure = PackagedMaterial(visuals, shader, MapVisualClosure.MapMaterialPaths[0], "Card concrete " + visuals.MapId, .5f);
            Material metal = PackagedMaterial(visuals, shader, MapVisualClosure.MapMaterialPaths[1], "Card metal " + visuals.MapId, .5f);
            Material trim = Variant(structure, "Card plaster " + visuals.MapId, ProductSurfaceResponse.TrimPlaster.BaseColor);
            var accents = ProductSurfaceMaterial.ArchitecturalAccents(structure, metal, value => owned.Add(value));
            ProductMapView.AssignSolidsMaterials(root.AddComponent<MeshRenderer>(), meshData, structure, metal, trim, accents);
            // Fit actual packaged bounds: compact arenas no longer occupy a tiny old 256m plate.
            Bounds bounds = mesh.bounds;
            Vector3 center = bounds.center + new Vector3(offset, 0, 0);
            float width = bounds.size.x + 8f, depth = bounds.size.z + 8f;
            int groundFamily = ProductMapView.UsesAsphaltYard(visuals.MapId) ? 3 : 2;
            Material ground = PackagedMaterial(visuals, shader, MapVisualClosure.MapMaterialPaths[groundFamily],
                "Map card ground " + visuals.MapId, .5f);
            // Ground top sits 4 cm under the packaged floor so the two never share a depth (vista flicker).
            Group(root, Place("Map card ground " + visuals.MapId, new Vector3(center.x, -.09f, center.z), new Vector3(width, .1f, depth), ground));
            Material plate = Material(shader, "Map card plate " + visuals.MapId, new Color(.035f, .045f, .05f), .08f);
            Group(root, Place("Map card plate " + visuals.MapId, new Vector3(center.x, -.18f, center.z), new Vector3(width + 2f, .06f, depth + 2f), plate));
            Material rim = Variant(ground, "Map card rim " + visuals.MapId, new Color(.18f, .22f, .24f));
            Group(root, Place("Map card rim N " + visuals.MapId, new Vector3(center.x, .02f, center.z + depth * .5f), new Vector3(width, .08f, .3f), rim));
            Group(root, Place("Map card rim S " + visuals.MapId, new Vector3(center.x, .02f, center.z - depth * .5f), new Vector3(width, .08f, .3f), rim));
            Group(root, Place("Map card rim E " + visuals.MapId, new Vector3(center.x + width * .5f, .02f, center.z), new Vector3(.3f, .08f, depth), rim));
            Group(root, Place("Map card rim W " + visuals.MapId, new Vector3(center.x - width * .5f, .02f, center.z), new Vector3(.3f, .08f, depth), rim));
            // ADR-123: the card's ground, plate and rim ride with the solids, so the same root is the menu vista later.
            vistas[visuals.MapId] = (root.transform, new Bounds(bounds.center, bounds.size + new Vector3(8f, 0f, 8f)));
            var camera = new GameObject("Map card camera").AddComponent<Camera>();
            camera.transform.SetParent(transform, false);
            MenuStagingComposition.FrameOverview(camera, new Bounds(center, bounds.size));
            camera.cullingMask = 1 << StageLayer;
            camera.backgroundColor = MenuStagingComposition.MapCardClear;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.enabled = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var target = Own(new RenderTexture(384, 216, 16, RenderTextureFormat.ARGB32) { name = "Verified " + visuals.MapId + " overview" });
            target.Create();
            camera.targetTexture = target;
            mapCards.Add(visuals.MapId, target);
            // A camera rendered and destroyed during initial package binding can leave an empty
            // target in the player before URP's first frame. Keep it through pipeline startup.
            pendingCardCameras.Add(camera);
            cardWarmupFrames = 1;
            // Retain the tiny static card mesh inside this bounded scene until destruction.
        }

        private static void ValidateShader(PackagedVisualPayloads visuals)
        {
            var descriptor = MapJsonValue.Parse(visuals.Bytes(MapVisualClosure.ShaderPath(visuals.Profile))).Object;
            if (descriptor["unityShader"].String != "Universal Render Pipeline/Lit" ||
                descriptor["shaderException"].String != "procedural-urp-lit-v1")
                throw new InvalidOperationException("Packaged menu shader provenance rejected.");
        }

        private Material PackagedMaterial(PackagedVisualPayloads visuals, Shader shader, string path, string label, float repeats)
        {
            // Present companion planes must decode successfully. Missing optional planes retain
            // the material adapter's scalar defaults; corrupt bytes never turn into flat art.
            string prefix = path.EndsWith(".bin", StringComparison.Ordinal) ? path.Substring(0, path.Length - 4) : path;
            MaterialData? normal = visuals.Has(prefix + "-normal.bin")
                ? MapVisualClosure.DecodeMapMaterial(visuals, prefix + "-normal.bin") : null;
            MaterialData? mask = visuals.Has(prefix + "-mask.bin")
                ? MapVisualClosure.DecodeMapMaterial(visuals, prefix + "-mask.bin") : null;
            return ProductSurfaceMaterial.Create(shader, MapVisualClosure.DecodeMapMaterial(visuals, path),
                normal, mask, label, repeats, value => owned.Add(value));
        }

        private Material Variant(Material source, string label, Color tint)
        {
            var material = Own(new Material(source) { name = label, enableInstancing = true });
            material.SetColor("_BaseColor", tint);
            return material;
        }

        private void LateUpdate()
        {
            if (built && pendingCardCameras.Count > 0 && cardWarmupFrames-- <= 0)
            {
                CaptureMapCards();
                StageVista();
            }
            if (!built || Time.unscaledTime < nextRender) return;
            int tier = ProductPostProcessing.AppliedTier >= 0 ? ProductPostProcessing.AppliedTier : QualitySettings.GetQualityLevel();
            nextRender = Time.unscaledTime + 1f / MenuVistaShot.RenderHz(tier);
            float dt = reducedMotion ? 0 : Mathf.Min(Time.unscaledDeltaTime, .05f);
            sceneTime += dt;
            actors?.TickMenuShowcase(dt);
            ApplyCameraDrift();
            if (!reducedMotion)
            {
                for (int i = 0; i < motes.Count; i++)
                {
                    if (motes[i] == null || !motes[i].gameObject.activeSelf) continue;
                    var p = motes[i].localPosition;
                    p.y = .35f + i % 7 * .38f + Mathf.Sin(sceneTime * .3f + i) * .08f;
                    motes[i].localPosition = p;
                }
            }
            RenderStage();
        }

        private float openedAt = -1f;
        private Vector2 parallax;

        // The establishing push-in plays once; re-enabling the stage (focus changes, returning from a match) does not replay it.
        private void OnEnable()
        {
            if (openedAt < 0f) openedAt = Time.unscaledTime;
        }

        /// <summary>ADR-086 slow stage camera drift, short push-in after opening and gentle pointer parallax.</summary>
        private void ApplyCameraDrift()
        {
            if (stageCamera == null) return;
            if (openedAt < 0f) openedAt = Time.unscaledTime;
            Vector3 basePos = MenuStagingComposition.CameraPosition;
            Quaternion baseRot = Quaternion.LookRotation(MenuStagingComposition.CameraLookAt - basePos);
            // Real elapsed time: the stage renders at 30 Hz, so per-frame sceneTime under-counts.
            float sinceOpen = Time.unscaledTime - openedAt;
            var drift = Menus.ShellMotion.CameraDrift(sinceOpen, sinceOpen, reducedMotion);
            Vector2 target = Vector2.zero;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 m = mouse.position.ReadValue();
                var p = Menus.ShellMotion.PointerParallax(m.x / Screen.width * 2f - 1f, m.y / Screen.height * 2f - 1f, reducedMotion);
                target = new Vector2(p.X, p.Y);
            }
            float k = reducedMotion ? 1f : 1f - Mathf.Exp(-3f / 30f);
            parallax = Vector2.Lerp(parallax, target, k);
            Vector3 right = baseRot * Vector3.right, up = baseRot * Vector3.up, forward = baseRot * Vector3.forward;
            Vector3 offset = right * (drift.X + parallax.x) + up * (drift.Y + parallax.y) + forward * drift.Z;
            stageCamera.transform.position = basePos + offset;
            // Look target moves less than the eye, which reads as depth parallax on the staged layers.
            stageCamera.transform.LookAt(MenuStagingComposition.CameraLookAt + right * (drift.X + parallax.x) * .35f);
        }

        private void CaptureMapCards()
        {
            bool fog = RenderSettings.fog;
            AmbientMode mode = RenderSettings.ambientMode;
            Color sky = RenderSettings.ambientSkyColor;
            Color equator = RenderSettings.ambientEquatorColor;
            Color ground = RenderSettings.ambientGroundColor;
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = MenuStagingComposition.AmbientSky;
                RenderSettings.ambientEquatorColor = MenuStagingComposition.AmbientEquator;
                RenderSettings.ambientGroundColor = MenuStagingComposition.AmbientGround;
                foreach (var camera in pendingCardCameras) camera.Render();
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderTexture.active = previous;
                foreach (var camera in pendingCardCameras)
                {
                    camera.targetTexture = null;
                    DestroyObject(camera.gameObject);
                }
                pendingCardCameras.Clear();
            }
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
            float fogStartDistance = RenderSettings.fogStartDistance, fogEndDistance = RenderSettings.fogEndDistance;
            Material sky = RenderSettings.skybox;
            RenderTexture previous = RenderTexture.active;
            try
            {
                // ADR-123: the staged map's own sky palette, linear fog band and trilight ambient.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                if (skyMaterial != null) RenderSettings.skybox = skyMaterial;
                stageCamera.Render();
            }
            finally
            {
                RenderSettings.skybox = sky;
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.fogEndDistance = fogEndDistance;
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

        private static void Group(GameObject root, GameObject child) => child.transform.SetParent(root.transform, true);

        /// <summary>
        /// Put the focused map below the overlook (MenuVistaShot), hide the others, switch to its sky and fog, and start
        /// the cross-fade from the previous vista.
        /// </summary>
        private void StageVista()
        {
            if (vistas.Count == 0) return;
            if (!vistas.ContainsKey(focusedMap)) focusedMap = vistas.Keys.First();
            if (focusedMap == stagedMap) return;
            if (stagedMap.Length > 0 && frame != null && !reducedMotion)
            {
                if (previousFrame == null)
                {
                    previousFrame = Own(new RenderTexture(frame.width, frame.height, 0, RenderTextureFormat.ARGB32) { name = "Previous menu vista" });
                    previousFrame.Create();
                }
                Graphics.Blit(frame, previousFrame);
                fadeStartedAt = Time.unscaledTime;
            }
            foreach (var pair in vistas) pair.Value.Root.gameObject.SetActive(pair.Key == focusedMap);
            var (root, local) = vistas[focusedMap];
            var shot = MenuVistaShot.For(focusedMap);
            Quaternion yaw = Quaternion.Euler(0f, shot.YawDegrees, 0f);
            var anchor = shot.Anchor(shot.RotatedHalfDepth(local.extents.x, local.extents.z));
            Vector3 pivot = new Vector3(local.center.x, 0f, local.center.z);
            root.rotation = yaw;
            root.position = new Vector3(anchor.X, anchor.Y, anchor.Z) - yaw * pivot;
            ApplyAtmosphere(focusedMap);
            stagedMap = focusedMap;
            if (gameObject.activeInHierarchy) RenderStage();
        }

        /// <summary>Per-map sky gradient (Skybox/Panoramic over a generated lat-long strip), fog band and ambient.</summary>
        private void ApplyAtmosphere(string mapId)
        {
            GeneratedSkyMath.ResolveProductPalette(mapId, out var zenith, out var horizon, out var ground, out float start, out float end, out _);
            fogColor = Tint(horizon);
            fogStart = start;
            fogEnd = end * 1.6f;
            ambientSky = Tint(zenith);
            ambientEquator = Tint(horizon) * .8f;
            ambientGround = Tint(ground);
            const int rows = 64;
            if (skyGradient == null)
                skyGradient = Own(new Texture2D(2, rows, TextureFormat.RGBA32, false, false) { name = "Menu vista sky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear });
            var pixels = new Color32[2 * rows];
            for (int y = 0; y < rows; y++)
            {
                float elevation = y / (rows - 1f) * 2f - 1f;
                Color c = Tint(GeneratedSkyMath.SampleGradient(zenith, horizon, ground, elevation));
                pixels[y * 2] = pixels[y * 2 + 1] = c;
            }
            skyGradient.SetPixels32(pixels);
            skyGradient.Apply(false, false);
            if (skyMaterial == null)
            {
                var shader = Shader.Find("Skybox/Panoramic");
                if (shader != null) skyMaterial = Own(new Material(shader) { name = "Menu vista sky" });
            }
            if (skyMaterial != null)
            {
                if (skyMaterial.HasProperty("_MainTex")) skyMaterial.SetTexture("_MainTex", skyGradient);
                if (skyMaterial.HasProperty("_Exposure")) skyMaterial.SetFloat("_Exposure", 1f);
                if (stageCamera != null) stageCamera.clearFlags = CameraClearFlags.Skybox;
            }
            else if (stageCamera != null)
            {
                stageCamera.clearFlags = CameraClearFlags.SolidColor;
                stageCamera.backgroundColor = fogColor;
            }
        }

        private static Color Tint(GeneratedSkyMath.Rgb c) => new Color(c.R, c.G, c.B, 1f);

        private GameObject Place(string name, Vector3 position, Vector3 size, Material material, bool castShadows = false)
        {
            var go = new GameObject(name) { layer = StageLayer };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = ProductSurfaceMaterial.MetricBoxUv(box!, size, value => owned.Add(value));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
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

        private Mesh Upload(MeshData data, string name, bool keepReadable = false)
        {
            var mesh = Own(new Mesh { name = name, indexFormat = IndexFormat.UInt32 });
            mesh.vertices = data.Positions.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
            mesh.normals = data.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
            mesh.tangents = data.Tangents.Select(v => new Vector4(v.X, v.Y, v.Z, v.W)).ToArray();
            mesh.uv = data.Uv.Select(v => new Vector2(v.X, v.Y)).ToArray();
            mesh.subMeshCount = data.Submeshes.Count;
            for (int i = 0; i < data.Submeshes.Count; i++)
            {
                var submesh = data.Submeshes[i];
                mesh.SetTriangles(data.Indices.Skip((int)submesh.FirstIndex).Take((int)submesh.IndexCount).Select(v => (int)v).ToArray(), i);
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(!keepReadable);
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
        private static void DestroyObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private void OnDestroy() => ReleaseOwned();

        private void ReleaseOwned()
        {
            if (stageCamera != null)
            {
                ProductPostProcessing.Detach(stageCamera);
                stageCamera.targetTexture = null;
            }
            foreach (var camera in pendingCardCameras)
                if (camera != null) camera.targetTexture = null;
            pendingCardCameras.Clear();
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
