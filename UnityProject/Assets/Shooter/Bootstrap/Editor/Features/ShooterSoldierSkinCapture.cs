using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Presentation.Rig;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// ADR-124 editor review diagnostic: every licensed authored soldier variant in both team kits, standing aim, mid
/// jog, strafing left and right at the fixed aim (ADR-129) and crouched, with the held rifle, plus a close head shot. Flat stand-in team colours on URP Lit; not the
/// packaged-player look, and never visual acceptance.
/// </summary>
public static class ShooterSoldierSkinCapture
{
    [Serializable] public sealed class Proof
    {
        public string result = "Verified", scope = "ADR-124 authored soldier editor diagnostic; not P05/REL03 acceptance";
        public string[] captures; public string[] variants; public float[] headHeights, chestHeights, scales, leftGripErrors, rightGripErrors;
        public string[] diagnostics;
    }

    private sealed class Flat : ISoldierGround { public bool TrySample(float x, float z, out float height) { height = 0; return true; } }

    public static void Capture() => Guard(() =>
    {
        ShooterBuild.CheckToolchain();
        string output = Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot"));
        if (!output.StartsWith(Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must stay under repository Artifacts.");
        Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        QualitySettings.SetQualityLevel(0, true);
        var owned = new List<UnityEngine.Object>();
        var proof = new Proof();
        var captures = new List<string>(); var variants = new List<string>(); var heads = new List<float>(); var chests = new List<float>(); var scales = new List<float>(); var leftGrip = new List<float>(); var rightGrip = new List<float>(); var diagnostics = new List<string>();
        try
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("Required URP Lit unavailable.");
            Material Lit(Color color, float smoothness, Texture texture = null)
            {
                var m = new Material(shader); m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness);
                if (texture != null) m.SetTexture("_BaseMap", texture);
                owned.Add(m); return m;
            }
            // Stand-ins for the packaged team kits (uniform, webbing, lens, hardware).
            var kits = new Dictionary<byte, (Material[] gear, Material[] paint)>
            {
                [1] = (new[] { Lit(new Color(.38f, .44f, .38f), .28f), Lit(new Color(.10f, .14f, .15f), .4f), Lit(new Color(.08f, .18f, .20f), .76f), Lit(new Color(.10f, .14f, .15f), .4f) },
                       new[] { Lit(new Color(.38f, .44f, .38f), .28f), Lit(new Color(.10f, .12f, .12f), .35f), Lit(new Color(.12f, .11f, .10f), .3f) }),
                [2] = (new[] { Lit(new Color(.80f, .68f, .52f), .26f), Lit(new Color(.16f, .10f, .07f), .34f), Lit(new Color(.20f, .09f, .06f), .70f), Lit(new Color(.16f, .10f, .07f), .34f) },
                       new[] { Lit(new Color(.80f, .68f, .52f), .26f), Lit(new Color(.14f, .10f, .08f), .34f), Lit(new Color(.13f, .10f, .08f), .3f) })
            };
            var restyled = new Dictionary<(Material, Texture), Material>();
            // The capture restyles per team; these hold the current team's uniform and webbing stand-ins.
            var uniform = new Dictionary<SoldierSkinCatalog.Variant, Material>(); var webbing = new Dictionary<SoldierSkinCatalog.Variant, Material>();
            Func<Material, Material> Restyle(SoldierSkinCatalog.Variant variant) => source =>
            {
                if (source == null) return kits[1].paint[0];
                string name = source.name.Replace(" (Instance)", string.Empty);
                if (variant.UniformMaterials.Any(p => AuthoredSoldierBody.Matches(name, p))) return uniform[variant];
                if (variant.GearMaterials.Any(p => AuthoredSoldierBody.Matches(name, p))) return webbing[variant];
                Texture skin = variant.SkinMaterials.Any(p => AuthoredSoldierBody.Matches(name, p)) ? variant.SkinTexture : null;
                if (restyled.TryGetValue((source, skin), out var cached)) return cached;
                var texture = skin != null ? skin : source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
                var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
                return restyled[(source, skin)] = Lit(color, .25f, texture);
            };
            Mesh Upload(MeshData data)
            {
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 }; owned.Add(mesh);
                mesh.vertices = data.Positions.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
                mesh.normals = data.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
                mesh.subMeshCount = data.Submeshes.Max(s => (int)s.MaterialSlot) + 1;
                foreach (var sub in data.Submeshes)
                    mesh.SetIndices(data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v => (int)v).ToArray(), MeshTopology.Triangles, sub.MaterialSlot);
                mesh.RecalculateBounds();
                return mesh;
            }

            var rifle = WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath, "Shooter/Generation/FeatureRecipes/rifle.json")));
            var rifleLod = WeaponGeometry.Build(rifle).Lods[0];
            var rifleMesh = Upload(rifleLod);
            // ADR-129: the support palm wraps the rifle's own handguard socket.
            var (supportAxis, supportRadius) = AuthoredHandGrip.MeasureHandguard(rifleLod, WeaponGeometry.Sockets(rifle).First(s => s.Id == "left-hand-grip").Position);
            var support = new SupportSocket(V(supportAxis), supportRadius);
            var gunMetal = Lit(new Color(.09f, .09f, .10f), .45f);

            var cameraObject = new GameObject("Soldier skin camera"); owned.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.30f, .34f, .38f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 30; camera.fieldOfView = 30;
            var sun = new GameObject("Soldier skin sun"); owned.Add(sun);
            var key = sun.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.7f; key.shadows = LightShadows.Soft; key.color = new Color(1f, .96f, .9f);
            key.transform.rotation = Quaternion.Euler(38, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.52f, .56f, .62f);
            RenderSettings.ambientEquatorColor = new Color(.36f, .36f, .36f); RenderSettings.ambientGroundColor = new Color(.20f, .18f, .16f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); owned.Add(floor);
            floor.transform.localScale = new Vector3(3, 1, 3); floor.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(.42f, .40f, .37f), .1f);

            var catalog = SoldierSkinCatalog.Load();
            foreach (var variant in catalog.Variants)
            {
                var shape = AuthoredSoldierBody.Prepare(catalog, variant, Upload, owned.Add);
                if (shape.LeftHand != null)
                    diagnostics.Add($"{variant.Id} ADR-129 support hand: palmSkin {shape.LeftHand.PalmSkin:F3} knuckles {shape.LeftHand.Knuckles:F3} base0 {shape.LeftHand.Base[0]:F3} contact {shape.LeftHand.Contact(supportRadius):F3} radius {supportRadius:F3} curls {string.Join(",", AuthoredHandGrip.WrapCurls(shape.LeftHand, supportRadius).Select(c => c.ToString("F0")))} wrapError {AuthoredHandGrip.WrapError(shape.LeftHand, supportRadius, AuthoredHandGrip.WrapCurls(shape.LeftHand, supportRadius)):F3}");
                foreach (byte team in new byte[] { 1, 2 })
                {
                    uniform[variant] = kits[team].paint[0]; webbing[variant] = kits[team].gear[1];
                    foreach (var (pose, speed, crouch, travelDegrees) in new[] { ("aim", 0f, 0f, 0f), ("jog", 3.5f, 0f, 0f), ("strafe-left", 3.5f, 0f, -90f), ("strafe-right", 3.5f, 0f, 90f), ("crouch", 0f, 1f, 0f) })
                    {
                        var root = new GameObject("Soldier " + variant.Id).transform; owned.Add(root.gameObject);
                        var semantic = Semantic(root);
                        var body = new AuthoredSoldierBody(catalog, shape, root, 3, Restyle(variant), kits[team].gear, kits[team].paint);
                        var weapon = new GameObject("Rifle"); owned.Add(weapon);
                        weapon.AddComponent<MeshFilter>().sharedMesh = rifleMesh;
                        weapon.AddComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(gunMetal, rifleMesh.subMeshCount).ToArray();
                        try
                        {
                            var motion = new SoldierMotion(rifle, new ShootingPosture());
                            var ground = new Flat();
                            const float yaw = 0f;
                            double distance = 0;
                            SoldierPose solved = null;
                            Vector3 weaponPosition = default;
                            int frames = speed > 0f ? 47 : 90;
                            for (int i = 0; i < frames; i++)
                            {
                                distance += speed / 60.0;
                                solved = motion.Evaluate(new SoldierMotionInput(new Float3(0, 0, 0), yaw, distance, speed, crouch, 0f, -.05f, -1f, i == 0), ground);
                                for (int j = 0; j < semantic.Length; j++)
                                    semantic[j].SetPositionAndRotation(V(solved.WorldPositions[j]), Q(solved.WorldRotations[j]));
                                weaponPosition = V(solved.WeaponPosition);
                                body.Pose(1f / 60, speed, speed > 0f ? yaw + travelDegrees * Mathf.Deg2Rad : float.NaN, crouch, 0f, false, semantic, ref weaponPosition, Q(solved.WeaponRotation), SoldierGrip.Rifle, support);
                            }
                            weapon.transform.SetPositionAndRotation(weaponPosition, Q(solved.WeaponRotation));
                            if (team == 1 && pose == "aim") {
                                variants.Add(variant.Id); heads.Add(body.HeadCentre.y); chests.Add(body.ChestPosition.y); scales.Add(shape.Scale);
                                leftGrip.Add(body.LeftGripError); rightGrip.Add(body.RightGripError);
                                var anim = body.Instance.GetComponentInChildren<Animator>();
                                foreach (var b in new[] { HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightThumbProximal })
                                {
                                    var t = anim.GetBoneTransform(b);
                                    diagnostics.Add(variant.Id + " " + b + " " + (t == null ? "unmapped" : t.name + " " + t.position.ToString("F3") + " fwd " + (t.rotation * Vector3.forward).ToString("F2")));
                                }
                                diagnostics.Add(variant.Id + " semantic hand axes X " + (semantic[14].rotation * Vector3.right).ToString("F2") + " Y " + (semantic[14].rotation * Vector3.up).ToString("F2") + " Z " + (semantic[14].rotation * Vector3.forward).ToString("F2") + " weaponFwd " + (Q(solved.WeaponRotation) * Vector3.forward).ToString("F2") + " bindOffsetX " + (shape.BindOffset[14].Value * Vector3.right).ToString("F2"));
                                diagnostics.Add(variant.Id + " semantic rightHand " + semantic[14].position.ToString("F3") + " rightForearm " + semantic[13].position.ToString("F3") + " weapon " + weaponPosition.ToString("F3"));
                            }
                            var shots = pose == "aim" ? new[] { "front", "three-quarter", "back", "head", "support-hand", "support-hand-under" } : new[] { "three-quarter" };
                            foreach (string angle in shots)
                            {
                                bool hand = angle.StartsWith("support-hand", StringComparison.Ordinal);
                                if (hand && !body.SupportGripSocket.HasValue) throw new InvalidOperationException(variant.Id + ": support hand not on the rifle.");
                                Vector3 target = angle == "head" ? body.HeadCentre : hand ? body.SupportGripSocket!.Value : new Vector3(0, crouch > 0 ? .65f : .92f, 0);
                                Vector3 direction = angle == "front" ? Vector3.forward : angle == "back" ? Vector3.back
                                    : angle == "support-hand" ? new Vector3(-.9f, .35f, .25f).normalized : angle == "support-hand-under" ? new Vector3(-.5f, -.6f, .6f).normalized
                                    : new Vector3(.75f, .22f, .85f).normalized;
                                camera.transform.position = target + direction * (angle == "head" ? .9f : hand ? .45f : 4.6f);
                                camera.transform.LookAt(target);
                                string path = Path.Combine(output, $"{variant.Id}-team{team}-{pose}-{angle}.png");
                                ShooterFeatureBuild.Render(camera, path);
                                captures.Add(path);
                            }
                        }
                        finally { body.Dispose(); UnityEngine.Object.DestroyImmediate(root.gameObject); UnityEngine.Object.DestroyImmediate(weapon); }
                    }
                }
            }
            proof.captures = captures.ToArray(); proof.variants = variants.ToArray(); proof.headHeights = heads.ToArray(); proof.chestHeights = chests.ToArray(); proof.scales = scales.ToArray(); proof.leftGripErrors = leftGrip.ToArray(); proof.rightGripErrors = rightGrip.ToArray(); proof.diagnostics = diagnostics.ToArray();
            ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), proof);
            Debug.Log("SHOOTER_SOLDIER_SKIN_CAPTURE files=" + captures.Count);
        }
        finally { foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value); }
    });

    private static Transform[] Semantic(Transform root)
    {
        var bones = new Transform[SoldierSkeleton.JointCount];
        var skeleton = SoldierSkeleton.Definition;
        for (int j = 0; j < bones.Length; j++)
        {
            var definition = skeleton.Joints[j];
            bones[j] = new GameObject(SoldierSkeleton.Names[j]).transform;
            bones[j].SetParent(definition.ParentId == ushort.MaxValue ? root : bones[definition.ParentId], false);
            bones[j].localPosition = V(definition.LocalBindPosition);
        }
        return bones;
    }

    private static Vector3 V(Float3 v) => new Vector3(v.X, v.Y, v.Z);
    private static Quaternion Q(Quat q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    private static void Guard(Action body) { try { body(); } catch (Exception error) { Debug.LogError("SHOOTER_FAILURE: " + error); EditorApplication.Exit(1); } }
}
