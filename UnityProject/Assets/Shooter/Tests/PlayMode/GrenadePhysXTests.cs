#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Shooter.World;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>
    /// ADR-129: authoritative grenade flight against real PhysX. Bounces leave the sphere touching a surface, where the
    /// multi-hit capsule cast reports distance 0 and point (0,0,0); the grenade must still explode where it landed.
    /// </summary>
    public sealed class GrenadePhysXTests
    {
        private readonly List<GameObject> solids = new List<GameObject>();
        private UnityWorldQueries? world;

        [SetUp]
        public void Setup() { Physics.simulationMode = SimulationMode.Script; }

        [TearDown]
        public void Cleanup()
        {
            world?.Dispose(); world = null;
            foreach (var g in solids) if (g != null) UnityEngine.Object.DestroyImmediate(g);
            solids.Clear();
        }

        private void Solid(string name, Vector3 centre, Vector3 size, ulong stableId)
        {
            // Mirrors CompoundMapRuntime.CreateSolid/InstallCollision (static box, layer 8, runtime-created).
            var go = new GameObject(name) { layer = 8, isStatic = true };
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = size;
            go.AddComponent<StableSurface>().Initialize(stableId, (uint)(100 + stableId), 1);
            solids.Add(go);
        }

        private static string F(Float3 v) => $"({v.X:F3},{v.Y:F3},{v.Z:F3})";

        private GrenadeExplosion Fly(Float3 origin, Float3 aim, StringBuilder log, out Float3 lastBeforeBlast)
        {
            Physics.SyncTransforms();
            world = new UnityWorldQueries(1);
            var sim = new GrenadeSimulator();
            Assert.That(sim.TrySpawn(new EntityId(7), 1, origin, aim, WeaponDefinition.Grenade, 120, 0, 1, out var g), Is.True);
            var blasts = new List<GrenadeExplosion>();
            lastBeforeBlast = g.Position;
            for (uint tick = 1; tick < 400; tick++)
            {
                var before = g.Position;
                sim.Step(tick, 1f / 60f, world, blasts);
                if (blasts.Count > 0) return blasts[0];
                log.AppendLine($"t{tick} p={F(g.Position)} v={F(g.Velocity)}");
                float dx = g.Position.X - before.X, dy = g.Position.Y - before.Y, dz = g.Position.Z - before.Z;
                Assert.That(Math.Sqrt(dx * dx + dy * dy + dz * dz), Is.LessThan(1.0), "grenade jumped\n" + log);
                lastBeforeBlast = g.Position;
            }
            throw new AssertionException("grenade never exploded\n" + log);
        }

        [Test]
        public void FloorLandingExplodesWhereTheGrenadeCameToRest()
        {
            Solid("Packaged floor", new Vector3(0, -0.05f, 0), new Vector3(200, 0.1f, 200), 1);
            var log = new StringBuilder();
            var blast = Fly(new Float3(20, 1.5f, 20), new Float3(0, 0, 1), log, out var last);
            Assert.That(blast.Position.Z, Is.GreaterThan(24f), "blast at " + F(blast.Position) + "\n" + log);
            Assert.That(blast.Position.Y, Is.InRange(0.05f, 0.2f), "blast at " + F(blast.Position) + "\n" + log);
            Assert.That(Math.Abs(blast.Position.X - 20f), Is.LessThan(0.1f), "blast at " + F(blast.Position) + "\n" + log);
        }

        [Test]
        public void WallBounceExplodesInFrontOfTheWall()
        {
            Solid("Packaged floor", new Vector3(0, -0.05f, 0), new Vector3(200, 0.1f, 200), 1);
            Solid("Wall", new Vector3(20, 2f, 23.15f), new Vector3(10, 4, 0.3f), 2);
            var log = new StringBuilder();
            var blast = Fly(new Float3(20, 1.5f, 20), new Float3(0, 0, 1), log, out _);
            Assert.That(blast.Position.Z, Is.InRange(14f, 23f), "blast at " + F(blast.Position) + "\n" + log);
            Assert.That(blast.Position.Y, Is.InRange(0.05f, 2f), "blast at " + F(blast.Position) + "\n" + log);
        }

        [Test]
        public void ThrowPressedAgainstAWallStartsOutsideIt()
        {
            Solid("Wall", new Vector3(0, 2f, 0.49f), new Vector3(10, 4, 0.3f), 2);
            Physics.SyncTransforms();
            world = new UnityWorldQueries(1);
            var sim = new GrenadeSimulator();
            var origin = sim.SafeLaunchOrigin(new Float3(0, 1.6f, 0), new Float3(0.18f, 1.45f, 0.45f), world);
            Assert.That(origin.Z + GrenadeSimulator.Radius, Is.LessThanOrEqualTo(0.34f), "origin " + F(origin));
        }
    }
}
