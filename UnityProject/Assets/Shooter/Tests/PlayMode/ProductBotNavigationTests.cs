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
using Shooter.World;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>
    /// Product path (verified map package -> CompoundMapRuntime with packaged-path fallback -> OfflineGameSession):
    /// bots must obtain paths and actually leave their spawns.
    /// </summary>
    public sealed class ProductBotNavigationTests
    {
        [SetUp]
        public void Setup() { Physics.simulationMode = SimulationMode.Script; }

        [Test]
        public void BotsCompletePathsAndLeaveSpawnsOnProductMap()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string lockPath = Path.Combine(root, "toolchain-lock.json");
            string cache = Path.Combine(root, "Artifacts", "map-packages");
            if (!File.Exists(lockPath) || !Directory.Exists(cache)) Assert.Inconclusive("no map package cache");
            var toolchain = ContentIdentity.Hash(File.ReadAllBytes(lockPath));
            Assert.That(StaticMapCatalog.TryLoadPublished(cache, toolchain, out var catalog, out string diag), Is.True, diag);
            var store = new VerifiedMapPackageStore(cache, toolchain, catalog.GameplayRootHashes,
                catalog.LowVisualRootHashes, catalog.BalancedVisualRootHashes, catalog.HighVisualRootHashes);
            var host = new GameObject("bot nav map host");
            try
            {
                var map = new CompoundMapRuntime(host.transform, store, mid =>
                { catalog.TryGet(mid, out var e); return e.GameplayRootHash; })
                {
                    AllowPackagedPathFallback = true
                };
                using var session = new OfflineGameSession(catalog, (_, __) => map, false);
                catalog.TryGet(WellKnownMaps.DepotYard, out var entry);
                var request = new SessionRequest(WellKnownMaps.DepotYard, WellKnownModes.TeamDeathmatch, 5, entry.RecipeSeed);
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

                float maxMoved = 0f;
                for (int tick = 0; tick < 600; tick++)
                {
                    var step = session.Step(null);
                    Assert.That(step.Succeeded, Is.True, "tick " + tick + ": " + step);
                    if (tick % 10 != 9) continue;
                    foreach (var pair in starts)
                    {
                        if (!session.Lifecycle!.TryGetActorState(new EntityId(pair.Key), out var s)) continue;
                        float dx = s.Feet.X - pair.Value.X, dz = s.Feet.Z - pair.Value.Z;
                        maxMoved = Math.Max(maxMoved, (float)Math.Sqrt(dx * dx + dz * dz));
                    }
                }

                var ai = session.BotCounters;
                Assert.That(ai, Is.Not.Null);
                string summary = "completed=" + ai!.PathsCompleted + " partial=" + ai.PathsPartial + " failed=" + ai.PathsFailed +
                                 " stuck=" + ai.StuckEvents + " " + map.NavCounterSummary + " maxMoved=" + maxMoved;
                Assert.That(ai.PathsCompleted, Is.GreaterThan(0), summary);
                Assert.That(maxMoved, Is.GreaterThan(2f), summary);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
