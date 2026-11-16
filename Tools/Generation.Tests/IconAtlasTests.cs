using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Generation.Icons;
using Xunit;
using Xunit.Abstractions;

/// <summary>ADR-085 procedural SDF icon atlas: deterministic glyphs and mesh-traced weapon silhouettes.</summary>
public sealed class IconAtlasTests
{
    private readonly ITestOutputHelper output;
    public IconAtlasTests(ITestOutputHelper output) { this.output = output; }

    /// <summary>Same inputs the player receives from the weapon package: LOD0 plus detachable parts.</summary>
    internal static List<WeaponSilhouetteSource> GeneratedWeapons()
    {
        var list = new List<WeaponSilhouetteSource>();
        foreach (var source in WeaponMeshCatalog.All)
        {
            MeshData[] meshes;
            if (source.Recipe != null)
            {
                var build = WeaponGeometry.Build(source.Recipe);
                meshes = new[] { build.Lods[0], build.Magazine, build.CyclePart };
            }
            else meshes = new[] { source.BuildPropLod(0) };
            list.Add(new WeaponSilhouetteSource(source.MeshKey, WeaponSilhouetteSource.SpanFor(source.MeshKey), meshes));
        }
        return list;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void Dump(IconAtlas atlas, string name)
    {
        string? dir = Environment.GetEnvironmentVariable("SHOOTER_ICON_DUMP");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        using var f = File.Create(Path.Combine(dir, name + ".pgm"));
        var header = System.Text.Encoding.ASCII.GetBytes("P5\n" + atlas.Width + " " + atlas.Height + "\n255\n");
        f.Write(header);
        f.Write(atlas.Pixels);
    }

    [Fact]
    public void Library_names_are_unique_and_every_glyph_has_ink_and_a_clear_border()
    {
        var atlas = IconAtlas.Build(IconLibrary.All);
        Dump(atlas, "library");
        Assert.Equal(IconLibrary.All.Count, atlas.Names.Count);
        foreach (var recipe in IconLibrary.All)
        {
            Assert.True(atlas.TryGet(recipe.Name, out var e));
            int inside = 0, borderInk = 0;
            for (int y = 0; y < e.Height; y++)
                for (int x = 0; x < e.Width; x++)
                {
                    byte v = atlas.Pixels[(e.Y + y) * atlas.Width + e.X + x];
                    if (v > 128) inside++;
                    bool border = x == 0 || y == 0 || x == e.Width - 1 || y == e.Height - 1;
                    if (border && v > 128) borderInk++;
                }
            Assert.True(inside > 120, recipe.Name + " has visible ink (" + inside + " px)");
            Assert.True(inside < e.Width * e.Height * 3 / 4, recipe.Name + " is not a filled block");
            Assert.Equal(0, borderInk);
        }
    }

    [Fact]
    public void Encoding_puts_the_edge_at_128_and_saturates_beyond_the_spread()
    {
        Assert.InRange(IconAtlas.Encode(0), 127, 128);
        Assert.Equal(255, IconAtlas.Encode(-IconAtlas.SpreadPixels));
        Assert.Equal(0, IconAtlas.Encode(IconAtlas.SpreadPixels));
        Assert.True(IconAtlas.Encode(-2) > IconAtlas.Encode(2));
    }

    [Fact]
    public void Shapes_have_exact_signed_distances()
    {
        var circle = IconShape.Circle(500, 500, 100);
        Assert.Equal(-100, circle.Distance(500, 500), 9);
        Assert.Equal(50, circle.Distance(650, 500), 9);
        var box = IconShape.Box(100, 100, 200, 100);
        Assert.Equal(-50, box.Distance(200, 150), 9);
        Assert.Equal(10, box.Distance(310, 150), 9);
        var tri = IconShape.Polygon(0, 0, 100, 0, 0, 100);
        Assert.True(tri.Distance(20, 20) < 0);
        Assert.True(tri.Distance(90, 90) > 0);
        var cut = new IconRecipe("cut").Add(IconShape.Circle(500, 500, 300)).Cut(IconShape.Circle(500, 500, 100));
        Assert.True(cut.Distance(500, 500) > 0, "subtraction leaves a hole");
        Assert.True(cut.Distance(500, 300) < 0);
    }

    [Fact]
    public void Weapon_silhouettes_follow_the_mesh_proportions()
    {
        var weapons = GeneratedWeapons();
        var atlas = IconAtlas.Build(IconLibrary.All, weapons);
        Dump(atlas, "full");
        foreach (var w in weapons)
        {
            Assert.True(atlas.TryGet(WeaponSilhouetteSource.IconName(w.MeshKey), out var e));
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1, ink = 0;
            for (int y = 0; y < e.Height; y++)
                for (int x = 0; x < e.Width; x++)
                    if (atlas.Pixels[(e.Y + y) * atlas.Width + e.X + x] > 128)
                    {
                        ink++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    }
            Assert.True(ink > 60, w.MeshKey + " silhouette has ink");
            double inkAspect = (maxX - minX + 1) / (double)(maxY - minY + 1);
            output.WriteLine(w.MeshKey + " ink aspect " + inkAspect.ToString("0.00") + " in " + e.Width + "x" + e.Height);
            if (w.MeshKey.StartsWith("rifle", StringComparison.Ordinal)) Assert.True(inkAspect > 2.2, "long gun reads long");
            if (w.MeshKey == "grenade") Assert.InRange(inkAspect, 0.5, 1.6);
        }
    }

    [Fact]
    public void Atlas_is_deterministic_and_matches_the_golden()
    {
        var a = IconAtlas.Build(IconLibrary.All, GeneratedWeapons());
        var b = IconAtlas.Build(IconLibrary.All, GeneratedWeapons());
        Assert.Equal(Hash(a.Pixels), Hash(b.Pixels));
        string library = Hash(IconAtlas.Build(IconLibrary.All).Pixels);
        output.WriteLine("library " + library);
        output.WriteLine("full " + Hash(a.Pixels) + " " + a.Width + "x" + a.Height);
        Assert.Equal(IconAtlasGoldens.Library, library);
    }
}

/// <summary>Shared with <c>IconAtlasEditModeTests</c> so the Unity Mono lane proves the same bytes.</summary>
internal static class IconAtlasGoldens
{
    public const string Library = "22964e2db82744ad8eb33c113c4c32b6468eae2112cae07c489b470129e798f0";
}
