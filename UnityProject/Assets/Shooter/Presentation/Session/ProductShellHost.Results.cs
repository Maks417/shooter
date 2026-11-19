#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Presentation.Input;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Scoreboard;
using Shooter.Generation.Icons;
using Shooter.Contracts;
using Shooter.Presentation.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Presentation.Session
{
    /// <summary>Playing scoreboard, results and debrief screens.</summary>
    public sealed partial class ProductShellHost : MonoBehaviour
    {
        private void DrawPlayingHud(float s)
        {
            var snap = session?.TryAcquireSnapshot();
            if (snap == null) return;
            // BFV-like top score/clock strip; the compass rings the minimap (ADR-104, ProductSessionView).
            float w = 248, h = 26, x = (ui.Width - w) * .5f, y = ui.Safe.y + 8;
            TacticalUi.CombatPlate(new Rect(x, y, w, h), strong: true);
            TacticalUi.Fill(new Rect(x, y, 2, h), TacticalUi.Ally);
            TacticalUi.Fill(new Rect(x + w - 2, y, 2, h), TacticalUi.Enemy);
            TacticalUi.Fill(new Rect(x + 2, y + h - 1, w - 4, 1), new Color(1f, 1f, 1f, .06f));
            ui.Label(new Rect(x + 10, y + 1, 54, 24), snap.Team1Kills.ToString("000"), 20, TacticalUi.Ally, TextAnchor.MiddleLeft);
            ui.Label(new Rect(x + 64, y + 1, w - 128, 24), TacticalUi.Clock(snap.RemainingActiveTicks), 16, TacticalUi.Text, TextAnchor.MiddleCenter);
            ui.Label(new Rect(x + w - 64, y + 1, 54, 24), snap.Team2Kills.ToString("000"), 20, TacticalUi.Enemy, TextAnchor.MiddleRight);
            float mid = x + w * .5f;
            float allyBar = (w * .5f - 6) * Mathf.Clamp01(snap.Team1Kills / (float)ActiveMode.ScoreLimit);
            float enemyBar = (w * .5f - 6) * Mathf.Clamp01(snap.Team2Kills / (float)ActiveMode.ScoreLimit);
            TacticalUi.Fill(new Rect(mid - allyBar, y + h - 2, allyBar, 1), TacticalUi.Ally);
            TacticalUi.Fill(new Rect(mid, y + h - 2, enemyBar, 1), TacticalUi.Enemy);

            // Pre-match freeze banner, then FIGHT! for the first second of the live clock.
            int remaining = session != null ? session.PreMatchTicksRemaining : 0;
            string? banner = null;
            if (remaining > 0)
                banner = "MATCH STARTS IN " + ((remaining + 59) / 60).ToString();
            else if (session != null && session.PreMatchCountdownTicks > 0 && snap.ActiveTicksElapsed < 60)
                banner = "FIGHT!";
            if (banner != null)
            {
                float bw = 640, bh = 84;
                ui.Label(new Rect((ui.Width - bw) * .5f, ui.Height * .30f, bw, bh), banner, 56, remaining > 0 ? TacticalUi.Text : TacticalUi.Accent, TextAnchor.MiddleCenter);
            }
        }

        private void DrawResults(float s)
        {
            ui.Backdrop();
            var results = coordinator.RetainedResults;
            if (results == null)
            {
                ui.Header(ShellUiCopy.EyebrowDebrief, ShellUiCopy.ResultsEmpty);
                ui.Label(new Rect(ui.Safe.x, ui.Safe.y + 90, ui.Safe.width, 48), "No retained results.");
                if (ui.Button(new Rect(ui.Safe.x, ui.Safe.yMax - 58, 180, 40), "results.menu", "MAIN MENU")) LeaveMatch(abort: false);
                return;
            }
            scoreboard.BindResults(results);
            DrawDebrief(Time.unscaledTime - enteredAt);
        }

        /// <summary>Review-only debrief preview with sample rows (<c>-shooterUiGallery results</c>); loops every 7 s.</summary>
        private void DrawDebriefPreview()
        {
            ui.Backdrop();
            var rows = new[]
            {
                new ScoreboardRow(new ParticipantId(1), "Player", 1, true, 1450, 14, 5, true, true, 7),
                new ScoreboardRow(new ParticipantId(2), "Nora", 1, false, 1200, 12, 9, false, true, 9),
                new ScoreboardRow(new ParticipantId(3), "Elena", 1, false, 900, 9, 3, false, true, 4),
                new ScoreboardRow(new ParticipantId(4), "Cole", 2, false, 1300, 15, 11, false, true, 11),
                new ScoreboardRow(new ParticipantId(5), "Omar", 2, false, 700, 7, 12, false, true, 6),
                new ScoreboardRow(new ParticipantId(6), "Aya", 2, false, 400, 4, 13, false, true, 5),
            };
            scoreboard.BindPreview(rows, 150, 132, MatchOutcome.Team1, FinishReason.ScoreLimit);
            DrawDebrief((Time.unscaledTime - enteredAt) % 7f);
        }

        private void DrawDebrief(float t)
        {
            bool reduced = ui.ReducedMotion;
            Rect a = ui.Safe;
            ui.Header(ShellUiCopy.EyebrowDebrief, "Match debrief", ActiveMode.DisplayName.ToUpperInvariant());

            // Outcome banner: sweeps in from the left, team emblems flank counting scores.
            string outcome = scoreboard.LocalOutcomeLabel(1).ToUpperInvariant();
            Color tone = scoreboard.Outcome == MatchOutcome.Team1 ? TacticalUi.Ally
                : scoreboard.Outcome == MatchOutcome.Team2 ? TacticalUi.Enemy : TacticalUi.Muted;
            Rect band = new Rect(a.x, a.y + 74, a.width, 92);
            float reveal = ResultsPresentation.BannerReveal(t, reduced);
            TacticalUi.Plate(new Rect(band.x, band.y, Mathf.Max(8f, band.width * reveal), band.height),
                UiSdf.Style.Solid(new Color(tone.r * .35f, tone.g * .35f, tone.b * .35f, .92f), Hud.UiShapeParams.PlateRadius)
                    .WithGradient(new Color(.03f, .04f, .045f, .9f), UiSdf.Gradient.Horizontal).WithShadow());
            TacticalUi.Plate(new Rect(band.x, band.y, Mathf.Max(8f, band.width * reveal), 2f), UiSdf.Style.Solid(tone, 1f));
            float textAlpha = reduced ? 1f : Hud.UiMotion.EnterAlpha(t - .2f);
            Color ink = new Color(TacticalUi.Text.r, TacticalUi.Text.g, TacticalUi.Text.b, textAlpha);
            ui.Label(new Rect(band.x, band.y + 8, band.width, 56), outcome, 52, ink, TextAnchor.MiddleCenter);
            string reason = scoreboard.Reason == FinishReason.ScoreLimit ? "SCORE LIMIT REACHED" : "TIME LIMIT REACHED";
            ui.Label(new Rect(band.x, band.y + 62, band.width, 20), reason, 12, new Color(TacticalUi.Muted.r, TacticalUi.Muted.g, TacticalUi.Muted.b, textAlpha), TextAnchor.MiddleCenter);
            for (byte team = 1; team <= 2; team++)
            {
                Color c = team == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                c.a = textAlpha;
                int shown = ResultsPresentation.TeamScore(team == 1 ? scoreboard.Team1Kills : scoreboard.Team2Kills, t, reduced);
                float ex = team == 1 ? band.x + 24 : band.xMax - 24 - 56;
                TacticalUi.Icon(new Rect(ex, band.y + 18, 56, 56), team == 1 ? IconLibrary.EmblemAlly : IconLibrary.EmblemEnemy, c, shadow: true);
                Rect score = team == 1 ? new Rect(ex + 68, band.y + 14, 160, 64) : new Rect(ex - 172, band.y + 14, 160, 64);
                ui.Label(score, shown.ToString(), 48, c, team == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            }

            // Award cards: medal stamps in, card rises, staggered left to right.
            float y = band.yMax + 16;
            // ADR-108 career strip: CP earned, the progress bar filling and any promotion.
            y = DrawCareerStrip(new Rect(a.x, y, a.width, 46), t, reduced);
            var awards = ResultsPresentation.Awards(scoreboard.Rows);
            // Large teams / high UI scale: cards shrink, then hide, so the tables keep room above the buttons.
            var awardMode = DebriefAwardLayout.Choose(a.yMax - 64 - y, ScoreboardStats.LargestTeam(scoreboard.Rows));
            if (awards.Count > 0 && awardMode == DebriefAwardMode.Compact)
            {
                const float gap = 14f, ch = DebriefAwardLayout.CompactCardHeight;
                float cw = (a.width - gap * (ResultsPresentation.MaxAwards - 1)) / ResultsPresentation.MaxAwards;
                for (int i = 0; i < awards.Count; i++)
                {
                    var award = awards[i];
                    float alpha = reduced ? 1f : Hud.UiMotion.EnterAlpha(ResultsPresentation.AwardElapsed(t, i));
                    if (alpha <= 0.01f) continue;
                    Rect card = new Rect(a.x + i * (cw + gap), y, cw, ch);
                    var plate = UiSdf.Style.Solid(new Color(.05f, .065f, .072f, .94f * alpha), Hud.UiShapeParams.CardRadius)
                        .WithGradient(new Color(.03f, .04f, .045f, .94f * alpha), UiSdf.Gradient.Vertical).WithShadow(.35f * alpha);
                    if (award.IsLocal) plate = plate.WithGlow(TacticalUi.Accent, 8f, (ui.ReducedFlash ? .14f : .26f) * alpha);
                    TacticalUi.Plate(card, plate);
                    Color team = award.Team == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                    ui.Label(new Rect(card.x + 12, card.y + 4, cw - 24, 14), award.Title + "  ·  " + award.Stat, 11, new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, alpha));
                    ui.Label(new Rect(card.x + 12, card.y + 20, cw - 24, 22), (award.IsLocal ? "YOU / " : "") + award.DisplayName, 16, new Color(team.r, team.g, team.b, alpha));
                }
                y += DebriefAwardLayout.BlockHeight(DebriefAwardMode.Compact);
            }
            else if (awards.Count > 0 && awardMode == DebriefAwardMode.Full)
            {
                const float gap = 14f, ch = DebriefAwardLayout.FullCardHeight;
                float cw = (a.width - gap * (ResultsPresentation.MaxAwards - 1)) / ResultsPresentation.MaxAwards;
                for (int i = 0; i < awards.Count; i++)
                {
                    var award = awards[i];
                    float e = ResultsPresentation.AwardElapsed(t, i);
                    float alpha = reduced ? 1f : Hud.UiMotion.EnterAlpha(e);
                    if (alpha <= 0.01f) continue;
                    float rise = reduced ? 0f : (1f - Hud.UiMotion.OutCubic(Hud.UiMotion.Progress(e, .3f))) * 14f;
                    Rect card = new Rect(a.x + i * (cw + gap), y + rise, cw, ch);
                    var plate = UiSdf.Style.Solid(new Color(.05f, .065f, .072f, .94f * alpha), Hud.UiShapeParams.CardRadius)
                        .WithGradient(new Color(.03f, .04f, .045f, .94f * alpha), UiSdf.Gradient.Vertical).WithShadow(.35f * alpha);
                    if (award.IsLocal) plate = plate.WithGlow(TacticalUi.Accent, 8f, (ui.ReducedFlash ? .14f : .26f) * alpha);
                    TacticalUi.Plate(card, plate);
                    // Medal centre leaves room for its laurel strokes (about 0.4 × size each side) inside the card.
                    float medal = 50f * Hud.UiMotion.StampScale(e, reduced);
                    TacticalUi.Medal(new Rect(card.x + 52 - medal * .5f, card.center.y - medal * .5f + 6, medal, medal), award.Tier, alpha,
                        Hud.UiMotion.GlintPosition(e, ui.ReducedFlash));
                    float tx = card.x + 110;
                    Color team = award.Team == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                    ui.Label(new Rect(tx, card.y + 14, cw - 118, 16), award.Title, 12, new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, alpha));
                    ui.Label(new Rect(tx, card.y + 34, cw - 118, 28), (award.IsLocal ? "YOU / " : "") + award.DisplayName, 22, new Color(team.r, team.g, team.b, alpha));
                    ui.Label(new Rect(tx, card.y + 66, cw - 118, 20), award.Stat, 14, new Color(TacticalUi.Muted.r, TacticalUi.Muted.g, TacticalUi.Muted.b, alpha));
                }
                y += DebriefAwardLayout.BlockHeight(DebriefAwardMode.Full);
            }

            // Team tables fade in last.
            float tableHeight = a.yMax - 64 - y;
            DrawTeamTables(y, tableHeight);
            float tablesVeil = 1f - ResultsPresentation.TablesAlpha(t, reduced);
            if (tablesVeil > .005f)
                TacticalUi.Fill(new Rect(a.x - 4, y - 4, a.width + 8, tableHeight + 8),
                    new Color(TacticalUi.Background.r, TacticalUi.Background.g, TacticalUi.Background.b, tablesVeil));

            float by = a.yMax - 52;
            if (ui.Button(new Rect(a.x, by, 180, 40), "results.replay", ShellUiCopy.ResultsPlayAgain, primary: true)) RestartMatch();
            if (ui.Button(new Rect(a.x + 192, by, 180, 40), "results.menu", ShellUiCopy.ResultsMain)) LeaveMatch(abort: false);
            if (tableScrollHint != null)
                ui.Label(new Rect(a.x + 392, by, a.width - 392, 40), tableScrollHint, 12, TacticalUi.Muted, TextAnchor.MiddleRight);
        }

        /// <summary>
        /// In-match Tab board: a translucent overlay (the fight stays visible behind it) with live team totals, alive
        /// counts, the clock, the local player's standing and a compact per-operator stat table. Distinct from the
        /// opaque post-match debrief table.
        /// </summary>
        private void DrawScoreboard(float s)
        {
            Rect a = ui.Safe;
            var snap = session?.TryAcquireSnapshot();
            var rows = scoreboard.Rows;
            TacticalUi.Fill(new Rect(-40, -60, ui.Width + 80, ui.Height + 120), new Color(.01f, .016f, .02f, .5f));

            // Header: team totals vs the score limit, alive counts and the match clock.
            float y0 = a.y + 2;
            float cx = a.x + a.width * .5f;
            string clock = snap != null ? TacticalUi.Clock(snap.RemainingActiveTicks) : "--:--";
            ui.Label(new Rect(cx - 90, y0, 180, 30), clock, 26, TacticalUi.Text, TextAnchor.MiddleCenter);
            ui.Label(new Rect(cx - 140, y0 + 30, 280, 14), ActiveMode.DisplayName.ToUpperInvariant() + "  ·  FIRST TO " + ActiveMode.ScoreLimit, 11, TacticalUi.Muted, TextAnchor.MiddleCenter);
            float halfBar = a.width * .5f - 120;
            for (byte team = 1; team <= 2; team++)
            {
                bool left = team == 1;
                Color color = left ? TacticalUi.Ally : TacticalUi.Enemy;
                uint kills = left ? scoreboard.Team1Kills : scoreboard.Team2Kills;
                int size = ScoreboardStats.TeamRowCount(rows, team), alive = ScoreboardStats.AliveCount(rows, team);
                float ex = left ? a.x : a.xMax - 30;
                TacticalUi.Icon(new Rect(ex, y0 + 4, 30, 30), left ? IconLibrary.EmblemAlly : IconLibrary.EmblemEnemy, color, shadow: true);
                Rect big = left ? new Rect(a.x + 38, y0, 80, 36) : new Rect(a.xMax - 118, y0, 80, 36);
                ui.Label(big, kills.ToString(), 30, color, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                Rect info = left ? new Rect(a.x + 118, y0 + 4, 160, 14) : new Rect(a.xMax - 278, y0 + 4, 160, 14);
                ui.Label(info, left ? "TEAM 1 / ALLIES" : "TEAM 2 / ENEMIES", 12, color, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                Rect aliveRect = left ? new Rect(a.x + 118, y0 + 20, 160, 14) : new Rect(a.xMax - 278, y0 + 20, 160, 14);
                ui.Label(aliveRect, alive + " / " + size + " ALIVE", 11, TacticalUi.Muted, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                float fill = halfBar * Mathf.Clamp01(kills / (float)ActiveMode.ScoreLimit);
                Rect track = left ? new Rect(a.x, y0 + 44, halfBar, 3) : new Rect(a.xMax - halfBar, y0 + 44, halfBar, 3);
                TacticalUi.Fill(track, new Color(1f, 1f, 1f, .08f));
                TacticalUi.Fill(left ? new Rect(track.x, track.y, fill, 3) : new Rect(track.xMax - fill, track.y, fill, 3), color);
            }

            // Local standing at a glance.
            float y1 = y0 + 54;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].IsLocal) continue;
                var me = rows[i];
                TacticalUi.Fill(new Rect(a.x, y1, a.width, 22), new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, .1f));
                TacticalUi.Fill(new Rect(a.x, y1, 3, 22), TacticalUi.Accent);
                string line = "YOU   #" + ScoreboardStats.LocalOverallRank(rows) + " OF " + rows.Count
                    + "     SCORE " + me.Score + "     KILLS " + me.Kills + "     DEATHS " + me.Deaths
                    + "     K/D " + ScoreboardStats.KillDeathRatio(me.Kills, me.Deaths) + "     " + (me.Alive ? "IN ACTION" : "RESPAWNING");
                ui.Label(new Rect(a.x + 12, y1, a.width - 24, 22), line, 13, TacticalUi.Accent, TextAnchor.MiddleLeft);
                break;
            }

            // Team columns.
            float y2 = y1 + 30;
            float w = (a.width - 24) * .5f;
            float rowsTop = y2 + 22;
            var metrics = TeamTableMetrics.Fit(a.yMax - 20 - rowsTop, ScoreboardStats.LargestTeam(rows), 26f, 18f, 5);
            var ev = Event.current;
            if (ev != null && ev.type == EventType.ScrollWheel)
            {
                liveFirstRow = metrics.Scroll(liveFirstRow, ev.delta.y);
                ev.Use();
            }
            liveFirstRow = metrics.ClampFirstRow(liveFirstRow);
            int localIndex = ScoreboardStats.LocalTeamIndex(rows, out byte localTeam);
            bool pinLocal = localIndex >= 0 && !metrics.IsVisible(liveFirstRow, localIndex) && metrics.VisibleRows > 1;
            int font = metrics.RowHeight >= 24f ? 15 : 13;
            for (byte team = 1; team <= 2; team++)
            {
                float x = a.x + (team - 1) * (w + 24);
                Color color = team == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                LiveColumns(x, w, out float rankX, out float nameX, out float scoreX, out float killsX, out float deathsX, out float kdX);
                ui.Label(new Rect(rankX, y2, 24, 16), "#", 11, TacticalUi.Muted, TextAnchor.MiddleRight);
                ui.Label(new Rect(nameX, y2, scoreX - nameX, 16), "OPERATOR", 11, TacticalUi.Muted);
                ui.Label(new Rect(scoreX, y2, 56, 16), "SCORE", 11, TacticalUi.Muted, TextAnchor.MiddleRight);
                ui.Label(new Rect(killsX, y2, 36, 16), "K", 11, TacticalUi.Muted, TextAnchor.MiddleRight);
                ui.Label(new Rect(deathsX, y2, 36, 16), "D", 11, TacticalUi.Muted, TextAnchor.MiddleRight);
                ui.Label(new Rect(kdX, y2, 44, 16), "K/D", 11, TacticalUi.Muted, TextAnchor.MiddleRight);
                TacticalUi.Fill(new Rect(x, y2 + 18, w, 2), new Color(color.r, color.g, color.b, .85f));

                bool pinHere = pinLocal && team == localTeam;
                int slots = pinHere ? metrics.VisibleRows - 1 : metrics.VisibleRows;
                int rowIndex = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row.Team != team) continue;
                    int slot = rowIndex - liveFirstRow;
                    if (slot >= 0 && slot < slots)
                        DrawLiveRow(new Rect(x, rowsTop + slot * metrics.RowHeight, w, metrics.RowHeight - 2), row, rowIndex + 1, color, slot % 2 == 0, font);
                    if (pinHere && row.IsLocal)
                    {
                        float py = rowsTop + slots * metrics.RowHeight;
                        TacticalUi.Fill(new Rect(x, py - 1, w, 1), new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, .6f));
                        DrawLiveRow(new Rect(x, py, w, metrics.RowHeight - 2), row, rowIndex + 1, color, false, font);
                    }
                    rowIndex++;
                }
                if (metrics.NeedsScroll) DrawScrollRail(new Rect(x + w + 4, rowsTop, 3, metrics.UsedHeight), metrics, liveFirstRow, color);
            }
            ui.Label(new Rect(a.x, a.yMax - 16, a.width, 16),
                metrics.NeedsScroll ? "HOLD TAB  ·  MOUSE WHEEL TO SCROLL" : "HOLD TAB", 11, TacticalUi.Muted, TextAnchor.MiddleCenter);
        }

        private static void LiveColumns(float x, float w, out float rankX, out float nameX, out float scoreX, out float killsX, out float deathsX, out float kdX)
        {
            rankX = x + 2;
            nameX = x + 46;
            kdX = x + w - 50;
            deathsX = kdX - 42;
            killsX = deathsX - 40;
            scoreX = killsX - 62;
        }

        private void DrawLiveRow(Rect r, ScoreboardRow row, int rank, Color team, bool stripe, int font)
        {
            LiveColumns(r.x, r.width, out float rankX, out float nameX, out float scoreX, out float killsX, out float deathsX, out float kdX);
            if (row.IsLocal)
            {
                TacticalUi.Fill(r, new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, .2f));
                TacticalUi.Fill(new Rect(r.x, r.y, 3, r.height), TacticalUi.Accent);
            }
            else if (stripe) TacticalUi.Fill(r, new Color(1f, 1f, 1f, .05f));
            float alpha = row.Alive ? 1f : .45f;
            Color text = new Color(TacticalUi.Text.r, TacticalUi.Text.g, TacticalUi.Text.b, alpha);
            Color muted = new Color(TacticalUi.Muted.r, TacticalUi.Muted.g, TacticalUi.Muted.b, alpha);
            ui.Label(new Rect(rankX, r.y, 24, r.height), rank.ToString(), font - 2, muted, TextAnchor.MiddleRight);
            // Alive: solid team marker; down: hollow marker plus dimmed row (shape cue, not colour alone).
            float m = 7f, my = r.y + (r.height - m) * .5f, mx = rankX + 30;
            if (row.Alive) TacticalUi.Fill(new Rect(mx, my, m, m), team);
            else
            {
                Color edge = new Color(team.r, team.g, team.b, .6f);
                TacticalUi.Fill(new Rect(mx, my, m, 1), edge);
                TacticalUi.Fill(new Rect(mx, my + m - 1, m, 1), edge);
                TacticalUi.Fill(new Rect(mx, my, 1, m), edge);
                TacticalUi.Fill(new Rect(mx + m - 1, my, 1, m), edge);
            }
            string name = (row.IsLocal ? "YOU / " : row.IsHuman ? "OP / " : "") + row.DisplayName;
            // ADR-108 rank insignia before the name.
            if (row.RankIndex > 0)
            {
                float icon = Mathf.Min(r.height - 4f, 18f);
                if (DrawRankIcon(new Rect(nameX, r.y + (r.height - icon) * .5f, icon, icon), row.RankIndex, alpha)) nameX += icon + 6f;
            }
            ui.Label(new Rect(nameX, r.y, scoreX - nameX - 4, r.height), name, font, row.IsLocal ? TacticalUi.Accent : text, TextAnchor.MiddleLeft);
            ui.Label(new Rect(scoreX, r.y, 56, r.height), row.Score.ToString(), font, new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, alpha), TextAnchor.MiddleRight);
            ui.Label(new Rect(killsX, r.y, 36, r.height), row.Kills.ToString(), font, text, TextAnchor.MiddleRight);
            ui.Label(new Rect(deathsX, r.y, 36, r.height), row.Deaths.ToString(), font, muted, TextAnchor.MiddleRight);
            ui.Label(new Rect(kdX, r.y, 44, r.height), ScoreboardStats.KillDeathRatio(row.Kills, row.Deaths), font - 1, muted, TextAnchor.MiddleRight);
        }

        private void DrawTeamTables(float y, float height)
        {
            Rect a = ui.Safe;
            float w = (a.width - 16) * .5f;
            // ADR-101 team sizes up to 15: rows keep the 5v5 design height and scroll (mouse wheel) when they
            // would otherwise run over the buttons or off the screen.
            var metrics = TeamTableMetrics.Fit(height - 78, ScoreboardStats.LargestTeam(scoreboard.Rows), 36f, 26f, 6);
            Rect rowsArea = new Rect(a.x, y + 66, a.width, metrics.ViewportHeight);
            var ev = Event.current;
            if (ev != null && ev.type == EventType.ScrollWheel && rowsArea.Contains(ev.mousePosition))
            {
                debriefFirstRow = metrics.Scroll(debriefFirstRow, ev.delta.y);
                ev.Use();
            }
            debriefFirstRow = metrics.ClampFirstRow(debriefFirstRow);
            // Top scorer across both teams wears the crown (presentation only; ties keep the first row).
            int topRow = -1;
            uint topScore = 0;
            for (int i = 0; i < scoreboard.RowCount; i++)
                if (scoreboard.Rows[i].Score > topScore) { topScore = scoreboard.Rows[i].Score; topRow = i; }
            for (byte team = 1; team <= 2; team++)
            {
                float x = a.x + (team - 1) * (w + 16);
                Color color = team == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                ui.Panel(new Rect(x, y, w, height - 8));
                // Header band: team-tinted gradient, faction emblem, name and kills.
                Rect band = new Rect(x, y, w, 40);
                TacticalUi.Plate(band, UiSdf.Style.Solid(new Color(color.r, color.g, color.b, .28f), Hud.UiShapeParams.PlateRadius)
                    .WithGradient(new Color(color.r, color.g, color.b, 0f), UiSdf.Gradient.Horizontal));
                TacticalUi.Fill(new Rect(x + 4, y, w - 8, 2), color);
                bool emblem = TacticalUi.Icon(new Rect(x + 10, y + 6, 28, 28), team == 1 ? IconLibrary.EmblemAlly : IconLibrary.EmblemEnemy, color, shadow: true);
                float tx = emblem ? x + 46 : x + 12;
                uint kills = team == 1 ? scoreboard.Team1Kills : scoreboard.Team2Kills;
                ui.Label(new Rect(tx, y + 10, w - 120, 24), team == 1 ? "TEAM 1 / ALLIES" : "TEAM 2 / ENEMIES", 14, color);
                ui.Label(new Rect(x + w - 74, y + 4, 62, 30), kills.ToString(), 24, TacticalUi.Text, TextAnchor.UpperRight);
                float rowY = y + 46;
                TacticalUi.Fill(new Rect(x + 10, rowY + 16, w - 20, 1), TacticalUi.Raised);
                ui.Label(new Rect(x + 12, rowY, w - 148, 16), "OPERATOR", 12, TacticalUi.Muted);
                ui.Label(new Rect(x + w - 140, rowY, 44, 16), "SCORE", 12, TacticalUi.Muted, TextAnchor.UpperRight);
                ui.Label(new Rect(x + w - 90, rowY, 32, 16), "K", 12, TacticalUi.Muted, TextAnchor.UpperRight);
                ui.Label(new Rect(x + w - 44, rowY, 32, 16), "D", 12, TacticalUi.Muted, TextAnchor.UpperRight);
                int rowIndex = 0;
                float rh = metrics.RowHeight;
                for (int i = 0; i < scoreboard.RowCount; i++)
                {
                    var row = scoreboard.Rows[i];
                    if (row.Team != team) continue;
                    if (!metrics.IsVisible(debriefFirstRow, rowIndex)) { rowIndex++; continue; }
                    float ry = rowY + 20 + (rowIndex - debriefFirstRow) * rh;
                    Rect rowRect = new Rect(x + 4, ry, w - 8, rh - 3);
                    Color rowFill = row.IsLocal ? new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, .16f)
                        : rowIndex % 2 == 0 ? new Color(.05f, .065f, .07f, .7f) : new Color(.035f, .048f, .055f, .55f);
                    var rowStyle = UiSdf.Style.Solid(rowFill, Hud.UiShapeParams.ChipRadius);
                    if (row.IsLocal) rowStyle = rowStyle.WithGlow(TacticalUi.Accent, 6f, ui.ReducedFlash ? .1f : .18f);
                    TacticalUi.Plate(rowRect, rowStyle);
                    if (row.IsLocal) TacticalUi.Plate(new Rect(x + 4, ry + 3, 3, rh - 9), UiSdf.Style.Solid(TacticalUi.Accent, 1.5f));
                    float nx = x + 14;
                    if (i == topRow && TacticalUi.Icon(new Rect(nx, ry + (rh - 3) * .5f - 9, 18, 18), IconLibrary.Crown, TacticalUi.Gold, shadow: true))
                        nx += 24;
                    if (row.RankIndex > 0 && DrawRankIcon(new Rect(nx, ry + (rh - 3) * .5f - 10, 20, 20), row.RankIndex, 1f))
                        nx += 26;
                    string name = (row.IsLocal ? "YOU / " : row.IsHuman ? "OP / " : "BOT / ") + row.DisplayName;
                    ui.Label(new Rect(nx, ry, w - 160 - (nx - x - 14), rh - 3), name, 16, TacticalUi.Text, TextAnchor.MiddleLeft);
                    ui.Label(new Rect(x + w - 140, ry, 44, rh - 3), row.Score.ToString(), 16, TacticalUi.Accent, TextAnchor.MiddleRight);
                    ui.Label(new Rect(x + w - 90, ry, 32, rh - 3), row.Kills.ToString(), 16, TacticalUi.Text, TextAnchor.MiddleRight);
                    ui.Label(new Rect(x + w - 44, ry, 32, rh - 3), row.Deaths.ToString(), 16, TacticalUi.Muted, TextAnchor.MiddleRight);
                    rowIndex++;
                }
                if (metrics.NeedsScroll) DrawScrollRail(new Rect(x + w - 6, y + 66, 3, metrics.UsedHeight), metrics, debriefFirstRow, color);
            }
            tableScrollHint = metrics.NeedsScroll
                ? "MOUSE WHEEL  ·  ROWS " + (debriefFirstRow + 1) + "-" + (debriefFirstRow + metrics.VisibleRows) + " OF " + metrics.TotalRows
                : null;
        }

        /// <summary>Thin track plus thumb showing which rows of a scrolled table are on screen.</summary>
        private static void DrawScrollRail(Rect track, TeamTableMetrics metrics, int firstRow, Color color)
        {
            if (track.height <= 0f || metrics.TotalRows <= 0) return;
            TacticalUi.Fill(track, new Color(1f, 1f, 1f, .08f));
            float thumb = track.height * metrics.VisibleRows / metrics.TotalRows;
            float top = track.y + track.height * metrics.ClampFirstRow(firstRow) / metrics.TotalRows;
            TacticalUi.Fill(new Rect(track.x, top, track.width, Mathf.Max(6f, thumb)), new Color(color.r, color.g, color.b, .8f));
        }
    }
}
