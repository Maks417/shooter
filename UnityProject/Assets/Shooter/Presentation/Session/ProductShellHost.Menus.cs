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
    /// <summary>Main, setup, loading, pause and confirm screens of the product shell.</summary>
    public sealed partial class ProductShellHost : MonoBehaviour
    {
        private void DrawMain(float s)
        {
            ui.Backdrop();
            Rect a = ui.Safe;
            float navWidth = Mathf.Min(236, a.width * .22f);
            ui.BrandMark(new Rect(a.x, a.y, navWidth, 28), ShellUiCopy.Brand);
            ui.Section(new Rect(a.x, a.y + 34, navWidth, 16), ShellUiCopy.LocalMatch);
            ui.Label(new Rect(a.x, a.y + 58, navWidth + 56, 64), ShellUiCopy.MainHeadline, 28);
            float y = a.y + 140;
            if (ui.Button(new Rect(a.x, y, navWidth, 36), "main.play", ShellUiCopy.Play, quiet: true)) navigation.OpenSetup();
            if (ui.Button(new Rect(a.x, y + 40, navWidth, 36), "main.armory", ShellUiCopy.Armory, quiet: true)) OpenArmory();
            if (ui.Button(new Rect(a.x, y + 80, navWidth, 36), "main.career", ShellUiCopy.Career, quiet: true)) OpenCareer();
            if (ui.Button(new Rect(a.x, y + 120, navWidth, 36), "main.settings", ShellUiCopy.Settings, quiet: true)) OpenSettingsScreen();
            if (ui.Button(new Rect(a.x, y + 160, navWidth, 36), "main.quit", ShellUiCopy.Quit, quiet: true))
            {
                quitRequested = true;
                Application.Quit();
            }
            // ADR-108: current rank under the menu, one click from the Career page.
            DrawRankBadge(new Rect(a.x, y + 210, navWidth + 40, 44), "main.rank");
            if (armory != null)
            {
                ui.Section(new Rect(a.x, y + 266, navWidth, 16), ShellUiCopy.ArmoryLoadout);
                ui.Label(new Rect(a.x, y + 286, navWidth + 40, 40), armory.Summary, 14, TacticalUi.Muted);
            }
            float cardsX = a.x + navWidth + 28;
            float cardsWidth = a.xMax - cardsX;
            float cardGap = 10f;
            float cardWidth = (cardsWidth - cardGap * 2f) / 3f;
            float cardHeight = Mathf.Min(198, a.height * .38f);
            float cardsY = a.yMax - cardHeight - 22;
            ui.Section(new Rect(cardsX, cardsY - 22, cardsWidth, 16), ShellUiCopy.MainBattles);
            DrawMainMapCard(new Rect(cardsX, cardsY, cardWidth, cardHeight), "main.compound", WellKnownMaps.TrainingCompound, ShellUiCopy.MapCompound);
            DrawMainMapCard(new Rect(cardsX + cardWidth + cardGap, cardsY, cardWidth, cardHeight), "main.depot", WellKnownMaps.DepotYard, ShellUiCopy.MapDepot);
            DrawMainMapCard(new Rect(cardsX + (cardWidth + cardGap) * 2f, cardsY, cardWidth, cardHeight), "main.oil", WellKnownMaps.OilDepot, ShellUiCopy.MapOil);
            string status = !string.IsNullOrEmpty(navigation.StatusMessage) ? navigation.StatusMessage : menuScene?.Status ?? "";
            DrawFooter(string.IsNullOrEmpty(status) ? ShellUiCopy.MainNavigate : status);
        }

        private void DrawSetup(float s)
        {
            ui.Backdrop();
            ui.Header(ShellUiCopy.EyebrowBriefing, ShellUiCopy.SetupTitle);
            Rect a = ui.Safe;
            float y = a.y + 78;
            float left = a.width * .54f;
            float rx = a.x + left + 20;
            float rw = a.width - left - 20;
            ui.Section(new Rect(a.x, y, left, 18), ShellUiCopy.SetupSelectArea);
            DrawSetupMapButton(new Rect(a.x, y + 24, left, 36), "setup.compound", WellKnownMaps.TrainingCompound, ShellUiCopy.SetupCompound);
            DrawSetupMapButton(new Rect(a.x, y + 64, left, 36), "setup.depot", WellKnownMaps.DepotYard, ShellUiCopy.SetupDepot);
            DrawSetupMapButton(new Rect(a.x, y + 104, left, 36), "setup.oil", WellKnownMaps.OilDepot, ShellUiCopy.SetupOil);
            float previewH = Mathf.Min(118, a.height * .22f);
            Rect preview = new Rect(a.x, y + 150, left, previewH);
            ui.Panel(preview, rail: true);
            Texture? card = menuScene?.MapCard(setup.SelectedMapId.Value);
            if (card != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(preview.x + 4, preview.y + 4, preview.width * .42f, preview.height - 8), card, ScaleMode.ScaleAndCrop);
            float textX = card != null ? preview.x + preview.width * .44f : preview.x + 14;
            float textW = card != null ? preview.width * .54f : preview.width - 28;
            ui.Label(new Rect(textX, preview.y + 12, textW, 22), setup.SelectedDisplayName.ToUpperInvariant(), 16, TacticalUi.Text);
            ui.Label(new Rect(textX, preview.y + 36, textW, preview.height - 48), setup.SelectedDescription, 16, TacticalUi.Muted);
            ui.Section(new Rect(rx, y, rw, 18), ShellUiCopy.SetupMode);
            float half = (rw - 8) * .5f;
            if (ui.Button(new Rect(rx, y + 24, half, 36), "setup.5", "5 v 5", selected: setup.SelectedTeamSize == 5)) setup.SelectTeamSize(5);
            if (ui.Button(new Rect(rx + half + 8, y + 24, half, 36), "setup.8", "8 v 8", selected: setup.SelectedTeamSize == 8)) setup.SelectTeamSize(8);
            if (ui.Button(new Rect(rx, y + 64, half, 36), "setup.10", "10 v 10", selected: setup.SelectedTeamSize == 10)) setup.SelectTeamSize(10);
            if (ui.Button(new Rect(rx + half + 8, y + 64, half, 36), "setup.15", "15 v 15", selected: setup.SelectedTeamSize == 15)) setup.SelectTeamSize(15);
            ui.Panel(new Rect(rx, y + 110, rw, 128), accentTop: true);
            ui.Label(new Rect(rx + 14, y + 122, rw - 28, 22), ShellUiCopy.SetupRules, 12, TacticalUi.Accent);
            ui.Label(new Rect(rx + 14, y + 144, rw - 28, 40), ShellUiCopy.SetupRulesBody, 18);
            ui.Label(new Rect(rx + 14, y + 188, rw - 28, 40), ShellUiCopy.SetupRosterLine(setup.SelectedTeamSize), 16, TacticalUi.Muted);
            if (armory != null)
            {
                // ADR-107: the loadout this match will use, one click from the Armory.
                ui.Section(new Rect(rx, y + 250, rw, 18), ShellUiCopy.ArmoryLoadout);
                if (ui.Button(new Rect(rx, y + 274, rw, 36), "setup.armory", armory.Summary)) OpenArmory();
            }
            GUI.enabled = !coordinator.StartInFlight;
            if (ui.Button(new Rect(rx, a.yMax - 66, rw, 44), "setup.start", ShellUiCopy.SetupStart, primary: true)) StartSelectedMatch();
            GUI.enabled = true;
            if (ui.Button(new Rect(a.x, a.yMax - 66, 150, 44), "setup.back", ShellUiCopy.SetupBack)) navigation.ResetToMain();
            DrawFooter(navigation.StatusMessage);
        }

        private void DrawLoading(float s)
        {
            Texture? card = menuScene?.MapCard(setup!.SelectedMapId.Value);
            bool reducedMotion = settingsEditor != null && settingsEditor.Applied.ReducedCameraMotion;
            string mapName = InsertionScreenCopy.DisplayNameOrFallback(setup.SelectedDisplayName, setup.SelectedMapId.Value);
            System.Collections.Generic.IReadOnlyList<Shooter.Generation.Environment.CollisionBox>? blueprint = null;
            Shooter.Contracts.Float3? spawn1 = null, spawn2 = null;
            if (backend != null && backend.TryGetVerifiedMap(out var verifiedBoxes, out var meta, out var verifiedMap) && verifiedMap == setup.SelectedMapId.Value)
            {
                blueprint = verifiedBoxes;
                if (meta != null)
                    foreach (var slot in Shooter.Generation.Environment.MapJsonValue.Parse(meta).Object["spawnSlots"].Array)
                    {
                        var v = slot.Object; var p = v["positionMeters"].Array;
                        var point = new Shooter.Contracts.Float3((float)p[0].Number, (float)p[1].Number, (float)p[2].Number);
                        if (v["team"].Int32 == 1 && !spawn1.HasValue) spawn1 = point;
                        if (v["team"].Int32 == 2 && !spawn2.HasValue) spawn2 = point;
                    }
            }
            if (ui.DrawInsertionScreen(
                    mapName,
                    InsertionScreenCopy.MatchTitle,
                    mapName,
                    InsertionScreenCopy.MatchRules(setup.SelectedTeamSize),
                    navigation.StatusMessage,
                    card,
                    showCancel: true,
                    reducedMotion,
                    InsertionScreenCopy.MatchFooter(setup.SelectedTeamSize), blueprint, spawn1, spawn2))
                CancelLoading();
        }

        /// <summary>Yields so IMGUI can paint Loading before the next synchronous hitch.</summary>
        private static async Task AwaitLoadingPaintAsync()
        {
            await Task.Yield();
            await Task.Yield();
        }

        private void DrawPause(float s)
        {
            ui.Backdrop(overlay: true);
            ui.Header(ShellUiCopy.EyebrowPaused, ShellUiCopy.PauseTitle);
            Rect a = ui.Safe;
            float w = Mathf.Min(300, a.width * .40f), y = a.y + 82;
            ui.Section(new Rect(a.x, y - 22, w, 18), ShellUiCopy.PauseCommands);
            if (ui.Button(new Rect(a.x, y, w, 42), "pause.resume", ShellUiCopy.Resume, primary: true)) ResumeMatch();
            if (ui.Button(new Rect(a.x, y + 50, w, 38), "pause.settings", ShellUiCopy.Settings)) OpenSettingsScreen();
            if (ui.Button(new Rect(a.x, y + 96, w, 38), "pause.restart", ShellUiCopy.RestartMatch)) navigation.AskRestart();
            if (ui.Button(new Rect(a.x, y + 142, w, 38), "pause.leave", ShellUiCopy.MainMenu)) navigation.AskLeave();
            float px = a.x + w + 24;
            float pw = a.xMax - px;
            ui.Panel(new Rect(px, y, pw, 180), accentTop: true, rail: true);
            ui.Label(new Rect(px + 16, y + 16, pw - 32, 22), ShellUiCopy.PauseHold, 14, TacticalUi.Accent);
            ui.Label(new Rect(px + 16, y + 46, pw - 32, 72), ShellUiCopy.PauseBody, 18, TacticalUi.Muted);
            ui.Label(new Rect(px + 16, y + 128, pw - 32, 36), ShellUiCopy.PauseHint, 14, TacticalUi.Muted);
            DrawFooter(ShellUiCopy.PauseFooter);
        }

        private void DrawConfirm(float s, string message, Action confirm, Action cancel)
        {
            ui.Backdrop(overlay: true);
            float w = Mathf.Min(540, ui.Safe.width), x = (ui.Width - w) * .5f, y = ui.Height * .5f - 92;
            ui.Panel(new Rect(x, y, w, 184), accentTop: true);
            ui.Label(new Rect(x + 22, y + 18, w - 44, 24), ShellUiCopy.ConfirmTitle, 20);
            ui.Label(new Rect(x + 22, y + 54, w - 44, 48), message, 18, TacticalUi.Muted);
            float bw = (w - 56) * .5f;
            // Safe choice receives initial keyboard focus.
            if (ui.Button(new Rect(x + 22, y + 124, bw, 40), "confirm.cancel", ShellUiCopy.ConfirmCancel)) cancel();
            if (ui.Button(new Rect(x + 34 + bw, y + 124, bw, 40), "confirm.accept", ShellUiCopy.ConfirmAccept, primary: true)) confirm();
        }

        private void DrawMainMapCard(Rect rect, string id, MapId mapId, string title)
        {
            bool selected = setup!.SelectedMapId.Equals(mapId);
            if (ui.MapCard(rect, id, menuScene?.MapCard(mapId.Value), title, ShellUiCopy.MainCardSubtitle, selected))
            {
                setup.SelectMap(mapId);
                navigation.OpenSetup();
            }
        }

        private void DrawSetupMapButton(Rect rect, string id, MapId mapId, string label)
        {
            bool selected = setup!.SelectedMapId.Equals(mapId);
            if (ui.Button(rect, id, label, selected: selected) && !selected)
                setup.SelectMap(mapId);
        }

        // ------------------------------------------------------------ ADR-107 Armory

        private void DrawFooter(string text)
        {
            Rect a = ui.Safe;
            TacticalUi.Fill(new Rect(a.x, a.yMax - 24, Mathf.Min(96, a.width * .16f), 1), TacticalUi.Raised);
            TacticalUi.Fill(new Rect(a.x, a.yMax - 24, 16, 1), TacticalUi.Accent);
            ui.Label(new Rect(a.x, a.yMax - 16, a.width, 16), text, 12, TacticalUi.Muted);
        }
    }
}
