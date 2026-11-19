#nullable enable
using System;
using Shooter.Presentation.Hud;

namespace Shooter.Presentation.Menus
{
    /// <summary>
    /// ADR-123 shared page skeleton in logical UI pixels: header band, content band, the action bar and the key-hint
    /// footer. Every shell page is placed inside it, so headers, bars and hints sit at the same place on every page.
    /// </summary>
    public static class ShellPage
    {
        /// <summary>Header (eyebrow, title, rule) height from the safe-area top.</summary>
        public const float HeaderHeight = 74f;
        /// <summary>Key-hint footer height at the safe-area bottom.</summary>
        public const float FooterHeight = 28f;
        public const float ActionHeight = 44f;
        public const float Gap = 12f;

        public static HudRect SafeFor(float logicalWidth, float logicalHeight) => CareerLayout.SafeFor(logicalWidth, logicalHeight);

        /// <summary>The action bar (Back / Apply / Start) just above the footer.</summary>
        public static HudRect Actions(HudRect safe) =>
            new HudRect(safe.X, safe.Bottom - FooterHeight - ActionHeight, safe.Width, ActionHeight);

        public static HudRect Footer(HudRect safe) => new HudRect(safe.X, safe.Bottom - FooterHeight, safe.Width, FooterHeight);

        internal static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }

    /// <summary>ADR-123 Main page: brand rail on the left over the vista, operation cards along the bottom right.</summary>
    public sealed class MainMenuLayout
    {
        public const int NavCount = 5;
        /// <summary>Below this safe-area height the headline collapses to one line and nav rows tighten.</summary>
        public const float CompactBelow = 560f;

        public HudRect Safe { get; }
        public bool Compact { get; }
        public HudRect Brand { get; }
        public HudRect Headline { get; }
        public HudRect Loadout { get; }
        public HudRect Rank { get; }
        public HudRect CardsLabel { get; }
        public HudRect Cards { get; }
        public HudRect Footer { get; }
        public float NavRowHeight { get; }
        private readonly float navTop, navPitch, rail;

        private MainMenuLayout(HudRect safe)
        {
            Safe = safe;
            Compact = safe.Height < CompactBelow;
            rail = ShellPage.Clamp(safe.Width * .24f, 220f, 300f);
            Footer = ShellPage.Footer(safe);
            Brand = new HudRect(safe.X, safe.Y, rail, 30f);
            Headline = new HudRect(safe.X, Brand.Bottom + (Compact ? 10f : 18f), rail + 60f, Compact ? 30f : 100f);
            NavRowHeight = Compact ? 32f : 38f;
            navPitch = NavRowHeight + 4f;
            navTop = Headline.Bottom + (Compact ? 12f : 16f);
            float navBottom = navTop + NavCount * navPitch - 4f;
            Rank = new HudRect(safe.X, navBottom + (Compact ? 10f : 12f), rail + 40f, Compact ? 44f : 48f);
            Loadout = new HudRect(safe.X, Rank.Bottom + (Compact ? 10f : 12f), rail + 40f, Compact ? 54f : 64f);
            if (Loadout.Bottom > Footer.Y - 4f) throw new ArgumentOutOfRangeException(nameof(safe), "Main rail does not fit");

            float cardsX = safe.X + rail + 72f;
            float cardsW = safe.Right - cardsX;
            if (cardsW < 360f) throw new ArgumentOutOfRangeException(nameof(safe), "Operation cards need 360 px");
            float cardH = ShellPage.Clamp(safe.Height * .34f, 150f, 212f);
            Cards = new HudRect(cardsX, Footer.Y - 14f - cardH, cardsW, cardH);
            CardsLabel = new HudRect(cardsX, Cards.Y - 24f, cardsW, 16f);
        }

        public static MainMenuLayout Build(HudRect safe) => new MainMenuLayout(safe);

        public HudRect Nav(int index) => new HudRect(Safe.X, navTop + index * navPitch, rail, NavRowHeight);

