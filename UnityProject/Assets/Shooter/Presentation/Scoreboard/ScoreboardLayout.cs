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
}
