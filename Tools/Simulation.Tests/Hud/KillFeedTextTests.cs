#nullable enable
using Shooter.Presentation.Hud;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    public sealed class KillFeedTextTests
    {
        [Fact]
        public void Killer_present_formats_killer_then_victim()
        {
            Assert.Equal("YOU  >  Bot 3", KillFeedText.Format("YOU", "Bot 3"));
        }

        [Fact]
        public void Null_killer_shows_environment_label()
        {
            Assert.Equal("Bot 3  >  ENVIRONMENT", KillFeedText.Format(null, "Bot 3"));
        }

        [Fact]
        public void Empty_killer_shows_environment_label()
        {
            Assert.Equal("YOU  >  ENVIRONMENT", KillFeedText.Format("", "YOU"));
        }

        [Fact]
        public void Local_killer_and_victim_roles_detect_YOU_mentions()
        {
            Assert.True(KillFeedText.IsLocalKiller("YOU  >  Bot 3"));
            Assert.False(KillFeedText.IsLocalVictim("YOU  >  Bot 3"));
            Assert.True(KillFeedText.IsLocalVictim("Bot 3  >  YOU"));
            Assert.False(KillFeedText.IsLocalKiller("Bot 3  >  YOU"));
            Assert.True(KillFeedText.IsLocalVictim("YOU  >  ENVIRONMENT"));
            Assert.False(KillFeedText.IsLocalKiller("YOU  >  ENVIRONMENT"));
            Assert.False(KillFeedText.IsLocalKiller("Bot 1  >  Bot 2"));
            Assert.False(KillFeedText.IsLocalVictim("Bot 1  >  Bot 2"));
        }

        [Fact]
        public void Entry_roles_and_labels_match_AAA_feed_layout()
        {
            var kill = KillFeedText.Create("YOU", "Bot 3", KillFeedWeapon.Rifle, headshot: true);
            Assert.True(KillFeedText.IsLocalKiller(kill));
            Assert.False(KillFeedText.IsLocalVictim(kill));
            Assert.Equal("YOU", kill.LeftLabel);
            Assert.Equal("Bot 3", kill.RightLabel);
            Assert.True(kill.Headshot);
            Assert.Equal(KillFeedWeapon.Rifle, kill.Weapon);

            var env = KillFeedText.Create(null, "YOU", KillFeedWeapon.None, headshot: false);
            Assert.False(KillFeedText.IsLocalKiller(env));
            Assert.True(KillFeedText.IsLocalVictim(env));
            Assert.Equal("YOU", env.LeftLabel);
            Assert.Equal(KillFeedText.EnvironmentLabel, env.RightLabel);
            Assert.Equal(KillFeedWeapon.None, env.Weapon);
        }
    }
}
