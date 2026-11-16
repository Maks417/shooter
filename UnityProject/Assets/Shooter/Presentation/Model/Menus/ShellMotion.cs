#nullable enable
using System;
using Shooter.Presentation.Hud;

namespace Shooter.Presentation.Menus
{
    /// <summary>
    /// ADR-086 front-end motion: screen enter transitions and the menu stage camera drift.
    /// Cosmetic only; reduced camera motion removes movement (screens still fade, briefly).
    /// </summary>
    public static class ShellMotion
    {
        public const float EnterSeconds = 0.26f;
        public const float EnterReducedSeconds = 0.12f;
        public const float EnterSlide = 18f;

        /// <summary>
        /// Full-screen pages that fade in from the dark veil. Gameplay and the pause/confirm overlays never veil,
        /// so opening them over the live match does not flash the screen.
        /// </summary>
        public static bool AnimatesEnter(ShellScreen screen) =>
            screen != ShellScreen.Playing && screen != ShellScreen.Paused &&
            screen != ShellScreen.ConfirmLeave && screen != ShellScreen.ConfirmRestart;

        public static bool SlidesOnEnter(ShellScreen screen) =>
            screen == ShellScreen.Main || screen == ShellScreen.Setup || screen == ShellScreen.Settings || screen == ShellScreen.Results;

        /// <summary>Opacity of the dark veil laid over a freshly entered screen (1 → 0).</summary>
        public static float EnterVeil(ShellScreen screen, float sinceEnter, bool reducedMotion)
        {
            if (!AnimatesEnter(screen)) return 0f;
            float duration = reducedMotion ? EnterReducedSeconds : EnterSeconds;
            return 1f - UiMotion.OutCubic(UiMotion.Progress(sinceEnter, duration));
        }

        /// <summary>Vertical content offset in logical px (rises into place); zero under reduced motion.</summary>
        public static float EnterOffset(ShellScreen screen, float sinceEnter, bool reducedMotion)
        {
            if (reducedMotion || !SlidesOnEnter(screen)) return 0f;
            return (1f - UiMotion.OutCubic(UiMotion.Progress(sinceEnter, EnterSeconds * 1.4f))) * EnterSlide;
        }

        /// <summary>
        /// Stage camera drift (metres, camera-local x/y/z) — two slow incommensurate sways plus a short push-in
        /// after the menu opens. Bounded by <see cref="MaxDriftMeters"/>; zero under reduced motion.
        /// </summary>
        public static (float X, float Y, float Z) CameraDrift(double sceneSeconds, float sinceOpen, bool reducedMotion)
        {
            if (reducedMotion) return (0f, 0f, 0f);
            double t = sceneSeconds;
            float x = (float)(Math.Sin(t * (2 * Math.PI / 23.0)) * 0.065 + Math.Sin(t * (2 * Math.PI / 37.0) + 1.3) * 0.025);
            float y = (float)(Math.Sin(t * (2 * Math.PI / 29.0) + 0.7) * 0.018);
            float push = 1f - UiMotion.OutCubic(UiMotion.Progress(sinceOpen, 3.2f));
            float z = -0.35f * push;
            return (x, y, z);
        }

        public const float MaxDriftMeters = 0.6f;

        /// <summary>Pointer parallax target (metres) from a normalised pointer in [-1, 1]; zero under reduced motion.</summary>
        public static (float X, float Y) PointerParallax(float pointerX, float pointerY, bool reducedMotion)
        {
            if (reducedMotion) return (0f, 0f);
            float px = Math.Max(-1f, Math.Min(1f, pointerX)), py = Math.Max(-1f, Math.Min(1f, pointerY));
            return (px * 0.04f, py * 0.02f);
        }
    }
}
