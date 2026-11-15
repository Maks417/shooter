#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.AI;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>T084: rear awareness (footsteps, unseen attackers), ally gunfire, and team spread.</summary>
    public sealed class BotAwarenessTests
    {
        private sealed class OpenWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
        }

        private sealed class LineNav : INavigation
        {
            private ulong next;
            private readonly Dictionary<ulong, Float3> goals = new Dictionary<ulong, Float3>();
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
            {
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

        private static readonly EntityId Front = new EntityId(99);
        private static readonly EntityId Behind = new EntityId(98);

        private static SenseTarget At(EntityId id, float x, float z) =>
            new SenseTarget(id, new Float3(x, CombatAiDirector.StandingAimHeight, z), team: 2);

        /// <summary>Bot 4 at the origin walking a route straight ahead (+Z), so its back faces -Z.</summary>
        private static CombatAiDirector FacingForward(out AiBlackboard bot)
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            ai.SetNavigationDestinations(new[] { new Float3(0, 0, 60) }, null);
            bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            return ai;
        }

        [Fact]
        public void A_sprinting_enemy_behind_is_heard_turned_on_and_engaged()
        {
            var ai = FacingForward(out var bot);
            var cmds = new InputCommand[1];
            bool heard = false;
            for (uint t = 1; t <= 150; t++)
            {
                // 6 m/s across the bot's back, 15 m away: outside the vision cone, inside sprint hearing.
                var enemies = new[] { At(Front, -3f + 0.1f * t, -15f) };
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                if (t == 5)
                {
                    Assert.False(bot.HasVisibleTarget);
                    Assert.Equal(HeardClass.Footsteps, bot.Heard);
                    Assert.Equal(AiState.Investigate, bot.State);
                    heard = true;
                }
            }
            Assert.True(heard);
            Assert.True(ai.Counters.FootstepsHeard >= 1);
            Assert.Equal(Front, bot.TargetId);
            Assert.Equal(AiState.Engage, bot.State);
        }

        [Fact]
        public void A_still_enemy_behind_is_not_heard_but_one_at_arms_length_is()
        {
            var ai = FacingForward(out var bot);
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 60; t++) ai.PlanEnemies(t, new[] { At(Front, 0, -6f) }, 1, cmds, 0);
            Assert.Equal(HeardClass.None, bot.Heard);
            Assert.False(bot.HasVisibleTarget);
            Assert.Equal(AiState.Patrol, bot.State);

            var ai2 = FacingForward(out var bot2);
            for (uint t = 1; t <= 60; t++) ai2.PlanEnemies(t, new[] { At(Front, 0.5f, -2f) }, 1, cmds, 0);
            Assert.True(bot2.HasVisibleTarget || bot2.Heard == HeardClass.Footsteps);
            Assert.Equal(Front, bot2.TargetId);
        }

        [Fact]
        public void Repeated_hits_from_an_unseen_attacker_make_the_bot_turn_on_it()
        {
            var ai = FacingForward(out var bot);
            var cmds = new InputCommand[1];
            var enemies = new[] { At(Front, 0, 18f), At(Behind, 2f, -12f) };
            uint t = 1;
            for (; t <= 60; t++) ai.PlanEnemies(t, enemies, 2, cmds, 0);
            Assert.Equal(AiState.Engage, bot.State);
            Assert.Equal(Front, bot.TargetId);

            // One stray hit: keep fighting the visible enemy.
            ai.ObserveDamage(bot.Self, Behind, enemies[1].Point);
            for (; t <= 75; t++) ai.PlanEnemies(t, enemies, 2, cmds, 0);
            Assert.Equal(Front, bot.TargetId);
            Assert.True(bot.HasVisibleTarget);

            // A second hit inside the window: turn and fight the attacker.
            ai.ObserveDamage(bot.Self, Behind, enemies[1].Point);
            for (; t <= 160; t++) ai.PlanEnemies(t, enemies, 2, cmds, 0);
            Assert.Equal(1, ai.Counters.ThreatTurns);
            Assert.Equal(Behind, bot.TargetId);
            Assert.True(Math.Abs(cmds[0].AimYaw) > 2.4f, "yaw " + cmds[0].AimYaw);
        }

        [Fact]
        public void Teammate_gunfire_points_at_its_enemy_never_at_the_teammate()
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            var shooter = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var listener = ai.RegisterBot(new EntityId(5), new Float3(10, 0, 0), 0, team: 1);
            var cmds = new InputCommand[2];
            ai.PlanEnemies(1, null, 0, cmds, 0);

            ai.ObserveGunfire(shooter.Self, new Float3(0, 1.5f, 0));
            Assert.Equal(AiState.Patrol, listener.State);
            Assert.Equal(1, ai.Counters.AllyGunfireIgnored);

            shooter.LastSeenEnemy = new Float3(0, 1.3f, -30);
            shooter.LastSeenTick = 1;
            ai.ObserveGunfire(shooter.Self, new Float3(0, 1.5f, 0));
            Assert.Equal(AiState.Investigate, listener.State);
            Assert.True(listener.LastHeard!.Value.Z < -25f, "heard " + listener.LastHeard.Value.Z);
        }

        [Fact]
        public void Only_two_teammates_investigate_the_same_noise_the_rest_watch_it()
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            var bots = new List<AiBlackboard>();
            for (ulong id = 1; id <= 5; id++) bots.Add(ai.RegisterBot(new EntityId(id), new Float3(id, 0, 0), 0, team: 1));
            var cmds = new InputCommand[5];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            ai.ObserveGunfire(Front, new Float3(20, 1.5f, 10));
            int investigating = 0;
            foreach (var b in bots)
            {
                Assert.Equal(HeardClass.Gunfire, b.Heard);
                if (b.State == AiState.Investigate) investigating++;
            }
            Assert.Equal(AiTuning.InvestigateCrowdLimit, investigating);
            Assert.Equal(3, ai.Counters.CrowdedStimuliHeld);
            // Watchers still face the noise instead of the route.
            for (uint t = 2; t <= 50; t++) ai.PlanEnemies(t, null, 0, cmds, 0);
            foreach (var c in cmds) Assert.True(c.AimYaw > 0.6f && c.AimYaw < 1.8f, "yaw " + c.AimYaw);
        }

        [Fact]
        public void Teammates_pick_spread_patrol_goals_instead_of_trailing_one_route()
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            // A dense ordered route (product patrol destinations are subsampled route corners, ~6 m apart).
            var route = new List<Float3>();
            for (int k = 0; k < 16; k++) route.Add(new Float3(-45 + 6 * k, 0, 25));
            ai.SetNavigationDestinations(route, null);
            var bots = new List<AiBlackboard>();
            for (ulong id = 1; id <= 4; id++) bots.Add(ai.RegisterBot(new EntityId(id), new Float3(id, 0, 0), 0, team: 1));
            var cmds = new InputCommand[4];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            for (int i = 0; i < bots.Count; i++)
            {
                Assert.True(bots[i].PatrolGoal.HasValue);
                for (int j = 0; j < i; j++)
                {
                    float dx = bots[i].PatrolGoal!.Value.X - bots[j].PatrolGoal!.Value.X;
                    Assert.True(Math.Abs(dx) >= AiTuning.SpreadMeters, $"bots {j} and {i} share goal area (dx {dx})");
                }
            }
            // Deterministic: a fresh director picks the same goals.
            var again = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            again.SetNavigationDestinations(route, null);
            var copies = new List<AiBlackboard>();
            for (ulong id = 1; id <= 4; id++) copies.Add(again.RegisterBot(new EntityId(id), new Float3(id, 0, 0), 0, team: 1));
            again.PlanEnemies(1, null, 0, cmds, 0);
            for (int i = 0; i < 4; i++) Assert.Equal(bots[i].PatrolGoal!.Value.X, copies[i].PatrolGoal!.Value.X);
        }
    }
}
