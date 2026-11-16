#nullable enable
using System.Linq;
using Shooter.Game.Contracts;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Scoreboard;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    public sealed class ResultsAndShellMotionTests
    {
        private static ScoreboardRow Row(uint id, string name, byte team, uint score, uint kills, uint deaths, bool local = false) =>
            new ScoreboardRow(new ParticipantId(id), name, team, local, score, kills, deaths, local, true);

        [Fact]
        public void Awards_pick_mvp_fragger_survivor_and_efficiency_with_first_row_tie_break()
        {
            // Authority order: score, then kills.
            var rows = new[]
            {
                Row(1, "Nora", 1, 900, 9, 4),
                Row(2, "Cole", 2, 900, 12, 6),
                Row(3, "You", 1, 500, 5, 1, local: true),
                Row(4, "Kai", 1, 0, 0, 0),
            };
            var awards = ResultsPresentation.Awards(rows);
            Assert.Equal(4, awards.Count);
            Assert.Equal(ResultsAwardKind.Mvp, awards[0].Kind);
            Assert.Equal("Nora", awards[0].DisplayName); // tie on score keeps the earlier row
            Assert.Equal("Cole", awards.Single(a => a.Kind == ResultsAwardKind.TopFragger).DisplayName);
            var survivor = awards.Single(a => a.Kind == ResultsAwardKind.Survivor);
            Assert.Equal("You", survivor.DisplayName); // Kai has no eliminations, so is not eligible
            Assert.True(survivor.IsLocal);
            var sharp = awards.Single(a => a.Kind == ResultsAwardKind.Efficiency);
            Assert.Equal("You", sharp.DisplayName);
            Assert.Equal("5.00 K/D", sharp.Stat);
            Assert.All(awards, a => Assert.InRange(a.Tier, 1, 4));
        }

        [Fact]
        public void Preview_binding_sorts_like_authority_results()
        {
            var table = new ScoreboardTable();
            table.BindPreview(new[] { Row(1, "Low", 1, 100, 1, 1), Row(2, "High", 2, 900, 9, 2) }, 150, 120, MatchOutcome.Team1, FinishReason.ScoreLimit);
            Assert.Equal("High", table.Rows[0].DisplayName);
            Assert.Equal(150u, table.Team1Kills);
            Assert.Equal("Victory", table.LocalOutcomeLabel(1));
        }

        [Fact]
        public void A_scoreless_match_has_no_awards()
        {
            var rows = new[] { Row(1, "A", 1, 0, 0, 0), Row(2, "B", 2, 0, 0, 3) };
            Assert.Empty(ResultsPresentation.Awards(rows));
        }

        [Fact]
        public void Ratio_formatting_is_invariant()
        {
            Assert.Equal("1.67", ResultsPresentation.FormatRatio(5 / 3.0));
            Assert.Equal("2.00", ResultsPresentation.FormatRatio(2));
        }

        [Fact]
        public void Debrief_timeline_counts_up_and_reduced_motion_shows_final_state()
        {
            Assert.Equal(0, ResultsPresentation.TeamScore(150, 0f, false));
            Assert.Equal(150, ResultsPresentation.TeamScore(150, 5f, false));
            Assert.Equal(150, ResultsPresentation.TeamScore(150, 0f, true));
            Assert.Equal(0f, ResultsPresentation.BannerReveal(0f, false));
            Assert.Equal(1f, ResultsPresentation.BannerReveal(0f, true));
            Assert.Equal(1f, ResultsPresentation.TablesAlpha(0f, true));
            Assert.True(ResultsPresentation.AwardElapsed(1f, 1) < ResultsPresentation.AwardElapsed(1f, 0));
        }

        [Fact]
        public void Screen_enter_veil_fades_out_and_never_touches_gameplay()
        {
            Assert.Equal(1f, ShellMotion.EnterVeil(ShellScreen.Main, 0f, false), 4);
            Assert.Equal(0f, ShellMotion.EnterVeil(ShellScreen.Main, 1f, false), 4);
            Assert.Equal(0f, ShellMotion.EnterVeil(ShellScreen.Playing, 0f, false));
            Assert.Equal(0f, ShellMotion.EnterVeil(ShellScreen.Paused, 0f, false));
            Assert.Equal(0f, ShellMotion.EnterVeil(ShellScreen.ConfirmLeave, 0f, false));
            Assert.Equal(1f, ShellMotion.EnterVeil(ShellScreen.Results, 0f, false), 4);
            Assert.Equal(0f, ShellMotion.EnterVeil(ShellScreen.Main, ShellMotion.EnterReducedSeconds, true), 4);
            Assert.True(ShellMotion.EnterOffset(ShellScreen.Main, 0f, false) > 0f);
            Assert.Equal(0f, ShellMotion.EnterOffset(ShellScreen.Main, 0f, true));
            Assert.Equal(0f, ShellMotion.EnterOffset(ShellScreen.Paused, 0f, false));
            Assert.Equal(0f, ShellMotion.EnterOffset(ShellScreen.Main, 2f, false), 4);
        }

        [Fact]
        public void Camera_drift_is_bounded_settles_its_push_in_and_stops_under_reduced_motion()
        {
            for (int i = 0; i < 2000; i++)
            {
                var d = ShellMotion.CameraDrift(i * 0.37, i * 0.01f, false);
                Assert.True(System.Math.Abs(d.X) + System.Math.Abs(d.Y) + System.Math.Abs(d.Z) <= ShellMotion.MaxDriftMeters);
            }
            Assert.Equal(-0.35f, ShellMotion.CameraDrift(0, 0f, false).Z, 4);
            Assert.Equal(0f, ShellMotion.CameraDrift(0, 10f, false).Z, 4);
            Assert.Equal((0f, 0f, 0f), ShellMotion.CameraDrift(12.3, 0.5f, true));
            Assert.Equal((0f, 0f), ShellMotion.PointerParallax(1f, -1f, true));
            Assert.Equal(0.04f, ShellMotion.PointerParallax(5f, 0f, false).X, 5);
        }
    }
}
