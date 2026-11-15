#nullable enable
using Shooter.Presentation.Hud;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    public sealed class ScoreBadgeVisualTests
    {
        [Fact]
        public void Labels_distinguish_kill_and_assist()
        {
            Assert.Equal("ENEMY KILLED", ScoreBadgeVisual.LabelFor(true));
            Assert.Equal("ASSIST", ScoreBadgeVisual.LabelFor(false));
            Assert.Equal("+100", ScoreBadgeVisual.FormatPoints(100));
            Assert.Equal("+65", ScoreBadgeVisual.FormatPoints(65));
        }

        [Fact]
        public void Kill_labels_escalate_by_life_streak()
        {
            Assert.Equal(ScoreBadgeVisual.KillLabel, ScoreBadgeVisual.KillLabelFor(0));
            Assert.Equal(ScoreBadgeVisual.KillLabel, ScoreBadgeVisual.KillLabelFor(1));
            Assert.Equal(ScoreBadgeVisual.DoubleKillLabel, ScoreBadgeVisual.KillLabelFor(2));
            Assert.Equal(ScoreBadgeVisual.TripleKillLabel, ScoreBadgeVisual.KillLabelFor(3));
            Assert.Equal(ScoreBadgeVisual.MultiKillLabel, ScoreBadgeVisual.KillLabelFor(4));
            Assert.Equal(ScoreBadgeVisual.MultiKillLabel, ScoreBadgeVisual.KillLabelFor(9));
            Assert.Equal("HEADSHOT", ScoreBadgeVisual.HeadshotLabel);
        }

        [Fact]
        public void Feed_alpha_holds_then_fades()
        {
            Assert.Equal(1f, ScoreBadgeVisual.FeedAlpha(ScoreBadgeVisual.FeedHoldSeconds));
            Assert.Equal(1f, ScoreBadgeVisual.FeedAlpha(ScoreBadgeVisual.FeedFadeSeconds));
            Assert.Equal(0.5f, ScoreBadgeVisual.FeedAlpha(ScoreBadgeVisual.FeedFadeSeconds * 0.5f));
            Assert.Equal(0f, ScoreBadgeVisual.FeedAlpha(0f));
        }

        [Fact]
        public void Ribbon_alpha_dims_under_reduced_flash()
        {
            Assert.Equal(1f, ScoreBadgeVisual.RibbonAlpha(ScoreBadgeVisual.RibbonHoldSeconds, reducedFlash: false));
            Assert.Equal(0.72f, ScoreBadgeVisual.RibbonAlpha(ScoreBadgeVisual.RibbonHoldReducedFlashSeconds, reducedFlash: true));
            Assert.True(ScoreBadgeVisual.RibbonHoldDuration(true) < ScoreBadgeVisual.RibbonHoldDuration(false));
            Assert.Equal(0f, ScoreBadgeVisual.RibbonAlpha(0f, false));
        }

        [Fact]
        public void Ammo_popup_alpha_holds_then_fades()
        {
            Assert.Equal(1f, ScoreBadgeVisual.AmmoPopupAlpha(ScoreBadgeVisual.AmmoPopupHoldSeconds));
            Assert.Equal(0.5f, ScoreBadgeVisual.AmmoPopupAlpha(ScoreBadgeVisual.AmmoPopupFadeSeconds * 0.5f));
            Assert.Equal(0f, ScoreBadgeVisual.AmmoPopupAlpha(0f));
        }
    }
}
