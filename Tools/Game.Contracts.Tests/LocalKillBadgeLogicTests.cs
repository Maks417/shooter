#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class LocalKillBadgeLogicTests
    {
        private static EventId Ev(ushort ordinal) => new EventId(1, 10, new EntityId(1), ordinal);
        private static EntityId Killer => new EntityId(1);
        private static EntityId Victim => new EntityId(2);

        [Fact]
        public void Increment_streak_clamps_at_byte_max()
        {
            Assert.Equal(1, LocalKillBadgeLogic.IncrementStreak(0));
            Assert.Equal(2, LocalKillBadgeLogic.IncrementStreak(1));
            Assert.Equal(4, LocalKillBadgeLogic.IncrementStreak(3));
            Assert.Equal(byte.MaxValue, LocalKillBadgeLogic.IncrementStreak(byte.MaxValue));
        }

        [Fact]
        public void Push_kill_without_headshot_emits_single_award()
        {
            var awards = new List<LocalScoreAward>();
            LocalKillBadgeLogic.PushKillBadges(
                awards.Add,
                "Bot 3",
                streak: 2,
                headshot: false,
                killPoints: (uint)GameContractVersion.PersonalKillPoints);

            Assert.Single(awards);
            Assert.Equal(LocalScoreAwardKind.Kill, awards[0].Kind);
            Assert.Equal(2, awards[0].KillStreak);
            Assert.False(awards[0].Headshot);
            Assert.Equal((uint)GameContractVersion.PersonalKillPoints, awards[0].Points);
            Assert.Equal("Bot 3", awards[0].VictimName);
        }

        [Fact]
        public void Push_kill_with_headshot_emits_extra_badge()
        {
            var awards = new List<LocalScoreAward>();
            LocalKillBadgeLogic.PushKillBadges(
                awards.Add,
                "YOU",
                streak: 4,
                headshot: true,
                killPoints: 100);

            Assert.Equal(2, awards.Count);
            Assert.Equal(LocalScoreAwardKind.Kill, awards[0].Kind);
            Assert.Equal(4, awards[0].KillStreak);
            Assert.True(awards[0].Headshot);
            Assert.Equal(100u, awards[0].Points);
            Assert.Equal(LocalScoreAwardKind.Headshot, awards[1].Kind);
            Assert.Equal(LocalKillBadgeLogic.HeadshotBonusPoints, awards[1].Points);
            Assert.Equal(20u, awards[1].Points);
            Assert.Equal(0, awards[1].KillStreak);
            Assert.True(awards[1].Headshot);
        }

        [Fact]
        public void Zero_streak_normalizes_to_one_on_push()
        {
            var awards = new List<LocalScoreAward>();
            LocalKillBadgeLogic.PushKillBadges(awards.Add, "A", streak: 0, headshot: false, killPoints: 100);
            Assert.Equal(1, awards[0].KillStreak);
        }

        [Fact]
        public void Fatal_headshot_uses_last_damage_region_before_death()
        {
            var events = new List<CombatEvent>
            {
                new CombatEvent(Ev(1), CombatEventKind.ShotAccepted, Killer, default,
                    new ShotPayload(new DefinitionId("weapons/rifle"), 1, new Float3(0, 1, 0), new Float3(0, 0, 1), 1, 1)),
                new CombatEvent(Ev(2), CombatEventKind.DamageApplied, Killer, Victim,
                    new DamagePayload(100, 0, LocalKillBadgeLogic.HeadRegionId)),
                new CombatEvent(Ev(3), CombatEventKind.Death, Killer, Victim, new DeathPayload(Killer))
            };

            Assert.True(LocalKillBadgeLogic.IsFatalHeadshot(events, 2));
        }

        [Fact]
        public void Torso_kill_is_not_headshot()
        {
            var events = new List<CombatEvent>
            {
                new CombatEvent(Ev(1), CombatEventKind.DamageApplied, Killer, Victim,
                    new DamagePayload(100, 0, 2)),
                new CombatEvent(Ev(2), CombatEventKind.Death, Killer, Victim, new DeathPayload(Killer))
            };

            Assert.False(LocalKillBadgeLogic.IsFatalHeadshot(events, 1));
        }

        [Fact]
        public void Environment_death_is_not_headshot()
        {
            var events = new List<CombatEvent>
            {
                new CombatEvent(Ev(1), CombatEventKind.Death, default, Victim, new DeathPayload(default))
            };

            Assert.False(LocalKillBadgeLogic.IsFatalHeadshot(events, 0));
        }

        [Fact]
        public void Assist_award_shape_unchanged()
        {
            var assist = new LocalScoreAward(LocalScoreAwardKind.Assist, 65, "Bot 2");
            Assert.Equal(LocalScoreAwardKind.Assist, assist.Kind);
            Assert.Equal(65u, assist.Points);
            Assert.Equal(0, assist.KillStreak);
            Assert.False(assist.Headshot);
        }
    }
}
