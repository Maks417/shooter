#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Presentation.View;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Presentation.Rig
{
    /// <summary>
    /// Authority-bound procedural soldiers/weapons for the product client.
    /// Reuses SoldierGeometry/WeaponGeometry/SoldierMotion; never launches galleries or mutates authority.
    /// </summary>
    public sealed class ProductActorPresentation : MonoBehaviour
    {
        private sealed class Avatar
        {
            public EntityId EntityId;
            public byte Team;
            public bool Corpse;
            public Transform Root = null!;
            public Transform Weapon = null!;
            public Transform[] Bones = null!;
            public SkinnedMeshRenderer Body = null!;
            public MeshRenderer WeaponRenderer = null!;
            public SoldierMotion Motion = null!;
            public Float3 PreviousFeet;
            public double Distance;
            public ushort PreviousHealth = 100;
            public ushort PreviousMagazine = 30;
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
        private readonly List<ActorLifecycleState> livingScratch = new List<ActorLifecycleState>(16);
        private readonly List<ActorLifecycleState> corpseScratch = new List<ActorLifecycleState>(4);
        private readonly List<EntityId> spawnScratch = new List<EntityId>(16);
        private readonly List<EntityId> despawnScratch = new List<EntityId>(16);
        private readonly List<EntityId> corpseEnsureScratch = new List<EntityId>(4);
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly FlatGround ground = new FlatGround();
        private Mesh? bodyMesh;
        private Mesh? rifleMesh;
        private Material[]? team1Palette;
        private Material[]? team2Palette;
        private Material? rifleMetal;
        private Material? riflePolymer;
        private WeaponRecipe? rifleRecipe;
        private SoldierRecipe? soldierRecipe;
        private EntityId localHuman;
        private Camera? fpsCamera;
        private Transform? localWeapon;
        private readonly ProceduralViewModel localView = new ProceduralViewModel();
        private Float3 sightSocketLocal;
        private bool ready;
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

            soldierRecipe = SoldierRecipe.Parse(Encoding.ASCII.GetBytes(
                "{\"schemaId\":\"shooter.soldier.v1\",\"recipeId\":\"soldiers/baseline\",\"generatorId\":\"soldier.loft\",\"generatorVersion\":\"1.0.2\",\"seed\":\"460072030\",\"parameters\":{\"heightMm\":1800,\"shoulderWidthMm\":420,\"hipWidthMm\":280,\"uniformOffsetMm\":6,\"nearTorsoSegments\":16,\"nearLimbSegments\":12,\"walkCycleMm\":1400,\"runCycleMm\":2200,\"walkDutyPpm\":500000,\"runDutyPpm\":350000},\"dependencies\":[]}"));
            rifleRecipe = WeaponRecipe.Parse(Encoding.ASCII.GetBytes(
                "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/rifle\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.0.1\",\"seed\":\"460072030\",\"parameters\":{\"kind\":\"rifle\"},\"dependencies\":[]}"));

            var soldierBuild = SoldierGeometry.Build(soldierRecipe);
            bodyMesh = Own(Upload(soldierBuild.Lods[QualitySettings.GetQualityLevel() >= 2 ? 0 : 1], true));
            var weaponBuild = WeaponGeometry.Build(rifleRecipe);
            rifleMesh = Own(Upload(weaponBuild.Lods[1], false));
            sightSocketLocal = default;
            for (int i = 0; i < weaponBuild.Sockets.Count; i++)
            {
                var socket = weaponBuild.Sockets[i];
                if (socket.Id == "sight-eye")
                {
                    sightSocketLocal = socket.Position;
                    break;
                }
            }

            team1Palette = new[]
            {
                Solid(shader, "Team1 uniform", new Color(0.16f, 0.28f, 0.42f)),
                Solid(shader, "Team1 gear", new Color(0.06f, 0.08f, 0.12f)),
                Solid(shader, "Team1 skin", new Color(0.48f, 0.32f, 0.22f)),
                Solid(shader, "Team1 goggles", new Color(0.04f, 0.08f, 0.12f), 0.3f, 0.55f)
            };
            team2Palette = new[]
            {
                Solid(shader, "Team2 uniform", new Color(0.42f, 0.22f, 0.12f)),
                Solid(shader, "Team2 gear", new Color(0.12f, 0.07f, 0.05f)),
                Solid(shader, "Team2 skin", new Color(0.48f, 0.32f, 0.22f)),
                Solid(shader, "Team2 goggles", new Color(0.10f, 0.04f, 0.03f), 0.3f, 0.55f)
            };
            rifleMetal = Solid(shader, "Product rifle metal", new Color(0.08f, 0.09f, 0.10f), 0.15f, 0.35f);
            riflePolymer = Solid(shader, "Product rifle polymer", new Color(0.05f, 0.055f, 0.045f));

            localWeapon = new GameObject("Local FP rifle").transform;
            localWeapon.SetParent(fpsCamera!.transform, false);
            localWeapon.gameObject.AddComponent<MeshFilter>().sharedMesh = rifleMesh;
            var lr = localWeapon.gameObject.AddComponent<MeshRenderer>();
            lr.sharedMaterials = new[] { rifleMetal, riflePolymer };
            localWeapon.localPosition = new Vector3(0.08f, -0.10f, 0.40f);
            localWeapon.localRotation = Quaternion.identity;
            localWeapon.gameObject.SetActive(false);
            ready = true;
            Debug.Log("SHOOTER_PRODUCT_ACTOR_VIEW ready lodTriangles≈product");
        }

        public void ClearAll()
        {
            binder.Clear();
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
            float dtSeconds)
        {
            if (!ready) return;
            localHuman = humanEntity;
            livingScratch.Clear();
            corpseScratch.Clear();

            if (snapshot != null && lifecycle != null)
            {
                for (int i = 0; i < snapshot.Participants.Count; i++)
                {
                    var p = snapshot.Participants[i];
                    if (!p.Alive || !p.EntityId.IsValid) continue;
                    if (!lifecycle.TryGetActorState(p.EntityId, out var state) || !state.Alive) continue;
                    livingScratch.Add(state);
                }
            }

            if (tryCorpse != null)
            {
                for (int i = 0; i < corpseCount && i < ProductActorBinder.MaxCosmeticCorpses; i++)
                {
                    var state = tryCorpse(i);
                    if (state.EntityId.IsValid) corpseScratch.Add(state);
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
                PoseAvatar(avatar, state, dtSeconds);
            }

            bool showLocalWeapon = humanEntity.IsValid
                && lifecycle != null
                && lifecycle.TryGetActorState(humanEntity, out var human)
                && human.Alive;
            if (localWeapon != null) localWeapon.gameObject.SetActive(showLocalWeapon);
            if (showLocalWeapon)
            {
                // Cosmetic FP spring only; hit poses remain authority feet/aim.
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
            if (rifleRecipe == null || bodyMesh == null || rifleMesh == null) return;

            var avatar = new Avatar
            {
                EntityId = state.EntityId,
                Team = state.Team,
                Corpse = corpse,
                Root = new GameObject("Actor " + state.EntityId.Value).transform,
                Bones = new Transform[SoldierSkeleton.JointCount],
                Motion = new SoldierMotion(rifleRecipe),
                PreviousFeet = state.Feet,
                PreviousHealth = state.Health,
                PreviousMagazine = state.Magazine
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

            avatar.Weapon = new GameObject("Held rifle").transform;
            avatar.Weapon.SetParent(avatar.Root, false);
            avatar.Weapon.gameObject.AddComponent<MeshFilter>().sharedMesh = rifleMesh;
            avatar.WeaponRenderer = avatar.Weapon.gameObject.AddComponent<MeshRenderer>();
            avatar.WeaponRenderer.sharedMaterials = new[] { rifleMetal, riflePolymer };

            bool local = state.EntityId.Equals(localHuman) && !corpse;
            if (local)
            {
                // Local body stays for shadow/third-person coherence but weapons use FP child.
                avatar.Body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            avatars[state.EntityId.Value] = avatar;
            PoseAvatar(avatar, state, 0f);
        }

        private void PoseAvatar(Avatar avatar, ActorLifecycleState state, float dtSeconds)
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

            if (!avatar.Corpse && state.Alive)
            {
                if (state.Magazine > avatar.PreviousMagazine) avatar.ReloadPhase = -1f;
                else if (state.Magazine < avatar.PreviousMagazine && state.Reserve > avatar.PreviousMagazine)
                    avatar.ReloadPhase = 0.01f;
                if (avatar.ReloadPhase >= 0f)
                {
                    avatar.ReloadPhase = Mathf.Min(1f, avatar.ReloadPhase + dtSeconds / 2.4f);
                    if (avatar.ReloadPhase >= 1f) avatar.ReloadPhase = -1f;
                }
            }
            else avatar.ReloadPhase = -1f;

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

            var input = new SoldierMotionInput(
                state.Feet,
                yaw,
                avatar.Distance,
                avatar.Corpse || !state.Alive ? 0f : speed,
                avatar.Corpse || !state.Alive ? 1f : 0f,
                aimYaw,
                pitch,
                avatar.ReloadPhase,
                reset);
            avatar.Root.position = V(state.Feet);
            var pose = avatar.Motion.Evaluate(input, ground);
            for (int j = 0; j < SoldierSkeleton.JointCount; j++)
                avatar.Bones[j].SetPositionAndRotation(V(pose.WorldPositions[j]), Q(pose.WorldRotations[j]));
            avatar.Weapon.SetPositionAndRotation(V(pose.WeaponPosition), Q(pose.WeaponRotation));
            avatar.Weapon.gameObject.SetActive(!avatar.Corpse && state.Alive);

            avatar.LastArmBodyClearance = pose.ArmBodyClearanceMeters;
            avatar.LastEyeSightLineError = pose.EyeSightLineErrorMeters;
            avatar.LastLeftGripError = pose.LeftGripError;
            avatar.LastRightGripError = pose.RightGripError;
            avatar.LastPlantSlide = pose.PlantSlide;
            avatar.LastStockError = pose.RightStockErrorMeters;
            avatar.LastAimPitch = pitch;
            avatar.LastRightEye = pose.RightEyePosition;
            avatar.LastWeaponPosition = pose.WeaponPosition;
            avatar.LastSightWorld = Add(pose.WeaponPosition, Rotate(sightSocketLocal, pose.WeaponRotation));
            avatar.LastFeet = state.Feet;
            avatar.HasReviewMetrics = true;

            avatar.PreviousFeet = state.Feet;
            avatar.PreviousHealth = state.Health;
            avatar.PreviousMagazine = state.Magazine;
            avatar.HasPrevious = true;
            avatar.Team = state.Team;
            if (avatar.Body != null)
                avatar.Body.sharedMaterials = state.Team == 1 ? team1Palette : team2Palette;
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
            if (avatar.Root != null) DestroyObject(avatar.Root.gameObject);
        }

        private Mesh Upload(MeshData data, bool skin)
        {
            var mesh = Own(new Mesh { name = skin ? "Product soldier" : "Product rifle" });
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

        private Material Solid(Shader shader, string label, Color color, float metallic = 0f, float smoothness = 0.2f)
        {
            var material = Own(new Material(shader) { name = label });
            material.SetColor("_BaseColor", color);
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
