using System;
using System.Security.Cryptography;
using Shooter.Generation.Icons;
using Xunit;

/// <summary>ADR-123 analytic brand icon: deterministic, transparent outside the tile, amber emblem on charcoal.</summary>
public sealed class AppIconTests
{
    [Fact]
    public void Icon_is_deterministic_rgba()
    {
        var a = AppIcon.Rgba();
        var b = AppIcon.Rgba();
        Assert.Equal(AppIcon.DefaultSize * AppIcon.DefaultSize * 4, a.Length);
        using var sha = SHA256.Create();
        Assert.Equal(Convert.ToBase64String(sha.ComputeHash(a)), Convert.ToBase64String(sha.ComputeHash(b)));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void Every_size_has_clear_corners_charcoal_centre_and_amber_ring(int s)
    {
        var px = AppIcon.Rgba(s);
        Assert.Equal(0, px[3]);                                 // bottom-left
        Assert.Equal(0, px[((s - 1) * s + (s - 1)) * 4 + 3]);   // top-right
        // Inside the ring, below the chevron (icon units y = 700): plain charcoal tile.
        int row = s - 1 - (int)(s * 0.70);
        int c = (row * s + s / 2) * 4;
        Assert.Equal(255, px[c + 3]);
        if (s >= 32) Assert.True(px[c] < 90 && px[c + 1] < 90 && px[c + 2] < 90, "tile is charcoal inside the ring at " + s + " px");
        // Left edge of the ring on the horizontal centre line: the W tick and ring are amber (R > G > B, bright).
        int x = (int)Math.Round(s * (BrandEmblem.Centre - BrandEmblem.RingRadiusUnits) / 1000.0);
        int o = ((s / 2) * s + x) * 4;
        Assert.True(px[o] > 150 && px[o] > px[o + 2] + 60, "ring is amber at " + s + " px");
    }

    [Fact]
    public void Top_down_rows_mirror_bottom_up_rows()
    {
        const int s = 32;
        var up = AppIcon.Rgba(s);
        var down = AppIcon.RgbaTopDown(s);
        for (int y = 0; y < s; y++)
            for (int i = 0; i < s * 4; i++)
                Assert.Equal(up[y * s * 4 + i], down[(s - 1 - y) * s * 4 + i]);
    }
}
