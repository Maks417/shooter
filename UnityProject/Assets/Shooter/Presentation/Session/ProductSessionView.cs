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
        private SynthAudioMixer? mixer;
        private ProductAudioEventRouter? audio;
        private readonly AnalyticFxPool analyticFxPool = new AnalyticFxPool();
        private readonly FirstPersonCameraBob cameraBob = new FirstPersonCameraBob();
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
        private readonly string[] scoreFeedLabels = new string[ScoreBadgeVisual.MaxFeedRows];
        private readonly uint[] scoreFeedPoints = new uint[ScoreBadgeVisual.MaxFeedRows];
        private readonly float[] scoreFeedUntil = new float[ScoreBadgeVisual.MaxFeedRows];
        private readonly List<LocalScoreAward> scoreAwardDrain = new List<LocalScoreAward>(8);
        private float scoreRibbonUntil;
        private uint scoreRibbonPoints;
        private float ammoScoreUntil;
        private uint ammoScorePoints;
        private int lastSeenHealth = 100;
        private float damagePulseUntil;
        private float hitMarkerUntil;
        private float damageFlashUntil;
        // T085 incoming-damage direction markers: attacker position at the hit, held in world space so the
        // marker swings as the player turns toward it.
        private readonly ulong[] damageSourceIds = new ulong[CombatHudVisual.MaxDamageIndicators];
        private readonly float[] damageSourceX = new float[CombatHudVisual.MaxDamageIndicators];
        private readonly float[] damageSourceZ = new float[CombatHudVisual.MaxDamageIndicators];
        private readonly float[] damageSourceUntil = new float[CombatHudVisual.MaxDamageIndicators];
        // ADR-058: temporary enemy gunfire blips on the tactical map (ShotAccepted only; silent enemies stay hidden).
        private readonly ulong[] gunfireRevealIds = new ulong[CombatHudVisual.MaxGunfireReveals];
        private readonly float[] gunfireRevealX = new float[CombatHudVisual.MaxGunfireReveals];
        private readonly float[] gunfireRevealZ = new float[CombatHudVisual.MaxGunfireReveals];
        private readonly float[] gunfireRevealUntil = new float[CombatHudVisual.MaxGunfireReveals];
        private EntityId cameraBobLife;

        public ProductMapView? MapView => mapView;
        public ProductActorPresentation? Actors => actors;
        public ProductVisualReviewOverlay? VisualReview => visualReview;
        public LocalPlayerInput Input => input;
        public bool GameplayFocus => cursorLocked;
        public SynthAudioMixer Mixer => EnsureAudio().mixer;
        public ProductAudioEventRouter Audio => EnsureAudio().audio;
        public bool ReducedFlash => reducedFlash;
        public bool SubtitlesEnabled => subtitlesEnabled;
        public bool ReducedCameraMotion => reducedCameraMotion;

        private (SynthAudioMixer mixer, ProductAudioEventRouter audio) EnsureAudio()
        {
            if (mixer != null && audio != null) return (mixer, audio);
            string? sampleRoot = ResolveCombatAudioRoot();
            mixer = new SynthAudioMixer(CombatAudioBankBaker.Bake(sampleRoot: sampleRoot));
            audio = new ProductAudioEventRouter(mixer);
            return (mixer, audio);
        }

        private static string? ResolveCombatAudioRoot()
        {
            try
            {
                string installRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, Application.isEditor ? "../.." : ".."));
                string content = System.IO.Path.Combine(installRoot, "Content", "Audio", "combat");
                if (System.IO.Directory.Exists(content)
                    && System.IO.File.Exists(System.IO.Path.Combine(content, CombatAudioSamplePaths.ManifestFileName)))
                    return content;
                string streaming = System.IO.Path.Combine(Application.streamingAssetsPath, "Audio", "combat");
                if (System.IO.Directory.Exists(streaming)
                    && System.IO.File.Exists(System.IO.Path.Combine(streaming, CombatAudioSamplePaths.ManifestFileName)))
                    return streaming;
            }
            catch (System.Exception)
            {
                // Fall through to BCL path walk / generated bank.
            }
            return CombatAudioSamplePaths.ResolveCombatSampleRoot();
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
            adsFovDegrees = AdsOpticVisual.AdsFieldOfViewDegrees(
                hud.Settings.HipFov, ProductAuthorityActionView.RifleSlot);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = QualitySettings.GetQualityLevel() == 2;
            camera.cullingMask &= ~(1 << ProductMenuScene.StageLayer);

            // Bodycam daylight key: warm hard sun. GeneratedSky trilight owns outdoor fill after map install.
            var sun = new GameObject("Product sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            // Warm key, mid elevation + lateral azimuth: longer readable hard shadows on Low (URP Hard on).
            // Do not force Flat ambient — ProductMapView/GeneratedSky Trilight owns outdoor fill after install.
            sun.transform.rotation = Quaternion.Euler(44f, -32f, 0f);
            sun.color = new Color(1f, 0.94f, 0.84f, 1f);
            sun.intensity = 1.32f;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.85f;
            sun.cullingMask &= ~(1 << ProductMenuScene.StageLayer);
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
            var audioPair = EnsureAudio();
            audioOutput = UnitySynthAudioOutput.Attach(gameObject, audioPair.mixer);
            UserSettingsAudioBinding.ApplyGains(UserSettings.Defaults, audioPair.mixer);
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
            UserSettingsAudioBinding.ApplyGains(settings, EnsureAudio().mixer);
            string qualityName = settings.QualityTier < QualitySettings.names.Length
                ? QualitySettings.names[settings.QualityTier]
                : "Low";
            hud.ApplySettings(new HudSettings(
                settings.UiScale,
                settings.HipFovDegrees,
                qualityName,
                settings.ReducedCameraMotion,
                settings.Contrast));
            adsFovDegrees = AdsOpticVisual.AdsFieldOfViewDegrees(
                settings.HipFovDegrees, ProductAuthorityActionView.RifleSlot);
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

        private void ApplyPresentationFov(float adsPresentationBlend, byte weaponSlot)
        {
            if (camera == null) return;
            float hip = hud.Settings.HipFov;
            if (reducedCameraMotion)
            {
                camera.fieldOfView = hip;
                return;
            }
            float ads = AdsOpticVisual.AdsFieldOfViewDegrees(hip, weaponSlot);
            adsFovDegrees = ads;
            camera.fieldOfView = AdsOpticVisual.BlendedFieldOfViewDegrees(hip, ads, adsPresentationBlend);
        }

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
            System.Array.Clear(scoreFeedUntil, 0, scoreFeedUntil.Length);
            scoreRibbonUntil = 0f;
            ammoScoreUntil = 0f;
            scoreAwardDrain.Clear();
            cameraBob.Reset();
            hitMarkerUntil = 0f;
            damageFlashUntil = 0f;
            damagePulseUntil = 0f;
            for (int i = 0; i < damageSourceUntil.Length; i++) damageSourceUntil[i] = 0f;
            cameraBobLife = default;
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
                    && session.PreMatchTicksRemaining <= 0 // pre-match countdown: no movement/fire/look
                    && human.Alive
                    && cursorLocked
                    && Application.isFocused;
                input.CaptureFrame(acceptsGameplayInput);
                aimHeld = acceptsGameplayInput && input.AimHeld;
                listenerFeet = human.Feet;
                hasListener = true;
                if (camera != null)
                {
                    bool sliding = false;
                    float eye = human.Alive ? 1.65f : 0.4f;
                    if (session.TryGetMovementView(human.EntityId, out var movement))
                    {
                        eye = movement.EyeHeight;
                        sliding = movement.Sliding;
                    }
                    if (!human.EntityId.Equals(cameraBobLife) || !human.Alive)
                    {
                        cameraBob.Reset();
                        cameraBobLife = human.EntityId;
                    }
                    float dtBob = Time.unscaledDeltaTime;
                    if (session.PauseState == SessionPauseState.Running && human.Alive)
                        cameraBob.Step(human.Feet, dtBob, sliding);
                    else if (!human.Alive)
                        cameraBob.Reset();

                    // Prefer last Sync's equip-gated ADS so FOV/bob track the optic raise, not a binary click.
                    float adsBlend = actors != null ? actors.LocalAdsPresentationBlend : (aimHeld ? 1f : 0f);
                    Float3 bobLocal = cameraBob.LocalOffsetMeters(adsBlend, reducedCameraMotion, human.Alive);
                    float roll = cameraBob.RollDegrees(adsBlend, reducedCameraMotion, human.Alive);
                    float shake = FirstPersonHitFeedback.DamageShakeMeters(
                        damageFlashUntil - Time.unscaledTime, reducedCameraMotion, reducedFlash);
                    if (shake > 0f && adsBlend < 0.95f)
                    {
                        // Presentation-only translation; does not mutate aim input / authority pose.
                        bobLocal = new Float3(bobLocal.X + shake * 0.35f, bobLocal.Y + shake, bobLocal.Z);
                    }

                    float cameraYaw = acceptsGameplayInput ? input.Yaw : human.AimYaw;
                    float cameraPitch = acceptsGameplayInput ? input.Pitch : human.AimPitch;
                    var rotation = Quaternion.Euler(cameraPitch * Mathf.Rad2Deg, cameraYaw * Mathf.Rad2Deg, roll);
                    camera.transform.rotation = rotation;
                    var basePos = new Vector3(human.Feet.X, human.Feet.Y + eye, human.Feet.Z);
                    camera.transform.position = basePos + rotation * new Vector3(bobLocal.X, bobLocal.Y, bobLocal.Z);
                    ApplyPresentationFov(adsBlend, actors != null ? actors.LocalVisibleWeaponSlot : human.ActiveWeaponSlot);
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
                if (shellOwned) ObserveLocalScoreAwards();

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
                    // Refresh FOV after Sync so this frame's optic raise matches the viewmodel blend.
                    if (camera != null && session.Lifecycle != null
                        && session.Lifecycle.TryGetActorState(session.HumanEntityId, out var fovHuman) && fovHuman.Alive)
                        ApplyPresentationFov(actors.LocalAdsPresentationBlend, actors.LocalVisibleWeaponSlot);
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

        private void NotifyAccessibilityCue(CombatEvent ev)
        {
            EntityId local = session != null && session.HumanEntityId.IsValid ? session.HumanEntityId : default;
            if (FirstPersonHitFeedback.IsLocalShot(ev.Kind, ev.Instigator, local))
            {
                // Cosmetic local view flash only; bot/remote ShotAccepted must not flash the screen.
                // ReducedFlash suppresses the overlay while preserving shot audio and world muzzle proxies.
                if (!reducedFlash) muzzleFlashRemaining = 0.09f;
                if (subtitlesEnabled) ShowSubtitle("Shot");
            }
            else if (ev.Kind == CombatEventKind.Impact || ev.Kind == CombatEventKind.DamageApplied)
            {
                if (subtitlesEnabled) ShowSubtitle(ev.Kind == CombatEventKind.Impact ? "Impact" : "Hit");
            }
            else if (ev.Kind == CombatEventKind.ReloadStarted && subtitlesEnabled)
                ShowSubtitle("Reloading");
            else if (ev.Kind == CombatEventKind.ReloadCompleted && subtitlesEnabled)
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
                // Push the proxy slightly past the muzzle so the flash clears the viewmodel.
                var muzzle = new Float3(
                    shot.Origin.X + dir.X * 0.18f,
                    shot.Origin.Y + dir.Y * 0.18f,
                    shot.Origin.Z + dir.Z * 0.18f);
                analyticFxPool.TrySpawn(FxKind.MuzzleFlash, muzzle, dir);
                return;
            }

            if (ev.Kind == CombatEventKind.Impact && ev.Payload is ImpactPayload impact)
            {
                analyticFxPool.TrySpawn(FxKind.ImpactSpark, impact.Position, impact.Normal);
                // Body-region impacts get a denser second spark (Warface readable hit feedback).
                if (impact.RegionId >= 1)
                    analyticFxPool.TrySpawn(FxKind.ImpactSpark, impact.Position, impact.Normal);
                // Smoke more often on hits; still capped by MaxSmokeProxies.
                if (impact.RegionId >= 1 || (ev.Id.Ordinal % 2) == 0)
                    analyticFxPool.TrySpawn(FxKind.SmokeProxy, impact.Position, impact.Normal);
            }
        }

        /// <summary>Authority-only damage feedback for local player (taken/dealt). No invented hits.</summary>
        private void ObserveHitFeedback(CombatEvent ev)
        {
            if (session == null || !session.HumanEntityId.IsValid) return;
            var local = session.HumanEntityId;
            if (FirstPersonHitFeedback.IsDamageTaken(ev.Kind, ev.Target, local))
            {
                float now = Time.unscaledTime;
                damagePulseUntil = now + CombatHudVisual.DamagePulseSeconds;
                damageFlashUntil = now + FirstPersonHitFeedback.DamageFlashSeconds;
                if (ev.Payload is DamagePayload dmg)
                    lastSeenHealth = dmg.RemainingHealth;
                RecordDamageSource(ev.Instigator, now);
            }
            else if (FirstPersonHitFeedback.IsDamageDealt(ev.Kind, ev.Instigator, local))
            {
                hitMarkerUntil = Time.unscaledTime + FirstPersonHitFeedback.HitMarkerSeconds;
            }
        }

        private void RecordDamageSource(EntityId instigator, float now)
        {
            if (!instigator.IsValid || session?.Lifecycle == null) return;
            if (!session.Lifecycle.TryGetActorState(instigator, out var attacker)) return;
            int slot = 0;
            for (int i = 0; i < damageSourceIds.Length; i++)
            {
                if (damageSourceIds[i] == instigator.Value && damageSourceUntil[i] > now) { slot = i; break; }
                if (damageSourceUntil[i] < damageSourceUntil[slot]) slot = i;
            }
            damageSourceIds[slot] = instigator.Value;
            damageSourceX[slot] = attacker.Feet.X;
            damageSourceZ[slot] = attacker.Feet.Z;
            damageSourceUntil[slot] = now + CombatHudVisual.DamageIndicatorSeconds;
        }

        /// <summary>
        /// Presentation-only: enemy ShotAccepted briefly marks last-known shot origin on the tactical map.
        /// Does not stream live poses or change AI sensing.
        /// </summary>
        private void ObserveGunfireReveal(CombatEvent ev)
        {
            if (ev.Kind != CombatEventKind.ShotAccepted || ev.Payload is not ShotPayload shot) return;
            if (session == null || !session.HumanEntityId.IsValid || session.Lifecycle == null) return;
            if (!ev.Instigator.IsValid) return;
            bool isLocal = ev.Instigator == session.HumanEntityId;
            if (!session.Lifecycle.TryGetActorState(session.HumanEntityId, out var human)) return;
            if (!session.Lifecycle.TryGetActorState(ev.Instigator, out var shooter)) return;
            bool enemy = shooter.Team != human.Team;
            if (!CombatHudVisual.ShouldRevealGunfire(true, isLocal, enemy)) return;

            float now = Time.unscaledTime;
            int slot = CombatHudVisual.PickGunfireRevealSlot(
                gunfireRevealIds, gunfireRevealUntil, ev.Instigator.Value, now, CombatHudVisual.MaxGunfireReveals);
            gunfireRevealIds[slot] = ev.Instigator.Value;
            gunfireRevealX[slot] = shot.Origin.X;
            gunfireRevealZ[slot] = shot.Origin.Z;
            gunfireRevealUntil[slot] = now + CombatHudVisual.GunfireRevealSeconds;
        }

        private void ClearGunfireReveals()
        {
            for (int i = 0; i < gunfireRevealUntil.Length; i++)
            {
                gunfireRevealIds[i] = 0;
                gunfireRevealX[i] = 0;
                gunfireRevealZ[i] = 0;
                gunfireRevealUntil[i] = 0;
            }
        }

        /// <summary>Red arc segments around the reticle pointing at whoever just hit the local player.</summary>
        private void DrawDamageIndicators(ActorLifecycleState human)
        {
            float now = Time.unscaledTime;
            float cx = combatUi.Width * .5f, cy = combatUi.Height * .5f;
            for (int i = 0; i < damageSourceUntil.Length; i++)
            {
                float alpha = CombatHudVisual.DamageIndicatorAlpha(damageSourceUntil[i] - now);
                if (alpha <= 0.02f) continue;
                float angle = CombatHudVisual.DamageIndicatorAngle(human.AimYaw, human.Feet.X, human.Feet.Z, damageSourceX[i], damageSourceZ[i]);
                var dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                var tangent = new Vector2(-dir.y, dir.x);
                var c = new Vector2(cx, cy) + dir * CombatHudVisual.DamageIndicatorRadius;
                float half = CombatHudVisual.DamageIndicatorLength * .5f;
                var color = new Color(TacticalUi.Enemy.r, TacticalUi.Enemy.g, TacticalUi.Enemy.b, alpha);
                // Slightly curved: two chords meeting at the outer middle read as an arc pointing outward.
                var outer = c + dir * 4f;
                TacticalUi.Line(c - tangent * half - dir * 2f, outer, color, 5f);
                TacticalUi.Line(outer, c + tangent * half - dir * 2f, color, 5f);
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
            var router = EnsureAudio().audio;
            if (!audioAttached || audioSessionId != sid)
            {
                router.AttachSession(sid);
                audioSessionId = sid;
                audioAttached = true;
                audioOutput?.ArmOutput();
            }

            if (combatEvents != null)
            {
                router.ConsumeCombatEvents(combatEvents, listenerFeet, session.HumanEntityId);
                for (int i = 0; i < combatEvents.Count; i++)
                {
                    var ev = combatEvents[i];
                    string key = ev.Id.AuthoritySessionId + ":" + ev.Id.Tick + ":" + ev.Id.EntityId + ":" + ev.Id.Ordinal;
                    if (!accessibilityCueKeys.Add(key)) continue;
                    NotifyAccessibilityCue(ev);
                    ObserveCombatFx(ev);
                    ObserveHitFeedback(ev);
                    ObserveGunfireReveal(ev);
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
            router.TickFootsteps(footstepScratch, listenerFeet, session.CompletedTick);
        }

        private void DetachAudioSession()
        {
            var router = audio;
            if (!audioAttached && (router == null || !router.IsAttached)) return;
            router?.DetachSession();
            audioAttached = false;
            audioSessionId = 0;
            accessibilityCueKeys.Clear();
            ClearGunfireReveals();
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
                CombatEventKind.ShotAccepted => SynthVoiceKind.RifleShot,
                CombatEventKind.Impact => SynthVoiceKind.Impact,
                CombatEventKind.DamageApplied => SynthVoiceKind.DamageTaken,
                CombatEventKind.ReloadStarted => SynthVoiceKind.ReloadStart,
                CombatEventKind.ReloadCompleted => SynthVoiceKind.ReloadComplete,
                CombatEventKind.Death => SynthVoiceKind.Death,
                _ => SynthVoiceKind.Footstep
            };
            EnsureAudio().mixer.TryPlay(voice, distanceMeters, seed);
        }

        public bool PlayUiCue(ulong seed)
        {
            if (subtitlesEnabled) ShowSubtitle("UI");
            return EnsureAudio().audio.PlayUi(seed);
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
                    lastSeenHealth = 100;
                    damagePulseUntil = 0f;
                    damageFlashUntil = 0f;
                    hitMarkerUntil = 0f;
                    cameraBob.Reset();
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
                    // Cinematic KIA: soft letterbox + centered status plate (code-only fills).
                    if (!reducedFlash)
                    {
                        TacticalUi.Fill(new Rect(0, 0, combatUi.Width, combatUi.Height * .16f), new Color(.02f, .03f, .035f, .72f));
                        TacticalUi.Fill(new Rect(0, combatUi.Height * .84f, combatUi.Width, combatUi.Height * .16f), new Color(.02f, .03f, .035f, .72f));
                    }
                    Rect panel = new Rect(combatUi.Width * .5f - 210, combatUi.Height * .5f - 58, 420, 116);
                    TacticalUi.CombatPlate(panel, strong: true);
                    TacticalUi.Fill(new Rect(panel.x, panel.y, 3, panel.height), TacticalUi.Enemy);
                    TacticalUi.Fill(new Rect(panel.x + 3, panel.y, panel.width - 3, 1), new Color(TacticalUi.Enemy.r, TacticalUi.Enemy.g, TacticalUi.Enemy.b, .5f));
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 14, panel.width - 32, 16), "STATUS", 11, TacticalUi.Muted, TextAnchor.UpperLeft);
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 34, panel.width - 32, 32), "KILLED IN ACTION", 26, TacticalUi.Enemy, TextAnchor.UpperLeft);
                    TacticalUi.Fill(new Rect(panel.x + 18, panel.y + 74, 42, 1), TacticalUi.Enemy);
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 82, panel.width - 32, 22), respawn, 15, TacticalUi.Text, TextAnchor.UpperLeft);
                    return;
                }
                // Read the current life again for definite assignment; never retain an authority-owned object.
                if (!session.Lifecycle!.TryGetActorState(session.HumanEntityId, out var human)) return;
                if (human.Health < lastSeenHealth) damagePulseUntil = Time.unscaledTime + CombatHudVisual.DamagePulseSeconds;
                lastSeenHealth = human.Health;
                float flash = FirstPersonHitFeedback.DamageFlashStrength(damageFlashUntil - Time.unscaledTime, reducedFlash);
                if (flash > 0.01f) TacticalUi.DamagePlateFlash(combatUi.Width, combatUi.Height, flash);
                DrawHealthVignette(human.Health);
                DrawDamageIndicators(human);
                DrawTacticalMap(a, human, snap);
                DrawAmmoPanel(a, human, snap);
                DrawAmmoScorePopup(a);
                DrawCompass(a, human.AimYaw);
                DrawKillFeed(a);
                DrawScoreFeed(a);
                DrawScoreRibbon(a);
                float cx = combatUi.Width * .5f, cy = combatUi.Height * .5f;
                float adsPresent = actors != null ? actors.LocalAdsPresentationBlend : (input.AimHeld ? 1f : 0f);
                byte opticSlot = actors != null ? actors.LocalVisibleWeaponSlot : human.ActiveWeaponSlot;
                if (AdsOpticVisual.UsesScopedOptic(opticSlot))
                    TacticalUi.AdsScope(combatUi.Width, combatUi.Height, AdsOpticVisual.ScopeMaskOpacity(adsPresent));
                else if (AdsOpticVisual.UsesIronSightOptic(opticSlot))
                    TacticalUi.AdsIronVignette(combatUi.Width, combatUi.Height, AdsOpticVisual.IronVignetteOpacity(adsPresent));
                // Hip reticle yields once the optic/irons take the aim picture.
                if (!AdsOpticVisual.HideHipReticle(adsPresent))
                {
                    const float arm = 5f, gap = 3.5f;
                    TacticalUi.ReticleArm(new Rect(cx - gap - arm, cy - 0.5f, arm, 1));
                    TacticalUi.ReticleArm(new Rect(cx + gap, cy - 0.5f, arm, 1));
                    TacticalUi.ReticleArm(new Rect(cx - 0.5f, cy - gap - arm, 1, arm));
                    TacticalUi.ReticleArm(new Rect(cx - 0.5f, cy + gap, 1, arm));
                }
                float markerAlpha = FirstPersonHitFeedback.HitMarkerAlpha(hitMarkerUntil - Time.unscaledTime);
                if (markerAlpha > 0.02f) TacticalUi.HitMarker(cx, cy, markerAlpha);
            }
            finally { combatUi.End(); }
        }

        private void DrawAmmoPanel(Rect safe, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            const float w = 152f, h = 62f;
            float x = safe.xMax - w, y = safe.yMax - h;
            TacticalUi.CombatPlate(new Rect(x, y, w, h), accentBottom: true);
            string weapon = human.ActiveWeaponSlot switch
            {
                1 => "SIDEARM",
                2 => "KNIFE",
                3 => "GRENADE",
                _ => "RIFLE"
            };
            combatUi.Label(new Rect(x + 8, y + 4, w - 16, 11), weapon, 9, TacticalUi.Muted, TextAnchor.UpperRight);
            bool knife = human.ActiveWeaponSlot == 2;
            bool grenade = human.ActiveWeaponSlot == 3;
            Color magColor = !knife && human.Magazine == 0 ? TacticalUi.Enemy : TacticalUi.Text;
            // Mag dominates; reserve stays quieter (BFV/Warface hierarchy). Knife shows an em dash; grenade is 1/0.
            string magText = knife ? "--" : (grenade ? human.Magazine.ToString() : human.Magazine.ToString("00"));
            string reserveText = knife ? "--" : (grenade ? human.Reserve.ToString() : human.Reserve.ToString("000"));
            combatUi.Label(new Rect(x + 6, y + 14, 72, 32), magText, 30, magColor, TextAnchor.MiddleRight);
            combatUi.Label(new Rect(x + 80, y + 20, 12, 14), "/", 11, TacticalUi.Muted, TextAnchor.MiddleCenter);
            combatUi.Label(new Rect(x + 92, y + 28, 50, 16), reserveText, 13, TacticalUi.Muted, TextAnchor.MiddleLeft);
            bool reloading = !knife && !grenade && actors != null && actors.Actions.IsReloading(session!.HumanEntityId);
            float trackX = x + 8, trackY = y + h - 7, trackW = w - 16, trackH = 2.5f;
            TacticalUi.Fill(new Rect(trackX, trackY, trackW, trackH), new Color(.06f, .08f, .09f, .65f));
            TacticalUi.Edge(new Rect(trackX, trackY, trackW, trackH));
            if (reloading)
            {
                float phase = Mathf.Clamp01(actors!.Actions.ReloadPhaseAtTick(session.HumanEntityId, snapshot?.CompletedSimulationTick ?? 0));
                combatUi.Label(new Rect(x, y - 14, w, 14), "RELOADING", 10, TacticalUi.Accent, TextAnchor.MiddleRight);
                float fillW = trackW * phase;
                TacticalUi.AccentBar(new Rect(trackX, trackY, fillW, trackH), TacticalUi.Accent);
                if (fillW > 2f) TacticalUi.Fill(new Rect(trackX + fillW - 2f, trackY - 1, 2, trackH + 2), TacticalUi.Text);
            }
            else if (!knife && human.Magazine == 0)
            {
                string reload = input.Bindings.TryGetControl("Reload", out string control) ? TacticalUi.ControlLabel(control) : "R";
                string hint = grenade ? "EMPTY" : (human.Reserve > 0 ? reload + "  RELOAD" : "OUT OF AMMO");
                combatUi.Label(new Rect(x - 20, y - 14, w + 20, 14), hint, 10, TacticalUi.Accent, TextAnchor.MiddleRight);
            }
        }

        private void DrawTacticalMap(Rect safe, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            const float size = 108, range = 42;
            Rect map = new Rect(safe.x, safe.yMax - size, size, size);
            TacticalUi.CombatPlate(new Rect(map.x - 1, map.y - 1, map.width + 2, map.height + 2));
            GUI.BeginGroup(map);
            try
            {
                TacticalUi.Fill(new Rect(0, 0, size, size), new Color(.03f, .05f, .055f, .55f));
                // Quiet cross only — no quarter grid noise.
                TacticalUi.Fill(new Rect(size * .5f, 8, 1, size - 16), new Color(.45f, .6f, .65f, .06f));
                TacticalUi.Fill(new Rect(8, size * .5f, size - 16, 1), new Color(.45f, .6f, .65f, .06f));
                float scale = size / (range * 2);
                for (int i = 0; i < hudBoxes.Count; i++)
                {
                    var box = hudBoxes[i];
                    if (!box.Gameplay || box.Size.Y < .3f) continue;
                    float cx = size * .5f + (box.Center.X - human.Feet.X) * scale;
                    float cy = size * .5f - (box.Center.Z - human.Feet.Z) * scale;
                    Rect r = new Rect(cx - box.Size.X * scale * .5f, cy - box.Size.Z * scale * .5f, box.Size.X * scale, box.Size.Z * scale);
                    if (r.xMax < 0 || r.yMax < 0 || r.x > size || r.y > size) continue;
                    TacticalUi.Fill(r, new Color(.48f, .58f, .60f, .72f));
                }
                if (snapshot != null && session?.Lifecycle != null)
                    for (int i = 0; i < snapshot.Participants.Count; i++)
                    {
                        var participant = snapshot.Participants[i];
                        if (!participant.Alive || participant.Team != human.Team || participant.IsHuman) continue;
                        if (!session.Lifecycle.TryGetActorState(participant.EntityId, out var ally)) continue;
                        float px = size * .5f + (ally.Feet.X - human.Feet.X) * scale;
                        float py = size * .5f - (ally.Feet.Z - human.Feet.Z) * scale;
                        if (px > 4 && px < size - 4 && py > 4 && py < size - 4)
                            DrawAllyMarker(px, py, ally.AimYaw);
                    }
                float now = Time.unscaledTime;
                for (int i = 0; i < gunfireRevealUntil.Length; i++)
                {
                    float alpha = CombatHudVisual.GunfireRevealAlpha(gunfireRevealUntil[i] - now);
                    if (alpha <= 0.02f) continue;
                    float px = size * .5f + (gunfireRevealX[i] - human.Feet.X) * scale;
                    float py = size * .5f - (gunfireRevealZ[i] - human.Feet.Z) * scale;
                    if (px > 4 && px < size - 4 && py > 4 && py < size - 4)
                        DrawGunfireRevealMarker(px, py, alpha);
                }
                DrawSelfMarker(size * .5f, human.AimYaw);
                combatUi.Label(new Rect(size - 16, 2, 14, 14), "N", 11, TacticalUi.Muted, TextAnchor.UpperCenter);
            }
            finally { GUI.EndGroup(); }

            float squadX = map.xMax + 8, squadTop = safe.yMax - 138;
            int allies = 0;
            if (snapshot != null)
                for (int i = 0; i < snapshot.Participants.Count; i++)
                {
                    var p = snapshot.Participants[i];
                    if (p.Team == human.Team && !p.IsHuman) allies++;
                }
            float squadH = 14 + allies * 13 + 36;
            TacticalUi.CombatPlate(new Rect(squadX - 4, squadTop - 2, 128, squadH));
            TacticalUi.Fill(new Rect(squadX - 4, squadTop - 2, 2, squadH), TacticalUi.Ally);
            combatUi.Label(new Rect(squadX, squadTop, 116, 11), "SQUAD", 9, TacticalUi.Ally);
            int row = 0;
            if (snapshot != null)
                for (int i = 0; i < snapshot.Participants.Count; i++)
                {
                    var participant = snapshot.Participants[i];
                    if (participant.Team != human.Team || participant.IsHuman) continue;
                    string name = participant.DisplayName.Length > 11 ? participant.DisplayName.Substring(0, 10) + "…" : participant.DisplayName;
                    float ry = squadTop + 13 + row * 13;
                    // Alive/dead cue stays color-independent (+ / -).
                    string mark = participant.Alive ? "+  " : "-  ";
                    combatUi.Label(new Rect(squadX, ry, 110, 12), mark + name, 9,
                        participant.Alive ? TacticalUi.Text : TacticalUi.Muted);
                    row++;
                }

            bool danger = CombatHudVisual.IsHealthDanger(human.Health);
            Color healthInk = danger ? TacticalUi.Enemy
                : CombatHudVisual.IsHealthWarning(human.Health) ? TacticalUi.Accent
                : TacticalUi.Text;
            float pulse = danger
                ? CombatHudVisual.CriticalBarPulse(Time.unscaledTime, reducedFlash, reducedCameraMotion)
                : 1f;
            float hx = squadX, hy = safe.yMax - 34;
            float barH = CombatHudVisual.HealthBarHeight;
            combatUi.Label(new Rect(hx, hy - 2, 44, 26), human.Health.ToString(), 24, healthInk, TextAnchor.MiddleLeft);
            combatUi.Label(new Rect(hx + 42, hy + 8, 60, 11), "HP", 9, TacticalUi.Muted, TextAnchor.MiddleLeft);
            float barW = 118f, gap = 2f;
            float segW = (barW - gap * (CombatHudVisual.HealthSegments - 1)) / CombatHudVisual.HealthSegments;
            float barY = safe.yMax - barH;
            // Dual-layer track under segmented fill.
            TacticalUi.Fill(new Rect(hx, barY, barW, barH), new Color(.05f, .07f, .08f, .7f));
            TacticalUi.Fill(new Rect(hx, barY, barW, 1), new Color(1f, 1f, 1f, .05f));
            for (int s = 0; s < CombatHudVisual.HealthSegments; s++)
            {
                float sx = hx + s * (segW + gap);
                float fill = CombatHudVisual.SegmentFill(human.Health, s, segW);
                if (fill <= 0f) continue;
                var ink = new Color(healthInk.r, healthInk.g, healthInk.b, healthInk.a * pulse);
                TacticalUi.Fill(new Rect(sx, barY, fill, barH), ink);
                if (fill > 1.5f)
                    TacticalUi.Fill(new Rect(sx, barY, Mathf.Min(1.5f, fill), barH), new Color(1f, 1f, 1f, .16f * pulse));
            }

            // ADR-060 tightness bar above HP; authority fraction via LocalMovementView (C0 unchanged).
            float tightness01 = 1f;
            if (session != null && session.TryGetMovementView(session.HumanEntityId, out var movement))
                tightness01 = movement.Tightness01;
            float tightH = CombatHudVisual.TightnessBarHeight;
            float tightY = barY - CombatHudVisual.TightnessBarGap - tightH;
            TacticalUi.Fill(new Rect(hx, tightY, barW, tightH), new Color(.05f, .07f, .08f, .7f));
            float tightFill = CombatHudVisual.TightnessFillWidth(tightness01, barW);
            if (tightFill > 0f)
            {
                var tightInk = tightness01 <= 0.25f ? TacticalUi.Accent : TacticalUi.Ally;
                TacticalUi.Fill(new Rect(hx, tightY, tightFill, tightH), tightInk);
            }
            combatUi.Label(new Rect(hx + barW + 4, tightY - 2, 28, 10), "STA", 8, TacticalUi.Muted, TextAnchor.MiddleLeft);
        }

        private static void DrawAllyMarker(float px, float py, float aimYaw)
        {
            // Outlined ally pip + Fill facing tick (no Line inside BeginGroup — IMGUI matrix clip).
            TacticalUi.Fill(new Rect(px - 3.5f, py - 3.5f, 7, 7), new Color(0.02f, 0.03f, 0.035f, 0.9f));
            TacticalUi.Fill(new Rect(px - 2f, py - 2f, 4, 4), TacticalUi.Ally);
            TacticalUi.Fill(new Rect(px - 0.5f, py - 0.5f, 1, 1), TacticalUi.Text);
            DrawFacingTick(px, py, aimYaw, TacticalUi.Ally, TacticalUi.Text, 6f);
        }

        /// <summary>Local player at map center: Fill pip + facing tick (visible inside BeginGroup).</summary>
        private static void DrawSelfMarker(float center, float aimYaw)
        {
            TacticalUi.Fill(new Rect(center - 4.5f, center - 4.5f, 9, 9), new Color(0.02f, 0.03f, 0.035f, 0.95f));
            TacticalUi.Fill(new Rect(center - 3f, center - 3f, 6, 6), TacticalUi.Accent);
            TacticalUi.Fill(new Rect(center - 1.5f, center - 1.5f, 3, 3), TacticalUi.Text);
            DrawFacingTick(center, center, aimYaw, TacticalUi.Text, TacticalUi.Accent, 8f);
        }

        /// <summary>Enemy gunfire reveal: plus/diamond made of Fills so it stays distinct from ally squares.</summary>
        private static void DrawGunfireRevealMarker(float px, float py, float alpha)
        {
            var outline = new Color(0.02f, 0.03f, 0.035f, 0.9f * alpha);
            var ink = new Color(TacticalUi.Enemy.r, TacticalUi.Enemy.g, TacticalUi.Enemy.b, alpha);
            var core = new Color(TacticalUi.Text.r, TacticalUi.Text.g, TacticalUi.Text.b, alpha);
            TacticalUi.Fill(new Rect(px - 1.5f, py - 4.5f, 3, 9), outline);
            TacticalUi.Fill(new Rect(px - 4.5f, py - 1.5f, 9, 3), outline);
            TacticalUi.Fill(new Rect(px - 1f, py - 3.5f, 2, 7), ink);
            TacticalUi.Fill(new Rect(px - 3.5f, py - 1f, 7, 2), ink);
            TacticalUi.Fill(new Rect(px - 1f, py - 1f, 2, 2), core);
        }

        private static void DrawFacingTick(float px, float py, float aimYaw, Color outer, Color inner, float length)
        {
            Vector2 fwd = new Vector2(Mathf.Sin(aimYaw), -Mathf.Cos(aimYaw));
            int steps = Mathf.Max(3, Mathf.CeilToInt(length));
            for (int s = 2; s <= steps; s++)
            {
                float t = s;
                float x = px + fwd.x * t;
                float y = py + fwd.y * t;
                float w = s >= steps - 1 ? 2f : 1.5f;
                TacticalUi.Fill(new Rect(x - w * .5f, y - w * .5f, w, w), s >= steps - 1 ? inner : outer);
            }
        }

        private void DrawCompass(Rect safe, float yaw)
        {
            // Sit under the shell score/clock strip (BFV-like top hierarchy). Quiet so world stays first.
            float center = combatUi.Width * .5f;
            float y = safe.y + 40;
            float w = 248, h = 14;
            TacticalUi.CombatPlate(new Rect(center - w * .5f, y, w, h));
            float degrees = Mathf.Repeat(yaw * Mathf.Rad2Deg, 360f);
            TacticalUi.Fill(new Rect(center - 1, y, 2, 2.5f), TacticalUi.Accent);
            int nearest = Mathf.RoundToInt(degrees / 15f) * 15;
            for (int i = -10; i <= 10; i++)
            {
                int bearing = ((nearest + i * 15) % 360 + 360) % 360;
                float offset = Mathf.DeltaAngle(degrees, bearing) * 1.3f;
                if (Mathf.Abs(offset) > w * .5f - 6f) continue;
                float x = center + offset;
                bool cardinal = CombatHudVisual.IsCardinal(bearing);
                float tickH = cardinal ? 5f : (bearing % 30 == 0 ? 3.5f : 2f);
                Color tick = cardinal ? TacticalUi.Text : new Color(TacticalUi.Muted.r, TacticalUi.Muted.g, TacticalUi.Muted.b, 0.65f);
                TacticalUi.Fill(new Rect(x - 0.5f, y + h - tickH - 1f, 1f, tickH), tick);
                if (cardinal || bearing % 30 == 0)
                {
                    combatUi.Label(new Rect(x - 14, y - 1, 28, 12), CombatHudVisual.CardinalOrBearing(bearing),
                        cardinal ? 11 : 9, cardinal ? TacticalUi.Text : TacticalUi.Muted, TextAnchor.UpperCenter);
                }
            }
        }

        private void DrawHealthVignette(int health)
        {
            // Accessibility: reducedFlash softens peak/pulse; muzzle overlay stays fully suppressed separately.
            float strength = CombatHudVisual.HealthVignette(health, damagePulseUntil > Time.unscaledTime, reducedFlash);
            if (strength <= 0.01f) return;
            TacticalUi.EdgeVignette(combatUi.Width, combatUi.Height, strength, TacticalUi.Enemy);
        }

        private void ObserveLocalScoreAwards()
        {
            if (session == null) return;
            scoreAwardDrain.Clear();
            session.DrainLocalScoreAwards(scoreAwardDrain);
            float now = Time.unscaledTime;
            for (int i = 0; i < scoreAwardDrain.Count; i++)
            {
                var award = scoreAwardDrain[i];
                if (award.Points == 0) continue;
                bool isKill = award.Kind == LocalScoreAwardKind.Kill;
                PushScoreFeed(ScoreBadgeVisual.LabelFor(isKill), award.Points, now);
                ammoScorePoints = award.Points;
                ammoScoreUntil = now + ScoreBadgeVisual.AmmoPopupHoldSeconds;
                if (isKill)
                {
                    scoreRibbonPoints = award.Points;
                    scoreRibbonUntil = now + ScoreBadgeVisual.RibbonHoldDuration(reducedFlash);
                }
            }
        }

        private void PushScoreFeed(string label, uint points, float now)
        {
            for (int j = scoreFeedLabels.Length - 1; j > 0; j--)
            {
                scoreFeedLabels[j] = scoreFeedLabels[j - 1];
                scoreFeedPoints[j] = scoreFeedPoints[j - 1];
                scoreFeedUntil[j] = scoreFeedUntil[j - 1];
            }
            scoreFeedLabels[0] = label;
            scoreFeedPoints[0] = points;
            scoreFeedUntil[0] = now + ScoreBadgeVisual.FeedHoldSeconds;
        }

        private void DrawScoreRibbon(Rect safe)
        {
            float remaining = scoreRibbonUntil - Time.unscaledTime;
            float alpha = ScoreBadgeVisual.RibbonAlpha(remaining, reducedFlash);
            if (alpha <= 0.02f) return;
            float w = 220f, h = 36f;
            float x = safe.x + (safe.width - w) * 0.5f;
            float y = safe.y + 78f;
            var banner = new Rect(x, y, w, h);
            TacticalUi.ScoreRibbon(banner, alpha);
            Color ink = TacticalUi.Text;
            ink.a = alpha;
            Color pts = TacticalUi.Accent;
            pts.a = alpha;
            combatUi.Label(new Rect(x, y - 16, w, 16), ScoreBadgeVisual.KillLabel, 12, ink, TextAnchor.MiddleCenter);
            combatUi.Label(new Rect(x, y + h + 2, w, 20), ScoreBadgeVisual.FormatPoints(scoreRibbonPoints), 18, pts, TextAnchor.MiddleCenter);
        }

        private void DrawScoreFeed(Rect safe)
        {
            float now = Time.unscaledTime;
            const float rowH = 16f;
            float baseY = safe.y + safe.height * 0.52f;
            float w = 200f;
            float x = safe.x + (safe.width - w) * 0.5f;
            for (int i = 0; i < scoreFeedLabels.Length; i++)
            {
                float remaining = scoreFeedUntil[i] - now;
                float alpha = ScoreBadgeVisual.FeedAlpha(remaining);
                if (alpha <= 0.02f) continue;
                string label = scoreFeedLabels[i];
                if (string.IsNullOrEmpty(label)) continue;
                Rect r = new Rect(x, baseY + i * (rowH + 2f), w, rowH);
                Color plate = TacticalUi.HudPlate;
                plate.a *= alpha * 0.75f;
                TacticalUi.Fill(r, plate);
                Color ink = TacticalUi.Text;
                ink.a = alpha;
                Color pts = TacticalUi.Accent;
                pts.a = alpha;
                combatUi.Label(new Rect(r.x + 8, r.y, r.width - 56, r.height), label, 11, ink, TextAnchor.MiddleLeft);
                combatUi.Label(new Rect(r.xMax - 52, r.y, 44, r.height), ScoreBadgeVisual.FormatPoints(scoreFeedPoints[i]), 12, pts, TextAnchor.MiddleRight);
            }
        }

        private void DrawAmmoScorePopup(Rect safe)
        {
            float remaining = ammoScoreUntil - Time.unscaledTime;
            float alpha = ScoreBadgeVisual.AmmoPopupAlpha(remaining);
            if (alpha <= 0.02f) return;
            const float w = 152f, h = 62f;
            float x = safe.xMax - w;
            float y = safe.yMax - h - 22f;
            Color ink = TacticalUi.Accent;
            ink.a = alpha;
            combatUi.Label(new Rect(x, y, w, 18), ScoreBadgeVisual.FormatPoints(ammoScorePoints), 16, ink, TextAnchor.MiddleRight);
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
                killFeedUntil[0] = Time.unscaledTime + CombatHudVisual.KillFeedHoldSeconds;
            }
        }

        private void DrawKillFeed(Rect safe)
        {
            float now = Time.unscaledTime;
            const float rowH = 16f;
            const float gap = 2f;
            for (int i = 0; i < killFeed.Length; i++)
            {
                float remaining = killFeedUntil[i] - now;
                float alpha = CombatHudVisual.KillFeedAlpha(remaining);
                if (alpha <= 0.02f) continue;
                string line = killFeed[i];
                Rect r = new Rect(safe.xMax - 216, safe.y + 62 + i * (rowH + gap), 216, rowH);
                Color plate = TacticalUi.HudPlate;
                plate.a *= alpha * 0.8f;
                TacticalUi.Fill(r, plate);
                bool localKiller = KillFeedText.IsLocalKiller(line);
                bool localVictim = KillFeedText.IsLocalVictim(line);
                Color rail = localKiller ? TacticalUi.Ally : (localVictim ? TacticalUi.Enemy : TacticalUi.HudEdge);
                rail.a *= alpha;
                TacticalUi.AccentBar(new Rect(r.x, r.y + 2, 2, r.height - 4), rail);
                if (CombatHudVisual.TrySplitKillFeed(line, out string left, out string right))
                {
                    bool env = string.Equals(right, "ENVIRONMENT", System.StringComparison.Ordinal);
                    Color leftInk = localKiller ? TacticalUi.Ally
                        : env ? TacticalUi.Text : TacticalUi.Text;
                    Color rightInk = localVictim ? TacticalUi.Enemy
                        : env ? TacticalUi.Muted : TacticalUi.Muted;
                    leftInk.a = alpha;
                    rightInk.a = alpha;
                    Color sep = TacticalUi.Muted;
                    sep.a = alpha * 0.85f;
                    combatUi.Label(new Rect(r.x + 8, r.y, 82, r.height), left, 11, leftInk, TextAnchor.MiddleRight);
                    combatUi.Label(new Rect(r.x + 92, r.y, 16, r.height), "›", 11, sep, TextAnchor.MiddleCenter);
                    combatUi.Label(new Rect(r.x + 110, r.y, 100, r.height), right, 11, rightInk, TextAnchor.MiddleLeft);
                }
                else
                {
                    Color ink = localKiller ? TacticalUi.Ally : (localVictim ? TacticalUi.Enemy : TacticalUi.Text);
                    ink.a = alpha;
                    combatUi.Label(new Rect(r.x + 8, r.y, r.width - 14, r.height), line, 11, ink, TextAnchor.MiddleRight);
                }
            }
        }

        private void DrawAccessibilityOverlays()
        {
            if (muzzleFlashRemaining > 0f && !reducedFlash)
            {
                float alpha = Mathf.Clamp01(muzzleFlashRemaining / 0.09f) * 0.42f;
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.95f, 0.75f, alpha);
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
