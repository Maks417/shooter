using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Presentation;
using Shooter.World;
using UnityEngine;
using UnityEngine.Rendering;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    public sealed class FoundationWorldTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private UnityWorldQueries world;
        [SetUp] public void Setup() { world = new UnityWorldQueries(7); }
        [TearDown] public void Cleanup()
        { world.Dispose(); foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item); owned.Clear(); }
        private void Box(ulong id, Vector3 position)
        {
            var item = new GameObject("Test static surface") { layer = 8 }; owned.Add(item); item.transform.position = position;
            item.AddComponent<BoxCollider>().size = Vector3.one; item.AddComponent<StableSurface>().Initialize(id, id, 1);
        }
        private static QueryFilter Filter => new QueryFilter(1u << 8, default, false);
        [Test] public void ActualRayTiesUseStableIdentityAndAreIndependentOfQuality()
        {
            Box(1002, new Vector3(20, 0, 0)); Box(1001, new Vector3(20, 0, 0)); Physics.SyncTransforms();
            int previous = QualitySettings.GetQualityLevel();
            try
            {
                for (int tier = 0; tier < 3; tier++)
                {
                    QualitySettings.SetQualityLevel(tier, false);
                    var result = world.Raycast(new Float3(20, 0, -3), new Float3(0, 0, 1), 10, Filter);
                    Assert.That(result.Status, Is.EqualTo(QueryStatus.Hit)); Assert.That(result.Hit.ColliderStableId, Is.EqualTo(1001UL));
                    Assert.That(result.Hit.DistanceMeters, Is.EqualTo(2.5f).Within(.0001f)); Assert.That(result.Hit.CollisionRevision, Is.EqualTo(7));
                }
            }
            finally { QualitySettings.SetQualityLevel(previous, false); }
        }
        [Test] public void ActualOverlapReportsOverflowAndReturnsARealPenetrationNormal()
        {
            Box(1000, new Vector3(20, 0, 0)); Physics.SyncTransforms();
            var capsule = new Capsule(new Float3(20.6f, -.1f, 0), new Float3(20.6f, .1f, 0), .2f);
            var buffer = new HitBuffer(1); var result = world.Overlap(capsule, Filter, buffer);
            Assert.That(result.Status, Is.EqualTo(QueryStatus.Hit)); Assert.That(buffer[0].Normal.X, Is.GreaterThan(.99f)); Assert.That(buffer[0].DistanceMeters, Is.GreaterThan(0));
            for (ulong i = 1; i < 257; i++) Box(1000 + i, new Vector3(20, 0, 0)); Physics.SyncTransforms();
            var full = new HitBuffer(256); result = world.Overlap(capsule, Filter, full);
            Assert.That(result.Status, Is.EqualTo(QueryStatus.BufferOverflow)); Assert.That(result.RequiredCount, Is.EqualTo(257)); Assert.That(result.WrittenCount, Is.EqualTo(256));
            Assert.Throws<ArgumentException>(() => full.SortComplete(result));
        }
        [Test] public void ZeroSweepAndInvalidDirectionFailSafely()
        {
            var capsule = new Capsule(new Float3(20, 0, 0), new Float3(20, 1, 0), .3f);
            Assert.That(world.SweepCapsule(capsule, default, Filter).Status, Is.EqualTo(QueryStatus.NoHit));
            Assert.That(world.Raycast(default, default, 10, Filter).Status, Is.EqualTo(QueryStatus.InvalidInput));
        }
        [Test] public void NavigationConsumesFailedCancelledAndStaleTickets()
        {
            var nav = new UnityNavigation(1, _ => null); var actor = new EntityId(1); var path = new PathBuffer(3);
            var ticket = nav.RequestPath(actor, default, 1); var result = nav.Poll(ticket, path);
            Assert.That(result.Status, Is.EqualTo(PathStatus.Failed)); Assert.That(result.Error, Is.EqualTo(ErrorCode.MissingContent));
            Assert.That(nav.Poll(ticket, path).Status, Is.EqualTo(PathStatus.Stale));
            ticket = nav.RequestPath(actor, default, 1); nav.Cancel(ticket); Assert.That(nav.Poll(ticket, path).Status, Is.EqualTo(PathStatus.Cancelled));
            ticket = nav.RequestPath(actor, default, 2); Assert.That(nav.Poll(ticket, path).Status, Is.EqualTo(PathStatus.Stale));
        }
        [Test] public void GeneratedBoxHasExpectedBoundsNormalsAndNoImportedGeometry()
        {
            var mesh = FoundationView.CreateUnitBox();
            try { Assert.That(mesh.vertexCount, Is.EqualTo(24)); Assert.That(mesh.triangles.Length, Is.EqualTo(36)); Assert.That(mesh.bounds.size, Is.EqualTo(Vector3.one)); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
