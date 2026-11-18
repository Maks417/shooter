#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.World;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>
    /// Product path (verified map package -> CompoundMapRuntime with packaged-path fallback -> OfflineGameSession):
    /// bots must obtain paths, actually leave their spawns and never walk into solid collision, on every product map.
    /// </summary>
    public sealed class ProductBotNavigationTests
    {
        [SetUp]
        public void Setup() { Physics.simulationMode = SimulationMode.Script; }

        /// <summary>Every registered product map (ADR-114: the ADR-109 rebuilds leaked nav inside walls).</summary>
        public static IEnumerable<string> ProductMaps() => MapRegistry.MapIds;

        private const int Ticks = 1800;
        private const float AgentHeight = 1.8f, MaxStep = 0.25f;

        /// <summary>
        /// ADR-114 product-path gate per map: bots obtain paths (at least 80% complete), leave their spawns, never stand
        /// inside a solid box and are relocated as stuck at most once each over 30 simulated seconds.
        /// </summary>
        [TestCaseSource(nameof(ProductMaps))]
        public void BotsCompletePathsAndLeaveSpawnsOnProductMap(string mapId)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string lockPath = Path.Combine(root, "toolchain-lock.json");
            // The shipped native-nav packages (Compile-GameContent -Native) when published, else the software cache.
            string cache = Path.Combine(root, "Artifacts", "map-packages-native");
            if (!File.Exists(Path.Combine(cache, "catalog.json"))) cache = Path.Combine(root, "Artifacts", "map-packages");
            if (!File.Exists(lockPath) || !Directory.Exists(cache)) Assert.Inconclusive("no map package cache");
            var toolchain = ContentIdentity.Hash(File.ReadAllBytes(lockPath));
            Assert.That(StaticMapCatalog.TryLoadPublished(cache, toolchain, out var catalog, out string diag), Is.True, diag);
            var store = new VerifiedMapPackageStore(cache, toolchain, catalog.GameplayRootHashes,
                catalog.LowVisualRootHashes, catalog.BalancedVisualRootHashes, catalog.HighVisualRootHashes);
            TestContext.WriteLine("SHOOTER_BOT_NAV cache=" + cache);
            var mapKey = new MapId(mapId);
            Assert.That(catalog.TryGet(mapKey, out var entry), Is.True, "catalog has no " + mapId);
            var solids = new List<CollisionBox>();
            foreach (var box in ProductMapPlanner.Generate(mapId).CollisionBoxes)
                if (NavCollisionAudit.IsSolid(box, MaxStep)) solids.Add(box);

            var host = new GameObject("bot nav map host " + mapId);
            try
            {
                var map = new CompoundMapRuntime(host.transform, store, mid =>
                { catalog.TryGet(mid, out var e); return e.GameplayRootHash; })
                {
                    AllowPackagedPathFallback = true
                };
                using var session = new OfflineGameSession(catalog, (_, __) => map, false);
                var request = new SessionRequest(mapKey, WellKnownModes.TeamDeathmatch, 5, entry.RecipeSeed);
                var load = session.BeginLoadAsync(request, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(load.Succeeded, Is.True, load.ToString());
                map.ActorLocator = e => session.Lifecycle!.TryGetActorState(e, out var s) ? s.Feet : (Float3?)null;
                Assert.That(session.SignalReady().Succeeded, Is.True);
                Assert.That(session.Start().Succeeded, Is.True);

                var snapshot = session.TryAcquireSnapshot();
                Assert.That(snapshot, Is.Not.Null);
                var starts = new Dictionary<ulong, Float3>();
                for (int i = 0; i < snapshot!.Participants.Count; i++)
                {
                    var p = snapshot.Participants[i];
                    if (p.IsHuman || !p.Alive) continue;
                    if (session.Lifecycle!.TryGetActorState(p.EntityId, out var state)) starts[p.EntityId.Value] = state.Feet;
                }
                Assert.That(starts.Count, Is.GreaterThan(0), "no bots spawned");

                var last = new Dictionary<ulong, Float3>();
                var relocations = new Dictionary<ulong, int>();
                var insideSolid = new List<string>();
                float maxMoved = 0f;
                for (int tick = 0; tick < Ticks; tick++)
                {
                    var step = session.Step(null);
                    Assert.That(step.Succeeded, Is.True, "tick " + tick + ": " + step);
                    foreach (var pair in starts)
                    {
                        ulong id = pair.Key;
                        if (!session.Lifecycle!.TryGetActorState(new EntityId(id), out var s) || !s.Alive)
                        {
                            last.Remove(id); // death and respawn are not stuck relocations
                            continue;
                        }
                        // A living bot that jumps more than 2 m in one 60 Hz tick was relocated by the stuck rescue.
                        if (last.TryGetValue(id, out var prev) && Horizontal(prev, s.Feet) > 2f)
                            relocations[id] = (relocations.TryGetValue(id, out int n) ? n : 0) + 1;
                        last[id] = s.Feet;
                        maxMoved = Math.Max(maxMoved, Horizontal(pair.Value, s.Feet));
                        if (tick % 10 == 9 && insideSolid.Count < 8 && TryFindSolidAt(solids, s.Feet, out var box))
                            insideSolid.Add("bot " + id + " tick " + tick + " feet " + s.Feet + " in box " + box.Center + "/" + box.Size);
                    }
                }

                var ai = session.BotCounters;
                Assert.That(ai, Is.Not.Null);
                int worstRelocations = 0;
                foreach (var count in relocations.Values) worstRelocations = Math.Max(worstRelocations, count);
                int requests = ai!.PathsCompleted + ai.PathsPartial + ai.PathsFailed;
                string summary = mapId + ": completed=" + ai.PathsCompleted + " partial=" + ai.PathsPartial + " failed=" + ai.PathsFailed +
                                 " stuck=" + ai.StuckEvents + " worstRelocations=" + worstRelocations + " " + map.NavCounterSummary +
                                 " maxMoved=" + maxMoved;
                TestContext.WriteLine("SHOOTER_BOT_NAV " + summary);
                Assert.That(insideSolid, Is.Empty, summary + " | " + string.Join("; ", insideSolid));
                Assert.That(ai.PathsCompleted, Is.GreaterThan(0), summary);
                Assert.That(ai.PathsCompleted, Is.GreaterThanOrEqualTo((int)Math.Ceiling(requests * 0.8)), "fewer than 80% of paths complete: " + summary);
                Assert.That(worstRelocations, Is.LessThanOrEqualTo(1), "a bot was relocated as stuck more than once: " + summary);
                Assert.That(maxMoved, Is.GreaterThan(2f), summary);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static float Horizontal(Float3 a, Float3 b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Feet inside the footprint (shrunk 0.05 m) of a solid that blocks an agent standing at that height.</summary>
        private static bool TryFindSolidAt(List<CollisionBox> solids, Float3 feet, out CollisionBox found)
        {
            foreach (var box in solids)
            {
                if (Math.Abs(feet.X - box.Center.X) >= box.Size.X * 0.5f - NavCollisionAudit.FootprintShrinkMeters) continue;
                if (Math.Abs(feet.Z - box.Center.Z) >= box.Size.Z * 0.5f - NavCollisionAudit.FootprintShrinkMeters) continue;
                if (!NavCollisionAudit.Blocks(box, feet.Y, AgentHeight, MaxStep)) continue;
                found = box;
                return true;
            }
            found = default;
            return false;
        }
    }
}
