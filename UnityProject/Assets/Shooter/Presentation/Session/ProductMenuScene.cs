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
using Shooter.Presentation.Rendering;
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
                var maps = new[] { WellKnownMaps.TrainingCompound, WellKnownMaps.DepotYard, WellKnownMaps.OilDepot };
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
                built = false;
                failed = true;
                throw;
            }
        }

        private void BuildVerified(PackagedVisualPayloads[] visuals)
        {
            if (visuals == null || visuals.Length != 3
                || visuals[0].MapId != "training-compound"
                || visuals[1].MapId != "depot-yard"
                || visuals[2].MapId != "oil-depot")
                throw new InvalidOperationException("All three approved menu visual packages are required.");
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
            Material asphalt = PackagedMaterial(visuals[1], shader, MapVisualClosure.MapMaterialPaths[3], "Stage asphalt", .5f);
            Material apron = Variant(asphalt, "Stage apron", new Color(.62f, .64f, .60f));
            Material apronWarm = Variant(asphalt, "Stage apron warm", new Color(.70f, .66f, .58f));
            Material contact = Material(shader, "Stage contact", new Color(.03f, .035f, .04f), .05f);
            Material light = Material(shader, "Stage lamps", new Color(.62f, .48f, .28f), .12f);
            Material sandbag = Variant(cover, "Stage sandbags", new Color(.94f, .88f, .68f));

            PlaceDistantShells(concrete, concreteCool, concreteWarm, steel, steelMatte);
            PlaceMidProps(concrete, concreteWarm, steel, steelMatte, sandbag, contact, light);
            PlaceApronAndSoldiersBand(asphalt, apron, apronWarm, soil, concreteWarm, steel, steelMatte, sandbag, contact);

            // Camera frames the three showcase soldiers with mid/background depth behind them.
            var cam = new GameObject("Menu scene camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.transform.position = MenuStagingComposition.CameraPosition;
            cam.transform.LookAt(MenuStagingComposition.CameraLookAt);
            cam.fieldOfView = MenuStagingComposition.CameraFieldOfView;
            cam.nearClipPlane = .05f;
            cam.farClipPlane = 120;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MenuStagingComposition.CameraClear;
            cam.cullingMask = 1 << StageLayer;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.enabled = false;
            stageCamera = cam;
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
            Debug.Log("SHOOTER_MENU_SCENE_READY actors=3 cards=2 colliders=0 authority=0");
        }

        /// <summary>Far band — industrial skyline and hangar shells read through staged fog.</summary>
        private void PlaceDistantShells(Material concrete, Material concreteCool, Material concreteWarm, Material steel, Material steelMatte)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = -24 + i * 9.2f;
                float h = 7.4f + (i % 3) * 2.6f;
                float z = MenuStagingComposition.DistantShellBandZ + (i % 2) * 10;
                Place("Skyline shell", new Vector3(x, h * .5f, z), new Vector3(8.2f, h, 12), i % 2 == 0 ? concrete : concreteCool, castShadows: false);
                Place("Skyline roof", new Vector3(x, h + .14f, z), new Vector3(8.6f, .28f, 12.4f), steel, castShadows: false);
                // Functional loading-bay facades give the distant masses a human scale.
                if (i % 2 == 0)
                {
                    Place("Warehouse loading shutter", new Vector3(x, 1.8f, z - 6.025f), new Vector3(4.2f, 3.6f, .06f), steelMatte);
                    Place("Warehouse clerestory", new Vector3(x, 4.6f, z - 6.04f), new Vector3(5.6f, .72f, .08f), steel);
                    Place("Warehouse lintel", new Vector3(x, 3.72f, z - 6.08f), new Vector3(4.5f, .18f, .18f), concreteWarm);
                    for (int jamb = -1; jamb <= 1; jamb += 2)
                        Place("Warehouse portal jamb", new Vector3(x + jamb * 2.2f, 1.8f, z - 6.08f), new Vector3(.18f, 3.6f, .18f), concreteWarm);
                }
                if (i % 2 == 0)
                    Place("Skyline vent", new Vector3(x + 2.2f, h + 1.1f, z - 2f), new Vector3(1.4f, 1.6f, 1.4f), steelMatte, castShadows: false);
            }
            // Deep silhouette slab behind the skyline for BFV-like atmospheric layering.
            Place("Distant haze shell", new Vector3(2f, 10f, MenuStagingComposition.DistantShellBandZ + 22f),
                new Vector3(58f, 18f, 8f), concreteCool, castShadows: false);
            Place("Hangar volume", new Vector3(18, 5.5f, 34), new Vector3(16, 11, 18), concreteWarm, castShadows: false);
            Place("Hangar door frame", new Vector3(10.5f, 3.2f, 25.5f), new Vector3(.45f, 6.4f, 8), steel, castShadows: false);
            Place("Hangar threshold", new Vector3(10.5f, .04f, 25.5f), new Vector3(1.2f, .06f, 8.4f), concrete, castShadows: false);
        }

        /// <summary>Mid band — cargo, barriers and gantries frame the soldier showcase.</summary>
        private void PlaceMidProps(Material concrete, Material concreteWarm, Material steel, Material steelMatte, Material sandbag, Material contact, Material light)
        {
            for (int row = 0; row < 2; row++)
            {
                float x = row == 0 ? -5.6f : 7.1f;
                float z = 10 + row * 3.6f;
                Place("Cargo container", new Vector3(x, 1.25f, z), new Vector3(2.55f, 2.5f, 6.2f), steel, castShadows: true);
                for (int rib = 0; rib < 10; rib++)
                    Place("Container rib", new Vector3(x - 1.3f, 1.25f, z - 2.9f + rib * .58f), new Vector3(.09f, 2.48f, .07f), concrete, castShadows: false);
                for (int door = -1; door <= 1; door += 2)
                {
                    Place("Cargo door panel", new Vector3(x + door * .61f, 1.25f, z - 3.13f),
                        new Vector3(1.14f, 2.28f, .06f), steelMatte, castShadows: false);
                    Place("Cargo locking bar", new Vector3(x + door * .58f, 1.25f, z - 3.19f),
                        new Vector3(.055f, 2.12f, .055f), concrete, castShadows: false);
                    Place("Cargo latch", new Vector3(x + door * .48f, 1.05f, z - 3.23f),
                        new Vector3(.22f, .065f, .055f), steel, castShadows: false);
                }
                Place("Container contact", new Vector3(x, .02f, z), new Vector3(2.7f, .04f, 6.4f), contact, castShadows: false);
            }
            for (int i = 0; i < 7; i++)
            {
                Place("Blast barrier", new Vector3(-7.2f + i * 2.55f, .52f, 17.5f), new Vector3(2.1f, 1.05f, .58f), concreteWarm, castShadows: true);
                Place("Barrier contact", new Vector3(-7.2f + i * 2.55f, .02f, 17.5f), new Vector3(2.2f, .035f, .7f), contact, castShadows: false);
            }
            for (int i = 0; i < 5; i++)
                Place("Sandbag stack", new Vector3(-4.8f + i * 2.4f, .28f, MenuStagingComposition.MidPropBandZ), new Vector3(1.6f, .55f, .7f), sandbag, castShadows: true);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -9.2f : 11.4f;
                Place("Gantry upright", new Vector3(x, 6.2f, 22), new Vector3(.38f, 12.4f, .38f), steel, castShadows: true);
                Place("Gantry crossbar", new Vector3(x + (i == 0 ? 3.4f : -3.4f), 12.2f, 22), new Vector3(7.2f, .32f, .32f), steel, castShadows: true);
                Place("Apron lamp", new Vector3(x + (i == 0 ? 6.2f : -6.2f), 12f, 22), new Vector3(.7f, .14f, .45f), light, castShadows: false);
                Place("Gantry brace", new Vector3(x + (i == 0 ? 1.6f : -1.6f), 8.4f, 22), new Vector3(3.2f, .18f, .18f), steelMatte, castShadows: false);
            }
            Place("Watch tower", new Vector3(-14.5f, 4.2f, 28), new Vector3(2.4f, 8.4f, 2.4f), concrete, castShadows: true);
            Place("Tower cabin", new Vector3(-14.5f, 8.6f, 28), new Vector3(3.2f, 1.6f, 3.2f), steelMatte, castShadows: true);
        }

        /// <summary>Near apron + soldier-band pads/props under the showcase feet.</summary>
        private void PlaceApronAndSoldiersBand(Material asphalt, Material apron, Material apronWarm, Material soil, Material concreteWarm, Material steel, Material steelMatte, Material sandbag, Material contact)
        {
            Place("Apron slab", new Vector3(1.2f, -.12f, MenuStagingComposition.ApronBandZ), new Vector3(42, .22f, 48), asphalt, castShadows: false);
            Place("Apron soil edge", new Vector3(1.2f, -.09f, 26f), new Vector3(42, .16f, 10f), soil, castShadows: false);
            Place("Apron seam", new Vector3(1.2f, -.01f, 8), new Vector3(42, .02f, .08f), apron, castShadows: false);
            Place("Apron seam B", new Vector3(1.2f, -.01f, 14), new Vector3(42, .02f, .08f), apron, castShadows: false);
            Place("Apron seam C", new Vector3(1.2f, -.01f, 2.4f), new Vector3(42, .02f, .06f), apronWarm, castShadows: false);
            Place("Apron seam cross", new Vector3(-2.4f, -.01f, 8), new Vector3(.06f, .02f, 36), apron, castShadows: false);
            Place("Apron seam cross B", new Vector3(5.1f, -.01f, 8), new Vector3(.06f, .02f, 36), apron, castShadows: false);
            Place("Apron curb", new Vector3(1.2f, .05f, -1.85f), new Vector3(18f, .12f, .22f), apronWarm, castShadows: false);
            // Soft contact discs then hard pads — staged under exact BindMenuShowcase feet.
            Place("Hero soft A", new Vector3(1.25f, .008f, .3f), new Vector3(1.55f, .02f, 1.55f), contact, castShadows: false);
            Place("Hero soft B", new Vector3(-.25f, .008f, 3.2f), new Vector3(1.55f, .02f, 1.55f), contact, castShadows: false);
            Place("Hero soft C", new Vector3(3.2f, .008f, 4.8f), new Vector3(1.55f, .02f, 1.55f), contact, castShadows: false);
            Place("Hero pad A", new Vector3(1.25f, .018f, .3f), new Vector3(1.05f, .028f, 1.05f), apron, castShadows: false);
            Place("Hero pad B", new Vector3(-.25f, .018f, 3.2f), new Vector3(1.05f, .028f, 1.05f), apron, castShadows: false);
            Place("Hero pad C", new Vector3(3.2f, .018f, 4.8f), new Vector3(1.05f, .028f, 1.05f), apron, castShadows: false);
            Place("Crate near", new Vector3(-2.8f, .42f, -.4f), new Vector3(.9f, .84f, .9f), concreteWarm, castShadows: true);
            Place("Crate near contact", new Vector3(-2.8f, .02f, -.4f), new Vector3(1.05f, .03f, 1.05f), contact, castShadows: false);
            Place("Ammo box", new Vector3(4.1f, .22f, .6f), new Vector3(.55f, .44f, .7f), steelMatte, castShadows: true);
            Place("Ammo box contact", new Vector3(4.1f, .02f, .6f), new Vector3(.65f, .03f, .8f), contact, castShadows: false);
            Place("Low barrier FG", new Vector3(.4f, .28f, -1.1f), new Vector3(2.4f, .55f, .28f), sandbag, castShadows: true);
            Place("Fence post L", new Vector3(-3.6f, .55f, -1.6f), new Vector3(.12f, 1.1f, .12f), steelMatte, castShadows: true);
            Place("Fence post R", new Vector3(2.8f, .55f, -1.6f), new Vector3(.12f, 1.1f, .12f), steelMatte, castShadows: true);
            Place("Fence rail", new Vector3(-.4f, .95f, -1.6f), new Vector3(6.4f, .08f, .08f), steel, castShadows: false);
            Place("Shelter lean-to", new Vector3(6.4f, 1.6f, 7.2f), new Vector3(3.2f, .18f, 4.4f), steelMatte, castShadows: true);
            Place("Shelter upright", new Vector3(5.1f, .9f, 7.2f), new Vector3(.22f, 1.8f, .22f), steel, castShadows: true);
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
            int groundFamily = visuals.MapId == "depot-yard" || visuals.MapId == "oil-depot" ? 3 : 2;
            Material ground = PackagedMaterial(visuals, shader, MapVisualClosure.MapMaterialPaths[groundFamily],
                "Map card ground " + visuals.MapId, .5f);
            Place("Map card ground " + visuals.MapId, new Vector3(center.x, -.05f, center.z), new Vector3(width, .1f, depth), ground);
            Material plate = Material(shader, "Map card plate " + visuals.MapId, new Color(.035f, .045f, .05f), .08f);
            Place("Map card plate " + visuals.MapId, new Vector3(center.x, -.18f, center.z), new Vector3(width + 2f, .06f, depth + 2f), plate);
            Material rim = Variant(ground, "Map card rim " + visuals.MapId, new Color(.18f, .22f, .24f));
            Place("Map card rim N " + visuals.MapId, new Vector3(center.x, .02f, center.z + depth * .5f), new Vector3(width, .08f, .3f), rim);
            Place("Map card rim S " + visuals.MapId, new Vector3(center.x, .02f, center.z - depth * .5f), new Vector3(width, .08f, .3f), rim);
            Place("Map card rim E " + visuals.MapId, new Vector3(center.x + width * .5f, .02f, center.z), new Vector3(.3f, .08f, depth), rim);
            Place("Map card rim W " + visuals.MapId, new Vector3(center.x - width * .5f, .02f, center.z), new Vector3(.3f, .08f, depth), rim);
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
                CaptureMapCards();
            if (!built || Time.unscaledTime < nextRender) return;
            nextRender = Time.unscaledTime + 1f / 30;
            float dt = reducedMotion ? 0 : Mathf.Min(Time.unscaledDeltaTime, .05f);
            sceneTime += dt;
            actors?.TickMenuShowcase(dt);
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
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = MenuStagingComposition.FogColor;
                RenderSettings.fogDensity = MenuStagingComposition.FogDensity;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = MenuStagingComposition.AmbientSky;
                RenderSettings.ambientEquatorColor = MenuStagingComposition.AmbientEquator;
                RenderSettings.ambientGroundColor = MenuStagingComposition.AmbientGround;
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
            if (stageCamera != null) stageCamera.targetTexture = null;
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
