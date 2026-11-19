#nullable enable
using System;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Rig;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>0.3.20 playtest regressions: pink weapons after a rebind and reload hidden behind the scope.</summary>
    public sealed class PlaytestRegressionTests
    {
        private sealed class Lifecycle : IActorLifecycle, Shooter.Game.Session.IActorStateReader
        {
            public ActorLifecycleState State;
            public int ActiveActorCount => 1;
            public EntityId SpawnLife(ParticipantId participant, LifeId life, Float3 feet, float yaw, byte team) => default;
            public void DespawnLife(EntityId entity) { }
            public void ResetLife(EntityId entity, Float3 feet, float yaw) { }
            public bool TryGetActorState(EntityId entity, out ActorLifecycleState state) { state = State; return entity.Equals(State.EntityId); }
        }

        private static MatchViewSnapshot Snapshot(EntityId id) => new MatchViewSnapshot(1, 10, 10, 53990, GameMatchPhase.Playing, MatchOutcome.None,
            FinishReason.None, 0, 0, new[] { new ParticipantState(new ParticipantId(1), "Human", 1, true, id, new LifeId(1), true, 0, 0, 0) }, 0);

        private static PackagedVisualPayloads Visuals(string salt) => PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low",
            ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes(salt))));

        [Test]
        public void RebindingForANewMatchNeverLeavesWeaponsOnDestroyedMaterials()
        {
            var root = new GameObject("Rebind palette test");
            try
            {
                var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                var presentation = ProductActorPresentation.Create(root.transform, camera);
                var visuals = Visuals("rebind-palette");
                presentation.BindPackagedVisuals(visuals);
                var id = new EntityId(1);
                var lifecycle = new Lifecycle { State = new ActorLifecycleState(id, 1, true, 100, default, 0, 0, 0, 30, 90) };
                var snapshot = Snapshot(id);
                presentation.Sync(snapshot, lifecycle, null, 0, id, false, .016f, Array.Empty<CombatEvent>(), 10);

                // Second match: the bind yields across frames while the session keeps syncing.
                uint tick = 11;
                foreach (var _ in presentation.BindPackagedVisualSteps(visuals))
                    presentation.Sync(snapshot, lifecycle, null, 0, id, false, .016f, Array.Empty<CombatEvent>(), tick++);
                presentation.Sync(snapshot, lifecycle, null, 0, id, false, .016f, Array.Empty<CombatEvent>(), tick);

                var weapon = camera.transform.Find("Local FP rifle");
                Assert.That(weapon, Is.Not.Null);
                foreach (var renderer in weapon.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is SkinnedMeshRenderer) continue;
                    foreach (var material in renderer.sharedMaterials)
                        Assert.That(material != null, Is.True, renderer.name + " kept a destroyed (magenta) material after the rebind.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthorityReloadLowersScopedSightsWithoutTheReloadEvent()
        {
            var root = new GameObject("Scoped reload test");
            try
            {
                var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                var presentation = ProductActorPresentation.Create(root.transform, camera);
                presentation.BindPackagedVisuals(Visuals("scoped-reload"));
                var id = new EntityId(1);
                var lifecycle = new Lifecycle { State = new ActorLifecycleState(id, 1, true, 100, default, 0, 0, 0, 30, 90) };
                var snapshot = Snapshot(id);
                uint tick = 10;
                for (int i = 0; i < 90; i++)
                    presentation.Sync(snapshot, lifecycle, null, 0, id, true, 1f / 60f, Array.Empty<CombatEvent>(), tick++);
                Assert.That(presentation.LocalAdsPresentationBlend, Is.GreaterThan(.9f));

                // Missed ReloadStarted edge: only the authoritative weapon state says the reload is running.
                lifecycle.State = new ActorLifecycleState(id, 1, true, 100, default, 0, 0, 0, 0, 90, WeaponAction.Reloading);
                for (int i = 0; i < 90; i++)
                    presentation.Sync(snapshot, lifecycle, null, 0, id, true, 1f / 60f, Array.Empty<CombatEvent>(), tick++);
                Assert.That(presentation.LocalAdsPresentationBlend, Is.LessThan(.05f));
                Assert.That(camera.transform.Find("Local FP rifle").gameObject.activeSelf, Is.True, "The reload must be visible, not hidden behind the scope.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
