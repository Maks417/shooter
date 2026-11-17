#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.AI;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>ADR-100: bots hop low obstacles, hop on some strafe reversals and slide more often.</summary>
    public sealed class BotJumpSlideTests
    {
        private sealed class BoxWorld : IWorldQueries
        {
            private readonly List<(Float3 Min, Float3 Max)> boxes = new List<(Float3, Float3)>();
            public BoxWorld Add(Float3 min, Float3 max) { boxes.Add((min, max)); return this; }
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 dir, float maxMeters, QueryFilter filter)
            {
                float best = float.MaxValue;
                foreach (var (min, max) in boxes)
                    if (Slab(origin, dir, min, max, out float t) && t <= maxMeters && t < best) best = t;
                if (best == float.MaxValue) return new RayHit(QueryStatus.NoHit);
                var p = new Float3(origin.X + dir.X * best, origin.Y + dir.Y * best, origin.Z + dir.Z * best);
                return new RayHit(QueryStatus.Hit, new QueryHit(best, p, new Float3(-1, 0, 0), default, new SurfaceId(1), 7, 1, 1));
            }
            private static bool Slab(Float3 o, Float3 d, Float3 min, Float3 max, out float tEnter)
            {
                float t0 = 0, t1 = float.MaxValue; tEnter = 0;
                for (int axis = 0; axis < 3; axis++)
                {
                    float oo = axis == 0 ? o.X : axis == 1 ? o.Y : o.Z, dd = axis == 0 ? d.X : axis == 1 ? d.Y : d.Z;
                    float lo = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z, hi = axis == 0 ? max.X : axis == 1 ? max.Y : max.Z;
                    if (Math.Abs(dd) < 1e-8f) { if (oo < lo || oo > hi) return false; continue; }
                    float a = (lo - oo) / dd, b = (hi - oo) / dd;
                    if (a > b) { var s = a; a = b; b = s; }
                    t0 = Math.Max(t0, a); t1 = Math.Min(t1, b);
                    if (t0 > t1) return false;
                }
                tEnter = t0; return true;
            }
        }

        private sealed class FixedNav : INavigation
        {
            private readonly Float3[] corners; private ulong next;
            public FixedNav(Float3[] corners) { this.corners = corners; }
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) => new PathTicket(new NavTicketId(++next), actor, navRevision);
            public void Cancel(PathTicket ticket) { }
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
            {
                for (int i = 0; i < corners.Length; i++) callerOwned[i] = corners[i];
                return new PathResult(PathStatus.Complete, corners.Length, corners.Length, ErrorCode.None);
            }
        }

        private static readonly Float3[] Leg = { new Float3(0, 0, 0), new Float3(60, 0, 0) };

        private static (List<uint> jumps, CombatAiDirector ai) WalkPast(float boxTop)
        {
            var world = new BoxWorld().Add(new Float3(4f, 0f, -2f), new Float3(4.6f, boxTop, 2f));
            var ai = new CombatAiDirector(world, new FixedNav(Leg), 1u << 8);
            ai.SetNavigationDestinations(new[] { new Float3(60, 0, 0) }, null);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), MathF.PI / 2, team: 1);
            var cmds = new InputCommand[1];
            var jumps = new List<uint>();
            for (uint t = 1; t <= 120; t++)
            {
                bot.Feet = new Float3(0.06f * t, 0, 0); // walking along the route through the obstacle line
                ai.Plan(t, null, cmds, 0);
                if ((cmds[0].Buttons & InputButtons.JumpPressed) != 0) jumps.Add(t);
            }
            return (jumps, ai);
        }

        [Fact]
        public void Travelling_bot_hops_a_low_obstacle_once_and_ignores_tall_walls()
        {
            var (low, lowAi) = WalkPast(0.5f);
            Assert.NotEmpty(low);
            Assert.True(lowAi.Counters.TraversalJumps >= 1);
            for (int i = 1; i < low.Count; i++) Assert.True(low[i] - low[i - 1] >= CombatAiDirector.JumpCooldownTicks);
            // The hop happens on the approach: feet within the probe reach of the box face (x = 4).
            float feetX = 0.06f * low[0];
            Assert.InRange(feetX, 4f - AiTuning.VaultProbeMeters - 0.1f, 4f);

            var (tall, tallAi) = WalkPast(1.5f);
            Assert.Equal(0, tallAi.Counters.TraversalJumps);
            Assert.Empty(tall);
        }

        private static List<uint> EngageHops(out CombatAiDirector ai, out List<uint> fireTicks)
        {
            ai = new CombatAiDirector(new BoxWorld(), new FixedNav(Leg), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1); // strafer (4 % 3 != 0)
            var enemies = new[] { new SenseTarget(new EntityId(99), new Float3(0, CombatAiDirector.StandingAimHeight, 20f), 2) };
            var cmds = new InputCommand[1];
            var hops = new List<uint>();
            fireTicks = new List<uint>();
            for (uint t = 1; t <= 3000; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                var b = cmds[0].Buttons;
                if ((b & InputButtons.JumpPressed) != 0)
                {
                    hops.Add(t);
                    Assert.Equal(InputButtons.None, b & (InputButtons.FireHeld | InputButtons.FirePressed | InputButtons.ReloadPressed | InputButtons.CrouchHeld));
                }
                if ((b & InputButtons.FireHeld) != 0) fireTicks.Add(t);
                bot.Feet = new Float3(bot.Feet.X + cmds[0].MoveX * 0.05f, 0, 0);
            }
            return hops;
        }

        [Fact]
        public void Mid_range_strafer_sometimes_hops_on_reversal_deterministically_with_cooldown()
        {
            var hops = EngageHops(out var ai, out var fire);
            Assert.NotEmpty(fire);
            Assert.NotEmpty(hops);
            Assert.Equal(hops.Count, ai.Counters.CombatHops);
            for (int i = 1; i < hops.Count; i++) Assert.True(hops[i] - hops[i - 1] >= AiTuning.CombatHopCooldownTicks);
            Assert.Equal(hops, EngageHops(out _, out _));
        }

        [Fact]
        public void Long_range_engagements_never_hop()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new FixedNav(Leg), 1u << 8);
            ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var enemies = new[] { new SenseTarget(new EntityId(99), new Float3(0, CombatAiDirector.StandingAimHeight, 40f), 2) };
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 1500; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                Assert.Equal(InputButtons.None, cmds[0].Buttons & InputButtons.JumpPressed);
            }
            Assert.Equal(0, ai.Counters.CombatHops);
        }

        private static List<uint> SlideRequests(AiState state, int ticks)
        {
            var ai = new CombatAiDirector(new BoxWorld(), new FixedNav(new[] { new Float3(0, 0, 0), new Float3(500, 0, 0) }), 1u << 8);
            ai.SetCoverSites(new[] { new CoverSite(1, new Float3(500, 0, 0), new Float3(500, 0, 2)) });
            ai.SetNavigationDestinations(new[] { new Float3(500, 0, 0) }, null);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), MathF.PI / 2, team: 1);
            bot.State = state;
            var cmds = new InputCommand[1];
            var requests = new List<uint>();
            for (uint t = 1; t <= ticks; t++)
            {
                bot.Feet = new Float3(0.1f * t, 0, 0); // 6 m/s along the leg
                ai.Plan(t, null, cmds, 0);
                if (ai.WantsSlide(new EntityId(1))) requests.Add(t);
            }
            return requests;
        }

        [Fact]
        public void Sprinting_bots_slide_sooner_and_again_after_the_shorter_cooldown()
        {
            var cover = SlideRequests(AiState.SeekCover, 800);
            Assert.True(cover.Count >= 2, "slides " + cover.Count);
            Assert.InRange(cover[0], CombatAiDirector.SlideMinSprintTicks, 30u + 40u);
            for (int i = 1; i < cover.Count; i++)
                Assert.True(cover[i] - cover[i - 1] >= CombatAiDirector.SlideBotCooldownBaseTicks);
        }
    }
}
