#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Shooter.Simulation.AI;
using Shooter.Simulation.Motor;
using Shooter.World.Navigation;
using Shooter.World.Streaming;
using Xunit;

namespace Shooter.Simulation.Tests.M3
{
    public sealed class CompoundIntegrationTests
    {
        private sealed class PlanWorld : IWorldQueries, IFakeAdapter
        {
            private readonly IReadOnlyList<CollisionBox> boxes;
            private readonly uint revision;
            public PlanWorld(CompoundPlan plan)
            {
                boxes = plan.CollisionBoxes;
                revision = plan.CollisionRevision;
            }
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
            {
                // Analytic occlusion against gameplay boxes for AI vision.
                float best = maxMeters;
                bool hit = false;
                ulong stable = 0;
                ushort tag = 1;
                for (int i = 0; i < boxes.Count; i++)
                {
                    var b = boxes[i];
                    if (!b.Gameplay) continue;
                    if (!RayAabb(origin, unitDirection, maxMeters, b, out float t) || t >= best) continue;
                    best = t; hit = true; stable = b.StableId; tag = b.SurfaceTag;
                }
                if (!hit) return new RayHit(QueryStatus.NoHit);
                var point = new Float3(origin.X + unitDirection.X * best, origin.Y + unitDirection.Y * best, origin.Z + unitDirection.Z * best);
                return new RayHit(QueryStatus.Hit, new QueryHit(best, point, new Float3(0, 1, 0), default, new SurfaceId(1), stable, tag, revision));
            }
            private static bool RayAabb(Float3 o, Float3 d, float max, CollisionBox b, out float tHit)
            {
                tHit = 0;
                float tmin = 0, tmax = max;
                float[] oA = { o.X, o.Y, o.Z }, dA = { d.X, d.Y, d.Z };
                float[] cA = { b.Center.X, b.Center.Y, b.Center.Z }, sA = { b.Size.X * 0.5f, b.Size.Y * 0.5f, b.Size.Z * 0.5f };
                for (int a = 0; a < 3; a++)
                {
                    float min = cA[a] - sA[a], maxA = cA[a] + sA[a];
                    if (Math.Abs(dA[a]) < 1e-8f)
                    {
                        if (oA[a] < min || oA[a] > maxA) return false;
                        continue;
                    }
                    float inv = 1f / dA[a];
                    float t0 = (min - oA[a]) * inv, t1 = (maxA - oA[a]) * inv;
                    if (t0 > t1) { float tmp = t0; t0 = t1; t1 = tmp; }
                    if (t0 > tmin) tmin = t0;
                    if (t1 < tmax) tmax = t1;
                    if (tmax < tmin) return false;
                }
                if (tmin < 0 || tmin > max) return false;
                tHit = tmin;
                return true;
            }
        }

        [Fact]
        public void Canonical_plan_nav_streaming_and_ai_budgets_hold()
        {
            var plan = CompoundPlanner.Generate(460072030UL);
            var report = CompoundValidation.Validate(plan);
            Assert.True(report.Passed, report.Diagnostic);

            var settings = NavSettings.SliceDefault;
            var covers = new List<(ulong, Float3, float, float, float, Float3, Float3, bool)>();
            for (int i = 0; i < plan.Covers.Count; i++)
            {
                var c = plan.Covers[i];
                covers.Add((c.StableId, c.Center, c.SizeX, c.SizeY, c.SizeZ, c.PeekStanding, c.PeekCrouch, c.HardCover));
            }
            bool Path(Float3 a, Float3 b, List<Float3> corners) =>
                CompoundConnectivity.TryFindPath(plan.CollisionBoxes, a, b, corners, settings.RequiredClearance);
            bool Blocked(float x, float z, float clearance) =>
                CompoundConnectivity.IsBlocked(plan.CollisionBoxes, x, z, clearance);
            var nav = NavCompiler.Compile(plan.NavRevision, plan.CollisionHash, settings, plan.PlayerSpawn, plan.ObjectiveCenter, plan.BotSpawns, covers, Path, Blocked);
            Assert.True(nav.AllObjectivesReachable);
            Assert.True(nav.MinClearanceMeters + 1e-4f >= settings.RequiredClearance);

            var streaming = new VisualStreamingScheduler();
            var path = new List<ChunkKey>();
            for (int i = 0; i < 100; i++)
            {
                var corner = plan.RouteCorners[Math.Min(i, plan.RouteCorners.Count - 1)];
                var chunk = CompoundPlan.ChunkAt(corner.X, corner.Z);
                path.Add(new ChunkKey(chunk.X, chunk.Z));
            }
            streaming.RunTraversal(path, _ => 4096, _ => 0.4);
            Assert.Equal(0, streaming.HandleLeakCount);

            var world = new PlanWorld(plan);
            var positions = new Dictionary<ulong, Float3>();
            var adapter = new RevisionedPathAdapter(plan.NavRevision,
                id => positions.TryGetValue(id.Value, out var p) ? p : (Float3?)null,
                (a, b, corners) => CompoundConnectivity.TryFindPath(plan.CollisionBoxes, a, b, corners!, settings.RequiredClearance));
            var ai = new CombatAiDirector(world, adapter, CollisionLayers.VisionMask, plan.NavRevision);
            var sites = new List<CoverSite>();
            for (int i = 0; i < plan.Covers.Count; i++)
                sites.Add(new CoverSite(plan.Covers[i].StableId, plan.Covers[i].Center, plan.Covers[i].PeekStanding));
            ai.SetCoverSites(sites);
            for (int i = 0; i < plan.BotSpawns.Count; i++)
            {
                var id = new EntityId((ulong)(i + 1));
                positions[id.Value] = plan.BotSpawns[i];
                ai.RegisterBot(id, plan.BotSpawns[i], 0);
            }
            var commands = new InputCommand[8];
            var player = new SenseTarget(new EntityId(99), new Float3(plan.PlayerSpawn.X, plan.PlayerSpawn.Y + 1.65f, plan.PlayerSpawn.Z));
            for (uint t = 1; t <= 120; t++)
            {
                ai.Plan(t, player, commands, 0);
                Assert.True(ai.Counters.VisionRaysThisTick <= 16);
                Assert.True(ai.Counters.PathsRequestedThisTick <= 2);
            }
        }

