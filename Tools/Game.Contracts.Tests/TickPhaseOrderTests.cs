#nullable enable
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Content.Maps;
using Shooter.Game.Session;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>T195: the session's live phase list equals the documented offline tick order.</summary>
    public sealed class TickPhaseOrderTests
    {
        private static readonly string[] Documented =
        {
            "ScheduleRespawns", "PlanBots", "MergeCommands", "StepHarness", "SyncLifecycle", "ResolveCombatEvents", "SettleDeaths", "EndOfTick"
        };

        [Fact]
        public void Session_phases_run_in_the_documented_order()
        {
            using var session = new OfflineGameSession(StaticMapCatalog.CreateReleaseSeed(),
                (_, __) => new FakeMapRuntime(new PreMatchCountdownTests.FloorWorld(), new PreMatchCountdownTests.NoNav()),
                allowFakeMapRuntime: true);
            Assert.Equal(Documented, session.TickPhaseNames.ToArray());
        }

        [Fact]
        public void Simulation_protocol_lists_the_same_phases()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "toolchain-lock.json"))) dir = dir.Parent;
            Assert.NotNull(dir);
            string root = dir!.FullName;
            string page = File.ReadAllText(Path.Combine(root, "docs/simulation-protocol.md"));
            int start = page.IndexOf("## Offline session phases", System.StringComparison.Ordinal);
            Assert.True(start >= 0);
            var listed = Regex.Matches(page.Substring(start), @"^\d+\. `([A-Za-z]+)`", RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            Assert.Equal(Documented, listed);
        }
    }
}
