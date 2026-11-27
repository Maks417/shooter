#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.View;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Shooter.Presentation.Rig
{
    /// <summary>
    /// ADR-124 licensed authored soldier body for one actor. Authored clips (Playables, manual evaluation, no
    /// controller asset) drive the legs, hips and spine; the procedural SoldierMotion pose still owns the upper body
    /// task: the chest, head and arms are retargeted from the semantic rig and the hands are solved onto the weapon
    /// grips, so aim, grips, reload, knife and grenade overlays keep their validated choreography. Generated military
    /// gear rides the humanoid bones. Presentation only: hit volumes stay the analytic capsules.
    /// </summary>
    /// <summary>How the hands hold the current item: both on a long gun, the trigger hand on a pistol, or free (knife, grenade).</summary>
    public enum SoldierGrip : byte { Rifle = 0, Pistol = 1, None = 2 }

    public sealed class AuthoredSoldierBody : IDisposable
    {
        /// <summary>Per-variant data shared by every actor that wears it.</summary>
        public sealed class Prototype
        {
            public SoldierSkinCatalog.Variant Variant = null!;
            public float Scale;
            /// <summary>Humanoid bone T-pose world rotation (root identity) for each semantic joint, or null when unmapped.</summary>
            public Quaternion?[] BindOffset = new Quaternion?[SoldierSkeleton.JointCount];
            public Mesh?[] GearNear = Array.Empty<Mesh?>(), GearFar = Array.Empty<Mesh?>();
            public int[] GearJoints = Array.Empty<int>();
            /// <summary>Yaw that turns the model to face +Z like the semantic rig.</summary>
            public Quaternion Facing = Quaternion.identity;
            /// <summary>Centre of the head surface in the head bone's local space.</summary>
            public Vector3 HeadCentreLocal;
            /// <summary>Measured gear fit per semantic joint (final metres, T-pose character axes).</summary>
            public readonly Dictionary<int, SoldierSkinCatalog.GearFit> AutoFit = new Dictionary<int, SoldierSkinCatalog.GearFit>();
            /// <summary>Region-painted copies of bare base-body meshes, by renderer name (submeshes: uniform, skin, gloves, boots).</summary>
            public readonly Dictionary<string, Mesh> Painted = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            /// <summary>Copies with hidden material slots emptied (hair under a generated helmet), by renderer name.</summary>
            public readonly Dictionary<string, Mesh> Trimmed = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            /// <summary>
            /// Owned copies of every other visible authored mesh, by renderer name: a body never draws an imported
            /// asset mesh, so destroying the owner (menu stage, match presentation) releases all of its geometry.
            /// </summary>
            public readonly Dictionary<string, Mesh> Copies = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            /// <summary>T-pose world rotation of each mapped finger bone (semantic hand frame = identity at bind).</summary>
            public readonly Dictionary<HumanBodyBones, Quaternion> FingerBind = new Dictionary<HumanBodyBones, Quaternion>();
        }

        /// <summary>Painted body regions; also the submesh order of a painted mesh.</summary>
        public const int RegionUniform = 0, RegionSkin = 1, RegionGlove = 2, RegionBoot = 3;

        // Semantic joint → humanoid bone (UpperChest falls back to Chest when the rig has none).
        private static readonly HumanBodyBones[] Map =
        {
            HumanBodyBones.LastBone, HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
        };
        private const int ChestJoint = 4, Spine1Joint = 3, NeckJoint = 5, HeadJoint = 6;
        // Upper-body joints taken from the procedural pose, with their blend weight.
        private static readonly (int joint, float weight)[] UpperBody =
        {
            (Spine1Joint, .5f), (ChestJoint, 1f), (NeckJoint, 1f), (HeadJoint, 1f),
            (7, 1f), (8, 1f), (9, 1f), (10, 1f), (11, 1f), (12, 1f), (13, 1f), (14, 1f)
        };

        public GameObject Instance { get; }
        public Prototype Shape { get; }
        public IReadOnlyList<Renderer> Renderers => renderers;
        private readonly Transform?[] bones = new Transform?[SoldierSkeleton.JointCount];
        private readonly Animator animator;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<SkinnedMeshRenderer> skinned = new List<SkinnedMeshRenderer>();
        private readonly Dictionary<Renderer, Material[]> authoredMaterials = new Dictionary<Renderer, Material[]>();
        private readonly List<(int joint, MeshFilter filter, MeshRenderer renderer, Mesh? near, Mesh? far)> gear = new List<(int, MeshFilter, MeshRenderer, Mesh?, Mesh?)>();
        private readonly SoldierSkinCatalog catalog;
        private PlayableGraph graph;
        private AnimationMixerPlayable top, locomotion;
        private readonly AnimationClipPlayable[] roleClips = new AnimationClipPlayable[(int)SoldierClipRole.Count];
        private readonly bool[] hasRole = new bool[(int)SoldierClipRole.Count];
        private AnimationClipPlayable death;
        private AnimationClip? deathClip;
        private float idleTime, deathTime = -1f, upperWeight = 1f;
        private readonly AuthoredSoldierBlend blend = new AuthoredSoldierBlend();
        private int lod;
        private float legYaw, facingYaw;
        private readonly Material[] teamPaint;
        private readonly List<(HumanBodyBones bone, Transform transform, Quaternion bind)> fingers = new List<(HumanBodyBones, Transform, Quaternion)>();
        private readonly Dictionary<SkinnedMeshRenderer, Material> paintedSkin = new Dictionary<SkinnedMeshRenderer, Material>();

        /// <param name="teamPaint">Team materials for the painted regions: uniform, gloves, boots.</param>
        public AuthoredSoldierBody(SoldierSkinCatalog catalog, Prototype shape, Transform parent, ulong entityId, Func<Material, Material> restyle, Material[] teamGear, Material[] teamPaint)
        {
            this.catalog = catalog;
            Shape = shape;
            this.teamPaint = (Material[])teamPaint.Clone();
            Instance = UnityEngine.Object.Instantiate(shape.Variant.Model, parent, false);
            Instance.name = "Authored soldier " + shape.Variant.Id;
            Instance.transform.localPosition = Vector3.zero;
            Instance.transform.localRotation = Quaternion.identity;
            Instance.transform.localScale = Vector3.one * shape.Scale;
            AttachParts(Instance, shape.Variant);
            animator = Instance.GetComponentInChildren<Animator>(true);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = null;
            for (int j = 1; j < bones.Length; j++)
                bones[j] = BoneFor(animator, j);
            foreach (var pair in shape.FingerBind)
            {
                var bone = animator.GetBoneTransform(pair.Key);
                if (bone != null) fingers.Add((pair.Key, bone, pair.Value));
            }

            foreach (var r in Instance.GetComponentsInChildren<Renderer>(true))
            {
                if (shape.Variant.HideRenderers.Any(pattern => Matches(r.name, pattern)))
                {
                    // Hidden renderers drop their imported mesh and materials too, so the body holds no asset references.
                    r.enabled = false;
                    r.sharedMaterials = Array.Empty<Material>();
                    if (r is SkinnedMeshRenderer hidden) hidden.sharedMesh = null;
                    else if (r.TryGetComponent<MeshFilter>(out var hiddenFilter)) hiddenFilter.sharedMesh = null;
                    continue;
                }
                renderers.Add(r);
                if (r is SkinnedMeshRenderer smr)
                {
                    skinned.Add(smr); smr.updateWhenOffscreen = true; smr.quality = SkinQuality.Bone4;
                    if (shape.Painted.TryGetValue(smr.name, out var painted))
                    {
                        paintedSkin[smr] = smr.sharedMaterial;
                        smr.sharedMesh = painted;
                        smr.sharedMaterials = PaintMaterials(restyle(smr.sharedMaterial));
                        continue;
                    }
                    if (shape.Trimmed.TryGetValue(smr.name, out var trimmed)) smr.sharedMesh = trimmed;
                    else if (shape.Copies.TryGetValue(smr.name, out var copy)) smr.sharedMesh = copy;
                }
                else if (r.TryGetComponent<MeshFilter>(out var staticFilter) && shape.Copies.TryGetValue(r.name, out var staticCopy))
                    staticFilter.sharedMesh = staticCopy;
                authoredMaterials[r] = r.sharedMaterials;
                r.sharedMaterials = r.sharedMaterials.Select(restyle).ToArray();
            }

            for (int i = 0; i < shape.GearJoints.Length; i++)
            {
                var go = new GameObject("Gear " + SoldierSkeleton.Names[shape.GearJoints[i]]);
                go.transform.SetParent(parent, false);
                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                filter.sharedMesh = shape.GearNear[i];
                renderer.sharedMaterials = teamGear;
                renderers.Add(renderer);
                gear.Add((shape.GearJoints[i], filter, renderer, shape.GearNear[i], shape.GearFar[i]));
            }

            graph = PlayableGraph.Create("Authored soldier " + entityId);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Body", animator);
            top = AnimationMixerPlayable.Create(graph, 2);
            locomotion = AnimationMixerPlayable.Create(graph, (int)SoldierClipRole.Count);
            for (int r = 0; r < (int)SoldierClipRole.Count; r++)
            {
                var clip = catalog.Roles[r];
                if (clip == null) continue;
                hasRole[r] = true;
                roleClips[r] = AnimationClipPlayable.Create(graph, clip);
                roleClips[r].SetApplyFootIK(true);
                roleClips[r].SetSpeed(0);
                graph.Connect(roleClips[r], 0, locomotion, r);
            }
            graph.Connect(locomotion, 0, top, 0);
            deathClip = catalog.Deaths[(int)(entityId % (ulong)catalog.Deaths.Length)];
            death = AnimationClipPlayable.Create(graph, deathClip);
            death.SetSpeed(0);
            death.SetApplyFootIK(false);
            graph.Connect(death, 0, top, 1);
            top.SetInputWeight(0, 1f);
            top.SetInputWeight(1, 0f);
            output.SetSourcePlayable(top);
            graph.Play();
        }

        private Material[] PaintMaterials(Material skin) => new[] { teamPaint[0], skin, teamPaint[1], teamPaint[2] };

        /// <summary>Builds (once per variant) the facing fix, T-pose retarget offsets, body scale, clothed body meshes, gear meshes and gear fit.</summary>
        public static Prototype Prepare(SoldierSkinCatalog catalog, SoldierSkinCatalog.Variant variant, Func<MeshData, Mesh> upload, Action<UnityEngine.Object> own)
        {
            var shape = new Prototype { Variant = variant };
            var probe = UnityEngine.Object.Instantiate(variant.Model);
            try
            {
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                probe.transform.localScale = Vector3.one;
                AttachParts(probe, variant);
                var animator = probe.GetComponentInChildren<Animator>(true);
                ApplySkeletonTPose(probe.transform, animator.avatar.humanDescription);
                Transform B(HumanBodyBones b) => animator.GetBoneTransform(b) ?? throw new InvalidOperationException("Humanoid bone missing: " + b + " in " + variant.Id);
                // Facing: the semantic rig faces +Z with its left arm at -X; turn the model to match.
                var across = B(HumanBodyBones.RightUpperArm).position - B(HumanBodyBones.LeftUpperArm).position;
                var facing = Vector3.ProjectOnPlane(Vector3.Cross(across, Vector3.up), Vector3.up);
                if (facing.sqrMagnitude < 1e-8f) throw new InvalidOperationException("Cannot tell the facing of " + variant.Id);
                shape.Facing = Quaternion.FromToRotation(facing.normalized, Vector3.forward);
                probe.transform.rotation = shape.Facing;
                ForceTPose(animator);

                var bodies = probe.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => !variant.HideRenderers.Any(p => Matches(s.name, p))).ToArray();
                var bakedWorld = new Dictionary<SkinnedMeshRenderer, Vector3[]>();
                var bounds = new Bounds(); bool any = false;
                foreach (var smr in bodies)
                {
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    var world = baked.vertices.Select(v => smr.transform.TransformPoint(v)).ToArray();
                    Kill(baked);
                    bakedWorld[smr] = world;
                    foreach (var w in world) { if (!any) { bounds = new Bounds(w, Vector3.zero); any = true; } else bounds.Encapsulate(w); }
                }
                if (!any || bounds.size.y < 1e-3f) throw new InvalidOperationException("Soldier model has no skinned mesh: " + variant.Id);
                float feetY = bounds.min.y;
                shape.Scale = catalog.BodyHeightMeters / bounds.size.y;
                for (int j = 1; j < SoldierSkeleton.JointCount; j++)
                {
                    var bone = BoneFor(animator, j);
                    if (bone != null) shape.BindOffset[j] = bone.rotation;
                }
                foreach (int required in new[] { 1, ChestJoint, HeadJoint, 8, 9, 10, 12, 13, 14 })
                    if (shape.BindOffset[required] == null) throw new InvalidOperationException("Humanoid bone missing for " + SoldierSkeleton.Names[required] + " in " + variant.Id);
                foreach (var finger in FingerBones)
                {
                    var bone = animator.GetBoneTransform(finger);
                    if (bone != null) shape.FingerBind[finger] = bone.rotation;
                }

                // Hidden material slots (hair under a generated helmet): empty those triangles and skip their vertices below.
                var keep = new Dictionary<SkinnedMeshRenderer, bool[]>();
                foreach (var smr in bodies)
                {
                    var mesh = smr.sharedMesh; var mats = smr.sharedMaterials;
                    var flags = new bool[mesh.vertexCount];
                    bool trims = false;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        bool hidden = sub < mats.Length && mats[sub] != null && variant.HideMaterials.Any(p => Matches(mats[sub].name, p));
                        trims |= hidden;
                        if (!hidden) foreach (int index in mesh.GetTriangles(sub)) flags[index] = true;
                    }
                    keep[smr] = flags;
                    if (!trims) continue;
                    var copy = UnityEngine.Object.Instantiate(mesh);
                    copy.name = mesh.name + " (ADR-124 trimmed)";
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        if (sub < mats.Length && mats[sub] != null && variant.HideMaterials.Any(p => Matches(mats[sub].name, p))) copy.SetTriangles(Array.Empty<int>(), sub, false);
                    own(copy);
                    shape.Trimmed[smr.name] = copy;
                }

                // Regions per vertex for every body renderer; painted renderers also get the generated clothing layer.
                var regions = new Dictionary<SkinnedMeshRenderer, int[]>();
                foreach (var smr in bodies) regions[smr] = Regions(smr, bakedWorld[smr], animator, bounds.size.y);
                foreach (var smr in bodies)
                    if (variant.PaintRenderers.Any(p => Matches(smr.name, p)))
                    {
                        var clothed = Clothe(smr, regions[smr], bakedWorld[smr]);
                        own(clothed);
                        shape.Painted[smr.name] = clothed;
                    }
                if (variant.PaintRenderers.Length != 0 && shape.Painted.Count == 0) throw new InvalidOperationException("No paintable body renderer in " + variant.Id);

                // Every other visible authored mesh is drawn from an owned copy: imported assets cannot be destroyed,
                // so a body that referenced them would pin them past its owner (menu stage lifetime check).
                foreach (var renderer in probe.GetComponentsInChildren<Renderer>(true))
                {
                    string rendererName = renderer.name;
                    if (variant.HideRenderers.Any(p => Matches(rendererName, p)) || shape.Painted.ContainsKey(rendererName)
                        || shape.Trimmed.ContainsKey(rendererName) || shape.Copies.ContainsKey(rendererName)) continue;
                    var source = renderer is SkinnedMeshRenderer skinnedSource ? skinnedSource.sharedMesh
                        : renderer.TryGetComponent<MeshFilter>(out var filterSource) ? filterSource.sharedMesh : null;
                    if (source == null) continue;
                    var owned = UnityEngine.Object.Instantiate(source);
                    owned.name = source.name + " (ADR-124 owned)";
                    own(owned);
                    shape.Copies[rendererName] = owned;
                }

                // Measurements in the final (scaled) frame for the gear fit and the head centre.
                float s = shape.Scale;
                Vector3 S(Vector3 world) => new Vector3(world.x * s, (world.y - feetY) * s, world.z * s);
                var skin = new List<Vector3>(); var torso = new List<Vector3>(); var hips = new List<Vector3>(); var legs = new List<Vector3>();
                var chestBones = new HashSet<Transform> { B(HumanBodyBones.Chest) };
                var upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest); if (upperChest != null) chestBones.Add(upperChest);
                var hipBone = B(HumanBodyBones.Hips);
                var kneeBones = new HashSet<Transform> { B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.RightLowerLeg) };
                foreach (var smr in bodies)
                {
                    var world = bakedWorld[smr]; var weights = smr.sharedMesh.boneWeights; var bones = smr.bones; var region = regions[smr]; var kept = keep[smr];
                    for (int i = 0; i < world.Length; i++)
                    {
                        if (!kept[i]) continue;
                        var bone = Dominant(weights[i], bones);
                        if (region[i] == RegionSkin && bone != null && bone.IsChildOf(B(HumanBodyBones.Head))) skin.Add(S(world[i]));
                        else if (bone != null && chestBones.Contains(bone)) torso.Add(S(world[i]));
                        else if (bone == hipBone) hips.Add(S(world[i]));
                        else if (bone != null && kneeBones.Contains(bone)) legs.Add(S(world[i]));
                    }
                }
                Vector3 Bone(HumanBodyBones b) => S(B(b).position);
                var headBone = Bone(HumanBodyBones.Head);
                if (skin.Count == 0 || torso.Count == 0) throw new InvalidOperationException("Cannot measure the head or torso of " + variant.Id);
                var headBox = Box(skin); var torsoBox = Box(torso);
                shape.HeadCentreLocal = B(HumanBodyBones.Head).InverseTransformPoint(B(HumanBodyBones.Head).position + (headBox.center - headBone) / s);

                // Helmet (semantic head joint +0.012..+0.1375 m, 0.117 m radius over a 0.164 x 0.21 m head).
                float hs = Mathf.Clamp(Mathf.Max(headBox.size.x / .164f, headBox.size.z / .21f), .8f, 1.6f);
                shape.AutoFit[HeadJoint] = new SoldierSkinCatalog.GearFit
                {
                    Offset = new Vector3(headBox.center.x - headBone.x, headBox.max.y + .014f - .1375f * hs - headBone.y, headBox.center.z - headBone.z - .004f * hs),
                    Scale = Vector3.one * hs
                };
                // Vest and backpack (semantic chest joint; top edge 0.045 m under the shoulder line, 0.432 x 0.252 m torso).
                var chestBone = S((upperChest != null ? upperChest : B(HumanBodyBones.Chest)).position);
                float shoulderY = (Bone(HumanBodyBones.LeftUpperArm).y + Bone(HumanBodyBones.RightUpperArm).y) * .5f;
                var vestScale = new Vector3(Mathf.Clamp(torsoBox.size.x / .40f, .8f, 1.5f), 1f, Mathf.Clamp(torsoBox.size.z / .25f, .8f, 1.6f));
                shape.AutoFit[ChestJoint] = new SoldierSkinCatalog.GearFit
                {
                    Offset = new Vector3(torsoBox.center.x - chestBone.x, shoulderY - .045f - .025f - chestBone.y, torsoBox.center.z - chestBone.z),
                    Scale = vestScale
                };
                // Belt (semantic pelvis joint, 0.35 x 0.258 m waist).
                if (hips.Count > 0)
                {
                    var hipBox = Box(hips); var pelvis = Bone(HumanBodyBones.Hips);
                    shape.AutoFit[1] = new SoldierSkinCatalog.GearFit
                    {
                        Offset = new Vector3(hipBox.center.x - pelvis.x, hipBox.max.y - .06f - pelvis.y + .03f, hipBox.center.z - pelvis.z),
                        Scale = new Vector3(Mathf.Clamp(hipBox.size.x / .35f, .8f, 1.5f) * 1.04f, 1f, Mathf.Clamp(hipBox.size.z / .258f, .8f, 1.6f) * 1.06f)
                    };
                }
                // Knee pads: front of the knee (semantic pad face 0.07 m ahead of the calf joint).
                foreach (var (joint, bone) in new[] { (16, HumanBodyBones.LeftLowerLeg), (20, HumanBodyBones.RightLowerLeg) })
                {
                    var knee = Bone(bone);
                    var near = legs.Where(p => Mathf.Abs(p.y - knee.y) < .05f && Mathf.Abs(p.x - knee.x) < .09f).ToList();
                    if (near.Count == 0) continue;
                    float front = near.Max(p => p.z);
                    shape.AutoFit[joint] = new SoldierSkinCatalog.GearFit { Offset = new Vector3(0f, 0f, front - knee.z - .062f), Scale = Vector3.one };
                }
            }
            finally { Kill(probe); }

            var nearGear = SoldierGearKit.Build(variant.Gear, true);
            var farGear = SoldierGearKit.Build(variant.Gear, false);
            shape.GearJoints = nearGear.Select(p => (int)p.Joint).ToArray();
            shape.GearNear = nearGear.Select(p => (Mesh?)upload(p.Mesh)).ToArray();
            shape.GearFar = farGear.Select(p => (Mesh?)upload(p.Mesh)).ToArray();
            return shape;
        }

        private static Bounds Box(List<Vector3> points)
        {
            var box = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) box.Encapsulate(p);
            return box;
        }

        private static Transform? Dominant(BoneWeight w, Transform[] bones)
        {
            int dominant = w.weight0 >= w.weight1 && w.weight0 >= w.weight2 && w.weight0 >= w.weight3 ? w.boneIndex0
                : w.weight1 >= w.weight2 && w.weight1 >= w.weight3 ? w.boneIndex1 : w.weight2 >= w.weight3 ? w.boneIndex2 : w.boneIndex3;
            return dominant >= 0 && dominant < bones.Length ? bones[dominant] : null;
        }

        /// <summary>
        /// ADR-124 body regions: skin (head and upper neck), gloves (hands), boots (feet and lower shins), uniform
        /// (everything else), by the bone that dominates each vertex in the T-pose.
        /// </summary>
        private static int[] Regions(SkinnedMeshRenderer smr, Vector3[] world, Animator animator, float height)
        {
            Transform B(HumanBodyBones b) => animator.GetBoneTransform(b) ?? throw new InvalidOperationException("Humanoid bone missing: " + b);
            var head = new HashSet<Transform>(B(HumanBodyBones.Head).GetComponentsInChildren<Transform>(true));
            var hands = new HashSet<Transform>(B(HumanBodyBones.LeftHand).GetComponentsInChildren<Transform>(true).Concat(B(HumanBodyBones.RightHand).GetComponentsInChildren<Transform>(true)));
            var feet = new HashSet<Transform>(B(HumanBodyBones.LeftFoot).GetComponentsInChildren<Transform>(true).Concat(B(HumanBodyBones.RightFoot).GetComponentsInChildren<Transform>(true)));
            var calves = new HashSet<Transform> { B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.RightLowerLeg) };
            var neck = B(HumanBodyBones.Neck);
            float neckY = neck.position.y, headY = B(HumanBodyBones.Head).position.y;
            float ankleY = Mathf.Max(B(HumanBodyBones.LeftFoot).position.y, B(HumanBodyBones.RightFoot).position.y);
            var weights = smr.sharedMesh.boneWeights;
            var bones = smr.bones;
            var region = new int[world.Length];
            for (int i = 0; i < region.Length; i++)
            {
                var bone = Dominant(weights[i], bones);
                float y = world[i].y;
                region[i] = bone == null ? RegionUniform
                    : head.Contains(bone) ? RegionSkin
                    : bone == neck ? (y > neckY + .45f * (headY - neckY) ? RegionSkin : RegionUniform)
                    : hands.Contains(bone) ? RegionGlove
                    : feet.Contains(bone) || (calves.Contains(bone) && y < ankleY + .10f * height) ? RegionBoot
                    : RegionUniform;
            }
            return region;
        }

        /// <summary>
        /// ADR-124 generated clothing layer for a bare base body. Triangles are regrouped by region (submeshes:
        /// uniform, skin, gloves, boots). Uniform and boot vertices are Taubin-smoothed over the welded surface, which
        /// removes muscle and anatomy detail without shrinking, then pushed out along the smoothed normal (cloth and
        /// boot thickness). Vertices touching skin or gloves stay pinned, so collars and cuffs close on the body.
        /// Skin weights, UVs and bind poses are the authored ones.
        /// </summary>
        private static Mesh Clothe(SkinnedMeshRenderer smr, int[] region, Vector3[] world)
        {
            var source = smr.sharedMesh;
            if (source.subMeshCount != 1) throw new InvalidOperationException("Clothed body needs one submesh: " + smr.name);
            var vertices = source.vertices;
            int n = vertices.Length;
            // Mesh-space size of a metre: the bind mesh and the baked world mesh differ only by the import transform.
            var worldBox = new Bounds(world[0], Vector3.zero); foreach (var p in world) worldBox.Encapsulate(p);
            float metre = worldBox.size.magnitude > 1e-5f ? source.bounds.size.magnitude / worldBox.size.magnitude : 1f;

            // Weld seams by position so smoothing sees one surface.
            var weld = new int[n]; var keys = new Dictionary<Vector3Int, int>();
            float q = 1e5f / Mathf.Max(1e-5f, source.bounds.size.magnitude);
            for (int i = 0; i < n; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(vertices[i].x * q), Mathf.RoundToInt(vertices[i].y * q), Mathf.RoundToInt(vertices[i].z * q));
                if (!keys.TryGetValue(k, out int w)) keys[k] = w = keys.Count;
                weld[i] = w;
            }
            int m = keys.Count;
            var neighbours = new HashSet<int>[m]; for (int i = 0; i < m; i++) neighbours[i] = new HashSet<int>();
            var triangles = source.GetTriangles(0);
            var cloth = new bool[m]; var pinned = new bool[m];
            for (int i = 0; i < n; i++) if (region[i] == RegionUniform || region[i] == RegionBoot) cloth[weld[i]] = true;
            for (int t = 0; t < triangles.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = weld[triangles[t + e]], b = weld[triangles[t + (e + 1) % 3]];
                    neighbours[a].Add(b); neighbours[b].Add(a);
                }
            for (int i = 0; i < n; i++) if (region[i] == RegionSkin || region[i] == RegionGlove) pinned[weld[i]] = true;
            var boot = new bool[m]; for (int i = 0; i < n; i++) if (region[i] == RegionBoot) boot[weld[i]] = true;

            var position = new Vector3[m];
            for (int i = 0; i < n; i++) position[weld[i]] = vertices[i];
            var next = new Vector3[m];
            for (int iteration = 0; iteration < 24; iteration++)
            {
                float factor = (iteration & 1) == 0 ? .55f : -.57f;
                for (int v = 0; v < m; v++)
                {
                    next[v] = position[v];
                    if (!cloth[v] || pinned[v] || neighbours[v].Count == 0) continue;
                    var mean = Vector3.zero; foreach (int u in neighbours[v]) mean += position[u];
                    mean /= neighbours[v].Count;
                    next[v] = position[v] + factor * (mean - position[v]);
                }
                (position, next) = (next, position);
            }

            var smoothed = UnityEngine.Object.Instantiate(source);
            var temp = UnityEngine.Object.Instantiate(source);
            var welded = new Vector3[n]; for (int i = 0; i < n; i++) welded[i] = position[weld[i]];
            temp.vertices = welded; temp.RecalculateNormals();
            var normals = temp.normals;
            // Average normals across the weld so the push keeps seams closed.
            var weldNormal = new Vector3[m]; for (int i = 0; i < n; i++) weldNormal[weld[i]] += normals[i];
            Kill(temp);
            var result = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                int w = weld[i];
                float push = !cloth[w] || pinned[w] ? 0f : (boot[w] ? .016f : .007f) * metre;
                result[i] = position[w] + weldNormal[w].normalized * push;
            }
            smoothed.name = source.name + " (ADR-124 clothed)";
            smoothed.vertices = result;
            smoothed.subMeshCount = 4;
            var lists = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = region[triangles[t]], b = region[triangles[t + 1]], c = region[triangles[t + 2]];
                int r = a == b || a == c ? a : b == c ? b : RegionUniform;
                lists[r].Add(triangles[t]); lists[r].Add(triangles[t + 1]); lists[r].Add(triangles[t + 2]);
            }
            for (int r = 0; r < 4; r++) smoothed.SetTriangles(lists[r], r, false);
            smoothed.RecalculateNormals();
            smoothed.RecalculateTangents();
            smoothed.RecalculateBounds();
            return smoothed;
        }

        /// <summary>
        /// Plays the clips for this frame, then lays the procedural upper body over them. <paramref name="semantic"/>
        /// holds the solved SoldierMotion joints; the weapon pose is shifted with the chest so it stays in the hands.
        /// </summary>
        public void Pose(float dtSeconds, float speed, float localHeadingRadians, float crouch, float airborne, bool dying,
            Transform[] semantic, ref Vector3 weaponPosition, Quaternion weaponRotation, SoldierGrip grip = SoldierGrip.Rifle)
        {
            float dt = float.IsFinite(dtSeconds) && dtSeconds > 0f ? dtSeconds : 0f;
            // Facing follows the solved pelvis (yaw only); a dying body keeps the last living facing.
            if (!dying)
            {
                var forward = Vector3.ProjectOnPlane(semantic[1].rotation * Vector3.forward, Vector3.up);
                if (forward.sqrMagnitude > 1e-6f) facingYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }
            float heading = float.IsFinite(localHeadingRadians) ? localHeadingRadians * Mathf.Rad2Deg : 0f;
            bool reverse = false;
            float wantedLegYaw = 0f;
            // When no clip covers the travel direction for this gait, the legs turn toward it (backpedal plays the
            // forward cycle in reverse) while the procedural chest keeps facing the aim.
            int direction = Mathf.Abs(heading) <= 45f ? 0 : Mathf.Abs(heading) >= 135f ? 1 : heading < 0f ? 2 : 3;
            bool covered = direction == 0 || (crouch > .5f
                ? hasRole[(int)SoldierClipRole.CrouchForward + direction]
                : hasRole[(int)SoldierClipRole.WalkForward + direction] || hasRole[(int)SoldierClipRole.RunForward + direction]);
            if (!covered && speed > .3f && !dying)
            {
                reverse = Mathf.Abs(heading) > 110f;
                wantedLegYaw = Mathf.Clamp(reverse ? Mathf.DeltaAngle(180f, heading) : heading, -75f, 75f);
                heading = 0f;
            }
            legYaw = Mathf.MoveTowardsAngle(legYaw, wantedLegYaw, 300f * dt);
            Instance.transform.rotation = Quaternion.Euler(0f, facingYaw + legYaw, 0f) * Shape.Facing;
            blend.Step(dt, speed, heading * Mathf.Deg2Rad, crouch, airborne, RoleLength);
            idleTime += dt;
            var weights = blend.Weights;
            for (int r = 0; r < (int)SoldierClipRole.Count; r++)
            {
                if (!hasRole[r]) continue;
                locomotion.SetInputWeight(r, weights[r]);
                float length = Mathf.Max(1e-3f, catalog.Roles[r]!.length);
                bool cyclic = r != (int)SoldierClipRole.Idle && r != (int)SoldierClipRole.CrouchIdle && r != (int)SoldierClipRole.Airborne;
                float phase = reverse ? 1f - blend.Phase : blend.Phase;
                roleClips[r].SetTime(cyclic ? phase * length : idleTime % length);
            }
            if (dying)
            {
                deathTime = deathTime < 0f ? 0f : deathTime + dt;
                death.SetTime(Mathf.Min(deathTime, deathClip!.length));
                float w = Mathf.Clamp01(deathTime / .12f);
                top.SetInputWeight(0, 1f - w);
                top.SetInputWeight(1, w);
                upperWeight = Mathf.MoveTowards(upperWeight, 0f, dt / .15f);
            }
            else
            {
                deathTime = -1f;
                top.SetInputWeight(0, 1f);
                top.SetInputWeight(1, 0f);
                upperWeight = Mathf.MoveTowards(upperWeight, 1f, dt / .2f);
            }
            graph.Evaluate(0f);

            if (upperWeight > 1e-3f) LayUpperBody(semantic, ref weaponPosition, weaponRotation, grip);
            PlaceGear();
        }

        private float RoleLength(SoldierClipRole role) => hasRole[(int)role] ? catalog.Roles[(int)role]!.length : 0f;

        private void LayUpperBody(Transform[] semantic, ref Vector3 weaponPosition, Quaternion weaponRotation, SoldierGrip grip)
        {
            float w = upperWeight;
            // 1. Chest, neck, head, clavicles and arms take the solver's rotations (aim, grips, reload, knife, grenade).
            foreach (var (joint, weight) in UpperBody)
            {
                var bone = bones[joint];
                var offset = Shape.BindOffset[joint];
                if (bone == null || offset == null) continue;
                bone.rotation = Quaternion.Slerp(bone.rotation, semantic[joint].rotation * offset.Value, weight * w);
            }

            // 2. The drawn head must sit on the hit volume: the solver's head is within 0.05 m of the head sphere
            // (ADR-112), so the hips carry the authored head there and the legs re-solve onto the clip's foot plants.
            var delta = Vector3.ClampMagnitude(semantic[HeadJoint].position - HeadCentre, .45f) * w;
            if (delta.sqrMagnitude > 1e-8f)
            {
                Vector3 leftFoot = bones[17]!.position, rightFoot = bones[21]!.position;
                Quaternion leftFootRotation = bones[17]!.rotation, rightFootRotation = bones[21]!.rotation;
                bones[1]!.position += delta;
                var forward = Instance.transform.rotation * Quaternion.Inverse(Shape.Facing) * Vector3.forward;
                SolveLimb(bones[15]!, bones[16]!, bones[17]!, leftFoot, bones[16]!.position + forward * .5f, leftFootRotation, 1f);
                SolveLimb(bones[19]!, bones[20]!, bones[21]!, rightFoot, bones[20]!.position + forward * .5f, rightFootRotation, 1f);
            }

            // 3. The weapon and hand targets ride the authored chest; both hands are solved onto the grips.
            Vector3 shift = (bones[ChestJoint]!.position - semantic[ChestJoint].position) * w;
            weaponPosition += shift;
            Vector3 leftTarget = semantic[10].position + shift, rightTarget = semantic[14].position + shift;
            // Hand frames (semantic glove convention: fingers along ±X, back of the hand +Y). On a gun they come from
            // the weapon axes, since the solver's hand rotation only places the wrist.
            Quaternion leftHand = semantic[10].rotation, rightHand = semantic[14].rotation;
            if (grip != SoldierGrip.None)
            {
                Vector3 forward = weaponRotation * Vector3.forward, up = weaponRotation * Vector3.up, right = weaponRotation * Vector3.right;
                rightHand = HandFrame(forward * .55f - up * .65f - right * .2f, right, false);
                if (grip == SoldierGrip.Rifle)
                {
                    leftHand = HandFrame(right * .75f + forward * .35f + up * .1f, -up, true);
                    // Short-armed bodies slide the support hand back along the handguard until it can reach.
                    float reach = (Vector3.Distance(bones[8]!.position, bones[9]!.position) + Vector3.Distance(bones[9]!.position, bones[10]!.position)) * .985f;
                    for (int i = 0; i < 12 && Vector3.Distance(bones[8]!.position, leftTarget) > reach; i++) leftTarget -= forward * .02f;
                }
            }
            SolveLimb(bones[8]!, bones[9]!, bones[10]!, leftTarget, semantic[9].position + shift, leftHand * Shape.BindOffset[10]!.Value, w);
            SolveLimb(bones[12]!, bones[13]!, bones[14]!, rightTarget, semantic[13].position + shift, rightHand * Shape.BindOffset[14]!.Value, w);
            LeftGripError = Vector3.Distance(bones[10]!.position, semantic[10].position + shift);
            RightGripError = Vector3.Distance(bones[14]!.position, rightTarget);
            CurlFingers(leftHand, rightHand, w);
        }

        private static readonly HumanBodyBones[] FingerBones =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal
        };

        /// <summary>
        /// Grip: fingers curl round the semantic hand frame's forward axis (palm down, fingers along ±X at bind);
        /// thumbs keep their bind line against the palm. Replaces whatever open hand the locomotion clips carry.
        /// </summary>
        private void CurlFingers(Quaternion leftHand, Quaternion rightHand, float w)
        {
            foreach (var (finger, bone, bind) in fingers)
            {
                bool left = finger <= HumanBodyBones.LeftLittleDistal;
                var hand = left ? leftHand : rightHand;
                int segment = ((int)finger - (int)HumanBodyBones.LeftThumbProximal) % 3;
                bool thumb = finger == HumanBodyBones.LeftThumbProximal || finger == HumanBodyBones.LeftThumbIntermediate || finger == HumanBodyBones.LeftThumbDistal
                    || finger == HumanBodyBones.RightThumbProximal || finger == HumanBodyBones.RightThumbIntermediate || finger == HumanBodyBones.RightThumbDistal;
                float curl = thumb ? new[] { 10f, 25f, 40f }[segment] : new[] { 55f, 115f, 150f }[segment];
                // Right fingers (+X) curl toward -Y about +Z with a negative angle; left fingers (-X) with a positive one.
                var target = hand * Quaternion.AngleAxis(left ? curl : -curl, thumb ? Vector3.right * (left ? -1f : 1f) : Vector3.forward) * bind;
                bone.rotation = Quaternion.Slerp(bone.rotation, target, w);
            }
        }

        /// <summary>Hand rotation in the semantic glove convention whose fingers point along <paramref name="fingers"/> and whose back faces <paramref name="back"/>.</summary>
        private static Quaternion HandFrame(Vector3 fingers, Vector3 back, bool left)
        {
            var x = (left ? -fingers : fingers).normalized;
            var y = Vector3.ProjectOnPlane(back, x).normalized;
            if (y.sqrMagnitude < 1e-6f) return Quaternion.identity;
            return Quaternion.LookRotation(Vector3.Cross(x, y), y);
        }

        /// <summary>Analytic two-bone solve: the root and middle joints turn so the end reaches the target, bending toward the pole.</summary>
        private static void SolveLimb(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, Quaternion endRotation, float w)
        {
            if (w <= 1e-4f) return;
            Vector3 origin = upper.position;
            float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, end.position);
            Vector3 toTarget = target - origin;
            if (toTarget.sqrMagnitude < 1e-8f || a < 1e-4f || b < 1e-4f) return;
            float c = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-4f, a + b - 1e-4f);
            Vector3 dir = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole - origin, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(lower.position - origin, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.down;
            bend.Normalize();
            float cosA = Mathf.Clamp((a * a + c * c - b * b) / (2f * a * c), -1f, 1f);
            Vector3 middle = origin + dir * (a * cosA) + bend * (a * Mathf.Sqrt(1f - cosA * cosA));
            var upperTurn = Quaternion.FromToRotation(lower.position - origin, middle - origin);
            upper.rotation = Quaternion.Slerp(Quaternion.identity, upperTurn, w) * upper.rotation;
            var lowerTurn = Quaternion.FromToRotation(end.position - lower.position, origin + dir * c - lower.position);
            lower.rotation = Quaternion.Slerp(Quaternion.identity, lowerTurn, w) * lower.rotation;
            end.rotation = Quaternion.Slerp(end.rotation, endRotation, w);
        }

        private void PlaceGear()
        {
            foreach (var (joint, filter, _, _, _) in gear)
            {
                var bone = bones[joint];
                var offset = Shape.BindOffset[joint];
                if (bone == null || offset == null) continue;
                // Measured fit, then the catalog's hand correction on top (offset added, scale multiplied).
                var auto = Shape.AutoFit.TryGetValue(joint, out var a) ? a : null;
                var hand = Shape.Variant.Fit.TryGetValue(joint, out var f) ? f : null;
                var fitOffset = (auto?.Offset ?? Vector3.zero) + (hand?.Offset ?? Vector3.zero);
                var fitScale = Vector3.Scale(auto?.Scale ?? Vector3.one, hand?.Scale ?? Vector3.one);
                var rotation = bone.rotation * Quaternion.Inverse(offset.Value);
                filter.transform.SetPositionAndRotation(bone.position + rotation * fitOffset, rotation);
                filter.transform.localScale = fitScale;
            }
        }

        /// <summary>ADR-106 distance tiers: 0 near, 1 mid, 2 far. Skin influences and gear detail follow.</summary>
        public void SetLod(int value)
        {
            if (value == lod) return;
            lod = value;
            var quality = value == 0 ? SkinQuality.Bone4 : value == 1 ? SkinQuality.Bone2 : SkinQuality.Bone1;
            foreach (var smr in skinned) smr.quality = quality;
            foreach (var (_, filter, _, near, far) in gear) filter.sharedMesh = value == 0 ? near : far;
        }

        public void SetShadowMode(UnityEngine.Rendering.ShadowCastingMode mode)
        {
            foreach (var r in renderers) r.shadowCastingMode = mode;
        }

        public void SetMaterials(Func<Material, Material> restyle, Material[] teamGear, Material[] paint)
        {
            Array.Copy(paint, teamPaint, Math.Min(paint.Length, teamPaint.Length));
            foreach (var pair in paintedSkin) pair.Key.sharedMaterials = PaintMaterials(restyle(pair.Value));
            foreach (var (_, _, renderer, _, _) in gear) renderer.sharedMaterials = teamGear;
            foreach (var pair in authoredMaterials) pair.Key.sharedMaterials = pair.Value.Select(restyle).ToArray();
        }

        /// <summary>Wrist distance from the solver's grip targets after the arm solve (review metric, metres).</summary>
        public float LeftGripError { get; private set; }
        public float RightGripError { get; private set; }
        public Vector3 HeadPosition => bones[HeadJoint]!.position;
        /// <summary>Centre of the drawn head (the head bone itself sits at the skull base).</summary>
        public Vector3 HeadCentre => bones[HeadJoint]!.TransformPoint(Shape.HeadCentreLocal);
        public Vector3 ChestPosition => bones[ChestJoint]!.position;

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            foreach (var (_, filter, _, _, _) in gear) if (filter != null) Kill(filter.gameObject);
            gear.Clear();
            if (Instance != null) Kill(Instance);
        }

        private static void Kill(UnityEngine.Object o) { if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o); }

        private static Transform? BoneFor(Animator animator, int joint)
        {
            var bone = Map[joint];
            if (bone == HumanBodyBones.LastBone) return null;
            var t = animator.GetBoneTransform(bone);
            if (t == null && bone == HumanBodyBones.UpperChest) t = animator.GetBoneTransform(HumanBodyBones.Chest);
            return t;
        }

        // The avatar's configured skeleton is the T-pose the humanoid was built from (ADR-124 retarget reference).
        /// <summary>Rebinds each borrowed part's skinned mesh onto this instance's skeleton by bone name.</summary>
        private static void AttachParts(GameObject instance, SoldierSkinCatalog.Variant variant)
        {
            if (variant.Parts.Length == 0) return;
            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var t in instance.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            foreach (var (model, rendererName) in variant.Parts)
            {
                var donor = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == rendererName);
                var go = new GameObject(rendererName);
                go.transform.SetParent(instance.transform, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = donor.sharedMesh;
                smr.sharedMaterials = donor.sharedMaterials;
                smr.bones = donor.bones.Select(b => b != null && byName.TryGetValue(b.name, out var mine) ? mine
                    : throw new InvalidOperationException("Part bone missing on " + variant.Id + ": " + (b != null ? b.name : "null"))).ToArray();
                smr.rootBone = donor.rootBone != null && byName.TryGetValue(donor.rootBone.name, out var root) ? root : null;
            }
        }

        /// <summary>
        /// Straightens the limbs into the semantic rig's T-pose (arms along ±X, legs down, feet forward), whatever
        /// bind pose the model was authored in (A-pose packs). Run after the facing fix, root at the origin.
        /// </summary>
        private static void ForceTPose(Animator animator)
        {
            Transform? B(HumanBodyBones b) => animator.GetBoneTransform(b);
            void Aim(Transform? bone, Transform? child, Vector3 direction)
            {
                if (bone == null || child == null) return;
                var current = child.position - bone.position;
                if (current.sqrMagnitude < 1e-10f) return;
                bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
            }
            foreach (var (side, upper, lower, hand, finger, thumb) in new[]
            {
                (-1f, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftThumbProximal),
                (1f, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightThumbProximal)
            })
            {
                var x = new Vector3(side, 0, 0);
                Aim(B(upper), B(lower), x);
                Aim(B(lower), B(hand), x);
                var tip = B(finger) ?? B(hand == HumanBodyBones.LeftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
                if (tip == null && B(hand) != null && B(hand)!.childCount > 0) tip = B(hand)!.GetChild(0);
                Aim(B(hand), tip, x);
                // Palm down, thumb forward, like the semantic glove.
                var h = B(hand); var t = B(thumb);
                if (h != null && t != null)
                {
                    var roll = Vector3.ProjectOnPlane(t.position - h.position, x);
                    if (roll.sqrMagnitude > 1e-10f) h.rotation = Quaternion.FromToRotation(roll, Vector3.forward) * h.rotation;
                }
            }
            foreach (var (upper, lower, foot, toes) in new[]
            {
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes)
            })
            {
                Aim(B(upper), B(lower), Vector3.down);
                Aim(B(lower), B(foot), Vector3.down);
                var toe = B(toes);
                if (toe != null && B(foot) != null)
                {
                    var flat = Vector3.ProjectOnPlane(toe.position - B(foot)!.position, Vector3.up);
                    if (flat.sqrMagnitude > 1e-10f) B(foot)!.rotation = Quaternion.FromToRotation(flat, Vector3.forward) * B(foot)!.rotation;
                }
            }
        }

        private static void ApplySkeletonTPose(Transform root, HumanDescription description)
        {
            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            foreach (var bone in description.skeleton)
            {
                if (!byName.TryGetValue(bone.name, out var t) || t == root) continue;
                t.localPosition = bone.position;
                t.localRotation = bone.rotation;
            }
        }

        public static bool Matches(string name, string pattern) => pattern.EndsWith("*", StringComparison.Ordinal)
            ? name.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.Ordinal)
            : string.Equals(name, pattern, StringComparison.Ordinal);
    }
}