        [Fact]
        public void Thirty_minute_tick_soak_has_no_unbounded_queues_or_orphan_covers()
        {
            // 30 minutes × 60 Hz = 108000 ticks. Pure software soak (not wall-clock / hardware).
            const uint soakTicks = 108000;
            var plan = CompoundPlanner.Generate(42UL);
            var world = new PlanWorld(plan);
            var positions = new Dictionary<ulong, Float3>();
            var adapter = new RevisionedPathAdapter(1,
                id => positions.TryGetValue(id.Value, out var p) ? p : (Float3?)null,
                (a, b, corners) =>
                {
                    corners!.Clear();
                    corners.Add(a); corners.Add(b);
                    return true;
                });
            var ai = new CombatAiDirector(world, adapter, CollisionLayers.VisionMask);
            var sites = new List<CoverSite>();
            for (int i = 0; i < Math.Min(8, plan.Covers.Count); i++)
                sites.Add(new CoverSite(plan.Covers[i].StableId, plan.Covers[i].Center, plan.Covers[i].PeekStanding));
            if (sites.Count == 0) sites.Add(new CoverSite(1, new Float3(0, 0, 0), new Float3(1, 0, 0)));
            ai.SetCoverSites(sites);
            for (int i = 0; i < 8; i++)
            {
                var id = new EntityId((ulong)(i + 1));
                var feet = i < plan.BotSpawns.Count ? plan.BotSpawns[i] : new Float3(i, 0, i);
                positions[id.Value] = feet;
                ai.RegisterBot(id, feet, 0);
            }
            var commands = new InputCommand[8];
            var player = new SenseTarget(new EntityId(99), new Float3(plan.PlayerSpawn.X, plan.PlayerSpawn.Y + 1.65f, plan.PlayerSpawn.Z));
            for (uint t = 1; t <= soakTicks; t++)
            {
                // Keep bot positions refreshed for path adapter.
                for (int i = 0; i < ai.BotCount; i++)
                    positions[ai.GetBoard(i).Self.Value] = ai.GetBoard(i).Feet;
                ai.Plan(t, player, commands, 0);
                Assert.True(ai.Counters.VisionRaysThisTick <= 16);
                Assert.True(ai.Counters.PathsRequestedThisTick <= 2);
                Assert.True(adapter.PendingCount <= 128);
            }
            // Kill all bots and ensure cover board empties.
            for (int i = 0; i < ai.BotCount; i++) ai.NotifyDeath(ai.GetBoard(i).Self);
            Assert.Equal(0, ai.CoverBoard.ActiveClaims);
            Assert.True(ai.Counters.OrphanReleases == 0 || ai.Counters.CoverReleases >= ai.Counters.CoverClaims);
        }

        [Fact]
        public void Shipping_composition_rejects_plan_world_fake()
        {
            var plan = CompoundPlanner.Generate(1UL);
            Assert.Throws<InvalidOperationException>(() =>
                CompositionPolicy.RequireReal(new PlanWorld(plan)));
        }
    }
}
