#nullable enable
using Shooter.Presentation.Hud;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    public sealed class UiMotionTests
    {
        [Fact]
        public void Easings_hit_their_endpoints()
        {
            Assert.Equal(0f, UiMotion.OutCubic(0f));
            Assert.Equal(1f, UiMotion.OutCubic(1f));
            Assert.Equal(0f, UiMotion.InOutCubic(0f));
            Assert.Equal(1f, UiMotion.InOutCubic(1f));
            Assert.Equal(0.5f, UiMotion.InOutCubic(0.5f), 5);
            Assert.Equal(0f, UiMotion.OutBack(0f), 5);
            Assert.Equal(1f, UiMotion.OutBack(1f), 5);
            Assert.Equal(1f, UiMotion.OutCubic(5f));
            Assert.Equal(0f, UiMotion.OutCubic(-1f));
        }

        [Fact]
        public void Out_back_overshoots_before_settling()
        {
            float peak = 0f;
            for (int i = 0; i <= 100; i++) peak = System.Math.Max(peak, UiMotion.OutBack(i / 100f));
            Assert.True(peak > 1.05f);
        }

        [Fact]
        public void Stamp_starts_large_and_settles_at_one()
        {
            Assert.Equal(UiMotion.StampStartScale, UiMotion.StampScale(0f, reducedMotion: false), 4);
            Assert.Equal(1f, UiMotion.StampScale(UiMotion.StampSeconds, reducedMotion: false), 4);
            Assert.Equal(1f, UiMotion.StampScale(10f, reducedMotion: false), 4);
            Assert.Equal(1f, UiMotion.StampScale(0f, reducedMotion: true));
        }

        [Fact]
        public void Slide_settles_and_reduced_motion_never_moves()
        {
            Assert.Equal(UiMotion.SlideDistance, UiMotion.SlideOffset(0f, 1f, false), 4);
            Assert.Equal(-UiMotion.SlideDistance, UiMotion.SlideOffset(0f, -1f, false), 4);
            Assert.Equal(0f, UiMotion.SlideOffset(UiMotion.TransitionSeconds, 1f, false), 4);
            Assert.Equal(0f, UiMotion.SlideOffset(0f, 1f, true));
        }

        [Fact]
        public void Follow_converges_without_overshoot_and_snaps_under_reduced_motion()
        {
            float v = 0f;
            for (int i = 0; i < 120; i++)
            {
                float next = UiMotion.Follow(v, 100f, UiMotion.FocusFollowRate, 1f / 60f, false);
                Assert.True(next >= v && next <= 100f);
                v = next;
            }
            Assert.Equal(100f, v);
            Assert.Equal(100f, UiMotion.Follow(0f, 100f, UiMotion.FocusFollowRate, 1f / 60f, true));
            Assert.Equal(5f, UiMotion.Follow(5f, 100f, UiMotion.FocusFollowRate, 0f, false));
        }

        [Fact]
        public void Follow_is_frame_rate_independent()
        {
            float a = 0f, b = 0f;
            for (int i = 0; i < 6; i++) a = UiMotion.Follow(a, 100f, 4f, 1f / 60f, false);
            for (int i = 0; i < 3; i++) b = UiMotion.Follow(b, 100f, 4f, 1f / 30f, false);
            Assert.Equal(a, b, 3);
        }

        [Fact]
        public void Count_up_reaches_target_and_reduced_motion_jumps()
        {
            Assert.Equal(0, UiMotion.CountUp(0, 150, 0f, 1f, false));
            Assert.Equal(150, UiMotion.CountUp(0, 150, 1f, 1f, false));
            int mid = UiMotion.CountUp(0, 150, 0.5f, 1f, false);
            Assert.InRange(mid, 76, 149);
            Assert.Equal(150, UiMotion.CountUp(0, 150, 0f, 1f, true));
        }

        [Fact]
        public void Glint_runs_once_after_landing_and_obeys_reduced_flash()
        {
            Assert.Equal(-1f, UiMotion.GlintPosition(0f, false));
            float during = UiMotion.GlintPosition(UiMotion.StampSeconds * 0.8f + 0.2f, false);
            Assert.InRange(during, 0f, 1f);
            Assert.Equal(-1f, UiMotion.GlintPosition(5f, false));
            Assert.Equal(-1f, UiMotion.GlintPosition(UiMotion.StampSeconds * 0.8f + 0.2f, true));
        }

        [Fact]
        public void Stagger_delays_later_items_and_pulse_is_steady_with_reduced_flash()
        {
            Assert.Equal(1f - 2 * UiMotion.StaggerSeconds, UiMotion.Staggered(1f, 2, false), 5);
            Assert.Equal(1f, UiMotion.Staggered(1f, 2, true));
            Assert.Equal(0f, UiMotion.Pulse(0f, 1f, false), 5);
            Assert.Equal(1f, UiMotion.Pulse(0.5f, 1f, false), 5);
            Assert.Equal(0.5f, UiMotion.Pulse(0.25f, 1f, true));
        }
    }
}
