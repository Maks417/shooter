#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation;
using Shooter.Simulation.Motor;
using Shooter.World;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>
    /// Sprint / jump / slide against real PhysX: first at the bare motor level on a product-style runtime floor, then
    /// through the product path (verified map package -> CompoundMapRuntime -> OfflineGameSession).
    /// </summary>
    public sealed class ProductLocomotionPhysXTests
    {
        private GameObject? floor;
        private UnityWorldQueries? world;

        [SetUp]
        public void Setup() { Physics.simulationMode = SimulationMode.Script; }

        [TearDown]
        public void Cleanup()
        {
            world?.Dispose(); world = null;
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
            floor = null;
        }

        private static string F(Float3 v) => $"({v.X:F3},{v.Y:F3},{v.Z:F3})";

        private static string H(SweepHit h) => h.Status == QueryStatus.Hit ? $"Hit d={h.Hit.DistanceMeters:F4} n={F(h.Hit.Normal)}" : h.Status.ToString();

        private static float Speed(Float3 a, Float3 b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz) * 60f;
        }


        [Test]
        public void MotorOnProductStyleFloorSprintsJumpsAndSlides()
        {
            // Mirrors CompoundMapRuntime.CreateSolid/InstallCollision (static box, layer 8, runtime-created).
            floor = new GameObject("Packaged floor") { layer = 8, isStatic = true };
            floor.transform.position = new Vector3(0, -0.05f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(200, 0.1f, 200);
            floor.AddComponent<StableSurface>().Initialize(1, 100, 1);
            Physics.SyncTransforms();
            world = new UnityWorldQueries(1);

            var motor = new KinematicMotor();
            var id = new EntityId(1);
            var state = new MotorBodyState(new Float3(0, 0, 0), default, 0, 0, Stance.Standing, motor.Config.StandingHeight, false,
                default, new Float3(0, 1, 0), 0, 0, 0, false);
            var log = new StringBuilder();
            uint seq = 1, tick = 0;
            void Step(InputButtons b, float moveY, bool slide = false)
            {
                state = motor.Step(state, new InputCommand(id, tick, seq, 0, moveY, 0, 0, b, 0), tick, world!, slide);
                log.AppendLine($"t{tick} b={b} feet={F(state.Feet)} v={F(state.Velocity)} g={state.Grounded} sp={state.SprintActive} sl={state.SlideTicksRemaining} ovl={state.OverlapUnresolved}");
                tick++; seq++;
            }
            for (int i = 0; i < 20; i++) Step(InputButtons.None, 0);
            Assert.That(state.Grounded, Is.True, "grounded after settle\n" + log);
            float maxSpeed = 0;
            for (int i = 0; i < 60; i++)
            {
                var before = state.Feet; Step(InputButtons.SprintHeld, 1);
                maxSpeed = Math.Max(maxSpeed, Speed(before, state.Feet));
            }
            Assert.That(maxSpeed, Is.GreaterThan(5.2f), "sprint speed\n" + log);
            Assert.That(state.SprintActive, Is.True, "SprintActive\n" + log);
            Step(InputButtons.SprintHeld, 1, slide: true);
            Assert.That(state.SlideActive, Is.True, "slide\n" + log);
            for (int i = 0; i < 90; i++) Step(InputButtons.None, 0);
            float y0 = state.Feet.Y, maxY = y0;
            Step(InputButtons.JumpPressed, 0);
            for (int i = 0; i < 40; i++) { Step(InputButtons.None, 0); maxY = Math.Max(maxY, state.Feet.Y); }
            Assert.That(maxY - y0, Is.GreaterThan(0.5f), "jump rise\n" + log);
        }

        [Test]
        public void ProductSessionPlayerSprintsJumpsAndSlides()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string lockPath = Path.Combine(root, "toolchain-lock.json");
            string cache = Path.Combine(root, "Artifacts", "map-packages");
            if (!File.Exists(lockPath) || !Directory.Exists(cache)) Assert.Inconclusive("no map package cache");
            var toolchain = ContentIdentity.Hash(File.ReadAllBytes(lockPath));
            Assert.That(StaticMapCatalog.TryLoadPublished(cache, toolchain, out var catalog, out string diag), Is.True, diag);
            var store = new VerifiedMapPackageStore(cache, toolchain, catalog.GameplayRootHashes,
                catalog.LowVisualRootHashes, catalog.BalancedVisualRootHashes, catalog.HighVisualRootHashes);
            var host = new GameObject("map host");
            try
            {
                var map = new CompoundMapRuntime(host.transform, store, mid =>
                { catalog.TryGet(mid, out var e); return e.GameplayRootHash; });
                using var session = new OfflineGameSession(catalog, (_, __) => map, false);
                catalog.TryGet(WellKnownMaps.DepotYard, out var entry);
                var request = new SessionRequest(WellKnownMaps.DepotYard, WellKnownModes.TeamDeathmatch, 5, entry.RecipeSeed);
                var load = session.BeginLoadAsync(request, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(load.Succeeded, Is.True, load.ToString());
                map.ActorLocator = e => session.Lifecycle!.TryGetActorState(e, out var s) ? s.Feet : (Float3?)null;
                Assert.That(session.SignalReady().Succeeded, Is.True);
                Assert.That(session.Start().Succeeded, Is.True);

                var human = session.HumanEntityId;
                var log = new StringBuilder();
                uint seq = 1;
                Float3 Feet() { session.Lifecycle!.TryGetActorState(human, out var s); return s.Feet; }
                float yaw = 0;
                void Tick(InputButtons b, float moveY, bool slide = false)
                {
                    var before = Feet();
                    uint t = session.CompletedTick;
                    var r = session.Step(new InputCommand(human, t, seq++, 0, moveY, yaw, 0, b, 0), slide);
                    session.TryGetMovementView(human, out var view);
                    var after = Feet();
                    log.AppendLine($"t{t} b={b} feet={F(after)} dv={Speed(before, after):F2} sliding={view.Sliding} stance={view.Stance} r={r.Succeeded}");
                }
                for (int i = 0; i < 40; i++) Tick(InputButtons.None, 0);
                float y0 = Feet().Y;
                float maxY = y0;
                Tick(InputButtons.JumpPressed, 0);
                for (int i = 0; i < 40; i++) { Tick(InputButtons.None, 0); maxY = Math.Max(maxY, Feet().Y); }
                Assert.That(maxY - y0, Is.GreaterThan(0.5f), "human jump rise (y0=" + y0 + ")\n" + log);

                for (int i = 0; i < 40; i++) Tick(InputButtons.None, 0);
                float maxSpeed = 0;
                for (int i = 0; i < 30; i++)
                {
                    var before = Feet(); Tick(InputButtons.SprintHeld, 1);
                    maxSpeed = Math.Max(maxSpeed, Speed(before, Feet()));
                }
                Tick(InputButtons.SprintHeld, 1, slide: true);
                session.TryGetMovementView(human, out var v);
                Assert.That(maxSpeed, Is.GreaterThan(5.2f), "human sprint speed\n" + log);
                Assert.That(v.Sliding, Is.True, "human slide\n" + log);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
