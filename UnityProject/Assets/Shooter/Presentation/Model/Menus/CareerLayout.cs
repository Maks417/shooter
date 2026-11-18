#nullable enable
using System;
using Shooter.Presentation.Hud;

namespace Shooter.Presentation.Menus
{
    /// <summary>ADR-114 Career page tabs: where the player stands and what comes next, and what they have done.</summary>
    public enum CareerTab : byte
    {
        Progress = 0,
        Statistics = 1,
    }

    public static class CareerTabs
    {
        public const int Count = 2;

        /// <summary>Next or previous tab (wraps), for Q/E and arrow keys on the tab strip.</summary>
        public static CareerTab Step(CareerTab tab, int direction)
        {
            int i = ((int)tab + Math.Sign(direction) + Count) % Count;
            return (CareerTab)i;
        }
    }

    /// <summary>
    /// Pure Career page layout in logical UI pixels (ADR-114). The page is a tab strip under the header, one content
    /// area and the back bar. Progress holds the hero band and the progress line; Statistics holds the active perks and
    /// the service record. Every band is placed from the space actually there, so nothing overlaps at 720p–1080p and
    /// 100–150% UI scale.
    /// </summary>
    public sealed class CareerLayout
    {
        public const float HeaderHeight = 74f;
        public const float TabHeight = 36f;
        public const float BackHeight = 44f;
        public const float BackBottom = 22f;
        public const float Gap = 12f;
        /// <summary>Below this content height the hero band collapses to one compact row.</summary>
        public const float CompactBelow = 400f;
        public const float CompactHeroHeight = 76f;
        public const float SectionHeight = 18f;
        public const float PerkRowHeight = 48f;
        /// <summary>Service-record tiles always stack label over value; below this tile height the grid goes 6 × 2.</summary>
        public const float MinTileHeight = 44f;

        public HudRect Safe { get; }
        public HudRect Tabs { get; }
        public HudRect Back { get; }
        public HudRect Content { get; }

        public HudRect Hero { get; }
        public bool CompactHero { get; }
        public HudRect Track { get; }

        public HudRect PerksLabel { get; }
        public HudRect Perks { get; }
        public HudRect RecordLabel { get; }
        public HudRect Record { get; }
        public int RecordColumns { get; }
        public int RecordRows { get; }

        private CareerLayout(HudRect safe)
        {
            Safe = safe;
            Tabs = new HudRect(safe.X, safe.Y + HeaderHeight, safe.Width, TabHeight);
            Back = new HudRect(safe.X, safe.Bottom - BackBottom - BackHeight, safe.Width, BackHeight);
            float top = Tabs.Bottom + Gap;
            float bottom = Back.Y - Gap;
            if (bottom - top < 120f) throw new ArgumentOutOfRangeException(nameof(safe), "Career page needs at least 120 px of content");
            Content = new HudRect(safe.X, top, safe.Width, bottom - top);

            // Progress tab.
            CompactHero = Content.Height < CompactBelow;
            float heroH = CompactHero ? CompactHeroHeight : Clamp(Content.Height * .3f, 120f, 160f);
            Hero = new HudRect(Content.X, Content.Y, Content.Width, heroH);
            Track = new HudRect(Content.X, Hero.Bottom + Gap, Content.Width, Content.Bottom - Hero.Bottom - Gap);

            // Statistics tab.
            PerksLabel = new HudRect(Content.X, Content.Y, Content.Width, SectionHeight);
            Perks = new HudRect(Content.X, PerksLabel.Bottom + 6f, Content.Width, PerkRowHeight);
            RecordLabel = new HudRect(Content.X, Perks.Bottom + 16f, Content.Width, SectionHeight);
            float recordTop = RecordLabel.Bottom + 6f;
            Record = new HudRect(Content.X, recordTop, Content.Width, Content.Bottom - recordTop);
            RecordColumns = 4;
            RecordRows = 3;
            if (TileHeight(Record.Height, RecordRows) < MinTileHeight) { RecordColumns = 6; RecordRows = 2; }
        }

        public static CareerLayout Build(HudRect safe) => new CareerLayout(safe);

        /// <summary>Safe area TacticalUi uses: the logical screen inset 5% per side.</summary>
        public static HudRect SafeFor(float logicalWidth, float logicalHeight) =>
            new HudRect(logicalWidth * .05f, logicalHeight * .05f, logicalWidth * .9f, logicalHeight * .9f);

        public const float TileGap = 6f;

        private static float TileHeight(float height, int rows) => (height - (rows - 1) * TileGap) / rows;

        /// <summary>Service-record tile <paramref name="index"/> (row-major).</summary>
        public HudRect RecordTile(int index)
        {
            if (index < 0 || index >= RecordColumns * RecordRows) throw new ArgumentOutOfRangeException(nameof(index));
            float w = (Record.Width - (RecordColumns - 1) * TileGap) / RecordColumns;
            float h = Math.Min(TileHeight(Record.Height, RecordRows), 110f);
            int row = index / RecordColumns, col = index % RecordColumns;
            return new HudRect(Record.X + col * (w + TileGap), Record.Y + row * (h + TileGap), w, h);
        }

        /// <summary>Active-perk chip <paramref name="index"/> of <paramref name="count"/> in one row (at most 5, at most 280 px each).</summary>
        public HudRect PerkChip(int index, int count)
        {
            if (count <= 0 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            float w = Math.Min(280f, (Perks.Width - (count - 1) * TileGap) / count);
            return new HudRect(Perks.X + index * (w + TileGap), Perks.Y, w, Perks.Height);
        }

        internal static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }

