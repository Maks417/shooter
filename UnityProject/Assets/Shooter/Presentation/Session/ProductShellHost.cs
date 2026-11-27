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
    /// <summary>
    /// T059 product shell: owns menu → load → play → pause → results lifecycle.
    /// Presentation binds/clears through ProductSessionView; pause goes through <see cref="IProductShellBackend"/>.
    /// </summary>
    public sealed partial class ProductShellHost : MonoBehaviour
    {
        private IProductShellBackend? backend;
        private IGameSessionControl? session;
        // ADR-122: rules of the mode being played (limits and labels in the HUD and results).
        private ModeRules activeMode = ModeRules.All[0];
        private ModeRules ActiveMode => activeMode;
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
        // ADR-107 Armory: the player's saved primary + sight and sidearm, applied to every match.
        private IArmoryStore? armoryStore;
        private ArmorySelection? armory;
        private int armorySavedRevision;
        private string armoryStatus = "";

        public ArmorySelection? Armory => armory;

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
            LoadCareer(System.IO.Path.Combine(Application.persistentDataPath, "shooter-career.json"));
            LoadArmory(System.IO.Path.Combine(Application.persistentDataPath, "shooter-armory.json"));
            scoreboard.RankOf = (participant, isHuman) => session?.DisplayRankOf(participant, isHuman) ?? 0;

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
                // The session stays Finished with the same results after the shell leaves the match (and a new
                // epoch would pass CareerRecorder's guard), so the award is recorded only on this match's first
                // Finished frame.
                var results = coordinator.MatchActive ? session.TryAcquireResults() : null;
                if (results != null && coordinator.TryFinishMatch(results)) RecordCareer(results);
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
                    tension = MusicDirector.IsTense(snap.Team1Kills, snap.Team2Kills, ActiveMode.ScoreLimit, snap.RemainingActiveTicks);
            }
            else if (context == MusicContext.Finished && coordinator.RetainedResults != null)
            {
                var results = coordinator.RetainedResults;
                outcome = MusicDirector.OutcomeFor(results.Outcome, MusicDirector.LocalTeam(results, session.HumanEntityId));
            }
            view.UpdateMusic(context, tension, outcome, navigation.Screen != ShellScreen.Playing);
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
                case ShellScreen.Armory:
                    CloseArmory();
                    break;
                case ShellScreen.Career:
                    CloseCareer();
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
            if (request != null) activeMode = request.Rules;
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

            ApplyArmoryToSession();
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
                || navigation.Screen == ShellScreen.Armory || navigation.Screen == ShellScreen.Career
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
            // ADR-123: the live map vista backs every front-end page; Loading and Results keep its last (frozen) frame of
            // the selected map, so the whole shell reads as one place. The stage itself only renders on front-end pages.
            if (menuScene != null && navigation.Screen != ShellScreen.Main) menuScene.SetFocusedMap(setup.SelectedMapId.Value);
            bool vistaPage = menuScene != null && (menuScene.gameObject.activeInHierarchy
                || navigation.Screen == ShellScreen.Loading || navigation.Screen == ShellScreen.Results);
            ui.SceneTexture = vistaPage ? menuScene!.Frame : null;
            ui.SceneFade = vistaPage ? menuScene!.PreviousFrame : null;
            ui.SceneFadeAlpha = vistaPage ? menuScene!.FadeAlpha : 0f;
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
                    else if (uiGalleryMode == "career" && !careerGalleryOpened) { OpenCareerGallery(); DrawMain(s); }
                    else if (uiGallery && uiGalleryMode != "career") ui.DrawUiGallery();
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
                case ShellScreen.Armory:
                    DrawArmory();
                    break;
                case ShellScreen.Career:
                    DrawCareer();
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
                    UiInk.Alpha(TacticalUi.Background, veil * .85f));
            }
            finally { ui.End(); }
        }

        private void ConfirmLeave() => LeaveMatch(abort: true);

        private static readonly string[] SettingsTabs = { "AUDIO", "VIDEO", "CONTROLS", "ACCESSIBILITY" };
        private static readonly string[] SettingsTabIcons = { IconLibrary.Audio, IconLibrary.Video, IconLibrary.Controls, IconLibrary.Accessibility };

        private void OnDestroy()
        {
            loadCancel?.Cancel();
            loadCancel?.Dispose();
            _ = quitRequested;
        }
    }
}
