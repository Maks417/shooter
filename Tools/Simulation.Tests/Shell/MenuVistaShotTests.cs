using System;
using Shooter.Generation.Environment;
using Shooter.Presentation.Menus;
using Xunit;

/// <summary>ADR-123 menu vista: every registered map has a reviewed shot that sits below and beyond the overlook.</summary>
public sealed class MenuVistaShotTests
{
    [Fact]
    public void Every_registered_map_has_a_reviewed_shot()
    {
        foreach (string id in MapRegistry.MapIds)
            Assert.True(MenuVistaShot.HasReviewedShot(id), id + " needs a MenuVistaShot entry.");
    }

    [Fact]
    public void Map_lies_below_the_roof_and_beyond_its_edge()
    {
        foreach (string id in MapRegistry.MapIds)
        {
            var shot = MenuVistaShot.For(id);
            float half = shot.RotatedHalfDepth(60f, 45f);
            var anchor = shot.Anchor(half);
            Assert.True(anchor.Y <= -3f && anchor.Y >= -8f, id + " drop keeps the map visible past the roof edge.");
            Assert.True(anchor.Z - half > MenuVistaShot.RoofEdgeZ, id + " near edge is beyond the roof.");
            // The overlook camera (eye 1.7 m above the roof at z -7.5, MenuStagingComposition) must see the map's far half over the roof edge.
            double roofAngle = Math.Atan2(1.7, MenuVistaShot.RoofEdgeZ + 7.5);
            double farAngle = Math.Atan2(1.7 - anchor.Y, anchor.Z + half + 7.5);
            Assert.True(farAngle < roofAngle, id + " far ground is above the roof line.");
        }
    }

    [Fact]
    public void Rotated_half_depth_matches_the_axis_cases()
    {
        Assert.Equal(45f, new MenuVistaShot("x", 0f, 4f, 4f, 0f).RotatedHalfDepth(60f, 45f), 3);
        Assert.Equal(60f, new MenuVistaShot("x", 90f, 4f, 4f, 0f).RotatedHalfDepth(60f, 45f), 3);
    }

    [Fact]
    public void Low_tier_renders_the_vista_at_half_rate()
    {
        Assert.Equal(15f, MenuVistaShot.RenderHz(0));
        Assert.Equal(30f, MenuVistaShot.RenderHz(1));
        Assert.Equal(30f, MenuVistaShot.RenderHz(2));
    }
}
