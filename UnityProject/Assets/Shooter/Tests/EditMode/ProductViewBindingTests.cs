#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Rendering;
using Shooter.Presentation.Rig;
using Shooter.Presentation.View;
using Shooter.Presentation.Session;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>T056/T067/T068 EditMode: packaged map/actor visuals, binder policy, authority weapon/action views.</summary>
    public sealed class ProductViewBindingTests
    {
        private sealed class FakeLifecycle : IActorLifecycle
        {
            private readonly Dictionary<ulong, ActorLifecycleState> states = new Dictionary<ulong, ActorLifecycleState>();
            public int ActiveActorCount => states.Count;
            public EntityId SpawnLife(ParticipantId participant, LifeId life, Float3 feet, float yaw, byte team) => default;
            public void DespawnLife(EntityId entity) => states.Remove(entity.Value);
            public void ResetLife(EntityId entity, Float3 feet, float yaw) { }
            public bool TryGetActorState(EntityId entity, out ActorLifecycleState state) =>
                states.TryGetValue(entity.Value, out state);
            public void Set(ActorLifecycleState state) => states[state.EntityId.Value] = state;
        }

        [Test]
        public void ProductMapView_InstallsPackagedSolidsAndClearsWithoutLeavingRenderers()
        {
            var toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("product-view-binding-test"));
            var package = MapPackageBuilder.Build("training-compound", "low", toolchain);
            var visuals = PackagedVisualPayloads.FromBuild(package);
            var boxes = new List<CollisionBox>
            {
                new CollisionBox(1001, 2001, 1, new Float3(0, 1, 0), new Float3(2, 2, 2), true),
                new CollisionBox(1002, 2002, 2, new Float3(4, 0.6f, 0), new Float3(1.2f, 1.2f, 1.2f), true)
            };
            var root = new GameObject("ProductViewBindingTests");
            try
            {
                var view = ProductMapView.Create(root.transform);
                view.Install(boxes, null, "training-compound", visuals);
                Assert.That(view.IsInstalled, Is.True);
                Assert.That(view.VisualsBound, Is.True);
                Assert.That(view.SolidCount, Is.EqualTo(2));
                Assert.That(view.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(0));
                view.ClearVisuals();
                Assert.That(view.IsInstalled, Is.False);
                Assert.That(view.SolidCount, Is.EqualTo(0));
                Assert.That(view.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProductMapView_RejectsEmptyPackageSolids()
        {
            var toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("product-view-binding-empty"));
            var visuals = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", toolchain));
            var root = new GameObject("ProductViewBindingEmpty");
            try
            {
                var view = ProductMapView.Create(root.transform);
                Assert.Throws<System.InvalidOperationException>(() =>
                    view.Install(new List<CollisionBox>(), null, "training-compound", visuals));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProductActorBinder_RepeatedReconcileDoesNotLeakHandles()
        {
            var binder = new ProductActorBinder();
            var spawn = new List<EntityId>();
            var despawn = new List<EntityId>();
            var corpses = new List<EntityId>();
            var living = new[]
            {
                new ActorLifecycleState(new EntityId(1), 1, true, 100, new Float3(0, 0, 0), 0, 0, 0, 30, 90),
                new ActorLifecycleState(new EntityId(2), 2, true, 100, new Float3(1, 0, 0), 0, 0, 0, 30, 90)
            };
            for (int i = 0; i < 20; i++)
            {
                binder.Reconcile(living, System.Array.Empty<ActorLifecycleState>(), spawn, despawn, corpses);
                if (i == 0) Assert.That(spawn.Count, Is.EqualTo(2));
                else Assert.That(spawn.Count, Is.EqualTo(0));
                Assert.That(binder.LiveHandleCount, Is.EqualTo(2));
            }
            binder.Clear();
            Assert.That(binder.LiveHandleCount, Is.EqualTo(0));
        }

        [Test]
        public void ProductActorPresentation_ConsumesActiveWeaponSlotAndReloadEventsNotAmmoDrop()
        {
            var toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("product-actor-authority-view"));
            var visuals = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", toolchain));
            var root = new GameObject("ProductActorAuthorityView");
            var cameraGo = new GameObject("Cam");
            cameraGo.transform.SetParent(root.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            try
            {
                var presentation = ProductActorPresentation.Create(root.transform, camera);
                presentation.BindPackagedVisuals(visuals);
                Assert.That(presentation.VisualsBound, Is.True);
                Assert.That(camera.transform.Find("Local FP rifle").GetComponent<MeshFilter>().sharedMesh, Is.Not.Null,
                    "The first cold bind must assign the first-person weapon, before any slot switch.");

                var lifecycle = new FakeLifecycle();
                var human = new EntityId(42);
                lifecycle.Set(new ActorLifecycleState(human, 1, true, 100, new Float3(0, 0, 0), 0, 0, 0, 30, 90));
                var snap = new MatchViewSnapshot(
                    1, 10, 10, 53990, GameMatchPhase.Playing, MatchOutcome.None, FinishReason.None, 0, 0,
                    new[]
                    {
                        new ParticipantState(new ParticipantId(1), "human", 1, true, human, new LifeId(1), true, 0, 0, 0)
                    },
                    0);

                presentation.Sync(snap, lifecycle, null, 0, human, false, 1f / 60f, null, 10);
                Assert.That(presentation.LiveAvatarCount, Is.EqualTo(1));
                Assert.That(presentation.Actions.IsRifle(human), Is.True);

                lifecycle.Set(new ActorLifecycleState(human, 1, true, 100, new Float3(0, 0, 0), 0, 0, 0, 29, 90));
                presentation.Sync(snap, lifecycle, null, 0, human, false, 1f / 60f, System.Array.Empty<CombatEvent>(), 11);
                Assert.That(presentation.Actions.IsReloading(human), Is.False);

                var reload = new CombatEvent(
                    new EventId(1, 12, human, 1),
                    CombatEventKind.ReloadStarted,
                    human,
                    default,
                    new ActionPayload(0, 12, 156));
                presentation.Sync(snap, lifecycle, null, 0, human, false, 1f / 60f, new[] { reload }, 12);
                Assert.That(presentation.Actions.IsReloading(human), Is.True);

                lifecycle.Set(new ActorLifecycleState(human, 1, true, 100, new Float3(0, 0, 0), 0, 0, 1, 15, 45));
                presentation.Sync(snap, lifecycle, null, 0, human, false, 1f / 60f, System.Array.Empty<CombatEvent>(), 13);
                Assert.That(presentation.Actions.IsPistol(human), Is.True);

                var replacement = new EntityId(43);
                lifecycle.DespawnLife(human);
                lifecycle.Set(new ActorLifecycleState(replacement, 1, true, 100, new Float3(1, 0, 0), 0, 0, 0, 30, 90));
                var snap2 = new MatchViewSnapshot(
                    1, 14, 14, 53986, GameMatchPhase.Playing, MatchOutcome.None, FinishReason.None, 0, 0,
                    new[]
                    {
                        new ParticipantState(new ParticipantId(1), "human", 1, true, replacement, new LifeId(2), true, 0, 1, 0)
                    },
                    0);
                presentation.Sync(
                    snap2,
                    lifecycle,
                    _ => new ActorLifecycleState(human, 1, false, 0, new Float3(0, 0, 0), 0, 0, 0, 0, 0),
                    1,
                    replacement,
                    false,
                    1f / 60f,
                    System.Array.Empty<CombatEvent>(),
                    14);
                Assert.That(presentation.LiveAvatarCount, Is.EqualTo(1));
                Assert.That(presentation.CorpseAvatarCount, Is.EqualTo(1));
                Assert.That(presentation.Actions.IsRifle(replacement), Is.True);
                Assert.That(presentation.Actions.IsReloading(human), Is.False);

                presentation.ClearAll();
                Assert.That(presentation.HandleCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MenuStageRendersPackagedSoldiersAndMapCardsWithoutGameplayObjects()
        {
            var hash = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("menu-stage-review"));
            var visuals = new[]
            {
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", hash)),
                PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("depot-yard", "low", hash))
            };
            var root = new GameObject("MenuStageReview");
            var previous = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                var scene = root.AddComponent<ProductMenuScene>();
                scene.BindVerified(visuals);
                Assert.That(scene.IsReady, Is.True);
                Assert.That(scene.ShowcaseActorCount, Is.EqualTo(3));
                Assert.That(scene.MapCard("training-compound"), Is.Not.Null);
                Assert.That(scene.MapCard("depot-yard"), Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.Zero);
                Assert.That(root.GetComponentsInChildren<Rigidbody>().Length, Is.Zero);
                string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                    "../../Artifacts/t074-visual-review/editor-" + System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
                System.IO.Directory.CreateDirectory(directory);
                RenderTexture.active = (RenderTexture)scene.Frame!;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "staging-area.png"), ImageConversion.EncodeToPNG(texture));
                TestContext.Progress.WriteLine("SHOOTER_MENU_REVIEW_CAPTURE " + directory);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(root);
            }
        }
    }
}
