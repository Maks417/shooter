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
    /// Presentation only reads authority results; the buffs reach the simulation through the session's Armory loadout.
    /// </summary>
    public sealed partial class ProductShellHost
    {
        private static readonly Color RankEnlisted = new Color32(204, 192, 132, 255);
        private static readonly Color RankSilver = new Color32(208, 215, 218, 255);
        private static readonly Color RankBronze = new Color32(209, 146, 86, 255);

        private ICareerStore? careerStore;
        private CareerRecorder career = new CareerRecorder(CareerProfile.Empty);
        private string careerStatus = "";
        private int careerFirstRow = -1;

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
            navigation.OpenCareer();
            ApplyCursorForScreen();
        }

        private void CloseCareer()
        {
            navigation.CloseCareer();
            ApplyCursorForScreen();
        }

        // ------------------------------------------------------------ insignia

        private static Color RankInk(int rank)
        {
            switch (CareerLadder.Get(rank).Band)
            {
                case CareerRankBand.General: return TacticalUi.Gold;
                case CareerRankBand.Bronze: return RankBronze;
                case CareerRankBand.Warrant:
                case CareerRankBand.Officer: return RankSilver;
                default: return RankEnlisted;
            }
        }

        /// <summary>Draws a rank insignia; false when the atlas glyph is unavailable (callers keep the text).</summary>
        private static bool DrawRankIcon(Rect r, int rank, float alpha)
        {
            if (rank <= 0) return false;
            Color ink = RankInk(rank);
            ink.a *= alpha;
            return TacticalUi.Icon(r, IconLibrary.CareerRank(rank), ink, shadow: alpha > .5f);
        }

        /// <summary>Rank insignia, name and a thin CP bar; a click opens the Career page.</summary>
        private void DrawRankBadge(Rect r, string id, bool alignRight = false)
        {
            var profile = career.Profile;
            int rank = profile.Rank;
            var def = CareerLadder.Get(rank);
            var next = CareerLadder.Next(rank);
            if (ui.Button(r, id, "", quiet: true)) OpenCareer();
            float icon = r.height - 12f;
            float ix = alignRight ? r.xMax - icon - 8f : r.x + 8f;
            DrawRankIcon(new Rect(ix, r.y + 6, icon, icon), rank, 1f);
            float tx = alignRight ? r.x + 8f : ix + icon + 10f;
            float tw = r.width - icon - 26f;
            TextAnchor anchor = alignRight ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            ui.Label(new Rect(tx, r.y + 4, tw, 18), def.Name.ToUpperInvariant(), 15, RankInk(rank), anchor);
            string progress = next == null ? ShellUiCopy.CareerTopRank : ShellUiCopy.CareerProgress(profile.CareerPoints, next.Threshold);
            ui.Label(new Rect(tx, r.y + 22, tw, 14), progress, 11, TacticalUi.Muted, anchor);
            Rect track = new Rect(tx, r.yMax - 8, tw, 3);
            TacticalUi.Fill(track, new Color(1f, 1f, 1f, .1f));
            float fill = track.width * CareerLadder.Progress01(profile.CareerPoints);
            TacticalUi.Fill(alignRight ? new Rect(track.xMax - fill, track.y, fill, 3) : new Rect(track.x, track.y, fill, 3), RankInk(rank));
        }

        // ------------------------------------------------------------ Armory locks

        private static string LockedText(DefinitionId id) =>
            ShellUiCopy.UnlocksAt(CareerLadder.Get(CareerLadder.UnlockRankFor(id.ToString())).Name);

        private void DrawLockTag(Rect r)
        {
            TacticalUi.Fill(r, new Color(1f, 1f, 1f, .06f));
            ui.Label(r, ShellUiCopy.ArmoryLocked, 11, TacticalUi.Muted, TextAnchor.MiddleCenter);
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
            float icon = 18f;
            DrawRankIcon(new Rect(tx, row.y + 28, icon, icon), unlock, .8f);
            ui.Label(new Rect(tx + icon + 6, row.y + 29, tw - icon - 6, 18), LockedText(weapon), 12, TacticalUi.Muted);
            DrawLockTag(new Rect(row.xMax - 84, row.y + 7, 72, 16));
        }

        // ------------------------------------------------------------ Career page

        private void DrawCareer()
        {
            ui.Backdrop();
            ui.Header(ShellUiCopy.CareerEyebrow, ShellUiCopy.CareerTitle, "");
            Rect a = ui.Safe;
            var profile = career.Profile;
            int rank = profile.Rank;
            var def = CareerLadder.Get(rank);
            var next = CareerLadder.Next(rank);
            float top = a.y + 74;
            float bottom = a.yMax - 74;
            float leftW = Mathf.Min(420, a.width * .4f);
            float gap = 18;
            float rightX = a.x + leftW + gap;
            float rightW = a.xMax - rightX;

            // Current rank card.
            ui.Section(new Rect(a.x, top, leftW, 18), ShellUiCopy.CareerRankSection);
            Rect card = new Rect(a.x, top + 24, leftW, 168);
            ui.Panel(card, accentTop: true);
            DrawRankIcon(new Rect(card.x + 16, card.y + 18, 92, 92), rank, 1f);
            float tx = card.x + 124, tw = card.width - 136;
            ui.Label(new Rect(tx, card.y + 16, tw, 14), def.Code + "  ·  RANK " + rank + " OF " + CareerLadder.RankCount, 11, TacticalUi.Muted);
            ui.Label(new Rect(tx, card.y + 32, tw, 34), def.Name, 26, RankInk(rank));
            ui.Label(new Rect(tx, card.y + 70, tw, 16),
                next == null ? ShellUiCopy.CareerTopRank : ShellUiCopy.CareerProgress(profile.CareerPoints, next.Threshold), 13, TacticalUi.Text);
            Rect track = new Rect(tx, card.y + 92, tw, 6);
            TacticalUi.Fill(track, new Color(1f, 1f, 1f, .1f));
            TacticalUi.Fill(new Rect(track.x, track.y, track.width * CareerLadder.Progress01(profile.CareerPoints), track.height), RankInk(rank));
            if (next != null)
            {
                ui.Label(new Rect(card.x + 16, card.y + 122, card.width - 32, 14), ShellUiCopy.CareerNextReward + "  ·  " + next.Name.ToUpperInvariant(), 11, TacticalUi.Accent);
                ui.Label(new Rect(card.x + 16, card.y + 138, card.width - 32, 20), next.RewardLabel, 15, TacticalUi.Text);
            }
            else
                ui.Label(new Rect(card.x + 16, card.y + 128, card.width - 32, 20), def.RewardLabel, 15, TacticalUi.Gold);

            // Active buffs.
            float by = card.yMax + 14;
            ui.Section(new Rect(a.x, by, leftW, 18), ShellUiCopy.CareerActiveBuffs);
            ui.Label(new Rect(a.x, by + 22, leftW, 36), BuffSummary(CareerLadder.BuffsFor(rank)), 13, TacticalUi.Muted);

            // Service record.
            float sy = by + 66;
            ui.Section(new Rect(a.x, sy, leftW, 18), ShellUiCopy.CareerStatsSection);
            DrawServiceRecord(new Rect(a.x, sy + 24, leftW, bottom - sy - 24), profile);

            // Ladder.
            ui.Section(new Rect(rightX, top, rightW, 18), ShellUiCopy.CareerLadderSection);
            DrawLadder(new Rect(rightX, top + 24, rightW, bottom - top - 24), profile);

            ui.Panel(new Rect(a.x, a.yMax - 66, a.width, 44), accentTop: true);
            ui.Label(new Rect(a.x + 170, a.yMax - 56, a.width - 190, 30), ShellUiCopy.CareerHint, 13, TacticalUi.Muted, TextAnchor.MiddleLeft);
            if (ui.Button(new Rect(a.x, a.yMax - 66, 150, 44), "career.back", ShellUiCopy.SetupBack)) CloseCareer();
            DrawFooter(careerStatus);
        }

        private static string BuffSummary(CareerBuffs b)
        {
            if (b.IsNone) return ShellUiCopy.CareerNoBuffs;
            var parts = new System.Collections.Generic.List<string>(5);
            if (b.ReloadPerMille < 1000) parts.Add("Reload -" + Percent(b.ReloadPerMille));
            if (b.AdsSpreadPerMille < 1000) parts.Add("ADS spread -" + Percent(b.AdsSpreadPerMille));
            if (b.RecoilPerMille < 1000) parts.Add("Recoil -" + Percent(b.RecoilPerMille));
            if (b.EquipPerMille < 1000) parts.Add("Equip -" + Percent(b.EquipPerMille));
            if (b.ExtraMagazines > 0) parts.Add("+" + b.ExtraMagazines + " spare mag" + (b.ExtraMagazines > 1 ? "s" : ""));
            return string.Join("  ·  ", parts);
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
            const int columns = 2;
            int rows = (cells.Length + columns - 1) / columns;
            float cellH = Mathf.Clamp(r.height / rows, 30f, 44f);
            float cellW = (r.width - 8f) / columns;
            for (int i = 0; i < cells.Length; i++)
            {
                int row = i / columns, col = i % columns;
                Rect c = new Rect(r.x + col * (cellW + 8f), r.y + row * cellH, cellW, cellH - 4f);
                if (c.yMax > r.yMax + 1f) break;
                TacticalUi.Fill(c, new Color(1f, 1f, 1f, row % 2 == 0 ? .04f : .025f));
                ui.Label(new Rect(c.x + 10, c.y, c.width * .5f, c.height), cells[i].Item1, 11, TacticalUi.Muted, TextAnchor.MiddleLeft);
                ui.Label(new Rect(c.x + c.width * .4f, c.y, c.width * .6f - 10, c.height), cells[i].Item2, 15, TacticalUi.Text, TextAnchor.MiddleRight);
            }
        }

        private void DrawLadder(Rect r, CareerProfile profile)
        {
            int rank = profile.Rank;
            const float rowH = 40f;
            int visible = Mathf.Max(1, Mathf.FloorToInt(r.height / rowH));
            int maxFirst = Mathf.Max(0, CareerLadder.RankCount - visible);
            if (careerFirstRow < 0) careerFirstRow = Mathf.Clamp(rank - 1 - visible / 2, 0, maxFirst);
            var ev = Event.current;
            if (ev != null && ev.type == EventType.ScrollWheel && r.Contains(ev.mousePosition))
            {
                careerFirstRow += ev.delta.y > 0 ? 1 : -1;
                ev.Use();
            }
            careerFirstRow = Mathf.Clamp(careerFirstRow, 0, maxFirst);
            for (int slot = 0; slot < visible && careerFirstRow + slot < CareerLadder.RankCount; slot++)
            {
                var step = CareerLadder.Ranks[careerFirstRow + slot];
                bool reached = step.Index <= rank;
                bool current = step.Index == rank;
                Rect row = new Rect(r.x, r.y + slot * rowH, r.width - 8, rowH - 4);
                Color fill = current ? new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, .16f)
                    : new Color(1f, 1f, 1f, slot % 2 == 0 ? .045f : .025f);
                TacticalUi.Fill(row, fill);
                if (current) TacticalUi.Fill(new Rect(row.x, row.y, 3, row.height), TacticalUi.Accent);
                float alpha = reached ? 1f : .55f;
                DrawRankIcon(new Rect(row.x + 10, row.y + 4, row.height - 8, row.height - 8), step.Index, alpha);
                Color name = reached ? RankInk(step.Index) : TacticalUi.Muted;
                ui.Label(new Rect(row.x + 52, row.y + 3, 220, 18), step.Name, 15, name);
                ui.Label(new Rect(row.x + 52, row.y + 20, 220, 14), step.Code + "  ·  " + ShellUiCopy.Thousands(step.Threshold) + " CP", 11, TacticalUi.Muted);
                float rx = row.x + 280;
                ui.Label(new Rect(rx, row.y, row.xMax - rx - 90, row.height), step.RewardLabel, 13, reached ? TacticalUi.Text : TacticalUi.Muted, TextAnchor.MiddleLeft);
                ui.Label(new Rect(row.xMax - 86, row.y, 76, row.height), reached ? (current ? "CURRENT" : "EARNED") : ShellUiCopy.ArmoryLocked, 11,
                    current ? TacticalUi.Accent : reached ? TacticalUi.Ally : TacticalUi.Muted, TextAnchor.MiddleRight);
            }
            if (CareerLadder.RankCount > visible)
            {
                Rect rail = new Rect(r.xMax - 3, r.y, 3, visible * rowH - 4);
                TacticalUi.Fill(rail, new Color(1f, 1f, 1f, .08f));
                float thumb = rail.height * visible / CareerLadder.RankCount;
                float y = rail.y + rail.height * careerFirstRow / CareerLadder.RankCount;
                TacticalUi.Fill(new Rect(rail.x, y, rail.width, Mathf.Max(6f, thumb)), TacticalUi.Accent);
            }
        }

        // ------------------------------------------------------------ debrief

        /// <summary>
        /// Debrief career strip under the outcome banner: insignia, CP earned with its breakdown, the bar filling from
        /// the old total to the new one (wrapping through each promotion) and a promotion line. Returns the next free y.
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
            TacticalUi.Plate(r, UiSdf.Style.Solid(new Color(.05f, .065f, .072f, .92f * alpha), Hud.UiShapeParams.PlateRadius));
            DrawRankIcon(new Rect(r.x + 10, r.y + 5, r.height - 10, r.height - 10), shownRank, alpha);
            float tx = r.x + r.height + 12;
            float tw = r.width * .58f - tx + r.x;
            string outcomeLabel = award.Outcome == CareerMatchOutcome.Win ? "WIN" : "DRAW";
            Color accent = new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, alpha);
            ui.Label(new Rect(tx, r.y + 4, tw, 18),
                ShellUiCopy.CareerAwardLine(award.Total, award.ScorePoints, award.CompletionPoints, award.OutcomePoints, outcomeLabel), 14, accent);
            var next = CareerLadder.Next(shownRank);
            Color muted = new Color(TacticalUi.Muted.r, TacticalUi.Muted.g, TacticalUi.Muted.b, alpha);
            ui.Label(new Rect(tx, r.y + 24, tw, 16), CareerLadder.Get(shownRank).Name.ToUpperInvariant() + "  ·  "
                + (next == null ? ShellUiCopy.CareerTopRank : ShellUiCopy.CareerProgress(shownCp, next.Threshold)), 11, muted);
            Rect track = new Rect(r.x + r.width * .6f, r.y + 20, r.width * .4f - 14, 5);
            TacticalUi.Fill(track, new Color(1f, 1f, 1f, .1f * alpha));
            Color ink = RankInk(shownRank);
            ink.a = alpha;
            TacticalUi.Fill(new Rect(track.x, track.y, track.width * CareerLadder.Progress01(shownCp), track.height), ink);
            if (award.Promoted && shownRank > award.RankBefore)
            {
                var reached = CareerLadder.Get(shownRank);
                Color gold = new Color(TacticalUi.Gold.r, TacticalUi.Gold.g, TacticalUi.Gold.b, alpha);
                ui.Label(new Rect(track.x, r.y + 2, track.width, 16), ShellUiCopy.CareerPromotion(reached.Name, reached.RewardLabel), 12, gold, TextAnchor.UpperRight);
            }
            return r.yMax + 12;
        }
    }
}
