#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Presentation.Input;
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
        private ProductClientSettingsApplier? productSettingsApplier;
        private InputBindingEditor? bindingEditor;
        private readonly DisplayChangeConfirmClock displayClock = new DisplayChangeConfirmClock();
        private string settingsStatus = "";
        private Vector2 settingsScroll;
        private readonly TacticalUi ui = new TacticalUi();
        private int settingsTab;
        private int bindingListenAfterFrame;
        private int lastEscapeFrame = -1;
        private ProductMenuScene? menuScene;

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
            if (backend is IProductMenuVisualSource menuVisuals) menuScene = ProductMenuScene.Create(transform, menuVisuals);
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
            productSettingsApplier = new ProductClientSettingsApplier(view);
            settingsApplier = productSettingsApplier;
            bindingEditor = new InputBindingEditor(loaded.Bindings);
            var bootApply = settingsApplier.Apply(loaded);
            if (!bootApply.Succeeded)
                settingsStatus = bootApply.UserFacing;
            else if (!load.Succeeded)
                settingsStatus = load.UserFacing;
            else
                settingsStatus = "";
            uiScale = Mathf.Clamp(settingsEditor.Applied.UiScale, GameContractVersion.MinUiScale, GameContractVersion.MaxUiScale);
            menuScene?.SetReducedMotion(settingsEditor.Applied.ReducedCameraMotion);
            view.ApplyUserSettings(settingsEditor.Applied);

            navigation.ResetToMain();
            ApplyCursorForScreen();
            Debug.Log("SHOOTER_SHELL screen=Main matchActive=0 settings=" + path);
        }

        private void Update()
        {
            if (backend == null || session == null || view == null || setup == null) return;

            // Reserved Escape always restores menu access (Pause is schema-locked to Escape).
            if (BoundControlMap.WasControlPressedThisFrame(UserSettings.ReservedEscapeControl))
                HandleEscapeOnce();

            scoreboardHeld = navigation.ScoreboardAllowed
                && view != null
                && view.Input.ScoreboardHeld;

            if (session.TerminalFault != null
                && (navigation.Screen == ShellScreen.Playing || navigation.Screen == ShellScreen.Paused))
            {
                // Recoverable terminal fault (for example SpawnFailure): return to the menu with the message.
                string message = session.TerminalFault.UserFacing;
                LeaveMatch(abort: false);
                navigation.ResetToMain(message);
                ApplyCursorForScreen();
                return;
            }

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
                    if (bindingEditor != null && bindingEditor.IsListening)
                    {
                        bindingEditor.CancelListen();
                        settingsStatus = "Rebind cancelled.";
                        break;
                    }
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

        private void HandleEscapeOnce()
        {
            if (lastEscapeFrame == Time.frameCount) return;
            lastEscapeFrame = Time.frameCount;
            HandleEscape();
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
            navigation.BeginLoading(InsertionScreenCopy.LoadingPhase(request.MapId.Value));
            ApplyCursorForScreen();
            view.ClearPresentation();
            // Let OnGUI paint the insertion screen before synchronous package/nav work.
            await AwaitLoadingPaintAsync().ConfigureAwait(true);
            if (!coordinator.SameLoadEpoch(epoch)) return;
            if (loadCancel.IsCancellationRequested)
            {
                coordinator.CancelStart();
                navigation.CancelLoad();
                return;
            }

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

            navigation.SetLoadingPhase(InsertionScreenCopy.BindingPresentation);
            await AwaitLoadingPaintAsync().ConfigureAwait(true);
            if (!coordinator.SameLoadEpoch(epoch)) return;
            if (loadCancel.IsCancellationRequested)
            {
                coordinator.CompleteStartFailure();
                navigation.CancelLoad();
                return;
            }

            backend.WireMapActors();
            if (!backend.TryGetVerifiedMap(out var boxes, out var metadata, out var mapId))
            {
                session.RequestAbort();
                coordinator.CompleteStartFailure();
                navigation.FailLoad("Map package is incomplete. Repair or reinstall the game.");
                return;
            }
            if (!backend.TryGetVerifiedVisuals(out var visuals, out string visualDiagnostic))
            {
                session.RequestAbort();
                coordinator.CompleteStartFailure();
                navigation.FailLoad("Visual package missing (" + visualDiagnostic + "). Repair or reinstall — no substitute map will load.");
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

            session.PreMatchCountdownTicks = SessionDefaults.PreMatchCountdownTicks;
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
            navigation.ResetToMain(abort ? "Back at headquarters." : "");
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
            bool frontEnd = navigation.Screen == ShellScreen.Main || navigation.Screen == ShellScreen.Setup
                || navigation.Screen == ShellScreen.Settings && navigation.SettingsReturn != ShellScreen.Paused;
            if (menuScene != null && menuScene.gameObject.activeSelf != frontEnd) menuScene.gameObject.SetActive(frontEnd);
        }

        private void OnGUI()
        {
            if (setup == null) return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                HandleEscapeOnce();
                Event.current.Use();
            }
            float s = 1f;
            ui.SceneTexture = menuScene != null && menuScene.gameObject.activeInHierarchy ? menuScene.Frame : null;
            ui.Begin(uiScale, navigation.Screen.ToString(), bindingEditor == null || !bindingEditor.IsListening);
            try
            {
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
                    DrawConfirm(s, ShellUiCopy.ConfirmLeave, ConfirmLeave, () => navigation.CancelConfirm());
                    break;
                case ShellScreen.ConfirmRestart:
                    DrawConfirm(s, ShellUiCopy.ConfirmRestart, () => RestartMatch(), () => navigation.CancelConfirm());
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
            finally { ui.End(); }
        }

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
            if (ui.Button(new Rect(a.x, y + 40, navWidth, 36), "main.settings", ShellUiCopy.Settings, quiet: true)) OpenSettingsScreen();
            if (ui.Button(new Rect(a.x, y + 80, navWidth, 36), "main.quit", ShellUiCopy.Quit, quiet: true))
            {
                quitRequested = true;
                Application.Quit();
            }
            float cardsX = a.x + navWidth + 28;
            float cardsWidth = a.xMax - cardsX;
            float cardWidth = (cardsWidth - 14) * .5f;
            float cardHeight = Mathf.Min(198, a.height * .38f);
            float cardsY = a.yMax - cardHeight - 22;
            ui.Section(new Rect(cardsX, cardsY - 22, cardsWidth, 16), ShellUiCopy.MainBattles);
            bool compound = setup!.SelectedMapId.Equals(WellKnownMaps.TrainingCompound);
            if (ui.MapCard(new Rect(cardsX, cardsY, cardWidth, cardHeight), "main.compound", menuScene?.MapCard("training-compound"), ShellUiCopy.MapCompound, ShellUiCopy.MainCardSubtitle, compound))
            {
                if (!compound) setup.CycleMap(1);
                navigation.OpenSetup();
            }
            if (ui.MapCard(new Rect(cardsX + cardWidth + 14, cardsY, cardWidth, cardHeight), "main.depot", menuScene?.MapCard("depot-yard"), ShellUiCopy.MapDepot, ShellUiCopy.MainCardSubtitle, !compound))
            {
                if (compound) setup.CycleMap(1);
                navigation.OpenSetup();
            }
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
            bool compound = setup!.SelectedMapId.Equals(WellKnownMaps.TrainingCompound);
            if (ui.Button(new Rect(a.x, y + 24, left, 40), "setup.compound", ShellUiCopy.SetupCompound, selected: compound) && !compound) setup.CycleMap(1);
            if (ui.Button(new Rect(a.x, y + 70, left, 40), "setup.depot", ShellUiCopy.SetupDepot, selected: !compound) && compound) setup.CycleMap(1);
            float previewH = Mathf.Min(118, a.height * .22f);
            Rect preview = new Rect(a.x, y + 120, left, previewH);
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
            if (ui.Button(new Rect(rx, y + 24, half, 40), "setup.5", "5 v 5", selected: setup.SelectedTeamSize == 5) && setup.SelectedTeamSize != 5) setup.CycleTeamSize(1);
            if (ui.Button(new Rect(rx + half + 8, y + 24, half, 40), "setup.6", "6 v 6", selected: setup.SelectedTeamSize == 6) && setup.SelectedTeamSize != 6) setup.CycleTeamSize(1);
            ui.Panel(new Rect(rx, y + 78, rw, 128), accentTop: true);
            ui.Label(new Rect(rx + 14, y + 90, rw - 28, 22), ShellUiCopy.SetupRules, 12, TacticalUi.Accent);
            ui.Label(new Rect(rx + 14, y + 112, rw - 28, 40), ShellUiCopy.SetupRulesBody, 18);
            ui.Label(new Rect(rx + 14, y + 156, rw - 28, 40), ShellUiCopy.SetupRosterLine(setup.SelectedTeamSize), 16, TacticalUi.Muted);
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
            if (ui.DrawInsertionScreen(
                    mapName,
                    InsertionScreenCopy.MatchTitle,
                    mapName,
                    InsertionScreenCopy.MatchRules(setup.SelectedTeamSize),
                    navigation.StatusMessage,
                    card,
                    showCancel: true,
                    reducedMotion,
                    InsertionScreenCopy.MatchFooter(setup.SelectedTeamSize)))
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

        private void DrawFooter(string text)
        {
            Rect a = ui.Safe;
            TacticalUi.Fill(new Rect(a.x, a.yMax - 24, Mathf.Min(96, a.width * .16f), 1), TacticalUi.Raised);
            TacticalUi.Fill(new Rect(a.x, a.yMax - 24, 16, 1), TacticalUi.Accent);
            ui.Label(new Rect(a.x, a.yMax - 16, a.width, 16), text, 12, TacticalUi.Muted);
        }

        private void ConfirmLeave() => LeaveMatch(abort: true);

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
            string[] tabs = SettingsTabs;
            float tw = (a.width - 18) / 4;
            float tabY = a.y + 74;
            ui.Panel(new Rect(a.x, tabY, a.width, 36));
            for (int i = 0; i < tabs.Length; i++)
                if (ui.Button(new Rect(a.x + i * (tw + 6), tabY + 1, tw, 34), "settings.tab." + i, tabs[i], selected: settingsTab == i, quiet: true))
                {
                    settingsTab = i;
                    settingsScroll = Vector2.zero;
                }

            var draft = settingsEditor.Draft;
            float master = draft.MasterVolume, effects = draft.EffectsVolume, uiVol = draft.UiVolume;
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
                fov, sens, invertY, scale, reducedMotion, reducedFlash, contrast, subtitles, bindingEditor.ToArray()));
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
                    : binding.Action;
                bool pause = string.Equals(binding.Action, "Pause", StringComparison.Ordinal);
                ui.Label(new Rect(0, y, width * .5f, 34), action, 18, TacticalUi.Text, TextAnchor.MiddleLeft);
                if (pause) ui.Label(new Rect(width * .5f, y, width * .5f, 34), "Escape / reserved", 18, TacticalUi.Muted, TextAnchor.MiddleLeft);
                else if (ui.Button(new Rect(width * .5f, y, width * .5f, 34), "binding." + binding.Action,
                    bindingEditor.IsListening && bindingEditor.ListeningAction == binding.Action ? "LISTENING..." : TacticalUi.ControlLabel(binding.Control)))
                {
                    bindingEditor.TryBeginListen(binding.Action);
                    bindingListenAfterFrame = Time.frameCount;
                }
                y += 40;
            }
        }

        private static string QualityLabel(byte tier) => tier switch { 0 => "Low", 1 => "Balanced", _ => "High" };
        private static readonly string[] SettingsTabs = { "AUDIO", "VIDEO", "CONTROLS", "ACCESSIBILITY" };

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

        private void DrawPlayingHud(float s)
        {
            var snap = session?.TryAcquireSnapshot();
            if (snap == null) return;
            // BFV-like top score/clock strip; combat compass sits just below (ProductSessionView).
            float w = 248, h = 26, x = (ui.Width - w) * .5f, y = ui.Safe.y + 8;
            TacticalUi.CombatPlate(new Rect(x, y, w, h), strong: true);
            TacticalUi.Fill(new Rect(x, y, 2, h), TacticalUi.Ally);
            TacticalUi.Fill(new Rect(x + w - 2, y, 2, h), TacticalUi.Enemy);
            TacticalUi.Fill(new Rect(x + 2, y + h - 1, w - 4, 1), new Color(1f, 1f, 1f, .06f));
            ui.Label(new Rect(x + 10, y + 1, 54, 24), snap.Team1Kills.ToString("000"), 20, TacticalUi.Ally, TextAnchor.MiddleLeft);
            ui.Label(new Rect(x + 64, y + 1, w - 128, 24), TacticalUi.Clock(snap.RemainingActiveTicks), 16, TacticalUi.Text, TextAnchor.MiddleCenter);
            ui.Label(new Rect(x + w - 64, y + 1, 54, 24), snap.Team2Kills.ToString("000"), 20, TacticalUi.Enemy, TextAnchor.MiddleRight);
            float mid = x + w * .5f;
            float allyBar = (w * .5f - 6) * Mathf.Clamp01(snap.Team1Kills / 150f);
            float enemyBar = (w * .5f - 6) * Mathf.Clamp01(snap.Team2Kills / 150f);
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
            ui.Header(ShellUiCopy.EyebrowDebrief, scoreboard.LocalOutcomeLabel(1).ToUpperInvariant(), ShellUiCopy.TeamDeathmatch);
            DrawTeamTables(ui.Safe.y + 78, ui.Safe.height - 150);
            float y = ui.Safe.yMax - 52;
            if (ui.Button(new Rect(ui.Safe.x, y, 180, 40), "results.replay", ShellUiCopy.ResultsPlayAgain, primary: true)) RestartMatch();
            if (ui.Button(new Rect(ui.Safe.x + 192, y, 180, 40), "results.menu", ShellUiCopy.ResultsMain)) LeaveMatch(abort: false);
            ui.Label(new Rect(ui.Safe.x + 392, y, ui.Safe.width - 392, 40), results.Reason == FinishReason.ScoreLimit ? "SCORE LIMIT REACHED" : "TIME LIMIT REACHED", 12, TacticalUi.Muted, TextAnchor.MiddleRight);
        }

        private void DrawScoreboard(float s)
        {
            ui.Backdrop(overlay: true);
            ui.Header(ShellUiCopy.EyebrowRoster, ShellUiCopy.ScoreboardTitle, ShellUiCopy.TeamDeathmatch);
            DrawTeamTables(ui.Safe.y + 78, ui.Safe.height - 112);
            DrawFooter("TEAM 1 / ALLIES     TEAM 2 / ENEMIES     YOU / LOCAL OPERATOR");
        }

        private void DrawTeamTables(float y, float height)
        {
            Rect a = ui.Safe;
            float w = (a.width - 16) * .5f;
            for (byte team = 1; team <= 2; team++)
            {
                float x = a.x + (team - 1) * (w + 16);
                Color color = team == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                ui.Panel(new Rect(x, y, w, height - 8));
                TacticalUi.Fill(new Rect(x, y, w, 2), color);
                uint kills = team == 1 ? scoreboard.Team1Kills : scoreboard.Team2Kills;
                ui.Label(new Rect(x + 12, y + 8, w - 92, 24), team == 1 ? "TEAM 1 / ALLIES" : "TEAM 2 / ENEMIES", 14, color);
                ui.Label(new Rect(x + w - 74, y + 4, 62, 30), kills.ToString(), 24, TacticalUi.Text, TextAnchor.UpperRight);
                float rowY = y + 40;
                TacticalUi.Fill(new Rect(x + 10, rowY + 16, w - 20, 1), TacticalUi.Raised);
                ui.Label(new Rect(x + 12, rowY, w - 148, 16), "OPERATOR", 12, TacticalUi.Muted);
                ui.Label(new Rect(x + w - 140, rowY, 44, 16), "SCORE", 12, TacticalUi.Muted, TextAnchor.UpperRight);
                ui.Label(new Rect(x + w - 90, rowY, 32, 16), "K", 12, TacticalUi.Muted, TextAnchor.UpperRight);
                ui.Label(new Rect(x + w - 44, rowY, 32, 16), "D", 12, TacticalUi.Muted, TextAnchor.UpperRight);
                int rowIndex = 0;
                float rh = Mathf.Min(36, (height - 72) / 6);
                for (int i = 0; i < scoreboard.RowCount; i++)
                {
                    var row = scoreboard.Rows[i];
                    if (row.Team != team) continue;
                    float ry = rowY + 20 + rowIndex * rh;
                    TacticalUi.Fill(new Rect(x + 2, ry, w - 4, rh - 3), row.IsLocal ? TacticalUi.Raised : new Color(.04f, .055f, .06f, .55f));
                    if (row.IsLocal) TacticalUi.Fill(new Rect(x + 2, ry, 3, rh - 3), TacticalUi.Accent);
                    string name = (row.IsLocal ? "YOU / " : row.IsHuman ? "OP / " : "BOT / ") + row.DisplayName;
                    ui.Label(new Rect(x + 14, ry, w - 160, rh - 3), name, 16, TacticalUi.Text, TextAnchor.MiddleLeft);
                    ui.Label(new Rect(x + w - 140, ry, 44, rh - 3), row.Score.ToString(), 16, TacticalUi.Accent, TextAnchor.MiddleRight);
                    ui.Label(new Rect(x + w - 90, ry, 32, rh - 3), row.Kills.ToString(), 16, TacticalUi.Text, TextAnchor.MiddleRight);
                    ui.Label(new Rect(x + w - 44, ry, 32, rh - 3), row.Deaths.ToString(), 16, TacticalUi.Muted, TextAnchor.MiddleRight);
                    rowIndex++;
                }
            }
        }

        private void OnDestroy()
        {
            loadCancel?.Cancel();
            loadCancel?.Dispose();
            _ = quitRequested;
        }
    }
}
