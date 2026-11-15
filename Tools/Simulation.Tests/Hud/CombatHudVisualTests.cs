#nullable enable
using Shooter.Presentation.Hud;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    public sealed class CombatHudVisualTests
    {
        [Fact]
        public void Kill_feed_alpha_holds_then_fades()
        {
            Assert.Equal(1f, CombatHudVisual.KillFeedAlpha(6f));
            Assert.Equal(1f, CombatHudVisual.KillFeedAlpha(CombatHudVisual.KillFeedFadeSeconds));
            Assert.Equal(0.5f, CombatHudVisual.KillFeedAlpha(CombatHudVisual.KillFeedFadeSeconds * 0.5f));
            Assert.Equal(0f, CombatHudVisual.KillFeedAlpha(0f));
            Assert.Equal(0f, CombatHudVisual.KillFeedAlpha(-1f));
        }

        [Fact]
        public void Health_vignette_escalates_on_danger_and_damage_pulse()
        {
            Assert.Equal(0f, CombatHudVisual.HealthVignette(100, false));
            Assert.Equal(0f, CombatHudVisual.HealthVignette(41, false));
            Assert.True(CombatHudVisual.HealthVignette(40, false) > 0f);
            Assert.True(CombatHudVisual.HealthVignette(25, false) > CombatHudVisual.HealthVignette(40, false));
            Assert.True(CombatHudVisual.HealthVignette(20, false) > 0f);
            Assert.True(CombatHudVisual.HealthVignette(100, true) > 0f);
            Assert.True(CombatHudVisual.HealthVignette(20, true) <= 0.28f);
        }

        [Fact]
        public void Reduced_flash_keeps_static_critical_vignette_without_damage_pulse()
        {
            Assert.Equal(0f, CombatHudVisual.HealthVignette(100, true, reducedFlash: true));
            float critical = CombatHudVisual.HealthVignette(20, false, reducedFlash: true);
            Assert.True(critical > 0f);
            Assert.True(critical <= 0.14f);
            Assert.Equal(critical, CombatHudVisual.HealthVignette(20, true, reducedFlash: true));
        }

        [Fact]
        public void Health_danger_threshold_matches_brand_rule()
        {
            Assert.False(CombatHudVisual.IsHealthDanger(26));
            Assert.True(CombatHudVisual.IsHealthDanger(25));
            Assert.True(CombatHudVisual.IsHealthDanger(0));
            Assert.True(CombatHudVisual.IsHealthWarning(40));
            Assert.False(CombatHudVisual.IsHealthWarning(25));
            Assert.Equal(0.25f, CombatHudVisual.HealthFraction(25));
            Assert.Equal(1f, CombatHudVisual.HealthFraction(100));
            Assert.Equal(0f, CombatHudVisual.HealthFraction(0));
        }

        [Fact]
        public void Critical_bar_pulse_freezes_under_accessibility_prefs()
        {
            Assert.Equal(1f, CombatHudVisual.CriticalBarPulse(1.2f, reducedFlash: true, reducedMotion: false));
            Assert.Equal(1f, CombatHudVisual.CriticalBarPulse(1.2f, reducedFlash: false, reducedMotion: true));
            float a = CombatHudVisual.CriticalBarPulse(0.1f, false, false);
            float b = CombatHudVisual.CriticalBarPulse(0.8f, false, false);
            Assert.InRange(a, 0.72f, 1f);
            Assert.InRange(b, 0.72f, 1f);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Kill_feed_split_and_cardinals_are_stable()
        {
            Assert.True(CombatHudVisual.TrySplitKillFeed("YOU  >  Bot 3", out string left, out string right));
            Assert.Equal("YOU", left);
            Assert.Equal("Bot 3", right);
            Assert.False(CombatHudVisual.TrySplitKillFeed("broken", out _, out _));
            Assert.True(CombatHudVisual.IsCardinal(0));
            Assert.True(CombatHudVisual.IsCardinal(90));
            Assert.False(CombatHudVisual.IsCardinal(45));
            Assert.Equal("N", CombatHudVisual.CardinalOrBearing(0));
            Assert.Equal("45", CombatHudVisual.CardinalOrBearing(45));
        }
    }
}
