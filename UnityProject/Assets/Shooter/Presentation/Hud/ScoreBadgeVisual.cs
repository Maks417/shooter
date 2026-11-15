#nullable enable
using System;

namespace Shooter.Presentation.Hud
{
    /// <summary>Pure timing/labels for ADR-059 combat score badges (no Unity).</summary>
    public static class ScoreBadgeVisual
    {
        public const float FeedHoldSeconds = 3.0f;
        public const float FeedFadeSeconds = 0.8f;
        public const int MaxFeedRows = 4;
        public const float RibbonHoldSeconds = 2.2f;
        public const float RibbonFadeSeconds = 0.7f;
        public const float RibbonHoldReducedFlashSeconds = 1.4f;
        public const float AmmoPopupHoldSeconds = 1.6f;
        public const float AmmoPopupFadeSeconds = 0.5f;
        public const string KillLabel = "ENEMY KILLED";
        public const string AssistLabel = "ASSIST";

        public static string LabelFor(bool isKill) => isKill ? KillLabel : AssistLabel;

        public static float FeedAlpha(float remainingSeconds, float fadeSeconds = FeedFadeSeconds)
        {
            if (remainingSeconds <= 0f) return 0f;
            if (fadeSeconds <= 0f) return 1f;
            if (remainingSeconds >= fadeSeconds) return 1f;
            return remainingSeconds / fadeSeconds;
        }

        public static float RibbonAlpha(float remainingSeconds, bool reducedFlash)
        {
            float fade = RibbonFadeSeconds;
            float hold = reducedFlash ? RibbonHoldReducedFlashSeconds : RibbonHoldSeconds;
            if (remainingSeconds <= 0f) return 0f;
            float peak = reducedFlash ? 0.72f : 1f;
            if (remainingSeconds >= fade) return peak;
            return peak * (remainingSeconds / fade);
        }

        public static float AmmoPopupAlpha(float remainingSeconds)
        {
            if (remainingSeconds <= 0f) return 0f;
            if (remainingSeconds >= AmmoPopupFadeSeconds) return 1f;
            return remainingSeconds / AmmoPopupFadeSeconds;
        }

        public static float RibbonHoldDuration(bool reducedFlash) =>
            reducedFlash ? RibbonHoldReducedFlashSeconds : RibbonHoldSeconds;

        public static string FormatPoints(uint points) => "+" + points.ToString();
    }
}
