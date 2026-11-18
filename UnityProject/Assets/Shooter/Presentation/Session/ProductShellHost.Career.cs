#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Settings;
using Shooter.Generation.Icons;
using Shooter.Presentation.Menus;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// ADR-108 career: rank and service record loaded from <c>shooter-career.json</c>, recorded once per finished match,
    /// shown on the Career page, the Armory header, the main menu, every scoreboard row and the debrief strip.
    /// ADR-113 draws it with layered metal emblems, recessed CP bars, a horizontal progress line and reward cards
    /// (<see cref="CareerArt"/>). Presentation only reads authority results; the buffs reach the simulation through
    /// the session's Armory loadout.
    /// </summary>
    public sealed partial class ProductShellHost
    {
        private ICareerStore? careerStore;
        private CareerRecorder career = new CareerRecorder(CareerProfile.Empty);
        private string careerStatus = "";
        // First visible rank (0-based) of the progress line; -1 recentres on the current rank.
        private int careerFirstRow = -1;
        private float careerOpenedAt = -1f;

        private int CareerRank => career.Profile.Rank;

        private void LoadCareer(string path)
        {
            careerStore = new FileCareerStore(path);
            var result = careerStore.Load(out var profile);
            career = new CareerRecorder(profile);
            careerStatus = result.Succeeded ? "" : result.UserFacing;
            Debug.Log("SHOOTER_CAREER rank=" + profile.Rank + " cp=" + profile.CareerPoints + " matches=" + profile.Matches + " file=" + path);
        }

        /// <summary>Lock rule handed to the Armory: guns and sights above the current rank stay locked.</summary>
        private Func<DefinitionId, bool> CareerLockRule() => id => !CareerLadder.IsUnlocked(id.ToString(), CareerRank);

        /// <summary>Records a finished match once (the host sees Finished every frame), saves, and applies a promotion.</summary>
        private void RecordCareer(MatchResults results)
        {
            if (session == null) return;
            var tally = session.LocalTally;
            int before = CareerRank;
            if (!career.TryRecord(session.SessionEpoch, results, new CareerMatchExtras(tally.Assists, tally.Headshots, tally.BestStreak))) return;
            if (careerStore != null)
            {
                var saved = careerStore.Save(career.Profile);
                careerStatus = saved.Succeeded ? "" : ShellUiCopy.CareerSaveFailed;
            }
            if (CareerRank != before && armory != null)
            {
                armory.SetLocks(CareerLockRule());
                SaveArmoryIfChanged();
            }
            ApplyArmoryToSession();
            var award = career.LastAward;
            if (award != null)
                Debug.Log("SHOOTER_CAREER award=+" + award.Total + " rank=" + award.RankBefore + "->" + award.RankAfter + " cp=" + career.Profile.CareerPoints);
        }

        private void OpenCareer()
        {
            if (navigation.Screen == ShellScreen.Armory) SaveArmoryIfChanged();
            careerFirstRow = -1;
            careerOpenedAt = -1f;
            navigation.OpenCareer();
            ApplyCursorForScreen();
        }

        private void CloseCareer()
        {
            navigation.CloseCareer();
            ApplyCursorForScreen();
        }

        // ------------------------------------------------------------ insignia

        /// <summary>ADR-113 layered rank emblem; false when the atlas glyph is unavailable (callers keep the text).</summary>
        private static bool DrawRankIcon(Rect r, int rank, float alpha, float glint = -1f, bool dim = false) =>
            CareerArt.RankEmblem(r, rank, alpha, glint, dim);

        /// <summary>Rank emblem, name, CP line and a recessed segmented CP bar; a click opens the Career page.</summary>
        private void DrawRankBadge(Rect r, string id, bool alignRight = false)
        {
            var profile = career.Profile;
            int rank = profile.Rank;
            var def = CareerLadder.Get(rank);
            var next = CareerLadder.Next(rank);
            var pal = CareerArt.Palette(rank);
            if (ui.Button(r, id, "", quiet: true)) OpenCareer();
            float icon = r.height - 6f;
            float ix = alignRight ? r.xMax - icon - 6f : r.x + 6f;
            DrawRankIcon(new Rect(ix, r.y + 3, icon, icon), rank, 1f);
            float tx = alignRight ? r.x + 8f : ix + icon + 10f;
            float tw = r.width - icon - 26f;
            TextAnchor anchor = alignRight ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            ui.Label(new Rect(tx, r.y + 2, tw, 18), def.Name.ToUpperInvariant(), 15, pal.MetalTop, anchor);
            string progress = next == null ? ShellUiCopy.CareerTopRank : ShellUiCopy.CareerProgress(profile.CareerPoints, next.Threshold);
            ui.Label(new Rect(tx, r.y + 19, tw, 14), progress, 11, TacticalUi.Muted, anchor);
            CareerArt.ProgressBar(new Rect(tx, r.yMax - 10, tw, 7), CareerLadder.Progress01(profile.CareerPoints), pal.MetalTop, pal.MetalBottom, 10, 1f);
        }

        // ------------------------------------------------------------ Armory locks

        private static string LockedText(DefinitionId id) =>
            ShellUiCopy.UnlocksAt(CareerLadder.Get(CareerLadder.UnlockRankFor(id.ToString())).Name);

        private void DrawLockTag(Rect r)
        {
            TacticalUi.Plate(r, UiSdf.Style.Solid(new Color(0f, 0f, 0f, .35f), r.height * .5f));
            float x = r.x + 6f;
            if (TacticalUi.Icon(new Rect(x, r.y + 2f, r.height - 4f, r.height - 4f), IconLibrary.UiLock, TacticalUi.Muted)) x += r.height;
            ui.Label(new Rect(x, r.y, r.xMax - x - 4f, r.height), ShellUiCopy.ArmoryLocked, 10, TacticalUi.Muted, TextAnchor.MiddleLeft);
        }

        private void DrawLockedWeaponRow(Rect row, DefinitionId weapon)
        {
            float iconW = Mathf.Min(150, row.width * .3f);
            Color dim = new Color(TacticalUi.Muted.r, TacticalUi.Muted.g, TacticalUi.Muted.b, .45f);
            if (Shooter.Presentation.View.WeaponVisualProfiles.TryGet(weapon, out var profile))
                TacticalUi.KillFeedWeaponIcon(new Rect(row.x + 14, row.y + row.height * .2f, iconW, row.height * .6f), profile.KillFeedGlyph, dim);
            float tx = row.x + iconW + 28;
            float tw = row.xMax - tx - 12;
            ui.Label(new Rect(tx, row.y + 6, tw, 22), ArmorySelection.LabelFor(weapon), 17, TacticalUi.Muted);
            int unlock = CareerLadder.UnlockRankFor(weapon.ToString());
            float icon = 20f;
            DrawRankIcon(new Rect(tx, row.y + 27, icon, icon), unlock, .9f);
            ui.Label(new Rect(tx + icon + 6, row.y + 29, tw - icon - 6, 18), LockedText(weapon), 12, TacticalUi.Muted);
            DrawLockTag(new Rect(row.xMax - 84, row.y + 7, 74, 16));
        }

        // ------------------------------------------------------------ Career page

        private void DrawCareer()
        {
            ui.Backdrop();
            ui.Header(ShellUiCopy.CareerEyebrow, ShellUiCopy.CareerTitle, "");
            Rect a = ui.Safe;
            var profile = career.Profile;
            float top = a.y + 74;
            float bottom = a.yMax - 74;
            float content = bottom - top;
            if (careerOpenedAt < 0f) careerOpenedAt = Time.unscaledTime;
            float opened = Time.unscaledTime - careerOpenedAt;

            float heroH = Mathf.Clamp(content * .27f, 124f, 160f);
            float lowerH = Mathf.Clamp(content * .25f, 120f, 196f);
            const float gap = 14f;
            Rect hero = new Rect(a.x, top, a.width, heroH);
            Rect lower = new Rect(a.x, bottom - lowerH, a.width, lowerH);
            Rect track = new Rect(a.x, hero.yMax + gap, a.width, lower.y - gap - hero.yMax - gap);

            DrawCareerHero(hero, profile, opened);
            DrawProgressLine(track, profile, opened);

            float buffsW = Mathf.Min(a.width * .38f, 520f);
            ui.Section(new Rect(lower.x, lower.y, buffsW, 18), ShellUiCopy.CareerActiveBuffs);
            DrawActiveBuffs(new Rect(lower.x, lower.y + 24, buffsW, lower.height - 24), CareerLadder.BuffsFor(profile.Rank));
            float recordX = lower.x + buffsW + 18f;
            ui.Section(new Rect(recordX, lower.y, lower.xMax - recordX, 18), ShellUiCopy.CareerStatsSection);
            DrawServiceRecord(new Rect(recordX, lower.y + 24, lower.xMax - recordX, lower.height - 24), profile);

            ui.Panel(new Rect(a.x, a.yMax - 66, a.width, 44), accentTop: true);
            ui.Label(new Rect(a.x + 170, a.yMax - 56, a.width - 190, 30), ShellUiCopy.CareerHint, 13, TacticalUi.Muted, TextAnchor.MiddleLeft);
            if (ui.Button(new Rect(a.x, a.yMax - 66, 150, 44), "career.back", ShellUiCopy.SetupBack)) CloseCareer();
            DrawFooter(careerStatus);
        }

        /// <summary>Hero band: big emblem, rank, CP bar and the next reward card.</summary>
        private void DrawCareerHero(Rect hero, CareerProfile profile, float opened)
        {
            int rank = profile.Rank;
            var def = CareerLadder.Get(rank);
            var next = CareerLadder.Next(rank);
            var pal = CareerArt.Palette(rank);
            ui.Panel(hero, accentTop: true);
            if (UiSdf.Available)
            {
                Color wash = pal.MetalBottom;
                UiSdf.Rect(new Rect(hero.x, hero.y, hero.width * .5f, hero.height),
                    UiSdf.Style.Solid(new Color(wash.r, wash.g, wash.b, .14f), Hud.UiShapeParams.PlateRadius)
                        .WithGradient(new Color(wash.r, wash.g, wash.b, 0f), UiSdf.Gradient.Horizontal));
            }
            float emblem = hero.height - 20f;
            float glint = ui.ReducedFlash || ui.ReducedMotion ? -1f : Hud.UiMotion.GlintPosition(opened, ui.ReducedFlash);
            DrawRankIcon(new Rect(hero.x + 16, hero.y + 10, emblem, emblem), rank, 1f, glint);

            float cardW = next == null ? 0f : Mathf.Min(360f, hero.width * .32f);
            float tx = hero.x + emblem + 34f;
            float tw = hero.xMax - tx - cardW - 36f;
            ui.Label(new Rect(tx, hero.y + 14, tw, 14), ShellUiCopy.CareerRankOf(def.Code, rank, CareerLadder.RankCount), 11, TacticalUi.Muted);
            ui.Label(new Rect(tx, hero.y + 28, tw, 38), def.Name.ToUpperInvariant(), 30, pal.MetalTop);
            float barY = hero.y + Mathf.Max(74f, hero.height * .56f);
            CareerArt.ProgressBar(new Rect(tx, barY, tw, 14), CareerLadder.Progress01(profile.CareerPoints), pal.MetalTop, pal.MetalBottom, 10, 1f);
            ui.Label(new Rect(tx, barY + 20, tw * .5f, 18),
                next == null ? ShellUiCopy.CareerTopRank : ShellUiCopy.CareerProgress(profile.CareerPoints, next.Threshold), 13, TacticalUi.Text);
            if (next == null) return;
            ui.Label(new Rect(tx + tw * .5f, barY + 20, tw * .5f, 18), ShellUiCopy.CareerToNext(next.Threshold - profile.CareerPoints, next.Name), 12,
                TacticalUi.Accent, TextAnchor.UpperRight);

            Rect card = new Rect(hero.xMax - cardW - 14f, hero.y + 30f, cardW, hero.height - 42f);
            ui.Label(new Rect(card.x, hero.y + 10, card.width, 16), ShellUiCopy.CareerNextReward + "  ·  " + next.Name.ToUpperInvariant(), 11, TacticalUi.Accent);
            float pulse = ui.ReducedFlash ? 0f : Hud.UiMotion.Pulse(Time.unscaledTime, 2.4f, ui.ReducedFlash);
            CareerArt.RewardCard(ui, card, CareerRewardCatalog.For(next), CareerArt.RewardState.Next, pulse, 1f, -1f, ui.ReducedFlash);
        }

        /// <summary>
        /// ADR-113 horizontal progress line: a recessed rail through one emblem node per rank, filled up to the
        /// current CP, with each rank's reward card under its node. Scrolls with the wheel and the end arrows.
        /// </summary>
        private void DrawProgressLine(Rect r, CareerProfile profile, float opened)
        {
            int rank = profile.Rank;
            const float arrowW = 30f;
            Rect inner = new Rect(r.x + arrowW + 10f, r.y, r.width - (arrowW + 10f) * 2f, r.height);
            int visible = Mathf.Clamp(Mathf.FloorToInt(inner.width / 170f), 3, 8);
            int maxFirst = CareerLadder.RankCount - visible;
            if (careerFirstRow < 0) careerFirstRow = Mathf.Clamp(rank - visible / 2, 0, maxFirst);
            var ev = Event.current;
            if (ev != null && ev.type == EventType.ScrollWheel && r.Contains(ev.mousePosition))
            {
                float delta = Mathf.Abs(ev.delta.y) >= Mathf.Abs(ev.delta.x) ? ev.delta.y : ev.delta.x;
                careerFirstRow += delta > 0 ? 1 : -1;
                ev.Use();
            }
            careerFirstRow = Mathf.Clamp(careerFirstRow, 0, maxFirst);

            ui.Panel(r);
            ui.Label(new Rect(inner.x, r.y + 8, inner.width * .5f, 16), ShellUiCopy.CareerLadderSection, 12, TacticalUi.Accent);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && careerFirstRow > 0;
            if (ui.Button(new Rect(r.x + 6, r.center.y - 30, arrowW, 60), "career.prev", "<", quiet: true)) careerFirstRow--;
            GUI.enabled = wasEnabled && careerFirstRow < maxFirst;
            if (ui.Button(new Rect(r.xMax - arrowW - 6, r.center.y - 30, arrowW, 60), "career.next", ">", quiet: true)) careerFirstRow++;
            GUI.enabled = wasEnabled;
            careerFirstRow = Mathf.Clamp(careerFirstRow, 0, maxFirst);

            float colW = inner.width / visible;
            float node = Mathf.Clamp(Mathf.Min(colW * .36f, r.height * .2f), 34f, 64f);
            float lineY = r.y + 34f + node * .5f;
            var pal = CareerArt.Palette(rank);

            // Rail: groove across the window, filled up to the current CP position measured in rank units.
            float x0 = inner.x + colW * .5f;
            Rect rail = new Rect(inner.x + 4f, lineY - 4f, inner.width - 8f, 8f);
            float position = (rank - 1) + (rank >= CareerLadder.RankCount ? 0f : CareerLadder.Progress01(profile.CareerPoints));
            float fillX = Mathf.Clamp(x0 + (position - careerFirstRow) * colW, rail.x, rail.xMax);
            CareerArt.ProgressBar(rail, (fillX - rail.x) / rail.width, pal.MetalTop, pal.MetalBottom, 0, 1f);

            float cardBottom = r.yMax - 16f;
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : new Vector2(-1f, -1f);
            for (int slot = 0; slot < visible; slot++)
            {
                int index = careerFirstRow + slot + 1;
                var step = CareerLadder.Get(index);
                bool reached = index <= rank;
                bool current = index == rank;
                bool isNext = index == rank + 1;
                float cx = x0 + slot * colW;
                Rect column = new Rect(cx - colW * .5f, r.y + 26f, colW, r.height - 30f);
                float emphasis = ui.Emphasis("career.rank." + index, column.Contains(mouse));

                float size = current ? node * 1.22f : node;
                Rect nodeR = new Rect(cx - size * .5f, lineY - size * .5f, size, size);
                if (UiSdf.Available)
                {
                    float disc = current ? size * 1.32f : size * 1.08f;
                    Rect discR = new Rect(cx - disc * .5f, lineY - disc * .5f, disc, disc);
                    UiSdf.Circle(discR, UiSdf.Style.Solid(new Color(.02f, .03f, .035f, .85f)));
                    if (current)
                    {
                        var ring = UiSdf.Style.Solid(TacticalUi.Accent).WithGlow(TacticalUi.Accent, 10f, ui.ReducedFlash ? .2f : .35f);
                        ring.Stroke = 2f;
                        UiSdf.Circle(discR, ring);
                    }
                }
                float glint = current && !ui.ReducedFlash && !ui.ReducedMotion ? Hud.UiMotion.GlintPosition(opened + .4f, false) : -1f;
                DrawRankIcon(nodeR, index, reached ? 1f : .85f, glint, dim: !reached);

                Color nameInk = reached ? CareerArt.Palette(index).MetalTop : TacticalUi.Muted;
                float labelY = lineY + size * .5f + (current ? 10f : 6f);
                ui.Label(new Rect(column.x + 4, labelY, column.width - 8, 16), current ? ShellUiCopy.CareerCurrent : step.Code, 12,
                    current ? TacticalUi.Accent : nameInk, TextAnchor.UpperCenter);
                ui.Label(new Rect(column.x + 4, labelY + 15, column.width - 8, 14), ShellUiCopy.Thousands(step.Threshold) + " CP", 10,
                    TacticalUi.Muted, TextAnchor.UpperCenter);

                float cardY = lineY + node * .61f + 50f;
                float lift = ui.ReducedMotion ? 0f : 4f * emphasis;
                Rect card = new Rect(column.x + 6f, cardY - lift, column.width - 12f, cardBottom - cardY);
                if (card.height < 52f) continue;
                var state = reached ? CareerArt.RewardState.Earned : isNext ? CareerArt.RewardState.Next : CareerArt.RewardState.Locked;
                float alpha = ui.ReducedMotion ? 1f : Hud.UiMotion.EnterAlpha(opened - slot * .04f);
                CareerArt.RewardCard(ui, card, CareerRewardCatalog.For(index), state, emphasis, alpha, -1f, ui.ReducedFlash);
            }

            // Window position rail.
            Rect pos = new Rect(inner.x + inner.width * .25f, r.yMax - 8f, inner.width * .5f, 3f);
            TacticalUi.Plate(pos, UiSdf.Style.Solid(new Color(1f, 1f, 1f, .08f), 1.5f));
            float thumbW = pos.width * visible / CareerLadder.RankCount;
            float thumbX = pos.x + (pos.width - thumbW) * (maxFirst <= 0 ? 0f : careerFirstRow / (float)maxFirst);
            TacticalUi.Plate(new Rect(thumbX, pos.y, thumbW, pos.height), UiSdf.Style.Solid(TacticalUi.Accent, 1.5f));
        }

        private void DrawActiveBuffs(Rect r, CareerBuffs b)
        {
            var chips = new System.Collections.Generic.List<(string Icon, string Name, string Value)>(5);
            if (b.ReloadPerMille < 1000) chips.Add((IconLibrary.RewardFastHands, "Reload", "-" + Percent(b.ReloadPerMille)));
            if (b.AdsSpreadPerMille < 1000) chips.Add((IconLibrary.RewardSteadyAim, "ADS spread", "-" + Percent(b.AdsSpreadPerMille)));
            if (b.RecoilPerMille < 1000) chips.Add((IconLibrary.RewardRecoil, "Recoil", "-" + Percent(b.RecoilPerMille)));
            if (b.EquipPerMille < 1000) chips.Add((IconLibrary.RewardQuickDraw, "Equip", "-" + Percent(b.EquipPerMille)));
            if (b.ExtraMagazines > 0) chips.Add((IconLibrary.RewardExtraMag, "Spare mags", "+" + b.ExtraMagazines));
            if (chips.Count == 0)
            {
                Rect empty = new Rect(r.x, r.y, r.width, Mathf.Min(44f, r.height));
                TacticalUi.Plate(empty, UiSdf.Style.Solid(new Color(0f, 0f, 0f, .25f), Hud.UiShapeParams.PlateRadius));
                ui.Label(new Rect(empty.x + 12, empty.y, empty.width - 24, empty.height), ShellUiCopy.CareerNoBuffs, 13, TacticalUi.Muted, TextAnchor.MiddleLeft);
                return;
            }
            const int columns = 2;
            int rows = (chips.Count + columns - 1) / columns;
            float chipH = Mathf.Clamp((r.height - (rows - 1) * 6f) / rows, 28f, 46f);
            float chipW = (r.width - 6f) / columns;
            for (int i = 0; i < chips.Count; i++)
            {
                Rect c = new Rect(r.x + (i % columns) * (chipW + 6f), r.y + (i / columns) * (chipH + 6f), chipW, chipH);
                if (c.yMax > r.yMax + 1f) break;
                CareerArt.BuffChip(ui, c, chips[i].Icon, chips[i].Name, chips[i].Value);
            }
        }

        private static string Percent(ushort perMille) => ((1000 - perMille + 5) / 10) + "%";

        private void DrawServiceRecord(Rect r, CareerProfile p)
        {
            long matches = p.Matches;
            var cells = new[]
            {
                ("MATCHES", ShellUiCopy.Thousands(matches)),
                ("W / L / D", p.Wins + " / " + p.Losses + " / " + p.Draws),
                ("WIN RATE", ShellUiCopy.Percent(p.Wins, matches)),
                ("K / D", ShellUiCopy.Ratio(p.Kills, p.Deaths)),
                ("KILLS", ShellUiCopy.Thousands(p.Kills)),
                ("DEATHS", ShellUiCopy.Thousands(p.Deaths)),
                ("ASSISTS", ShellUiCopy.Thousands(p.Assists)),
                ("HEADSHOTS", ShellUiCopy.Thousands(p.Headshots) + "  (" + ShellUiCopy.Percent(p.Headshots, p.Kills) + ")"),
                ("AVG SCORE", ShellUiCopy.Thousands(matches > 0 ? p.ScoreTotal / matches : 0)),
                ("BEST SCORE", ShellUiCopy.Thousands(p.BestScore)),
                ("BEST STREAK", ShellUiCopy.Thousands(p.BestStreak)),
                ("TIME PLAYED", ShellUiCopy.PlayTime(p.ActiveTicks)),
            };
            const int columns = 4;
            int rows = (cells.Length + columns - 1) / columns;
            const float g = 6f;
            float cellH = (r.height - (rows - 1) * g) / rows;
            float cellW = (r.width - (columns - 1) * g) / columns;
            bool stacked = cellH >= 40f;
            Color rule = new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, .7f);
            for (int i = 0; i < cells.Length; i++)
            {
                int row = i / columns, col = i % columns;
                Rect c = new Rect(r.x + col * (cellW + g), r.y + row * (cellH + g), cellW, cellH);
                TacticalUi.Plate(c, UiSdf.Style.Solid(new Color(.12f, .15f, .16f, .92f), Hud.UiShapeParams.PlateRadius)
                    .WithGradient(new Color(.06f, .075f, .08f, .92f), UiSdf.Gradient.Vertical));
                TacticalUi.Fill(new Rect(c.x, c.y + 4f, 2f, c.height - 8f), rule);
                if (stacked)
                {
                    ui.Label(new Rect(c.x + 10, c.y + 4, c.width - 14, 14), cells[i].Item1, 10, TacticalUi.Muted);
                    ui.Label(new Rect(c.x + 10, c.y + 17, c.width - 14, c.height - 19), cells[i].Item2, 17, TacticalUi.Text, TextAnchor.MiddleLeft);
                }
                else
                {
                    ui.Label(new Rect(c.x + 10, c.y, c.width * .5f, c.height), cells[i].Item1, 10, TacticalUi.Muted, TextAnchor.MiddleLeft);
                    ui.Label(new Rect(c.x + c.width * .4f, c.y, c.width * .6f - 8, c.height), cells[i].Item2, 14, TacticalUi.Text, TextAnchor.MiddleRight);
                }
            }
        }

        // ------------------------------------------------------------ debrief

        /// <summary>
        /// Debrief career strip under the outcome banner: emblem, CP earned with its breakdown, a recessed bar filling
        /// from the old total to the new one (gain segment lit, wrapping through each promotion) and, on promotion,
        /// a gold-edged strip with the unlocked reward's icon. Returns the next free y.
        /// </summary>
        private float DrawCareerStrip(Rect r, float t, bool reduced)
        {
            var results = coordinator.RetainedResults;
            var award = career.LastAward;
            if (award == null || results == null || career.LastAwardSessionId != results.AuthoritySessionId) return r.y;
            float alpha = reduced ? 1f : Hud.UiMotion.EnterAlpha(t - .5f);
            float fillT = reduced ? 1f : Mathf.Clamp01((t - .8f) / 1.4f);
            long shownCp = award.Before.CareerPoints + (long)Math.Round(award.Total * (double)Hud.UiMotion.OutCubic(fillT));
            int shownRank = CareerLadder.RankFor(shownCp);
            bool promotedNow = award.Promoted && shownRank > award.RankBefore;
            TacticalUi.Plate(r, UiSdf.Style.Solid(new Color(.07f, .085f, .092f, .94f * alpha), Hud.UiShapeParams.PlateRadius)
                .WithGradient(new Color(.035f, .045f, .05f, .94f * alpha), UiSdf.Gradient.Vertical).WithShadow(.35f * alpha));
            if (promotedNow && UiSdf.Available)
            {
                var edge = UiSdf.Style.Solid(new Color(TacticalUi.Gold.r, TacticalUi.Gold.g, TacticalUi.Gold.b, .6f * alpha), Hud.UiShapeParams.PlateRadius)
                    .WithGlow(TacticalUi.Gold, 8f, (ui.ReducedFlash ? .15f : .3f) * alpha);
                edge.Stroke = 1f;
                UiSdf.Rect(r, edge);
            }
            float glint = promotedNow && !reduced && !ui.ReducedFlash ? Hud.UiMotion.GlintPosition(t - 2.2f, false) : -1f;
            DrawRankIcon(new Rect(r.x + 6, r.y + 3, r.height - 6, r.height - 6), shownRank, alpha, glint);
            float tx = r.x + r.height + 10;
            float tw = r.width * .58f - tx + r.x;
            string outcomeLabel = award.Outcome == CareerMatchOutcome.Win ? "WIN" : "DRAW";
            Color accent = new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, alpha);
            ui.Label(new Rect(tx, r.y + 4, tw, 18),
                ShellUiCopy.CareerAwardLine(award.Total, award.ScorePoints, award.CompletionPoints, award.OutcomePoints, outcomeLabel), 14, accent);
            var next = CareerLadder.Next(shownRank);
            Color muted = new Color(TacticalUi.Muted.r, TacticalUi.Muted.g, TacticalUi.Muted.b, alpha);
            ui.Label(new Rect(tx, r.y + 24, tw, 16), CareerLadder.Get(shownRank).Name.ToUpperInvariant() + "  ·  "
                + (next == null ? ShellUiCopy.CareerTopRank : ShellUiCopy.CareerProgress(shownCp, next.Threshold)), 11, muted);

            Rect track = new Rect(r.x + r.width * .6f, r.y + 25, r.width * .4f - 14, 9);
            var pal = CareerArt.Palette(shownRank);
            float now01 = CareerLadder.Progress01(shownCp);
            // The gain segment starts at the old total while still on the starting rank, else at the new rank's floor.
            float from01 = shownRank == award.RankBefore ? CareerLadder.Progress01(award.Before.CareerPoints) : 0f;
            CareerArt.ProgressBar(track, now01, pal.MetalTop, pal.MetalBottom, 10, alpha, Mathf.Min(from01, now01));
            if (promotedNow)
            {
                var reached = CareerLadder.Get(shownRank);
                var art = CareerRewardCatalog.For(reached);
                Color gold = new Color(TacticalUi.Gold.r, TacticalUi.Gold.g, TacticalUi.Gold.b, alpha);
                float iconW = art.Wide ? 44f : 18f;
                Rect iconR = new Rect(track.xMax - iconW, r.y + 3, iconW, 18);
                if (CareerArt.RewardIcon(iconR, art, Color.Lerp(Color.white, TacticalUi.Gold, .3f), TacticalUi.GoldDeep, alpha))
                    ui.Label(new Rect(track.x, r.y + 3, track.width - iconW - 6, 18), ShellUiCopy.CareerPromotion(reached.Name, art.FullTitle), 12, gold, TextAnchor.UpperRight);
                else
                    ui.Label(new Rect(track.x, r.y + 3, track.width, 18), ShellUiCopy.CareerPromotion(reached.Name, reached.RewardLabel), 12, gold, TextAnchor.UpperRight);
            }
            return r.yMax + 12;
        }
    }
}
