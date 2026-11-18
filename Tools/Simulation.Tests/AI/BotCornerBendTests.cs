#nullable enable
using System;
using Shooter.Contracts;
using Shooter.VectorMath;
using Shooter.Simulation;
using Shooter.Simulation.AI;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>ADR-114: the ADR-112 corner bend is taken only when the bent line is clear; a wall corner keeps the leg.</summary>
    public sealed class BotCornerBendTests
    {
        /// <summary>Every horizontal ray heading into +Z (the inside of the turn) hits a wall; everything else is open.</summary>
        private sealed class WallInsideTurn : IWorldQueries
        {
            public readonly bool Blocked;
            public int BendRays;
            public WallInsideTurn(bool blocked) { Blocked = blocked; }
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
            {
                if (unitDirection.Y != 0f || unitDirection.Z <= 0.05f) return new RayHit(QueryStatus.NoHit);
                BendRays++;
                if (!Blocked) return new RayHit(QueryStatus.NoHit);
                var point = new Float3(origin.X + unitDirection.X * 0.3f, origin.Y, origin.Z + unitDirection.Z * 0.3f);
                return new RayHit(QueryStatus.Hit, new QueryHit(0.3f, point, new Float3(0f, 0f, -1f), default, new SurfaceId(1), 1, 1, 1));
            }
        }

        private sealed class FixedNav : INavigation
        {
            private readonly Float3[] corners;
            private ulong next;
            public FixedNav(Float3[] corners) { this.corners = corners; }
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) =>
                new PathTicket(new NavTicketId(++next), actor, navRevision);
            public void Cancel(PathTicket ticket) { }
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
            {
                for (int i = 0; i < corners.Length; i++) callerOwned[i] = corners[i];
                return new PathResult(PathStatus.Complete, corners.Length, corners.Length, ErrorCode.None);
            }
        }

        private static AiBlackboard Run(WallInsideTurn world)
        {
            // The bot stands 0.6 m before a right-angle corner: the route turns from +X onto +Z.
            var corners = new[] { new Float3(0.6f, 0f, 0f), new Float3(0.6f, 0f, 12f) };
            var ai = new CombatAiDirector(world, new FixedNav(corners), 1u << 8);
            ai.SetNavigationDestinations(new[] { corners[1] }, null);
            var board = ai.RegisterBot(new EntityId(1), new Float3(0f, 0f, 0f), MathF.PI * 0.5f, team: 1);
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 20; t++) ai.Plan(t, null, cmds, 0);
            return board;
        }

        [Fact]
        public void Open_corner_bends_into_the_next_leg()
        {
            var world = new WallInsideTurn(blocked: false);
            var board = Run(world);
            Assert.True(board.SmoothMoveZ > 0.2f, "bend toward +Z expected, got z=" + board.SmoothMoveZ);
            Assert.True(board.SmoothMoveX > 0.5f);
            Assert.True(world.BendRays > 0);
        }

        [Fact]
        public void Blocked_corner_follows_the_leg_instead_of_cutting_the_wall()
        {
            var board = Run(new WallInsideTurn(blocked: true));
            Assert.True(Math.Abs(board.SmoothMoveZ) < 0.02f, "no bend into the wall expected, got z=" + board.SmoothMoveZ);
            Assert.True(board.SmoothMoveX > 0.9f);
        }
    }
}
