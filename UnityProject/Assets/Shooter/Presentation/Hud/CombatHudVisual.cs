#nullable enable
using System;

namespace Shooter.Presentation.Hud
{
    /// <summary>
    /// Pure combat-HUD visual math for fade/danger reads. Presentation-only; no authority.
    /// </summary>
    public static class CombatHudVisual
    {
        public const float KillFeedHoldSeconds = 6f;
        public const float KillFeedFadeSeconds = 1.6f;
        public const int HealthDangerThreshold = 25;
        public const int HealthWarnThreshold = 40;
        public const float DamagePulseSeconds = 0.45f;
        public const float MaxHealth = 100f;
        public const float HealthBarHeight = 5f;
        public const int HealthSegments = 5;
        public const string KillFeedSeparator = "  >  ";

        /// <summary>Opacity for a kill-feed row given remaining display time.</summary>
        public static float KillFeedAlpha(float remainingSeconds, float fadeSeconds = KillFeedFadeSeconds)
        {
            if (remainingSeconds <= 0f) return 0f;
            if (fadeSeconds <= 0f) return 1f;
            if (remainingSeconds >= fadeSeconds) return 1f;
            return remainingSeconds / fadeSeconds;
        }

        /// <summary>
        /// Edge vignette strength from observed health and an optional recent-damage pulse.
        /// Reduced flash keeps a static critical/warn cue only (no damage pulse).
        /// </summary>
        public static float HealthVignette(int health, bool recentDamage, bool reducedFlash = false)
        {
            float danger = health <= HealthDangerThreshold ? 0.22f
                : health <= HealthWarnThreshold ? 0.10f
                : 0f;
            if (reducedFlash)
                return danger > 0f ? Math.Min(0.14f, danger * 0.65f) : 0f;
            float pulse = recentDamage ? 0.16f : 0f;
            float peak = danger > pulse ? danger : pulse;
            return peak > 0.28f ? 0.28f : peak;
        }

        /// <summary>True when health should use the danger ink (coral) rather than ivory.</summary>
        public static bool IsHealthDanger(int health) => health <= HealthDangerThreshold;

        /// <summary>True when health is wounded but not yet critical (sand-amber cue).</summary>
        public static bool IsHealthWarning(int health)
            => health <= HealthWarnThreshold && health > HealthDangerThreshold;

        public static float HealthFraction(int health)
        {
            if (health <= 0) return 0f;
            if (health >= MaxHealth) return 1f;
            return health / MaxHealth;
        }

        /// <summary>Filled width for health segment index (0..HealthSegments-1), left-to-right.</summary>
        public static float SegmentFill(float health, int index, float segmentWidth)
        {
            if (index < 0 || index >= HealthSegments || segmentWidth <= 0f) return 0f;
            float per = MaxHealth / HealthSegments;
            float start = index * per;
            float local = health - start;
            if (local <= 0f) return 0f;
            if (local >= per) return segmentWidth;
            return segmentWidth * (local / per);
        }

        /// <summary>Critical health bar opacity pulse. Reduced flash/motion freeze at full readability.</summary>
        public static float CriticalBarPulse(float unscaledTime, bool reducedFlash, bool reducedMotion)
        {
            if (!reducedFlash && !reducedMotion)
                return 0.72f + 0.28f * (0.5f + 0.5f * (float)Math.Sin(unscaledTime * 4.5));
            return 1f;
        }

        public static bool IsCardinal(int bearingDegrees)
        {
            int b = ((bearingDegrees % 360) + 360) % 360;
            return b == 0 || b == 90 || b == 180 || b == 270;
        }

        public static string CardinalOrBearing(int bearingDegrees)
        {
            int b = ((bearingDegrees % 360) + 360) % 360;
            return b switch
            {
                0 => "N",
                90 => "E",
                180 => "S",
                270 => "W",
                _ => b.ToString()
            };
        }

        public static bool TrySplitKillFeed(string line, out string left, out string right)
        {
            left = "";
            right = "";
            if (string.IsNullOrEmpty(line)) return false;
            int at = line.IndexOf(KillFeedSeparator, StringComparison.Ordinal);
            if (at < 0) return false;
            left = line.Substring(0, at);
            right = line.Substring(at + KillFeedSeparator.Length);
            return left.Length > 0 && right.Length > 0;
        }
    }
}
