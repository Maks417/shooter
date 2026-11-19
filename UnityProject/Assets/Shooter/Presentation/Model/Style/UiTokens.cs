#nullable enable
using System;

namespace Shooter.Presentation.Style
{
    /// <summary>One sRGB colour token (0..255 per channel, straight alpha).</summary>
    public readonly struct UiRgba : IEquatable<UiRgba>
    {
        public UiRgba(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        /// <summary>WCAG 2.x relative luminance of the opaque colour.</summary>
        public double RelativeLuminance => 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);

        public string Hex => "#" + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");

        private static double Linear(byte channel)
        {
            double c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        public bool Equals(UiRgba other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object? obj) => obj is UiRgba other && Equals(other);
        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;
        public override string ToString() => Hex + (A == 255 ? "" : "/" + A);
    }

    /// <summary>
    /// ADR-123 single source of every interface colour. Screens, HUD, career art and the app icon read roles from here;
    /// UI code never writes a colour literal (enforced by <c>UiLiteralGuardTests</c>). Values are sRGB.
    /// </summary>
    public static class UiTokens
    {
        // Surfaces, darkest to lightest. Base is the shell floor; Panel/PanelTop form the plate gradient.
        public static readonly UiRgba Shade = new UiRgba(5, 8, 9);
        public static readonly UiRgba Well = new UiRgba(10, 14, 16);
        public static readonly UiRgba Base = new UiRgba(18, 23, 26);
        public static readonly UiRgba Panel = new UiRgba(28, 36, 41);
        public static readonly UiRgba PanelTop = new UiRgba(35, 44, 49);
        public static readonly UiRgba Raised = new UiRgba(42, 52, 57);
        public static readonly UiRgba RaisedHover = new UiRgba(56, 68, 74);
        public static readonly UiRgba Edge = new UiRgba(54, 66, 72);

        // Text.
        public static readonly UiRgba Text = new UiRgba(241, 243, 239);
        public static readonly UiRgba TextMuted = new UiRgba(173, 183, 183);
        public static readonly UiRgba TextDim = new UiRgba(118, 128, 130);
        public static readonly UiRgba ThumbShade = new UiRgba(206, 210, 204);

        // Brand accent (focus, selection, rails) and its ramp.
        public static readonly UiRgba Accent = new UiRgba(230, 197, 106);
        public static readonly UiRgba AccentBright = new UiRgba(255, 230, 158);
        public static readonly UiRgba AccentDeep = new UiRgba(196, 160, 72);
        public static readonly UiRgba AccentShadow = new UiRgba(150, 108, 42);

        // Teams and status. Team identity is always paired with shape or text.
        public static readonly UiRgba Ally = new UiRgba(123, 197, 209);
        public static readonly UiRgba Enemy = new UiRgba(230, 128, 116);
        public static readonly UiRgba Armor = new UiRgba(158, 189, 219);
        public static readonly UiRgba ReticleRed = new UiRgba(255, 31, 20);

        // Combat HUD plates: translucent charcoal so the world stays readable behind them.
        public static readonly UiRgba HudPlate = new UiRgba(9, 12, 14, 128);
        public static readonly UiRgba HudPlateStrong = new UiRgba(8, 11, 13, 166);
        public static readonly UiRgba HudBarWell = new UiRgba(13, 18, 20, 178);
        public static readonly UiRgba HudFeature = new UiRgba(140, 161, 166);
        public static readonly UiRgba HudRangeRing = new UiRgba(153, 184, 191);

        // Medals and enamels (kill ribbon, awards).
        public static readonly UiRgba MedalHighlight = new UiRgba(250, 230, 168);
        public static readonly UiRgba EnamelRedTop = new UiRgba(122, 42, 36);
        public static readonly UiRgba EnamelRedBottom = new UiRgba(70, 20, 18);
        public static readonly UiRgba EnamelGreenTop = new UiRgba(30, 62, 52);
        public static readonly UiRgba EnamelGreenBottom = new UiRgba(16, 34, 30);

        // Map blueprint (insertion screen) and decorative contour lines.
        public static readonly UiRgba Blueprint = new UiRgba(64, 166, 184);
        public static readonly UiRgba BlueprintFill = new UiRgba(38, 122, 143);
        public static readonly UiRgba BlueprintScan = new UiRgba(128, 230, 255);
        public static readonly UiRgba Contour = new UiRgba(51, 69, 71);

        // Subtitles (warm caption ink and its soft glow).
        public static readonly UiRgba Caption = new UiRgba(255, 235, 173);
        public static readonly UiRgba CaptionGlow = new UiRgba(255, 217, 115);

        // Career reward kinds (ADR-113) and locked reward ink.
        public static readonly UiRgba RewardWeapon = new UiRgba(236, 168, 84);
        public static readonly UiRgba RewardSight = new UiRgba(108, 200, 224);
        public static readonly UiRgba RewardBuff = new UiRgba(132, 212, 140);
        public static readonly UiRgba RewardKit = new UiRgba(176, 186, 190);
        public static readonly UiRgba BuffWell = new UiRgba(13, 23, 15);
        public static readonly UiRgba LockedTop = new UiRgba(117, 128, 133);
        public static readonly UiRgba LockedBottom = new UiRgba(66, 74, 79);
        public static readonly UiRgba RewardIconBase = new UiRgba(158, 168, 173);

        // Rank band metals: metal top, metal bottom, enamel top, enamel bottom.
        public static readonly UiRgba[] BandEnlisted = { new UiRgba(240, 226, 164), new UiRgba(148, 126, 68), new UiRgba(50, 60, 42), new UiRgba(20, 26, 18) };
        public static readonly UiRgba[] BandSilver = { new UiRgba(244, 247, 249), new UiRgba(124, 136, 146), new UiRgba(36, 50, 68), new UiRgba(14, 20, 32) };
        public static readonly UiRgba[] BandBronze = { new UiRgba(240, 186, 124), new UiRgba(126, 74, 38), new UiRgba(64, 32, 26), new UiRgba(26, 12, 10) };
        public static readonly UiRgba[] BandGeneral = { new UiRgba(255, 234, 152), new UiRgba(164, 108, 32), new UiRgba(104, 24, 28), new UiRgba(42, 8, 12) };

        // Diagnostic overlays only (kill volumes on the escape HUD).
        public static readonly UiRgba DebugKill = new UiRgba(255, 64, 51);
        public static readonly UiRgba DebugWarn = new UiRgba(255, 230, 51);

        /// <summary>WCAG contrast ratio between two opaque colours (1..21).</summary>
        public static double Contrast(UiRgba a, UiRgba b)
        {
            double la = a.RelativeLuminance, lb = b.RelativeLuminance;
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        /// <summary>Straight-alpha source-over of <paramref name="top"/> onto opaque <paramref name="bottom"/>.</summary>
        public static UiRgba Over(UiRgba top, UiRgba bottom)
        {
            double a = top.A / 255.0;
            byte Mix(byte t, byte b) => (byte)Math.Round(t * a + b * (1 - a));
            return new UiRgba(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));
        }
    }

    /// <summary>ADR-123 type scale (logical px at 100% UI scale). Bold from <see cref="BoldFrom"/> up (ADR-066 bank).</summary>
    public static class UiType
    {
        public const int Display = 56;
        public const int Title = 32;
        public const int Heading = 22;
        public const int Lead = 18;
        public const int Body = 16;
        public const int Small = 14;
        public const int Caption = 12;
        public const int Micro = 10;

        public const int Button = 18;
        public const int NavButton = 20;
        public const int BoldFrom = Heading;
    }

    /// <summary>ADR-123 frame kinds: one plate language at four weights.</summary>
    public enum FrameKind : byte
    {
        /// <summary>Default content plate.</summary>
        Plate = 0,
        /// <summary>Dark media well (map cards, previews).</summary>
        Card = 1,
        /// <summary>Translucent feature panel over the vista, with corner brackets.</summary>
        Hero = 2,
        /// <summary>Opaque dialog with corner brackets and a deep shadow.</summary>
        Modal = 3,
    }

    /// <summary>ADR-123 spacing and frame anatomy (logical px).</summary>
    public static class UiSpace
    {
        public const float Xs = 4f;
        public const float S = 8f;
        public const float M = 12f;
        public const float L = 16f;
        public const float Xl = 24f;
        public const float Xxl = 32f;

        /// <summary>Corner radius of every frame: hard-edged, just enough to anti-alias.</summary>
        public const float FrameRadius = 2f;
        /// <summary>Arm length of the amber corner brackets on hero and modal frames.</summary>
        public const float Bracket = 12f;
        /// <summary>Accent rail thickness on frames and focused controls.</summary>
        public const float Rail = 3f;
        /// <summary>Height of the shared screen header (eyebrow, title, rule).</summary>
        public const float HeaderHeight = 62f;
        /// <summary>Height of the shared key-hint footer.</summary>
        public const float FooterHeight = 28f;
        /// <summary>Smallest interactive target.</summary>
        public const float MinTarget = 32f;
    }
}
