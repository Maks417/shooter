#nullable enable
using System;
using System.Threading;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Scoreboard;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// T059 product shell: owns menu → load → play → pause → results lifecycle.
    /// Presentation binds/clears through ProductSessionView; pause goes through <see cref="IProductShellBackend"/>.
    /// </summary>
    public sealed class ProductShellHost : MonoBehaviour
    {
        private IProductShellBackend? backend;
        private OfflineGameSession? session;
        private IMapCatalog? catalog;
        private ProductSessionView? view;
        private SetupSelection? setup;
        private readonly ShellNavigation navigation = new ShellNavigation();
        private readonly ShellSessionCoordinator coordinator = new ShellSessionCoordinator();
        private readonly ScoreboardTable scoreboard = new ScoreboardTable();
        private CancellationTokenSource? loadCancel;
        private bool scoreboardHeld;
        private bool quitRequested;
        private float uiScale = 1f;
        private IUserSettingsStore? settingsStore;
        private UserSettingsEditor? settingsEditor;
        private IClientSettingsApplier? settingsApplier;
        private readonly DisplayChangeConfirmClock displayClock = new DisplayChangeConfirmClock();
        private string settingsStatus = "";
        private Vector2 settingsScroll;

        public ShellNavigation Navigation => navigation;
        public ShellSessionCoordinator Coordinator => coordinator;
        public ProductSessionView? View => view;

        public static ProductShellHost Create(
            IProductShellBackend backend,
            string preferredMapId,
            byte preferredTeamSize)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            var host = new GameObject("Product shell").AddComponent<ProductShellHost>();
            host.Initialize(backend, preferredMapId, preferredTeamSize);
            return host;
        }

        private void Initialize(IProductShellBackend backend, string preferredMapId, byte preferredTeamSize)
        {
            this.backend = backend;
            session = backend.Session ?? throw new InvalidOperationException("Product session missing.");
            catalog = backend.Catalog ?? throw new InvalidOperationException("Product catalog missing.");

            if (preferredTeamSize != 5 && preferredTeamSize != 6) preferredTeamSize = 5;
            MapId preferredMap;
            try { preferredMap = new MapId(string.IsNullOrEmpty(preferredMapId) ? WellKnownMaps.TrainingCompound.Value : preferredMapId); }
            catch { preferredMap = WellKnownMaps.TrainingCompound; }

            setup = new SetupSelection(catalog, preferredMap, preferredTeamSize);
            view = ProductSessionView.Create(session, shellOwned: true);
            view.SetGameplayFocus(false);
            backend.InstallHumanSampler(tick =>
            {
                if (!navigation.AllowsGameplayInput || view == null) return null;
                return view.SampleHuman(tick);
            });

            string path = System.IO.Path.Combine(Application.persistentDataPath, "shooter-user-settings.json");
            settingsStore = new FileUserSettingsStore(path);
            var load = settingsStore.Load(out var loaded);
            settingsEditor = new UserSettingsEditor(loaded);
            settingsApplier = new ProductClientSettingsApplier(view);
            var bootApply = settingsApplier.Apply(loaded);
            if (!bootApply.Succeeded)
                settingsStatus = bootApply.UserFacing;
            else if (!load.Succeeded)
                settingsStatus = load.UserFacing;
            else
                settingsStatus = "";
            uiScale = Mathf.Clamp(settingsEditor.Applied.UiScale, GameContractVersion.MinUiScale, GameContractVersion.MaxUiScale);
            view.ApplyUserSettings(settingsEditor.Applied);

            navigation.ResetToMain();
            ApplyCursorForScreen();
            Debug.Log("SHOOTER_SHELL screen=Main matchActive=0 settings=" + path);
        }

        private void Update()
        {
            if (backend == null || session == null || view == null || setup == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    HandleEscape();
                scoreboardHeld = keyboard.tabKey.isPressed && navigation.ScoreboardAllowed;
            }
            else scoreboardHeld = false;

            if (session.Phase == GameMatchPhase.Finished)
            {
                var results = session.TryAcquireResults();
                coordinator.MarkMatchFinished(results);
                navigation.SyncFromAuthority(matchFinished: true, sessionPaused: false);
                view.SetGameplayFocus(false);
            }
            else
            {
                navigation.SyncFromAuthority(
                    matchFinished: false,
                    sessionPaused: session.PauseState == SessionPauseState.Paused);
            }

            ApplyCursorForScreen();
            TickDisplayConfirmClock();

            if (navigation.Screen == ShellScreen.Playing && session.Phase == GameMatchPhase.Playing)
            {
                var snap = session.TryAcquireSnapshot();
                if (snap != null) scoreboard.BindLive(snap, session.HumanEntityId);
            }
            else if (navigation.Screen == ShellScreen.Results && coordinator.RetainedResults != null)
            {
                scoreboard.BindResults(coordinator.RetainedResults);
            }
        }

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

        private void HandleEscape()
        {
            if (session == null || backend == null || view == null) return;
            switch (navigation.Screen)
            {
                case ShellScreen.Playing:
                    if (backend.Pause().Succeeded)
                    {
                        view.NotifyMenuTookFocus();
                        view.PlayUiCue(11UL);
                        navigation.EnterPaused();
                    }
                    break;
                case ShellScreen.Paused:
                    if (backend.Resume().Succeeded)
                    {
                        navigation.ResumePlaying();
                        view.SetGameplayFocus(true);
                        view.PlayUiCue(22UL);
                    }
                    break;
                case ShellScreen.Settings:
                    if (settingsApplier != null && settingsApplier.HasPendingDisplayChange)
                        RevertPendingDisplay();
                    CancelSettingsAndClose();
                    break;
                case ShellScreen.ConfirmLeave:
                case ShellScreen.ConfirmRestart:
                    navigation.CancelConfirm();
                    break;
                case ShellScreen.Setup:
                    navigation.ResetToMain();
                    break;
                case ShellScreen.Loading:
                    CancelLoading();
                    break;
            }
            ApplyCursorForScreen();
        }

        private void ResumeMatch()
        {
            if (backend == null || view == null) return;
            if (backend.Resume().Succeeded)
            {
                navigation.ResumePlaying();
                view.SetGameplayFocus(true);
            }
        }

        private async void StartSelectedMatch()
        {
            if (session == null || catalog == null || setup == null || view == null || backend == null) return;
            var request = setup.TryBuildRequest(out string error);
            if (request == null)
            {
                navigation.FailLoad(error);
                return;
            }
            if (!coordinator.TryBeginStart(request, out error))
            {
                navigation.FailLoad(error);
                return;
            }

            PlayerPrefs.SetString("shooter.map", request.MapId.Value);
            PlayerPrefs.SetInt("shooter.teamSize", request.TeamSize);
            PlayerPrefs.Save();

            loadCancel?.Cancel();
            loadCancel?.Dispose();
            loadCancel = new CancellationTokenSource();
            ulong epoch = coordinator.LoadEpoch;
            navigation.BeginLoading("Loading " + request.MapId.Value + "…");
            ApplyCursorForScreen();
            view.ClearPresentation();

            GameOperationResult load;
            try
            {
                load = await session.BeginLoadAsync(request, loadCancel.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                if (!coordinator.SameLoadEpoch(epoch)) return;
                coordinator.CancelStart();
                navigation.CancelLoad();
                return;
            }
            catch (Exception ex)
            {
                if (!coordinator.SameLoadEpoch(epoch)) return;
                coordinator.CompleteStartFailure();
                navigation.FailLoad(ex.Message);
                return;
            }

            if (!coordinator.SameLoadEpoch(epoch)) return;
            if (loadCancel.IsCancellationRequested || !load.Succeeded)
            {
                coordinator.CompleteStartFailure();
                if (loadCancel.IsCancellationRequested) navigation.CancelLoad();
                else navigation.FailLoad(string.IsNullOrEmpty(load.UserFacing) ? load.Diagnostic : load.UserFacing);
                return;
            }

            navigation.SetLoadingPhase("Binding presentation…");
            backend.WireMapActors();
            if (!backend.TryGetVerifiedMap(out var boxes, out var metadata, out var mapId))
            {
                session.RequestAbort();
                coordinator.CompleteStartFailure();
                navigation.FailLoad("Verified map package solids missing.");
                return;
            }
            if (!backend.TryGetVerifiedVisuals(out var visuals, out string visualDiagnostic))
            {
                session.RequestAbort();
                coordinator.CompleteStartFailure();
                navigation.FailLoad("Verified visual package missing: " + visualDiagnostic + ". Repair required; no generation fallback.");
                return;
            }

            try
            {
                view.BindVerifiedMap(boxes, metadata, mapId, visuals);
            }
            catch (Exception ex)
            {
                session.RequestAbort();
                coordinator.CompleteStartFailure();
                navigation.FailLoad(ex.Message);
                return;
            }

            var ready = session.SignalReady();
            if (!ready.Succeeded)
            {
                session.RequestAbort();
                coordinator.CompleteStartFailure();
                navigation.FailLoad(ready.UserFacing);
                return;
            }

            var started = session.Start();
            if (!started.Succeeded)
            {
                session.RequestAbort();
                coordinator.CompleteStartFailure();
                navigation.FailLoad(started.UserFacing);
                return;
            }

            coordinator.CompleteStartSuccess();
            navigation.EnterPlaying();
            view.SetGameplayFocus(true);
            ApplyCursorForScreen();
            Debug.Log("SHOOTER_SHELL screen=Playing map=" + request.MapId.Value + " teamSize=" + request.TeamSize +
                      " session=" + session.AuthoritySessionId);
        }

        private void CancelLoading()
        {
            if (loadCancel == null || !coordinator.StartInFlight) return;
            loadCancel.Cancel();
            coordinator.CancelStart();
            session?.RequestAbort();
            view?.ClearPresentation();
            navigation.CancelLoad();
            ApplyCursorForScreen();
        }

        private void LeaveMatch(bool abort)
        {
            if (session == null || view == null || backend == null) return;
            if (session.PauseState == SessionPauseState.Paused)
                backend.Resume();

            if (abort && session.Phase == GameMatchPhase.Playing)
                session.RequestAbort();
            else
                session.Stop();

            view.ClearPresentation();
            view.SetGameplayFocus(false);
            coordinator.LeaveToMenu();
            navigation.ResetToMain(abort ? "Returned to menu." : "");
            ApplyCursorForScreen();
        }

        private void RestartMatch()
        {
            coordinator.PrepareReplay(out var request, out string error);
            if (request == null)
            {
                navigation.FailLoad(error);
                return;
            }
            LeaveMatch(abort: session?.Phase == GameMatchPhase.Playing);
            if (setup != null && catalog != null)
                setup = new SetupSelection(catalog, request.MapId, request.TeamSize);
            StartSelectedMatch();
        }

        private void ApplyCursorForScreen()
        {
            bool gameplay = navigation.AllowsGameplayInput;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
            view?.SetGameplayFocus(gameplay);
        }

        private void OnGUI()
        {
            if (setup == null) return;
            float s = uiScale;
            switch (navigation.Screen)
            {
                case ShellScreen.Main:
                    DrawMain(s);
                    break;
                case ShellScreen.Setup:
                    DrawSetup(s);
                    break;
                case ShellScreen.Loading:
                    DrawLoading(s);
                    break;
                case ShellScreen.Paused:
                    DrawPause(s);
                    break;
                case ShellScreen.ConfirmLeave:
                    DrawConfirm(s, "Leave match? Progress will be lost.", ConfirmLeave, () => navigation.CancelConfirm());
                    break;
                case ShellScreen.ConfirmRestart:
                    DrawConfirm(s, "Restart match? Progress will be lost.", () => RestartMatch(), () => navigation.CancelConfirm());
                    break;
                case ShellScreen.Settings:
                    DrawSettings(s);
                    break;
                case ShellScreen.Results:
                    DrawResults(s);
                    break;
                case ShellScreen.Playing:
                    DrawPlayingHud(s);
                    if (scoreboardHeld) DrawScoreboard(s);
                    break;
            }
        }

        private void DrawMain(float s)
        {
            GUI.Box(new Rect(40 * s, 40 * s, 520 * s, 280 * s), "Shooter — Offline Team Deathmatch");
            if (GUI.Button(new Rect(60 * s, 100 * s, 200 * s, 36 * s), "Play"))
                navigation.OpenSetup();
            if (GUI.Button(new Rect(60 * s, 150 * s, 200 * s, 36 * s), "Settings"))
                navigation.OpenSettings();
            if (GUI.Button(new Rect(60 * s, 200 * s, 200 * s, 36 * s), "Quit"))
            {
                quitRequested = true;
                Application.Quit();
            }
            if (!string.IsNullOrEmpty(navigation.StatusMessage))
                GUI.Label(new Rect(60 * s, 250 * s, 480 * s, 40 * s), navigation.StatusMessage);
        }

        private void DrawSetup(float s)
        {
            GUI.Box(new Rect(40 * s, 40 * s, 640 * s, 360 * s), "Match setup");
            GUI.Label(new Rect(60 * s, 80 * s, 580 * s, 24 * s), "Map: " + setup!.SelectedDisplayName);
            GUI.Label(new Rect(60 * s, 104 * s, 580 * s, 40 * s), setup.SelectedDescription);
            if (GUI.Button(new Rect(60 * s, 150 * s, 140 * s, 28 * s), "Prev map")) setup.CycleMap(-1);
            if (GUI.Button(new Rect(220 * s, 150 * s, 140 * s, 28 * s), "Next map")) setup.CycleMap(1);
            GUI.Label(new Rect(60 * s, 200 * s, 480 * s, 24 * s),
                "Mode: Team Deathmatch   Size: " + setup.SelectedTeamSize + "v" + setup.SelectedTeamSize);
            if (GUI.Button(new Rect(60 * s, 230 * s, 140 * s, 28 * s), "Cycle size")) setup.CycleTeamSize(1);
            GUI.Label(new Rect(60 * s, 270 * s, 480 * s, 24 * s), "150 kills / 15 minutes");
            GUI.enabled = !coordinator.StartInFlight;
            if (GUI.Button(new Rect(60 * s, 310 * s, 200 * s, 36 * s), "Start match"))
                StartSelectedMatch();
            GUI.enabled = true;
            if (GUI.Button(new Rect(280 * s, 310 * s, 140 * s, 36 * s), "Back"))
                navigation.ResetToMain();
            if (!string.IsNullOrEmpty(navigation.StatusMessage))
                GUI.Label(new Rect(60 * s, 355 * s, 580 * s, 30 * s), navigation.StatusMessage);
        }

        private void DrawLoading(float s)
        {
            GUI.Box(new Rect(40 * s, 40 * s, 520 * s, 180 * s), "Loading");
            GUI.Label(new Rect(60 * s, 90 * s, 480 * s, 40 * s), navigation.StatusMessage);
            if (GUI.Button(new Rect(60 * s, 140 * s, 160 * s, 36 * s), "Cancel"))
                CancelLoading();
        }

        private void DrawPause(float s)
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 160 * s, Screen.height * 0.5f - 140 * s, 320 * s, 280 * s), "Paused");
            float x = Screen.width * 0.5f - 100 * s;
            float y = Screen.height * 0.5f - 90 * s;
            if (GUI.Button(new Rect(x, y, 200 * s, 36 * s), "Resume")) ResumeMatch();
            if (GUI.Button(new Rect(x, y + 44 * s, 200 * s, 36 * s), "Settings")) navigation.OpenSettings();
            if (GUI.Button(new Rect(x, y + 88 * s, 200 * s, 36 * s), "Restart match")) navigation.AskRestart();
            if (GUI.Button(new Rect(x, y + 132 * s, 200 * s, 36 * s), "Main menu")) navigation.AskLeave();
        }

        private void DrawConfirm(float s, string message, Action confirm, Action cancel)
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 220 * s, Screen.height * 0.5f - 90 * s, 440 * s, 180 * s), "Confirm");
            GUI.Label(new Rect(Screen.width * 0.5f - 200 * s, Screen.height * 0.5f - 40 * s, 400 * s, 40 * s), message);
            if (GUI.Button(new Rect(Screen.width * 0.5f - 200 * s, Screen.height * 0.5f + 20 * s, 160 * s, 36 * s), "Confirm"))
                confirm();
            if (GUI.Button(new Rect(Screen.width * 0.5f + 20 * s, Screen.height * 0.5f + 20 * s, 160 * s, 36 * s), "Cancel"))
                cancel();
        }

        private void ConfirmLeave() => LeaveMatch(abort: true);

        private void CancelSettingsAndClose()
        {
            settingsEditor?.Cancel();
            navigation.CloseSettings();
            settingsStatus = "";
        }

        private void SyncUiScaleFromApplied()
        {
            if (settingsEditor == null) return;
            uiScale = Mathf.Clamp(settingsEditor.Applied.UiScale, GameContractVersion.MinUiScale, GameContractVersion.MaxUiScale);
            view?.ApplyUserSettings(settingsEditor.Applied);
        }

        private void ApplySettingsDraft()
        {
            if (settingsEditor == null || settingsStore == null || settingsApplier == null) return;
            var result = settingsEditor.Apply(settingsStore, settingsApplier);
            SyncUiScaleFromApplied();
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
            if (settingsEditor == null || settingsApplier == null) return;
            var result = settingsEditor.ConfirmDisplay(settingsApplier);
            displayClock.Clear();
            settingsStatus = result.Succeeded ? "Display kept." : result.UserFacing;
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
            if (settingsEditor == null) return;
            var draft = settingsEditor.Draft;
            GUI.Box(new Rect(40 * s, 40 * s, 720 * s, 560 * s), "Settings");
            settingsScroll = GUI.BeginScrollView(
                new Rect(50 * s, 70 * s, 700 * s, 400 * s),
                settingsScroll,
                new Rect(0, 0, 660 * s, 520 * s));

            float y = 0;
            GUI.Label(new Rect(10 * s, y, 640 * s, 22 * s), "Audio (0 = mute)");
            y += 26 * s;
            draft = WithAudio(
                draft,
                SliderRow(s, ref y, "Master", draft.MasterVolume),
                SliderRow(s, ref y, "Effects", draft.EffectsVolume),
                SliderRow(s, ref y, "UI", draft.UiVolume));

            GUI.Label(new Rect(10 * s, y, 640 * s, 22 * s), "Display / quality");
            y += 26 * s;
            byte quality = draft.QualityTier;
            if (GUI.Button(new Rect(10 * s, y, 200 * s, 28 * s), "Quality: " + QualityLabel(quality)))
                quality = (byte)((quality + 1) % 3);
            y += 34 * s;
            int width = draft.ResolutionWidth;
            int height = draft.ResolutionHeight;
            if (GUI.Button(new Rect(10 * s, y, 280 * s, 28 * s), "Resolution: " + width + "x" + height))
                CycleResolution(ref width, ref height);
            y += 34 * s;
            var mode = draft.DisplayMode;
            if (GUI.Button(new Rect(10 * s, y, 220 * s, 28 * s), "Mode: " + mode))
                mode = NextDisplayMode(mode);
            y += 34 * s;
            bool vsync = draft.VSync;
            vsync = GUI.Toggle(new Rect(10 * s, y, 200 * s, 24 * s), vsync, "VSync");
            y += 28 * s;
            int fps = draft.FpsCap;
            if (GUI.Button(new Rect(10 * s, y, 200 * s, 28 * s), "FPS cap: " + (fps == 0 ? "uncapped" : fps.ToString())))
                fps = NextFpsCap(fps);
            y += 34 * s;

            GUI.Label(new Rect(10 * s, y, 640 * s, 22 * s), "Controls / accessibility");
            y += 26 * s;
            float fov = SliderRow(s, ref y, "Hip FOV", draft.HipFovDegrees, GameContractVersion.MinHipFovDegrees, GameContractVersion.MaxHipFovDegrees);
            float sens = SliderRow(s, ref y, "Sensitivity", draft.MouseSensitivity, GameContractVersion.MinMouseSensitivity, GameContractVersion.MaxMouseSensitivity);
            bool invertY = GUI.Toggle(new Rect(10 * s, y, 200 * s, 24 * s), draft.InvertY, "Invert Y");
            y += 28 * s;
            float ui = SliderRow(s, ref y, "UI scale", draft.UiScale, GameContractVersion.MinUiScale, GameContractVersion.MaxUiScale);
            bool reducedMotion = GUI.Toggle(new Rect(10 * s, y, 260 * s, 24 * s), draft.ReducedCameraMotion, "Reduced camera motion");
            y += 28 * s;
            bool reducedFlash = GUI.Toggle(new Rect(10 * s, y, 200 * s, 24 * s), draft.ReducedFlash, "Reduced flash");
            y += 28 * s;
            float contrast = SliderRow(s, ref y, "Contrast", draft.Contrast, GameContractVersion.MinContrast, GameContractVersion.MaxContrast);
            bool subtitles = GUI.Toggle(new Rect(10 * s, y, 200 * s, 24 * s), draft.Subtitles, "Subtitles");
            y += 28 * s;
            GUI.Label(new Rect(10 * s, y, 640 * s, 40 * s),
                "Bindings: " + draft.Bindings.Count + " actions (defaults recoverable; reserved Escape).");

            settingsEditor.SetDraft(new UserSettings(
                draft.Schema,
                draft.MasterVolume,
                draft.EffectsVolume,
                draft.UiVolume,
                quality,
                width,
                height,
                mode,
                vsync,
                fps,
                fov,
                sens,
                invertY,
                ui,
                reducedMotion,
                reducedFlash,
                contrast,
                subtitles,
                CopyBindings(draft)));

            GUI.EndScrollView();

            if (settingsApplier != null && settingsApplier.HasPendingDisplayChange)
            {
                GUI.Label(new Rect(60 * s, 480 * s, 500 * s, 24 * s),
                    "Confirm display (" + displayClock.RemainingSeconds.ToString("0.0") + "s remaining)");
                if (GUI.Button(new Rect(60 * s, 508 * s, 120 * s, 36 * s), "Keep"))
                    ConfirmPendingDisplay();
                if (GUI.Button(new Rect(200 * s, 508 * s, 120 * s, 36 * s), "Revert"))
                    RevertPendingDisplay();
            }
            else
            {
                if (GUI.Button(new Rect(60 * s, 480 * s, 120 * s, 36 * s), "Apply"))
                    ApplySettingsDraft();
                if (GUI.Button(new Rect(200 * s, 480 * s, 120 * s, 36 * s), "Cancel"))
                    CancelSettingsAndClose();
                if (GUI.Button(new Rect(340 * s, 480 * s, 180 * s, 36 * s), "Restore defaults"))
                {
                    settingsEditor.RestoreDefaults();
                    settingsStatus = "Defaults staged — Apply to save.";
                }
                if (GUI.Button(new Rect(540 * s, 480 * s, 120 * s, 36 * s), "Back"))
                    CancelSettingsAndClose();
            }

            if (!string.IsNullOrEmpty(settingsStatus))
                GUI.Label(new Rect(60 * s, 552 * s, 680 * s, 24 * s), settingsStatus);
            else if (settingsEditor.IsDirty)
                GUI.Label(new Rect(60 * s, 552 * s, 680 * s, 24 * s), "Unsaved draft changes.");
        }

        private static float SliderRow(float s, ref float y, string label, float value, float min = 0f, float max = 1f)
        {
            GUI.Label(new Rect(10 * s, y, 140 * s, 22 * s), label + " " + value.ToString("0.00"));
            float next = GUI.HorizontalSlider(new Rect(160 * s, y + 4 * s, 420 * s, 20 * s), value, min, max);
            y += 28 * s;
            return next;
        }

        private static UserSettings WithAudio(UserSettings source, float master, float effects, float ui) =>
            new UserSettings(
                source.Schema, master, effects, ui, source.QualityTier,
                source.ResolutionWidth, source.ResolutionHeight, source.DisplayMode, source.VSync, source.FpsCap,
                source.HipFovDegrees, source.MouseSensitivity, source.InvertY, source.UiScale,
                source.ReducedCameraMotion, source.ReducedFlash, source.Contrast, source.Subtitles,
                CopyBindings(source));

        private static InputBinding[] CopyBindings(UserSettings source)
        {
            var copy = new InputBinding[source.Bindings.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source.Bindings[i];
            return copy;
        }

        private static string QualityLabel(byte tier) => tier switch { 0 => "Low", 1 => "Balanced", _ => "High" };

        private static void CycleResolution(ref int width, ref int height)
        {
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

        private void DrawPlayingHud(float s)
        {
            if (session == null) return;
            var snap = session.TryAcquireSnapshot();
            string line = "TDM";
            if (snap != null)
                line = "T1 " + snap.Team1Kills + "  T2 " + snap.Team2Kills + "  rem=" + snap.RemainingActiveTicks;
            GUI.Label(new Rect(24 * s, 24 * s, 640 * s, 24 * s), line);
            GUI.Label(new Rect(24 * s, 48 * s, 640 * s, 24 * s), "Tab scoreboard · Esc pause");
        }

        private void DrawResults(float s)
        {
            var results = coordinator.RetainedResults;
            GUI.Box(new Rect(40 * s, 40 * s, 720 * s, 520 * s), "Match results");
            if (results == null)
            {
                GUI.Label(new Rect(60 * s, 90 * s, 600 * s, 24 * s), "No retained results.");
            }
            else
            {
                scoreboard.BindResults(results);
                string outcome = scoreboard.LocalOutcomeLabel(1);
                GUI.Label(new Rect(60 * s, 80 * s, 680 * s, 24 * s),
                    outcome + " — " + results.Reason + "  " + results.Team1Kills + "-" + results.Team2Kills +
                    "  map=" + results.MapId.Value);
                DrawScoreboardRows(60 * s, 120 * s, 680 * s, s);
            }
            if (GUI.Button(new Rect(60 * s, 500 * s, 180 * s, 36 * s), "Play again"))
                RestartMatch();
            if (GUI.Button(new Rect(260 * s, 500 * s, 180 * s, 36 * s), "Main menu"))
                LeaveMatch(abort: false);
        }

        private void DrawScoreboard(float s)
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 360 * s, 80 * s, 720 * s, 520 * s), "Live scoreboard");
            GUI.Label(new Rect(Screen.width * 0.5f - 340 * s, 110 * s, 680 * s, 24 * s),
                "T1 " + scoreboard.Team1Kills + "  T2 " + scoreboard.Team2Kills);
            DrawScoreboardRows(Screen.width * 0.5f - 340 * s, 150 * s, 680 * s, s);
        }

        private void DrawScoreboardRows(float x, float y, float width, float s)
        {
            GUI.Label(new Rect(x, y, width, 22 * s), "Team  Name            Role    K   D");
            for (int i = 0; i < scoreboard.RowCount; i++)
            {
                var row = scoreboard.Rows[i];
                string marker = row.IsLocal ? ">" : " ";
                string line = marker + " T" + row.Team + "  " + Pad(row.DisplayName, 14) + "  " +
                              Pad(row.RoleLabel, 6) + "  " + row.Kills + "  " + row.Deaths;
                GUI.Label(new Rect(x, y + (24 + i * 28) * s, width, 24 * s), line);
            }
        }

        private static string Pad(string value, int width)
        {
            if (value.Length >= width) return value.Substring(0, width);
            return value.PadRight(width);
        }

        private void OnDestroy()
        {
            loadCancel?.Cancel();
            loadCancel?.Dispose();
            _ = quitRequested;
        }
    }
}
