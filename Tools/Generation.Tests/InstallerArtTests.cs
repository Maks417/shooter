using System;
using System.Security.Cryptography;
using Shooter.Generation.Icons;
using Xunit;

/// <summary>ADR-123 generated installer art: valid ICO/BMP containers, deterministic bytes.</summary>
public sealed class InstallerArtTests
{
    [Fact]
    public void Ico_lists_every_icon_size_with_valid_offsets()
    {
        var ico = InstallerArt.Ico();
        Assert.Equal(0, BitConverter.ToUInt16(ico, 0));
        Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
        int count = BitConverter.ToUInt16(ico, 4);
        Assert.Equal(AppIcon.Sizes.Length, count);
        for (int i = 0; i < count; i++)
        {
            int entry = 6 + i * 16;
            int size = ico[entry] == 0 ? 256 : ico[entry];
            Assert.Equal(AppIcon.Sizes[i], size);
            int bytes = BitConverter.ToInt32(ico, entry + 8);
            int offset = BitConverter.ToInt32(ico, entry + 12);
            Assert.True(offset + bytes <= ico.Length);
            Assert.Equal(40, BitConverter.ToInt32(ico, offset));
            Assert.Equal(size * 2, BitConverter.ToInt32(ico, offset + 8));
        }
    }

    [Theory]
    [InlineData(true, InstallerArt.WizardWidth, InstallerArt.WizardHeight)]
    [InlineData(false, InstallerArt.SmallSize, InstallerArt.SmallSize)]
    public void Wizard_bitmaps_are_24_bit_bmp(bool large, int width, int height)
    {
        var bmp = large ? InstallerArt.WizardImage() : InstallerArt.WizardSmallImage();
        Assert.Equal((byte)'B', bmp[0]);
        Assert.Equal((byte)'M', bmp[1]);
        Assert.Equal(bmp.Length, BitConverter.ToInt32(bmp, 2));
        Assert.Equal(width, BitConverter.ToInt32(bmp, 18));
        Assert.Equal(height, BitConverter.ToInt32(bmp, 22));
        Assert.Equal(24, BitConverter.ToUInt16(bmp, 28));
    }

    [Fact]
    public void Art_is_deterministic()
    {
        using var sha = SHA256.Create();
        Assert.Equal(sha.ComputeHash(InstallerArt.Ico()), sha.ComputeHash(InstallerArt.Ico()));
        Assert.Equal(sha.ComputeHash(InstallerArt.WizardImage()), sha.ComputeHash(InstallerArt.WizardImage()));
    }
}
