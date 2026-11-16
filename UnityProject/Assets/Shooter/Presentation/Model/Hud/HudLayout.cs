#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Presentation.Hud
{
    public enum HudFocusTarget { None = 0, Resume = 1, Settings = 2, Restart = 3, Quality = 4, Rebind = 5 }

    public sealed class HudSettings
    {
        public const int SchemaVersion = 1;
        public float UiScale { get; }
        public float HipFov { get; }
        public string QualityProfile { get; }
        public bool ReducedMotion { get; }
        public float Contrast { get; }
        public HudSettings(float uiScale, float hipFov, string qualityProfile, bool reducedMotion, float contrast)
        {
            if (uiScale < 1f || uiScale > 1.5f || hipFov < 70f || hipFov > 100f || contrast < 0.8f || contrast > 1.5f)
                throw new ArgumentOutOfRangeException(nameof(uiScale));
            if (string.IsNullOrEmpty(qualityProfile)) throw new ArgumentNullException(nameof(qualityProfile));
            UiScale = uiScale; HipFov = hipFov; QualityProfile = qualityProfile; ReducedMotion = reducedMotion; Contrast = contrast;
        }
        public static HudSettings Default => new HudSettings(1f, 80f, "Low", false, 1f);
    }

    public readonly struct HudRect
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public HudRect(float x, float y, float width, float height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            X = x; Y = y; Width = width; Height = height;
        }
        public float Right => X + Width;
        public float Bottom => Y + Height;
        public bool Intersects(HudRect other)
            => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
    }

    /// <summary>Pure 720p HUD layout + accessibility rules. No sprites; engine font allowlisted separately.</summary>
    public sealed class HudLayout
    {
        public const int ReferenceWidth = 1280;
        public const int ReferenceHeight = 720;

        private readonly Dictionary<string, string> bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        public HudSettings Settings { get; private set; }
        public HudFocusTarget Focus { get; private set; } = HudFocusTarget.Resume;
        public IReadOnlyDictionary<string, string> Bindings => bindings;

        public HudLayout(HudSettings? settings = null)
        {
            Settings = settings ?? HudSettings.Default;
            bindings["Fire"] = "Mouse0";
            bindings["Reload"] = "R";
            bindings["Jump"] = "Space";
            bindings["Menu"] = "Escape";
        }

        public void ApplySettings(HudSettings settings) => Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        public bool TryRebind(string action, string control)
        {
            if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(control)) return false;
            foreach (var kv in bindings)
                if (kv.Key != action && string.Equals(kv.Value, control, StringComparison.OrdinalIgnoreCase))
                    return false;
            bindings[action] = control;
            return true;
        }

        public void CycleFocus(int delta)
        {
            int n = Enum.GetValues(typeof(HudFocusTarget)).Length - 1; // skip None
            int cur = Focus == HudFocusTarget.None ? 1 : (int)Focus;
            cur = ((cur - 1 + delta) % n + n) % n + 1;
            Focus = (HudFocusTarget)cur;
        }

        public IReadOnlyList<HudRect> BuildLayout()
        {
            float s = Settings.UiScale;
            var rects = new List<HudRect>
            {
                new HudRect(16 * s, 16 * s, 220 * s, 48 * s), // health
                new HudRect(16 * s, 70 * s, 180 * s, 36 * s), // ammo
                new HudRect(ReferenceWidth - 240 * s, 16 * s, 220 * s, 48 * s), // objective
                new HudRect(ReferenceWidth * 0.5f - 160 * s, ReferenceHeight * 0.5f - 120 * s, 320 * s, 240 * s) // menu card
            };
            return rects;
        }

        public bool LayoutFitsReference()
        {
            var rects = BuildLayout();
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                if (r.X < 0 || r.Y < 0 || r.Right > ReferenceWidth + 1e-3f || r.Bottom > ReferenceHeight + 1e-3f)
                    return false;
                for (int j = i + 1; j < rects.Count; j++)
                    if (r.Intersects(rects[j]) && !(i == 0 && j == 1)) // health/ammo may stack
                        if (i >= 2 || j >= 2) return false;
            }
            return true;
        }

        public string SerializeSettings()
            => string.Format(CultureInfo.InvariantCulture,
                "v{0};scale={1};fov={2};quality={3};reduced={4};contrast={5}",
                HudSettings.SchemaVersion, Settings.UiScale, Settings.HipFov, Settings.QualityProfile, Settings.ReducedMotion ? 1 : 0, Settings.Contrast);
    }
}
