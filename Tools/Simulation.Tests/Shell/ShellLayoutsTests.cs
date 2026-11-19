#nullable enable
using System.Collections.Generic;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Menus;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    /// <summary>ADR-123: Main, Settings and Armory pages never overlap at 720p-1080p and 100-150% UI scale.</summary>
    public sealed class ShellLayoutsTests
    {
        public static IEnumerable<object[]> Screens()
        {
            foreach (var (w, h) in new[] { (1280, 720), (1366, 768), (1600, 900), (1920, 1080) })
                foreach (float s in new[] { 1f, 1.25f, 1.5f })
                    yield return new object[] { w, h, s };
        }

        private static HudRect Safe(int w, int h, float s) => ShellPage.SafeFor(w / s, h / s);

        private static void Inside(HudRect inner, HudRect outer, string what)
        {
            const float e = .01f;
            Assert.True(inner.X >= outer.X - e && inner.Y >= outer.Y - e && inner.Right <= outer.Right + e && inner.Bottom <= outer.Bottom + e,
                $"{what} ({inner.X},{inner.Y},{inner.Width},{inner.Height}) leaves ({outer.X},{outer.Y},{outer.Width},{outer.Height})");
        }

        private static void Apart(HudRect a, HudRect b, string what) =>
            Assert.False(a.Intersects(b), $"{what}: ({a.X},{a.Y},{a.Width},{a.Height}) overlaps ({b.X},{b.Y},{b.Width},{b.Height})");

        [Theory]
        [MemberData(nameof(Screens))]
        public void Main_rail_cards_and_footer_never_overlap(int w, int h, float s)
        {
            var safe = Safe(w, h, s);
            var l = MainMenuLayout.Build(safe);
            var rail = new List<HudRect> { l.Brand, l.Headline };
            for (int i = 0; i < MainMenuLayout.NavCount; i++) rail.Add(l.Nav(i));
            rail.Add(l.Rank);
            rail.Add(l.Loadout);
            for (int i = 0; i < rail.Count; i++)
            {
                Inside(rail[i], safe, "rail " + i);
                Apart(rail[i], l.Footer, "rail " + i + "/footer");
                Apart(rail[i], l.Cards, "rail " + i + "/cards");
                Apart(rail[i], l.CardsLabel, "rail " + i + "/cards label");
                for (int j = i + 1; j < rail.Count; j++) Apart(rail[i], rail[j], "rail " + i + "/" + j);
            }
            for (int i = 0; i < 3; i++)
            {
                Inside(l.Card(i, 3), l.Cards, "card " + i);
                Assert.True(l.Card(i, 3).Width >= 150f, "card " + i + " is too narrow");
                if (i > 0) Apart(l.Card(i - 1, 3), l.Card(i, 3), "cards");
            }
            Apart(l.Cards, l.Footer, "cards/footer");
            Apart(l.CardsLabel, l.Cards, "cards label/cards");
            Assert.True(l.NavRowHeight >= 32f, "nav rows keep the 32 px minimum target");
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public void Settings_bands_never_overlap(int w, int h, float s)
        {
            var safe = Safe(w, h, s);
            var l = SettingsLayout.Build(safe);
            foreach (var r in new[] { l.Tabs, l.Controls, l.Actions, l.Notice }) Inside(r, safe, "band");
            Apart(l.Tabs, l.Controls, "tabs/controls");
            Apart(l.Controls, l.Notice, "controls/notice");
            Apart(l.Notice, l.Actions, "notice/actions");
            if (l.Help.HasValue)
            {
                Inside(l.Help.Value, safe, "help");
                Apart(l.Help.Value, l.Controls, "help/controls");
            }
            for (int i = 0; i < 4; i++)
            {
                Inside(l.Action(i), l.Actions, "action " + i);
                if (i > 0) Apart(l.Action(i - 1), l.Action(i), "actions");
            }
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public void Armory_columns_never_overlap(int w, int h, float s)
        {
            var safe = Safe(w, h, s);
            var l = ArmoryLayout.Build(safe);
            foreach (var r in new[] { l.Slots, l.List, l.Hero, l.Sights, l.Actions }) Inside(r, safe, "band");
            Apart(l.Slots, l.List, "slots/list");
            Apart(l.List, l.Hero, "list/hero");
            Apart(l.List, l.Sights, "list/sights");
            Apart(l.Hero, l.Sights, "hero/sights");
            foreach (var r in new[] { l.Slots, l.List, l.Hero, l.Sights }) Apart(r, l.Actions, "column/actions");
        }
    }
}
