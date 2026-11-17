#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Presentation.Hud;

namespace Shooter.Presentation.Scoreboard
{
    /// <summary>Pure 720p scoreboard/results layout for up to twelve rows at 100–150% UI scale.</summary>
    public sealed class ScoreboardLayout
    {
        public const int ReferenceWidth = HudLayout.ReferenceWidth;
        public const int ReferenceHeight = HudLayout.ReferenceHeight;

        public float UiScale { get; }

        public ScoreboardLayout(float uiScale = 1f)
        {
            if (uiScale < 1f || uiScale > 1.5f) throw new ArgumentOutOfRangeException(nameof(uiScale));
            UiScale = uiScale;
        }

        public HudRect PanelRect()
        {
            float s = UiScale;
            // Base size chosen so twelve rows still fit the 720p safe area at 150% UI scale.
            float width = Math.Min(720f * s, ReferenceWidth - 40f);
            float height = Math.Min(460f * s, ReferenceHeight - 40f);
            return new HudRect(
                ReferenceWidth * 0.5f - width * 0.5f,
                ReferenceHeight * 0.5f - height * 0.5f,
                width,
                height);
        }

        public HudRect HeaderRect()
        {
            var panel = PanelRect();
            return new HudRect(panel.X + 16f * UiScale, panel.Y + 12f * UiScale, panel.Width - 32f * UiScale, 36f * UiScale);
        }

        public IReadOnlyList<HudRect> BuildRowRects(int rowCount)
        {
            if (rowCount < 0 || rowCount > ScoreboardTable.MaxRows) throw new ArgumentOutOfRangeException(nameof(rowCount));
            float s = UiScale;
            var panel = PanelRect();
            float header = 56f * s;
            float available = panel.Height - header - 12f * s;
            float pitch = rowCount == 0 ? 0f : available / Math.Max(rowCount, 1);
            float rowHeight = Math.Max(16f * s, Math.Min(28f * s, pitch - 2f * s));
            float startY = panel.Y + header;
            var rects = new List<HudRect>(rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                rects.Add(new HudRect(
                    panel.X + 16f * s,
                    startY + i * pitch,
                    panel.Width - 32f * s,
                    rowHeight));
            }
            return rects;
        }

        public bool FitsReference(int rowCount)
        {
            var panel = PanelRect();
            if (panel.X < 0 || panel.Y < 0 || panel.Right > ReferenceWidth + 1e-3f || panel.Bottom > ReferenceHeight + 1e-3f)
                return false;
            var rows = BuildRowRects(rowCount);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.X < panel.X || r.Right > panel.Right + 1e-3f) return false;
                if (r.Bottom > panel.Bottom + 1e-3f) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Row metrics for a team table drawn in a fixed viewport. Rows scroll by whole rows, so a 15v15 table never
    /// spills over the buttons, footer or screen edge; small teams keep their design row height.
    /// </summary>
    public readonly struct TeamTableMetrics
    {
        public float RowHeight { get; }
        public int VisibleRows { get; }
        public int TotalRows { get; }
        public float ViewportHeight { get; }

        private TeamTableMetrics(float rowHeight, int visibleRows, int totalRows, float viewportHeight)
        {
            RowHeight = rowHeight;
            VisibleRows = visibleRows;
            TotalRows = totalRows;
            ViewportHeight = viewportHeight;
        }

        public bool NeedsScroll => TotalRows > VisibleRows;
        public int MaxFirstRow => Math.Max(0, TotalRows - VisibleRows);
        /// <summary>Height actually covered by the drawn rows.</summary>
        public float UsedHeight => Math.Min(TotalRows, VisibleRows) * RowHeight;

        /// <param name="viewportHeight">Space reserved for rows (logical px).</param>
        /// <param name="rowsPerTeam">Rows in the larger team.</param>
        /// <param name="preferredRowHeight">Upper bound on the row pitch.</param>
        /// <param name="minRowHeight">Rows never shrink below this; extra rows scroll instead.</param>
        /// <param name="designRows">Row slots a small team is laid out for (keeps the 5v5 look unchanged).</param>
        public static TeamTableMetrics Fit(float viewportHeight, int rowsPerTeam, float preferredRowHeight, float minRowHeight, int designRows)
        {
            if (rowsPerTeam < 0 || rowsPerTeam > ScoreboardTable.MaxRows) throw new ArgumentOutOfRangeException(nameof(rowsPerTeam));
            if (!(minRowHeight > 0f) || preferredRowHeight < minRowHeight) throw new ArgumentOutOfRangeException(nameof(minRowHeight));
            float viewport = Math.Max(0f, viewportHeight);
            int slots = Math.Max(1, Math.Max(rowsPerTeam, designRows));
            float rowHeight = Math.Max(minRowHeight, Math.Min(preferredRowHeight, viewport / slots));
            int fit = (int)Math.Floor(viewport / rowHeight + 1e-3f);
            int visible = Math.Max(1, Math.Min(Math.Max(rowsPerTeam, 1), fit));
            return new TeamTableMetrics(rowHeight, visible, rowsPerTeam, viewport);
        }

        public int ClampFirstRow(int firstRow) => Math.Max(0, Math.Min(MaxFirstRow, firstRow));

        /// <summary>First row after a wheel step; positive notches scroll down.</summary>
        public int Scroll(int firstRow, float wheelDelta)
        {
            if (wheelDelta == 0f || float.IsNaN(wheelDelta)) return ClampFirstRow(firstRow);
            int step = wheelDelta > 0f ? Math.Max(1, (int)Math.Round(wheelDelta / 3f)) : Math.Min(-1, (int)Math.Round(wheelDelta / 3f));
            return ClampFirstRow(firstRow + step);
        }

        /// <summary>True when <paramref name="rowIndex"/> is inside the visible window starting at <paramref name="firstRow"/>.</summary>
        public bool IsVisible(int firstRow, int rowIndex)
        {
            int first = ClampFirstRow(firstRow);
            return rowIndex >= first && rowIndex < first + VisibleRows;
        }
    }

