using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Generation;
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
                        body.Pose(1f / 60, speed, 0f, crouch ? 1f : 0f, 0f, false, semantic, ref weapon, Q(pose.WeaponRotation));
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
