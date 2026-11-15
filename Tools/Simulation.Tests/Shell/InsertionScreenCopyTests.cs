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
            Assert.Equal("TEAM DEATHMATCH  ·  6 v 6  ·  150 KILLS / 15 MIN", InsertionScreenCopy.MatchRules(6));
            Assert.Equal("TEAM DEATHMATCH   /   5 v 5", InsertionScreenCopy.MatchFooter(5));
            Assert.Equal("TEAM DEATHMATCH   /   6 v 6", InsertionScreenCopy.MatchFooter(6));
        }

        [Fact]
        public void Loading_phase_and_display_name_stay_truthful()
        {
            Assert.Equal("Loading training-compound…", InsertionScreenCopy.LoadingPhase("training-compound"));
            Assert.Equal("Loading map…", InsertionScreenCopy.LoadingPhase(""));
            Assert.Equal("TRAINING COMPOUND", InsertionScreenCopy.DisplayNameOrFallback("TRAINING COMPOUND", "training-compound"));
            Assert.Equal("depot-yard", InsertionScreenCopy.DisplayNameOrFallback("  ", "depot-yard"));
            Assert.Equal("OPERATION", InsertionScreenCopy.DisplayNameOrFallback(null, ""));
        }

        [Fact]
        public void Boot_copy_has_no_cancel_or_percentage_claims()
        {
            Assert.Contains("Binding packaged content", InsertionScreenCopy.BootPhase);
            Assert.DoesNotContain("%", InsertionScreenCopy.BootPhase);
            Assert.DoesNotContain("%", InsertionScreenCopy.MatchTitle);
            Assert.Equal(InsertionScreenCopy.BindingPresentation, "Binding presentation…");
        }
    }
}