    public enum DebriefAwardMode : byte { Full = 0, Compact = 1, Hidden = 2 }

    /// <summary>
    /// Post-match debrief vertical budget: award cards shrink, then hide, so the team tables always keep at least
    /// <see cref="MinTableRows"/> visible rows above the buttons (720p at 150% UI scale has very little height).
    /// </summary>
    public static class DebriefAwardLayout
    {
        public const float FullCardHeight = 104f, CompactCardHeight = 46f, CardGapBelow = 18f;
        /// <summary>Table panel header/padding that is not rows (see the shell host team table).</summary>
        public const float TableChrome = 78f;
        public const float MinRowHeight = 26f;
        public const int MinTableRows = 3;

        public static float BlockHeight(DebriefAwardMode mode) => mode switch
        {
            DebriefAwardMode.Full => FullCardHeight + CardGapBelow,
            DebriefAwardMode.Compact => CompactCardHeight + CardGapBelow - 6f,
            _ => 0f
        };

        /// <param name="heightBelowBand">Height from the card row top to the table bottom when no cards are drawn.</param>
        public static DebriefAwardMode Choose(float heightBelowBand, int rowsPerTeam)
        {
            float need = TableChrome + Math.Min(Math.Max(rowsPerTeam, 1), MinTableRows) * MinRowHeight;
            if (heightBelowBand - BlockHeight(DebriefAwardMode.Full) >= need) return DebriefAwardMode.Full;
            if (heightBelowBand - BlockHeight(DebriefAwardMode.Compact) >= need) return DebriefAwardMode.Compact;
            return DebriefAwardMode.Hidden;
        }
    }

    /// <summary>Per-row and per-team figures shown on the in-match Tab scoreboard (presentation only).</summary>
    public static class ScoreboardStats
    {
        public static string KillDeathRatio(uint kills, uint deaths) =>
            (kills / (float)Math.Max(1u, deaths)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        public static int TeamRowCount(IReadOnlyList<ScoreboardRow> rows, byte team)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            int n = 0;
            for (int i = 0; i < rows.Count; i++) if (rows[i].Team == team) n++;
            return n;
        }

        public static int AliveCount(IReadOnlyList<ScoreboardRow> rows, byte team)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            int n = 0;
            for (int i = 0; i < rows.Count; i++) if (rows[i].Team == team && rows[i].Alive) n++;
            return n;
        }

        public static int LargestTeam(IReadOnlyList<ScoreboardRow> rows) => Math.Max(TeamRowCount(rows, 1), TeamRowCount(rows, 2));

        /// <summary>Index of the local row inside its team's ordered rows, or -1.</summary>
        public static int LocalTeamIndex(IReadOnlyList<ScoreboardRow> rows, out byte team)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            team = 0;
            int t1 = 0, t2 = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.IsLocal) { team = row.Team; return row.Team == 1 ? t1 : t2; }
                if (row.Team == 1) t1++; else if (row.Team == 2) t2++;
            }
            return -1;
        }

        /// <summary>1-based rank of the local row across both teams (rows are pre-sorted by score), or 0.</summary>
        public static int LocalOverallRank(IReadOnlyList<ScoreboardRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            for (int i = 0; i < rows.Count; i++) if (rows[i].IsLocal) return i + 1;
            return 0;
        }
    }
}
