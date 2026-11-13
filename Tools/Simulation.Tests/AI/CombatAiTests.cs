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
            private ulong next;
            public uint Revision { get; set; } = 1;
            public int PendingCount => open.Count;
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
            {
                var ticket = new PathTicket(new NavTicketId(checked(++next)), actor, navRevision);
                open[ticket.Id] = ticket;
                return ticket;
            }
            public void Cancel(PathTicket ticket) { open.Remove(ticket.Id); }
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
            {
                if (!open.Remove(ticket.Id)) return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
                if (ticket.NavRevision != Revision) return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
                callerOwned[0] = new Float3(0, 0, 0);
                callerOwned[1] = new Float3(1, 0, 1);
                return new PathResult(PathStatus.Complete, 2, 2, ErrorCode.None);
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
        public void Reload_exits_without_loop_and_path_budget_holds()
        {
            var world = new OcclusionWorld(occlude: false);
            var nav = new FakeNav();
            var ai = new CombatAiDirector(world, nav, 1u << 8);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0);
            bot.MagAmmo = 0;
            var commands = new InputCommand[1];
            ai.Plan(1, null, commands, 0);
            Assert.NotEqual(AiState.Reload, bot.State);
            Assert.Equal(30, bot.MagAmmo);
            for (int t = 2; t < 10; t++) ai.Plan((uint)t, null, commands, 0);
            Assert.True(ai.Counters.PathsRequestedThisTick <= 2);
        }

        [Fact]
        public void Shipping_policy_rejects_fake_world_and_nav()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CompositionPolicy.RequireReal(new OcclusionWorld(false), new FakeNav()));
        }
    }
}
