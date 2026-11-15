#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation.Environment;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Fx;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Input;
using Shooter.Presentation.Rendering;
using Shooter.Presentation.Rig;
using Shooter.Presentation.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Presentation.Session
{
    /// <summary>M8/T056 product presentation: map surfaces, team actors, weapons, FPS camera/HUD. Authority remains OfflineGameSession.</summary>
    public sealed class ProductSessionView : MonoBehaviour
    {
        private OfflineGameSession? session;
        private LocalPlayerInput input = new LocalPlayerInput();
        private System.Func<GameOperationResult>? pauseSession;
        private System.Func<EntityId, LocalMovementView?>? movementReader;
        private System.Func<GameOperationResult>? resumeSession;
        private readonly HudLayout hud = new HudLayout();
        private readonly SynthAudioMixer mixer = new SynthAudioMixer();
        private readonly ProductAudioEventRouter audio;
        private readonly AnalyticFxPool analyticFxPool = new AnalyticFxPool();
        private readonly List<ActorLifecycleState> footstepScratch = new List<ActorLifecycleState>(GameContractVersion.MaxParticipants);
        private readonly HashSet<string> accessibilityCueKeys = new HashSet<string>(System.StringComparer.Ordinal);
        private UnitySynthAudioOutput? audioOutput;
        private AnalyticFxPresenter? analyticFxPresenter;
        private Camera? camera;
        private ProductMapView? mapView;
        private ProductActorPresentation? actors;
        private ProductVisualReviewOverlay? visualReview;
        private EntityId inputLifeEntity;
        private bool cursorLocked;
        private bool shellOwned;
        private string boundMapId = "";
        private bool audioAttached;
        private ulong audioSessionId;
        private float adsFovDegrees = 60f;
        private bool reducedCameraMotion;
        private bool reducedFlash;
        private bool subtitlesEnabled;
        private float muzzleFlashRemaining;
        private string subtitleText = "";
        private float subtitleRemaining;
        private readonly TacticalUi combatUi = new TacticalUi();
        private IReadOnlyList<CollisionBox> hudBoxes = System.Array.Empty<CollisionBox>();
        private readonly EventId[] recentDeathIds = new EventId[32];
        private int deathIdCursor;
        private readonly string[] killFeed = new string[4];
        private readonly float[] killFeedUntil = new float[4];

        public ProductMapView? MapView => mapView;
        public ProductActorPresentation? Actors => actors;
        public ProductVisualReviewOverlay? VisualReview => visualReview;
        public LocalPlayerInput Input => input;
        public bool GameplayFocus => cursorLocked;
        public SynthAudioMixer Mixer => mixer;
        public ProductAudioEventRouter Audio => audio;
        public bool ReducedFlash => reducedFlash;
        public bool SubtitlesEnabled => subtitlesEnabled;
        public bool ReducedCameraMotion => reducedCameraMotion;

        public ProductSessionView()
        {
            audio = new ProductAudioEventRouter(mixer);
        }

        public static ProductSessionView Create(OfflineGameSession session, bool shellOwned = false)
        {
            var root = new GameObject("Product session view").AddComponent<ProductSessionView>();
            root.Initialize(session, session.Pause, session.Resume, shellOwned);
            return root;
        }

        private void Initialize(
            OfflineGameSession session,
            System.Func<GameOperationResult> pauseSession,
            System.Func<GameOperationResult> resumeSession,
            bool shellOwned)
        {
            this.session = session ?? throw new System.ArgumentNullException(nameof(session));
            movementReader = entity => session.TryGetMovementView(entity, out var movement) ? movement : (LocalMovementView?)null;
            this.pauseSession = pauseSession ?? throw new System.ArgumentNullException(nameof(pauseSession));
            this.resumeSession = resumeSession ?? throw new System.ArgumentNullException(nameof(resumeSession));
            this.shellOwned = shellOwned;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("Required locked URP Lit shader missing; no art fallback");

            camera = new GameObject("Product camera").AddComponent<Camera>();
            camera.transform.SetParent(transform, false);
            camera.fieldOfView = hud.Settings.HipFov;
            adsFovDegrees = hud.Settings.HipFov * (60f / 80f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = QualitySettings.GetQualityLevel() == 2;
            camera.cullingMask &= ~(1 << ProductMenuScene.StageLayer);

            var sun = new GameObject("Product sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            // Slightly lower elevation + more lateral azimuth: longer readable hard shadows on Low.
            sun.transform.rotation = Quaternion.Euler(46f, 52f, 0f);
            sun.color = new Color(1f, 0.93f, 0.82f, 1f);
            sun.intensity = 1.28f;
            sun.shadows = LightShadows.Hard;
            sun.cullingMask &= ~(1 << ProductMenuScene.StageLayer);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.29f, 0.26f, 1f);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            int profile = QualitySettings.GetQualityLevel();
            Screen.SetResolution(profile == 0 ? 1280 : profile == 1 ? 1600 : 1920, profile == 0 ? 720 : profile == 1 ? 900 : 1080, FullScreenMode.Windowed);

            // Shell owns start: launch unlocked on menu. Legacy non-shell path keeps locked gameplay cursor.
            cursorLocked = !shellOwned;
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !cursorLocked;

            mapView = ProductMapView.Create(transform);
            actors = ProductActorPresentation.Create(transform, camera);
            analyticFxPresenter = AnalyticFxPresenter.Create(transform, analyticFxPool);
            audioOutput = UnitySynthAudioOutput.Attach(gameObject, mixer);
            UserSettingsAudioBinding.ApplyGains(UserSettings.Defaults, mixer);
            TryEnableVisualReviewFromCommandLine();
            Debug.Log("SHOOTER_PRODUCT_VIEW gpu=" + SystemInfo.graphicsDeviceName + " profile=" + QualitySettings.names[profile] +
                      " shellOwned=" + (shellOwned ? 1 : 0) +
                      " visualReview=" + (visualReview != null ? 1 : 0));
        }

        /// <summary>
        /// T062 capture/overlay on the existing T056 presentation path. Enable with
        /// <c>-shooterVisualReview 1</c> and optional <c>-shooterVisualReviewOutput &lt;Artifacts/...&gt;</c>.
        /// Does not reopen P05 clearance search.
        /// </summary>
        public ProductVisualReviewOverlay EnableVisualReview(string? outputDirectory = null)
        {
            if (actors == null || camera == null)
                throw new System.InvalidOperationException("Product presentation is not initialized.");
            ProductVisualDefectCatalog.ValidateImmutableP05Fail();
            visualReview = ProductVisualReviewOverlay.Attach(transform, actors, camera, outputDirectory ?? "");
            visualReview.SetContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], 0);
            return visualReview;
        }

        private void TryEnableVisualReviewFromCommandLine()
        {
            string enabled = ReadCommandLine("-shooterVisualReview", "0");
            if (enabled != "1" && !string.Equals(enabled, "true", System.StringComparison.OrdinalIgnoreCase))
                return;
            string output = ReadCommandLine("-shooterVisualReviewOutput", "");
            EnableVisualReview(output);
        }

        private static string ReadCommandLine(string name, string fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != name) continue;
                if (i + 1 >= args.Length) throw new System.ArgumentException("Missing value for " + name);
                return args[i + 1];
            }
            return fallback;
        }

        /// <summary>
        /// Apply non-display UserSettings (audio/FOV/input/HUD/bindings/accessibility).
        /// Display surface is owned by <see cref="ProductClientSettingsApplier"/>.
        /// </summary>
        public void ApplyUserSettings(UserSettings settings)
        {
            if (settings == null) throw new System.ArgumentNullException(nameof(settings));
            UserSettingsAudioBinding.ApplyGains(settings, mixer);
            string qualityName = settings.QualityTier < QualitySettings.names.Length
                ? QualitySettings.names[settings.QualityTier]
                : "Low";
            hud.ApplySettings(new HudSettings(
                settings.UiScale,
                settings.HipFovDegrees,
                qualityName,
                settings.ReducedCameraMotion,
                settings.Contrast));
            adsFovDegrees = settings.HipFovDegrees * (60f / 80f);
            input.Sensitivity = settings.MouseSensitivity;
            input.InvertY = settings.InvertY;
            input.ApplyBindings(settings.Bindings);
            reducedCameraMotion = settings.ReducedCameraMotion;
            reducedFlash = settings.ReducedFlash;
            subtitlesEnabled = settings.Subtitles;
            if (!subtitlesEnabled)
            {
                subtitleText = "";
                subtitleRemaining = 0f;
            }
            if (reducedFlash)
            {
                muzzleFlashRemaining = 0f;
                analyticFxPresenter?.Sync(suppressMuzzleFlash: true);
            }
            if (camera != null && !IsAiming())
                camera.fieldOfView = settings.HipFovDegrees;
        }

        private bool IsAiming() => input.AimHeld && cursorLocked;

        /// <summary>
        /// Bind verified packaged solids/cover metadata and actor visual payloads into MeshRenderers.
        /// Safe to call again after unload (clears prior visuals first). Missing packages fail recoverably
        /// rather than drawing diagnostic shapes or regenerating geometry.
        /// </summary>
        public void BindVerifiedMap(
            IReadOnlyList<CollisionBox> boxes,
            byte[]? metadataBytes,
            string mapId,
            PackagedVisualPayloads visuals)
        {
            if (mapView == null) throw new System.InvalidOperationException("Product map view missing.");
            if (actors == null) throw new System.InvalidOperationException("Product actor presentation missing.");
            if (boxes == null || boxes.Count == 0)
                throw new System.InvalidOperationException("Verified map package solids missing; refusing silent empty/diagnostic visuals.");
            if (visuals == null)
                throw new System.InvalidOperationException("Verified visual package missing; refusing generation fallback.");
            mapView.Install(boxes, metadataBytes, mapId ?? "", visuals);
            actors.ClearAll();
            actors.BindPackagedVisuals(visuals);
            boundMapId = mapId ?? "";
            hudBoxes = boxes;
            if (camera != null && mapView.IsInstalled)
                camera.clearFlags = CameraClearFlags.Skybox;
            visualReview?.SetContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], 0);
            Debug.Log("SHOOTER_PRODUCT_MAP_BOUND map=" + boundMapId + " solids=" + mapView.SolidCount + " packagedVisuals=1");
        }

        public void ClearPresentation()
        {
            DetachAudioSession();
            analyticFxPool.Clear();
            analyticFxPresenter?.ClearVisuals();
            mapView?.ClearVisuals();
            actors?.ClearAll();
            boundMapId = "";
            hudBoxes = System.Array.Empty<CollisionBox>();
            System.Array.Clear(recentDeathIds, 0, recentDeathIds.Length);
            System.Array.Clear(killFeedUntil, 0, killFeedUntil.Length);
            deathIdCursor = 0;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 1f);
            }
        }

        public void SetGameplayFocus(bool enabled)
        {
            if (cursorLocked == enabled) return;
            cursorLocked = enabled;
            if (!enabled) input.ClearCapturedInput();
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        public void NotifyMenuTookFocus()
        {
            SetGameplayFocus(false);
        }

        private void Update()
        {
            if (session == null) return;

            // Legacy Escape pause only when shell does not own the session lifecycle.
            if (!shellOwned)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (session.PauseState == SessionPauseState.Running && session.Phase == GameMatchPhase.Playing)
                    {
                        if (pauseSession!().Succeeded)
                            SetGameplayFocus(false);
                    }
                    else if (session.PauseState == SessionPauseState.Paused)
                    {
                        if (resumeSession!().Succeeded)
                            SetGameplayFocus(true);
                    }
                }
            }

            bool acceptsGameplayInput = false;
            bool aimHeld = false;
            Float3 listenerFeet = default;
            bool hasListener = false;
            if (session.Lifecycle != null && session.Lifecycle.TryGetActorState(session.HumanEntityId, out var human))
            {
                if (!human.EntityId.Equals(inputLifeEntity))
                {
                    inputLifeEntity = human.EntityId;
                    input.ResetForLife(human.AimYaw, human.AimPitch, human.ActiveWeaponSlot);
                }
                acceptsGameplayInput = session.Phase == GameMatchPhase.Playing
                    && session.PauseState == SessionPauseState.Running
                    && human.Alive
                    && cursorLocked
                    && Application.isFocused;
                input.CaptureFrame(acceptsGameplayInput);
                aimHeld = acceptsGameplayInput && input.AimHeld;
                listenerFeet = human.Feet;
                hasListener = true;
                if (camera != null)
                {
                    float eye = human.Alive ? 1.65f : 0.4f;
                    if (session.TryGetMovementView(human.EntityId, out var movement)) eye = movement.EyeHeight;
                    camera.transform.position = new Vector3(human.Feet.X, human.Feet.Y + eye, human.Feet.Z);
                    float cameraYaw = acceptsGameplayInput ? input.Yaw : human.AimYaw;
                    float cameraPitch = acceptsGameplayInput ? input.Pitch : human.AimPitch;
                    camera.transform.rotation = Quaternion.Euler(cameraPitch * Mathf.Rad2Deg, cameraYaw * Mathf.Rad2Deg, 0f);
                    float hip = hud.Settings.HipFov;
                    float ads = adsFovDegrees;
                    // Reduced camera motion: keep hip FOV (cosmetic ADS zoom only when preference is off).
                    float targetFov = (!reducedCameraMotion && aimHeld) ? ads : hip;
                    camera.fieldOfView = targetFov;
                }
            }
            else input.CaptureFrame(false);

            IReadOnlyList<CombatEvent>? combatEvents = null;
            EventLease? combatLease = null;
            try
            {
                combatLease = session.TryLeaseCombatEvents();
                if (combatLease != null) combatEvents = combatLease.Events;
                if (shellOwned && combatEvents != null) ObserveEliminations(combatEvents);

                if (actors != null)
                {
                    var snap = session.TryAcquireSnapshot();
                    actors.SetReviewContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], session.CompletedTick);
                    actors.Sync(
                        snap,
                        session.Lifecycle,
                        i =>
                        {
                            session.TryGetCosmeticCorpse(i, out var corpse);
                            return corpse;
                        },
                        session.CosmeticCorpseCount,
                        session.HumanEntityId,
                        aimHeld,
                        Time.unscaledDeltaTime,
                        combatEvents,
                        session.CompletedTick,
                        movementReader,
                        reducedMotion: reducedCameraMotion);
                    visualReview?.SetContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], session.CompletedTick);
                }

                PumpProductAudio(hasListener ? listenerFeet : default, combatEvents);
            }
            finally
            {
                combatLease?.Dispose();
            }

            TickAccessibilityPresentation(Time.unscaledDeltaTime);
        }

        private void TickAccessibilityPresentation(float dt)
        {
            if (dt < 0f) dt = 0f;
            if (muzzleFlashRemaining > 0f)
            {
                muzzleFlashRemaining -= dt;
                if (muzzleFlashRemaining < 0f) muzzleFlashRemaining = 0f;
            }
            if (subtitleRemaining > 0f)
            {
                subtitleRemaining -= dt;
                if (subtitleRemaining <= 0f)
                {
                    subtitleRemaining = 0f;
                    subtitleText = "";
                }
            }
            analyticFxPool.Tick(dt);
            analyticFxPresenter?.Sync(suppressMuzzleFlash: reducedFlash);
        }

        private void NotifyAccessibilityCue(CombatEventKind kind)
        {
            if (kind == CombatEventKind.ShotAccepted)
            {
                // Cosmetic view flash only; ReducedFlash suppresses it while preserving shot audio/hit info.
                if (!reducedFlash) muzzleFlashRemaining = 0.06f;
                if (subtitlesEnabled) ShowSubtitle("Shot");
            }
            else if (kind == CombatEventKind.Impact || kind == CombatEventKind.DamageApplied)
            {
                if (subtitlesEnabled) ShowSubtitle(kind == CombatEventKind.Impact ? "Impact" : "Hit");
            }
            else if (kind == CombatEventKind.ReloadStarted && subtitlesEnabled)
                ShowSubtitle("Reloading");
            else if (kind == CombatEventKind.ReloadCompleted && subtitlesEnabled)
                ShowSubtitle("Reload complete");
        }

        /// <summary>
        /// Spawn short-lived analytic mesh FX from authority combat events already leased for audio/HUD.
        /// ReducedFlash suppresses muzzle mesh + overlay only; impact/smoke proxies remain.
        /// </summary>
        private void ObserveCombatFx(CombatEvent ev)
        {
            if (ev.Kind == CombatEventKind.ShotAccepted && ev.Payload is ShotPayload shot)
            {
                // Accessibility: reducedFlash suppresses world muzzle proxies and the IMGUI overlay.
                if (reducedFlash) return;
                Float3 dir = shot.UnitDirection;
                var muzzle = new Float3(
                    shot.Origin.X + dir.X * 0.12f,
                    shot.Origin.Y + dir.Y * 0.12f,
                    shot.Origin.Z + dir.Z * 0.12f);
                analyticFxPool.TrySpawn(FxKind.MuzzleFlash, muzzle, dir);
                return;
            }

            if (ev.Kind == CombatEventKind.Impact && ev.Payload is ImpactPayload impact)
            {
                analyticFxPool.TrySpawn(FxKind.ImpactSpark, impact.Position, impact.Normal);
                // Occasional smoke so the smoke cap stays readable under heavy fire.
                if ((ev.Id.Ordinal % 3) == 0)
                    analyticFxPool.TrySpawn(FxKind.SmokeProxy, impact.Position, impact.Normal);
            }
        }

        private void ShowSubtitle(string text)
        {
            subtitleText = text ?? "";
            subtitleRemaining = 1.6f;
        }

        private void PumpProductAudio(Float3 listenerFeet, IReadOnlyList<CombatEvent>? combatEvents)
        {
            if (session == null) return;
            if (session.Phase == GameMatchPhase.Stopped || session.Phase == GameMatchPhase.Loading)
            {
                DetachAudioSession();
                return;
            }

            ulong sid = session.AuthoritySessionId;
            if (sid == 0)
            {
                DetachAudioSession();
                return;
            }
            if (!audioAttached || audioSessionId != sid)
            {
                audio.AttachSession(sid);
                audioSessionId = sid;
                audioAttached = true;
                audioOutput?.ArmOutput();
            }

            if (combatEvents != null)
            {
                audio.ConsumeCombatEvents(combatEvents, listenerFeet);
                for (int i = 0; i < combatEvents.Count; i++)
                {
                    var ev = combatEvents[i];
                    string key = ev.Id.AuthoritySessionId + ":" + ev.Id.Tick + ":" + ev.Id.EntityId + ":" + ev.Id.Ordinal;
                    if (!accessibilityCueKeys.Add(key)) continue;
                    NotifyAccessibilityCue(ev.Kind);
                    ObserveCombatFx(ev);
                }
            }

            footstepScratch.Clear();
            var snap = session.TryAcquireSnapshot();
            if (snap != null && session.Lifecycle != null)
            {
                for (int i = 0; i < snap.Participants.Count; i++)
                {
                    var p = snap.Participants[i];
                    if (!p.Alive || !p.EntityId.IsValid) continue;
                    if (!session.Lifecycle.TryGetActorState(p.EntityId, out var state) || !state.Alive) continue;
                    footstepScratch.Add(state);
                }
            }
            audio.TickFootsteps(footstepScratch, listenerFeet, session.CompletedTick);
        }

        private void DetachAudioSession()
        {
            if (!audioAttached && !audio.IsAttached) return;
            audio.DetachSession();
            audioAttached = false;
            audioSessionId = 0;
            accessibilityCueKeys.Clear();
            audioOutput?.Detach();
        }

        public LocalPlayerCommand? SampleHuman(uint tick)
        {
            if (session == null || !session.HumanEntityId.IsValid) return null;
            if (session.PauseState != SessionPauseState.Running || session.Phase != GameMatchPhase.Playing) return null;
            if (!cursorLocked || !Application.isFocused) return null;
            return input.ConsumeLocalTick(session.HumanEntityId, tick);
        }

        /// <summary>Compatibility entry for direct combat cues; product path prefers ordered event leases.</summary>
        public void NotifyCombat(CombatEventKind kind, float distanceMeters, ulong seed)
        {
            if (!audioAttached) return;
            var voice = kind switch
            {
                CombatEventKind.ShotAccepted => SynthVoiceKind.Shot,
                CombatEventKind.Impact => SynthVoiceKind.Impact,
                CombatEventKind.DamageApplied => SynthVoiceKind.Impact,
                _ => SynthVoiceKind.Footstep
            };
            mixer.TryPlay(voice, distanceMeters, seed);
        }

        public bool PlayUiCue(ulong seed)
        {
            if (subtitlesEnabled) ShowSubtitle("UI");
            return audio.PlayUi(seed);
        }

        private void OnGUI()
        {
            DrawAccessibilityOverlays();
            // Shell host draws menus/scoreboard/results; keep a minimal combat HUD only while playing under shell.
            if (session == null) return;
            if (shellOwned)
            {
                if (!cursorLocked || session.Phase != GameMatchPhase.Playing || session.PauseState != SessionPauseState.Running) return;
                DrawCombatHud();
                return;
            }

            var snap = session.TryAcquireSnapshot();
            string line1 = "Shooter offline TDM";
            string line2 = session.Phase + " pause=" + session.PauseState;
            if (snap != null)
                line2 = "T1 " + snap.Team1Kills + "  T2 " + snap.Team2Kills + "  rem=" + snap.RemainingActiveTicks +
                        "  phase=" + snap.Phase;
            if (session.Lifecycle != null && session.Lifecycle.TryGetActorState(session.HumanEntityId, out var human))
            {
                GUI.Label(new Rect(24, Screen.height - 72, 640, 24),
                    "HP " + human.Health + "  Ammo " + human.Magazine + "/" + human.Reserve + "  slot " + human.ActiveWeaponSlot,
                    GuiStyle());
            }
            GUI.Label(new Rect(24, 24, 800, 24), line1, GuiStyle());
            GUI.Label(new Rect(24, 48, 800, 24), line2, GuiStyle());
            if (mapView != null && mapView.IsInstalled)
                GUI.Label(new Rect(24, 72, 900, 24),
                    "map " + boundMapId + " solids=" + mapView.SolidCount + " covers=" + mapView.CoverCount +
                    " actors=" + (actors?.LiveAvatarCount ?? 0) + " corpses=" + (actors?.CorpseAvatarCount ?? 0),
                    GuiStyle());
            if (session.PauseState == SessionPauseState.Paused)
                GUI.Label(new Rect(Screen.width * 0.5f - 80, Screen.height * 0.5f - 20, 200, 40), "PAUSED (Esc)", GuiStyle());
            var results = session.TryAcquireResults();
            if (results != null)
                GUI.Label(new Rect(24, 104, 900, 24),
                    "Result " + results.Outcome + " / " + results.Reason + "  " + results.Team1Kills + "-" + results.Team2Kills,
                    GuiStyle());
        }

        private void DrawCombatHud()
        {
            if (session == null) return;
            combatUi.Begin(hud.Settings.UiScale, "combat", keyboardNavigation: false);
            try
            {
                Rect a = combatUi.Safe;
                var snap = session.TryAcquireSnapshot();
                bool alive = session.Lifecycle != null && session.Lifecycle.TryGetActorState(session.HumanEntityId, out var state) && state.Alive;
                if (!alive)
                {
                    string respawn = "Awaiting a safe spawn";
                    if (snap != null)
                        for (int i = 0; i < snap.Participants.Count; i++)
                        {
                            var participant = snap.Participants[i];
                            if (!participant.IsHuman) continue;
                            uint due = participant.RespawnDueActiveTick;
                            uint remaining = Serial32.IsNewer(due, snap.ActiveTicksElapsed) ? Serial32.ForwardDistance(due, snap.ActiveTicksElapsed) : 0;
                            if (remaining > 0) respawn = "Returning in " + (remaining / 60f).ToString("0.0") + "s";
                        }
                    Rect panel = new Rect(combatUi.Width * .5f - 220, combatUi.Height * .5f - 38, 440, 96);
                    TacticalUi.Fill(panel, TacticalUi.Background);
                    combatUi.Label(new Rect(panel.x, panel.y + 12, panel.width, 32), "KILLED IN ACTION", 26, TacticalUi.Enemy, TextAnchor.UpperCenter);
                    combatUi.Label(new Rect(panel.x, panel.y + 54, panel.width, 30), respawn, 18, TacticalUi.Text, TextAnchor.UpperCenter);
                    return;
                }
                // Read the current life again for definite assignment; never retain an authority-owned object.
                if (!session.Lifecycle!.TryGetActorState(session.HumanEntityId, out var human)) return;
                DrawTacticalMap(a, human, snap);
                float x = a.xMax - 174, y = a.yMax - 76;
                TacticalUi.Fill(new Rect(x, y, 174, 76), new Color(.025f, .04f, .05f, .82f));
                string weapon = human.ActiveWeaponSlot == 0 ? "RIFLE / 01" : "SIDEARM / 02";
                combatUi.Label(new Rect(x + 12, y + 8, 150, 18), weapon, 12, TacticalUi.Muted, TextAnchor.UpperRight);
                combatUi.Label(new Rect(x + 12, y + 26, 78, 42), human.Magazine.ToString("00"), 30, human.Magazine == 0 ? TacticalUi.Enemy : TacticalUi.Text, TextAnchor.MiddleRight);
                combatUi.Label(new Rect(x + 98, y + 37, 64, 28), "/ " + human.Reserve.ToString("000"), 18, TacticalUi.Muted, TextAnchor.MiddleRight);
                bool reloading = actors != null && actors.Actions.IsReloading(session.HumanEntityId);
                if (reloading)
                {
                    float phase = actors!.Actions.ReloadPhaseAtTick(session.HumanEntityId, snap?.CompletedSimulationTick ?? 0);
                    combatUi.Label(new Rect(x, y - 32, 174, 26), "RELOADING", 14, TacticalUi.Accent, TextAnchor.MiddleRight);
                    TacticalUi.Fill(new Rect(x + 12, y + 70, 150, 2), TacticalUi.Raised);
                    TacticalUi.Fill(new Rect(x + 12, y + 70, 150 * Mathf.Clamp01(phase), 2), TacticalUi.Accent);
                }
                else if (human.Magazine == 0)
                {
                    string reload = input.Bindings.TryGetControl("Reload", out string control) ? TacticalUi.ControlLabel(control) : "R";
                    combatUi.Label(new Rect(x - 30, y - 30, 204, 26), human.Reserve > 0 ? reload + "  RELOAD" : "OUT OF AMMUNITION", 14, TacticalUi.Accent, TextAnchor.MiddleRight);
                }
                DrawCompass(a, human.AimYaw);
                DrawKillFeed(a);
                // Analytic center reticle. ADS uses the physical weapon sight instead.
                if (!input.AimHeld)
                {
                    float cx = combatUi.Width * .5f, cy = combatUi.Height * .5f;
                    ReticleStroke(new Rect(cx - 14, cy - 1, 7, 2));
                    ReticleStroke(new Rect(cx + 7, cy - 1, 7, 2));
                    ReticleStroke(new Rect(cx - 1, cy - 14, 2, 7));
                    ReticleStroke(new Rect(cx - 1, cy + 7, 2, 7));
                }
            }
            finally { combatUi.End(); }
        }

        private void DrawTacticalMap(Rect safe, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            const float size = 122, range = 42;
            Rect map = new Rect(safe.x, safe.yMax - size, size, size);
            GUI.BeginGroup(map);
            try
            {
                TacticalUi.Fill(new Rect(0, 0, size, size), new Color(.025f, .065f, .08f, .88f));
                for (int i = 1; i < 4; i++)
                {
                    TacticalUi.Fill(new Rect(i * size / 4, 0, 1, size), new Color(.45f, .65f, .7f, .12f));
                    TacticalUi.Fill(new Rect(0, i * size / 4, size, 1), new Color(.45f, .65f, .7f, .12f));
                }
                float scale = size / (range * 2);
                for (int i = 0; i < hudBoxes.Count; i++)
                {
                    var box = hudBoxes[i];
                    if (!box.Gameplay || box.Size.Y < .3f) continue;
                    float cx = size * .5f + (box.Center.X - human.Feet.X) * scale;
                    float cy = size * .5f - (box.Center.Z - human.Feet.Z) * scale;
                    Rect r = new Rect(cx - box.Size.X * scale * .5f, cy - box.Size.Z * scale * .5f, box.Size.X * scale, box.Size.Z * scale);
                    if (r.xMax < 0 || r.yMax < 0 || r.x > size || r.y > size) continue;
                    TacticalUi.Fill(r, new Color(.38f, .49f, .51f, .7f));
                }
                if (snapshot != null && session?.Lifecycle != null)
                    for (int i = 0; i < snapshot.Participants.Count; i++)
                    {
                        var participant = snapshot.Participants[i];
                        if (!participant.Alive || participant.Team != human.Team || participant.IsHuman) continue;
                        if (!session.Lifecycle.TryGetActorState(participant.EntityId, out var ally)) continue;
                        float px = size * .5f + (ally.Feet.X - human.Feet.X) * scale;
                        float py = size * .5f - (ally.Feet.Z - human.Feet.Z) * scale;
                        if (px > 3 && px < size - 3 && py > 3 && py < size - 3)
                            TacticalUi.Fill(new Rect(px - 2, py - 2, 4, 4), TacticalUi.Ally);
                    }
                float center = size * .5f;
                Vector2 forward = new Vector2(Mathf.Sin(human.AimYaw), -Mathf.Cos(human.AimYaw));
                Vector2 side = new Vector2(-forward.y, forward.x);
                Vector2 tip = new Vector2(center, center) + forward * 7;
                TacticalUi.Line(tip, new Vector2(center, center) - forward * 4 + side * 4, TacticalUi.Text, 2);
                TacticalUi.Line(tip, new Vector2(center, center) - forward * 4 - side * 4, TacticalUi.Text, 2);
                combatUi.Label(new Rect(size - 19, 4, 16, 18), "N", 12, TacticalUi.Text, TextAnchor.UpperCenter);
            }
            finally { GUI.EndGroup(); }
            float x = map.xMax + 12, y = safe.yMax - 150;
            TacticalUi.Fill(new Rect(x - 6, y - 4, 150, 154), new Color(.02f, .035f, .04f, .82f));
            combatUi.Label(new Rect(x, y, 136, 20), "YOUR SQUAD", 12, TacticalUi.Ally);
            int row = 0;
            if (snapshot != null)
                for (int i = 0; i < snapshot.Participants.Count; i++)
                {
                    var participant = snapshot.Participants[i];
                    if (participant.Team != human.Team || participant.IsHuman) continue;
                    string name = participant.DisplayName.Length > 14 ? participant.DisplayName.Substring(0, 13) + "…" : participant.DisplayName;
                    combatUi.Label(new Rect(x, y + 20 + row * 18, 140, 18), (participant.Alive ? "+  " : "-  ") + name, 12, participant.Alive ? TacticalUi.Text : TacticalUi.Muted);
                    row++;
                }
            Color health = human.Health <= 25 ? TacticalUi.Enemy : TacticalUi.Text;
            combatUi.Label(new Rect(x, safe.yMax - 38, 66, 36), human.Health.ToString(), 28, health);
            combatUi.Label(new Rect(x + 65, safe.yMax - 24, 58, 24), "HEALTH", 12, TacticalUi.Muted);
            TacticalUi.Fill(new Rect(x, safe.yMax - 2, 124, 2), new Color(.05f, .08f, .09f, .7f));
            TacticalUi.Fill(new Rect(x, safe.yMax - 2, 124 * Mathf.Clamp01(human.Health / 100f), 2), health);
        }

        private void DrawCompass(Rect safe, float yaw)
        {
            float center = combatUi.Width * .5f;
            float degrees = Mathf.Repeat(yaw * Mathf.Rad2Deg, 360);
            TacticalUi.Fill(new Rect(center - 135, safe.y, 270, 20), new Color(.02f, .035f, .04f, .72f));
            int nearest = Mathf.RoundToInt(degrees / 30) * 30;
            for (int i = -2; i <= 2; i++)
            {
                int bearing = ((nearest + i * 30) % 360 + 360) % 360;
                float offset = Mathf.DeltaAngle(degrees, bearing) * 1.5f;
                string label = bearing == 0 ? "N" : bearing == 90 ? "E" : bearing == 180 ? "S" : bearing == 270 ? "W" : bearing.ToString();
                Rect r = new Rect(center + offset - 20, safe.y, 40, 20);
                combatUi.Label(r, label, 12, TacticalUi.Text, TextAnchor.UpperCenter);
            }
        }

        private void ObserveEliminations(IReadOnlyList<CombatEvent> events)
        {
            var snapshot = session?.TryAcquireSnapshot();
            if (snapshot == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.Kind != CombatEventKind.Death) continue;
                bool seen = false;
                for (int j = 0; j < recentDeathIds.Length; j++) if (recentDeathIds[j].Equals(ev.Id)) { seen = true; break; }
                if (seen) continue;
                recentDeathIds[deathIdCursor++ % recentDeathIds.Length] = ev.Id;
                string? killer = null;
                string victim = "Operator";
                for (int j = 0; j < snapshot.Participants.Count; j++)
                {
                    var p = snapshot.Participants[j];
                    if (ev.Instigator.IsValid && p.EntityId.Equals(ev.Instigator)) killer = p.IsHuman ? "YOU" : p.DisplayName;
                    if (p.EntityId.Equals(ev.Target)) victim = p.IsHuman ? "YOU" : p.DisplayName;
                }
                for (int j = killFeed.Length - 1; j > 0; j--) { killFeed[j] = killFeed[j - 1]; killFeedUntil[j] = killFeedUntil[j - 1]; }
                killFeed[0] = KillFeedText.Format(killer, victim);
                killFeedUntil[0] = Time.unscaledTime + 6;
            }
        }

        private void DrawKillFeed(Rect safe)
        {
            for (int i = 0; i < killFeed.Length; i++)
            {
                if (killFeedUntil[i] <= Time.unscaledTime) continue;
                Rect r = new Rect(safe.xMax - 230, safe.y + i * 24, 230, 22);
                TacticalUi.Fill(r, new Color(.02f, .035f, .04f, .74f));
                combatUi.Label(new Rect(r.x + 6, r.y, r.width - 12, r.height), killFeed[i], 12, TacticalUi.Text, TextAnchor.MiddleRight);
            }
        }

        private static void ReticleStroke(Rect rect)
        {
            TacticalUi.Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), TacticalUi.Background);
            TacticalUi.Fill(rect, TacticalUi.Text);
        }

        private void DrawAccessibilityOverlays()
        {
            if (muzzleFlashRemaining > 0f && !reducedFlash)
            {
                float alpha = Mathf.Clamp01(muzzleFlashRemaining / 0.06f) * 0.35f;
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 0.85f, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            if (subtitlesEnabled && subtitleRemaining > 0f && !string.IsNullOrEmpty(subtitleText))
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 160f, Screen.height - 178f * hud.Settings.UiScale, 320f, 28f),
                    subtitleText,
                    GuiStyle());
            }
        }

        private static GUIStyle? cached;
        private static GUIStyle GuiStyle()
        {
            if (cached != null) return cached;
            cached = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = Color.white }
            };
            return cached;
        }

        private void OnDestroy()
        {
            ClearPresentation();
            analyticFxPresenter = null;
            audioOutput?.Detach();
            audioOutput = null;
        }
    }
}
