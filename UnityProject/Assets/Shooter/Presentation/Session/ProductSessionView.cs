#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Fx;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Input;
using Shooter.Presentation.Menus;
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
    public sealed partial class ProductSessionView : MonoBehaviour
    {
        private IGameSessionView? session;
        private LocalPlayerInput input = new LocalPlayerInput();
        private System.Func<GameOperationResult>? pauseSession;
        private System.Func<EntityId, LocalMovementView?>? movementReader;
        private System.Func<GameOperationResult>? resumeSession;
        private readonly HudLayout hud = new HudLayout();
        private SynthAudioMixer? mixer;
        private ProductAudioEventRouter? audio;
        private const int AudioProbeBudget = 8;
        private int audioProbesRemaining;
        private float nextAcousticProbe;
        private float audioReflection;
        private Vector3 acousticListenerPosition;
        private readonly Dictionary<Vector3Int, (float value, float expires)> audioObstructions =
            new Dictionary<Vector3Int, (float value, float expires)>();
        private readonly AnalyticFxPool analyticFxPool = new AnalyticFxPool();
        private readonly CosmeticGrenadeFlight grenadeFlight = new CosmeticGrenadeFlight();
        private readonly FirstPersonCameraBob cameraBob = new FirstPersonCameraBob();
        private readonly FirstPersonCameraRecoil cameraRecoil = new FirstPersonCameraRecoil();
        // ADR-103 damage feedback v2: view flinch, damage-scaled flash and the URP low-health grade.
        private readonly FirstPersonDamagePunch damagePunch = new FirstPersonDamagePunch();
        private int lastDamageAmount;
        private float damageAudioGain = 1f;
        private float hitGradeUntil;
        private CosmeticGrenadePresenter? grenadePresenter;
        private readonly List<ActorLifecycleState> footstepScratch = new List<ActorLifecycleState>(GameContractVersion.MaxParticipants);
        private readonly HashSet<string> accessibilityCueKeys = new HashSet<string>(System.StringComparer.Ordinal);
        private UnitySynthAudioOutput? audioOutput;
        // ADR-096: menu UI clicks need sound while no match is attached, so they get their own small mixer
        // (same immutable bank) and output; music streams through its own sources.
        private SynthAudioMixer? shellUiMixer;
        private UnitySynthAudioOutput? shellUiOutput;
        private UnityMusicOutput? musicOutput;
        private readonly MusicDirector musicDirector = new MusicDirector();
        private ulong shellUiOrdinal;
        private readonly LocalWeaponCueTracker localWeaponCues = new LocalWeaponCueTracker();
        private readonly SynthVoiceKind[] localCueScratch = new SynthVoiceKind[3];
        private AnalyticFxPresenter? analyticFxPresenter;
        private Camera? camera;
        private AdsScopePipPresenter? scopePip;
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
        private int presentationBindEpoch;
        private Float3? enemySpawn;
        private IReadOnlyList<CollisionBox> hudBoxes = System.Array.Empty<CollisionBox>();
        private bool[] impactMetalBoxes = System.Array.Empty<bool>();
        private bool impactAsphaltGround;
        private const float ImpactBoxSlack = 0.03f;
        private readonly EventId[] recentDeathIds = new EventId[32];
        private int deathIdCursor;
        private readonly KillFeedEntry[] killFeed = new KillFeedEntry[4];
        private readonly float[] killFeedUntil = new float[4];
        private readonly string[] scoreFeedLabels = new string[ScoreBadgeVisual.MaxFeedRows];
        private readonly uint[] scoreFeedPoints = new uint[ScoreBadgeVisual.MaxFeedRows];
        private readonly float[] scoreFeedUntil = new float[ScoreBadgeVisual.MaxFeedRows];
        private readonly List<LocalScoreAward> scoreAwardDrain = new List<LocalScoreAward>(8);
        private float scoreRibbonUntil;
        private float scoreRibbonStart;
        private byte scoreRibbonStreak;
        private uint scoreRibbonPoints;
        private string scoreRibbonLabel = ScoreBadgeVisual.KillLabel;
        private float ammoScoreUntil;
        private uint ammoScorePoints;
        private int lastSeenHealth = 100;
        private float damagePulseUntil;
        private float hitMarkerUntil;
        private bool hitMarkerHeadshot;
        private float explosionShakeUntil;
        private float explosionShakeScale;
        private readonly ulong[] dustLife=new ulong[12];
        private readonly float[] dustDistance=new float[12];
        private readonly float[] killFeedY = new float[4];
        private readonly byte[] killFeedKillerTeam = new byte[4];
        private readonly byte[] killFeedVictimTeam = new byte[4];
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
        /// <summary>Eased camera eye height (ADR-099): crouch/stand glide instead of snapping; authority eye is unchanged.</summary>
        private float presentedEye = -1f;

        public ProductMapView? MapView => mapView;
        public ProductActorPresentation? Actors => actors;

        private WeaponHandlingScale localHandlingScale;

        /// <summary>ADR-108: the local player's career handling buffs mirrored into the first-person view (display only).</summary>
        public WeaponHandlingScale LocalHandling
        {
            get => localHandlingScale;
            set
            {
                localHandlingScale = value;
                if (actors != null) actors.LocalHandling = value;
            }
        }
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
            audio = new ProductAudioEventRouter(mixer) { ObstructionProbe = ProbeAudioObstruction };
            return (mixer, audio);
        }

        public static ProductSessionView Create(IGameSessionControl session, bool shellOwned = false)
        {
            var root = new GameObject("Product session view").AddComponent<ProductSessionView>();
            root.Initialize(session, session.Pause, session.Resume, shellOwned);
            return root;
        }

        private void Initialize(
            IGameSessionView session,
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
            // ADR-103: MSAA/FXAA are owned by ProductPostProcessing.Apply so the saved tier is honoured.
            camera.cullingMask &= ~(1 << ProductMenuScene.StageLayer);
            // ADR-080: code-built post stack; HDR follows the quality tier.
            Shooter.Presentation.Rendering.ProductPostProcessing.Attach(camera);
            scopePip = AdsScopePipPresenter.Attach(camera);

            // Bodycam daylight key: warm hard sun. GeneratedSky trilight owns outdoor fill after map install.
            var sun = new GameObject("Product sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            // Warm key, mid elevation + lateral azimuth: longer readable hard shadows on Low (URP Hard on).
            // Do not force Flat ambient — ProductMapView/GeneratedSky Trilight owns outdoor fill after install.
            sun.transform.rotation = Quaternion.Euler(44f, -32f, 0f);
            sun.color = new Color(1f, 0.94f, 0.84f, 1f);
            sun.intensity = 1.50f;
            // ADR-103: Soft where the tier's URP asset supports it (Balanced/High); Low's asset resolves this to hard.
            // Bias comes from the pipeline asset (per-light values were silently ignored by URP before).
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.92f; // ADR-116: deeper, more defined sun shadows
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
            // ADR-107: read-only sight lookup so every soldier's gun shows the sight the authority issued.
            actors.SightReader = entity => session != null && session.TryGetPrimarySight(entity, out var sight) ? sight : default;
            actors.LocalHandling = localHandlingScale;
            analyticFxPresenter = AnalyticFxPresenter.Create(transform, analyticFxPool);
            grenadePresenter = CosmeticGrenadePresenter.Create(transform, grenadeFlight);
            // ADR-111: the thrown grenade is the packaged prop and bounces off the map collision like the authority one.
            grenadePresenter.PropSource = () => actors != null && actors.TryGetPropVisual(WeaponVisualProfiles.MeshKeyGrenade, out var m, out var mats)
                ? (m, mats) : ((Mesh?)null, System.Array.Empty<Material>());
            grenadeFlight.Probe = GrenadeArcPresenter.Sweep;
            var audioPair = EnsureAudio();
            audioOutput = UnitySynthAudioOutput.Attach(gameObject, audioPair.mixer);
            UserSettingsAudioBinding.ApplyGains(UserSettings.Defaults, audioPair.mixer);
            if (shellOwned)
            {
                shellUiMixer = new SynthAudioMixer(audioPair.mixer.Bank);
                var uiHost = new GameObject("Shell UI audio");
                uiHost.transform.SetParent(transform, false);
                shellUiOutput = UnitySynthAudioOutput.Attach(uiHost, shellUiMixer);
                UserSettingsAudioBinding.ApplyGains(UserSettings.Defaults, shellUiMixer);
                musicOutput = UnityMusicOutput.Create(transform, ResolveMusicFolder());
                musicOutput.SetChannelGain(UserSettingsAudioBinding.MusicGain(UserSettings.Defaults));
            }
            TryEnableVisualReviewFromCommandLine();
            Debug.Log("SHOOTER_PRODUCT_VIEW gpu=" + SystemInfo.graphicsDeviceName + " profile=" + QualitySettings.names[profile] +
                      " shellOwned=" + (shellOwned ? 1 : 0) +
                      " visualReview=" + (visualReview != null ? 1 : 0));
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
            presentationBindEpoch++;
            foreach(var step in BindVerifiedMapSteps(boxes,metadataBytes,mapId,visuals)) { }
        }
        private IEnumerable<int> BindVerifiedMapSteps(IReadOnlyList<CollisionBox> boxes,byte[]? metadataBytes,string mapId,PackagedVisualPayloads visuals)
        {
            if (mapView == null) throw new System.InvalidOperationException("Product map view missing.");
            if (actors == null) throw new System.InvalidOperationException("Product actor presentation missing.");
            if (boxes == null || boxes.Count == 0)
                throw new System.InvalidOperationException("Verified map package solids missing; refusing silent empty/diagnostic visuals.");
            if (visuals == null)
                throw new System.InvalidOperationException("Verified visual package missing; refusing generation fallback.");
            foreach(var step in mapView.InstallSteps(boxes,metadataBytes,mapId??"",visuals))yield return step;
            actors.ClearAll();
            foreach(var step in actors.BindPackagedVisualSteps(visuals))yield return step;
            actors.AmbientVisibility=mapView.SampleActorVisibility;
            actors.CollisionGround=new CollisionSoldierGround(boxes);
            if(analyticFxPresenter!=null)foreach(var step in analyticFxPresenter.BindVerifiedVisualSteps(visuals))yield return step;
            boundMapId = mapId ?? "";
            hudBoxes = boxes;
            impactMetalBoxes = ClassifyImpactMetal(boxes, boundMapId);
            impactAsphaltGround = MapRegistry.TryGet(boundMapId, out var impactMap) && impactMap.GroundSurface == "asphalt";
            enemySpawn = null;
            if (metadataBytes != null)
            {
                var root = MapJsonValue.Parse(metadataBytes).Object;
                if (root.TryGetValue("spawnSlots", out var slots))
                    foreach (var slot in slots.Array)
                    {
                        var v = slot.Object;
                        if (v["team"].Int32 != 2) continue;
                        var xyz = v["positionMeters"].Array;
                        enemySpawn = new Float3((float)xyz[0].Number, (float)xyz[1].Number, (float)xyz[2].Number);
                        break;
                    }
            }
            if (camera != null && mapView.IsInstalled)
                camera.clearFlags = CameraClearFlags.Skybox;
            visualReview?.SetContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], 0);
            Debug.Log("SHOOTER_PRODUCT_MAP_BOUND map=" + boundMapId + " solids=" + mapView.SolidCount + " packagedVisuals=1");
        }

        /// <summary>Cooperative paint points at the existing map/actor binding boundaries.
        /// One Unity upload cannot be preempted; elapsed overruns are diagnosed rather than hidden by progress.</summary>
        public async System.Threading.Tasks.Task BindVerifiedMapAsync(IReadOnlyList<CollisionBox> boxes,
            byte[]? metadata, string mapId, PackagedVisualPayloads visuals, System.Threading.CancellationToken cancel)
        {
            int epoch=++presentationBindEpoch;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            await System.Threading.Tasks.Task.Run(()=>AtmosphereBaker.Sky(MapRegistry.Require(mapId).SkyPreset),cancel);
            try
            {
                using var phases=BindVerifiedMapSteps(boxes,metadata,mapId,visuals).GetEnumerator();
                while(true)
                {
                    cancel.ThrowIfCancellationRequested();
                    if(epoch!=presentationBindEpoch)throw new System.OperationCanceledException();
                    clock.Restart();long before=0;
                    if(!phases.MoveNext())break;
                    long phaseMs=clock.ElapsedMilliseconds-before;
                    if(phaseMs>33)Debug.Log("SHOOTER_PRESENTATION_BIND_INDIVISIBLE_OVERRUN ms="+phaseMs);
                    await System.Threading.Tasks.Task.Yield();
                }
                cancel.ThrowIfCancellationRequested();
            }
            catch
            {
                if(epoch==presentationBindEpoch)ClearPresentation();
                throw;
            }
        }

        public void ClearPresentation()
        {
            presentationBindEpoch++;
            DetachAudioSession();
            analyticFxPool.Clear();
            grenadeFlight.Clear();
            analyticFxPresenter?.ClearVisuals();
            grenadePresenter?.ClearVisuals();
            mapView?.ClearVisuals();
            actors?.ClearAll();
            boundMapId = "";
            hudBoxes = System.Array.Empty<CollisionBox>();
            impactMetalBoxes = System.Array.Empty<bool>();
            impactAsphaltGround = false;
            enemySpawn = null;
            System.Array.Clear(recentDeathIds, 0, recentDeathIds.Length);
            System.Array.Clear(killFeedUntil, 0, killFeedUntil.Length);
            System.Array.Clear(scoreFeedUntil, 0, scoreFeedUntil.Length);
            scoreRibbonUntil = 0f;
            ammoScoreUntil = 0f;
            scoreAwardDrain.Clear();
            cameraBob.Reset();
            cameraRecoil.Reset();
            damagePunch.Reset();
            hitGradeUntil = 0f;
            lastDamageAmount = 0;
            Shooter.Presentation.Rendering.ProductPostProcessing.SetDamageGrade(default);
            audioOutput?.Fx.Reset();
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

            // ADR-112: development builds and the editor toggle the authoritative hitbox overlay with F8.
            if (Debug.isDebugBuild)
            {
                var debugKeyboard = Keyboard.current;
                if (debugKeyboard != null && debugKeyboard.f8Key.wasPressedThisFrame) hitboxOverlay = !hitboxOverlay;
            }

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

            // Lease this frame's combat events before the camera so local recoil lands on the same frame as the
            // shot report and viewmodel kick.
            IReadOnlyList<CombatEvent>? combatEvents = null;
            EventLease? combatLease = null;
            try
            {
                combatLease = session.TryLeaseCombatEvents();
                if (combatLease != null) combatEvents = combatLease.Events;
                bool acceptsGameplayInput = false;
                bool aimHeld = false;
                Float3 listenerFeet = default;
                bool hasListener = false;
                if (session.TryGetActorState(session.HumanEntityId, out var human))
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
                            cameraRecoil.Reset();
                            damagePunch.Reset();
                            cameraBobLife = human.EntityId;
                            presentedEye = -1f;
                        }
                        // Stance changes glide over ~0.1 s (time constant 45 ms); death and spawn snap.
                        if (presentedEye < 0f || !human.Alive) presentedEye = eye;
                        else presentedEye += (eye - presentedEye) * (1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.045f));
                        eye = presentedEye;
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

                        if(!reducedCameraMotion&&!reducedFlash&&explosionShakeUntil>Time.unscaledTime)
                        {
                            // ADR-111: decays over the shake window, scaled by distance to the blast (0 beyond 25 m).
                            float strength=(explosionShakeUntil-Time.unscaledTime)/AnalyticFxPool.BlastShakeSeconds*.034f*explosionShakeScale;
                            bobLocal=new Float3(bobLocal.X+Mathf.Sin(Time.unscaledTime*47)*strength,bobLocal.Y+Mathf.Cos(Time.unscaledTime*39)*strength,bobLocal.Z);
                        }
                        float cameraYaw = acceptsGameplayInput ? input.Yaw : human.AimYaw;
                        float cameraPitch = acceptsGameplayInput ? input.Pitch : human.AimPitch;
                        // Presentation-only weapon recoil (per-weapon definition): mirrors the authoritative climb the
                        // simulation already applies to shots, plus a short per-shot punch. Never feeds aim input.
                        bool recoilRunning = session.PauseState == SessionPauseState.Running && human.Alive;
                        cameraRecoil.Step(combatEvents, human.EntityId, recoilRunning ? Time.unscaledDeltaTime : 0f, reducedCameraMotion);
                        cameraYaw += cameraRecoil.YawRadians;
                        cameraPitch += cameraRecoil.PitchRadians;
                        // ADR-103: presentation-only flinch when hit; never feeds aim input or authority.
                        damagePunch.Step(recoilRunning ? Time.unscaledDeltaTime : 0f, reducedCameraMotion);
                        cameraYaw += damagePunch.YawRadians;
                        cameraPitch += damagePunch.PitchRadians;
                        var rotation = Quaternion.Euler(cameraPitch * Mathf.Rad2Deg, cameraYaw * Mathf.Rad2Deg, roll);
                        camera.transform.rotation = rotation;
                        var basePos = new Vector3(human.Feet.X, human.Feet.Y + eye, human.Feet.Z);
                        camera.transform.position = basePos + rotation * new Vector3(bobLocal.X, bobLocal.Y, bobLocal.Z);
                        Shooter.Presentation.Rendering.ProductPostProcessing.SetDamageGrade(CombatHudVisual.LowHealth(
                            human.Alive ? human.Health : 0, Time.unscaledTime, hitGradeUntil - Time.unscaledTime, reducedFlash));
                        int lifeHealth = human.Alive ? human.Health : 100;
                        float lowSeverity = lifeHealth >= CombatHudVisual.LowHealthGradeThreshold ? 0f
                            : (CombatHudVisual.LowHealthGradeThreshold - lifeHealth) / (float)CombatHudVisual.LowHealthGradeThreshold;
                        audioOutput?.Fx.SetState(lowSeverity, CombatHudVisual.HeartbeatRateHz(lifeHealth), damageAudioGain);
                        ApplyPresentationFov(
                            adsBlend,
                            actors != null ? actors.LocalVisibleWeaponSlot : human.ActiveWeaponSlot,
                            actors != null ? actors.LocalVisibleWeaponId : human.ActiveWeaponDefinition);
                    }
                }
                else input.CaptureFrame(false);

                if (shellOwned && combatEvents != null) ObserveEliminations(combatEvents);
                if (shellOwned) ObserveLocalScoreAwards();

                if (actors != null)
                {
                    var snap = session.TryAcquireSnapshot();
                    actors.SetReviewContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], session.CompletedTick);
                    actors.Sync(
                        snap,
                        session,
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
                    actors.SyncDroppedWeapons(
                        i =>
                        {
                            session.TryGetDroppedWeapon(i, out var dropped);
                            return dropped;
                        },
                        session.DroppedWeaponCount,
                        Time.unscaledTime);
                    visualReview?.SetContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], session.CompletedTick);
                    // Refresh FOV after Sync so this frame's optic raise matches the viewmodel blend.
                    if (camera != null
                        && session.TryGetActorState(session.HumanEntityId, out var fovHuman) && fovHuman.Alive)
                        ApplyPresentationFov(actors.LocalAdsPresentationBlend, actors.LocalVisibleWeaponSlot, actors.LocalVisibleWeaponId);
                }

                PumpProductAudio(hasListener ? listenerFeet : default, combatEvents);
            }
            finally
            {
                combatLease?.Dispose();
            }

            TickAccessibilityPresentation(Time.unscaledDeltaTime);
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
                CombatEventKind.ProjectileSpawned => SynthVoiceKind.GrenadeThrow,
                CombatEventKind.Impact => SynthVoiceKind.Impact,
                CombatEventKind.DamageApplied => SynthVoiceKind.DamageTaken,
                CombatEventKind.ReloadStarted => SynthVoiceKind.ReloadStart,
                CombatEventKind.ReloadCompleted => SynthVoiceKind.ReloadComplete,
                CombatEventKind.Death => SynthVoiceKind.Death,
                _ => SynthVoiceKind.Footstep
            };
            EnsureAudio().mixer.TryPlay(voice, distanceMeters, seed);
        }

        private bool hitboxOverlay;
        private readonly List<HitVolume> overlayVolumes = new List<HitVolume>();

        private static GUIStyle? cached;
        private void OnDestroy()
        {
            ClearPresentation();
            scopePip?.Configure(0f, 1f, AdsOpticVisual.LensRadiusFraction);
            scopePip = null;
            analyticFxPresenter = null;
            audioOutput?.Detach();
            audioOutput = null;
        }
    }
}
