#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation.Environment;
using Shooter.Presentation.Audio;
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
        private readonly List<ActorLifecycleState> footstepScratch = new List<ActorLifecycleState>(GameContractVersion.MaxParticipants);
        private readonly HashSet<string> accessibilityCueKeys = new HashSet<string>(System.StringComparer.Ordinal);
        private UnitySynthAudioOutput? audioOutput;
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

            var sun = new GameObject("Product sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, 40f, 0f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Hard;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.30f, 0.34f, 1f);
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
            if (reducedFlash) muzzleFlashRemaining = 0f;
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
            if (camera != null && mapView.IsInstalled)
                camera.clearFlags = CameraClearFlags.Skybox;
            visualReview?.SetContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], 0);
            Debug.Log("SHOOTER_PRODUCT_MAP_BOUND map=" + boundMapId + " solids=" + mapView.SolidCount + " packagedVisuals=1");
        }

        public void ClearPresentation()
        {
            DetachAudioSession();
            mapView?.ClearVisuals();
            actors?.ClearAll();
            boundMapId = "";
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
                        movementReader);
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
                if (session.Phase != GameMatchPhase.Playing || session.PauseState != SessionPauseState.Running) return;
                if (session.Lifecycle != null && session.Lifecycle.TryGetActorState(session.HumanEntityId, out var humanHud))
                {
                    GUI.Label(new Rect(24, Screen.height - 72, 640, 24),
                        "HP " + humanHud.Health + "  Ammo " + humanHud.Magazine + "/" + humanHud.Reserve + "  slot " + humanHud.ActiveWeaponSlot,
                        GuiStyle());
                }
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
                    new Rect(Screen.width * 0.5f - 160f, Screen.height - 120f, 320f, 28f),
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
            audioOutput?.Detach();
            audioOutput = null;
        }
    }
}
