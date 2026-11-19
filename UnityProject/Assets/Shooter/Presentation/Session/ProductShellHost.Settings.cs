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
            ui.Backdrop(navigation.SettingsReturn == ShellScreen.Paused);
            ui.Header(ShellUiCopy.EyebrowPreferences, ShellUiCopy.SettingsTitle);
            Rect a = ui.Safe;
            // ADR-085 category glyphs; ADR-114 shared tab strip.
            int pickedTab = ui.TabStrip(new Rect(a.x, a.y + 74, a.width, 36), "settings.tab", SettingsTabs, SettingsTabIcons, settingsTab);
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
            Rect viewport = new Rect(a.x, a.y + 120, a.width, a.height - 214);
            float contentHeight = settingsTab == 2 ? 300 + bindingEditor.Working.Count * 40 : settingsTab == 3 ? 350 : settingsTab == 1 ? 310 : 285;
            float cw = viewport.width - 30;
            settingsScroll = GUI.BeginScrollView(viewport, settingsScroll, new Rect(0, 0, cw, contentHeight));
            float y = 4;
            if (settingsTab == 0)
            {
                ui.Section(new Rect(0, y, cw, 18), "SOUND LEVELS");
                y += 28;
                master = SettingsSlider(ref y, cw, "Master volume", master, 0, 1, (master * 100).ToString("0") + "%");
                effects = SettingsSlider(ref y, cw, "Effects volume", effects, 0, 1, (effects * 100).ToString("0") + "%");
                uiVol = SettingsSlider(ref y, cw, "Interface volume", uiVol, 0, 1, (uiVol * 100).ToString("0") + "%");
                music = SettingsSlider(ref y, cw, "Music volume", music, 0, 1, (music * 100).ToString("0") + "%");
                ui.Label(new Rect(0, y + 8, cw, 36), "Set a channel to 0% to mute it.", 16, TacticalUi.Muted);
            }
            else if (settingsTab == 1)
            {
                ui.Section(new Rect(0, y, cw, 18), "DISPLAY OUTPUT");
                y += 28;
                if (SettingButton(ref y, cw, "quality", "QUALITY", QualityLabel(quality))) quality = (byte)((quality + 1) % 3);
                if (SettingButton(ref y, cw, "resolution", "RESOLUTION", width + " x " + height)) CycleResolution(ref width, ref height);
                if (SettingButton(ref y, cw, "display", "DISPLAY MODE", mode.ToString())) mode = NextDisplayMode(mode);
                vsync = ui.Toggle(new Rect(0, y, cw, 36), "settings.vsync", "V-SYNC", vsync); y += 46;
                if (SettingButton(ref y, cw, "cap", "FRAME RATE LIMIT", fps == 0 ? "Uncapped" : fps + " FPS")) fps = NextFpsCap(fps);
                ui.Label(new Rect(0, y + 10, cw, 56), "Display changes must be kept within 15 seconds. Otherwise your previous display is restored.", 16, TacticalUi.Muted);
            }
            else if (settingsTab == 2)
            {
                ui.Section(new Rect(0, y, cw, 18), "LOOK AND AIM");
                y += 28;
                fov = SettingsSlider(ref y, cw, "Vertical field of view", fov, GameContractVersion.MinHipFovDegrees, GameContractVersion.MaxHipFovDegrees, fov.ToString("0") + " deg");
                sens = SettingsSlider(ref y, cw, "Mouse sensitivity", sens, GameContractVersion.MinMouseSensitivity, GameContractVersion.MaxMouseSensitivity, sens.ToString("0.00") + " deg / unit");
                invertY = ui.Toggle(new Rect(0, y, cw, 36), "settings.invert", "INVERT VERTICAL LOOK", invertY); y += 48;
                ui.Section(new Rect(0, y, cw, 18), "BINDINGS / ESCAPE IS RESERVED FOR PAUSE"); y += 28;
                DrawBindingRows(1, ref y, cw);
            }
            else
            {
                ui.Section(new Rect(0, y, cw, 18), "READABILITY");
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

            float by = a.yMax - 72;
            float bw = (a.width - 24) / 4;
            if (settingsApplier != null && settingsApplier.HasPendingDisplayChange)
            {
                ui.Label(new Rect(a.x, by - 28, a.width, 24), "KEEP THIS DISPLAY?   " + displayClock.RemainingSeconds.ToString("0") + " seconds remaining", 16, TacticalUi.Accent);
                if (ui.Button(new Rect(a.x, by, bw, 38), "settings.keep", "KEEP", primary: true)) ConfirmPendingDisplay();
                if (ui.Button(new Rect(a.x + bw + 8, by, bw, 38), "settings.revert", "REVERT")) RevertPendingDisplay();
            }
            else
            {
                GUI.enabled = !bindingEditor.IsListening;
                if (ui.Button(new Rect(a.x, by, bw, 38), "settings.apply", "APPLY", primary: true)) ApplySettingsDraft();
                GUI.enabled = true;
                if (ui.Button(new Rect(a.x + bw + 8, by, bw, 38), "settings.cancel", "CANCEL")) CancelSettingsAndClose();
                if (ui.Button(new Rect(a.x + (bw + 8) * 2, by, bw, 38), "settings.defaults", "DEFAULTS"))
                {
                    settingsEditor.RestoreDefaults(); bindingEditor.RestoreDefaults(); settingsStatus = "Defaults staged. Apply to save.";
                }
                if (ui.Button(new Rect(a.x + (bw + 8) * 3, by, bw, 38), "settings.back", "BACK")) CancelSettingsAndClose();
            }
            ui.Label(new Rect(a.x, a.yMax - 26, a.width, 24), string.IsNullOrEmpty(settingsStatus)
                ? (settingsEditor.IsDirty ? "Unsaved changes. Apply to save." : "Preferences are saved on this PC.") : settingsStatus, 14, TacticalUi.Muted);
        }

        private float SettingsSlider(ref float y, float width, string label, float value, float min, float max, string formatted)
        {
            float next = ui.Slider(new Rect(0, y, width, 26), "settings.slider." + label, label, value, min, max, formatted);
            y += 66;
            return next;
        }

        private bool SettingButton(ref float y, float width, string id, string label, string value)
        {
            bool next = ui.Button(new Rect(0, y, width, 36), "settings." + id, label + "   /   " + value);
            y += 46;
            return next;
        }

        private void DrawBindingRows(float s, ref float y, float width)
        {
            if (bindingEditor == null) return;
            if (bindingEditor.HasConflict)
            {
                ui.Label(new Rect(0, y, width, 50), "Already assigned to " + bindingEditor.ConflictAction + ". Swap or cancel?", 18, TacticalUi.Accent); y += 54;
                if (ui.Button(new Rect(0, y, 160, 36), "binding.swap", "SWAP")) bindingEditor.TrySwapConflict();
                if (ui.Button(new Rect(170, y, 160, 36), "binding.cancel", "CANCEL")) bindingEditor.TryCancelConflict();
                y += 46;
                return;
            }
            if (bindingEditor.IsListening)
            {
                ui.Label(new Rect(0, y, width, 46), "Press a key or mouse button for " + bindingEditor.ListeningAction + ". Escape cancels.", 18, TacticalUi.Accent); y += 54;
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
                string action = binding.Action == "Slide" ? "Slide (hold Sprint)"
                    : binding.Action == "LastWeapon" ? "Last weapon"
                    : binding.Action == "Slot3" ? "Knife"
                    : binding.Action == "Slot4" ? "Grenade"
                    : binding.Action == "Pickup" ? "Pick up weapon"
                    : binding.Action;
                bool pause = string.Equals(binding.Action, "Pause", StringComparison.Ordinal);
                ui.Label(new Rect(0, y, width * .5f, 34), action, 18, TacticalUi.Text, TextAnchor.MiddleLeft);
                if (pause) ui.Label(new Rect(width * .5f, y, width * .5f, 34), "Escape / reserved", 18, TacticalUi.Muted, TextAnchor.MiddleLeft);
                else if (ui.Button(new Rect(width * .5f, y, width * .5f, 34), "binding." + binding.Action,
                    bindingEditor.IsListening && bindingEditor.ListeningAction == binding.Action ? "LISTENING..." : ""))
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
