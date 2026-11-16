#nullable enable
using System;
using Shooter.Presentation.Hud;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    public sealed class UiShapeParamsTests
    {
        [Fact]
        public void Rounded_box_distance_is_signed_and_exact_on_flat_edges()
        {
            Assert.Equal(-5f, UiShapeParams.RoundedBoxDistance(0f, 0f, 10f, 5f, 2f), 4);
            Assert.Equal(0f, UiShapeParams.RoundedBoxDistance(10f, 0f, 10f, 5f, 2f), 4);
            Assert.Equal(3f, UiShapeParams.RoundedBoxDistance(13f, 0f, 10f, 5f, 2f), 4);
            // A corner with radius 2 is cut back: the sharp corner point lies outside the shape.
            float corner = UiShapeParams.RoundedBoxDistance(10f, 5f, 10f, 5f, 2f);
            Assert.Equal((float)(Math.Sqrt(8) - 2), corner, 4);
            // Radius 0 keeps the sharp corner exactly on the boundary.
            Assert.Equal(0f, UiShapeParams.RoundedBoxDistance(10f, 5f, 10f, 5f, 0f), 4);
        }

        [Fact]
        public void Radius_is_clamped_to_half_the_short_side()
        {
            Assert.Equal(4f, UiShapeParams.FitRadius(10f, 8f, 40f));
            Assert.Equal(0f, UiShapeParams.FitRadius(-3f, 8f, 40f));
            Assert.Equal(3f, UiShapeParams.FitRadius(3f, 8f, 40f));
            // A fully rounded pill behaves like a circle at its ends.
            Assert.Equal(0f, UiShapeParams.RoundedBoxDistance(14f, 0f, 14f, 4f, 99f), 4);
        }

        [Fact]
        public void Coverage_ramps_over_one_anti_alias_width()
        {
            Assert.Equal(1f, UiShapeParams.Coverage(-2f, 1f));
            Assert.Equal(0.5f, UiShapeParams.Coverage(0f, 1f), 5);
            Assert.Equal(0f, UiShapeParams.Coverage(2f, 1f));
            Assert.Equal(1f, UiShapeParams.AntiAliasPixels(1f));
            Assert.Equal(1f / 1.5f, UiShapeParams.AntiAliasPixels(1.5f), 5);
        }

        [Fact]
        public void Edge_fade_is_quadratic_and_bounded()
        {
            Assert.Equal(0.8f, UiShapeParams.EdgeFade(0f, 40f, 0.8f));
            Assert.Equal(0.2f, UiShapeParams.EdgeFade(20f, 40f, 0.8f), 5);
            Assert.Equal(0f, UiShapeParams.EdgeFade(40f, 40f, 0.8f));
            Assert.Equal(0f, UiShapeParams.EdgeFade(5f, 0f, 0.8f));
        }

        [Fact]
        public void Padding_covers_shadow_and_glow()
        {
            float pad = UiShapeParams.Padding(UiShapeParams.ShadowSoftness, UiShapeParams.ShadowOffset, UiShapeParams.FocusGlowWidth);
            Assert.True(pad >= UiShapeParams.ShadowSoftness + UiShapeParams.ShadowOffset + UiShapeParams.FocusGlowWidth);
            Assert.Equal(1f, UiShapeParams.Padding(0f, 0f, 0f));
        }

        [Fact]
        public void Arc_spans_normalise_to_a_positive_sweep()
        {
            const float Tau = (float)(Math.PI * 2);
            var a = UiShapeParams.NormaliseArc(-0.5f, 0.5f);
            Assert.Equal(Tau - 0.5f, a.Start, 4);
            Assert.Equal(1f, a.Sweep, 4);
            var wrap = UiShapeParams.NormaliseArc(1f, 0.5f);
            Assert.Equal(Tau - 0.5f, wrap.Sweep, 4);
            var full = UiShapeParams.NormaliseArc(0f, 10f);
            Assert.Equal(Tau, full.Sweep, 4);
        }
    }
}
