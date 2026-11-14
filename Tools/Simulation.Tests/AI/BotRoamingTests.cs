using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.AI;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>
    /// T070 capture finding (2026-11-14): product bots never left spawn, never fired and retried a failing path every
    /// tick. These pin the roaming behaviour that makes opposing teams meet.
    /// </summary>
    public sealed class BotRoamingTests
    {
        private sealed class OpenWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
        }

        /// <summary>Records destinations; answers every poll with either fixed corners or a failure.</summary>
        private sealed class ScriptedNav : INavigation
        {
            private readonly Float3[]? corners;
            private ulong next;
            public readonly List<Float3> Requested = new List<Float3>();
            public ScriptedNav(Float3[]? corners) { this.corners = corners; }
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
            {
                Requested.Add(destination);
                return new PathTicket(new NavTicketId(++next), actor, navRevision);
            }
            public void Cancel(PathTicket ticket) { }
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
            {
                if (corners == null) return new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
                for (int i = 0; i < corners.Length; i++) callerOwned[i] = corners[i];
                return new PathResult(PathStatus.Complete, corners.Length, corners.Length, ErrorCode.None);
            }
        }

        [Fact]
        public void Failed_path_backs_off_and_rotates_to_another_destination()
        {
            var nav = new ScriptedNav(null);
            var ai = new CombatAiDirector(new OpenWorld(), nav, 1u << 8);
            var a = new Float3(10, 0, 0);
            var b = new Float3(-10, 0, 0);
            ai.SetNavigationDestinations(new[] { a, b }, null);
            ai.RegisterBot(new EntityId(2), new Float3(0, 0, 0), 0, team: 1);
            var commands = new InputCommand[1];
            for (uint t = 1; t <= 60; t++) ai.Plan(t, null, commands, 0);

            // One request, one failure, then a 30-tick back-off before the next attempt (not one per tick).
            Assert.InRange(nav.Requested.Count, 2, 3);
            Assert.True(ai.Counters.PathsFailed >= 1);
            Assert.NotEqual(nav.Requested[0].X, nav.Requested[1].X);
        }

        [Fact]
        public void Moving_bot_without_a_target_faces_its_route()
        {
            var nav = new ScriptedNav(new[] { new Float3(0, 0, 0), new Float3(8, 0, 0) });
            var ai = new CombatAiDirector(new OpenWorld(), nav, 1u << 8);
            ai.SetNavigationDestinations(new[] { new Float3(8, 0, 0) }, null);
            ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), yaw: 0, team: 1);
            var commands = new InputCommand[1];
            ai.Plan(1, null, commands, 0);
            ai.Plan(2, null, commands, 0);

            Assert.Equal((float)(Math.PI / 2), commands[0].AimYaw, 3);
            Assert.True(commands[0].MoveY > 0.99f);
        }

        [Fact]
        public void Walking_due_south_keeps_yaw_inside_the_contract_interval()
        {
            // Atan2(0, -z) is float π, outside the C0 [-π, π) yaw range; building the command used to throw.
            var nav = new ScriptedNav(new[] { new Float3(0, 0, 0), new Float3(0, 0, -8) });
            var ai = new CombatAiDirector(new OpenWorld(), nav, 1u << 8);
            ai.SetNavigationDestinations(new[] { new Float3(0, 0, -8) }, null);
            ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), yaw: 0, team: 1);
            var commands = new InputCommand[1];
            ai.Plan(1, null, commands, 0);
            ai.Plan(2, null, commands, 0);

            Assert.True(commands[0].AimYaw >= -Math.PI && commands[0].AimYaw < Math.PI);
            Assert.True(commands[0].MoveY > 0.99f);
        }

        [Fact]
        public void Seek_cover_without_free_sites_moves_on_to_combat_destinations()
        {
            var combat = new Float3(30, 0, 30);
            var nav = new ScriptedNav(new[] { new Float3(0, 0, 0), combat });
            var ai = new CombatAiDirector(new OpenWorld(), nav, 1u << 8);
            ai.SetNavigationDestinations(null, new[] { combat });
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            bot.State = AiState.SeekCover;
            var commands = new InputCommand[1];
            ai.Plan(1, null, commands, 0);
            ai.Plan(2, null, commands, 0);

            Assert.Equal(AiState.Search, bot.State);
            Assert.Contains(combat, nav.Requested);
        }
    }
}
