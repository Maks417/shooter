#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation.Environment;
using Shooter.Presentation.Rendering;
using Shooter.Presentation.View;
using UnityEngine;

namespace Shooter.Tests
{
    /// <summary>T056/FIX05 EditMode: map visuals install/clear and actor binder stale-handle policy.</summary>
    public sealed class ProductViewBindingTests
    {
        [Test]
        public void ProductMapView_InstallsSolidsAndClearsWithoutLeavingRenderers()
        {
            var boxes = new List<CollisionBox>
            {
                new CollisionBox(1001, 2001, 1, new Float3(0, 1, 0), new Float3(2, 2, 2), true),
                new CollisionBox(1002, 2002, 2, new Float3(4, 0.6f, 0), new Float3(1.2f, 1.2f, 1.2f), true)
            };
            var root = new GameObject("ProductViewBindingTests");
            try
            {
                var view = ProductMapView.Create(root.transform);
                view.Install(boxes, null, "training-compound");
                Assert.That(view.IsInstalled, Is.True);
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
            var root = new GameObject("ProductViewBindingEmpty");
            try
            {
                var view = ProductMapView.Create(root.transform);
                Assert.Throws<System.InvalidOperationException>(() =>
                    view.Install(new List<CollisionBox>(), null, "training-compound"));
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
    }
}
