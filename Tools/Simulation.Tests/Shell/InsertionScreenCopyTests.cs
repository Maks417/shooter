#nullable enable
using Shooter.Presentation.Menus;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    public sealed class InsertionScreenCopyTests
    {
        [Fact]
        public void Match_rules_and_footer_use_selected_team_size()
        {
            Assert.Equal("TEAM DEATHMATCH  ·  5 v 5  ·  150 KILLS / 15 MIN", InsertionScreenCopy.MatchRules(5));
            Assert.Equal("TEAM DEATHMATCH  ·  8 v 8  ·  150 KILLS / 15 MIN", InsertionScreenCopy.MatchRules(8));
            Assert.Equal("TEAM DEATHMATCH  ·  15 v 15  ·  150 KILLS / 15 MIN", InsertionScreenCopy.MatchRules(15));
            Assert.Equal("TEAM DEATHMATCH   /   5 v 5", InsertionScreenCopy.MatchFooter(5));
            Assert.Equal("TEAM DEATHMATCH   /   8 v 8", InsertionScreenCopy.MatchFooter(8));
            Assert.Equal("TEAM DEATHMATCH   /   15 v 15", InsertionScreenCopy.MatchFooter(15));
        }

        [Fact]
        public void Loading_phase_and_display_name_stay_friendly()
        {
            Assert.Equal("Loading Old Town…", InsertionScreenCopy.LoadingPhase("training-compound"));
            Assert.Equal("Loading Harbour…", InsertionScreenCopy.LoadingPhase("depot-yard"));
            Assert.Equal("Loading the map…", InsertionScreenCopy.LoadingPhase(""));
            Assert.Equal("TRAINING COMPOUND", InsertionScreenCopy.DisplayNameOrFallback("TRAINING COMPOUND", "training-compound"));
            Assert.Equal("Harbour", InsertionScreenCopy.DisplayNameOrFallback("  ", "depot-yard"));
            Assert.Equal("OPERATION", InsertionScreenCopy.DisplayNameOrFallback(null, ""));
        }

        [Fact]
        public void Boot_copy_has_no_cancel_or_percentage_claims()
        {
            Assert.Contains("Loading your arsenal", InsertionScreenCopy.BootPhase);
            Assert.DoesNotContain("%", InsertionScreenCopy.BootPhase);
            Assert.DoesNotContain("%", InsertionScreenCopy.MatchTitle);
            Assert.Equal("Linking the battlefield…", InsertionScreenCopy.BindingPresentation);
            Assert.Equal("Standing by", InsertionScreenCopy.BootHeader);
            Assert.Equal("SOLO OPS  ·  NO NETWORK", InsertionScreenCopy.BootFooter);
        }
    }
}
