using System;
using System.Security.Cryptography;
using Shooter.Generation.Icons;
using Xunit;

/// <summary>Analytic executable icon: deterministic, transparent outside the rounded tile, opaque reticle centre.</summary>
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

    [Fact]
    public void Corners_are_transparent_and_centre_is_opaque_red_pip()
    {
        int s = AppIcon.DefaultSize;
        var px = AppIcon.Rgba(s);
        Assert.Equal(0, px[3]);                                 // bottom-left
        Assert.Equal(0, px[((s - 1) * s + (s - 1)) * 4 + 3]);   // top-right
        int c = ((s / 2) * s + s / 2) * 4;
        Assert.Equal(255, px[c + 3]);
        Assert.True(px[c] > 180 && px[c + 1] < 120, "centre pip is red");
    }

    [Fact]
    public void Smaller_sizes_render()
    {
        foreach (int size in new[] { 16, 32, 48, 64, 128 })
            Assert.Equal(size * size * 4, AppIcon.Rgba(size).Length);
    }
}
