#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.Simulation.AI;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    public sealed class CombatAiTests
    {
        private sealed class OcclusionWorld : IWorldQueries, IFakeAdapter
        {
            private readonly bool occlude;
            public OcclusionWorld(bool occlude) { this.occlude = occlude; }
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
            {
                if (!occlude) return new RayHit(QueryStatus.NoHit);
                var hit = new QueryHit(maxMeters * 0.5f,
                    new Float3(origin.X + unitDirection.X * maxMeters * 0.5f, origin.Y, origin.Z + unitDirection.Z * maxMeters * 0.5f),
                    new Float3(0, 1, 0), default, new SurfaceId(1), 42, 1, 1);
                return new RayHit(QueryStatus.Hit, hit);
            }
        }

        private sealed class FakeNav : INavigation, IFakeAdapter
        {
            private readonly Dictionary<NavTicketId, PathTicket> open = new Dictionary<NavTicketId, PathTicket>();
            private readonly Float3[] corners;
            private ulong next;
            public uint Revision { get; set; } = 1;
            public int PendingCount => open.Count;
            public int CancelCount { get; private set; }

            public FakeNav(params Float3[] corners)
            {
                this.corners = corners.Length == 0
                    ? new[] { new Float3(0, 0, 0), new Float3(1, 0, 1) }
                    : corners;
            }

            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
            {
                var ticket = new PathTicket(new NavTicketId(checked(++next)), actor, navRevision);
                open[ticket.Id] = ticket;
                return ticket;
            }
            public void Cancel(PathTicket ticket)
            {
                CancelCount++;
                open.Remove(ticket.Id);
            }
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
            {
                if (!open.Remove(ticket.Id)) return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
                if (ticket.NavRevision != Revision) return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
                int written = Math.Min(corners.Length, callerOwned.Capacity);
                for (int i = 0; i < written; i++) callerOwned[i] = corners[i];
                return new PathResult(PathStatus.Complete, written, written, ErrorCode.None);
            }
        }

        [Fact]
        public void Occluded_target_is_not_acquired_by_vision()
        {
            var world = new OcclusionWorld(occlude: true);
            var nav = new FakeNav();
            var ai = new CombatAiDirector(world, nav, visionMask: 1u << 8);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), yaw: 0);
            var commands = new InputCommand[8];
            ai.Plan(1, new SenseTarget(new EntityId(99), new Float3(0, 1.65f, 20)), commands, 0);
            Assert.False(bot.HasVisibleTarget);
            Assert.True(ai.Counters.VisionRaysThisTick <= PerceptionSystem.MaxVisionRaysPerTick);
            Assert.Equal(InputButtons.None, commands[0].Buttons & (InputButtons.FireHeld | InputButtons.FirePressed));
        }

        [Fact]
        public void Eight_bots_obey_vision_and_path_budgets()
        {
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav();
            var ai = new CombatAiDirector(world, nav, visionMask: 1u << 8);
            for (int i = 0; i < 8; i++)
                ai.RegisterBot(new EntityId((ulong)(i + 1)), new Float3(i * 2, 0, 0), 0);
            var commands = new InputCommand[8];
            int written = ai.Plan(10, new SenseTarget(new EntityId(99), new Float3(0, 1.65f, 30)), commands, 0);
            Assert.Equal(8, written);
            Assert.True(ai.Counters.VisionRaysThisTick <= 16);
            Assert.True(ai.Counters.PathsRequestedThisTick <= 2);
        }

        [Fact]
        public void Cover_reservations_release_on_death_and_two_bots_do_not_share()
        {
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav();
            var ai = new CombatAiDirector(world, nav, 1u << 8);
            ai.SetCoverSites(new[]
            {
                new CoverSite(10, new Float3(1, 0, 1), new Float3(0, 0, 1)),
                new CoverSite(11, new Float3(2, 0, 2), new Float3(1, 0, 2))
            });
            var a = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0);
            var b = ai.RegisterBot(new EntityId(2), new Float3(3, 0, 0), 0);
            Assert.True(ai.CoverBoard.TryClaim(10, a.Self));
            Assert.False(ai.CoverBoard.TryClaim(10, b.Self));
            Assert.True(ai.CoverBoard.TryClaim(11, b.Self));
            a.CoverReservation = 10;
            ai.NotifyDeath(a.Self);
            Assert.False(ai.CoverBoard.IsClaimed(10));
            Assert.True(ai.CoverBoard.IsClaimed(11));
            Assert.Equal(AiState.Dead, a.State);
        }

        [Fact]
        public void Reload_issues_command_without_blackboard_ammo_fabrication()
        {
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav();
            var ai = new CombatAiDirector(world, nav, 1u << 8);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0);
            bot.MagAmmo = 0;
            bot.ReserveAmmo = 90;
            var commands = new InputCommand[1];
            ai.Plan(1, null, commands, 0);
            Assert.Equal(AiState.Reload, bot.State);
            Assert.Equal(0, bot.MagAmmo);
            Assert.Equal(InputButtons.ReloadPressed, commands[0].Buttons & InputButtons.ReloadPressed);

            // Authority replenishes magazine; AI observes and leaves Reload without inventing ammo.
            bot.MagAmmo = 30;
            bot.ReserveAmmo = 60;
            ai.Plan(2, null, commands, 0);
            Assert.NotEqual(AiState.Reload, bot.State);
            Assert.Equal(30, bot.MagAmmo);
            Assert.True(ai.Counters.PathsRequestedThisTick <= 2);
        }

        [Fact]
        public void Allied_and_dead_candidates_never_generate_fire_intent()
        {
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav();
            var ai = new CombatAiDirector(world, nav, 1u << 8);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), yaw: 0, team: 1);
            var candidates = new[]
            {
                new SenseTarget(new EntityId(2), new Float3(0, 1.65f, 10), team: 1, alive: true),
                new SenseTarget(new EntityId(3), new Float3(0, 1.65f, 12), team: 2, alive: false),
                new SenseTarget(new EntityId(4), new Float3(0, 1.65f, 15), team: 2, alive: true)
            };
            var commands = new InputCommand[2];
            // tick+self ≡ 0 (mod 3) so the living ally is visited before the living enemy.
            ai.PlanEnemies(2, candidates, candidates.Length, commands, 0);
            Assert.True(bot.HasVisibleTarget);
            Assert.Equal(new EntityId(4), bot.VisibleTargetId);
            Assert.True(ai.Counters.AlliedCandidatesSkipped >= 1);
            uint ready = bot.ReactionReadyTick;
            Assert.True(ready > 2);

            // T082: no trigger before the reaction delay; once ready, the bot fires as soon as its bounded
            // turn has settled on its (seeded, imperfect) aim point.
            bool firedEarly = false, fired = false;
            for (uint t = 3; t < ready + 40; t++)
            {
                ai.PlanEnemies(t, candidates, candidates.Length, commands, 0);
                bool trigger = (commands[0].Buttons & (InputButtons.FireHeld | InputButtons.FirePressed)) ==
                    (InputButtons.FireHeld | InputButtons.FirePressed);
                if (t < ready) firedEarly |= trigger;
                else fired |= trigger;
                Assert.Equal(new EntityId(4), bot.TargetId);
            }
            Assert.False(firedEarly);
            Assert.True(fired);
            Assert.Equal(AiState.Engage, bot.State);
            ready += 40; // continue after the loop's last planned tick

            // Ally-only roster: no acquisition, no fire.
            ai.NotifyDeath(bot.Self);
            var allyBot = ai.RegisterBot(new EntityId(10), new Float3(0, 0, 0), 0, team: 1);
            var alliesOnly = new[]
            {
                new SenseTarget(new EntityId(11), new Float3(0, 1.65f, 8), team: 1, alive: true)
            };
            ai.PlanEnemies(ready + 1, alliesOnly, alliesOnly.Length, commands, 0);
            Assert.False(allyBot.HasVisibleTarget);
            Assert.Equal(InputButtons.None, commands[0].Buttons & (InputButtons.FireHeld | InputButtons.FirePressed));
        }

        [Fact]
        public void Occlusion_clears_stale_fire_intent()
        {
            var nav = new FakeNav();
            var occluded = new CombatAiDirector(new OcclusionWorld(occlude: true), nav, 1u << 8);
            var bot = occluded.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            bot.State = AiState.Engage;
            bot.Confidence = 1f;
            bot.ReactionReadyTick = 0;
            bot.HasVisibleTarget = true;
            bot.VisibleTargetId = new EntityId(99);
            bot.VisibleTarget = new Float3(0, 1.65f, 20);
            var commands = new InputCommand[1];
            occluded.PlanEnemies(50, new[]
            {
                new SenseTarget(new EntityId(99), new Float3(0, 1.65f, 20), team: 2, alive: true)
            }, 1, commands, 0);
            Assert.False(bot.HasVisibleTarget);
            Assert.Equal(InputButtons.None, commands[0].Buttons & (InputButtons.FireHeld | InputButtons.FirePressed));
        }

        [Fact]
        public void Movement_follows_path_corners_not_direct_goal()
        {
            // Direct goal is +X; first corner is +Z — command must chase the corner.
            var corners = new[]
            {
                new Float3(0, 0, 5),
                new Float3(10, 0, 5),
                new Float3(10, 0, 0)
            };
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav(corners);
            var ai = new CombatAiDirector(world, nav, 1u << 8);
            ai.SetNavigationDestinations(new[] { new Float3(10, 0, 0) }, null);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), yaw: 0);
            var commands = new InputCommand[1];
            ai.Plan(1, null, commands, 0);
            Assert.Equal(1, ai.Counters.PathsRequestedThisTick);
            ai.Plan(2, null, commands, 0);
            Assert.True(bot.PathCornerCount >= 2);
            Assert.Equal(0, bot.PathCornerIndex);
            // Facing +Z (yaw 0): forward is +Z toward first corner => MoveY > 0, MoveX ~ 0.
            Assert.True(commands[0].MoveY > 0.5f);
            Assert.True(Math.Abs(commands[0].MoveX) < 0.25f);
        }

        [Fact]
        public void Eleven_bots_obey_global_budgets_and_fair_sense_within_twelve_ticks()
        {
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav();
            var ai = new CombatAiDirector(world, nav, 1u << 8);
            for (int i = 0; i < CombatAiDirector.MaxBots; i++)
                ai.RegisterBot(new EntityId((ulong)(i + 1)), new Float3(i * 2, 0, 0), 0, team: (byte)(i < 5 ? 1 : 2));
            var enemy = new SenseTarget(new EntityId(99), new Float3(0, 1.65f, 30), team: 2, alive: true);
            var commands = new InputCommand[CombatAiDirector.MaxBots];
            for (uint t = 1; t <= 12; t++)
            {
                ai.PlanEnemies(t, new[] { enemy }, 1, commands, 0);
                Assert.True(ai.Counters.VisionRaysThisTick <= PerceptionSystem.MaxVisionRaysPerTick);
                Assert.True(ai.Counters.PathsRequestedThisTick <= CombatAiDirector.MaxNewPathsPerTick);
            }
            for (int i = 0; i < ai.BotCount; i++)
            {
                var board = ai.GetBoard(i);
                Assert.True(board.LastSenseOpportunityTick > 0);
                Assert.True(unchecked(12u - board.LastSenseOpportunityTick) < 12u || board.LastSenseOpportunityTick <= 12u);
                Assert.True(board.LastSenseOpportunityTick <= 12u);
            }
        }

        [Fact]
        public void ReplaceLife_resets_dead_reaction_target_and_sequence_epoch()
        {
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav();
            var planner = new TeamBotPlanner(world, nav, 1u << 8);
            planner.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            var board = planner.Director.GetBoard(0);
            board.State = AiState.Engage;
            board.HasVisibleTarget = true;
            board.VisibleTargetId = new EntityId(99);
            board.VisibleTarget = new Float3(1, 1, 1);
            board.ReactionReadyTick = 40;
            board.Confidence = 1f;
            Assert.True(planner.Director.CoverBoard.TryClaim(7, board.Self));
            board.CoverReservation = 7;
            board.PathTicket = nav.RequestPath(board.Self, new Float3(5, 0, 5), 1);

            planner.ResetLife(new EntityId(1));
            Assert.Equal(AiState.Dead, board.State);
            Assert.False(planner.Director.CoverBoard.IsClaimed(7));
            Assert.Null(board.PathTicket);
            Assert.Equal(0u, planner.GetSequence(new EntityId(1)));

            Assert.True(planner.TryReplaceLife(new EntityId(1), new EntityId(50), new Float3(2, 0, 2), 1.2f, team: 1));
            Assert.Equal(new EntityId(50), board.Self);
            Assert.Equal(AiState.Patrol, board.State);
            Assert.True(board.Alive);
            Assert.False(board.HasVisibleTarget);
            Assert.Null(board.VisibleTargetId);
            Assert.Equal(0u, board.ReactionReadyTick);
            Assert.Equal(0f, board.Confidence);
            Assert.Equal(0u, planner.GetSequence(new EntityId(50)));
            Assert.True(nav.CancelCount >= 1);
        }

        [Fact]
        public void CancelAll_releases_paths_and_cover()
        {
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav();
            var ai = new CombatAiDirector(world, nav, 1u << 8);
            ai.SetCoverSites(new[] { new CoverSite(3, new Float3(1, 0, 1), new Float3(0, 0, 1)) });
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0);
            Assert.True(ai.CoverBoard.TryClaim(3, bot.Self));
            bot.CoverReservation = 3;
            bot.PathTicket = nav.RequestPath(bot.Self, new Float3(4, 0, 4), 1);
            ai.CancelAll();
            Assert.False(ai.CoverBoard.IsClaimed(3));
            Assert.Null(bot.PathTicket);
            Assert.Equal(0, bot.PathCornerCount);
            Assert.True(nav.CancelCount >= 1);
        }

        [Fact]
        public void Shipping_policy_rejects_fake_world_and_nav()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CompositionPolicy.RequireReal(new OcclusionWorld(false), new FakeNav()));
        }
    }
}
