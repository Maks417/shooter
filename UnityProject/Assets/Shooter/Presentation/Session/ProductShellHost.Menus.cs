#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Presentation.Input;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Style;
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
        /// <summary>
        /// ADR-123 Main: brand lockup, headline and navigation on a scrimmed rail over the live map vista; operation cards
        /// along the bottom right. Hovering or focusing a card stages that map's vista. Rects come from the pure
        /// <see cref="MainMenuLayout"/>.
        /// </summary>
        private void DrawMain(float s)
        {
            ui.Backdrop();
            var l = MainMenuLayout.Build(new HudRect(ui.Safe.x, ui.Safe.y, ui.Safe.width, ui.Safe.height));
            ui.BrandMark(R(l.Brand), ShellUiCopy.Brand);
            if (l.Compact) ui.LabelFit(R(l.Headline), ShellUiCopy.MainHeadline.Replace('\n', ' '), UiType.Heading + 4, TacticalUi.Text, TextAnchor.UpperLeft, UiType.Heading);
            else
            {
                ui.Section(new Rect(l.Headline.X, l.Headline.Y, l.Headline.Width, 16), ShellUiCopy.LocalMatch);
                ui.Label(new Rect(l.Headline.X, l.Headline.Y + 22, l.Headline.Width, l.Headline.Height - 22), ShellUiCopy.MainHeadline, UiType.Title);
            }
            string[] ids = { "main.play", "main.armory", "main.career", "main.settings", "main.quit" };
            string[] labels = { ShellUiCopy.Play, ShellUiCopy.Armory, ShellUiCopy.Career, ShellUiCopy.Settings, ShellUiCopy.Quit };
            for (int i = 0; i < MainMenuLayout.NavCount; i++)
            {
                Rect row = R(l.Nav(i));
                if (!ui.Button(row, ids[i], labels[i], quiet: true)) continue;
                switch (i)
                {
                    case 0: navigation.OpenSetup(); break;
                    case 1: OpenArmory(); break;
                    case 2: OpenCareer(); break;
                    case 3: OpenSettingsScreen(); break;
                    default:
                        quitRequested = true;
                        Application.Quit();
                        break;
                }
            }
            // ADR-108: current rank under the menu, one click from the Career page.
            DrawRankBadge(R(l.Rank), "main.rank");
            if (armory != null)
            {
                ui.Section(new Rect(l.Loadout.X, l.Loadout.Y, l.Loadout.Width, 16), ShellUiCopy.ArmoryLoadout);
                ui.LabelFit(new Rect(l.Loadout.X, l.Loadout.Y + 24, l.Loadout.Width, 20), armory.Summary, UiType.Small, TacticalUi.Muted, TextAnchor.UpperLeft, UiType.Caption);
            }
            ui.Section(R(l.CardsLabel), ShellUiCopy.MainBattles);
            string? hovered = null;
            hovered = DrawMainMapCard(R(l.Card(0, 3)), "main.compound", WellKnownMaps.TrainingCompound, ShellUiCopy.MapCompound) ?? hovered;
            hovered = DrawMainMapCard(R(l.Card(1, 3)), "main.depot", WellKnownMaps.DepotYard, ShellUiCopy.MapDepot) ?? hovered;
            hovered = DrawMainMapCard(R(l.Card(2, 3)), "main.oil", WellKnownMaps.OilDepot, ShellUiCopy.MapOil) ?? hovered;
            // ADR-123: the vista follows the card under the pointer or keyboard focus, else the selected operation.
            menuScene?.SetFocusedMap(hovered ?? setup!.SelectedMapId.Value);
            string status = !string.IsNullOrEmpty(navigation.StatusMessage) ? navigation.StatusMessage : menuScene?.Status ?? "";
            DrawFooter(status);
        }

        /// <summary>ADR-123 Setup: operation list and briefing on the left, mode and roster on the right, over the vista.</summary>
        private void DrawSetup(float s)
        {
            ui.Backdrop(dim: .3f);
            ui.Header(ShellUiCopy.EyebrowBriefing, ShellUiCopy.SetupTitle, ShellUiCopy.LocalMatch);
            Rect a = ui.Safe;
            var actions = ShellPage.Actions(new HudRect(a.x, a.y, a.width, a.height));
            float y = a.y + ShellPage.HeaderHeight;
            float left = Mathf.Min(560, a.width * .52f);
            float rx = a.x + left + 24;
            float rw = Mathf.Min(420, a.xMax - rx);
            ui.Section(new Rect(a.x, y, left, 18), ShellUiCopy.SetupSelectArea);
            DrawSetupMapButton(new Rect(a.x, y + 26, left, 36), "setup.compound", WellKnownMaps.TrainingCompound, ShellUiCopy.SetupCompound);
            DrawSetupMapButton(new Rect(a.x, y + 66, left, 36), "setup.depot", WellKnownMaps.DepotYard, ShellUiCopy.SetupDepot);
            DrawSetupMapButton(new Rect(a.x, y + 106, left, 36), "setup.oil", WellKnownMaps.OilDepot, ShellUiCopy.SetupOil);
            float previewTop = y + 154;
            float previewH = Mathf.Min(150, actions.Y - 16 - previewTop);
            Rect preview = new Rect(a.x, previewTop, left, previewH);
            ui.Frame(preview, FrameKind.Hero);
            Texture? card = menuScene?.MapCard(setup!.SelectedMapId.Value);
            float textX = preview.x + 16, textW = preview.width - 32;
            if (card != null)
            {
                Rect media = new Rect(preview.x + 10, preview.y + 10, preview.width * .4f, preview.height - 20);
                TacticalUi.DrawFrame(media, FrameKind.Card);
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(new Rect(media.x + 2, media.y + 2, media.width - 4, media.height - 4), card, ScaleMode.ScaleAndCrop);
                textX = media.xMax + 14;
                textW = preview.xMax - textX - 14;
            }
            ui.Label(new Rect(textX, preview.y + 14, textW, 16), ShellUiCopy.SetupBriefing, UiType.Caption, TacticalUi.Accent);
            ui.LabelFit(new Rect(textX, preview.y + 32, textW, 28), setup!.SelectedDisplayName.ToUpperInvariant(), UiType.Heading, TacticalUi.Text, TextAnchor.UpperLeft, UiType.Lead);
            ui.Label(new Rect(textX, preview.y + 64, textW, preview.height - 72), setup.SelectedDescription, UiType.Small, TacticalUi.Muted);

            ui.Section(new Rect(rx, y, rw, 18), ShellUiCopy.SetupMode);
            float half = (rw - 8) * .5f;
            if (ui.Button(new Rect(rx, y + 26, half, 36), "setup.5", "5 v 5", selected: setup.SelectedTeamSize == 5)) setup.SelectTeamSize(5);
            if (ui.Button(new Rect(rx + half + 8, y + 26, half, 36), "setup.8", "8 v 8", selected: setup.SelectedTeamSize == 8)) setup.SelectTeamSize(8);
            if (ui.Button(new Rect(rx, y + 66, half, 36), "setup.10", "10 v 10", selected: setup.SelectedTeamSize == 10)) setup.SelectTeamSize(10);
            if (ui.Button(new Rect(rx + half + 8, y + 66, half, 36), "setup.15", "15 v 15", selected: setup.SelectedTeamSize == 15)) setup.SelectTeamSize(15);
            Rect rules = new Rect(rx, y + 114, rw, 118);
            ui.Frame(rules, FrameKind.Plate, rail: true);
            ui.Label(new Rect(rx + 16, y + 126, rw - 32, 16), ShellUiCopy.SetupRules, UiType.Caption, TacticalUi.Accent);
            ui.Label(new Rect(rx + 16, y + 146, rw - 32, 44), ShellUiCopy.SetupRulesBody, UiType.Lead);
            ui.Label(new Rect(rx + 16, y + 194, rw - 32, 24), ShellUiCopy.SetupRosterLine(setup.SelectedTeamSize), UiType.Small, TacticalUi.Muted);
            if (armory != null && y + 280 < actions.Y)
            {
                // ADR-107: the loadout this match will use, one click from the Armory.
                ui.Section(new Rect(rx, y + 244, rw, 18), ShellUiCopy.ArmoryLoadout);
                if (ui.Button(new Rect(rx, y + 270, rw, 36), "setup.armory", armory.Summary)) OpenArmory();
            }
            GUI.enabled = !coordinator.StartInFlight;
            if (ui.Button(new Rect(rx, actions.Y, rw, actions.Height), "setup.start", ShellUiCopy.SetupStart, primary: true)) StartSelectedMatch();
            GUI.enabled = true;
            if (ui.Button(new Rect(a.x, actions.Y, 160, actions.Height), "setup.back", ShellUiCopy.SetupBack)) navigation.ResetToMain();
            DrawFooter(navigation.StatusMessage, back: true);
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

        /// <summary>ADR-123 Pause: command column and a hold briefing on a modal frame over the dimmed match.</summary>
        private void DrawPause(float s)
        {
            ui.Backdrop(overlay: true);
            ui.Header(ShellUiCopy.EyebrowPaused, ShellUiCopy.PauseTitle, ActiveMode.DisplayName.ToUpperInvariant());
            Rect a = ui.Safe;
            float w = Mathf.Min(320, a.width * .4f), y = a.y + ShellPage.HeaderHeight + 26;
            ui.Section(new Rect(a.x, y - 26, w, 18), ShellUiCopy.PauseCommands);
            if (ui.Button(new Rect(a.x, y, w, 44), "pause.resume", ShellUiCopy.Resume, primary: true)) ResumeMatch();
            if (ui.Button(new Rect(a.x, y + 52, w, 40), "pause.settings", ShellUiCopy.Settings)) OpenSettingsScreen();
            if (ui.Button(new Rect(a.x, y + 100, w, 40), "pause.restart", ShellUiCopy.RestartMatch)) navigation.AskRestart();
            if (ui.Button(new Rect(a.x, y + 148, w, 40), "pause.leave", ShellUiCopy.MainMenu)) navigation.AskLeave();
            float px = a.x + w + 28;
            float pw = Mathf.Min(520, a.xMax - px);
            Rect panel = new Rect(px, y, pw, 188);
            ui.Frame(panel, FrameKind.Modal);
            ui.Label(new Rect(px + 20, y + 18, pw - 40, 16), ShellUiCopy.PauseHold, UiType.Caption, TacticalUi.Accent);
            ui.Label(new Rect(px + 20, y + 42, pw - 40, 72), ShellUiCopy.PauseBody, UiType.Lead, TacticalUi.Text);
            ui.Label(new Rect(px + 20, y + 124, pw - 40, 44), ShellUiCopy.PauseHint, UiType.Small, TacticalUi.Muted);
            ui.KeyHints("", ShellUiCopy.KeyEsc, ShellUiCopy.HintResume, ShellUiCopy.KeyTab, ShellUiCopy.HintNavigate, ShellUiCopy.KeyEnter, ShellUiCopy.HintSelect);
        }

        /// <summary>ADR-123 confirm dialog: centred modal frame, the safe choice focused first.</summary>
        private void DrawConfirm(float s, string message, Action confirm, Action cancel)
        {
            ui.Backdrop(overlay: true);
            float w = Mathf.Min(560, ui.Safe.width), x = (ui.Width - w) * .5f, y = ui.Height * .5f - 98;
            ui.Frame(new Rect(x, y, w, 196), FrameKind.Modal);
            ui.Label(new Rect(x + 24, y + 20, w - 48, 16), ShellUiCopy.ConfirmTitle, UiType.Caption, TacticalUi.Accent);
            ui.Label(new Rect(x + 24, y + 44, w - 48, 60), message, UiType.Heading - 2, TacticalUi.Text);
            float bw = (w - 60) * .5f;
            // Safe choice receives initial keyboard focus.
            if (ui.Button(new Rect(x + 24, y + 128, bw, 44), "confirm.cancel", ShellUiCopy.ConfirmCancel)) cancel();
            if (ui.Button(new Rect(x + 36 + bw, y + 128, bw, 44), "confirm.accept", ShellUiCopy.ConfirmAccept, primary: true)) confirm();
        }

        /// <summary>Draws one operation card; returns its map id while the pointer or keyboard focus is on it.</summary>
        private string? DrawMainMapCard(Rect rect, string id, MapId mapId, string title)
        {
            bool selected = setup!.SelectedMapId.Equals(mapId);
            if (ui.MapCard(rect, id, menuScene?.MapCard(mapId.Value), title, ShellUiCopy.MainCardSubtitle, selected))
            {
                setup.SelectMap(mapId);
                navigation.OpenSetup();
            }
            return rect.Contains(Event.current.mousePosition) || ui.IsFocused(id) ? mapId.Value : null;
        }

        private void DrawSetupMapButton(Rect rect, string id, MapId mapId, string label)
        {
            bool selected = setup!.SelectedMapId.Equals(mapId);
            if (ui.Button(rect, id, label, selected: selected) && !selected)
                setup.SelectMap(mapId);
        }

        // ------------------------------------------------------------ ADR-107 Armory

        /// <summary>ADR-123 shared footer: keycap hints plus the page status line.</summary>
        private void DrawFooter(string text, bool back = false)
        {
            if (back)
                ui.KeyHints(text, ShellUiCopy.KeyTab, ShellUiCopy.HintNavigate, ShellUiCopy.KeyEnter, ShellUiCopy.HintSelect, ShellUiCopy.KeyEsc, ShellUiCopy.HintBack);
            else
                ui.KeyHints(text, ShellUiCopy.KeyTab, ShellUiCopy.HintNavigate, ShellUiCopy.KeyEnter, ShellUiCopy.HintSelect);
        }
    }
}
