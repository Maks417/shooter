#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.View;
using Shooter.Generation.Icons;
using Shooter.Presentation.Rendering;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Presentation.Rig
{
    /// <summary>
    /// Authority-bound procedural soldiers/weapons for the product client.
    /// Consumes ActiveWeaponSlot and combat Reload/Shot events; never infers reload from ammo drop.
    /// </summary>
    public sealed class ProductActorPresentation : MonoBehaviour
    {
        private const int RightHandJoint = 14;

        private sealed class Avatar
        {
            public EntityId EntityId;
            public int BodyLod = -1;
            public byte Team;
            public bool Corpse;
            public byte WeaponSlot;
            public DefinitionId WeaponDefinition;
            public Transform Root = null!;
            public Transform Weapon = null!;
            public Transform[] Bones = null!;
            public SkinnedMeshRenderer? Body;
            /// <summary>ADR-124 licensed authored body; null when the generated body renders (diagnostics only).</summary>
            public AuthoredSoldierBody? Authored;
            public MeshRenderer WeaponRenderer = null!;
            public MeshFilter WeaponFilter = null!;
            public Transform? MagazinePart;
            public MeshFilter? MagazineFilter;
            public MeshRenderer? MagazineRenderer;
            public Transform? CyclePart;
            public MeshFilter? CycleFilter;
            public MeshRenderer? CycleRenderer;
            /// <summary>ADR-107 sight mounted on the held primary (invalid: none / factory sight).</summary>
            public DefinitionId Sight;
            public Transform? SightPart;
            public MeshFilter? SightFilter;
            public MeshRenderer? SightRenderer;
            public float CycleOpen;
            /// <summary>ADR-111 third-person knife cut / grenade throw overlay clocks (seconds since start; &lt;0 idle).</summary>
            public float MeleeSeconds = -1f;
            public float ThrowSeconds = -1f;
            public uint MeleeSwings;
            public SoldierMotion Motion = null!;
            public Float3 PreviousFeet;
            /// <summary>Cosmetic root blend target; authority feet stay on PreviousFeet / LastFeet.</summary>
            public Float3 DisplayedFeet;
            public double Distance;
            public ushort PreviousHealth = 100;
            public float ReloadPhase = -1f;
            public float DeathPhase = -1f;
            public bool DeathCollapseActive;
            public bool HasPrevious;
            /// <summary>ADR-100 travel heading, airborne, landing and slide blends for the cosmetic legs.</summary>
            public readonly ThirdPersonLocomotionBlend Locomotion = new ThirdPersonLocomotionBlend();
            // T062 review metrics (read-only presentation; does not change authority or generator sources).
            public float LastArmBodyClearance;
            public float LastEyeSightLineError;
            public float LastLeftGripError;
            public float LastRightGripError;
            public float LastPlantSlide;
            public float LastStockError;
            public float LastAimPitch;
            public Float3 LastRightEye;
            public Float3 LastWeaponPosition;
            public Float3 LastSightWorld;
            public Float3 LastFeet;
            public bool HasReviewMetrics;
            public GameObject? Contact;
        }

        /// <summary>Cosmetic foot support: installed collision boxes first, then the actor's own feet height. A jump,
        /// ledge or map-edge step with no box under the foot must never throw out of Sync (that aborted the local
        /// weapon update and left ADS/scope state stale).</summary>
        private sealed class FallbackGround : ISoldierGround
        {
            public ISoldierGround? Primary;
            public float Height;
            public bool TrySample(float x, float z, out float y)
            {
                if (Primary != null && Primary.TrySample(x, z, out y) && !float.IsNaN(y) && !float.IsInfinity(y)) return true;
                y = Height;
                return true;
            }
        }
        private readonly FallbackGround footGround = new FallbackGround();

        public CollisionSoldierGround? CollisionGround { get; set; }
        public Func<Float3,float>? AmbientVisibility { get; set; }
        private MaterialPropertyBlock ambientBlock=null!;
        private int packagedBindEpoch;
        private Mesh? contactMesh;private Material? contactMaterial;
        private Renderer[] localLitRenderers=Array.Empty<Renderer>();
        private readonly Dictionary<ulong, Avatar> avatars = new Dictionary<ulong, Avatar>();
        private readonly ProductActorBinder binder = new ProductActorBinder();
        private readonly ProductAuthorityActionView actions = new ProductAuthorityActionView();
        private readonly List<ActorLifecycleState> livingScratch = new List<ActorLifecycleState>(16);
        private readonly List<ActorLifecycleState> corpseScratch = new List<ActorLifecycleState>(4);
        private readonly List<EntityId> spawnScratch = new List<EntityId>(16);
        private readonly List<EntityId> despawnScratch = new List<EntityId>(16);
        private readonly List<EntityId> corpseEnsureScratch = new List<EntityId>(4);
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private Mesh? bodyMesh;
        // ADR-106: every packaged soldier LOD at or coarser than the tier's own, chosen per avatar by camera distance.
        private readonly Mesh?[] bodyLods = new Mesh?[3];
        private int bodyBaseLod;
        /// <summary>Uploaded packaged weapon meshes keyed by visual profile MeshAssetKey (ADR-078).</summary>
        private sealed class WeaponMeshSet
        {
            public Mesh? Held, Local, Magazine, Cycle;
            public Float3 SightSocket;
            public Float3 MuzzleSocket;
            public Float3 SupportSocket;
            public bool HasSupportSocket;
        }
        private readonly Dictionary<string, WeaponMeshSet> weaponMeshes = new Dictionary<string, WeaponMeshSet>(StringComparer.Ordinal);
        /// <summary>ADR-107 rail-mounted sight meshes by key (<c>sight-&lt;sight&gt;-&lt;kind&gt;</c>): world LOD and arm's-length LOD0.</summary>
        private readonly Dictionary<string, (Mesh held, Mesh local)> sightMeshes = new Dictionary<string, (Mesh held, Mesh local)>(StringComparer.Ordinal);
        private Transform? localSight;
        private MeshFilter? localSightFilter;
        private DefinitionId localVisibleSight;
        private Material[]? team1Palette;
        private Material[]? team2Palette;
        // ADR-124 authored soldier bodies: catalog, per-variant prototypes, restyled materials and gear palettes.
        /// <summary>True for the product: licensed authored bodies. False renders the generated soldier (diagnostics).</summary>
        public bool UseAuthoredBodies { get; set; } = true;
        private SoldierSkinCatalog? skinCatalog;
        private Shader? surfaceShader;
        private readonly Dictionary<string, AuthoredSoldierBody.Prototype> skinPrototypes = new Dictionary<string, AuthoredSoldierBody.Prototype>(StringComparer.Ordinal);
        private readonly Dictionary<(Material, Texture?), Material> restyledMaterials = new Dictionary<(Material, Texture?), Material>();
        private Material[]? team1Gear, team2Gear, team1Paint, team2Paint;
        // Solid team kit for authored flat-colour packs, whose UVs cannot carry the camo texture: uniform, webbing.
        private Material[]? team1Solid, team2Solid;
        private Material? rifleMetal;
        private Material? riflePolymer;
        private Material[] weaponMaterials = Array.Empty<Material>();
        private readonly Dictionary<Mesh, Material[]> weaponPalettes = new Dictionary<Mesh, Material[]>();
        private WeaponRecipe? motionRecipe;
        private DefinitionId localVisibleWeaponId;
        /// <summary>Last gun seen in each slot this life: random loadouts mean a slot no longer implies a weapon (ADR-102).</summary>
        private readonly DefinitionId[] localSlotWeaponIds = new DefinitionId[4];
        private EntityId localHuman;
        private Camera? fpsCamera;
        private Transform? localWeapon;
        private Transform? localMagazine;
        private MeshFilter? localMagazineFilter;
        /// <summary>Second magazine carried up from the belt during a reload (ADR-099).</summary>
        private Transform? localFreshMagazine;
        private MeshFilter? localFreshMagazineFilter;
        private Transform? localCycle;
        private MeshFilter? localCycleFilter;
        private SkinnedMeshRenderer? triggerHand;
        private SkinnedMeshRenderer? supportHand;
        private Shooter.Presentation.Fx.KnifeBladeTrail? bladeTrail;
        private Shooter.Presentation.Fx.GrenadeArcPresenter? grenadeArc;
        private static readonly Vector3 KnifeEdgeBase = new Vector3(0f, -.004f, .035f);
        private static readonly Vector3 KnifeEdgeTip = new Vector3(0f, .004f, .158f);
        private readonly Mesh?[] firstPersonHandMeshes = new Mesh?[8];
        private Material[]? firstPersonHandMaterials;
        private readonly SkeletonData?[] handSkeletons = new SkeletonData?[8];
        private readonly Transform[][] handBones = { new Transform[FirstPersonHandRig.JointCount], new Transform[FirstPersonHandRig.JointCount] };
        private readonly ProceduralViewModel localView = new ProceduralViewModel();
        private readonly FirstPersonHandling localHandling = new FirstPersonHandling();
        private readonly FirstPersonLocomotionPose localLocomotion = new FirstPersonLocomotionPose();
        private bool hasLookSample;
        private float lastLookYaw;
        private float lastLookPitch;
        private float localAdsPresentation;
        private byte localVisibleWeaponSlot;
        private EntityId localViewHuman;
        private bool ready;
        private bool visualsBound;
        private bool localWeaponScopeHidden;
        private string reviewMapId = "";
        private string reviewProfile = "";
        private uint reviewTick;
        private float aggregateMinArmClearance = float.MaxValue;
        private float aggregateMaxEyeSightError;
        private int reviewSampleCount;
        private ActorLifecycleState[]? showcaseStates;

        public int LiveAvatarCount
        {
            get
            {
                int n = 0;
                foreach (var pair in avatars)
                    if (!pair.Value.Corpse) n++;
                return n;
            }
        }

        public float AggregateMinArmBodyClearanceMeters =>
            reviewSampleCount == 0 ? float.NaN : aggregateMinArmClearance;
        public float AggregateMaxEyeSightLineErrorMeters => aggregateMaxEyeSightError;
        public int ReviewSampleCount => reviewSampleCount;
        /// <summary>Equip-gated ADS blend for the local FP weapon (presentation FOV/optic).</summary>
        public float LocalAdsPresentationBlend => localAdsPresentation;
        /// <summary>Mesh slot currently shown for the local FP weapon (lags desired mid-switch).</summary>
        public byte LocalVisibleWeaponSlot => localVisibleWeaponSlot;
        /// <summary>DefinitionId for the mesh currently shown (ADR-072).</summary>
        public DefinitionId LocalVisibleWeaponId => localVisibleWeaponId;
        /// <summary>ADR-107 sight mounted on the shown weapon (invalid when it carries its factory sight or none).</summary>
        public DefinitionId LocalVisibleSight => localVisibleSight;
        /// <summary>Presentation profile of the shown weapon through its mounted sight (optic mode and ADS field of view).</summary>
        public WeaponVisualProfile LocalVisibleProfile =>
            Sighted(WeaponVisualProfiles.Resolve(localVisibleWeaponId, localVisibleWeaponSlot), localVisibleSight).WithHandling(LocalHandling);

        /// <summary>ADR-108: the local player's career handling buffs (recoil, reload, equip); identity when none.</summary>
        public WeaponHandlingScale LocalHandling { get; set; }

        /// <summary>ADR-107: reads the sight on an actor's primary from the session (read-only; null = factory sights).</summary>
        public Func<EntityId, DefinitionId>? SightReader { get; set; }

        private DefinitionId SightOf(EntityId entity, byte slot) =>
            slot == ProductAuthorityActionView.RifleSlot && SightReader != null ? SightReader(entity) : default;

        /// <summary>The profile ADS presents with: the gun's own row, its optic taken from the mounted sight when known.</summary>
        public static WeaponVisualProfile Sighted(in WeaponVisualProfile profile, DefinitionId sight) =>
            sight.IsValid ? WeaponSightChoices.Apply(profile, sight) : profile;

        /// <summary>The sight a gun actually carries: <paramref name="sight"/> when allowed, else its factory sight.</summary>
        private static DefinitionId EffectiveSight(DefinitionId weapon, DefinitionId sight, out WeaponSightChoice? choice)
        {
            if (!WeaponSightChoices.TryGet(weapon, out var found)) { choice = null; return default; }
            choice = found;
            return found.Allows(sight) ? sight : found.Default;
        }

        private Mesh? SightMeshFor(DefinitionId weapon, DefinitionId sight, bool local)
        {
            var effective = EffectiveSight(weapon, sight, out var choice);
            string? key = choice?.MeshKeyFor(effective);
            if (key == null || !sightMeshes.TryGetValue(key, out var meshes)) return null;
            return local ? meshes.local : meshes.held;
        }

        /// <summary>Line of sight in weapon space through the mounted sight (ADR-107), else the gun's own sight-eye socket.</summary>
        private Float3 SightEyeFor(in WeaponVisualProfile profile, DefinitionId sight)
        {
            var effective = EffectiveSight(profile.DefinitionId, sight, out var choice);
            if (choice != null && WeaponSightChoices.TryGetSight(effective, out var optic))
                return WeaponSights.EyeFor(choice.MountKind, optic.Name);
            return SightSocketForProfile(profile);
        }

        public int CorpseAvatarCount
        {
            get
            {
                int n = 0;
                foreach (var pair in avatars)
                    if (pair.Value.Corpse) n++;
                return n;
            }
        }

        public int HandleCount => avatars.Count;
        public ProductActorBinder Binder => binder;
        public ProductAuthorityActionView Actions => actions;
        public bool VisualsBound => visualsBound;

        public static ProductActorPresentation Create(Transform parent, Camera fpsCamera)
        {
            var root = new GameObject("Product actor presentation").AddComponent<ProductActorPresentation>();
            root.transform.SetParent(parent, false);
            root.fpsCamera = fpsCamera ?? throw new ArgumentNullException(nameof(fpsCamera));
            root.InitializeShared();
            return root;
        }

        private void InitializeShared()
        {
            ambientBlock=new MaterialPropertyBlock();
            var shader = Shader.Find("Shooter/MapSurface");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Required locked URP Lit shader missing; no art fallback.");

            // Recipe for soldier motion sockets only — no WeaponGeometry mesh generation on the product path.
            motionRecipe = WeaponMeshCatalog.Require(WeaponVisualProfiles.MeshKeyRifle).Recipe
                ?? throw new InvalidOperationException("Rifle mesh source must be recipe-backed.");

            localWeapon = new GameObject("Local FP weapon").transform;
            localWeapon.SetParent(fpsCamera!.transform, false);
            localWeapon.gameObject.AddComponent<MeshFilter>();
            localWeapon.gameObject.AddComponent<MeshRenderer>();
            localMagazineFilter = CreateWeaponChild(localWeapon, "FP magazine", out localMagazine);
            localFreshMagazineFilter = CreateWeaponChild(localWeapon, "FP fresh magazine", out localFreshMagazine);
            localFreshMagazine.gameObject.SetActive(false);
            localCycleFilter = CreateWeaponChild(localWeapon, "FP cycle", out localCycle);
            localSightFilter = CreateWeaponChild(localWeapon, "FP sight", out localSight);
            SkinnedMeshRenderer CreateHand(string name, int handIndex)
            {
                var hand = new GameObject(name);
                hand.transform.SetParent(localWeapon, false);
                var renderer = hand.AddComponent<SkinnedMeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.quality = SkinQuality.Bone2;
                renderer.updateWhenOffscreen = true;
                renderer.localBounds = new Bounds(new Vector3(0,-.1f,.1f),Vector3.one*1.2f);
                for (int j=0;j<FirstPersonHandRig.JointCount;j++)
                {
                    var bone = new GameObject("FP joint " + j).transform;
                    bone.SetParent(hand.transform,false); handBones[handIndex][j]=bone;
                }
                renderer.bones = handBones[handIndex]; renderer.rootBone=handBones[handIndex][0];
                return renderer;
            }
            triggerHand = CreateHand("FP trigger glove and sleeve",0);
            supportHand = CreateHand("FP support glove and sleeve",1);
            localWeapon.localPosition = new Vector3(0.08f, -0.10f, 0.40f);
            localWeapon.localRotation = Quaternion.identity;
            localWeapon.gameObject.SetActive(false);
            localLitRenderers=localWeapon.GetComponentsInChildren<Renderer>(true);
            ready = true;
            visualsBound = false;
            Debug.Log("SHOOTER_PRODUCT_ACTOR_VIEW awaiting packaged visuals");
        }

        /// <summary>
        /// Upload verified packaged soldier/rifle/pistol meshes and materials. Missing deps fail recoverably; no generation fallback.
        /// </summary>
        public void BindPackagedVisuals(PackagedVisualPayloads visuals, bool showcase = false)
        {foreach(var step in BindPackagedVisualSteps(visuals,showcase)) { }}
        public IEnumerable<int> BindPackagedVisualSteps(PackagedVisualPayloads visuals, bool showcase = false)
        {
            int epoch=++packagedBindEpoch;
            // Unbind before destroying: the bind yields across frames, and a Sync/avatar/weapon apply in that window
            // would otherwise cache the new weapon meshes against destroyed materials (magenta for the session).
            visualsBound=false;
            ClearAll();
            foreach(var value in owned)DestroyObject(value);owned.Clear();
            bodyMesh=null;Array.Clear(bodyLods,0,bodyLods.Length);contactMesh=null;contactMaterial=null;weaponMeshes.Clear();sightMeshes.Clear();weaponPalettes.Clear();
            weaponMaterials=Array.Empty<Material>();team1Palette=null;team2Palette=null;firstPersonHandMaterials=null;
            skinPrototypes.Clear();restyledMaterials.Clear();team1Gear=null;team2Gear=null;team1Paint=null;team2Paint=null;team1Solid=null;team2Solid=null;surfaceShader=null;
            if (visuals == null) throw new ArgumentNullException(nameof(visuals));
            var shader = Shader.Find("Shooter/MapSurface");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Required locked URP Lit shader missing; no art fallback.");

            bool complete=false;
            try
            {
                var shaderRoot = MapJsonValue.Parse(visuals.Bytes(MapVisualClosure.ShaderPath(visuals.Profile))).Object;
                if (shaderRoot["unityShader"].String != "Universal Render Pipeline/Lit" ||
                    shaderRoot["shaderException"].String != "procedural-urp-lit-v1")
                    throw new InvalidOperationException("Packaged shader provenance rejected.");

                var skeleton = MapVisualClosure.DecodeSoldierSkeleton(visuals);
                if (skeleton.Joints.Count != SoldierSkeleton.JointCount)
                    throw new InvalidOperationException("Packaged soldier skeleton joint count mismatch.");
                byte[] expected = GeometryCodec.EncodeSkeleton(SoldierSkeleton.Definition);
                byte[] actual = GeometryCodec.EncodeSkeleton(skeleton);
                if (expected.Length != actual.Length)
                    throw new InvalidOperationException("Packaged soldier skeleton identity mismatch.");
                for (int i = 0; i < expected.Length; i++)
                    if (expected[i] != actual[i])
                        throw new InvalidOperationException("Packaged soldier skeleton identity mismatch.");

                int soldierLod = showcase ? 0 : MapVisualClosure.SoldierLodIndex(visuals.Profile);
                int weaponLod = showcase ? 0 : MapVisualClosure.WeaponLodIndex(visuals.Profile);
                bodyMesh = Own(Upload(MapVisualClosure.DecodeSoldierLod(visuals, soldierLod), true));
                Array.Clear(bodyLods, 0, bodyLods.Length); bodyBaseLod = soldierLod; bodyLods[soldierLod] = bodyMesh;
                if (!showcase)
                    for (int lod = soldierLod + 1; lod < bodyLods.Length; lod++)
                    { bodyLods[lod] = Own(Upload(MapVisualClosure.DecodeSoldierLod(visuals, lod), true)); yield return 0; }
                weaponMeshes.Clear();
                var silhouettes = new List<WeaponSilhouetteSource>();
                sightMeshes.Clear();
                foreach (var source in WeaponMeshCatalog.All)
                {
                    if (source.IsSight)
                    {
                        // ADR-107: sights parent to the gun at its origin; same LOD policy as the gun meshes.
                        var heldSight = MapVisualClosure.DecodeWeaponLod(visuals, source.MeshKey, weaponLod);
                        Mesh held = Own(Upload(heldSight, false));
                        Mesh near = weaponLod == 0 ? held : Own(Upload(MapVisualClosure.DecodeWeaponLod(visuals, source.MeshKey, 0), false));
                        sightMeshes[source.MeshKey] = (held, near);
                        yield return 0;
                        continue;
                    }
                    var set = new WeaponMeshSet();
                    var heldData = MapVisualClosure.DecodeWeaponLod(visuals, source.MeshKey, weaponLod);
                    set.Held = Own(Upload(heldData, false));
                    // The camera sees equipment at arm's length even on Low. Keep world LOD
                    // scalability without replacing the player's sights/grips with distant proxies.
                    var localData = weaponLod == 0 ? heldData : MapVisualClosure.DecodeWeaponLod(visuals, source.MeshKey, 0);
                    set.Local = weaponLod == 0 ? set.Held : Own(Upload(localData, false));
                    var silhouetteParts = new List<MeshData> { localData };
                    var magazine = MapVisualClosure.DecodeWeaponMagazine(visuals, source.MeshKey);
                    if (magazine != null) { set.Magazine = Own(Upload(magazine, false)); silhouetteParts.Add(magazine); }
                    var cycle = MapVisualClosure.DecodeWeaponCycle(visuals, source.MeshKey);
                    if (cycle != null) { set.Cycle = Own(Upload(cycle, false)); silhouetteParts.Add(cycle); }
                    // ADR-107: a gun's icon includes its factory sight, decoded straight from the package.
                    foreach (var row in WeaponVisualProfiles.All)
                    {
                        if (row.MeshAssetKey != source.MeshKey || !WeaponSightChoices.TryGet(row.DefinitionId, out var factory)) continue;
                        string? sightKey = factory.MeshKeyFor(factory.Default);
                        if (sightKey != null) silhouetteParts.Add(MapVisualClosure.DecodeWeaponLod(visuals, sightKey, 0));
                        break;
                    }
                    // ADR-085: HUD weapon icons are traced from these same packaged LOD0 meshes.
                    silhouettes.Add(new WeaponSilhouetteSource(source.MeshKey, WeaponSilhouetteSource.SpanFor(source.MeshKey), silhouetteParts));
                    if (source.Recipe != null) { var sockets = WeaponGeometry.Sockets(source.Recipe); set.SightSocket = FindSocket(sockets, "sight-eye"); set.MuzzleSocket = FindSocket(sockets, "muzzle"); set.SupportSocket = FindSocket(sockets, "left-hand-grip"); set.HasSupportSocket = true; }
                    weaponMeshes[source.MeshKey] = set;
                    yield return 0;
                }
                Shooter.Presentation.Session.UiIconAtlas.RegisterWeapons(silhouettes);
                for (byte slot = 0; slot < 4; slot++)
                    for (int hand = 0; hand < 2; hand++)
                    {
                        var cameraSkeleton = MapVisualClosure.DecodeFirstPersonHandSkeleton(visuals,slot,hand==1);
                        handSkeletons[slot*2+hand]=cameraSkeleton;
                        firstPersonHandMeshes[slot*2+hand]=Own(Upload(MapVisualClosure.DecodeFirstPersonHand(visuals,slot,hand==1),false,cameraSkeleton));
                        yield return 0;
                    }


                team1Palette = new[]
                {
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[0], "Team1 uniform"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[1], "Team1 gear"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[2], "Team1 skin"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[3], "Team1 goggles", 0.3f, 0.55f),
                    FromPackaged(shader,visuals,MapVisualClosure.SoldierBandPath(1),"Team1 armband"),
                    FromPackaged(shader,visuals,MapVisualClosure.SoldierPatchPath(1),"Team1 shoulder patch")
                };
                yield return 0;
                team2Palette = new[]
                {
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[4], "Team2 uniform"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[5], "Team2 gear"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[6], "Team2 skin"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[7], "Team2 goggles", 0.3f, 0.55f),
                    FromPackaged(shader,visuals,MapVisualClosure.SoldierBandPath(2),"Team2 armband"),
                    FromPackaged(shader,visuals,MapVisualClosure.SoldierPatchPath(2),"Team2 shoulder patch")
                };
                yield return 0;
                // Presentation-only numeric polish: team kits must read in sun/shade without touching generators.
                ApplyPresentationSoldierPalette(team1Palette, team1: true);
                ApplyPresentationSoldierPalette(team2Palette, team1: false);
                // ADR-124: gear slots 0 cover, 1 webbing, 2 lens, 3 hardware reuse the team kit materials.
                team1Gear = new[] { team1Palette[0], team1Palette[1], team1Palette[3], team1Palette[1] };
                team2Gear = new[] { team2Palette[0], team2Palette[1], team2Palette[3], team2Palette[1] };
                // Painted base bodies: uniform, gloves, boots.
                team1Paint = new[] { team1Palette[0], team1Palette[1], team1Palette[1] };
                team2Paint = new[] { team2Palette[0], team2Palette[1], team2Palette[1] };
                // Lighter than the camo kit's average so a uniform read survives shade: olive-slate allies, desert enemies.
                Material Solid(string label, Color color, float smoothness) { var m = Own(new Material(shader) { name = label }); SetLit(m, color, 0f, smoothness); return m; }
                team1Solid = new[] { Solid("Team1 authored uniform", new Color(.46f, .54f, .45f), .26f), Solid("Team1 authored webbing", new Color(.22f, .27f, .25f), .34f) };
                team2Solid = new[] { Solid("Team2 authored uniform", new Color(.86f, .72f, .52f), .24f), Solid("Team2 authored webbing", new Color(.36f, .26f, .17f), .30f) };
                surfaceShader = shader;
                if (UseAuthoredBodies && skinCatalog == null) skinCatalog = SoldierSkinCatalog.Load();

                Material NearFieldSurface(string path, string label) => Shooter.Presentation.Rendering.ProductSurfaceMaterial.Create(
                    shader, MapVisualClosure.DecodeMapMaterial(visuals, path),
                    MapVisualClosure.DecodeMapMaterial(visuals, MapVisualClosure.MaterialCompanionPath(path, "normal")),
                    MapVisualClosure.DecodeMapMaterial(visuals, MapVisualClosure.MaterialCompanionPath(path, "mask")),
                    label, 1f / FirstPersonSurfaceBaker.TileMeters, value => owned.Add(value));
                rifleMetal = NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[0], "Product coated weapon metal");
                yield return 0;
                riflePolymer = NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[1], "Product stippled polymer");
                yield return 0;
                var sleeve = NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[2], "Product woven sleeve");
                yield return 0;
                var glove = NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[3], "Product glove grain");
                yield return 0;
                weaponMaterials = Shooter.Presentation.Rendering.ProductSurfaceMaterial.WeaponFinishes(
                    rifleMetal, riflePolymer,
                    NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[4], "Machined steel bevels and fasteners"),
                    NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[5], "Weapon recesses and rubber"),
                    NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[6], "Olive enamel grenade shell"),
                    NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[FirstPersonSurfaceBaker.BladeSteel], "Brushed blade steel"),
                    NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[FirstPersonSurfaceBaker.BladeEdge], "Polished edge grind"),
                    NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[FirstPersonSurfaceBaker.GrenadePaint], "Olive grenade enamel"),
                    value => owned.Add(value));
                firstPersonHandMaterials = new[] { sleeve, glove, riflePolymer };

                weaponPalettes.Clear();
                visualsBound = true;complete=true;
                ApplyLocalWeaponVisual(WeaponVisualProfiles.DefaultIdForSlot(ProductAuthorityActionView.RifleSlot), ProductAuthorityActionView.RifleSlot);
                Debug.Log("SHOOTER_PRODUCT_ACTOR_VIEW packaged visuals ready profile=" + visuals.Profile +
                          " soldierLod=" + soldierLod + " weaponLod=" + weaponLod);
            }
            finally { if(!complete&&epoch==packagedBindEpoch){visualsBound=false;bodyMesh=null;Array.Clear(bodyLods,0,bodyLods.Length);weaponMeshes.Clear();} }
        }

        public void ClearAll()
        {
            binder.Clear();
            actions.Clear();
            localView.ResetForLife();
            localHandling.Reset(ProductAuthorityActionView.RifleSlot);
            localViewHuman = default;
            foreach (var pair in avatars)
            {
                pair.Value.Authored?.Dispose();
                if (pair.Value.Root != null) DestroyObject(pair.Value.Root.gameObject);
            }
            avatars.Clear();
            ClearDroppedWeapons();
            if (localWeapon != null) localWeapon.gameObject.SetActive(false);
            aggregateMinArmClearance = float.MaxValue;
            aggregateMaxEyeSightError = 0f;
            reviewSampleCount = 0;
            showcaseStates = null;
        }

        /// <summary>Three cosmetic stage actors. These IDs never enter a session, collider, hit registry or authority.</summary>
        public void BindMenuShowcase(PackagedVisualPayloads visuals)
        {
            ClearAll();
            BindPackagedVisuals(visuals, showcase: true);
            showcaseStates = new[]
            {
                new ActorLifecycleState(new EntityId(1), 1, true, 100, new Float3(1.25f, 0, .3f), -2.45f, -.14f, 0, 30, 90),
                new ActorLifecycleState(new EntityId(2), 1, true, 100, new Float3(-.25f, 0, 3.2f), -2.95f, -.06f, 0, 30, 90),
                new ActorLifecycleState(new EntityId(3), 1, true, 100, new Float3(3.2f, 0, 4.8f), -2.4f, -.1f, 0, 30, 90)
            };
            // Menu showcase reuses the same Team1 presentation polish applied in BindPackagedVisuals.
            for (int i = 0; i < showcaseStates.Length; i++) CreateAvatar(showcaseStates[i], false);
        }

        public void TickMenuShowcase(float seconds)
        {
            if (showcaseStates == null) return;
            for (int i = 0; i < showcaseStates.Length; i++)
            {
                var state = showcaseStates[i];
                if (avatars.TryGetValue(state.EntityId.Value, out var avatar)) PoseAvatar(avatar, state, seconds, 0);
            }
        }

        public void SetReviewContext(string mapId, string profile, uint completedTick)
        {
            reviewMapId = mapId ?? "";
            reviewProfile = profile ?? "";
            reviewTick = completedTick;
        }

        /// <summary>Collect live product pose overlay samples for T062 inventory/capture. Does not mutate authority.</summary>
        public int CollectReviewSamples(List<ProductVisualReviewSample> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            foreach (var pair in avatars)
            {
                var avatar = pair.Value;
                if (!avatar.HasReviewMetrics) continue;
                destination.Add(new ProductVisualReviewSample(
                    avatar.EntityId.Value,
                    avatar.Team,
                    !avatar.Corpse,
                    avatar.Corpse,
                    avatar.LastArmBodyClearance,
                    avatar.LastEyeSightLineError,
                    avatar.LastLeftGripError,
                    avatar.LastRightGripError,
                    avatar.LastPlantSlide,
                    avatar.LastStockError,
                    avatar.ReloadPhase,
                    avatar.LastAimPitch,
                    avatar.LastFeet,
                    avatar.LastRightEye,
                    avatar.LastWeaponPosition,
                    avatar.LastSightWorld,
                    reviewTick,
                    reviewMapId,
                    reviewProfile));
            }
            return destination.Count;
        }

        public void Sync(
            MatchViewSnapshot? snapshot,
            IActorStateReader? lifecycle,
            Func<int, ActorLifecycleState>? tryCorpse,
            int corpseCount,
            EntityId humanEntity,
            bool aimHeld,
            float dtSeconds,
            IReadOnlyList<CombatEvent>? combatEvents = null,
            uint completedTick = 0,
            Func<EntityId, LocalMovementView?>? movementView = null,
            bool reducedMotion = false)
        {
            if (!ready || !visualsBound) return;
            localHuman = humanEntity;
            livingScratch.Clear();
            corpseScratch.Clear();

            if (combatEvents != null)
            {
                actions.ApplyEvents(combatEvents);
                for (int i = 0; i < combatEvents.Count; i++)
                {
                    var ev = combatEvents[i];
                    if (ev.Kind == CombatEventKind.ProjectileSpawned && avatars.TryGetValue(ev.Instigator.Value, out var thrower))
                        thrower.ThrowSeconds = 0f;
                    if (ev.Kind != CombatEventKind.ShotAccepted) continue;
                    if (!avatars.TryGetValue(ev.Instigator.Value, out var shooter)) continue;
                    if (ProductAuthorityActionView.IsKnifeSlot(shooter.WeaponSlot)) { shooter.MeleeSeconds = 0f; shooter.MeleeSwings++; }
                    if (FirstPersonWeaponMechanics.HasCyclePart(shooter.WeaponSlot))
                        shooter.CycleOpen = Mathf.Max(shooter.CycleOpen, FirstPersonWeaponMechanics.CycleImpulse);
                }
            }

            if (snapshot != null && lifecycle != null)
            {
                for (int i = 0; i < snapshot.Participants.Count; i++)
                {
                    var p = snapshot.Participants[i];
                    if (!p.Alive || !p.EntityId.IsValid) continue;
                    if (!lifecycle.TryGetActorState(p.EntityId, out var state) || !state.Alive) continue;
                    livingScratch.Add(state);
                    actions.SyncLifecycle(state.EntityId, state.ActiveWeaponSlot, state.Alive);
                    actions.ReconcileReload(state.EntityId, state.WeaponAction, completedTick);
                }
            }

            if (tryCorpse != null)
            {
                for (int i = 0; i < corpseCount && i < ProductActorBinder.MaxCosmeticCorpses; i++)
                {
                    var state = tryCorpse(i);
                    if (state.EntityId.IsValid)
                    {
                        corpseScratch.Add(state);
                        actions.SyncLifecycle(state.EntityId, state.ActiveWeaponSlot, false);
                    }
                }
            }

            binder.Reconcile(livingScratch, corpseScratch, spawnScratch, despawnScratch, corpseEnsureScratch);

            for (int i = 0; i < despawnScratch.Count; i++)
                DestroyAvatar(despawnScratch[i]);
            for (int i = 0; i < spawnScratch.Count; i++)
            {
                ActorLifecycleState state = default;
                for (int j = 0; j < livingScratch.Count; j++)
                    if (livingScratch[j].EntityId.Equals(spawnScratch[i])) { state = livingScratch[j]; break; }
                if (state.EntityId.IsValid) CreateAvatar(state, false);
            }
            for (int i = 0; i < corpseEnsureScratch.Count; i++)
            {
                if (avatars.TryGetValue(corpseEnsureScratch[i].Value, out var existing))
                {
                    existing.Corpse = true;
                    if (!existing.DeathCollapseActive)
                    {
                        existing.DeathCollapseActive = true;
                        existing.DeathPhase = 0f;
                    }
                    actions.SyncLifecycle(existing.EntityId, existing.WeaponSlot, false);
                    continue;
                }
                ActorLifecycleState state = default;
                for (int j = 0; j < corpseScratch.Count; j++)
                    if (corpseScratch[j].EntityId.Equals(corpseEnsureScratch[i])) { state = corpseScratch[j]; break; }
                if (state.EntityId.IsValid) CreateAvatar(state, true);
            }

            foreach (var pair in avatars)
            {
                var avatar = pair.Value;
                ActorLifecycleState state = default;
                bool found = false;
                if (!avatar.Corpse)
                {
                    for (int i = 0; i < livingScratch.Count; i++)
                        if (livingScratch[i].EntityId.Equals(avatar.EntityId)) { state = livingScratch[i]; found = true; break; }
                }
                else
                {
                    for (int i = 0; i < corpseScratch.Count; i++)
                        if (corpseScratch[i].EntityId.Equals(avatar.EntityId)) { state = corpseScratch[i]; found = true; break; }
                }
                if (!found) continue;
                PoseAvatar(avatar, state, dtSeconds, completedTick, movementView?.Invoke(avatar.EntityId));
            }

            ActorLifecycleState human = default;
            bool showLocalWeapon = humanEntity.IsValid
                && lifecycle != null
                && lifecycle.TryGetActorState(humanEntity, out human)
                && human.Alive;
            if (localWeapon != null) localWeapon.gameObject.SetActive(showLocalWeapon);
            localWeaponScopeHidden = false;
            if (showLocalWeapon)
            {
                float visibility=AmbientVisibility?.Invoke(human.Feet)??1f;foreach(var renderer in localLitRenderers)ApplyAmbient(renderer,visibility);
                byte slot = human.ActiveWeaponSlot;
                DefinitionId weaponId = human.ActiveWeaponDefinition.IsValid
                    ? human.ActiveWeaponDefinition
                    : WeaponVisualProfiles.DefaultIdForSlot(slot);
                if (!localViewHuman.Equals(human.EntityId)) Array.Clear(localSlotWeaponIds, 0, localSlotWeaponIds.Length);
                if (slot < localSlotWeaponIds.Length && weaponId.IsValid) localSlotWeaponIds[slot] = weaponId;
                if (!localViewHuman.Equals(human.EntityId))
                {
                    localView.ResetForLife(slot);
                    localHandling.Reset(slot);
                    localLocomotion.Reset();
                    hasLookSample = false;
                    localViewHuman = human.EntityId;
                }
                actions.SyncLifecycle(human.EntityId, slot, true);
                localView.SetActiveWeaponSlot(slot);
                localView.SetCooking(human.WeaponAction == WeaponAction.Cooking);
                // A reload lowers the sights (presentation only): scoped ADS hides the rifle mesh, which would
                // otherwise make the reload invisible.
                bool viewAim = FirstPersonHandling.ViewAimTarget(aimHeld, actions.IsReloading(human.EntityId),
                    human.WeaponAction == WeaponAction.Reloading, slot);
                var activeProfile = Sighted(WeaponVisualProfiles.Resolve(weaponId, slot), SightOf(human.EntityId, slot)).WithHandling(LocalHandling);
                localView.ApplyProfile(activeProfile);
                if (combatEvents != null)
                    localView.ConsumeEvents(combatEvents, human.EntityId, viewAim, dtSeconds);
                else
                    localView.TickAim(viewAim, dtSeconds);
                // Authority wins over an older action event on a switch/cancel frame.
                localView.SetActiveWeaponSlot(slot);
                localView.SetCooking(human.WeaponAction == WeaponAction.Cooking);
                var pose = localView.Pose;
                var movement = movementView?.Invoke(human.EntityId);
                // ADR-099: the switch fills the incoming weapon's authoritative equip window; a throw follow-through
                // holds the outgoing grenade on screen until the hand has left the view.
                localHandling.Step(human.Feet, slot, dtSeconds, movement.HasValue && movement.Value.Sliding, reducedMotion,
                    activeProfile.EquipSeconds, holdOutgoing: localView.Throwing);
                // Mesh follows the holster→draw visible slot so the outgoing gun finishes tucking first.
                byte shown = localHandling.VisibleWeaponSlot;
                // Mid-switch keeps the outgoing slot's last known id (each life draws its own guns).
                DefinitionId shownId = shown == slot
                    ? weaponId
                    : shown < localSlotWeaponIds.Length && localSlotWeaponIds[shown].IsValid
                        ? localSlotWeaponIds[shown]
                        : WeaponVisualProfiles.DefaultIdForSlot(shown);
                localHandling.VisibleWeaponId = shownId;
                localVisibleWeaponSlot = shown;
                localVisibleWeaponId = shownId;
                DefinitionId shownSight = SightOf(human.EntityId, shown);
                localVisibleSight = shownSight;
                var profile = Sighted(WeaponVisualProfiles.Resolve(shownId, shown), shownSight).WithHandling(LocalHandling);
                ApplyLocalWeaponVisual(shownId, shown, shownSight);
                float reload = localHandling.IsSwitching ? -1f : actions.ReloadPhaseAtTick(human.EntityId, completedTick);
                float recoilEnergy = Math.Abs(pose.RecoilPitch) + Math.Abs(pose.RecoilYaw);
                // Ammo refills only at ReloadCompleted, so an empty magazine marks an empty reload for its whole window.
                bool magazineEmpty = human.Magazine == 0 && shown == slot;
                float adsPresent = FirstPersonHandling.AdsPresentationBlend(pose.AdsBlend, localHandling.EquipBlend);
                localAdsPresentation = adsPresent;
                var reloadPose = FirstPersonReloadChoreography.Evaluate(profile, reload, magazineEmpty,
                    profile.ReloadSeconds, reducedMotion);
                var offset = localHandling.PositionOffset(adsPresent, reload, reducedMotion, recoilEnergy, magazineEmpty);
                var rotation = localHandling.RotationDegrees(adsPresent, reload, reducedMotion, recoilEnergy, magazineEmpty);
                var specialPos = localView.SpecialActionPosition(reducedMotion);
                var specialRot = localView.SpecialActionRotation(reducedMotion);
                var kick = localView.EquipmentKickMeters(reducedMotion);

                // Movement layers: sprint carry, jump lift / landing dip, crouch tuck, slide tilt and look inertia.
                float lookYawDelta = 0f, lookPitchDelta = 0f;
                if (fpsCamera != null)
                {
                    Vector3 forward = fpsCamera.transform.forward;
                    float lookYaw = Mathf.Atan2(forward.x, forward.z);
                    float lookPitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f));
                    if (hasLookSample)
                    {
                        lookYawDelta = Mathf.DeltaAngle(lastLookYaw * Mathf.Rad2Deg, lookYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        lookPitchDelta = lookPitch - lastLookPitch;
                    }
                    lastLookYaw = lookYaw;
                    lastLookPitch = lookPitch;
                    hasLookSample = true;
                }
                var locomotionInput = movement.HasValue
                    ? new FirstPersonLocomotionInput(movement.Value.Sprinting, movement.Value.Grounded,
                        movement.Value.Stance == Stance.Crouching, movement.Value.Sliding,
                        movement.Value.VerticalSpeedMetersPerSecond, movement.Value.LocomotionDistanceMeters,
                        movement.Value.HorizontalSpeedMetersPerSecond)
                    : FirstPersonLocomotionInput.Standing;
                bool busy = reload > 0f || localHandling.IsSwitching || localView.Throwing || localView.Cooking
                    || localView.MeleePhase01 > 0f || localView.BoltCycling || recoilEnergy > 0.02f;
                localLocomotion.Step(locomotionInput, dtSeconds, adsPresent, busy, lookYawDelta, lookPitchDelta);
                var moveOffset = localLocomotion.PositionOffset(shown, adsPresent, reducedMotion);
                var moveRotation = localLocomotion.RotationDegrees(shown, adsPresent, reducedMotion);

                Float3 sight = SightEyeFor(profile, shownSight);
                float scale = FirstPersonHandling.NearFieldScale(profile);
                Float3 hipBias = FirstPersonHandling.NearFieldHipBias(profile);
                Vector3 hip = new Vector3(pose.RightHand.X, pose.RightHand.Y, pose.RightHand.Z);
                // Keep the stock behind the receiver clear of the camera near plane. These
                // camera-local offsets affect presentation only; ballistic origin stays authoritative.
                // Stronger near-field hip so rifle/pistol read with Bodycam mass without breaking ADS truth.
                hip += new Vector3(hipBias.X, hipBias.Y, hipBias.Z);
                // Scale the sight socket with near-field mass so TransformPoint still lands on the camera ray.
                Vector3 aligned = new Vector3(-sight.X * scale, -sight.Y * scale, hip.z);
                localWeapon.localScale = new Vector3(scale, scale, scale);
                localWeapon.localPosition = Vector3.Lerp(hip, aligned, adsPresent) + V(offset) + V(kick) + V(specialPos)
                    + V(moveOffset);
                float rotKick = FirstPersonHandling.RecoilRotationScale(adsPresent, reducedMotion);
                var composition = FirstPersonHandling.HipRotationDegrees(profile, adsPresent);
                localWeapon.localRotation = Quaternion.Euler(
                    rotation.X + composition.X + specialRot.X + moveRotation.X + pose.RecoilPitch * Mathf.Rad2Deg * rotKick,
                    rotation.Y + composition.Y + specialRot.Y + moveRotation.Y + pose.RecoilYaw * Mathf.Rad2Deg * rotKick,
                    rotation.Z + composition.Z + specialRot.Z + moveRotation.Z);
                bool gun = FirstPersonWeaponMechanics.HasMagazinePart(profile);
                // Bolt/slide: reload choreography wins, then lock-back on an empty magazine, then the shot cycle.
                float cycle = reloadPose.CycleOverride01 >= 0f ? reloadPose.CycleOverride01
                    : gun && magazineEmpty ? 1f : localView.CycleOpen01;
                float trigger = gun && reload <= 0f ? localView.CycleOpen01 : 0f;
                PoseLocalHands(profile, shown, reloadPose, localView.SpecialSupportWrist, localView.SpecialSupportOpen, trigger);
                // ADR-111: one-handed knife; the grenade rests in the throwing hand and the support hand appears for the pin.
                supportHand!.enabled = FirstPersonSpecialActions.SupportHandVisible(shown, localView.Cooking, localView.CookSeconds);
                StepBladeTrail(shown, reducedMotion, dtSeconds);
                StepGrenadeArc(ProductAuthorityActionView.IsGrenadeSlot(shown) && localView.Cooking && !localView.Throwing,
                    profile.FuseSeconds - localView.CookSeconds);
                PoseLocalMechanicalParts(profile, reloadPose, cycle, reducedMotion);
                // Full scoped ADS uses the analytic scope overlay — hide the near-field mesh so the lens stays clean.
                // A released grenade hides only the prop; the hands finish the follow-through.
                var weaponRenderer = localWeapon.GetComponent<MeshRenderer>();
                weaponRenderer.enabled = !(ProductAuthorityActionView.IsGrenadeSlot(shown) && localView.HideHeldGrenade(human.Magazine));
                if (AdsOpticVisual.HideRifleViewModel(profile, adsPresent))
                {
                    localWeapon.gameObject.SetActive(false);
                    // Still posed on the sight line: the muzzle flash anchors to it so it shows in the scope lens.
                    localWeaponScopeHidden = true;
                }
            localLitRenderers=localWeapon.GetComponentsInChildren<Renderer>(true);
            }
            else if (localViewHuman.IsValid)
            {
                grenadeArc?.Step(false, default, default, default, default, 0f, 0f);
                localView.ResetForLife();
                localHandling.Reset(ProductAuthorityActionView.RifleSlot);
                localLocomotion.Reset();
                hasLookSample = false;
                localAdsPresentation = 0f;
                localVisibleWeaponSlot = ProductAuthorityActionView.RifleSlot;
                localVisibleSight = default;
                localViewHuman = default;
            }

            RefreshReviewAggregates();
        }

        private void RefreshReviewAggregates()
        {
            aggregateMinArmClearance = float.MaxValue;
            aggregateMaxEyeSightError = 0f;
            reviewSampleCount = 0;
            foreach (var pair in avatars)
            {
                var avatar = pair.Value;
                if (!avatar.HasReviewMetrics || avatar.Corpse) continue;
                aggregateMinArmClearance = Mathf.Min(aggregateMinArmClearance, avatar.LastArmBodyClearance);
                aggregateMaxEyeSightError = Mathf.Max(aggregateMaxEyeSightError, avatar.LastEyeSightLineError);
                reviewSampleCount++;
            }
            if (reviewSampleCount == 0) aggregateMinArmClearance = float.NaN;
        }

        private void CreateAvatar(ActorLifecycleState state, bool corpse)
        {
            if (avatars.ContainsKey(state.EntityId.Value)) return;
            if (!visualsBound || motionRecipe == null || bodyMesh == null || weaponMeshes.Count == 0) return;

            byte slot = state.ActiveWeaponSlot;
            actions.SyncLifecycle(state.EntityId, slot, state.Alive && !corpse);
            var avatar = new Avatar
            {
                EntityId = state.EntityId,
                Team = state.Team,
                Corpse = corpse,
                WeaponSlot = slot,
                DeathCollapseActive = corpse,
                DeathPhase = corpse ? 0f : -1f,
                Root = new GameObject("Actor " + state.EntityId.Value).transform,
                Bones = new Transform[SoldierSkeleton.JointCount],
                // ADR-095 v6 shooting posture (rifle-validated sockets; per-profile sockets follow via SetVisualWeapon).
                Motion = new SoldierMotion(motionRecipe,new ShootingPosture()),
                PreviousFeet = state.Feet,
                DisplayedFeet = state.Feet,
                PreviousHealth = state.Health
            };
            avatar.Root.SetParent(transform, false);
            var skeleton = SoldierSkeleton.Definition;
            // ADR-124: the semantic bones stay (unrendered under an authored body) as the SoldierMotion upper-body task.
            for (int j = 0; j < SoldierSkeleton.JointCount; j++)
            {
                var definition = skeleton.Joints[j];
                var bone = new GameObject(SoldierSkeleton.Names[j]).transform;
                avatar.Bones[j] = bone;
                bone.SetParent(definition.ParentId == ushort.MaxValue ? avatar.Root : avatar.Bones[definition.ParentId], false);
                bone.localPosition = V(definition.LocalBindPosition);
                bone.localRotation = Quaternion.identity;
            }

            if (UseAuthoredBodies)
                avatar.Authored = CreateAuthoredBody(avatar.Root, state.EntityId.Value, state.Team);
            else
            {
                avatar.Body = avatar.Root.gameObject.AddComponent<SkinnedMeshRenderer>();
                avatar.Body.sharedMesh = bodyMesh;
                avatar.Body.bones = avatar.Bones;
                avatar.Body.rootBone = avatar.Bones[0];
                avatar.Body.sharedMaterials = state.Team == 1 ? team1Palette : team2Palette;
                avatar.Body.quality = SkinQuality.Bone4;
                avatar.Body.updateWhenOffscreen = true;
                avatar.Body.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(4, 4, 4));
            }

            avatar.Weapon = new GameObject("Held weapon").transform;
            avatar.Weapon.SetParent(avatar.Root, false);
            avatar.WeaponFilter = avatar.Weapon.gameObject.AddComponent<MeshFilter>();
            avatar.WeaponRenderer = avatar.Weapon.gameObject.AddComponent<MeshRenderer>();
            avatar.MagazineFilter = CreateWeaponChild(avatar.Weapon, "Held magazine", out avatar.MagazinePart);
            avatar.MagazineRenderer = avatar.MagazinePart!.GetComponent<MeshRenderer>();
            avatar.CycleFilter = CreateWeaponChild(avatar.Weapon, "Held cycle", out avatar.CyclePart);
            avatar.CycleRenderer = avatar.CyclePart!.GetComponent<MeshRenderer>();
            avatar.SightFilter = CreateWeaponChild(avatar.Weapon, "Held sight", out avatar.SightPart);
            avatar.SightRenderer = avatar.SightPart!.GetComponent<MeshRenderer>();
            avatar.Sight = SightOf(state.EntityId, slot);
            DefinitionId createId = state.ActiveWeaponDefinition.IsValid
                ? state.ActiveWeaponDefinition
                : WeaponVisualProfiles.DefaultIdForSlot(slot);
            avatar.WeaponDefinition = createId;
            ApplyHeldWeaponVisual(avatar, createId, slot);
            ApplyLocalHumanShadowMode(avatar, state.EntityId.Equals(localHuman) && !corpse && state.Alive);

            avatars[state.EntityId.Value] = avatar;
            PoseAvatar(avatar, state, 0f, 0u);
        }

        /// <summary>
        /// ADR-106 distance LOD (presentation only): finest packaged LOD within 15 m, the next to 40 m, the coarsest
        /// beyond, never finer than the tier's own LOD; 1.5 m hysteresis keeps meshes from flickering at the boundary.
        /// </summary>
        private void SelectBodyLod(Avatar avatar)
        {
            float distance = fpsCamera != null ? Vector3.Distance(fpsCamera.transform.position, avatar.Root.position) : 0f;
            if (avatar.Authored != null)
            {
                int was = avatar.BodyLod < 0 ? bodyBaseLod : avatar.BodyLod;
                const float hysteresis = 1.5f;
                int tier = distance > 40f + (was >= 2 ? -hysteresis : hysteresis) ? 2 : distance > 15f + (was >= 1 ? -hysteresis : hysteresis) ? 1 : 0;
                avatar.BodyLod = Mathf.Max(tier, bodyBaseLod);
                avatar.Authored.SetLod(avatar.BodyLod);
                return;
            }
            if (avatar.Body == null || bodyLods[bodyBaseLod] == null) return;
            int current = avatar.BodyLod < 0 ? bodyBaseLod : avatar.BodyLod;
            float bias = 1.5f;
            int wanted = distance > 40f + (current >= 2 ? -bias : bias) ? 2 : distance > 15f + (current >= 1 ? -bias : bias) ? 1 : 0;
            wanted = Mathf.Clamp(wanted, bodyBaseLod, bodyLods.Length - 1);
            while (wanted > bodyBaseLod && bodyLods[wanted] == null) wanted--;
            if (wanted == avatar.BodyLod) return;
            avatar.BodyLod = wanted;
            avatar.Body.sharedMesh = bodyLods[wanted];
        }

        private void PoseAvatar(Avatar avatar, ActorLifecycleState state, float dtSeconds, uint completedTick, LocalMovementView? movement = null)
        {
            float speed = 0f;
            if (avatar.HasPrevious && !avatar.Corpse && state.Alive)
            {
                float dx = state.Feet.X - avatar.PreviousFeet.X;
                float dy = state.Feet.Y - avatar.PreviousFeet.Y;
                float dz = state.Feet.Z - avatar.PreviousFeet.Z;
                float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                avatar.Distance += dist;
                if (dtSeconds > 1e-4f) speed = Mathf.Clamp(dist / dtSeconds, 0f, 5.5f);
            }

            // Prefer authoritative motor speed/distance when the session view is available (stable gait).
            float slide = 0f;
            bool crouching = false;
            var locomotion = avatar.Locomotion;
            if (movement.HasValue && !avatar.Corpse && state.Alive)
            {
                var m = movement.Value;
                speed = Mathf.Clamp(m.HorizontalSpeedMetersPerSecond, 0f, 5.5f);
                avatar.Distance = m.LocomotionDistanceMeters;
                crouching = m.Stance == Stance.Crouching && !m.Sliding;
                // ADR-100: travel heading (side-steps/backpedal), airborne tuck, landing dip and eased slide.
                locomotion.Step(dtSeconds, true, m.Grounded, m.VerticalSpeedMetersPerSecond,
                    m.VelocityXMetersPerSecond, m.VelocityZMetersPerSecond, m.Sliding);
                slide = locomotion.Slide;
            }
            else
                locomotion.Reset();

            byte slot = state.ActiveWeaponSlot;
            DefinitionId weaponId = state.ActiveWeaponDefinition.IsValid
                ? state.ActiveWeaponDefinition
                : WeaponVisualProfiles.DefaultIdForSlot(slot);
            var heldProfile = WeaponVisualProfiles.Resolve(weaponId, slot);
            actions.SyncLifecycle(state.EntityId, slot, state.Alive && !avatar.Corpse);
            // A corpse keeps the sight it died with; the retired actor no longer reports one.
            DefinitionId heldSight = avatar.Corpse || !state.Alive ? avatar.Sight : SightOf(state.EntityId, slot);
            if (slot != avatar.WeaponSlot || !avatar.WeaponDefinition.Equals(weaponId) || !avatar.Sight.Equals(heldSight))
            {
                avatar.WeaponSlot = slot;
                avatar.WeaponDefinition = weaponId;
                avatar.Sight = heldSight;
                ApplyHeldWeaponVisual(avatar, weaponId, slot);
            }

            // Authority ReloadStarted/Completed only — never magazine decrease.
            if (!avatar.Corpse && state.Alive)
                avatar.ReloadPhase = actions.ResolveReloadPhase(state.EntityId, completedTick, dtSeconds);
            else
            {
                avatar.ReloadPhase = -1f;
                actions.SyncLifecycle(state.EntityId, slot, false);
            }
            // World cycle follows the same cosmetic close rate as the local FP spring.
            if (!avatar.Corpse && state.Alive && FirstPersonWeaponMechanics.HasCyclePart(heldProfile))
                avatar.CycleOpen = Mathf.MoveTowards(avatar.CycleOpen, 0f, dtSeconds / 0.09f);
            else
                avatar.CycleOpen = 0f;

            bool dying = avatar.Corpse || !state.Alive;
            if (dying)
            {
                if (!avatar.DeathCollapseActive)
                {
                    avatar.DeathCollapseActive = true;
                    avatar.DeathPhase = 0f;
                }
                else
                    avatar.DeathPhase = Mathf.Min(1f, avatar.DeathPhase + dtSeconds / SoldierMotion.DeathCollapseDurationSeconds);
                speed = 0f;
                slide = 0f;
            }

            // Reset only on first living pose or the first death-collapse frame (clears gait contacts).
            bool reset = !avatar.HasPrevious || (dying && avatar.DeathPhase <= 1e-4f);
            float yaw = state.AimYaw;
            float pitch = dying
                ? 0f
                : Mathf.Clamp(state.AimPitch, -SoldierMotion.MaxAimPitchRadians, SoldierMotion.MaxAimPitchRadians);
            float aimYaw = Mathf.Clamp(0f, -SoldierMotion.MaxAimYawRadians, SoldierMotion.MaxAimYawRadians);

            // Cosmetic root blend toward authoritative feet (hit capsules stay tick-snapped). ADR-112: the displayed root
            // first advances by the authoritative horizontal velocity, so a moving body no longer trails its hit
            // capsules by speed/18 m (0.3 m at sprint); the blend then only removes the per-tick stair-step.
            Float3 displayFeet = state.Feet;
            if (avatar.HasPrevious && !dying && dtSeconds > 1e-4f)
            {
                Float3 from = avatar.DisplayedFeet;
                if (movement.HasValue)
                {
                    var mv = movement.Value;
                    from = new Float3(from.X + mv.VelocityXMetersPerSecond * dtSeconds, from.Y, from.Z + mv.VelocityZMetersPerSecond * dtSeconds);
                }
                float blend = Mathf.Clamp01(dtSeconds * 18f);
                displayFeet = new Float3(
                    Mathf.Lerp(from.X, state.Feet.X, blend),
                    Mathf.Lerp(from.Y, state.Feet.Y, blend),
                    Mathf.Lerp(from.Z, state.Feet.Z, blend));
                // Never drift more than a sprint tick away from the authoritative feet (teleports, stops).
                float ex = displayFeet.X - state.Feet.X, ez = displayFeet.Z - state.Feet.Z;
                float err = Mathf.Sqrt(ex * ex + ez * ez);
                const float maxError = 0.25f;
                if (err > maxError)
                    displayFeet = new Float3(state.Feet.X + ex * maxError / err, displayFeet.Y, state.Feet.Z + ez * maxError / err);
            }
            avatar.DisplayedFeet = displayFeet;

            var input = new SoldierMotionInput(
                displayFeet,
                yaw,
                avatar.Distance,
                dying ? 0f : speed,
                crouching ? 1f : 0f,
                aimYaw,
                pitch,
                avatar.ReloadPhase,
                reset,
                dying ? avatar.DeathPhase : -1f,
                slide,
                dying ? float.NaN : locomotion.HeadingRadians,
                dying ? 0f : locomotion.Airborne,
                Mathf.Clamp(movement.HasValue ? movement.Value.VerticalSpeedMetersPerSecond : 0f, -30f, 30f),
                dying ? -1f : locomotion.Land);
            avatar.Root.position = V(displayFeet);
            SelectBodyLod(avatar);
            float visibility=AmbientVisibility?.Invoke(displayFeet)??1f;
            ApplyAmbient(avatar.Body,visibility);ApplyAmbient(avatar.WeaponRenderer,visibility);ApplyAmbient(avatar.MagazineRenderer,visibility);ApplyAmbient(avatar.CycleRenderer,visibility);ApplyAmbient(avatar.SightRenderer,visibility);
            if(avatar.Contact==null)avatar.Contact=CreateContact(avatar.Root);
            avatar.Contact.SetActive(QualitySettings.GetQualityLevel()==0&&!dying);
            avatar.Contact.transform.position=V(new Float3(displayFeet.X,displayFeet.Y+.025f,displayFeet.Z));
            if(CollisionGround!=null)CollisionGround.ReferenceHeight=state.Feet.Y;
            footGround.Primary = CollisionGround;
            footGround.Height = state.Feet.Y;
            var pose = avatar.Motion.Evaluate(input, footGround);
            for (int j = 0; j < SoldierSkeleton.JointCount; j++)
                avatar.Bones[j].SetPositionAndRotation(V(pose.WorldPositions[j]), Q(pose.WorldRotations[j]));

            Float3 weaponPosition = pose.WeaponPosition;
            Quat weaponRotation = pose.WeaponRotation;
            if (heldProfile.Family != WeaponVisualFamily.PrimaryGun && !avatar.Corpse && state.Alive)
                weaponPosition = pose.WorldPositions[RightHandJoint];
            ApplyThirdPersonActionOverlay(avatar, slot, state.WeaponAction == WeaponAction.Cooking, dtSeconds, ref weaponPosition, ref weaponRotation);
            if (avatar.Authored != null)
            {
                // ADR-124: clips drive legs/hips/spine; the solved upper body and the weapon ride the authored chest.
                float localHeading = float.IsNaN(locomotion.HeadingRadians) || dying ? 0f
                    : Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, locomotion.HeadingRadians * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                Vector3 shifted = V(weaponPosition);
                var grip = avatar.MeleeSeconds >= 0f || avatar.ThrowSeconds >= 0f ? SoldierGrip.None
                    : heldProfile.Family == WeaponVisualFamily.PrimaryGun ? SoldierGrip.Rifle
                    : heldProfile.Family == WeaponVisualFamily.Sidearm ? SoldierGrip.Pistol : SoldierGrip.None;
                avatar.Authored.Pose(dtSeconds, dying ? 0f : speed, localHeading, crouching || slide > .5f ? 1f : 0f, dying ? 0f : locomotion.Airborne, dying,
                    avatar.Bones, ref shifted, Q(weaponRotation), grip);
                weaponPosition = new Float3(shifted.x, shifted.y, shifted.z);
                foreach (var renderer in avatar.Authored.Renderers) ApplyAmbient(renderer, visibility);
            }

            avatar.Weapon.SetPositionAndRotation(V(weaponPosition), Q(weaponRotation));
            avatar.Weapon.gameObject.SetActive(!avatar.Corpse && state.Alive);
            PoseHeldMechanicalParts(avatar, heldProfile, state.Magazine == 0);
            // Re-assert each pose so respawn / corpse flip keeps local FP body+weapon ShadowsOnly.
            ApplyLocalHumanShadowMode(avatar, state.EntityId.Equals(localHuman) && !avatar.Corpse && state.Alive);

            Float3 sightLocal = SightEyeFor(heldProfile, avatar.Sight);
            avatar.LastArmBodyClearance = pose.ArmBodyClearanceMeters;
            avatar.LastEyeSightLineError = pose.EyeSightLineErrorMeters;
            avatar.LastLeftGripError = pose.LeftGripError;
            avatar.LastRightGripError = pose.RightGripError;
            avatar.LastPlantSlide = pose.PlantSlide;
            avatar.LastStockError = pose.RightStockErrorMeters;
            avatar.LastAimPitch = pitch;
            avatar.LastRightEye = pose.RightEyePosition;
            avatar.LastWeaponPosition = weaponPosition;
            Vector3 sightWorld = V(weaponPosition) + Q(weaponRotation) * V(sightLocal);
            avatar.LastSightWorld = new Float3(sightWorld.x, sightWorld.y, sightWorld.z);
            avatar.LastFeet = state.Feet;
            avatar.HasReviewMetrics = true;

            avatar.PreviousFeet = state.Feet;
            avatar.PreviousHealth = state.Health;
            avatar.HasPrevious = true;
            if (avatar.Authored != null && avatar.Team != state.Team)
            {
                var variant = avatar.Authored.Shape.Variant; byte team = state.Team;
                avatar.Authored.SetMaterials(m => RestyleAuthored(m, team, variant), (team == 1 ? team1Gear : team2Gear)!, (team == 1 ? team1Paint : team2Paint)!);
            }
            avatar.Team = state.Team;
            if (avatar.Body != null)
                avatar.Body.sharedMaterials = state.Team == 1 ? team1Palette : team2Palette;
        }

        private void ApplyHeldWeaponVisual(Avatar avatar, DefinitionId weaponId, byte slot)
        {
            var profile = WeaponVisualProfiles.Resolve(weaponId, slot);
            var meshDefinition=WeaponMeshCatalog.Require(profile.MeshAssetKey);
            // ADR-107: the head solves onto the mounted sight's line, not the bare rail.
            if(meshDefinition.Recipe!=null)avatar.Motion.SetVisualWeapon(meshDefinition.Recipe,
                WeaponSightChoices.TryGet(profile.DefinitionId,out _)?SightEyeFor(profile,avatar.Sight):(Float3?)null);
            if (avatar.SightFilter != null && avatar.SightPart != null)
            {
                avatar.SightFilter.sharedMesh = slot == ProductAuthorityActionView.RifleSlot ? SightMeshFor(profile.DefinitionId, avatar.Sight, false) : null;
                if (avatar.SightRenderer != null) avatar.SightRenderer.sharedMaterials = WeaponPalette(avatar.SightFilter.sharedMesh);
                avatar.SightPart.gameObject.SetActive(avatar.SightFilter.sharedMesh != null);
                avatar.SightPart.localPosition = Vector3.zero;
            }
            avatar.Weapon.name = WeaponDisplayName(profile, false);
            avatar.WeaponFilter.sharedMesh = HeldMeshForProfile(profile);
            avatar.WeaponRenderer.sharedMaterials = WeaponPalette(avatar.WeaponFilter.sharedMesh);
            bool guns = FirstPersonWeaponMechanics.HasMagazinePart(profile);
            if (avatar.MagazineFilter != null && avatar.MagazinePart != null)
            {
                avatar.MagazineFilter.sharedMesh = guns ? HeldMagazineForProfile(profile) : null;
                if (avatar.MagazineRenderer != null)
                    avatar.MagazineRenderer.sharedMaterials = WeaponPalette(avatar.MagazineFilter.sharedMesh);
                avatar.MagazinePart.gameObject.SetActive(guns);
                avatar.MagazinePart.localPosition = Vector3.zero;
            }
            if (avatar.CycleFilter != null && avatar.CyclePart != null)
            {
                avatar.CycleFilter.sharedMesh = guns ? HeldCycleForProfile(profile) : null;
                if (avatar.CycleRenderer != null)
                    avatar.CycleRenderer.sharedMaterials = WeaponPalette(avatar.CycleFilter.sharedMesh);
                avatar.CyclePart.gameObject.SetActive(guns);
                avatar.CyclePart.localPosition = Vector3.zero;
            }
            avatar.CycleOpen = 0f;
        }

        private void ApplyLocalWeaponVisual(DefinitionId weaponId, byte slot, DefinitionId sight = default)
        {
            if (localWeapon == null || !visualsBound) return;
            var profile = WeaponVisualProfiles.Resolve(weaponId, slot);
            localWeapon.name = WeaponDisplayName(profile, true);
            if (localSightFilter != null && localSight != null)
            {
                Mesh? sightMesh = slot == ProductAuthorityActionView.RifleSlot ? SightMeshFor(profile.DefinitionId, sight, true) : null;
                localSightFilter.sharedMesh = sightMesh;
                localSight.GetComponent<MeshRenderer>().sharedMaterials = WeaponPalette(sightMesh);
                localSight.gameObject.SetActive(sightMesh != null);
                localSight.localPosition = Vector3.zero;
            }
            localWeapon.GetComponent<MeshFilter>().sharedMesh = LocalMeshForProfile(profile);
            localWeapon.GetComponent<MeshRenderer>().sharedMaterials = WeaponPalette(localWeapon.GetComponent<MeshFilter>().sharedMesh);
            bool guns = FirstPersonWeaponMechanics.HasMagazinePart(profile);
            if (localMagazineFilter != null && localMagazine != null)
            {
                localMagazineFilter.sharedMesh = guns ? LocalMagazineForProfile(profile) : null;
                localMagazine.GetComponent<MeshRenderer>().sharedMaterials = WeaponPalette(localMagazineFilter!.sharedMesh);
                localMagazine.gameObject.SetActive(guns);
                localMagazine.localPosition = Vector3.zero;
            }
            if (localFreshMagazineFilter != null && localFreshMagazine != null)
            {
                localFreshMagazineFilter.sharedMesh = guns ? LocalMagazineForProfile(profile) : null;
                localFreshMagazine.GetComponent<MeshRenderer>().sharedMaterials = WeaponPalette(localFreshMagazineFilter!.sharedMesh);
                localFreshMagazine.gameObject.SetActive(false);
                localFreshMagazine.localPosition = Vector3.zero;
            }
            if (localCycleFilter != null && localCycle != null)
            {
                localCycleFilter.sharedMesh = guns ? LocalCycleForProfile(profile) : null;
                localCycle.GetComponent<MeshRenderer>().sharedMaterials = WeaponPalette(localCycleFilter!.sharedMesh);
                localCycle.gameObject.SetActive(guns);
                localCycle.localPosition = Vector3.zero;
            }
            if (triggerHand != null && supportHand != null)
            {
                int index = slot * 2;
                if (triggerHand.sharedMesh != firstPersonHandMeshes[index])
                {
                    triggerHand.sharedMesh = firstPersonHandMeshes[index];
                    supportHand.sharedMesh = firstPersonHandMeshes[index + 1];
                    triggerHand.sharedMaterials = firstPersonHandMaterials;
                    supportHand.sharedMaterials = firstPersonHandMaterials;
                    for (int hand=0;hand<2;hand++)
                    {
                        var skeleton=handSkeletons[index+hand]!;
                        for(int j=0;j<FirstPersonHandRig.JointCount;j++)
                        {
                            var definition=skeleton.Joints[j]; var bone=handBones[hand][j];
                            bone.SetParent(definition.ParentId==ushort.MaxValue ? (hand==0?triggerHand.transform:supportHand.transform) : handBones[hand][definition.ParentId],false);
                            bone.localPosition=V(definition.LocalBindPosition); bone.localRotation=Q(definition.LocalBindRotation);
                        }
                    }
                }
                // ADR-102: the hand meshes are baked per slot for the classic rifle (handguard grip at z 340 mm) and
                // pistol; each gun moves the support hand onto its own left-hand-grip socket.
                supportHand.transform.localPosition = V(SupportHandShift(profile, slot));
            }
        }

        private static readonly Float3 RifleSupportBind = new Float3(0f, .010f, .340f);
        private static readonly Float3 PistolSupportBind = new Float3(-.022f, 0f, -.005f);

        private Float3 SupportHandShift(in WeaponVisualProfile profile, byte slot)
        {
            if (slot > 1 || !weaponMeshes.TryGetValue(profile.MeshAssetKey, out var set) || !set.HasSupportSocket) return default;
            var bind = slot == 0 ? RifleSupportBind : PistolSupportBind;
            return new Float3(set.SupportSocket.X - bind.X, set.SupportSocket.Y - bind.Y, set.SupportSocket.Z - bind.Z);
        }

        private void PoseLocalHands(in WeaponVisualProfile profile, byte slot, in ReloadPose reloadPose,
            Float3 specialWrist, float specialOpen, float trigger)
        {
            if(triggerHand==null || supportHand==null) return;
            bool gun=FirstPersonWeaponMechanics.HasMagazinePart(profile);
            // ADR-099: the support hand follows the reload choreography (magazine, belt, bolt catch / slide) or the
            // grenade pin pull; the elbow stays on its bind so the sleeve stretches between them (ADR-069).
            Float3 reach=new Float3(reloadPose.SupportWristOffset.X+specialWrist.X,reloadPose.SupportWristOffset.Y+specialWrist.Y,
                reloadPose.SupportWristOffset.Z+specialWrist.Z);
            float supportOpen=gun ? Math.Max(reloadPose.SupportOpen01, specialOpen) : specialOpen;
            for(int hand=0;hand<2;hand++)
            {
                var skeleton=handSkeletons[slot*2+hand]!;
                float open=hand==1 ? supportOpen : 0;
                for(int j=0;j<FirstPersonHandRig.JointCount;j++)
                {
                    var bone=handBones[hand][j];
                    bone.localPosition=V(skeleton.Joints[j].LocalBindPosition)+(hand==1 && j==2?V(reach):Vector3.zero);
                    bone.localRotation=Q(FirstPersonHandRig.FingerRotation(j,hand==1,open,gun?trigger:0));
                }
            }
        }

        private void PoseLocalMechanicalParts(in WeaponVisualProfile profile, in ReloadPose reloadPose, float cycleOpen, bool reducedMotion)
        {
            bool gun = FirstPersonWeaponMechanics.HasMagazinePart(profile);
            if (localMagazine != null)
            {
                bool show = gun && reloadPose.MagazineVisible;
                localMagazine.gameObject.SetActive(show);
                if (show) localMagazine.localPosition = V(reloadPose.MagazineOffset);
            }
            if (localFreshMagazine != null)
            {
                bool show = gun && reloadPose.FreshMagazineVisible;
                localFreshMagazine.gameObject.SetActive(show);
                if (show) localFreshMagazine.localPosition = V(reloadPose.FreshMagazineOffset);
            }
            if (localCycle != null)
            {
                bool show = FirstPersonWeaponMechanics.HasCyclePart(profile);
                localCycle.gameObject.SetActive(show);
                if (show)
                    localCycle.localPosition = V(FirstPersonWeaponMechanics.CycleOffset(profile, cycleOpen, reducedMotion));
            }
        }

        private void PoseHeldMechanicalParts(Avatar avatar, in WeaponVisualProfile profile, bool magazineEmpty)
        {
            float reload = avatar.ReloadPhase > 0f ? avatar.ReloadPhase : 0f;
            if (avatar.MagazinePart != null)
            {
                bool show = FirstPersonWeaponMechanics.HasMagazinePart(profile) && !avatar.Corpse;
                avatar.MagazinePart.gameObject.SetActive(show);
                if (show)
                    avatar.MagazinePart.localPosition = V(FirstPersonWeaponMechanics.MagazineOffset(reload, magazineEmpty, false));
            }
            if (avatar.CyclePart != null)
            {
                bool show = FirstPersonWeaponMechanics.HasCyclePart(profile) && !avatar.Corpse;
                avatar.CyclePart.gameObject.SetActive(show);
                if (show)
                    avatar.CyclePart.localPosition = V(FirstPersonWeaponMechanics.CycleOffset(profile, avatar.CycleOpen, false));
            }
        }

        // Scene-object names key on the stable MeshAssetKey ("rifle", "rifle-carbine", "pistol", "knife", "grenade"), not on
        // DisplayLabel UI copy. This keeps the pre-ADR-072 hierarchy names that tests and diagnostics Find() by.
        private static string WeaponDisplayName(in WeaponVisualProfile profile, bool local) =>
            (local ? "Local FP " : "Held ") + profile.MeshAssetKey;

        // Unknown mesh keys fall back to the rifle set, as the pre-ADR-078 switches did.
        private WeaponMeshSet? MeshSetForProfile(in WeaponVisualProfile profile) =>
            weaponMeshes.TryGetValue(profile.MeshAssetKey, out var set) ? set
            : weaponMeshes.TryGetValue(WeaponVisualProfiles.MeshKeyRifle, out var rifle) ? rifle : null;

        /// <summary>
        /// World position of the muzzle the viewer sees for <paramref name="instigator"/>: the first-person
        /// view model for the local player, the held weapon otherwise. Presentation-only placement of the
        /// flash; the authoritative shot origin is unchanged.
        /// </summary>
        public bool TryGetMuzzleWorld(EntityId instigator, out Float3 muzzle)
        {
            muzzle = default;
            if (instigator.Equals(localHuman) && localWeapon != null && (localWeapon.gameObject.activeInHierarchy || localWeaponScopeHidden))
                return TryMuzzle(localWeapon, localVisibleWeaponId, out muzzle);
            if (avatars.TryGetValue(instigator.Value, out var avatar) && !avatar.Corpse && avatar.Weapon != null && avatar.Weapon.gameObject.activeInHierarchy)
                return TryMuzzle(avatar.Weapon, avatar.WeaponDefinition, out muzzle);
            return false;
        }

        private bool TryMuzzle(Transform weapon, DefinitionId id, out Float3 muzzle)
        {
            muzzle = default;
            if (!WeaponVisualProfiles.TryGet(id, out var profile) || !weaponMeshes.TryGetValue(profile.MeshAssetKey, out var set)) return false;
            if (set.MuzzleSocket.X == 0 && set.MuzzleSocket.Y == 0 && set.MuzzleSocket.Z == 0) return false;
            // The legacy rifle socket table is shared by the shorter carbine silhouette; never place the flash in
            // the air ahead of a barrel that ends sooner than the socket.
            Vector3 local = V(set.MuzzleSocket);
            var filter = weapon.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) local.z = Mathf.Min(local.z, filter.sharedMesh.bounds.max.z);
            Vector3 world = weapon.TransformPoint(local);
            muzzle = new Float3(world.x, world.y, world.z);
            return true;
        }

        private Float3 SightSocketForProfile(in WeaponVisualProfile profile) =>
            weaponMeshes.TryGetValue(profile.MeshAssetKey, out var set) ? set.SightSocket : default;

        private Mesh? HeldMeshForProfile(in WeaponVisualProfile profile) => MeshSetForProfile(profile)?.Held;
        private Mesh? LocalMeshForProfile(in WeaponVisualProfile profile) => MeshSetForProfile(profile)?.Local;
        private Mesh? HeldMagazineForProfile(in WeaponVisualProfile profile) => MeshSetForProfile(profile)?.Magazine;
        private Mesh? LocalMagazineForProfile(in WeaponVisualProfile profile) => MeshSetForProfile(profile)?.Magazine;
        private Mesh? HeldCycleForProfile(in WeaponVisualProfile profile) => MeshSetForProfile(profile)?.Cycle;
        private Mesh? LocalCycleForProfile(in WeaponVisualProfile profile) => MeshSetForProfile(profile)?.Cycle;

        private static MeshFilter CreateWeaponChild(Transform parent, string name, out Transform child)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return child.gameObject.AddComponent<MeshFilter>();
        }

        private static Float3 FindSocket(FrozenList<WeaponSocket> sockets, string id)
        {
            for (int i = 0; i < sockets.Count; i++)
                if (sockets[i].Id == id) return sockets[i].Position;
            return default;
        }

        private void DestroyAvatar(EntityId id)
        {
            if (!avatars.TryGetValue(id.Value, out var avatar)) return;
            avatars.Remove(id.Value);
            actions.Forget(id);
            avatar.Authored?.Dispose();
            if (avatar.Root != null) DestroyObject(avatar.Root.gameObject);
        }

        private sealed class DroppedWeaponVisual
        {
            public int Id;
            public Transform Root = null!;
            public MeshFilter Filter = null!;
            public MeshRenderer Renderer = null!;
            public MeshFilter SightFilter = null!;
            public Transform SightPart = null!;
        }

        private readonly List<DroppedWeaponVisual> droppedVisuals = new List<DroppedWeaponVisual>();
        /// <summary>Last 3 s of a dropped gun's lifetime blink so the player sees it is about to vanish.</summary>
        public const float DroppedWeaponBlinkSeconds = 3f;

        /// <summary>
        /// ADR-105: shows primaries lying on the floor. Read-only mirror of the session's dropped-weapon list;
        /// uses the same packaged held mesh and palette as the third-person weapon.
        /// </summary>
        public void SyncDroppedWeapons(Func<int, DroppedWeaponView> tryDropped, int count, float timeSeconds)
        {
            if (!ready || !visualsBound) return;
            if (count < 0) count = 0;
            for (int i = 0; i < count; i++)
            {
                var view = tryDropped(i);
                if (!view.IsValid) { count = i; break; }
                if (i == droppedVisuals.Count) droppedVisuals.Add(CreateDroppedVisual(i));
                var visual = droppedVisuals[i];
                if (visual.Id != view.Id || visual.Filter.sharedMesh == null)
                {
                    var profile = WeaponVisualProfiles.Resolve(view.WeaponId, 0);
                    var mesh = HeldMeshForProfile(profile);
                    visual.Filter.sharedMesh = mesh;
                    visual.Renderer.sharedMaterials = WeaponPalette(mesh);
                    // ADR-107: the gun lies on the floor with the sight it carried.
                    var sightMesh = SightMeshFor(profile.DefinitionId, view.SightId, false);
                    visual.SightFilter.sharedMesh = sightMesh;
                    visual.SightPart.GetComponent<MeshRenderer>().sharedMaterials = WeaponPalette(sightMesh);
                    visual.SightPart.gameObject.SetActive(sightMesh != null);
                    visual.Id = view.Id;
                    // Lying on its side; the yaw varies by drop so several guns never stack identically.
                    float yaw = (view.Id * 137.5f) % 360f;
                    visual.Root.SetPositionAndRotation(
                        new Vector3(view.Position.X, view.Position.Y + 0.05f, view.Position.Z),
                        Quaternion.Euler(0f, yaw, 90f));
                }
                bool blinkOff = view.SecondsRemaining < DroppedWeaponBlinkSeconds && Mathf.FloorToInt(timeSeconds * 6f) % 2 == 1;
                visual.Root.gameObject.SetActive(visual.Filter.sharedMesh != null && !blinkOff);
            }
            for (int i = count; i < droppedVisuals.Count; i++)
            {
                droppedVisuals[i].Id = 0;
                droppedVisuals[i].Root.gameObject.SetActive(false);
            }
        }

        private DroppedWeaponVisual CreateDroppedVisual(int index)
        {
            var root = new GameObject("Dropped weapon " + index).transform;
            root.SetParent(transform, false);
            var renderer = root.gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var filter = root.gameObject.AddComponent<MeshFilter>();
            var sightFilter = CreateWeaponChild(root, "Dropped sight", out var sightPart);
            sightPart.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            root.gameObject.SetActive(false);
            return new DroppedWeaponVisual { Root = root, Filter = filter, Renderer = renderer, SightFilter = sightFilter, SightPart = sightPart };
        }

        private void ClearDroppedWeapons()
        {
            for (int i = 0; i < droppedVisuals.Count; i++)
                if (droppedVisuals[i].Root != null) DestroyObject(droppedVisuals[i].Root.gameObject);
            droppedVisuals.Clear();
        }

        /// <summary>ADR-111: the packaged held-LOD mesh and finish palette of a prop (e.g. the thrown grenade).</summary>
        public bool TryGetPropVisual(string meshKey, out Mesh? mesh, out Material[] materials)
        {
            mesh = null; materials = Array.Empty<Material>();
            if (!visualsBound || !weaponMeshes.TryGetValue(meshKey, out var set) || set.Held == null) return false;
            mesh = set.Held; materials = WeaponPalette(set.Held);
            return true;
        }

        private Material[] WeaponPalette(Mesh? mesh)
        {
            if (mesh == null) return Array.Empty<Material>();
            if (!visualsBound) throw new InvalidOperationException("Weapon palette requested before packaged materials are bound.");
            if (weaponPalettes.TryGetValue(mesh, out var palette)) return palette;
            if (mesh.subMeshCount > weaponMaterials.Length) throw new InvalidOperationException("Unknown weapon material slot.");
            palette = weaponMaterials.Take(mesh.subMeshCount).ToArray();
            weaponPalettes.Add(mesh, palette);
            return palette;
        }

        private Mesh Upload(MeshData data, bool skin, SkeletonData? handSkeleton = null)
        {
            var mesh = Own(new Mesh { name = skin ? "Product soldier" : "Product weapon" });
            mesh.vertices = data.Positions.Select(V).ToArray();
            mesh.normals = data.Normals.Select(V).ToArray();
            mesh.tangents = data.Tangents.Select(t => new Vector4(t.X, t.Y, t.Z, t.W)).ToArray();
            mesh.uv = data.Uv.Select(t => new Vector2(t.X, t.Y)).ToArray();
            mesh.subMeshCount = data.Submeshes.Max(s => (int)s.MaterialSlot) + 1;
            for (int i = 0; i < data.Submeshes.Count; i++)
            {
                var sub = data.Submeshes[i];
                mesh.SetIndices(
                    data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v => (int)v).ToArray(),
                    MeshTopology.Triangles,
                    sub.MaterialSlot);
            }
            if (skin || handSkeleton != null)
            {
                if (data.Joints == null || data.Weights == null)
                    throw new InvalidOperationException("Soldier mesh missing skin weights.");
                var weights = new BoneWeight[data.Positions.Count];
                for (int i = 0; i < weights.Length; i++)
                {
                    var j = data.Joints[i];
                    var w = data.Weights[i];
                    weights[i] = new BoneWeight
                    {
                        boneIndex0 = j.X, boneIndex1 = j.Y, boneIndex2 = j.Z, boneIndex3 = j.W,
                        weight0 = w.X, weight1 = w.Y, weight2 = w.Z, weight3 = w.W
                    };
                }
                mesh.boneWeights = weights;
                if(handSkeleton!=null)
                {
                    var positions=new Vector3[handSkeleton.Joints.Count];
                    for(int j=0;j<positions.Length;j++)
                    { var definition=handSkeleton.Joints[j]; positions[j]=V(definition.LocalBindPosition)+(definition.ParentId==ushort.MaxValue?Vector3.zero:positions[definition.ParentId]); }
                    mesh.bindposes=positions.Select(position=>Matrix4x4.TRS(position,Quaternion.identity,Vector3.one).inverse).ToArray();
                }
                else mesh.bindposes = Enumerable.Range(0, SoldierSkeleton.JointCount)
                    .Select(j => Matrix4x4.TRS(V(SoldierSkeleton.BindPositions[j]), Quaternion.identity, Vector3.one).inverse)
                    .ToArray();
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>ADR-124: one authored body for an actor, its variant picked deterministically within the team.</summary>
        private AuthoredSoldierBody CreateAuthoredBody(Transform root, ulong entityId, byte team)
        {
            var catalog = skinCatalog ?? throw new InvalidOperationException("ADR-124 soldier skin catalog not bound; no generated-body fallback.");
            var variant = catalog.Pick(entityId, team);
            if (!skinPrototypes.TryGetValue(variant.Id, out var shape))
                skinPrototypes[variant.Id] = shape = AuthoredSoldierBody.Prepare(catalog, variant, mesh => Upload(mesh, false), value => owned.Add(value));
            return new AuthoredSoldierBody(catalog, shape, root, entityId, m => RestyleAuthored(m, team, variant), (team == 1 ? team1Gear : team2Gear)!, (team == 1 ? team1Paint : team2Paint)!);
        }

        /// <summary>
        /// ADR-124: uniform materials wear the team kit; every other authored material is rebuilt on the locked
        /// MapSurface shader from its own texture (skin materials take the variant's skin-tone texture) and colour, so
        /// actor ambient, fog and quality tiers stay one lighting model.
        /// </summary>
        private Material RestyleAuthored(Material source, byte team, SoldierSkinCatalog.Variant variant)
        {
            var palette = (team == 1 ? team1Palette : team2Palette)!;
            if (source == null) return palette[0];
            string name = source.name.Replace(" (Instance)", string.Empty);
            var solid = (team == 1 ? team1Solid : team2Solid)!;
            if (variant.UniformMaterials.Any(p => AuthoredSoldierBody.Matches(name, p))) return solid[0];
            if (variant.GearMaterials.Any(p => AuthoredSoldierBody.Matches(name, p))) return solid[1];
            Texture? skinTexture = variant.SkinMaterials.Any(p => AuthoredSoldierBody.Matches(name, p)) ? variant.SkinTexture : null;
            if (restyledMaterials.TryGetValue((source, skinTexture), out var cached)) return cached;
            var material = Own(new Material(surfaceShader!) { name = "Authored " + name });
            var texture = skinTexture != null ? skinTexture : source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
            if (texture != null) material.SetTexture("_BaseMap", texture);
            var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.color : Color.white;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", .25f);
            restyledMaterials[(source, skinTexture)] = material;
            return material;
        }

        private void ApplyAmbient(Renderer? renderer,float visibility)
        {
            if(renderer==null)return;renderer.GetPropertyBlock(ambientBlock);ambientBlock.SetFloat("_ShooterActorMode",1);ambientBlock.SetFloat("_ShooterActorAo",visibility);renderer.SetPropertyBlock(ambientBlock);
        }
        private GameObject CreateContact(Transform parent)
        {
            if(contactMesh==null)
            {
                contactMesh=Own(new Mesh{name="Numeric contact footprint"});contactMesh.vertices=new[]{new Vector3(-.42f,0,-.34f),new Vector3(-.42f,0,.34f),new Vector3(.42f,0,.34f),new Vector3(.42f,0,-.34f)};
                contactMesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};contactMesh.normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up};contactMesh.tangents=new[]{new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1)};contactMesh.triangles=new[]{0,1,2,0,2,3};contactMesh.RecalculateBounds();
                contactMaterial=Own(new Material(Shader.Find("Shooter/ContactShadow")??throw new InvalidOperationException("Contact shadow shader missing.")));
            }
            var go=new GameObject("Low contact shadow"){layer=11};go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=contactMesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=contactMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return go;
        }

        private Material FromPackaged(
            Shader shader,
            PackagedVisualPayloads visuals,
            string path,
            string label,
            float metallic = 0f,
            float smoothness = 0.2f)
        {
            var data = MapVisualClosure.DecodeMapMaterial(visuals, path);
            if(data.Width>1)
            {
                MaterialData? Companion(string plane)
                {
                    string companion=MapVisualClosure.MaterialCompanionPath(path,plane);
                    return visuals.Has(companion)?MapVisualClosure.DecodeMapMaterial(visuals,companion):null;
                }
                return ProductSurfaceMaterial.Create(shader,data,Companion("normal"),Companion("mask"),label,label.Contains("uniform")?4f:1f,value=>owned.Add(value),metallic,smoothness);
            }
            var material = Own(new Material(shader) { name = label });
            var mip0 = data.MipBytes[0];
            material.SetColor("_BaseColor", new Color(mip0[0] / 255f, mip0[1] / 255f, mip0[2] / 255f, mip0[3] / 255f));
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        /// <summary>
        /// Presentation-only kit accents after packaged load. Ally cyan-leaning olive/slate vs
        /// enemy coral-leaning desert/ochre; skin stays believable. Not solid flat blue.
        /// Does not change mesh topology, sockets, or generator recipes.
        /// </summary>
        private static void ApplyPresentationSoldierPalette(Material[] palette, bool team1)
        {
            if (palette == null || palette.Length < 4) return;
            if (team1)
            {
                // Olive/slate field kit with a cool cyan lean — readable allies without monochrome blue.
                SetLit(palette[0], new Color(.38f,.44f,.38f), 0.05f, 0.28f); // uniform
                SetLit(palette[1], new Color(0.10f, 0.14f, 0.15f), 0.28f, 0.40f); // gear / straps
                SetLit(palette[2], new Color(0.52f, 0.38f, 0.28f), 0f, 0.36f);    // skin
                SetLit(palette[3], new Color(0.08f, 0.18f, 0.20f), 0.58f, 0.76f); // goggles
            }
            else
            {
                // Desert/ochre opposing kit with coral warmth — readable enemies in sun and shade.
                SetLit(palette[0], new Color(1f,.85f,.65f), 0.05f, 0.26f); // uniform
                SetLit(palette[1], new Color(0.16f, 0.10f, 0.07f), 0.24f, 0.34f); // gear
                SetLit(palette[2], new Color(0.50f, 0.36f, 0.26f), 0f, 0.34f);    // skin
                SetLit(palette[3], new Color(0.20f, 0.09f, 0.06f), 0.52f, 0.70f); // goggles
            }
        }

        private static void SetLit(Material material, Color baseColor, float metallic, float smoothness)
        {
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
        }

        private static void ApplyLocalHumanShadowMode(Avatar avatar, bool shadowsOnly)
        {
            var mode = shadowsOnly
                ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                : UnityEngine.Rendering.ShadowCastingMode.On;
            if (avatar.Body != null) avatar.Body.shadowCastingMode = mode;
            avatar.Authored?.SetShadowMode(mode);
            if (avatar.WeaponRenderer != null) avatar.WeaponRenderer.shadowCastingMode = mode;
            if (avatar.MagazineRenderer != null) avatar.MagazineRenderer.shadowCastingMode = mode;
            if (avatar.CycleRenderer != null) avatar.CycleRenderer.shadowCastingMode = mode;
            if (avatar.SightRenderer != null) avatar.SightRenderer.shadowCastingMode = mode;
        }

        private static Vector3 V(Float3 v) => new Vector3(v.X, v.Y, v.Z);
        private static Quaternion Q(Quat q) => new Quaternion(q.X, q.Y, q.Z, q.W);

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void StepBladeTrail(byte shown, bool reducedMotion, float dtSeconds)
        {
            if (localWeapon == null) return;
            float strength = ProductAuthorityActionView.IsKnifeSlot(shown) && !reducedMotion
                ? FirstPersonSpecialActions.BladeTrailStrength(localView.MeleePhase01, localView.MeleeIsStab)
                : 0f;
            if (bladeTrail == null)
            {
                if (strength <= 0f || localWeapon.parent == null) return;
                bladeTrail = new Shooter.Presentation.Fx.KnifeBladeTrail(localWeapon.parent, localWeapon.gameObject.layer);
            }
            bladeTrail.Step(localWeapon.TransformPoint(KnifeEdgeBase), localWeapon.TransformPoint(KnifeEdgeTip), strength, dtSeconds);
        }

        private const int ChestJoint = 4, RightUpperArmJoint = 12;

        /// <summary>
        /// ADR-111: knife cut and overhand throw for other players, layered on the solved pose by swinging the right
        /// arm (upper arm, forearm, hand) and the held prop about the shoulder. Presentation only.
        /// </summary>
        private void ApplyThirdPersonActionOverlay(Avatar avatar, byte slot, bool cooking, float dtSeconds, ref Float3 weaponPosition, ref Quat weaponRotation)
        {
            float dt = float.IsFinite(dtSeconds) && dtSeconds > 0f ? dtSeconds : 0f;
            Quaternion swing = Quaternion.identity;
            var chest = avatar.Bones[ChestJoint].rotation;
            Vector3 up = chest * Vector3.up, right = chest * Vector3.right;
            if (avatar.MeleeSeconds >= 0f)
            {
                float t = avatar.MeleeSeconds / FirstPersonSpecialActions.MeleeDurationSeconds;
                avatar.MeleeSeconds += dt;
                if (t >= 1f || !ProductAuthorityActionView.IsKnifeSlot(slot)) avatar.MeleeSeconds = -1f;
                else
                {
                    float side = (avatar.MeleeSwings & 1u) == 1u ? 1f : -1f;
                    // Cock out and up, then cut across the body and recover.
                    float yaw = t < .15f ? Mathf.Lerp(0f, 40f, t / .15f)
                        : t < .40f ? Mathf.Lerp(40f, -70f, Mathf.SmoothStep(0f, 1f, (t - .15f) / .25f))
                        : Mathf.Lerp(-70f, 0f, Mathf.SmoothStep(0f, 1f, (t - .40f) / .60f));
                    float lift = Mathf.Sin(Mathf.Clamp01(t / .4f) * Mathf.PI) * -25f;
                    swing = Quaternion.AngleAxis(yaw * side, up) * Quaternion.AngleAxis(lift, right);
                }
            }
            else if (avatar.ThrowSeconds >= 0f)
            {
                const float duration = .45f;
                float t = avatar.ThrowSeconds / duration;
                avatar.ThrowSeconds += dt;
                if (t >= 1f) avatar.ThrowSeconds = -1f;
                else
                {
                    // Overhand: arm swings from behind the head forward and down through the release.
                    float pitch = t < .45f ? Mathf.Lerp(-120f, 30f, Mathf.SmoothStep(0f, 1f, t / .45f))
                        : Mathf.Lerp(30f, 0f, Mathf.SmoothStep(0f, 1f, (t - .45f) / .55f));
                    swing = Quaternion.AngleAxis(pitch, right);
                }
            }
            else if (ProductAuthorityActionView.IsGrenadeSlot(slot) && cooking)
            {
                // Cooking: the grenade arm is cocked back behind the head, ready to throw.
                swing = Quaternion.AngleAxis(-120f, right);
            }
            if (swing == Quaternion.identity) return;
            Vector3 pivot = avatar.Bones[RightUpperArmJoint].position;
            for (int j = RightUpperArmJoint; j <= RightHandJoint; j++)
            {
                var bone = avatar.Bones[j];
                bone.SetPositionAndRotation(pivot + swing * (bone.position - pivot), swing * bone.rotation);
            }
            var wp = pivot + swing * (V(weaponPosition) - pivot);
            var wr = swing * Q(weaponRotation);
            weaponPosition = new Float3(wp.x, wp.y, wp.z);
            weaponRotation = new Quat(wr.x, wr.y, wr.z, wr.w);
        }

        private void StepGrenadeArc(bool cooking, float fuseSecondsLeft)
        {
            if (fpsCamera == null) return;
            if (grenadeArc == null)
            {
                if (!cooking) return;
                grenadeArc = new Shooter.Presentation.Fx.GrenadeArcPresenter(transform, Shooter.Presentation.Fx.AnalyticFxPresenter.VisualLayer);
            }
            var cam = fpsCamera.transform;
            grenadeArc.Step(cooking, cam.position, cam.forward, cam.right, cam.up, fuseSecondsLeft, Time.unscaledTime);
        }

        private void OnDestroy()
        {
            ClearAll();
            grenadeArc?.Dispose();
            grenadeArc = null;
            bladeTrail?.Dispose();
            bladeTrail = null;
            if (localWeapon != null) DestroyObject(localWeapon.gameObject);
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null) DestroyObject(owned[i]);
            owned.Clear();
        }
    }
}