    /// <summary>
    /// Pure geometry of the horizontal progress line (ADR-113, re-laid by ADR-114): section label, end arrows, the node row
    /// with the current rank's halo, the code / CP labels and one reward card per column. The node row sits below the
    /// label by the full halo radius, and cards start below the lowest label with room for the hover lift, so the parts
    /// never touch. Cards narrower than tall stack; short ones switch to the wide (icon-left) card; under
    /// <see cref="MinCardHeight"/> they are left out.
    /// </summary>
    public sealed class CareerLineLayout
    {
        public const float ArrowWidth = 30f;
        public const float ArrowInset = 6f;
        public const float SideMargin = ArrowWidth + 10f;
        public const float MinColumnWidth = 170f;
        public const float CurrentScale = 1.22f;
        public const float HaloScale = 1.32f;
        public const float DiscScale = 1.08f;
        public const float HoverLift = 4f;
        public const float MinCardHeight = 56f;
        public const float MaxCardHeight = 230f;
        public const float BottomPad = 14f;

        public HudRect Track { get; }
        public HudRect Inner { get; }
        public HudRect SectionLabel { get; }
        public HudRect PrevArrow { get; }
        public HudRect NextArrow { get; }
        public HudRect Rail { get; }
        public HudRect PositionRail { get; }
        public int Visible { get; }
        public float ColumnWidth { get; }
        public float Node { get; }
        public float LineY { get; }
        public float CardTop { get; }
        public float CardHeight { get; }
        public bool CardsShown => CardHeight >= MinCardHeight;

        private CareerLineLayout(HudRect track, int rankCount)
        {
            Track = track;
            Inner = new HudRect(track.X + SideMargin, track.Y, track.Width - SideMargin * 2f, track.Height);
            Visible = Math.Max(1, Math.Min(rankCount, (int)CareerLayout.Clamp((float)Math.Floor(Inner.Width / MinColumnWidth), 3f, 8f)));
            ColumnWidth = Inner.Width / Visible;
            SectionLabel = new HudRect(Inner.X, track.Y + 8f, Inner.Width * .5f, 16f);
            Node = CareerLayout.Clamp(Math.Min(ColumnWidth * .36f, track.Height * .2f), 30f, 64f);
            float haloRadius = Node * CurrentScale * HaloScale * .5f;
            LineY = SectionLabel.Bottom + 6f + haloRadius;
            Rail = new HudRect(Inner.X + 4f, LineY - 4f, Inner.Width - 8f, 8f);
            float labelsBottom = CpLabel(0, true).Bottom;
            CardTop = labelsBottom + 8f + HoverLift;
            float cardBottom = track.Bottom - BottomPad;
            CardHeight = Math.Min(MaxCardHeight, cardBottom - CardTop);
            float arrowH = Math.Min(60f, track.Height - 16f);
            float arrowY = CareerLayout.Clamp(LineY - arrowH * .5f, track.Y + 4f, track.Bottom - arrowH - 4f);
            PrevArrow = new HudRect(track.X + ArrowInset, arrowY, ArrowWidth, arrowH);
            NextArrow = new HudRect(track.Right - ArrowWidth - ArrowInset, arrowY, ArrowWidth, arrowH);
            PositionRail = new HudRect(Inner.X + Inner.Width * .25f, track.Bottom - 8f, Inner.Width * .5f, 3f);
        }

        public static CareerLineLayout Build(HudRect track, int rankCount) => new CareerLineLayout(track, rankCount);

        public float ColumnCenter(int slot) => Inner.X + ColumnWidth * (slot + .5f);

        public HudRect Column(int slot) => new HudRect(Inner.X + slot * ColumnWidth, Track.Y + 26f, ColumnWidth, Track.Height - 30f);

        public float NodeSize(bool current) => current ? Node * CurrentScale : Node;

        public HudRect NodeRect(int slot, bool current)
        {
            float s = NodeSize(current);
            return new HudRect(ColumnCenter(slot) - s * .5f, LineY - s * .5f, s, s);
        }

        /// <summary>Dark disc behind the emblem; the current rank's disc carries the accent ring and glow.</summary>
        public HudRect HaloRect(int slot, bool current)
        {
            float s = NodeSize(current) * (current ? HaloScale : DiscScale);
            return new HudRect(ColumnCenter(slot) - s * .5f, LineY - s * .5f, s, s);
        }

        public HudRect CodeLabel(int slot, bool current)
        {
            // Below the disc / halo ring (its glow is drawn inside the halo rect) with a small gap.
            float y = LineY + NodeSize(current) * (current ? HaloScale : DiscScale) * .5f + 5f;
            return new HudRect(Inner.X + slot * ColumnWidth + 4f, y, ColumnWidth - 8f, 16f);
        }

        public HudRect CpLabel(int slot, bool current)
        {
            var code = CodeLabel(slot, current);
            return new HudRect(code.X, code.Y + 15f, code.Width, 14f);
        }

        /// <summary>Reward card for a column at rest (the hover lift moves it up by at most <see cref="HoverLift"/>).</summary>
        public HudRect Card(int slot)
        {
            if (!CardsShown) throw new InvalidOperationException("No room for reward cards");
            return new HudRect(Inner.X + slot * ColumnWidth + 6f, CardTop, ColumnWidth - 12f, CardHeight);
        }
    }
}
