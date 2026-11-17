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
            public byte Team;
            public bool Corpse;
            public byte WeaponSlot;
            public DefinitionId WeaponDefinition;
            public Transform Root = null!;
            public Transform Weapon = null!;
            public Transform[] Bones = null!;
            public SkinnedMeshRenderer Body = null!;
            public MeshRenderer WeaponRenderer = null!;
            public MeshFilter WeaponFilter = null!;
            public Transform? MagazinePart;
            public MeshFilter? MagazineFilter;
            public MeshRenderer? MagazineRenderer;
            public Transform? CyclePart;
            public MeshFilter? CycleFilter;
            public MeshRenderer? CycleRenderer;
            public float CycleOpen;
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

        private sealed class FlatGround : ISoldierGround
        {
            public float Height;
            public bool TrySample(float x, float z, out float y) { y = Height; return true; }
        }

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
        private readonly FlatGround ground = new FlatGround();
        private Mesh? bodyMesh;
        /// <summary>Uploaded packaged weapon meshes keyed by visual profile MeshAssetKey (ADR-078).</summary>
        private sealed class WeaponMeshSet
        {
            public Mesh? Held, Local, Magazine, Cycle;
            public Float3 SightSocket;
        }
        private readonly Dictionary<string, WeaponMeshSet> weaponMeshes = new Dictionary<string, WeaponMeshSet>(StringComparer.Ordinal);
        private Material[]? team1Palette;
        private Material[]? team2Palette;
        private Material? rifleMetal;
        private Material? riflePolymer;
        private Material[] weaponMaterials = Array.Empty<Material>();
        private readonly Dictionary<Mesh, Material[]> weaponPalettes = new Dictionary<Mesh, Material[]>();
        private WeaponRecipe? motionRecipe;
        private DefinitionId localVisibleWeaponId;
        private DefinitionId localPrimaryWeaponId = new DefinitionId("weapons/rifle");
        private EntityId localHuman;
        private Camera? fpsCamera;
        private Transform? localWeapon;
        private Transform? localMagazine;
        private MeshFilter? localMagazineFilter;
        private Transform? localCycle;
        private MeshFilter? localCycleFilter;
        private SkinnedMeshRenderer? triggerHand;
        private SkinnedMeshRenderer? supportHand;
        private readonly Mesh?[] firstPersonHandMeshes = new Mesh?[8];
        private Material[]? firstPersonHandMaterials;
        private readonly SkeletonData?[] handSkeletons = new SkeletonData?[8];
        private readonly Transform[][] handBones = { new Transform[FirstPersonHandRig.JointCount], new Transform[FirstPersonHandRig.JointCount] };
        private readonly ProceduralViewModel localView = new ProceduralViewModel();
        private readonly FirstPersonHandling localHandling = new FirstPersonHandling();
        private float localAdsPresentation;
        private byte localVisibleWeaponSlot;
        private EntityId localViewHuman;
        private bool ready;
        private bool visualsBound;
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
            localCycleFilter = CreateWeaponChild(localWeapon, "FP cycle", out localCycle);
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
            ClearAll();
            foreach(var value in owned)DestroyObject(value);owned.Clear();
            bodyMesh=null;contactMesh=null;contactMaterial=null;weaponMeshes.Clear();weaponPalettes.Clear();
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
                weaponMeshes.Clear();
                var silhouettes = new List<WeaponSilhouetteSource>();
                foreach (var source in WeaponMeshCatalog.All)
                {
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
                    // ADR-085: HUD weapon icons are traced from these same packaged LOD0 meshes.
                    silhouettes.Add(new WeaponSilhouetteSource(source.MeshKey, WeaponSilhouetteSource.SpanFor(source.MeshKey), silhouetteParts));
                    if (source.Recipe != null) set.SightSocket = FindSocket(WeaponGeometry.Sockets(source.Recipe), "sight-eye");
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
                    NearFieldSurface(MapVisualClosure.FirstPersonSurfacePaths[6], "Olive enamel grenade shell"));
                firstPersonHandMaterials = new[] { sleeve, glove, riflePolymer };

                visualsBound = true;complete=true;
                ApplyLocalWeaponVisual(WeaponVisualProfiles.DefaultIdForSlot(ProductAuthorityActionView.RifleSlot), ProductAuthorityActionView.RifleSlot);
                Debug.Log("SHOOTER_PRODUCT_ACTOR_VIEW packaged visuals ready profile=" + visuals.Profile +
                          " soldierLod=" + soldierLod + " weaponLod=" + weaponLod);
            }
            finally { if(!complete&&epoch==packagedBindEpoch){visualsBound=false;bodyMesh=null;weaponMeshes.Clear();} }
        }

        public void ClearAll()
        {
            binder.Clear();
            actions.Clear();
            localView.ResetForLife();
            localHandling.Reset(ProductAuthorityActionView.RifleSlot);
            localViewHuman = default;
            foreach (var pair in avatars)
                if (pair.Value.Root != null) DestroyObject(pair.Value.Root.gameObject);
            avatars.Clear();
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
            IActorLifecycle? lifecycle,
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
                    if (ev.Kind != CombatEventKind.ShotAccepted) continue;
                    if (!avatars.TryGetValue(ev.Instigator.Value, out var shooter)) continue;
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
            if (showLocalWeapon)
            {
                float visibility=AmbientVisibility?.Invoke(human.Feet)??1f;foreach(var renderer in localLitRenderers)ApplyAmbient(renderer,visibility);
                byte slot = human.ActiveWeaponSlot;
                DefinitionId weaponId = human.ActiveWeaponDefinition.IsValid
                    ? human.ActiveWeaponDefinition
                    : WeaponVisualProfiles.DefaultIdForSlot(slot);
                if (slot == ProductAuthorityActionView.RifleSlot && weaponId.IsValid)
                    localPrimaryWeaponId = weaponId;
                if (!localViewHuman.Equals(human.EntityId))
                {
                    localView.ResetForLife(slot);
                    localHandling.Reset(slot);
                    localViewHuman = human.EntityId;
                    if (slot == ProductAuthorityActionView.RifleSlot && weaponId.IsValid)
                        localPrimaryWeaponId = weaponId;
                }
                actions.SyncLifecycle(human.EntityId, slot, true);
                localView.SetActiveWeaponSlot(slot);
                localView.SetCooking(human.WeaponAction == WeaponAction.Cooking);
                if (combatEvents != null)
                    localView.ConsumeEvents(combatEvents, human.EntityId, aimHeld, dtSeconds);
                else
                    localView.TickAim(aimHeld, dtSeconds);
                // Authority wins over an older action event on a switch/cancel frame.
                localView.SetActiveWeaponSlot(slot);
                localView.SetCooking(human.WeaponAction == WeaponAction.Cooking);
                var pose = localView.Pose;
                var movement = movementView?.Invoke(human.EntityId);
                localHandling.Step(human.Feet, slot, dtSeconds, movement.HasValue && movement.Value.Sliding, reducedMotion);
                // Mesh follows the holster→draw visible slot so the outgoing gun finishes tucking first.
                byte shown = localHandling.VisibleWeaponSlot;
                // Mid-switch keeps the outgoing slot's last known id (primary may be carbine).
                DefinitionId shownId = shown == slot
                    ? weaponId
                    : (shown == ProductAuthorityActionView.RifleSlot
                        ? localPrimaryWeaponId
                        : WeaponVisualProfiles.DefaultIdForSlot(shown));
                localVisibleWeaponSlot = shown;
                localVisibleWeaponId = shownId;
                var profile = WeaponVisualProfiles.Resolve(shownId, shown);
                ApplyLocalWeaponVisual(shownId, shown);
                float reload = localHandling.IsSwitching ? -1f : actions.ReloadPhaseAtTick(human.EntityId, completedTick);
                float recoilEnergy = Math.Abs(pose.RecoilPitch) + Math.Abs(pose.RecoilYaw);
                bool magazineEmpty = human.Magazine == 0;
                float adsPresent = FirstPersonHandling.AdsPresentationBlend(pose.AdsBlend, localHandling.EquipBlend);
                localAdsPresentation = adsPresent;
                var offset = localHandling.PositionOffset(adsPresent, reload, reducedMotion, recoilEnergy, magazineEmpty);
                var rotation = localHandling.RotationDegrees(adsPresent, reload, reducedMotion, recoilEnergy);
                var specialPos = localView.SpecialActionPosition(reducedMotion);
                var specialRot = localView.SpecialActionRotation(reducedMotion);
                var kick = localView.EquipmentKickMeters(reducedMotion);
                Float3 sight = SightSocketForProfile(profile);
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
                localWeapon.localPosition = Vector3.Lerp(hip, aligned, adsPresent) + V(offset) + V(kick) + V(specialPos);
                float rotKick = FirstPersonHandling.RecoilRotationScale(adsPresent, reducedMotion);
                var composition = FirstPersonHandling.HipRotationDegrees(profile, adsPresent);
                localWeapon.localRotation = Quaternion.Euler(
                    rotation.X + composition.X + specialRot.X + pose.RecoilPitch * Mathf.Rad2Deg * rotKick,
                    rotation.Y + composition.Y + specialRot.Y + pose.RecoilYaw * Mathf.Rad2Deg * rotKick,
                    rotation.Z + composition.Z + specialRot.Z);
                PoseLocalHands(shown,reload,localView.CycleOpen01,reducedMotion,magazineEmpty);
                PoseLocalMechanicalParts(profile, shown, reload, magazineEmpty, localView.CycleOpen01, reducedMotion);
                // Full scoped ADS uses the analytic scope overlay — hide the near-field mesh so the lens stays clean.
                // Empty/thrown grenade hides the held prop so the throw reads clearly.
                if (AdsOpticVisual.HideRifleViewModel(profile, adsPresent)
                    || localView.HideHeldGrenade(human.Magazine))
                    localWeapon.gameObject.SetActive(false);
            localLitRenderers=localWeapon.GetComponentsInChildren<Renderer>(true);
            }
            else if (localViewHuman.IsValid)
            {
                localView.ResetForLife();
                localHandling.Reset(ProductAuthorityActionView.RifleSlot);
                localAdsPresentation = 0f;
                localVisibleWeaponSlot = ProductAuthorityActionView.RifleSlot;
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
            for (int j = 0; j < SoldierSkeleton.JointCount; j++)
            {
                var definition = skeleton.Joints[j];
                var bone = new GameObject(SoldierSkeleton.Names[j]).transform;
                avatar.Bones[j] = bone;
                bone.SetParent(definition.ParentId == ushort.MaxValue ? avatar.Root : avatar.Bones[definition.ParentId], false);
                bone.localPosition = V(definition.LocalBindPosition);
                bone.localRotation = Quaternion.identity;
            }

            avatar.Body = avatar.Root.gameObject.AddComponent<SkinnedMeshRenderer>();
            avatar.Body.sharedMesh = bodyMesh;
            avatar.Body.bones = avatar.Bones;
            avatar.Body.rootBone = avatar.Bones[0];
            avatar.Body.sharedMaterials = state.Team == 1 ? team1Palette : team2Palette;
            avatar.Body.quality = SkinQuality.Bone4;
            avatar.Body.updateWhenOffscreen = true;
            avatar.Body.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(4, 4, 4));

            avatar.Weapon = new GameObject("Held weapon").transform;
            avatar.Weapon.SetParent(avatar.Root, false);
            avatar.WeaponFilter = avatar.Weapon.gameObject.AddComponent<MeshFilter>();
            avatar.WeaponRenderer = avatar.Weapon.gameObject.AddComponent<MeshRenderer>();
            avatar.MagazineFilter = CreateWeaponChild(avatar.Weapon, "Held magazine", out avatar.MagazinePart);
            avatar.MagazineRenderer = avatar.MagazinePart!.GetComponent<MeshRenderer>();
            avatar.CycleFilter = CreateWeaponChild(avatar.Weapon, "Held cycle", out avatar.CyclePart);
            avatar.CycleRenderer = avatar.CyclePart!.GetComponent<MeshRenderer>();
            DefinitionId createId = state.ActiveWeaponDefinition.IsValid
                ? state.ActiveWeaponDefinition
                : WeaponVisualProfiles.DefaultIdForSlot(slot);
            avatar.WeaponDefinition = createId;
            ApplyHeldWeaponVisual(avatar, createId, slot);
            ApplyLocalHumanShadowMode(avatar, state.EntityId.Equals(localHuman) && !corpse && state.Alive);

            avatars[state.EntityId.Value] = avatar;
            PoseAvatar(avatar, state, 0f, 0u);
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
            if (movement.HasValue && !avatar.Corpse && state.Alive)
            {
                var m = movement.Value;
                speed = Mathf.Clamp(m.HorizontalSpeedMetersPerSecond, 0f, 5.5f);
                avatar.Distance = m.LocomotionDistanceMeters;
                crouching = m.Stance == Stance.Crouching && !m.Sliding;
                if (m.Sliding) slide = 1f;
            }

            byte slot = state.ActiveWeaponSlot;
            DefinitionId weaponId = state.ActiveWeaponDefinition.IsValid
                ? state.ActiveWeaponDefinition
                : WeaponVisualProfiles.DefaultIdForSlot(slot);
            var heldProfile = WeaponVisualProfiles.Resolve(weaponId, slot);
            actions.SyncLifecycle(state.EntityId, slot, state.Alive && !avatar.Corpse);
            if (slot != avatar.WeaponSlot || !avatar.WeaponDefinition.Equals(weaponId))
            {
                avatar.WeaponSlot = slot;
                avatar.WeaponDefinition = weaponId;
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
            ground.Height = state.Feet.Y;
            float yaw = state.AimYaw;
            float pitch = dying
                ? 0f
                : Mathf.Clamp(state.AimPitch, -SoldierMotion.MaxAimPitchRadians, SoldierMotion.MaxAimPitchRadians);
            float aimYaw = Mathf.Clamp(0f, -SoldierMotion.MaxAimYawRadians, SoldierMotion.MaxAimYawRadians);

            // Cosmetic root blend toward authoritative feet (hit capsules stay tick-snapped).
            Float3 displayFeet = state.Feet;
            if (avatar.HasPrevious && !dying && dtSeconds > 1e-4f)
            {
                float blend = Mathf.Clamp01(dtSeconds * 18f);
                displayFeet = new Float3(
                    Mathf.Lerp(avatar.DisplayedFeet.X, state.Feet.X, blend),
                    Mathf.Lerp(avatar.DisplayedFeet.Y, state.Feet.Y, blend),
                    Mathf.Lerp(avatar.DisplayedFeet.Z, state.Feet.Z, blend));
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
                slide);
            avatar.Root.position = V(displayFeet);
            float visibility=AmbientVisibility?.Invoke(displayFeet)??1f;
            ApplyAmbient(avatar.Body,visibility);ApplyAmbient(avatar.WeaponRenderer,visibility);ApplyAmbient(avatar.MagazineRenderer,visibility);ApplyAmbient(avatar.CycleRenderer,visibility);
            if(avatar.Contact==null)avatar.Contact=CreateContact(avatar.Root);
            avatar.Contact.SetActive(QualitySettings.GetQualityLevel()==0&&!dying);
            avatar.Contact.transform.position=V(new Float3(displayFeet.X,displayFeet.Y+.025f,displayFeet.Z));
            if(CollisionGround!=null)CollisionGround.ReferenceHeight=state.Feet.Y;
            var pose = avatar.Motion.Evaluate(input, CollisionGround??(ISoldierGround)ground);
            for (int j = 0; j < SoldierSkeleton.JointCount; j++)
                avatar.Bones[j].SetPositionAndRotation(V(pose.WorldPositions[j]), Q(pose.WorldRotations[j]));

            Float3 weaponPosition = pose.WeaponPosition;
            Quat weaponRotation = pose.WeaponRotation;
            if (heldProfile.Family != WeaponVisualFamily.PrimaryGun && !avatar.Corpse && state.Alive)
                weaponPosition = pose.WorldPositions[RightHandJoint];

            avatar.Weapon.SetPositionAndRotation(V(weaponPosition), Q(weaponRotation));
            avatar.Weapon.gameObject.SetActive(!avatar.Corpse && state.Alive);
            PoseHeldMechanicalParts(avatar, heldProfile, state.Magazine == 0);
            // Re-assert each pose so respawn / corpse flip keeps local FP body+weapon ShadowsOnly.
            ApplyLocalHumanShadowMode(avatar, state.EntityId.Equals(localHuman) && !avatar.Corpse && state.Alive);

            Float3 sightLocal = SightSocketForProfile(heldProfile);
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
            avatar.Team = state.Team;
            if (avatar.Body != null)
                avatar.Body.sharedMaterials = state.Team == 1 ? team1Palette : team2Palette;
        }

        private void ApplyHeldWeaponVisual(Avatar avatar, DefinitionId weaponId, byte slot)
        {
            var profile = WeaponVisualProfiles.Resolve(weaponId, slot);
            var meshDefinition=WeaponMeshCatalog.Require(profile.MeshAssetKey);
            if(meshDefinition.Recipe!=null)avatar.Motion.SetVisualWeapon(meshDefinition.Recipe);
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

        private void ApplyLocalWeaponVisual(DefinitionId weaponId, byte slot)
        {
            if (localWeapon == null || !visualsBound) return;
            var profile = WeaponVisualProfiles.Resolve(weaponId, slot);
            localWeapon.name = WeaponDisplayName(profile, true);
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
                    supportHand.transform.localPosition = Vector3.zero;
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
            }
        }

        private void PoseLocalHands(byte slot,float reload,float cycle,bool reducedMotion,bool magazineEmpty)
        {
            if(triggerHand==null || supportHand==null) return;
            bool gun=FirstPersonWeaponMechanics.HasMagazinePart(slot);
            for(int hand=0;hand<2;hand++)
            {
                var skeleton=handSkeletons[slot*2+hand]!;
                float open=hand==1 && gun ? FirstPersonHandRig.OpenForReload(reload) : 0;
                var reach=hand==1 ? FirstPersonHandling.SupportHandOffset(slot,reload,reducedMotion,magazineEmpty) : default;
                for(int j=0;j<FirstPersonHandRig.JointCount;j++)
                {
                    var bone=handBones[hand][j];
                    bone.localPosition=V(skeleton.Joints[j].LocalBindPosition)+(j==2?V(reach):Vector3.zero);
                    bone.localRotation=Q(FirstPersonHandRig.FingerRotation(j,hand==1,open,gun && reload<=0?cycle:0));
                }
            }
        }

        private void PoseLocalMechanicalParts(in WeaponVisualProfile profile, byte slot, float reload, bool magazineEmpty, float cycleOpen, bool reducedMotion)
        {
            if (localMagazine != null)
            {
                bool show = FirstPersonWeaponMechanics.HasMagazinePart(profile);
                localMagazine.gameObject.SetActive(show);
                if (show)
                    localMagazine.localPosition = V(FirstPersonWeaponMechanics.MagazineOffset(reload, magazineEmpty, reducedMotion));
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
            if (avatar.Root != null) DestroyObject(avatar.Root.gameObject);
        }

        private Material[] WeaponPalette(Mesh? mesh)
        {
            if (mesh == null) return Array.Empty<Material>();
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
            if (avatar.WeaponRenderer != null) avatar.WeaponRenderer.shadowCastingMode = mode;
            if (avatar.MagazineRenderer != null) avatar.MagazineRenderer.shadowCastingMode = mode;
            if (avatar.CycleRenderer != null) avatar.CycleRenderer.shadowCastingMode = mode;
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

        private void OnDestroy()
        {
            ClearAll();
            if (localWeapon != null) DestroyObject(localWeapon.gameObject);
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null) DestroyObject(owned[i]);
            owned.Clear();
        }
    }
}
