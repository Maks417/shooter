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
    }
}