        public HudRect Card(int index, int count)
        {
            const float gap = 12f;
            float w = (Cards.Width - gap * (count - 1)) / count;
            return new HudRect(Cards.X + index * (w + gap), Cards.Y, w, Cards.Height);
        }
    }

    /// <summary>ADR-123 Settings page: tab strip, a control column, an optional help pane and the action bar.</summary>
    public sealed class SettingsLayout
    {
        /// <summary>Below this safe width the help pane is dropped and controls take the full width.</summary>
        public const float HelpPaneFrom = 900f;
        public const float TabHeight = 36f;

        public HudRect Safe { get; }
        public HudRect Tabs { get; }
        public HudRect Controls { get; }
        public HudRect? Help { get; }
        public HudRect Actions { get; }
        public HudRect Notice { get; }

        private SettingsLayout(HudRect safe)
        {
            Safe = safe;
            Tabs = new HudRect(safe.X, safe.Y + ShellPage.HeaderHeight, safe.Width, TabHeight);
            Actions = ShellPage.Actions(safe);
            Notice = new HudRect(safe.X, Actions.Y - 30f, safe.Width, 24f);
            float top = Tabs.Bottom + ShellPage.Gap, bottom = Notice.Y - 6f;
            if (bottom - top < 120f) throw new ArgumentOutOfRangeException(nameof(safe), "Settings need 120 px of content");
            if (safe.Width >= HelpPaneFrom)
            {
                float helpW = ShellPage.Clamp(safe.Width * .3f, 260f, 380f);
                Controls = new HudRect(safe.X, top, safe.Width - helpW - 24f, bottom - top);
                Help = new HudRect(Controls.Right + 24f, top, helpW, bottom - top);
            }
            else
            {
                Controls = new HudRect(safe.X, top, safe.Width, bottom - top);
                Help = null;
            }
        }

        public static SettingsLayout Build(HudRect safe) => new SettingsLayout(safe);

        /// <summary>One of four equal action buttons.</summary>
        public HudRect Action(int index)
        {
            const float gap = 8f;
            float w = Math.Min(220f, (Actions.Width - gap * 3f) / 4f);
            return new HudRect(Actions.X + index * (w + gap), Actions.Y, w, Actions.Height);
        }
    }

    /// <summary>ADR-123 Armory page: slots, the weapon list, and a hero panel with the sights under it.</summary>
    public sealed class ArmoryLayout
    {
        public const float SectionHeight = 18f;

        public HudRect Safe { get; }
        public HudRect Slots { get; }
        public HudRect List { get; }
        public HudRect Hero { get; }
        public HudRect Sights { get; }
        public HudRect Actions { get; }

        private ArmoryLayout(HudRect safe)
        {
            Safe = safe;
            Actions = ShellPage.Actions(safe);
            float top = safe.Y + ShellPage.HeaderHeight, bottom = Actions.Y - ShellPage.Gap;
            if (bottom - top < 200f) throw new ArgumentOutOfRangeException(nameof(safe), "Armory needs 200 px of content");
            const float gap = 16f;
            float slotW = ShellPage.Clamp(safe.Width * .19f, 170f, 240f);
            float sideW = ShellPage.Clamp(safe.Width * .3f, 250f, 380f);
            Slots = new HudRect(safe.X, top, slotW, bottom - top);
            List = new HudRect(Slots.Right + gap, top, safe.Width - slotW - sideW - gap * 2f, bottom - top);
            if (List.Width < 240f) throw new ArgumentOutOfRangeException(nameof(safe), "Weapon list needs 240 px");
            float heroH = ShellPage.Clamp((bottom - top) * .42f, 128f, 210f);
            Hero = new HudRect(List.Right + gap, top + SectionHeight + 6f, sideW, heroH);
            Sights = new HudRect(Hero.X, Hero.Bottom + ShellPage.Gap, sideW, bottom - Hero.Bottom - ShellPage.Gap);
        }

        public static ArmoryLayout Build(HudRect safe) => new ArmoryLayout(safe);
    }
}
