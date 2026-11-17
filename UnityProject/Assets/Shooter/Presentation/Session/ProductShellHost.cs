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
        private ShellScreen enteredScreen = (ShellScreen)255;
        private float enteredAt;
        // Review-only: -shooterUiGallery 1 replaces Main with the ADR-084 element gallery.
        private readonly bool uiGallery = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-shooterUiGallery") >= 0;
        // "-shooterUiGallery results" shows the debrief preview instead of the element gallery.
        private readonly string uiGalleryMode = GalleryMode();

        private static string GalleryMode()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-shooterUiGallery");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
        }
        private IUserSettingsStore? settingsStore;
        private UserSettingsEditor? settingsEditor;
        private IClientSettingsApplier? settingsApplier;
        private ProductClientSettingsApplier? productSettingsApplier;
        private InputBindingEditor? bindingEditor;
        private readonly DisplayChangeConfirmClock displayClock = new DisplayChangeConfirmClock();
        private string settingsStatus = "";
        private Vector2 settingsScroll;
        // First visible row of the debrief / Tab team tables (whole-row scrolling, shared by both teams).
        private int debriefFirstRow;
        private int liveFirstRow;
        private string? tableScrollHint;
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

            if (!GameContractVersion.IsAllowedTeamSize(preferredTeamSize)) preferredTeamSize = 5;
            MapId preferredMap;
            try { preferredMap = new MapId(string.IsNullOrEmpty(preferredMapId) ? WellKnownMaps.TrainingCompound.Value : preferredMapId); }
            catch { preferredMap = WellKnownMaps.TrainingCompound; }

            setup = new SetupSelection(catalog, preferredMap, preferredTeamSize);
            view = ProductSessionView.Create(session, shellOwned: true);
            ui.Sound = kind => view?.PlayShellUi(kind);
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
            if (!scoreboardHeld) liveFirstRow = 0;

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
            UpdateMusic();
        }

        /// <summary>ADR-096: menu theme, calm/tension match bed, outcome stinger and results loop.</summary>
        private void UpdateMusic()
        {
            if (view == null || session == null) return;
            var context = MusicDirector.ContextFor(navigation.Screen, navigation.SettingsReturn);
            bool tension = false;
            var outcome = LocalMatchOutcome.Unknown;
            if (context == MusicContext.Match && session.Phase == GameMatchPhase.Playing)
            {
                var snap = session.TryAcquireSnapshot();
                if (snap != null)
                    tension = MusicDirector.IsTense(snap.Team1Kills, snap.Team2Kills, GameContractVersion.ScoreLimit, snap.RemainingActiveTicks);
            }
            else if (context == MusicContext.Finished && coordinator.RetainedResults != null)
            {
                var results = coordinator.RetainedResults;
                outcome = MusicDirector.OutcomeFor(results.Outcome, MusicDirector.LocalTeam(results, session.HumanEntityId));
            }
            view.UpdateMusic(context, tension, outcome, navigation.Screen != ShellScreen.Playing);
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
                    if (TryResumeSession())
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
            if (TryResumeSession())
            {
                navigation.ResumePlaying();
                view.SetGameplayFocus(true);
            }
        }

        /// <summary>Resume the authority, or accept it as already running (never strand the player on the pause menu).</summary>
        private bool TryResumeSession()
        {
            if (session != null && session.PauseState == SessionPauseState.Running) return true;
            return backend != null && backend.Resume().Succeeded;
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
            await AwaitLoadingPaintAsync().ConfigureAwait(true);
            if (!coordinator.SameLoadEpoch(epoch)) return;
            if(loadCancel.IsCancellationRequested){coordinator.CompleteStartFailure();navigation.CancelLoad();return;}
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
                await view.BindVerifiedMapAsync(boxes, metadata, mapId, visuals, loadCancel.Token);
            }
            catch (OperationCanceledException)
            {
                if(!coordinator.SameLoadEpoch(epoch))return;
                session.RequestAbort();coordinator.CompleteStartFailure();navigation.CancelLoad();return;
            }
            catch (Exception ex)
            {
                if(!coordinator.SameLoadEpoch(epoch))return;
                session.RequestAbort();
                coordinator.CompleteStartFailure();
                navigation.FailLoad(ex.Message);
                return;
            }

            if(!coordinator.SameLoadEpoch(epoch))return;
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
            if (settingsEditor != null)
            {
                ui.ReducedMotion = settingsEditor.Applied.ReducedCameraMotion;
                ui.ReducedFlash = settingsEditor.Applied.ReducedFlash;
            }
            if (navigation.Screen != enteredScreen)
            {
                enteredScreen = navigation.Screen;
                enteredAt = Time.unscaledTime;
                debriefFirstRow = 0;
                tableScrollHint = null;
            }
            float sinceEnter = Time.unscaledTime - enteredAt;
            ui.Begin(uiScale, navigation.Screen.ToString(), bindingEditor == null || !bindingEditor.IsListening,
                ShellMotion.EnterOffset(navigation.Screen, sinceEnter, ui.ReducedMotion));
            try
            {
            switch (navigation.Screen)
            {
                case ShellScreen.Main:
                    if (uiGalleryMode == "results") DrawDebriefPreview();
                    else if (uiGallery) ui.DrawUiGallery();
                    else DrawMain(s);
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
                    // The Tab board carries its own score/clock header, so the top strip yields to it.
                    if (scoreboardHeld) DrawScoreboard(s);
                    else DrawPlayingHud(s);
                    break;
            }
            // ADR-086: freshly entered pages rise out of a short dark veil.
            float veil = ShellMotion.EnterVeil(navigation.Screen, sinceEnter, ui.ReducedMotion);
            if (veil > 0.005f)
                TacticalUi.Fill(new Rect(-40, -60, ui.Width + 80, ui.Height + 120),
                    new Color(TacticalUi.Background.r, TacticalUi.Background.g, TacticalUi.Background.b, veil * .85f));
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
            string[] tabIcons = { IconLibrary.Audio, IconLibrary.Video, IconLibrary.Controls, IconLibrary.Accessibility };
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tab = new Rect(a.x + i * (tw + 6), tabY + 1, tw, 34);
                if (ui.Button(tab, "settings.tab." + i, tabs[i], selected: settingsTab == i, quiet: true))
                {
                    settingsTab = i;
                    settingsScroll = Vector2.zero;
                }
                // ADR-085 category glyph on the tab's right edge.
                TacticalUi.Icon(new Rect(tab.xMax - 34, tab.y + 7, 20, 20), tabIcons[i], settingsTab == i ? TacticalUi.Accent : TacticalUi.Muted);
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
            DrawDebrief(Time.unscaledTime - enteredAt);
        }

        /// <summary>Review-only debrief preview with sample rows (<c>-shooterUiGallery results</c>); loops every 7 s.</summary>
        private void DrawDebriefPreview()
        {
            ui.Backdrop();
            var rows = new[]
            {
                new ScoreboardRow(new ParticipantId(1), "Player", 1, true, 1450, 14, 5, true, true),
                new ScoreboardRow(new ParticipantId(2), "Nora", 1, false, 1200, 12, 9, false, true),
                new ScoreboardRow(new ParticipantId(3), "Elena", 1, false, 900, 9, 3, false, true),
                new ScoreboardRow(new ParticipantId(4), "Cole", 2, false, 1300, 15, 11, false, true),
                new ScoreboardRow(new ParticipantId(5), "Omar", 2, false, 700, 7, 12, false, true),
                new ScoreboardRow(new ParticipantId(6), "Aya", 2, false, 400, 4, 13, false, true),
            };
            scoreboard.BindPreview(rows, 150, 132, MatchOutcome.Team1, FinishReason.ScoreLimit);
            DrawDebrief((Time.unscaledTime - enteredAt) % 7f);
        }

        private void DrawDebrief(float t)
        {
            bool reduced = ui.ReducedMotion;
            Rect a = ui.Safe;
            ui.Header(ShellUiCopy.EyebrowDebrief, "Match debrief", ShellUiCopy.TeamDeathmatch);

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
            ui.Label(new Rect(cx - 140, y0 + 30, 280, 14), "TEAM DEATHMATCH  ·  FIRST TO " + GameContractVersion.ScoreLimit, 11, TacticalUi.Muted, TextAnchor.MiddleCenter);
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
                float fill = halfBar * Mathf.Clamp01(kills / (float)GameContractVersion.ScoreLimit);
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

        private void OnDestroy()
        {
            loadCancel?.Cancel();
            loadCancel?.Dispose();
            _ = quitRequested;
        }
    }
}
