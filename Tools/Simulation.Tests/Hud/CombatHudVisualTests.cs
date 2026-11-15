#nullable enable
using Shooter.Presentation.Hud;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    public sealed class CombatHudVisualTests
    {
        [Fact]
        public void Damage_indicator_points_at_the_attacker_relative_to_the_view()
        {
            const float Pi = 3.14159265f;
            // Facing +Z (yaw 0): ahead, right (+X), behind, left.
            Assert.Equal(0f, CombatHudVisual.DamageIndicatorAngle(0f, 0, 0, 0, 10), 4);
            Assert.Equal(Pi / 2, CombatHudVisual.DamageIndicatorAngle(0f, 0, 0, 10, 0), 4);
            Assert.Equal(-Pi / 2, CombatHudVisual.DamageIndicatorAngle(0f, 0, 0, -10, 0), 4);
            Assert.Equal(Pi, System.Math.Abs(CombatHudVisual.DamageIndicatorAngle(0f, 0, 0, 0, -10)), 4);
            // Turning right by 90 degrees puts an attacker on +X straight ahead; the marker follows the view.
            Assert.Equal(0f, CombatHudVisual.DamageIndicatorAngle(Pi / 2, 0, 0, 10, 0), 4);
            float a = CombatHudVisual.DamageIndicatorAngle(3f, 5, 5, -20, 40);
            Assert.InRange(a, -Pi, Pi);
        }

        [Fact]
        public void Damage_indicator_holds_then_fades()
        {
            Assert.Equal(0.9f, CombatHudVisual.DamageIndicatorAlpha(CombatHudVisual.DamageIndicatorSeconds));
            Assert.Equal(0.9f, CombatHudVisual.DamageIndicatorAlpha(CombatHudVisual.DamageIndicatorSeconds * 0.5f));
            Assert.Equal(0.45f, CombatHudVisual.DamageIndicatorAlpha(CombatHudVisual.DamageIndicatorSeconds * 0.25f), 4);
            Assert.Equal(0f, CombatHudVisual.DamageIndicatorAlpha(0f));
        }

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
        public void Tightness_fill_width_clamps_fraction()
        {
            Assert.Equal(0f, CombatHudVisual.TightnessFillWidth(0f, 100f));
            Assert.Equal(0f, CombatHudVisual.TightnessFillWidth(-1f, 100f));
            Assert.Equal(50f, CombatHudVisual.TightnessFillWidth(0.5f, 100f));
            Assert.Equal(100f, CombatHudVisual.TightnessFillWidth(1f, 100f));
            Assert.Equal(100f, CombatHudVisual.TightnessFillWidth(2f, 100f));
            Assert.Equal(0f, CombatHudVisual.TightnessFillWidth(0.5f, 0f));
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

        [Fact]
        public void Segment_fill_covers_left_to_right()
        {
            const float w = 20f;
            Assert.Equal(0f, CombatHudVisual.SegmentFill(0f, 0, w));
            Assert.Equal(w, CombatHudVisual.SegmentFill(20f, 0, w));
            Assert.Equal(0f, CombatHudVisual.SegmentFill(20f, 1, w));
            Assert.Equal(w * 0.5f, CombatHudVisual.SegmentFill(30f, 1, w));
            Assert.Equal(w, CombatHudVisual.SegmentFill(100f, 4, w));
        }

        [Fact]
        public void Gunfire_reveal_accepts_only_enemy_non_local_shots()
        {
            Assert.True(CombatHudVisual.ShouldRevealGunfire(true, isLocalHuman: false, isEnemyTeam: true));
            Assert.False(CombatHudVisual.ShouldRevealGunfire(true, isLocalHuman: true, isEnemyTeam: true));
            Assert.False(CombatHudVisual.ShouldRevealGunfire(true, isLocalHuman: false, isEnemyTeam: false));
            Assert.False(CombatHudVisual.ShouldRevealGunfire(false, isLocalHuman: false, isEnemyTeam: true));
        }

        [Fact]
        public void Gunfire_reveal_holds_then_fades()
        {
            Assert.Equal(0.95f, CombatHudVisual.GunfireRevealAlpha(CombatHudVisual.GunfireRevealSeconds));
            Assert.Equal(0.95f, CombatHudVisual.GunfireRevealAlpha(CombatHudVisual.GunfireRevealFadeSeconds));
            Assert.Equal(0.475f, CombatHudVisual.GunfireRevealAlpha(CombatHudVisual.GunfireRevealFadeSeconds * 0.5f), 4);
            Assert.Equal(0f, CombatHudVisual.GunfireRevealAlpha(0f));
            Assert.Equal(0f, CombatHudVisual.GunfireRevealAlpha(-1f));
        }

        [Fact]
        public void Gunfire_reveal_ring_reuses_entity_then_oldest()
        {
            var ids = new ulong[4];
            var until = new float[4];
            Assert.Equal(0, CombatHudVisual.PickGunfireRevealSlot(ids, until, 10, now: 1f, capacity: 4));
            ids[0] = 10; until[0] = 5f;
            ids[1] = 20; until[1] = 3f;
            ids[2] = 30; until[2] = 8f;
            ids[3] = 40; until[3] = 4f;
            // Active entity 20 refreshes its own slot.
            Assert.Equal(1, CombatHudVisual.PickGunfireRevealSlot(ids, until, 20, now: 2f, capacity: 4));
            // New entity takes the soonest-expiring slot (until 3).
            Assert.Equal(1, CombatHudVisual.PickGunfireRevealSlot(ids, until, 99, now: 2f, capacity: 4));
            // Expired same-entity slot is not treated as active; oldest wins.
            until[0] = 1.5f;
            Assert.Equal(0, CombatHudVisual.PickGunfireRevealSlot(ids, until, 10, now: 2f, capacity: 4));
        }
    }
}
