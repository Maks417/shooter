#nullable enable
using Shooter.Presentation.Hud;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    public sealed class HudLayoutTests
    {
        [Fact]
        public void Layout_fits_720p_at_150_percent_scale_without_clipping()
        {
            var hud = new HudLayout(new HudSettings(1.5f, 80f, "Low", true, 1.2f));
            Assert.True(hud.LayoutFitsReference());
            Assert.True(hud.Settings.ReducedMotion);
            Assert.Contains("scale=1.5", hud.SerializeSettings());
        }

        [Fact]
        public void Focus_cycles_and_rebind_rejects_duplicates()
        {
            var hud = new HudLayout();
            hud.CycleFocus(1);
            Assert.Equal(HudFocusTarget.Settings, hud.Focus);
            Assert.True(hud.TryRebind("Jump", "F"));
            Assert.False(hud.TryRebind("Reload", "F"));
            Assert.Equal("F", hud.Bindings["Jump"]);
        }

        [Fact]
        public void Quality_profile_change_does_not_alter_layout_geometry_contract()
        {
            var low = new HudLayout(new HudSettings(1f, 80f, "Low", false, 1f));
            var high = new HudLayout(new HudSettings(1f, 80f, "High", false, 1f));
            Assert.Equal(low.BuildLayout().Count, high.BuildLayout().Count);
            Assert.Equal(low.BuildLayout()[0].Width, high.BuildLayout()[0].Width);
        }
    }
}
