#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.World.Navigation;
using Xunit;

namespace Shooter.Generation.Tests
{
    public sealed class NavigationTests
    {
        [Fact]
        public void Settings_clearance_is_radius_plus_5cm_and_hash_stable()
        {
            var settings = NavSettings.SliceDefault;
            Assert.Equal(0.37f, settings.RequiredClearance, 3);
            Assert.Equal(settings.Hash(), NavSettings.SliceDefault.Hash());
        }

        [Fact]
        public void Compile_marks_unreachable_objective_and_reports_clearance()
        {
            var settings = NavSettings.SliceDefault;
            var spawn = new Float3(0, 0, 0);
            var objective = new Float3(10, 0, 0);
            var bots = new[] { new Float3(5, 0, 0) };
            var covers = new List<(ulong, Float3, float, float, float, Float3, Float3, bool)>
            {
                (1UL, new Float3(2, 1, 0), 1, 2, 1, new Float3(1, 0, 0), new Float3(1, 0, 0), true)
            };

            bool Path(Float3 a, Float3 b, List<Float3> corners)
            {
                corners.Clear();
                corners.Add(a); corners.Add(b);
                return true;
            }
            bool Blocked(float x, float z, float clearance) => false;

            var result = NavCompiler.Compile(1, new ContentId(new byte[32]), settings, spawn, objective, bots, covers, Path, Blocked);
            Assert.True(result.AllObjectivesReachable);
            Assert.True(result.MinClearanceMeters >= settings.RequiredClearance - 1e-4f);
            Assert.True(result.Covers.Count == 1);
        }

        [Fact]
        public void Gameplay_nav_hash_ignores_visual_profile_argument()
        {
            var settings = NavSettings.SliceDefault;
            var route = new List<Float3> { new Float3(0, 0, 0), new Float3(1, 0, 1) };
            var covers = new List<NavCoverCandidate> { new NavCoverCandidate(9, new Float3(0, 0, 0), new Float3(1, 0, 0), new Float3(1, 0, 0), true) };
            var collision = new ContentId(new byte[32]);
            var a = NavCompiler.HashNav(1, collision, settings.Hash(), route, covers, "Low");
            var b = NavCompiler.HashNav(1, collision, settings.Hash(), route, covers, "High");
            Assert.Equal(a, b);
        }

        [Fact]
        public void Revisioned_adapter_rejects_stale_and_releases_cancelled()
        {
            var positions = new Dictionary<ulong, Float3> { [1] = new Float3(0, 0, 0) };
            var adapter = new RevisionedPathAdapter(1, id => positions.TryGetValue(id.Value, out var p) ? p : (Float3?)null,
                (a, b, corners) =>
                {
                    corners!.Clear();
                    corners.Add(a); corners.Add(b);
                    return true;
                });
            var actor = new EntityId(1);
            var ticket = adapter.RequestPath(actor, new Float3(5, 0, 0), 1);
            adapter.Cancel(ticket);
            var buffer = new PathBuffer(16);
            var cancelled = adapter.Poll(ticket, buffer);
            Assert.Equal(PathStatus.Cancelled, cancelled.Status);

            var staleTicket = adapter.RequestPath(actor, new Float3(5, 0, 0), 1);
            adapter.SetRevision(2);
            var stale = adapter.Poll(staleTicket, buffer);
            Assert.Equal(PathStatus.Stale, stale.Status);
            Assert.True(adapter.StaleRejects >= 1);

            var ok = adapter.RequestPath(actor, new Float3(3, 0, 0), 2);
            var complete = adapter.Poll(ok, buffer);
            Assert.Equal(PathStatus.Complete, complete.Status);
            Assert.Equal(0, adapter.PendingCount);
        }
    }
}
