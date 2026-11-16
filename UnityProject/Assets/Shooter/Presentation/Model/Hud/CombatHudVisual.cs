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
        public const float DamagePulseSeconds = 0.55f;
        public const float MaxHealth = 100f;
        public const float HealthBarHeight = 5f;
        public const int HealthSegments = 5;
        /// <summary>ADR-060 tightness bar drawn above the health track.</summary>
        public const float TightnessBarHeight = 3f;
        public const float TightnessBarGap = 3f;
        public const string KillFeedSeparator = "  >  ";
        /// <summary>Elimination-row plate and icon band (720p tactical HUD).</summary>
        public const float KillFeedRowWidth = 248f;
        public const float KillFeedRowHeight = 16f;
        public const float KillFeedNameWidth = 82f;
        public const float KillFeedWeaponIconWidth = 26f;
        public const float KillFeedHeadshotIconWidth = 12f;
        public const float KillFeedIconGap = 3f;
        /// <summary>T085 incoming-damage direction marker: hold time, ring slots and radius around the reticle.</summary>
        public const float DamageIndicatorSeconds = 1.4f;
        public const int MaxDamageIndicators = 4;
        public const float DamageIndicatorRadius = 118f;
        public const float DamageIndicatorLength = 64f;
        /// <summary>ADR-058 minimap gunfire reveal: temporary enemy blip after ShotAccepted (not continuous sensing).</summary>
        public const float GunfireRevealSeconds = 2.5f;
        public const float GunfireRevealFadeSeconds = 0.5f;
        public const int MaxGunfireReveals = 8;

        /// <summary>
        /// Screen angle of a damage source relative to the viewer's aim yaw (radians in [-pi, pi)): 0 is straight
        /// ahead (top of the ring), positive is to the right. Uses the simulation yaw convention atan2(dx, dz).
        /// </summary>
        public static float DamageIndicatorAngle(float viewYaw, float viewerX, float viewerZ, float sourceX, float sourceZ)
        {
            float dx = sourceX - viewerX, dz = sourceZ - viewerZ;
            if (dx * dx + dz * dz < 1e-6f) return 0f;
            double a = Math.Atan2(dx, dz) - viewYaw;
            a = (a + Math.PI) % (2 * Math.PI);
            if (a < 0) a += 2 * Math.PI;
            return (float)(a - Math.PI);
        }

        /// <summary>Marker opacity from remaining hold time: full for the first half, then an ease-out fade.</summary>
        public static float DamageIndicatorAlpha(float remainingSeconds)
        {
            if (remainingSeconds <= 0f) return 0f;
            float t = remainingSeconds / DamageIndicatorSeconds;
            if (t >= 0.5f) return 0.9f;
            return 0.9f * (t / 0.5f);
        }

        /// <summary>
        /// True when an authority ShotAccepted should create/refresh a minimap gunfire blip.
        /// Local human and allied shots stay off the map; silent enemies are never plotted.
        /// </summary>
        public static bool ShouldRevealGunfire(bool instigatorValid, bool isLocalHuman, bool isEnemyTeam)
            => instigatorValid && !isLocalHuman && isEnemyTeam;

        /// <summary>Gunfire-reveal opacity: full until the last fade window, then linear to zero.</summary>
        public static float GunfireRevealAlpha(float remainingSeconds)
        {
            if (remainingSeconds <= 0f) return 0f;
            if (remainingSeconds >= GunfireRevealFadeSeconds) return 0.95f;
            return 0.95f * (remainingSeconds / GunfireRevealFadeSeconds);
        }

        /// <summary>
        /// Ring slot for a gunfire reveal: reuse the active slot for the same entity, else the oldest expiry.
        /// </summary>
        public static int PickGunfireRevealSlot(ulong[] ids, float[] until, ulong entityId, float now, int capacity)
        {
            if (ids == null || until == null || capacity <= 0) return 0;
            int n = capacity;
            if (ids.Length < n) n = ids.Length;
            if (until.Length < n) n = until.Length;
            if (n <= 0) return 0;
            int oldest = 0;
            for (int i = 0; i < n; i++)
            {
                if (ids[i] == entityId && until[i] > now) return i;
                if (until[i] < until[oldest]) oldest = i;
            }
            return oldest;
        }

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

        /// <summary>Clamped tightness fill width for a continuous bar of <paramref name="barWidth"/>.</summary>
        public static float TightnessFillWidth(float tightness01, float barWidth)
        {
            if (barWidth <= 0f || tightness01 <= 0f) return 0f;
            if (tightness01 >= 1f) return barWidth;
            return barWidth * tightness01;
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

        /// <summary>Bound draw cost for oversized magazines; use a bar above this cap.</summary>
        public const int MaxRoundGlyphs = 30;
        public static int RoundGlyphCount(int rounds) => Math.Max(0, Math.Min(MaxRoundGlyphs, rounds));

        /// <summary>Brief outward hit pop settling inside the existing hit window.</summary>
        public static float HitScale(float elapsed, bool headshot, bool reducedMotion)
        {
            float size = headshot ? 1.25f : 1f;
            return reducedMotion ? size : size * UiMotion.Lerp(0.75f, 1f,
                UiMotion.OutBack(UiMotion.Progress(elapsed, 0.14f)));
        }

        /// <summary>Bounded modulation of existing strength; accessibility holds the original cue.</summary>
        public static float VignetteModulation(float time, bool reducedFlash, bool reducedMotion) =>
            reducedFlash || reducedMotion ? 1f : 0.65f + 0.35f * UiMotion.Pulse(time, 1.4f, false);

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
