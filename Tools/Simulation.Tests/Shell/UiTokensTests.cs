using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Generation.Icons;
using Shooter.Presentation.Style;
using Xunit;

/// <summary>ADR-123 design tokens: contrast, team separation, emblem agreement and no colour literals in UI code.</summary>
public sealed class UiTokensTests
{
    [Theory]
    [InlineData(nameof(UiTokens.Text))]
    [InlineData(nameof(UiTokens.TextMuted))]
    [InlineData(nameof(UiTokens.Accent))]
    [InlineData(nameof(UiTokens.Ally))]
    [InlineData(nameof(UiTokens.Enemy))]
    public void Text_roles_meet_wcag_aa_on_every_surface(string role)
    {
        var ink = Token(role);
        foreach (var surface in new[] { UiTokens.Base, UiTokens.Panel, UiTokens.PanelTop, UiTokens.Raised })
            Assert.True(UiTokens.Contrast(ink, surface) >= 4.5, role + " on " + surface + " is " + UiTokens.Contrast(ink, surface).ToString("0.00"));
    }

    [Fact]
    public void Primary_button_ink_reads_on_the_accent()
    {
        Assert.True(UiTokens.Contrast(UiTokens.Base, UiTokens.Accent) >= 4.5);
        Assert.True(UiTokens.Contrast(UiTokens.Base, UiTokens.AccentDeep) >= 4.5);
    }

    [Fact]
    public void Text_reads_on_hud_plates_over_a_bright_scene()
    {
        // Worst case: the translucent plate over a near-white sky.
        var bright = new UiRgba(235, 238, 240);
        Assert.True(UiTokens.Contrast(UiTokens.Text, UiTokens.Over(UiTokens.HudPlateStrong, bright)) >= 3.0);
        Assert.True(UiTokens.Contrast(UiTokens.Text, UiTokens.Over(UiTokens.HudPlate, UiTokens.Over(UiTokens.HudPlate, bright))) >= 3.0);
    }

    [Fact]
    public void Ally_and_enemy_differ_in_luminance_not_only_hue()
    {
        // Deuteranopes lose the red/green axis; the pair must still separate by lightness (shape carries the rest).
        double ally = UiTokens.Ally.RelativeLuminance, enemy = UiTokens.Enemy.RelativeLuminance;
        Assert.True(Math.Abs(ally - enemy) >= 0.1, "ally " + ally.ToString("0.000") + " enemy " + enemy.ToString("0.000"));
    }

    [Fact]
    public void Brand_emblem_colours_are_the_ui_tokens()
    {
        AssertSame(UiTokens.Accent, BrandEmblem.Accent);
        AssertSame(UiTokens.AccentBright, BrandEmblem.AccentBright);
        AssertSame(UiTokens.AccentDeep, BrandEmblem.AccentDeep);
        AssertSame(UiTokens.PanelTop, BrandEmblem.TileTop);
        AssertSame(UiTokens.Shade, BrandEmblem.TileBottom);
        AssertSame(UiTokens.Edge, BrandEmblem.TileEdge);
    }

    private static readonly string[] GuardedFiles =
    {
        "TacticalUi.cs", "CareerArt.cs", "ProductSessionView.Hud.cs", "ProductShellHost.cs", "ProductShellHost.Menus.cs",
        "ProductShellHost.Armory.cs", "ProductShellHost.Career.cs", "ProductShellHost.Settings.cs", "ProductShellHost.Results.cs",
        "BootLoadingHost.cs", "ContentRecoveryHost.cs"
    };

    [Fact]
    public void Ui_code_writes_no_colour_literals()
    {
        // A literal is `new Color(` / `new Color32(` whose first argument is a number. Colours derived from a token
        // (`new Color(c.r, ...)`) are allowed; every base value must come from UiTokens through UiInk.
        var literal = new Regex(@"new\s+Color(32)?\s*\(\s*[-+.\d]");
        string session = Path.Combine(RepoRoot(), "UnityProject", "Assets", "Shooter", "Presentation", "Session");
        var offenders = GuardedFiles
            .SelectMany(file => File.ReadAllLines(Path.Combine(session, file)).Select((line, i) => (file, line, i)))
            .Where(x => literal.IsMatch(x.line))
            .Select(x => x.file + ":" + (x.i + 1) + "  " + x.line.Trim())
            .ToArray();
        Assert.True(offenders.Length == 0, "Colour literals outside UiTokens:\n" + string.Join("\n", offenders));
    }

    private static void AssertSame(UiRgba token, double[] rgb)
    {
        Assert.Equal(token.R, (int)Math.Round(rgb[0] * 255));
        Assert.Equal(token.G, (int)Math.Round(rgb[1] * 255));
        Assert.Equal(token.B, (int)Math.Round(rgb[2] * 255));
    }

    private static UiRgba Token(string name) =>
        (UiRgba)typeof(UiTokens).GetField(name)!.GetValue(null)!;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "toolchain-lock.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
