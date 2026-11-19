#nullable enable
using System.Linq;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Generation.Icons;
using Shooter.Presentation.Scoreboard;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    /// <summary>
    /// ADR-119: <see cref="CareerLadder.RankCount"/> is the one source of truth for the ladder length. Assemblies that
    /// cannot reference Game.Settings keep a mirror constant; this test pins every mirror and the per-rank art to it.
    /// </summary>
    public sealed class CareerRankCountAgreementTests
    {
        [Fact]
        public void Every_rank_count_mirror_matches_the_ladder()
        {
            Assert.Equal(30, CareerLadder.RankCount);
            Assert.Equal(CareerLadder.RankCount, CareerLadder.Ranks.Count);
            Assert.Equal(CareerLadder.RankCount, IconLibrary.CareerRankCount);
            Assert.Equal(CareerLadder.RankCount, BotRanks.MaxRank);
            Assert.Equal(CareerLadder.RankCount, ScoreboardRow.MaxRankIndex);
            Assert.Equal(CareerLadder.RankCount, CareerRewardCatalog.All.Count);
        }

        [Fact]
        public void Every_rank_has_its_own_insignia_and_no_more()
        {
            var names = IconLibrary.All.Select(r => r.Name).ToHashSet();
            for (int rank = 1; rank <= CareerLadder.RankCount; rank++)
            {
                Assert.Equal(CareerLadder.Get(rank).IconName, IconLibrary.CareerRank(rank));
                Assert.Contains(IconLibrary.CareerRank(rank), names);
            }
            Assert.DoesNotContain("rank-" + (CareerLadder.RankCount + 1).ToString("00"), names);
        }

        [Fact]
        public void Every_buff_reward_has_a_library_icon()
        {
            var names = IconLibrary.All.Select(r => r.Name).ToHashSet();
            foreach (var art in CareerRewardCatalog.All)
                if (!art.IconName.StartsWith("weapon.")) Assert.Contains(art.IconName, names);
        }
    }
}
