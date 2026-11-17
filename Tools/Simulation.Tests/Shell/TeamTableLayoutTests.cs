#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Scoreboard;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    /// <summary>15v15 (ADR-101) scoreboard/debrief tables stay inside their viewport and scroll by whole rows.</summary>
    public sealed class TeamTableLayoutTests
    {
        // Debrief at 720p, 100% UI scale: Safe = (64, 36, 1152, 648); cards end at y = 340, table to yMax - 64.
        private const float DebriefTableHeight = 684f - 64f - 340f;

        [Fact]
        public void Five_v_five_debrief_keeps_its_design_row_height_without_scrolling()
        {
            var m = TeamTableMetrics.Fit(DebriefTableHeight - 78f, 5, 36f, 26f, 6);
            Assert.Equal((DebriefTableHeight - 78f) / 6f, m.RowHeight, 3);
            Assert.False(m.NeedsScroll);
            Assert.Equal(5, m.VisibleRows);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        [InlineData(15)]
        public void Large_team_debrief_rows_stay_inside_the_viewport_and_scroll(int perTeam)
        {
            float viewport = DebriefTableHeight - 78f;
            var m = TeamTableMetrics.Fit(viewport, perTeam, 36f, 26f, 6);
            Assert.True(m.RowHeight >= 26f);
            Assert.True(m.UsedHeight <= viewport + 1e-3f, $"rows {m.UsedHeight} > viewport {viewport}");
            Assert.True(m.NeedsScroll);
            Assert.Equal(perTeam - m.VisibleRows, m.MaxFirstRow);
            // Scrolling to the end shows the last row and never runs past it.
            int first = m.ClampFirstRow(int.MaxValue);
            Assert.True(m.IsVisible(first, perTeam - 1));
            Assert.False(m.IsVisible(first, perTeam));
        }

        [Fact]
        public void Tiny_viewport_still_draws_one_row_and_scrolls()
        {
            var m = TeamTableMetrics.Fit(-14f, 15, 36f, 26f, 6);
            Assert.Equal(1, m.VisibleRows);
            Assert.True(m.NeedsScroll);
        }

        [Theory]
        [InlineData(1f, 36f, 648f)]
        [InlineData(1.5f, 24f, 432f)]
        public void Live_tab_board_fits_fifteen_per_team_at_720p(float scale, float safeY, float safeHeight)
        {
            // Mirrors the shell host Tab board: header 54, standing strip 30, column header 22, footer 20.
            float rowsTop = safeY + 2 + 54 + 30 + 22;
            float viewport = safeY + safeHeight - 20 - rowsTop;
            var m = TeamTableMetrics.Fit(viewport, 15, 26f, 18f, 5);
            Assert.True(m.UsedHeight <= viewport + 1e-3f);
            Assert.True(m.RowHeight >= 18f);
            // 100% and 150% UI scale both show the whole 15-operator team without scrolling.
            Assert.False(m.NeedsScroll, $"scale {scale}: {m.VisibleRows} visible");
        }

        [Fact]
        public void Wheel_steps_scroll_by_whole_rows_and_clamp()
        {
            var m = TeamTableMetrics.Fit(5 * 26f, 15, 26f, 26f, 5);
            Assert.Equal(5, m.VisibleRows);
            Assert.Equal(1, m.Scroll(0, 3f));
            Assert.Equal(1, m.Scroll(0, 1f));
            Assert.Equal(0, m.Scroll(0, -3f));
            Assert.Equal(m.MaxFirstRow, m.Scroll(m.MaxFirstRow, 30f));
            Assert.Equal(4, m.Scroll(4, 0f));
        }

        [Theory]
        [InlineData(1, 280f, DebriefAwardMode.Full)]
        [InlineData(15, 280f, DebriefAwardMode.Full)]
        [InlineData(15, 220f, DebriefAwardMode.Compact)]
        [InlineData(5, 100f, DebriefAwardMode.Hidden)]
        public void Debrief_awards_shrink_before_the_tables_lose_their_rows(int perTeam, float belowBand, DebriefAwardMode expected)
        {
            // belowBand = space from the award row to the table bottom when no cards are drawn.
            float available = belowBand + DebriefAwardLayout.BlockHeight(DebriefAwardMode.Full);
            if (expected != DebriefAwardMode.Full) available = belowBand;
            var mode = DebriefAwardLayout.Choose(available, perTeam);
            Assert.Equal(expected, mode);
            float table = available - DebriefAwardLayout.BlockHeight(mode) - DebriefAwardLayout.TableChrome;
            if (mode != DebriefAwardMode.Hidden)
                Assert.True(table >= System.Math.Min(perTeam, DebriefAwardLayout.MinTableRows) * DebriefAwardLayout.MinRowHeight);
        }

        [Fact]
        public void Kill_death_ratio_handles_zero_deaths_and_uses_invariant_format()
        {
            Assert.Equal("0.00", ScoreboardStats.KillDeathRatio(0, 0));
            Assert.Equal("7.00", ScoreboardStats.KillDeathRatio(7, 0));
            Assert.Equal("2.80", ScoreboardStats.KillDeathRatio(14, 5));
        }

        [Fact]
        public void Team_counts_alive_and_local_position_come_from_sorted_rows()
        {
            var rows = new List<ScoreboardRow>();
            for (int i = 0; i < 30; i++)
            {
                byte team = (byte)(i % 2 == 0 ? 1 : 2);
                bool local = i == 24;
                rows.Add(new ScoreboardRow(new ParticipantId((uint)(i + 1)), "P" + i, team, local, (uint)(1000 - i * 10), 0, 0, local, i % 3 != 0));
            }
            Assert.Equal(15, ScoreboardStats.TeamRowCount(rows, 1));
            Assert.Equal(15, ScoreboardStats.TeamRowCount(rows, 2));
            Assert.Equal(15, ScoreboardStats.LargestTeam(rows));
            Assert.Equal(10, ScoreboardStats.AliveCount(rows, 1));
            Assert.Equal(25, ScoreboardStats.LocalOverallRank(rows));
            Assert.Equal(12, ScoreboardStats.LocalTeamIndex(rows, out byte team1));
            Assert.Equal(1, team1);
        }
    }
}
