#nullable enable
using System.Collections.Generic;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Menus;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    /// <summary>ADR-114: the Career page tabs and progress line never overlap at 720p–1080p and 100–150% UI scale.</summary>
    public sealed class CareerLayoutTests
    {
        private const int Ranks = 20;

        public static IEnumerable<object[]> Screens()
        {
            foreach (var (w, h) in new[] { (1280, 720), (1920, 1080), (1366, 768), (1600, 900) })
                foreach (float s in new[] { 1f, 1.25f, 1.5f })
                    yield return new object[] { w, h, s };
        }

        private static CareerLayout Layout(int w, int h, float scale) => CareerLayout.Build(CareerLayout.SafeFor(w / scale, h / scale));

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
        public void Page_bands_stay_inside_the_safe_area_and_apart(int w, int h, float scale)
        {
            var l = Layout(w, h, scale);
            foreach (var r in new[] { l.Tabs, l.Content, l.Back, l.Hero, l.Track, l.PerksLabel, l.Perks, l.RecordLabel, l.Record })
                Inside(r, l.Safe, "band");
            Apart(l.Tabs, l.Content, "tabs/content");
            Apart(l.Content, l.Back, "content/back");
            Apart(l.Hero, l.Track, "hero/track");
            Apart(l.Perks, l.RecordLabel, "perks/record label");
            Apart(l.RecordLabel, l.Record, "record label/grid");
            Inside(l.Hero, l.Content, "hero"); Inside(l.Track, l.Content, "track");
            Inside(l.Record, l.Content, "record");
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public void Service_record_tiles_are_stacked_and_apart(int w, int h, float scale)
        {
            var l = Layout(w, h, scale);
            int n = l.RecordColumns * l.RecordRows;
            Assert.Equal(12, n);
            for (int i = 0; i < n; i++)
            {
                var t = l.RecordTile(i);
                Inside(t, l.Record, "tile " + i);
                Assert.True(t.Height >= CareerLayout.MinTileHeight, $"tile {i} only {t.Height} px tall");
                for (int j = i + 1; j < n; j++) Apart(t, l.RecordTile(j), $"tiles {i}/{j}");
            }
            for (int count = 1; count <= 5; count++)
                for (int i = 0; i < count; i++)
                {
                    Inside(l.PerkChip(i, count), l.Perks, "perk chip");
                    if (i > 0) Apart(l.PerkChip(i - 1, count), l.PerkChip(i, count), "perk chips");
                }
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public void Progress_line_parts_never_touch(int w, int h, float scale)
        {
            var l = Layout(w, h, scale);
            var line = CareerLineLayout.Build(l.Track, Ranks);
            Inside(line.SectionLabel, line.Track, "section label");
            Inside(line.PrevArrow, line.Track, "prev arrow");
            Inside(line.NextArrow, line.Track, "next arrow");
            Apart(line.PrevArrow, line.Inner, "prev arrow/columns");
            Apart(line.NextArrow, line.Inner, "next arrow/columns");
            for (int slot = 0; slot < line.Visible; slot++)
            {
                foreach (bool current in new[] { false, true })
                {
                    var halo = line.HaloRect(slot, current);
                    Apart(halo, line.SectionLabel, $"slot {slot} halo/section label");
                    Inside(halo, line.Track, $"slot {slot} halo");
                    Apart(halo, line.CodeLabel(slot, current), $"slot {slot} halo/code");
                    Inside(line.CpLabel(slot, current), line.Track, $"slot {slot} cp label");
                    if (!line.CardsShown) continue;
                    var card = line.Card(slot);
                    var lifted = new HudRect(card.X, card.Y - CareerLineLayout.HoverLift, card.Width, card.Height);
                    Apart(lifted, line.CpLabel(slot, current), $"slot {slot} card/cp label");
                    Apart(lifted, line.CodeLabel(slot, current), $"slot {slot} card/code label");
                    Inside(card, line.Track, $"slot {slot} card");
                    Apart(card, line.PositionRail, $"slot {slot} card/position rail");
                }
                if (slot > 0) Apart(line.HaloRect(slot - 1, false), line.HaloRect(slot, true), $"halo {slot - 1}/{slot}");
            }
        }

        [Theory]
        [InlineData(1280, 720, 1f)]
        [InlineData(1280, 720, 1.25f)]
        [InlineData(1920, 1080, 1f)]
        [InlineData(1920, 1080, 1.5f)]
        [InlineData(1366, 768, 1f)]
        public void Reward_cards_have_room_on_common_screens(int w, int h, float scale)
        {
            var line = CareerLineLayout.Build(Layout(w, h, scale).Track, Ranks);
            Assert.True(line.CardsShown, $"cards dropped: {line.CardHeight} px");
            Assert.True(line.Visible >= 3);
        }

        [Fact]
        public void Tabs_cycle_both_ways()
        {
            Assert.Equal(CareerTab.Statistics, CareerTabs.Step(CareerTab.Progress, 1));
            Assert.Equal(CareerTab.Progress, CareerTabs.Step(CareerTab.Statistics, 1));
            Assert.Equal(CareerTab.Statistics, CareerTabs.Step(CareerTab.Progress, -1));
        }
    }
}
