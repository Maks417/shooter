#nullable enable
using Shooter.Presentation.Style;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>ADR-123 bridge from pure <see cref="UiTokens"/> to Unity colours. The only place UI colours are built.</summary>
    public static class UiInk
    {
        public static Color Of(UiRgba c) => new Color32(c.R, c.G, c.B, c.A);

        /// <summary>Token with its alpha multiplied by <paramref name="alpha"/>.</summary>
        public static Color Of(UiRgba c, float alpha) => A(Of(c), alpha);

        /// <summary>Colour with its alpha multiplied by <paramref name="alpha"/>.</summary>
        public static Color A(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        /// <summary>Colour with its alpha replaced by <paramref name="alpha"/>.</summary>
        public static Color Alpha(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        /// <summary>Colour scaled toward black, alpha kept.</summary>
        public static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>White hairline/highlight at <paramref name="alpha"/>.</summary>
        public static Color Light(float alpha) => new Color(1f, 1f, 1f, alpha);

        /// <summary>Black shadow/scrim at <paramref name="alpha"/>.</summary>
        public static Color Dark(float alpha) => new Color(0f, 0f, 0f, alpha);

        /// <summary>Darkest charcoal (underlays, scrims) at <paramref name="alpha"/>.</summary>
        public static Color Shade(float alpha) => Of(UiTokens.Shade, alpha);

        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
    }
}
