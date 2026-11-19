#nullable enable
using Shooter.Contracts;
using Shooter.Game.Settings;
using Shooter.Generation.Icons;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Style;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// ADR-113 career art: layered metal rank emblems, recessed progress bars and reward cards, all drawn from the
    /// procedural glyph atlas with the code-only UiSdf shader (gradients, strokes, shadows, glow and glint). No
    /// texture is loaded. When the shader is unavailable each primitive falls back to flat fills and plain glyphs.
    /// </summary>
    public static class CareerArt
    {
        /// <summary>Metal and enamel colours for one rank band.</summary>
        public readonly struct BandPalette
        {
            public readonly Color MetalTop, MetalBottom, EnamelTop, EnamelBottom;

            public BandPalette(Color metalTop, Color metalBottom, Color enamelTop, Color enamelBottom)
            {
                MetalTop = metalTop;
                MetalBottom = metalBottom;
                EnamelTop = enamelTop;
                EnamelBottom = enamelBottom;
            }
        }

        public enum RewardState : byte { Locked = 0, Next = 1, Earned = 2 }

        // ADR-123: rank band metals and reward inks are UiTokens roles.
        private static BandPalette Band(UiRgba[] b) => new BandPalette(UiInk.Of(b[0]), UiInk.Of(b[1]), UiInk.Of(b[2]), UiInk.Of(b[3]));
        private static readonly BandPalette Enlisted = Band(UiTokens.BandEnlisted);
        private static readonly BandPalette Silver = Band(UiTokens.BandSilver);
        private static readonly BandPalette Bronze = Band(UiTokens.BandBronze);
        private static readonly BandPalette General = Band(UiTokens.BandGeneral);

        public static readonly Color WeaponInk = UiInk.Of(UiTokens.RewardWeapon);
        public static readonly Color SightInk = UiInk.Of(UiTokens.RewardSight);
        public static readonly Color BuffInk = UiInk.Of(UiTokens.RewardBuff);
        public static readonly Color KitInk = UiInk.Of(UiTokens.RewardKit);

        public static BandPalette Palette(int rank)
        {
            switch (CareerLadder.Get(rank).Band)
            {
                case CareerRankBand.General: return General;
                case CareerRankBand.Bronze: return Bronze;
                case CareerRankBand.Warrant:
                case CareerRankBand.Officer: return Silver;
                default: return Enlisted;
            }
        }

        public static Color KindInk(CareerRewardKind kind)
        {
            switch (kind)
            {
                case CareerRewardKind.Weapon: return WeaponInk;
                case CareerRewardKind.Sight: return SightInk;
                case CareerRewardKind.Buff: return BuffInk;
                case CareerRewardKind.Final: return TacticalUi.Gold;
                default: return KitInk;
            }
        }

        public static string KindLabel(CareerRewardKind kind)
        {
            switch (kind)
            {
                case CareerRewardKind.Weapon: return ShellUiCopy.CareerRewardWeapon;
                case CareerRewardKind.Sight: return ShellUiCopy.CareerRewardSight;
                case CareerRewardKind.Buff: return ShellUiCopy.CareerRewardBuff;
                case CareerRewardKind.Final: return ShellUiCopy.CareerRewardFinal;
                default: return ShellUiCopy.CareerRewardKit;
            }
        }

        private static string Plate(CareerRankBand band)
        {
            switch (band)
            {
                case CareerRankBand.General: return IconLibrary.CareerPlateGeneral;
                case CareerRankBand.Enlisted: return IconLibrary.CareerPlateEnlisted;
                default: return IconLibrary.CareerPlateOfficer;
            }
        }

        private static Color A(Color c, float alpha) => UiInk.A(c, alpha);

        /// <summary>Desaturated, darkened copy for locked art.</summary>
        public static Color Dim(Color c)
        {
            float l = c.r * .3f + c.g * .59f + c.b * .11f;
            return new Color(Mathf.Lerp(l, c.r, .2f) * .55f, Mathf.Lerp(l, c.g, .2f) * .55f, Mathf.Lerp(l, c.b, .2f) * .55f, c.a);
        }

        private static Rect Square(Rect r, float scale = 1f, float dy = 0f)
        {
            float s = Mathf.Min(r.width, r.height) * scale;
            return new Rect(r.center.x - s * .5f, r.center.y - s * .5f + dy, s, s);
        }

        private static Rect Offset(Rect r, float dx, float dy) => new Rect(r.x + dx, r.y + dy, r.width, r.height);

        /// <summary>
        /// Rank emblem. At 34 px and up: enamel plate with a metal rim and inner hairline, the insignia embossed in
        /// metal on top (dark offset under a vertical metal gradient and a light edge), a laurel wreath for generals.
        /// Smaller sizes draw the embossed insignia only so it stays legible on the scoreboards. False when the
        /// glyph is unavailable (callers keep their text).
        /// </summary>
        public static bool RankEmblem(Rect r, int rank, float alpha, float glint = -1f, bool dim = false)
        {
            if (rank <= 0 || alpha <= 0.01f) return false;
            var band = CareerLadder.Get(rank).Band;
            var pal = Palette(rank);
            Color top = dim ? Dim(pal.MetalTop) : pal.MetalTop, bottom = dim ? Dim(pal.MetalBottom) : pal.MetalBottom;
            string insignia = IconLibrary.CareerRank(rank);
            if (!UiSdf.Available) return TacticalUi.Icon(r, insignia, A(top, alpha), shadow: alpha > .5f);
            Rect sq = Square(r);
            float s = sq.width;
            if (s < 34f)
            {
                float depth = Mathf.Max(1f, s * .05f);
                UiSdf.Icon(Offset(sq, 0f, depth), insignia, UiSdf.Style.Solid(UiInk.Dark(.7f * alpha)).WithShadow(.4f * alpha, 3f, 1f));
                return UiSdf.Icon(sq, insignia, UiSdf.Style.Solid(A(top, alpha)).WithGradient(A(bottom, alpha), UiSdf.Gradient.Vertical));
            }

            Rect plate = sq;
            if (band == CareerRankBand.General)
            {
                UiSdf.Icon(Offset(sq, 0f, s * .02f), IconLibrary.CareerWreath, UiSdf.Style.Solid(UiInk.Dark(.55f * alpha)));
                UiSdf.Icon(sq, IconLibrary.CareerWreath, UiSdf.Style.Solid(A(top, alpha)).WithGradient(A(bottom, alpha), UiSdf.Gradient.Vertical)
                    .WithShadow(.4f * alpha, s * .06f, s * .02f));
                plate = Square(sq, .7f, -s * .04f);
            }
            string plateName = Plate(band);
            float ps = plate.width;
            Color enamelTop = dim ? Dim(pal.EnamelTop) : pal.EnamelTop, enamelBottom = dim ? Dim(pal.EnamelBottom) : pal.EnamelBottom;
            // Enamel body with a soft drop shadow.
            UiSdf.Icon(plate, plateName, UiSdf.Style.Solid(A(enamelTop, alpha)).WithGradient(A(enamelBottom, alpha), UiSdf.Gradient.Vertical)
                .WithShadow(.55f * alpha, ps * .1f, ps * .045f));
            // Metal rim and an inner hairline for a pressed, two-step bevel.
            var rim = UiSdf.Style.Solid(A(top, alpha)).WithGradient(A(bottom, alpha), UiSdf.Gradient.Vertical);
            rim.Stroke = Mathf.Max(1.5f, ps * .05f);
            UiSdf.Icon(plate, plateName, rim);
            var hair = UiSdf.Style.Solid(UiInk.Light(.16f * alpha));
            hair.Stroke = 1f;
            UiSdf.Icon(Square(plate, .86f), plateName, hair);
            // Embossed insignia: dark offset, metal body, light edge, optional glint.
            Rect ins = Square(plate, band == CareerRankBand.General ? .5f : .56f, band == CareerRankBand.Enlisted ? -ps * .03f : 0f);
            float depthPx = Mathf.Max(1f, ps * .025f);
            UiSdf.Icon(Offset(ins, 0f, depthPx), insignia, UiSdf.Style.Solid(UiInk.Dark(.75f * alpha)));
            var body = UiSdf.Style.Solid(A(top, alpha)).WithGradient(A(bottom, alpha), UiSdf.Gradient.Vertical);
            if (glint >= 0f) body = body.WithGlint(glint, .75f * alpha);
            UiSdf.Icon(ins, insignia, body);
            var edge = UiSdf.Style.Solid(UiInk.Of(UiTokens.Text, .32f * alpha));
            edge.Stroke = Mathf.Max(.8f, ps * .012f);
            UiSdf.Icon(Offset(ins, 0f, -.5f), insignia, edge);
            return true;
        }

        /// <summary>
        /// Recessed progress bar: dark groove, metal fill with a bright leading edge, segment ticks, and an optional
        /// gain segment from <paramref name="gainFrom01"/> to <paramref name="fill01"/> in the brighter accent.
        /// </summary>
        public static void ProgressBar(Rect r, float fill01, Color top, Color bottom, int ticks, float alpha, float gainFrom01 = -1f)
        {
            fill01 = Mathf.Clamp01(fill01);
            float radius = Mathf.Min(3f, r.height * .5f);
            if (!UiSdf.Available)
            {
                TacticalUi.Fill(r, UiInk.Light(.1f * alpha));
                TacticalUi.Fill(new Rect(r.x, r.y, r.width * fill01, r.height), A(top, alpha));
                return;
            }
            UiSdf.Rect(r, UiSdf.Style.Solid(UiInk.Dark(.6f * alpha), radius)
                .WithGradient(UiInk.Of(UiTokens.Panel, .8f * alpha), UiSdf.Gradient.Vertical));
            var groove = UiSdf.Style.Solid(UiInk.Light(.07f * alpha), radius);
            groove.Stroke = 1f;
            UiSdf.Rect(r, groove);
            float inset = r.height >= 8f ? 1.5f : 1f;
            Rect inner = new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f);
            float w = inner.width * fill01;
            if (w > .5f)
            {
                float baseW = gainFrom01 >= 0f ? inner.width * Mathf.Clamp(gainFrom01, 0f, fill01) : w;
                float innerRadius = Mathf.Max(0f, radius - inset);
                if (baseW > .5f)
                    UiSdf.Rect(new Rect(inner.x, inner.y, baseW, inner.height),
                        UiSdf.Style.Solid(A(top, alpha), innerRadius).WithGradient(A(bottom, alpha), UiSdf.Gradient.Vertical));
                if (w - baseW > .5f)
                {
                    Color gainTop = Color.Lerp(top, Color.white, .45f), gainBottom = Color.Lerp(bottom, TacticalUi.Accent, .5f);
                    UiSdf.Rect(new Rect(inner.x + baseW, inner.y, w - baseW, inner.height),
                        UiSdf.Style.Solid(A(gainTop, alpha), innerRadius).WithGradient(A(gainBottom, alpha), UiSdf.Gradient.Vertical));
                }
                // Top sheen and a hot leading edge.
                UiSdf.Rect(new Rect(inner.x, inner.y, w, Mathf.Max(1f, inner.height * .3f)), UiSdf.Style.Solid(UiInk.Light(.18f * alpha), innerRadius));
                if (fill01 < .999f)
                    UiSdf.Rect(new Rect(inner.x + w - 2f, inner.y - 1f, 2f, inner.height + 2f),
                        UiSdf.Style.Solid(UiInk.Of(UiTokens.MedalHighlight, .95f * alpha), 1f).WithGlow(A(top, 1f), 6f, .6f * alpha));
            }
            for (int i = 1; i < ticks; i++)
            {
                float x = inner.x + inner.width * i / ticks;
                TacticalUi.Fill(new Rect(x, inner.y, 1f, inner.height), UiInk.Dark(.45f * alpha));
            }
        }

        /// <summary>Reward glyph (or mesh-traced weapon silhouette) with a dark drop and a light metal gradient.</summary>
        public static bool RewardIcon(Rect r, CareerRewardArt art, Color top, Color bottom, float alpha)
        {
            if (!UiSdf.Available || !UiIconAtlas.TryGet(art.IconName, out _))
            {
                if (art.Kind != CareerRewardKind.Weapon) return false;
                var id = new DefinitionId(CareerLadder.Get(art.Rank).RewardId);
                if (!Shooter.Presentation.View.WeaponVisualProfiles.TryGet(id, out var profile)) return false;
                TacticalUi.KillFeedWeaponIcon(r, profile.KillFeedGlyph, A(top, alpha));
                return true;
            }
            float depth = Mathf.Max(1f, r.height * .03f);
            UiSdf.Icon(Offset(r, 0f, depth), art.IconName, UiSdf.Style.Solid(UiInk.Dark(.6f * alpha)).WithShadow(.35f * alpha, 5f, 1.5f));
            return UiSdf.Icon(r, art.IconName, UiSdf.Style.Solid(A(top, alpha)).WithGradient(A(bottom, alpha), UiSdf.Gradient.Vertical));
        }

        /// <summary>
        /// Reward card. Wide rects lay out icon left / text right (Career hero, debrief); tall rects stack band,
        /// icon, title, detail and a state chip (progress line). <paramref name="emphasis"/> 0..1 lifts and lights it.
        /// </summary>
        public static void RewardCard(TacticalUi ui, Rect r, CareerRewardArt art, RewardState state, float emphasis, float alpha,
            float glint = -1f, bool reducedFlash = false)
        {
            if (alpha <= 0.01f) return;
            bool locked = state == RewardState.Locked;
            Color kind = KindInk(art.Kind);
            Color kindShown = locked ? Dim(kind) : kind;
            float radius = Hud.UiShapeParams.CardRadius;
            var plate = UiSdf.Style.Solid(UiInk.Of(locked ? UiTokens.Panel : UiTokens.PanelTop, .97f * alpha), radius)
                .WithGradient(UiInk.Of(UiTokens.Well, .97f * alpha), UiSdf.Gradient.Vertical).WithShadow(.45f * alpha, 10f, 3f);
            if (state == RewardState.Next)
                plate = plate.WithGlow(kind, 10f, (reducedFlash ? .18f : .3f + .12f * emphasis) * alpha);
            else if (emphasis > .01f)
                plate = plate.WithGlow(kindShown, 8f, .2f * emphasis * alpha);
            TacticalUi.Plate(r, plate);
            if (UiSdf.Available)
            {
                var edge = UiSdf.Style.Solid(A(kindShown, (state == RewardState.Next ? .7f : .16f + .3f * emphasis) * alpha), radius);
                edge.Stroke = 1f;
                UiSdf.Rect(r, edge);
            }
            TacticalUi.Plate(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, 3f), UiSdf.Style.Solid(A(kindShown, alpha), 1.5f));

            Color iconTop = locked ? UiInk.Of(UiTokens.LockedTop) : Color.Lerp(TacticalUi.Text, kind, .18f);
            Color iconBottom = locked ? UiInk.Of(UiTokens.LockedBottom) : Color.Lerp(UiInk.Of(UiTokens.RewardIconBase), kind, .35f);
            Color title = locked ? TacticalUi.Muted : TacticalUi.Text;
            bool wideCard = r.width > r.height * 1.7f;
            Rect iconArea;
            float tx, ty, tw;
            if (wideCard)
            {
                iconArea = new Rect(r.x + 10f, r.y + 10f, Mathf.Min(r.width * .42f, r.height * 2.2f), r.height - 20f);
                tx = iconArea.xMax + 14f;
                ty = r.y + 10f;
                tw = r.xMax - tx - 10f;
            }
            else
            {
                float iconH = Mathf.Clamp(r.height * .42f, 26f, 88f);
                iconArea = new Rect(r.x + 8f, r.y + 10f, r.width - 16f, iconH);
                tx = r.x + 10f;
                ty = iconArea.yMax + 6f;
                tw = r.width - 20f;
            }
            if (UiSdf.Available)
                UiSdf.Circle(Square(iconArea, 1.25f), UiSdf.Style.Solid(A(kindShown, (locked ? .08f : .2f) * alpha))
                    .WithGradient(A(kindShown, 0f), UiSdf.Gradient.Radial));
            Rect icon = art.Wide ? new Rect(iconArea.x + iconArea.width * .06f, iconArea.y + iconArea.height * .1f, iconArea.width * .88f, iconArea.height * .8f)
                : Square(iconArea, .86f);
            RewardIcon(icon, art, iconTop, iconBottom, alpha);
            if (glint >= 0f && !locked && UiSdf.Available)
                UiSdf.Rect(iconArea, UiSdf.Style.Solid(UiInk.Clear, radius).WithGlint(glint, .25f * alpha));
            if (locked)
            {
                Rect lockR = new Rect(iconArea.xMax - 22f, iconArea.y, 20f, 20f);
                TacticalUi.Icon(lockR, IconLibrary.UiLock, A(TacticalUi.Muted, .9f * alpha), shadow: true);
            }

            // ADR-114: every line is one fitted line (no wrapping past the card), and each one is drawn only when it
            // clears the state chip at the bottom.
            bool tall = wideCard ? r.height >= 86f : r.height >= 128f;
            Rect chip = new Rect(tx, r.yMax - 22f, tw, 16f);
            float y = ty;
            if (tall)
            {
                ui.LabelFit(new Rect(tx, y, tw, 14), KindLabel(art.Kind), 10, A(kindShown, alpha), TextAnchor.UpperLeft, 9);
                y += 14f;
            }
            int titleSize = wideCard ? (r.height >= 80f ? 18 : 14) : r.width >= 150f ? 15 : 13;
            ui.LabelFit(new Rect(tx, y, tw, titleSize + 6), art.FullTitle.ToUpperInvariant(), titleSize, A(title, alpha), TextAnchor.UpperLeft, 10);
            y += titleSize + 5f;
            if ((wideCard || tall || r.height >= 104f) && y + 16f <= chip.y - 2f)
                ui.LabelFit(new Rect(tx, y, tw, 16), art.Detail, 11, A(TacticalUi.Muted, alpha), TextAnchor.UpperLeft, 9);

            // State chip along the bottom.
            string chipText;
            Color chipInk;
            string? chipIcon;
            switch (state)
            {
                case RewardState.Earned: chipText = ShellUiCopy.CareerUnlocked; chipInk = TacticalUi.Ally; chipIcon = IconLibrary.UiCheck; break;
                case RewardState.Next: chipText = ShellUiCopy.CareerNextChip; chipInk = TacticalUi.Accent; chipIcon = null; break;
                default: chipText = ShellUiCopy.CareerRankChip(art.Rank); chipInk = TacticalUi.Muted; chipIcon = IconLibrary.UiLock; break;
            }
            if (y > chip.y) return;
            float cx = chip.x;
            if (chipIcon != null && TacticalUi.Icon(new Rect(cx, chip.y + 1f, 13f, 13f), chipIcon, A(chipInk, alpha))) cx += 17f;
            ui.LabelFit(new Rect(cx, chip.y, chip.xMax - cx, chip.height), chipText, 10, A(chipInk, alpha), TextAnchor.UpperLeft, 9);
        }

        /// <summary>Active buff tile: icon plate and the combined value.</summary>
        public static void BuffChip(TacticalUi ui, Rect r, string icon, string name, string value)
        {
            TacticalUi.Plate(r, UiSdf.Style.Solid(UiInk.Of(UiTokens.PanelTop, .95f), UiSpace.FrameRadius)
                .WithGradient(UiInk.Of(UiTokens.Panel, .95f), UiSdf.Gradient.Vertical).WithShadow(.35f, 6f, 2f));
            float s = r.height - 12f;
            Rect tile = new Rect(r.x + 6f, r.y + 6f, s, s);
            TacticalUi.Plate(tile, UiSdf.Style.Solid(UiInk.Dim(BuffInk, .3f), 4f)
                .WithGradient(UiInk.Of(UiTokens.BuffWell), UiSdf.Gradient.Vertical));
            UiSdf.Icon(Square(tile, .72f), icon, UiSdf.Style.Solid(Color.Lerp(Color.white, BuffInk, .3f)).WithGradient(BuffInk, UiSdf.Gradient.Vertical));
            float tx = tile.xMax + 10f;
            // ADR-114: name and value split the chip height, so the value never drops out of a short chip.
            float half = (r.height - 8f) * .5f;
            ui.LabelFit(new Rect(tx, r.y + 4f, r.xMax - tx - 6f, half), name.ToUpperInvariant(), 11, TacticalUi.Muted, TextAnchor.MiddleLeft, 9);
            ui.LabelFit(new Rect(tx, r.y + 4f + half, r.xMax - tx - 6f, half), value, half >= 20f ? 16 : 13, TacticalUi.Text, TextAnchor.MiddleLeft, 11);
        }
    }
}
