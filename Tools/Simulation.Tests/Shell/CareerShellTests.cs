#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Scoreboard;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    /// <summary>ADR-108 shell model: Armory locks, the Career screen, rank on scoreboard rows and career copy.</summary>
    public sealed class CareerShellTests
    {
        private static readonly DefinitionId Ak47 = new DefinitionId("weapons/ak47");
        private static readonly DefinitionId M16 = new DefinitionId("weapons/m16");
        private static readonly DefinitionId Mk14 = new DefinitionId("weapons/mk14");
        private static readonly DefinitionId G17 = new DefinitionId("weapons/g17");
        private static readonly DefinitionId Eagle = new DefinitionId("weapons/golden-eagle");
        private static readonly DefinitionId Irons = new DefinitionId("sights/irons");
        private static readonly DefinitionId Collimator = new DefinitionId("sights/collimator");
        private static readonly DefinitionId Prism = new DefinitionId("sights/scope2");
        private static readonly DefinitionId Scope4 = new DefinitionId("sights/scope4");

        // Mirrors the ladder: rank 1 holds M16 + G17 on irons; collimator at 2, prism at 5, Eagle at 6, AK-47 at 8, 4x at 10, Mk 14 at 11.
        private static System.Func<DefinitionId, bool> LockedAbove(int rank) => id =>
        {
            var unlock = new Dictionary<string, int>
            {
                ["sights/collimator"] = 2, ["sights/scope2"] = 5, ["weapons/golden-eagle"] = 6, ["weapons/ak47"] = 8,
                ["sights/scope4"] = 10, ["weapons/mk14"] = 11
            };
            return unlock.TryGetValue(id.ToString(), out int at) && at > rank;
        };

        private static ArmorySelection Create(int rank, string? primary = null, string? sidearm = null, IReadOnlyDictionary<string, string>? sights = null) =>
            new ArmorySelection(new[] { Ak47, M16, Mk14 }, new[] { G17, Eagle }, primary, sidearm, sights, LockedAbove(rank));

        [Fact]
        public void A_new_career_starts_on_the_m16_with_iron_sights_and_the_g17()
        {
            var armory = Create(1, "weapons/ak47", "weapons/golden-eagle");
            Assert.Equal(M16, armory.Primary);
            Assert.Equal(G17, armory.Sidearm);
            Assert.Equal(Irons, armory.PrimarySight); // factory 2x prism is still locked
            Assert.True(armory.IsLocked(Ak47));
            Assert.False(armory.IsLocked(M16));
        }

        [Fact]
        public void Locked_guns_and_sights_cannot_be_selected()
        {
            var armory = Create(2);
            int revision = armory.Revision;
            Assert.False(armory.SelectWeapon(Ak47));
            Assert.False(armory.SelectSight(Prism, M16));
            Assert.Equal(revision, armory.Revision);
            Assert.True(armory.SelectSight(Collimator, M16));
            Assert.Equal(Collimator, armory.PrimarySight);
        }

        [Fact]
        public void A_saved_locked_sight_falls_back_to_the_first_unlocked_one_until_promotion()
        {
            var armory = Create(1, "weapons/m16", null, new Dictionary<string, string> { ["weapons/m16"] = "sights/scope2" });
            Assert.Equal(Irons, armory.PrimarySight);
            armory.SetLocks(LockedAbove(5));
            Assert.Equal(Prism, armory.PrimarySight);
        }

        [Fact]
        public void Promotion_keeps_choices_and_opens_new_guns()
        {
            var armory = Create(5);
            int revision = armory.Revision;
            armory.SetLocks(LockedAbove(11));
            Assert.Equal(revision, armory.Revision);
            Assert.True(armory.SelectWeapon(Mk14));
            Assert.Equal(Scope4, armory.PrimarySight);
            // A demotion (for example a reset career file) moves the locked pick back to an unlocked gun.
            armory.SetLocks(LockedAbove(1));
            Assert.Equal(M16, armory.Primary);
            Assert.True(armory.Revision > revision);
        }

        [Fact]
        public void Career_screen_opens_from_main_or_armory_and_returns_there()
        {
            var nav = new ShellNavigation();
            nav.ResetToMain();
            nav.OpenCareer();
            Assert.Equal(ShellScreen.Career, nav.Screen);
            nav.CloseCareer();
            Assert.Equal(ShellScreen.Main, nav.Screen);
            nav.OpenArmory();
            nav.OpenCareer();
            Assert.Equal(ShellScreen.Armory, nav.CareerReturn);
            nav.CloseCareer();
            Assert.Equal(ShellScreen.Armory, nav.Screen);
            Assert.Equal(MusicContext.Menu, MusicDirector.ContextFor(ShellScreen.Career, ShellScreen.Main));
            Assert.True(ShellMotion.SlidesOnEnter(ShellScreen.Career));
        }

        [Fact]
        public void Scoreboard_rows_carry_the_rank_from_the_lookup()
        {
            var rows = new List<ParticipantState>
            {
                new ParticipantState(new ParticipantId(1), "Player", 1, true, new EntityId(1), new LifeId(1), true, 3, 1, 0, 300),
                new ParticipantState(new ParticipantId(2), "Nora", 2, false, new EntityId(2), new LifeId(1), true, 1, 3, 0, 100),
            };
            var snap = new MatchViewSnapshot(1001, 10, 10, 100, GameMatchPhase.Playing, MatchOutcome.None, FinishReason.None, 3, 1, rows, 0);
            var table = new ScoreboardTable();
            table.BindLive(snap, new EntityId(1));
            Assert.All(table.Rows, r => Assert.Equal(0, r.RankIndex));
            table.RankOf = (p, human) => human ? 7 : 30;
            table.BindLive(snap, new EntityId(1));
            Assert.Equal(7, table.Rows[0].RankIndex);
            Assert.Equal(20, table.Rows[1].RankIndex); // clamped to the ladder
        }

        [Fact]
        public void Career_copy_formats_progress_awards_and_stats()
        {
            Assert.Equal("12,400 / 15,000 CP", ShellUiCopy.CareerProgress(12400, 15000));
            Assert.Equal("399,000 CP", ShellUiCopy.CareerProgress(399000, 0));
            Assert.Equal("+3,240 CP  ·  SCORE 1,740  ·  FINISH +500  ·  WIN +1,000", ShellUiCopy.CareerAwardLine(3240, 1740, 500, 1000, "WIN"));
            Assert.Equal("+900 CP  ·  SCORE 400  ·  FINISH +500", ShellUiCopy.CareerAwardLine(900, 400, 500, 0, "WIN"));
            Assert.Equal("PROMOTED: SERGEANT  ·  Golden Eagle sidearm", ShellUiCopy.CareerPromotion("Sergeant", "Golden Eagle sidearm"));
            Assert.Equal("Unlocks at Captain", ShellUiCopy.UnlocksAt("Captain"));
            Assert.Equal("12 m", ShellUiCopy.PlayTime(12 * 3600));
            Assert.Equal("3 h 07 m", ShellUiCopy.PlayTime((3 * 60 + 7) * 3600L));
            Assert.Equal("1.85", ShellUiCopy.Ratio(37, 20));
            Assert.Equal("4.00", ShellUiCopy.Ratio(4, 0));
            Assert.Equal("54%", ShellUiCopy.Percent(27, 50));
            Assert.Equal("0%", ShellUiCopy.Percent(3, 0));
        }
    }
}
