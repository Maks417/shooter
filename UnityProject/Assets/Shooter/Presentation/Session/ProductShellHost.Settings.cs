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
using Shooter.Presentation.Hud;
using Shooter.Presentation.Style;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Presentation.Session
{
    /// <summary>Settings screen: draft/apply/revert, display confirmation, sliders, bindings and display options.</summary>
    public sealed partial class ProductShellHost : MonoBehaviour
    {
        private void TickDisplayConfirmClock()
        {
            if (settingsEditor == null || settingsApplier == null) return;
            if (!displayClock.Pending && !settingsApplier.HasPendingDisplayChange) return;
            if (displayClock.Tick(Time.unscaledDeltaTime, focusLost: !Application.isFocused, out bool shouldRevert) && shouldRevert)
            {
                var revert = settingsEditor.RevertDisplay(settingsApplier);
                settingsStatus = revert.Succeeded ? "Display reverted." : revert.UserFacing;
                SyncUiScaleFromApplied();
            }
        }

        private void CancelSettingsAndClose()
        {
            settingsEditor?.Cancel();
            if (settingsEditor != null)
                bindingEditor?.Load(settingsEditor.Applied.Bindings);
            else
                bindingEditor?.CancelListen();
            navigation.CloseSettings();
            settingsStatus = "";
        }

        private void SyncUiScaleFromApplied()
        {
            if (settingsEditor == null) return;
            uiScale = Mathf.Clamp(settingsEditor.Applied.UiScale, GameContractVersion.MinUiScale, GameContractVersion.MaxUiScale);
            view?.ApplyUserSettings(settingsEditor.Applied);
            menuScene?.SetReducedMotion(settingsEditor.Applied.ReducedCameraMotion);
        }

        private void ApplySettingsDraft()
        {
            if (settingsEditor == null || settingsStore == null || settingsApplier == null) return;
            if (bindingEditor != null && bindingEditor.IsListening)
            {
                settingsStatus = "Finish or cancel rebind before Apply.";
                return;
            }
            var result = settingsEditor.Apply(settingsStore, settingsApplier);
            SyncUiScaleFromApplied();
            if (settingsEditor != null)
                bindingEditor?.Load(settingsEditor.Applied.Bindings);
            if (settingsApplier.HasPendingDisplayChange)
            {
                displayClock.Begin();
                settingsStatus = result.Succeeded
                    ? "Display changed — Keep or Revert (" + displayClock.RemainingSeconds.ToString("0") + "s)."
                    : result.UserFacing;
            }
            else
            {
                displayClock.Clear();
                settingsStatus = result.Succeeded ? "Settings applied." : result.UserFacing;
            }
        }

        private void ConfirmPendingDisplay()
        {
            if (settingsEditor == null || settingsApplier == null || settingsStore == null) return;
            var result = settingsEditor.ConfirmDisplay(settingsStore, settingsApplier);
            displayClock.Clear();
            settingsStatus = result.Succeeded ? "Display kept." : result.UserFacing;
        }

        private void OpenSettingsScreen()
        {
            if (settingsEditor != null && bindingEditor != null)
            {
                settingsEditor.Cancel();
                bindingEditor.Load(settingsEditor.Draft.Bindings);
            }
            navigation.OpenSettings();
        }

        private void RevertPendingDisplay()
        {
            if (settingsEditor == null || settingsApplier == null) return;
            var result = settingsEditor.RevertDisplay(settingsApplier);
            displayClock.Clear();
            SyncUiScaleFromApplied();
            settingsStatus = result.Succeeded ? "Display reverted." : result.UserFacing;
        }

        private void DrawSettings(float s)
        {
            if (settingsEditor == null || bindingEditor == null) return;
            ui.Backdrop(navigation.SettingsReturn == ShellScreen.Paused, dim: .5f);
            ui.Header(ShellUiCopy.EyebrowPreferences, ShellUiCopy.SettingsTitle);
            Rect a = ui.Safe;
            // ADR-123 page skeleton (pure SettingsLayout); ADR-085 category glyphs; ADR-114 shared tab strip.
            var layout = SettingsLayout.Build(new HudRect(a.x, a.y, a.width, a.height));
            int pickedTab = ui.TabStrip(R(layout.Tabs), "settings.tab", SettingsTabs, SettingsTabIcons, settingsTab);
            if (pickedTab != settingsTab)
            {
                settingsTab = pickedTab;
                settingsScroll = Vector2.zero;
            }

            var draft = settingsEditor.Draft;
            float master = draft.MasterVolume, effects = draft.EffectsVolume, uiVol = draft.UiVolume, music = draft.MusicVolume;
            byte quality = draft.QualityTier;
            int width = draft.ResolutionWidth, height = draft.ResolutionHeight, fps = draft.FpsCap;
            var mode = draft.DisplayMode;
            bool vsync = draft.VSync, invertY = draft.InvertY, reducedMotion = draft.ReducedCameraMotion;
            bool reducedFlash = draft.ReducedFlash, subtitles = draft.Subtitles;
            float fov = draft.HipFovDegrees, sens = draft.MouseSensitivity, scale = draft.UiScale, contrast = draft.Contrast;
            // ADR-123: controls sit on a Hero frame so sliders read over the vista; the scroll view is inset inside it.
            Rect controlsPanel = R(layout.Controls);
            ui.Frame(controlsPanel, FrameKind.Hero);
            Rect viewport = new Rect(controlsPanel.x + 16, controlsPanel.y + 12, controlsPanel.width - 24, controlsPanel.height - 20);
            if (layout.Help.HasValue) DrawSettingsHelp(R(layout.Help.Value), settingsTab);
            float contentHeight = settingsTab == 2 ? 300 + bindingEditor.Working.Count * 40 : settingsTab == 3 ? 350 : settingsTab == 1 ? 310 : 285;
            float cw = viewport.width - 30;
            settingsScroll = GUI.BeginScrollView(viewport, settingsScroll, new Rect(0, 0, cw, contentHeight));
            float y = 4;
            if (settingsTab == 0)
            {
                ui.Section(new Rect(0, y, cw, 18), ShellUiCopy.SettingsSound);
                y += 28;
                master = SettingsSlider(ref y, cw, "Master volume", master, 0, 1, (master * 100).ToString("0") + "%");
                effects = SettingsSlider(ref y, cw, "Effects volume", effects, 0, 1, (effects * 100).ToString("0") + "%");
                uiVol = SettingsSlider(ref y, cw, "Interface volume", uiVol, 0, 1, (uiVol * 100).ToString("0") + "%");
                music = SettingsSlider(ref y, cw, "Music volume", music, 0, 1, (music * 100).ToString("0") + "%");
                ui.Label(new Rect(0, y + 8, cw, 36), ShellUiCopy.SettingsMuteHint, UiType.Body, TacticalUi.Muted);
            }
            else if (settingsTab == 1)
            {
                ui.Section(new Rect(0, y, cw, 18), ShellUiCopy.SettingsDisplay);
                y += 28;
                if (SettingButton(ref y, cw, "quality", "QUALITY", QualityLabel(quality))) quality = (byte)((quality + 1) % 3);
                if (SettingButton(ref y, cw, "resolution", "RESOLUTION", width + " x " + height)) CycleResolution(ref width, ref height);
                if (SettingButton(ref y, cw, "display", "DISPLAY MODE", mode.ToString())) mode = NextDisplayMode(mode);
                vsync = ui.Toggle(new Rect(0, y, cw, 36), "settings.vsync", "V-SYNC", vsync); y += 46;
                if (SettingButton(ref y, cw, "cap", "FRAME RATE LIMIT", fps == 0 ? "Uncapped" : fps + " FPS")) fps = NextFpsCap(fps);
                ui.Label(new Rect(0, y + 10, cw, 56), ShellUiCopy.SettingsDisplayHint, UiType.Body, TacticalUi.Muted);
            }
            else if (settingsTab == 2)
            {
                ui.Section(new Rect(0, y, cw, 18), ShellUiCopy.SettingsLook);
                y += 28;
                fov = SettingsSlider(ref y, cw, "Vertical field of view", fov, GameContractVersion.MinHipFovDegrees, GameContractVersion.MaxHipFovDegrees, fov.ToString("0") + " deg");
                sens = SettingsSlider(ref y, cw, "Mouse sensitivity", sens, GameContractVersion.MinMouseSensitivity, GameContractVersion.MaxMouseSensitivity, sens.ToString("0.00") + " deg / unit");
                invertY = ui.Toggle(new Rect(0, y, cw, 36), "settings.invert", "INVERT VERTICAL LOOK", invertY); y += 48;
                ui.Section(new Rect(0, y, cw, 18), ShellUiCopy.SettingsBindings); y += 28;
                DrawBindingRows(1, ref y, cw);
            }
            else
            {
                ui.Section(new Rect(0, y, cw, 18), ShellUiCopy.SettingsReadability);
                y += 28;
                scale = SettingsSlider(ref y, cw, "Interface scale", scale, GameContractVersion.MinUiScale, GameContractVersion.MaxUiScale, (scale * 100).ToString("0") + "%");
                contrast = SettingsSlider(ref y, cw, "Scene contrast", contrast, GameContractVersion.MinContrast, GameContractVersion.MaxContrast, contrast.ToString("0.00"));
                reducedMotion = ui.Toggle(new Rect(0, y, cw, 36), "settings.motion", "REDUCED CAMERA MOTION", reducedMotion); y += 46;
                reducedFlash = ui.Toggle(new Rect(0, y, cw, 36), "settings.flash", "REDUCED FLASH", reducedFlash); y += 46;
                subtitles = ui.Toggle(new Rect(0, y, cw, 36), "settings.subtitles", "SOUND CAPTIONS", subtitles);
            }
            settingsEditor.SetDraft(new UserSettings(draft.Schema, master, effects, uiVol, quality, width, height, mode, vsync, fps,
                fov, sens, invertY, scale, reducedMotion, reducedFlash, contrast, subtitles, bindingEditor.ToArray(), music));
            GUI.EndScrollView();

            Rect notice = R(layout.Notice);
            if (settingsApplier != null && settingsApplier.HasPendingDisplayChange)
            {
                ui.Label(notice, ShellUiCopy.SettingsKeepDisplay(displayClock.RemainingSeconds), UiType.Body, TacticalUi.Accent);
                if (ui.Button(R(layout.Action(0)), "settings.keep", ShellUiCopy.SettingsKeep, primary: true)) ConfirmPendingDisplay();
                if (ui.Button(R(layout.Action(1)), "settings.revert", ShellUiCopy.SettingsRevert)) RevertPendingDisplay();
            }
            else
            {
                GUI.enabled = !bindingEditor.IsListening;
                if (ui.Button(R(layout.Action(0)), "settings.apply", ShellUiCopy.SettingsApply, primary: true)) ApplySettingsDraft();
                GUI.enabled = true;
                if (ui.Button(R(layout.Action(1)), "settings.cancel", ShellUiCopy.ConfirmCancel)) CancelSettingsAndClose();
                if (ui.Button(R(layout.Action(2)), "settings.defaults", ShellUiCopy.SettingsDefaults))
                {
                    settingsEditor.RestoreDefaults(); bindingEditor.RestoreDefaults(); settingsStatus = ShellUiCopy.SettingsDefaultsStaged;
                }
                if (ui.Button(R(layout.Action(3)), "settings.back", ShellUiCopy.SetupBack)) CancelSettingsAndClose();
            }
            string status = string.IsNullOrEmpty(settingsStatus)
                ? (settingsEditor.IsDirty ? ShellUiCopy.SettingsUnsaved : ShellUiCopy.SettingsSaved) : settingsStatus;
            ui.KeyHints(status, ShellUiCopy.KeyTab, ShellUiCopy.HintNavigate, ShellUiCopy.KeyEnter, ShellUiCopy.HintSelect, ShellUiCopy.KeyEsc, ShellUiCopy.HintBack);
        }

        /// <summary>ADR-123 help pane: what the open tab controls and how changes are kept.</summary>
        private void DrawSettingsHelp(Rect r, int tab)
        {
            ui.Frame(r, FrameKind.Hero);
            string icon = SettingsTabIcons[Mathf.Clamp(tab, 0, SettingsTabIcons.Length - 1)];
            TacticalUi.Icon(new Rect(r.x + 18, r.y + 18, 28, 28), icon, TacticalUi.Accent);
            ui.Label(new Rect(r.x + 56, r.y + 22, r.width - 74, 20), SettingsTabs[Mathf.Clamp(tab, 0, SettingsTabs.Length - 1)], UiType.Lead, TacticalUi.Text);
            TacticalUi.Fill(new Rect(r.x + 18, r.y + 58, r.width - 36, 1), UiInk.Alpha(TacticalUi.PanelEdge, .8f));
            ui.Label(new Rect(r.x + 18, r.y + 70, r.width - 36, r.height - 120), ShellUiCopy.SettingsHelp(tab), UiType.Body, TacticalUi.Muted);
            ui.Label(new Rect(r.x + 18, r.yMax - 44, r.width - 36, 32), ShellUiCopy.SettingsHelpFooter, UiType.Caption, TacticalUi.Dim);
        }

        private float SettingsSlider(ref float y, float width, string label, float value, float min, float max, string formatted)
        {
            float next = ui.Slider(new Rect(0, y, width, 26), "settings.slider." + label, label, value, min, max, formatted);
            y += 66;
            return next;
        }

        private bool SettingButton(ref float y, float width, string id, string label, string value)
        {
            // ADR-123: label on the left, current value on the right, like sliders and toggles.
            bool next = ui.Button(new Rect(0, y, width, 36), "settings." + id, label);
            ui.Label(new Rect(width * .5f, y, width * .5f - 18, 36), value, UiType.Lead, TacticalUi.Accent, TextAnchor.MiddleRight);
            y += 46;
            return next;
        }

        private void DrawBindingRows(float s, ref float y, float width)
        {
            if (bindingEditor == null) return;
            if (bindingEditor.HasConflict)
            {
                ui.Label(new Rect(0, y, width, 50), ShellUiCopy.BindingConflict(bindingEditor.ConflictAction), UiType.Lead, TacticalUi.Accent); y += 54;
                if (ui.Button(new Rect(0, y, 160, 36), "binding.swap", ShellUiCopy.BindingSwap)) bindingEditor.TrySwapConflict();
                if (ui.Button(new Rect(170, y, 160, 36), "binding.cancel", ShellUiCopy.ConfirmCancel)) bindingEditor.TryCancelConflict();
                y += 46;
                return;
            }
            if (bindingEditor.IsListening)
            {
                ui.Label(new Rect(0, y, width, 46), ShellUiCopy.BindingListen(bindingEditor.ListeningAction), UiType.Lead, TacticalUi.Accent); y += 54;
                string? captured = Time.frameCount > bindingListenAfterFrame ? BoundControlMap.TryCapturePressedControlThisFrame() : null;
                if (captured != null)
                {
                    if (string.Equals(captured, UserSettings.ReservedEscapeControl, StringComparison.OrdinalIgnoreCase)) bindingEditor.CancelListen();
                    else bindingEditor.TryOfferControl(captured);
                }
            }
            for (int i = 0; i < bindingEditor.Working.Count; i++)
            {
                var binding = bindingEditor.Working[i];
                string action = ShellUiCopy.BindingLabel(binding.Action);
                bool pause = string.Equals(binding.Action, "Pause", StringComparison.Ordinal);
                ui.Label(new Rect(0, y, width * .5f, 34), action, UiType.Lead, TacticalUi.Text, TextAnchor.MiddleLeft);
                if (pause) ui.Label(new Rect(width * .5f, y, width * .5f, 34), ShellUiCopy.BindingReserved, UiType.Lead, TacticalUi.Muted, TextAnchor.MiddleLeft);
                else if (ui.Button(new Rect(width * .5f, y, width * .5f, 34), "binding." + binding.Action,
                    bindingEditor.IsListening && bindingEditor.ListeningAction == binding.Action ? ShellUiCopy.BindingListening : ""))
                {
                    bindingEditor.TryBeginListen(binding.Action);
                    bindingListenAfterFrame = Time.frameCount;
                }
                if(!pause && !(bindingEditor.IsListening&&bindingEditor.ListeningAction==binding.Action))
                {
                    string key=TacticalUi.ControlLabel(binding.Control);float keyWidth=Mathf.Min(width*.45f,Mathf.Max(40,key.Length*6+14));
                    ui.Keycap(new Rect(width*.5f+(width*.5f-keyWidth)*.5f,y+8,keyWidth,18),key,TacticalUi.Text);
                }
                y += 40;
            }
        }

        private static string QualityLabel(byte tier) => tier switch { 0 => "Low", 1 => "Balanced", _ => "High" };

        private void CycleResolution(ref int width, ref int height)
        {
            if (productSettingsApplier != null
                && productSettingsApplier.TryCycleResolution(width, height, out int nextW, out int nextH))
            {
                width = nextW;
                height = nextH;
                return;
            }
            if (width == 1280 && height == 720) { width = 1920; height = 1080; }
            else { width = 1280; height = 720; }
        }

        private static DisplayMode NextDisplayMode(DisplayMode mode) => mode switch
        {
            DisplayMode.Windowed => DisplayMode.Borderless,
            DisplayMode.Borderless => DisplayMode.Fullscreen,
            _ => DisplayMode.Windowed
        };

        private static int NextFpsCap(int fps) => fps switch
        {
            30 => 60,
            60 => 120,
            120 => 0,
            _ => 30
        };
    }
}
