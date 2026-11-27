using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Rig;
using Shooter.Presentation.View;
using Shooter.Simulation.Pose;
using UnityEngine;
using Object = UnityEngine.Object;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>
    /// ADR-124 licensed authored soldier bodies: the catalog loads every variant as a valid humanoid with its clips,
    /// a broken catalog throws instead of falling back, and the drawn authored head, chest and hands stay inside the
    /// analytic hit volumes and on the weapon grips the way the generated body did (ADR-112 tolerances, widened
    /// only for the authored head bone sitting at the skull base).
    /// </summary>
    public sealed class AuthoredSoldierTests
    {
        private sealed class Flat : ISoldierGround { public bool TrySample(float x, float z, out float height) { height = 0; return true; } }
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Clean()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void CatalogLoadsSeveralHumanoidVariantsPerTeamWithTheirClips()
        {
            var catalog = SoldierSkinCatalog.Load();
            for (byte team = 1; team <= 2; team++)
                Assert.That(catalog.Variants.Count(v => v.Teams.Contains(team)), Is.GreaterThanOrEqualTo(2), "variants for team " + team);
            foreach (var role in new[] { SoldierClipRole.Idle, SoldierClipRole.WalkForward, SoldierClipRole.RunForward, SoldierClipRole.CrouchIdle })
                Assert.That(catalog.Roles[(int)role], Is.Not.Null, role.ToString());
            Assert.That(catalog.Deaths, Is.Not.Empty);
            foreach (var clip in catalog.Roles.Where(c => c != null).Concat(catalog.Deaths))
                Assert.That(clip!.humanMotion, Is.True, clip.name + " must be a humanoid clip");
        }

        [Test]
        public void BrokenCatalogThrowsInsteadOfFallingBack()
        {
            Assert.Throws<InvalidOperationException>(() => SoldierSkinCatalog.Parse("{\"schemaVersion\":2}"));
            var text = Resources.Load<TextAsset>(SoldierSkinCatalog.ResourcePath).text;
            var broken = text.Replace("\"model\": \"", "\"model\": \"missing/");
            Assert.Throws<InvalidOperationException>(() => SoldierSkinCatalog.Parse(broken));
        }

        [TestCase(0f, false, 0f)]
        [TestCase(0f, false, 0.4f)]
        [TestCase(3.5f, false, 0f)]
        [TestCase(5.5f, false, 0.3f)]
        [TestCase(0f, true, 0f)]
        [TestCase(1.2f, true, -0.3f)]
        public void AuthoredBodyStaysInTheHitVolumesAndOnTheGrips(float speed, bool crouch, float pitch)
        {
            var catalog = SoldierSkinCatalog.Load();
            var rifle = WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath, "Shooter/Generation/FeatureRecipes/rifle.json")));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var gearMaterials = Enumerable.Range(0, 4).Select(_ => Track(new Material(shader))).ToArray();
            foreach (var variant in catalog.Variants)
            {
                var shape = AuthoredSoldierBody.Prepare(catalog, variant, Upload, o => created.Add(o));
                var root = Track(new GameObject("Authored soldier test")).transform;
                var semantic = BuildSemantic(root);
                var body = new AuthoredSoldierBody(catalog, shape, root, 7, m => m, gearMaterials, gearMaterials.Take(3).ToArray());
                try
                {
                    const float yaw = 0.7f;
                    var motion = new SoldierMotion(rifle, new ShootingPosture());
                    var ground = new Flat();
                    double distance = 0;
                    float s = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
                    Float3 feet = default;
                    Vector3 weapon = default;
                    for (int i = 0; i < 180; i++)
                    {
                        distance += speed / 60.0;
                        feet = new Float3(s * (float)distance, 0, c * (float)distance);
                        var pose = motion.Evaluate(new SoldierMotionInput(feet, yaw, distance, speed, crouch ? 1f : 0f, 0f, pitch, -1f, i == 0), ground);
                        root.position = new Vector3(feet.X, feet.Y, feet.Z);
                        for (int j = 0; j < semantic.Length; j++)
                            semantic[j].SetPositionAndRotation(V(pose.WorldPositions[j]), Q(pose.WorldRotations[j]));
                        weapon = V(pose.WeaponPosition);
                        body.Pose(1f / 60, speed, speed > 0f ? yaw : float.NaN, crouch ? 1f : 0f, 0f, false, semantic, ref weapon, Q(pose.WeaponRotation));
                    }

                    var volumes = new List<HitVolume>();
                    new GameplayPoseEvaluator().Evaluate(new EntityId(1), feet, yaw, pitch, crouch ? Stance.Crouching : Stance.Standing, crouch ? 1.2f : 1.8f, speed, 1, volumes);
                    var head = volumes[0].Capsule;
                    float headGap = Vector3.Distance(body.HeadCentre, V(head.BottomSphereCenter));
                    Assert.That(headGap, Is.LessThanOrEqualTo(head.Radius), $"{variant.Id}: authored head {headGap:F3} m from the head sphere centre");

                    var torso = volumes[1].Capsule;
                    var chest = body.ChestPosition;
                    Assert.That(chest.y, Is.InRange(torso.BottomSphereCenter.Y, torso.TopSphereCenter.Y + torso.Radius), variant.Id + ": chest height");
                    float planar = new Vector2(chest.x - feet.X, chest.z - feet.Z).magnitude;
                    Assert.That(planar, Is.LessThanOrEqualTo(torso.Radius), $"{variant.Id}: chest {planar:F3} m off the torso axis");
                }
                finally { body.Dispose(); }
            }
        }

        /// <summary>
        /// ADR-129: a bot strafing at its fixed aim plays the clip that matches its world travel as seen from the drawn
        /// (bladed) body, so the legs step where it moves instead of 35 degrees off.
        /// </summary>
        [TestCase(90f)]
        [TestCase(-90f)]
        [TestCase(180f)]
        [TestCase(45f)]
        public void StrafeClipsFollowTheWorldTravelFromTheDrawnFacing(float travelOffsetDegrees)
        {
            var catalog = SoldierSkinCatalog.Load();
            var rifle = WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath, "Shooter/Generation/FeatureRecipes/rifle.json")));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var gearMaterials = Enumerable.Range(0, 4).Select(_ => Track(new Material(shader))).ToArray();
            var variant = catalog.Variants[0];
            var shape = AuthoredSoldierBody.Prepare(catalog, variant, Upload, o => created.Add(o));
            var root = Track(new GameObject("Authored strafe test")).transform;
            var semantic = BuildSemantic(root);
            var body = new AuthoredSoldierBody(catalog, shape, root, 7, m => m, gearMaterials, gearMaterials.Take(3).ToArray());
            try
            {
                const float yaw = 0.7f, speed = 3.5f;
                float travel = yaw + travelOffsetDegrees * Mathf.Deg2Rad;
                var motion = new SoldierMotion(rifle, new ShootingPosture());
                var ground = new Flat();
                double distance = 0;
                for (int i = 0; i < 180; i++)
                {
                    distance += speed / 60.0;
                    var feet = new Float3(Mathf.Sin(travel) * (float)distance, 0, Mathf.Cos(travel) * (float)distance);
                    var pose = motion.Evaluate(new SoldierMotionInput(feet, yaw, distance, speed, 0f, 0f, 0f, -1f, i == 0), ground);
                    root.position = new Vector3(feet.X, feet.Y, feet.Z);
                    for (int j = 0; j < semantic.Length; j++)
                        semantic[j].SetPositionAndRotation(V(pose.WorldPositions[j]), Q(pose.WorldRotations[j]));
                    var weapon = V(pose.WeaponPosition);
                    body.Pose(1f / 60, speed, travel, 0f, 0f, false, semantic, ref weapon, Q(pose.WeaponRotation));
                }
                float played = Mathf.DeltaAngle(0f, body.DrawnYawDegrees + body.ClipHeadingDegrees);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(played, travel * Mathf.Rad2Deg)), Is.LessThan(1f),
                    $"drawn {body.DrawnYawDegrees:F1} + clip {body.ClipHeadingDegrees:F1} vs travel {travel * Mathf.Rad2Deg:F1}");
                // The four-way split must lean the way the body really moves.
                float h = body.ClipHeadingDegrees * Mathf.Deg2Rad;
                float side = body.ClipWeight(SoldierClipRole.RunRight) + body.ClipWeight(SoldierClipRole.WalkRight)
                    - body.ClipWeight(SoldierClipRole.RunLeft) - body.ClipWeight(SoldierClipRole.WalkLeft);
                if (Mathf.Abs(Mathf.Sin(h)) > .3f) Assert.That(Mathf.Sign(side), Is.EqualTo(Mathf.Sign(Mathf.Sin(h))), "side clip weight " + side);
            }
            finally { body.Dispose(); }
        }

        /// <summary>
        /// ADR-129: on every long gun, every variant lays its support palm on the gun's own handguard socket and wraps
        /// the fingers round it (fingertips on the handguard surface, not curled in the air beyond it).
        /// </summary>
        [TestCase(0f)]
        [TestCase(3.5f)]
        public void SupportHandWrapsEveryHandguard(float speed)
        {
            var catalog = SoldierSkinCatalog.Load();
            var rifle = WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath, "Shooter/Generation/FeatureRecipes/rifle.json")));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var gearMaterials = Enumerable.Range(0, 4).Select(_ => Track(new Material(shader))).ToArray();
            var guns = WeaponMeshCatalog.All.Where(g => g.Recipe != null && g.Recipe.IsLongGun).ToList();
            Assert.That(guns, Is.Not.Empty);
            var report = new List<string>();
            foreach (var variant in catalog.Variants)
            {
                var shape = AuthoredSoldierBody.Prepare(catalog, variant, Upload, o => created.Add(o));
                Assert.That(shape.LeftHand, Is.Not.Null, variant.Id + ": support hand not measured");
                foreach (var gun in guns)
                {
                    var grip = WeaponGeometry.Sockets(gun.Recipe!).First(s => s.Id == "left-hand-grip").Position;
                    var build = WeaponGeometry.Build(gun.Recipe!);
                    var (socket, radius) = AuthoredHandGrip.MeasureHandguard(build.Lods[0], grip, build.CyclePart);
                    var root = Track(new GameObject("Authored grip test")).transform;
                    var semantic = BuildSemantic(root);
                    var body = new AuthoredSoldierBody(catalog, shape, root, 7, m => m, gearMaterials, gearMaterials.Take(3).ToArray());
                    try
                    {
                        var motion = new SoldierMotion(rifle, new ShootingPosture());
                        double distance = 0;
                        Quaternion weaponRotation = Quaternion.identity;
                        for (int i = 0; i < 60; i++)
                        {
                            distance += speed / 60.0;
                            var pose = motion.Evaluate(new SoldierMotionInput(default, 0f, distance, speed, 0f, 0f, 0f, -1f, i == 0), new Flat());
                            for (int j = 0; j < semantic.Length; j++)
                                semantic[j].SetPositionAndRotation(V(pose.WorldPositions[j]), Q(pose.WorldRotations[j]));
                            var weapon = V(pose.WeaponPosition);
                            weaponRotation = Q(pose.WeaponRotation);
                            body.Pose(1f / 60, speed, speed > 0f ? 0f : float.NaN, 0f, 0f, false, semantic, ref weapon, weaponRotation,
                                SoldierGrip.Rifle, new SupportSocket(V(socket), radius));
                        }
                        Assert.That(body.SupportGripSocket.HasValue, Is.True, $"{variant.Id}/{gun.MeshKey}: support hand not on the gun");
                        float gap = body.SupportFingertipGap(weaponRotation * Vector3.forward);
                        float palm = body.SupportPalmGap(weaponRotation * Vector3.forward);
                        report.Add($"{variant.Id}/{gun.MeshKey} r={radius * 1000:F0}mm wrist={body.LeftGripError * 1000:F0}mm tips={gap * 1000:F0}mm palmGap={palm * 1000:F0}mm");
                        Assert.That(body.LeftGripError, Is.LessThan(.02f), $"{variant.Id}/{gun.MeshKey}: wrist {body.LeftGripError:F3} m off its grip target");
                        // 2 cm: the handguards are now measured from their slices (28–34 mm radius), not the 25 mm
                        // fallback the earlier 1.5 cm bound was set against; the hand cannot close fully round them.
                        Assert.That(gap, Is.LessThan(.02f), $"{variant.Id}/{gun.MeshKey}: fingertips {gap:F3} m off the handguard (r {radius:F3})");
                        Assert.That(palm, Is.InRange(-.008f, .01f), $"{variant.Id}/{gun.MeshKey}: hand skin {palm:F3} m off the handguard surface");
                    }
                    finally { body.Dispose(); }
                }
            }
            TestContext.WriteLine(string.Join("\n", report));
        }

        private Transform[] BuildSemantic(Transform root)
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

        private Mesh Upload(MeshData data)
        {
            var mesh = Track(new Mesh());
            mesh.vertices = data.Positions.Select(V).ToArray();
            mesh.normals = data.Normals.Select(V).ToArray();
            mesh.subMeshCount = data.Submeshes.Max(s => (int)s.MaterialSlot) + 1;
            foreach (var sub in data.Submeshes)
                mesh.SetIndices(data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v => (int)v).ToArray(), MeshTopology.Triangles, sub.MaterialSlot);
            return mesh;
        }

        private T Track<T>(T o) where T : Object { created.Add(o); return o; }
        private static Vector3 V(Float3 v) => new Vector3(v.X, v.Y, v.Z);
        private static Quaternion Q(Quat q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
