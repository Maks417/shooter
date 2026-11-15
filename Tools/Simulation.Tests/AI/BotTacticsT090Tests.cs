#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.AI;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>T090 bot tactics: cover peek preference, wall-aware hearing, ledge-safe strafe, route-aware spread.</summary>
    public sealed class BotTacticsT090Tests
    {
        private sealed class BoxWorld : IWorldQueries
        {
            private readonly List<(Float3 Min, Float3 Max)> boxes = new List<(Float3, Float3)>();
            public int Rays { get; private set; }
            public BoxWorld Add(Float3 min, Float3 max) { boxes.Add((min, max)); return this; }
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) =>
                new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 dir, float maxMeters, QueryFilter filter)
            {
                Rays++;
                float best = float.MaxValue;
                foreach (var (min, max) in boxes)
                    if (Slab(origin, dir, min, max, out float t) && t <= maxMeters && t < best) best = t;
                if (best == float.MaxValue) return new RayHit(QueryStatus.NoHit);
                var p = new Float3(origin.X + dir.X * best, origin.Y + dir.Y * best, origin.Z + dir.Z * best);
                return new RayHit(QueryStatus.Hit, new QueryHit(best, p, new Float3(0, 1, 0), default, new SurfaceId(1), 7, 1, 1));
            }

            private static bool Slab(Float3 o, Float3 d, Float3 min, Float3 max, out float tEnter)
            {
                float t0 = 0, t1 = float.MaxValue;
                tEnter = 0;
                float[] os = { o.X, o.Y, o.Z }, ds = { d.X, d.Y, d.Z }, mins = { min.X, min.Y, min.Z }, maxs = { max.X, max.Y, max.Z };
                for (int a = 0; a < 3; a++)
                {
                    if (Math.Abs(ds[a]) < 1e-8f)
                    {
                        if (os[a] < mins[a] || os[a] > maxs[a]) return false;
                        continue;
                    }
                    float ta = (mins[a] - os[a]) / ds[a], tb = (maxs[a] - os[a]) / ds[a];
                    if (ta > tb) (ta, tb) = (tb, ta);
                    t0 = Math.Max(t0, ta);
                    t1 = Math.Min(t1, tb);
                    if (t0 > t1) return false;
                }
                tEnter = t0;
                return true;
            }
        }

        /// <summary>Ground plane at Y=0 everywhere except a deep void for X &gt; voidFromX (ledge).</summary>
        private sealed class LedgeWorld : IWorldQueries
        {
            private readonly float voidFromX;
            public LedgeWorld(float voidFromX) { this.voidFromX = voidFromX; }
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) =>
                new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 dir, float maxMeters, QueryFilter filter)
            {
                // Only answer downward ground probes.
                if (dir.Y > -0.9f) return new RayHit(QueryStatus.NoHit);
                if (origin.X > voidFromX) return new RayHit(QueryStatus.NoHit);
                float t = origin.Y / -dir.Y;
                if (t < 0 || t > maxMeters) return new RayHit(QueryStatus.NoHit);
                var p = new Float3(origin.X, 0f, origin.Z);
                return new RayHit(QueryStatus.Hit, new QueryHit(t, p, new Float3(0, 1, 0), default, new SurfaceId(1), 7, 1, 1));
            }
        }

        private sealed class LineNav : INavigation
        {
            private ulong next;
            private readonly Dictionary<ulong, Float3> goals = new Dictionary<ulong, Float3>();
            public List<Float3> Requested { get; } = new List<Float3>();
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
            {
                Requested.Add(destination);
                var id = ++next;
                goals[id] = destination;
                return new PathTicket(new NavTicketId(id), actor, navRevision);
            }
            public void Cancel(PathTicket ticket) => goals.Remove(ticket.Id.Value);
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
            {
                if (!goals.TryGetValue(ticket.Id.Value, out var goal)) return new PathResult(PathStatus.Cancelled, 0, 0, ErrorCode.Cancelled);
                goals.Remove(ticket.Id.Value);
                callerOwned[0] = goal;
                return new PathResult(PathStatus.Complete, 1, 1, ErrorCode.None);
            }
        }

        private static readonly EntityId Enemy = new EntityId(99);

        private static SenseTarget EnemyAt(float x, float z, ushort health = 100) =>
            new SenseTarget(Enemy, new Float3(x, CombatAiDirector.StandingAimHeight, z), team: 2, health: health);

        [Fact]
        public void Gunfire_behind_a_wall_is_not_investigated()
        {
            // Wall between origin and source at x=10.
            var world = new BoxWorld().Add(new Float3(4, 0, -2), new Float3(5, 3, 2));
            var ai = new CombatAiDirector(world, new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            ai.ObserveGunfire(Enemy, new Float3(10, 1.5f, 0));
            Assert.Equal(AiState.Patrol, bot.State);
            Assert.Equal(HeardClass.None, bot.Heard);
            Assert.True(ai.Counters.HearingOccluded >= 1);
            Assert.Equal(0, ai.Counters.StimuliHeard);
        }

        [Fact]
        public void Gunfire_in_open_air_is_still_investigated()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            ai.ObserveGunfire(Enemy, new Float3(10, 1.5f, 0));
            Assert.Equal(AiState.Investigate, bot.State);
            Assert.Equal(HeardClass.Gunfire, bot.Heard);
            Assert.Equal(1, ai.Counters.StimuliHeard);
        }

        [Fact]
        public void Cover_probe_prefers_a_peekable_point_over_a_blind_pocket()
        {
            // Narrow wall at z=-2: hides the south candidate chest. A side peek (+X) clears the wall edge.
            // A second "pocket" farther west is fully boxed (blind) but listed later — should not win.
            var world = new BoxWorld()
                .Add(new Float3(-0.4f, 0, -2.4f), new Float3(0.4f, 1.2f, -1.6f))
                .Add(new Float3(-6.5f, 0, -5.5f), new Float3(-5.5f, 2.5f, -3.5f))
                .Add(new Float3(-7.5f, 0, -4.5f), new Float3(-4.5f, 2.5f, -3.5f));
            var ai = new CombatAiDirector(world, new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            bot.Health = 20;
            var enemies = new[] { EnemyAt(0, 20f, health: 100) };
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 120 && bot.CoverPoint == null; t++) ai.PlanEnemies(t, enemies, 1, cmds, 0);
            Assert.NotNull(bot.CoverPoint);
            Assert.True(ai.Counters.CoverPeekAccepted >= 1 || ai.Counters.CoverPeekRays >= 1);
            // Preferred first candidate is ~4 m south when peek clears; accept nearby south cover.
            Assert.True(bot.CoverPoint!.Value.Z < -2f, "cover z " + bot.CoverPoint.Value.Z);
        }

        [Fact]
        public void Strafe_flips_or_cancels_when_the_next_step_has_no_footing()
        {
            // Ground only for X <= 0.2; enemy north so strafe axis is ±X. Bot starts at origin.
            var ai = new CombatAiDirector(new LedgeWorld(voidFromX: 0.2f), new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var enemies = new[] { EnemyAt(0, 12f) };
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 90; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                // Feed a small step so StrafeCheck can see progress; keep Y=0 footing under the bot.
                bot.Feet = new Float3(
                    Math.Clamp(bot.Feet.X + cmds[0].MoveX * 0.04f, -0.15f, 0.15f),
                    0f,
                    bot.Feet.Z + cmds[0].MoveY * 0.02f);
            }
            Assert.Equal(AiState.Engage, bot.State);
            Assert.True(ai.Counters.StrafeLedgeFlips >= 1, "expected ledge flips, got " + ai.Counters.StrafeLedgeFlips);
        }

        [Fact]
        public void Spread_avoids_destinations_on_an_ally_path_corridor()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var route = new List<Float3>();
            for (int k = 0; k < 12; k++) route.Add(new Float3(-40 + 8 * k, 0, 0));
            // Off-corridor destinations far from the east-west path.
            route.Add(new Float3(0, 0, 40));
            route.Add(new Float3(0, 0, -40));
            ai.SetNavigationDestinations(route, null);

            var leader = ai.RegisterBot(new EntityId(1), new Float3(-40, 0, 0), 0, team: 1);
            // Simulate an active corridor along +X that another bot would otherwise share.
            leader.PathCornerCount = 5;
            leader.PathCornerIndex = 0;
            for (int i = 0; i < 5; i++) leader.PathCorners[i] = new Float3(-30 + 10 * i, 0, 0);
            leader.PathGoal = new Float3(20, 0, 0);
            leader.State = AiState.Patrol;
            leader.PatrolGoal = leader.PathGoal;

            var follower = ai.RegisterBot(new EntityId(2), new Float3(-35, 0, 0), 0, team: 1);
            var cmds = new InputCommand[2];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            Assert.True(follower.PatrolGoal.HasValue);
            Assert.True(ai.Counters.RouteCorridorPenalties >= 1);
            // Follower should prefer a north/south destination rather than another point on the +X corridor.
            Assert.True(Math.Abs(follower.PatrolGoal!.Value.Z) > 20f,
                "follower goal " + follower.PatrolGoal.Value);
        }
    }
}
