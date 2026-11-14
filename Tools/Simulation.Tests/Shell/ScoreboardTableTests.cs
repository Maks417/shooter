#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Scoreboard;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    public sealed class ScoreboardTableTests
    {
        [Fact]
        public void Live_rows_sort_by_kills_then_deaths_then_participant_id()
        {
            var rows = new List<ParticipantState>
            {
                Row(3, "C", 1, false, 2, 5),
                Row(1, "A", 1, true, 5, 1),
                Row(2, "B", 2, false, 5, 0),
                Row(4, "D", 2, false, 2, 1)
            };
            var snap = new MatchViewSnapshot(
                1001, 10, 10, 100, GameMatchPhase.Playing, MatchOutcome.None, FinishReason.None,
                7, 7, rows, 0);
            var table = new ScoreboardTable();
            table.BindLive(snap, new EntityId(1));
            Assert.Equal(4, table.RowCount);
            Assert.Equal("B", table.Rows[0].DisplayName);
            Assert.Equal("A", table.Rows[1].DisplayName);
            Assert.Equal("D", table.Rows[2].DisplayName);
            Assert.Equal("C", table.Rows[3].DisplayName);
            Assert.True(table.Rows[1].IsLocal);
        }

        [Fact]
        public void Results_outcome_labels_use_local_team_perspective()
        {
            var results = new MatchResults(
                new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL),
                WellKnownMaps.TrainingCompound,
                "catalog",
                "gameplay",
                "build",
                9,
                0,
                100,
                100,
                150,
                40,
                MatchOutcome.Team1,
                FinishReason.ScoreLimit,
                new[] { Row(1, "Player", 1, true, 10, 2) });
            var table = new ScoreboardTable();
            table.BindResults(results);
            Assert.Equal("Victory", table.LocalOutcomeLabel(1));
            Assert.Equal("Defeat", table.LocalOutcomeLabel(2));
            Assert.Equal(FinishReason.ScoreLimit, table.Reason);
        }

        [Fact]
        public void Twelve_rows_fit_720p_at_100_and_150_percent_ui_scale()
        {
            Assert.True(new ScoreboardLayout(1f).FitsReference(12));
            Assert.True(new ScoreboardLayout(1.5f).FitsReference(12));
            var panel = new ScoreboardLayout(1.5f).PanelRect();
            Assert.True(panel.Right <= HudLayout.ReferenceWidth + 1e-3f);
            Assert.True(panel.Bottom <= HudLayout.ReferenceHeight + 1e-3f);
        }

        private static ParticipantState Row(uint id, string name, byte team, bool human, uint kills, uint deaths)
            => new ParticipantState(
                new ParticipantId(id),
                name,
                team,
                human,
                new EntityId(id),
                new LifeId(1),
                true,
                kills,
                deaths,
                0);
    }
}
