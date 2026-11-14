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
            public Transform Root = null!;
            public Transform Weapon = null!;
            public Transform[] Bones = null!;
            public SkinnedMeshRenderer Body = null!;
            public MeshRenderer WeaponRenderer = null!;
            public MeshFilter WeaponFilter = null!;
            public SoldierMotion Motion = null!;
            public Float3 PreviousFeet;
            public double Distance;
            public ushort PreviousHealth = 100;
            public float ReloadPhase = -1f;
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
        }

        private sealed class FlatGround : ISoldierGround
        {
            public float Height;
            public bool TrySample(float x, float z, out float y) { y = Height; return true; }
        }

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
        private Mesh? rifleMesh;
        private Mesh? pistolMesh;
        private Material[]? team1Palette;
        private Material[]? team2Palette;
        private Material? rifleMetal;
        private Material? riflePolymer;
        private WeaponRecipe? rifleRecipe;
        private WeaponRecipe? pistolRecipe;
        private EntityId localHuman;
        private Camera? fpsCamera;
        private Transform? localWeapon;
        private readonly ProceduralViewModel localView = new ProceduralViewModel();
        private Float3 rifleSightSocketLocal;
        private Float3 pistolSightSocketLocal;
        private bool ready;
        private bool visualsBound;
        private string reviewMapId = "";
        private string reviewProfile = "";
        private uint reviewTick;
        private float aggregateMinArmClearance = float.MaxValue;
        private float aggregateMaxEyeSightError;
        private int reviewSampleCount;

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
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Required locked URP Lit shader missing; no art fallback.");

            // Recipes for motion/sockets only — no SoldierGeometry/WeaponGeometry mesh generation on the product path.
            rifleRecipe = WeaponRecipe.Parse(Encoding.ASCII.GetBytes(
                "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/rifle\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.0.1\",\"seed\":\"460072030\",\"parameters\":{\"kind\":\"rifle\"},\"dependencies\":[]}"));
            pistolRecipe = WeaponRecipe.Parse(Encoding.ASCII.GetBytes(
                "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/pistol\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.0.1\",\"seed\":\"460072030\",\"parameters\":{\"kind\":\"pistol\"},\"dependencies\":[]}"));

            localWeapon = new GameObject("Local FP weapon").transform;
            localWeapon.SetParent(fpsCamera!.transform, false);
            localWeapon.gameObject.AddComponent<MeshFilter>();
            localWeapon.gameObject.AddComponent<MeshRenderer>();
            localWeapon.localPosition = new Vector3(0.08f, -0.10f, 0.40f);
            localWeapon.localRotation = Quaternion.identity;
            localWeapon.gameObject.SetActive(false);
            ready = true;
            visualsBound = false;
            Debug.Log("SHOOTER_PRODUCT_ACTOR_VIEW awaiting packaged visuals");
        }

        /// <summary>
        /// Upload verified packaged soldier/rifle/pistol meshes and materials. Missing deps fail recoverably; no generation fallback.
        /// </summary>
        public void BindPackagedVisuals(PackagedVisualPayloads visuals)
        {
            if (visuals == null) throw new ArgumentNullException(nameof(visuals));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Required locked URP Lit shader missing; no art fallback.");

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

                int soldierLod = MapVisualClosure.SoldierLodIndex(visuals.Profile);
                int weaponLod = MapVisualClosure.WeaponLodIndex(visuals.Profile);
                bodyMesh = Own(Upload(MapVisualClosure.DecodeSoldierLod(visuals, soldierLod), true));
                rifleMesh = Own(Upload(MapVisualClosure.DecodeRifleLod(visuals, weaponLod), false));
                pistolMesh = Own(Upload(MapVisualClosure.DecodePistolLod(visuals, weaponLod), false));

                rifleSightSocketLocal = FindSocket(WeaponGeometry.Sockets(rifleRecipe!), "sight-eye");
                pistolSightSocketLocal = FindSocket(WeaponGeometry.Sockets(pistolRecipe!), "sight-eye");

                team1Palette = new[]
                {
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[0], "Team1 uniform"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[1], "Team1 gear"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[2], "Team1 skin"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[3], "Team1 goggles", 0.3f, 0.55f)
                };
                team2Palette = new[]
                {
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[4], "Team2 uniform"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[5], "Team2 gear"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[6], "Team2 skin"),
                    FromPackaged(shader, visuals, MapVisualClosure.SoldierMaterialPaths[7], "Team2 goggles", 0.3f, 0.55f)
                };
                rifleMetal = FromPackaged(shader, visuals, MapVisualClosure.WeaponMaterialPaths[0], "Product rifle metal", 0.15f, 0.35f);
                riflePolymer = FromPackaged(shader, visuals, MapVisualClosure.WeaponMaterialPaths[1], "Product rifle polymer");

                ApplyLocalWeaponVisual(ProductAuthorityActionView.RifleSlot);

                visualsBound = true;
                Debug.Log("SHOOTER_PRODUCT_ACTOR_VIEW packaged visuals ready profile=" + visuals.Profile +
                          " soldierLod=" + soldierLod + " weaponLod=" + weaponLod);
            }
            catch (Exception error)
            {
                visualsBound = false;
                bodyMesh = null;
                rifleMesh = null;
                pistolMesh = null;
                throw new InvalidOperationException("Required packaged actor visuals missing/corrupt; refusing generation fallback: " + error.Message, error);
            }
        }

        public void ClearAll()
        {
            binder.Clear();
            actions.Clear();
            localView.ResetForLife();
            foreach (var pair in avatars)
                if (pair.Value.Root != null) DestroyObject(pair.Value.Root.gameObject);
            avatars.Clear();
            if (localWeapon != null) localWeapon.gameObject.SetActive(false);
            aggregateMinArmClearance = float.MaxValue;
            aggregateMaxEyeSightError = 0f;
            reviewSampleCount = 0;
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
            Func<EntityId, LocalMovementView?>? movementView = null)
        {
            if (!ready || !visualsBound) return;
            localHuman = humanEntity;
            livingScratch.Clear();
            corpseScratch.Clear();

            if (combatEvents != null)
                actions.ApplyEvents(combatEvents);

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
                byte slot = human.ActiveWeaponSlot;
                actions.SyncLifecycle(human.EntityId, slot, true);
                localView.SetActiveWeaponSlot(slot);
                ApplyLocalWeaponVisual(slot);
                if (combatEvents != null)
                    localView.ConsumeEvents(combatEvents, human.EntityId, aimHeld, dtSeconds);
                else
                    localView.TickAim(aimHeld, dtSeconds);
                var pose = localView.Pose;
                localWeapon.localPosition = new Vector3(pose.RightHand.X, pose.RightHand.Y, pose.RightHand.Z);
                localWeapon.localRotation = Quaternion.Euler(pose.RecoilPitch * Mathf.Rad2Deg, pose.RecoilYaw * Mathf.Rad2Deg, 0f);
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
            if (!visualsBound || rifleRecipe == null || bodyMesh == null || rifleMesh == null || pistolMesh == null) return;

            byte slot = state.ActiveWeaponSlot;
            actions.SyncLifecycle(state.EntityId, slot, state.Alive && !corpse);
            var avatar = new Avatar
            {
                EntityId = state.EntityId,
                Team = state.Team,
                Corpse = corpse,
                WeaponSlot = slot,
                Root = new GameObject("Actor " + state.EntityId.Value).transform,
                Bones = new Transform[SoldierSkeleton.JointCount],
                // Generation SoldierMotion spike validates rifle sockets; pistol mesh shares the bind.
                Motion = new SoldierMotion(rifleRecipe),
                PreviousFeet = state.Feet,
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
            ApplyHeldWeaponVisual(avatar, slot);

            bool local = state.EntityId.Equals(localHuman) && !corpse;
            if (local)
            {
                // Local body stays for shadow/third-person coherence but weapons use FP child.
                avatar.Body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

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

            byte slot = state.ActiveWeaponSlot;
            actions.SyncLifecycle(state.EntityId, slot, state.Alive && !avatar.Corpse);
            if (slot != avatar.WeaponSlot)
            {
                avatar.WeaponSlot = slot;
                ApplyHeldWeaponVisual(avatar, slot);
            }

            // Authority ReloadStarted/Completed only — never magazine decrease.
            if (!avatar.Corpse && state.Alive)
                avatar.ReloadPhase = actions.ResolveReloadPhase(state.EntityId, completedTick, dtSeconds);
            else
            {
                avatar.ReloadPhase = -1f;
                actions.SyncLifecycle(state.EntityId, slot, false);
            }

            bool reset = !avatar.HasPrevious || avatar.Corpse || !state.Alive;
            ground.Height = state.Feet.Y;
            float yaw = state.AimYaw;
            float pitch = Mathf.Clamp(state.AimPitch, -SoldierMotion.MaxAimPitchRadians, SoldierMotion.MaxAimPitchRadians);
            float aimYaw = Mathf.Clamp(0f, -SoldierMotion.MaxAimYawRadians, SoldierMotion.MaxAimYawRadians);
            if (avatar.Corpse || !state.Alive)
            {
                speed = 0f;
                pitch = 0f;
            }

            bool crouching = movement.HasValue && movement.Value.Stance == Stance.Crouching;
            if (movement.HasValue && movement.Value.Sliding) speed = 0f; // Slide momentum is not a running gait.
            var input = new SoldierMotionInput(
                state.Feet,
                yaw,
                avatar.Distance,
                avatar.Corpse || !state.Alive ? 0f : speed,
                avatar.Corpse || !state.Alive || crouching ? 1f : 0f,
                aimYaw,
                pitch,
                avatar.ReloadPhase,
                reset);
            avatar.Root.position = V(state.Feet);
            var pose = avatar.Motion.Evaluate(input, ground);
            for (int j = 0; j < SoldierSkeleton.JointCount; j++)
                avatar.Bones[j].SetPositionAndRotation(V(pose.WorldPositions[j]), Q(pose.WorldRotations[j]));

            Float3 weaponPosition = pose.WeaponPosition;
            Quat weaponRotation = pose.WeaponRotation;
            if (ProductAuthorityActionView.IsPistolSlot(slot) && !avatar.Corpse && state.Alive)
                weaponPosition = pose.WorldPositions[RightHandJoint];

            avatar.Weapon.SetPositionAndRotation(V(weaponPosition), Q(weaponRotation));
            avatar.Weapon.gameObject.SetActive(!avatar.Corpse && state.Alive);

            Float3 sightLocal = ProductAuthorityActionView.IsPistolSlot(slot) ? pistolSightSocketLocal : rifleSightSocketLocal;
            avatar.LastArmBodyClearance = pose.ArmBodyClearanceMeters;
            avatar.LastEyeSightLineError = pose.EyeSightLineErrorMeters;
            avatar.LastLeftGripError = pose.LeftGripError;
            avatar.LastRightGripError = pose.RightGripError;
            avatar.LastPlantSlide = pose.PlantSlide;
            avatar.LastStockError = pose.RightStockErrorMeters;
            avatar.LastAimPitch = pitch;
            avatar.LastRightEye = pose.RightEyePosition;
            avatar.LastWeaponPosition = weaponPosition;
            avatar.LastSightWorld = Add(weaponPosition, Rotate(sightLocal, weaponRotation));
            avatar.LastFeet = state.Feet;
            avatar.HasReviewMetrics = true;

            avatar.PreviousFeet = state.Feet;
            avatar.PreviousHealth = state.Health;
            avatar.HasPrevious = true;
            avatar.Team = state.Team;
            if (avatar.Body != null)
                avatar.Body.sharedMaterials = state.Team == 1 ? team1Palette : team2Palette;
        }

        private void ApplyHeldWeaponVisual(Avatar avatar, byte slot)
        {
            bool pistol = ProductAuthorityActionView.IsPistolSlot(slot);
            avatar.Weapon.name = pistol ? "Held pistol" : "Held rifle";
            avatar.WeaponFilter.sharedMesh = pistol ? pistolMesh : rifleMesh;
            avatar.WeaponRenderer.sharedMaterials = new[] { rifleMetal, riflePolymer };
        }

        private void ApplyLocalWeaponVisual(byte slot)
        {
            if (localWeapon == null || !visualsBound) return;
            bool pistol = ProductAuthorityActionView.IsPistolSlot(slot);
            localWeapon.name = pistol ? "Local FP pistol" : "Local FP rifle";
            localWeapon.GetComponent<MeshFilter>().sharedMesh = pistol ? pistolMesh : rifleMesh;
            localWeapon.GetComponent<MeshRenderer>().sharedMaterials = new[] { rifleMetal, riflePolymer };
        }

        private static Float3 FindSocket(FrozenList<WeaponSocket> sockets, string id)
        {
            for (int i = 0; i < sockets.Count; i++)
                if (sockets[i].Id == id) return sockets[i].Position;
            return default;
        }

        private static Float3 Add(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        private static Float3 Rotate(Float3 v, Quat q)
        {
            // Match Generation MotionMath quaternion rotation without pulling private helpers.
            float x2 = q.X + q.X, y2 = q.Y + q.Y, z2 = q.Z + q.Z;
            float xx = q.X * x2, yy = q.Y * y2, zz = q.Z * z2;
            float xy = q.X * y2, xz = q.X * z2, yz = q.Y * z2;
            float wx = q.W * x2, wy = q.W * y2, wz = q.W * z2;
            return new Float3(
                (1f - (yy + zz)) * v.X + (xy - wz) * v.Y + (xz + wy) * v.Z,
                (xy + wz) * v.X + (1f - (xx + zz)) * v.Y + (yz - wx) * v.Z,
                (xz - wy) * v.X + (yz + wx) * v.Y + (1f - (xx + yy)) * v.Z);
        }

        private void DestroyAvatar(EntityId id)
        {
            if (!avatars.TryGetValue(id.Value, out var avatar)) return;
            avatars.Remove(id.Value);
            actions.Forget(id);
            if (avatar.Root != null) DestroyObject(avatar.Root.gameObject);
        }

        private Mesh Upload(MeshData data, bool skin)
        {
            var mesh = Own(new Mesh { name = skin ? "Product soldier" : "Product weapon" });
            mesh.vertices = data.Positions.Select(V).ToArray();
            mesh.normals = data.Normals.Select(V).ToArray();
            mesh.tangents = data.Tangents.Select(t => new Vector4(t.X, t.Y, t.Z, t.W)).ToArray();
            mesh.uv = data.Uv.Select(t => new Vector2(t.X, t.Y)).ToArray();
            mesh.subMeshCount = data.Submeshes.Count;
            for (int i = 0; i < data.Submeshes.Count; i++)
            {
                var sub = data.Submeshes[i];
                mesh.SetIndices(
                    data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v => (int)v).ToArray(),
                    MeshTopology.Triangles,
                    i);
            }
            if (skin)
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
                mesh.bindposes = Enumerable.Range(0, SoldierSkeleton.JointCount)
                    .Select(j => Matrix4x4.TRS(V(SoldierSkeleton.BindPositions[j]), Quaternion.identity, Vector3.one).inverse)
                    .ToArray();
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
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
            var material = Own(new Material(shader) { name = label });
            var mip0 = data.MipBytes[0];
            material.SetColor("_BaseColor", new Color(mip0[0] / 255f, mip0[1] / 255f, mip0[2] / 255f, mip0[3] / 255f));
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            return material;
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
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null) DestroyObject(owned[i]);
            owned.Clear();
        }
    }
}
