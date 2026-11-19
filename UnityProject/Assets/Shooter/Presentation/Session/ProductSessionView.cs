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

        private static string? ResolveMusicFolder()
        {
            string? combat = ResolveCombatAudioRoot();
            if (combat == null) return null;
            string? parent = System.IO.Path.GetDirectoryName(combat);
            return parent == null ? null : System.IO.Path.Combine(parent, "music");
        }

        /// <summary>Menu/shell UI cue on the session-independent UI mixer (hover, click, back).</summary>
        public bool PlayShellUi(SynthVoiceKind kind)
        {
            if (shellUiMixer == null || !SynthAudioMixer.IsUiKind(kind)) return false;
            shellUiOrdinal++;
            return shellUiMixer.TryPlay(kind, 0f, 0f, shellUiOrdinal * 0x9E3779B97F4A7C15UL);
        }

        /// <summary>Advance the music director for this frame (shell-owned only). Never touches authority.</summary>
        public MusicCue UpdateMusic(MusicContext context, bool tension, LocalMatchOutcome outcome, bool paused)
        {
            if (musicOutput == null) return MusicCue.None;
            float duck = mixer != null ? mixer.LoudCueLevel : 0f;
            var command = musicDirector.Update(new MusicInput(context, tension, outcome, paused, duck, musicOutput.CurrentCueEnded));
            musicOutput.Apply(command);
            return command.Cue;
        }

        /// <summary>Presentation-originated local cue (grenade pin, dry fire, weapon switch) while a match is attached.</summary>
        public bool PlayLocalCue(SynthVoiceKind kind, ulong seed) => audioAttached && EnsureAudio().audio.PlayLocal(kind, seed);

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
            damageAudioGain = Mathf.Clamp01(settings.MasterVolume) * Mathf.Clamp01(settings.EffectsVolume);
            if (shellUiMixer != null) UserSettingsAudioBinding.ApplyGains(settings, shellUiMixer);
            musicOutput?.SetChannelGain(UserSettingsAudioBinding.MusicGain(settings));
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

        private void ApplyPresentationFov(float adsPresentationBlend, byte weaponSlot, DefinitionId weaponId = default)
        {
            if (camera == null) return;
            // ADR-107: the mounted sight decides optic mode and ADS framing for the gun actually shown.
            var profile = actors != null && actors.LocalVisibleWeaponId.Equals(weaponId) && actors.LocalVisibleWeaponSlot == weaponSlot
                ? actors.LocalVisibleProfile
                : WeaponVisualProfiles.Resolve(weaponId, weaponSlot);
            float hip = hud.Settings.HipFov;
            float ads = AdsOpticVisual.AdsFieldOfViewDegrees(hip, profile);
            adsFovDegrees = ads;
            // Scoped PiP keeps hip FOV; digital zoom lives in AdsScopePipPresenter.
            // Reduced camera motion keeps hip FOV for every weapon.
            if (reducedCameraMotion || AdsOpticVisual.KeepsHipCameraFov(profile))
                camera.fieldOfView = hip;
            else
                camera.fieldOfView = AdsOpticVisual.BlendedFieldOfViewDegrees(hip, ads, adsPresentationBlend);
            ApplyScopePip(adsPresentationBlend, profile);
        }

        private void ApplyScopePip(float adsPresentationBlend, in WeaponVisualProfile profile)
        {
            if (scopePip == null) return;
            float hip = hud.Settings.HipFov;
            if (!AdsOpticVisual.UsesScopedOptic(profile))
            {
                scopePip.Configure(0f, 1f, AdsOpticVisual.LensRadiusFraction);
                return;
            }
            float strength = AdsOpticVisual.PipStrength(adsPresentationBlend, reducedCameraMotion);
            float zoom = reducedCameraMotion
                ? 1f
                : AdsOpticVisual.LensUvZoom(hip, profile, adsPresentationBlend);
            // ADR-103: real optical lens camera once the rifle mesh is hidden; same framing as the digital zoom.
            bool optical = AdsOpticVisual.UsesLensCamera(profile, adsPresentationBlend, reducedCameraMotion);
            float lensFov = AdsOpticVisual.LensCameraFovDegrees(hip, AdsOpticVisual.LensRadiusFraction, zoom);
            scopePip.Configure(strength, zoom, AdsOpticVisual.LensRadiusFraction, optical, lensFov);
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
                    if (camera != null && session.Lifecycle != null
                        && session.Lifecycle.TryGetActorState(session.HumanEntityId, out var fovHuman) && fovHuman.Alive)
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
            ObserveMovementDust();
            analyticFxPool.Tick(dt);
            grenadeFlight.Tick(dt);
            analyticFxPresenter?.Sync(suppressMuzzleFlash: reducedFlash);
            grenadePresenter?.Sync();
        }

        private void NotifyAccessibilityCue(CombatEvent ev)
        {
            EntityId local = session != null && session.HumanEntityId.IsValid ? session.HumanEntityId : default;
            if (FirstPersonHitFeedback.IsLocalShot(ev.Kind, ev.Instigator, local)
                && !IsKnifeWeapon(ev))
            {
                // Cosmetic local view flash only; bot/remote ShotAccepted must not flash the screen.
                // ReducedFlash suppresses the overlay while preserving shot audio and world muzzle proxies.
                // Knife melee must not flash as a gunshot.
                if (!reducedFlash) muzzleFlashRemaining = 0.09f;
                if (subtitlesEnabled) ShowSubtitle("Shot");
            }
            else if (FirstPersonHitFeedback.IsLocalShot(ev.Kind, ev.Instigator, local) && IsKnifeWeapon(ev))
            {
                if (subtitlesEnabled) ShowSubtitle("Melee");
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
            if(ev.Kind==CombatEventKind.Impact && ev.Payload is ImpactPayload mark && mark.RegionId==0)
                mapView?.Decals?.Impact(mark.Position,mark.Normal,ProductAudioEventRouter.IsGrenadeBlastImpact(ev));
            if (ev.Kind == CombatEventKind.ProjectileSpawned && ev.Payload is ProjectilePayload projectile)
            {
                grenadeFlight.TrySpawn(projectile.ProjectileId, projectile.Origin, projectile.Velocity);
                if (session != null && ev.Instigator.Equals(session.HumanEntityId))
                    input.NotifyGrenadeSpent();
                return;
            }

            // Cooked-off in-hand boom also spends the only grenade this life.
            if (ev.Kind == CombatEventKind.Impact
                && session != null
                && ev.Instigator.Equals(session.HumanEntityId)
                && ProductAudioEventRouter.IsGrenadeBlastImpact(ev))
            {
                input.NotifyGrenadeSpent();
            }

            if (ev.Kind == CombatEventKind.ShotAccepted && ev.Payload is ShotPayload shot)
            {
                // Knife melee: no muzzle flash / tracer / casing — slash is the FP viewmodel.
                if (IsKnifeDefinition(shot.WeaponId)) return;
                // ADR-061: dual flame lobes + tracer + casing. ReducedFlash hides muzzle+tracer only.
                var profile=WeaponVisualProfiles.Require(shot.WeaponId);
                // The flash sits on the muzzle the viewer sees (FP view model or held weapon), not on the
                // gameplay shot origin, which floats beside the handguard. Falls back when no socket is shown.
                Float3 visibleMuzzle=default;
                bool anchored=actors!=null&&actors.TryGetMuzzleWorld(ev.Instigator,out visibleMuzzle);
                Float3 flashOrigin=anchored?visibleMuzzle:shot.Origin;
                // ADR-102: each gun sizes its own flash; a muzzle brake adds side jets.
                if(profile.MuzzleFlashVariant!=0)analyticFxPool.SpawnGunshotFeedback(flashOrigin, shot.UnitDirection, reducedFlash,
                    profile.MuzzleFlashVariant,
                    anchored?AnalyticFxPool.MuzzleSocketPushMeters:AnalyticFxPool.MuzzleFlashForwardPushMeters, profile.MuzzleFlashSideJets);
                analyticFxPool.TrySpawn(FxKind.SmokeProxy,flashOrigin,shot.UnitDirection);
                return;
            }

            if (ev.Kind == CombatEventKind.Impact && ev.Payload is ImpactPayload impact)
            {
                grenadeFlight.ObserveImpact(impact.Position);
                bool blast=ProductAudioEventRouter.IsGrenadeBlastImpact(ev);
                if(blast)
                {
                    // ADR-111: layered blast; ReducedFlash drops the bright layers inside the pool recipe.
                    analyticFxPool.SpawnGrenadeBlast(impact.Position,impact.Normal,reducedFlash,QualitySettings.GetQualityLevel());
                    if(!reducedCameraMotion&&!reducedFlash&&camera!=null)
                    {
                        float shake=AnalyticFxPool.BlastShakeScale(Vector3.Distance(camera.transform.position,new Vector3(impact.Position.X,impact.Position.Y,impact.Position.Z)));
                        if(shake>0f)
                        {
                            // A nearer blast during an older shake takes over; a farther one cannot weaken it.
                            float now=Time.unscaledTime,left=Mathf.Max(0f,explosionShakeUntil-now)/AnalyticFxPool.BlastShakeSeconds*explosionShakeScale;
                            if(shake>=left){explosionShakeScale=shake;explosionShakeUntil=now+AnalyticFxPool.BlastShakeSeconds;}
                        }
                    }
                }
                else if(impact.RegionId==0)
                {
                    ImpactSurfaceFx.Spawn(analyticFxPool,ResolveImpactSurface(impact),impact.Position,impact.Normal);
                }
            }
        }

        /// <summary>
        /// Presentation-only surface lookup against the verified packaged collision boxes: the hit box's packaged
        /// material role (same classifier as the solids mesh) decides metal; floor hits use the registry ground.
        /// </summary>
        private ImpactSurface ResolveImpactSurface(ImpactPayload impact)
        {
            bool metal=impact.SurfaceTag==ArenaBoxBuilder.TagCover,onSolid=false;
            var p=impact.Position;
            for(int i=0;i<hudBoxes.Count&&i<impactMetalBoxes.Length;i++)
            {
                var box=hudBoxes[i];
                if(System.Math.Abs(p.X-box.Center.X)>box.Size.X*.5f+ImpactBoxSlack||System.Math.Abs(p.Y-box.Center.Y)>box.Size.Y*.5f+ImpactBoxSlack||System.Math.Abs(p.Z-box.Center.Z)>box.Size.Z*.5f+ImpactBoxSlack)continue;
                onSolid=true;if(impactMetalBoxes[i]){metal=true;break;}
            }
            return ImpactSurfaceFx.Resolve(metal,onSolid,p,impact.Normal,impactAsphaltGround);
        }

        private static bool[] ClassifyImpactMetal(IReadOnlyList<CollisionBox> boxes,string mapId)
        {
            var result=new bool[boxes.Count];bool oil=string.Equals(mapId,OilDepotDressing.MapId,System.StringComparison.Ordinal);
            for(int i=0;i<boxes.Count;i++)
            {
                ushort role=MapSolidsMesh.Classify(boxes[i]);
                result[i]=oil&&OilDepotDressing.ReplacedByDressing(boxes[i])||role!=(ushort)MapSolidsMaterialRole.StructureConcrete&&role!=(ushort)MapSolidsMaterialRole.Trim&&role!=(ushort)MapSolidsMaterialRole.Foundation;
            }
            return result;
        }

        private void ObserveMovementDust()
        {
            if(session?.Lifecycle==null)return;var snapshot=session.TryAcquireSnapshot();if(snapshot==null)return;
            for(int i=0;i<snapshot.Participants.Count&&i<dustLife.Length;i++)
            {
                var participant=snapshot.Participants[i];
                if(!participant.Alive||!session.TryGetMovementView(participant.EntityId,out var movement)||!session.Lifecycle.TryGetActorState(participant.EntityId,out var state))continue;
                if(dustLife[i]!=participant.EntityId.Value){dustLife[i]=participant.EntityId.Value;dustDistance[i]=movement.LocomotionDistanceMeters;continue;}
                float interval=movement.Sliding?.5f:1.4f;
                if(movement.LocomotionDistanceMeters-dustDistance[i]<interval)continue;
                dustDistance[i]=movement.LocomotionDistanceMeters;
                var ground=actors?.CollisionGround;if(ground==null)continue;ground.ReferenceHeight=state.Feet.Y;
                if(!ground.TrySample(state.Feet.X,state.Feet.Z,out float floor)||Mathf.Abs(floor-state.Feet.Y)>.08f)continue;
                analyticFxPool.TrySpawn(FxKind.Dust,new Float3(state.Feet.X,floor+.05f,state.Feet.Z),new Float3(0,1,0));
            }
        }

        private static bool IsKnifeWeapon(CombatEvent ev) =>
            ev.Kind == CombatEventKind.ShotAccepted && ev.Payload is ShotPayload shot && IsKnifeDefinition(shot.WeaponId);

        private static bool IsKnifeDefinition(DefinitionId weaponId) =>
            string.Equals(weaponId.ToString(), "weapons/knife", System.StringComparison.Ordinal);

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
                hitGradeUntil = now + CombatHudVisual.HitGradeSeconds;
                bool headshot = false;
                lastDamageAmount = 25;
                if (ev.Payload is DamagePayload dmg)
                {
                    lastSeenHealth = dmg.RemainingHealth;
                    lastDamageAmount = dmg.Amount;
                    headshot = dmg.RegionId == 1;
                }
                RecordDamageSource(ev.Instigator, now);
                damagePunch.Kick(lastDamageAmount, headshot, AttackerBearing(ev.Instigator), reducedCameraMotion);
                audioOutput?.Fx.Hit(lastDamageAmount);
            }
            else if (FirstPersonHitFeedback.IsDamageDealt(ev.Kind, ev.Instigator, local))
            {
                hitMarkerUntil = Time.unscaledTime + FirstPersonHitFeedback.HitMarkerSeconds;
                hitMarkerHeadshot = ev.Payload is DamagePayload dealt && dealt.RegionId == 1;
            }
        }

        /// <summary>Attacker bearing relative to the local view (radians, positive = right); 0 if unknown.</summary>
        private float AttackerBearing(EntityId instigator)
        {
            if (session?.Lifecycle == null || !instigator.IsValid) return 0f;
            if (!session.Lifecycle.TryGetActorState(instigator, out var attacker)) return 0f;
            if (!session.Lifecycle.TryGetActorState(session.HumanEntityId, out var self)) return 0f;
            if (instigator.Equals(self.EntityId)) return 0f; // own grenade: straight flinch
            return CombatHudVisual.DamageIndicatorAngle(self.AimYaw, self.Feet.X, self.Feet.Z, attacker.Feet.X, attacker.Feet.Z);
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
            // Melee is not gunfire; keep the map reveal for firearms only.
            if (IsKnifeDefinition(shot.WeaponId)) return;
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
                TacticalUi.DamageArc(cx, cy, angle, alpha * (reducedFlash ? .5f : 1f));
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

            UpdateAudioAcoustics(router, listenerFeet);
            if (session.Phase == GameMatchPhase.Playing && session.PauseState == SessionPauseState.Running)
            {
                float dt = Time.unscaledDeltaTime;
                router.TickDistantBattle(dt);
                router.TickScheduled(dt, listenerFeet);
                if (session.Lifecycle != null && session.Lifecycle.TryGetActorState(session.HumanEntityId, out var localHuman))
                {
                    bool firePressed = cursorLocked && input.Bindings.WasPressedThisFrame("Fire");
                    int cues = localWeaponCues.Step(localHuman, firePressed, dt, localCueScratch);
                    for (int c = 0; c < cues; c++)
                        router.PlayLocal(localCueScratch[c], session.CompletedTick ^ ((ulong)c << 8));
                }
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
            router.TickFootsteps(footstepScratch, listenerFeet, session.CompletedTick, session.HumanEntityId);
        }

        private void UpdateAudioAcoustics(ProductAudioEventRouter router, Float3 listenerFeet)
        {
            audioProbesRemaining = AudioProbeBudget;
            Vector3 ears = new Vector3(listenerFeet.X, listenerFeet.Y + 1.35f, listenerFeet.Z);
            if ((ears - acousticListenerPosition).sqrMagnitude > 1f)
            {
                audioObstructions.Clear();
                nextAcousticProbe = 0f;
                acousticListenerPosition = ears;
            }
            if (Time.unscaledTime >= nextAcousticProbe)
            {
                nextAcousticProbe = Time.unscaledTime + 0.2f;
                // Authoritative map solids only (layer 8); ignore actors, triggers and cosmetic meshes.
                // Fixed ray count and cadence are independent of graphics quality.
                int nearby = 0;
                if (Physics.Raycast(ears, Vector3.up, 7f, 1 << 8, QueryTriggerInteraction.Ignore)) nearby += 2;
                if (Physics.Raycast(ears, Vector3.right, 10f, 1 << 8, QueryTriggerInteraction.Ignore)) nearby++;
                if (Physics.Raycast(ears, Vector3.left, 10f, 1 << 8, QueryTriggerInteraction.Ignore)) nearby++;
                audioProbesRemaining -= 3;
                audioReflection = nearby / 4f;
            }
            float yaw = camera != null ? camera.transform.eulerAngles.y * Mathf.Deg2Rad : 0f;
            router.SetListenerAcoustics(yaw, audioReflection, listenerFeet);
        }

        private float ProbeAudioObstruction(Float3 listener, Float3 source)
        {
            var point = new Vector3(source.X, source.Y, source.Z);
            var key = new Vector3Int(Mathf.FloorToInt(point.x * 2f), Mathf.FloorToInt(point.y * 2f), Mathf.FloorToInt(point.z * 2f));
            if (audioObstructions.TryGetValue(key, out var cached) && cached.expires > Time.unscaledTime)
                return cached.value;
            if (audioProbesRemaining <= 0) return 0f;
            audioProbesRemaining--;
            Vector3 start = new Vector3(listener.X, listener.Y, listener.Z);
            Vector3 delta = point - start;
            float distance = delta.magnitude;
            float obstruction = distance > 0.5f && Physics.Raycast(start, delta / distance,
                distance - 0.15f, 1 << 8, QueryTriggerInteraction.Ignore) ? 1f : 0f;
            if (audioObstructions.Count >= 128) audioObstructions.Clear();
            audioObstructions[key] = (obstruction, Time.unscaledTime + 0.15f);
            return obstruction;
        }

        private void DetachAudioSession()
        {
            var router = audio;
            if (!audioAttached && (router == null || !router.IsAttached)) return;
            router?.DetachSession();
            audioObstructions.Clear();
            nextAcousticProbe = 0f;
            audioReflection = 0f;
            audioAttached = false;
            audioSessionId = 0;
            localWeaponCues.Reset();
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

        public bool PlayUiCue(ulong seed)
        {
            if (subtitlesEnabled) ShowSubtitle("UI");
            return EnsureAudio().audio.PlayUi(seed);
        }

        private void OnGUI()
        {
            DrawAccessibilityOverlays();
            DrawHitboxOverlay();
            // Shell host draws menus/scoreboard/results; keep a minimal combat HUD only while playing under shell.
            if (session == null) return;
            if (shellOwned)
            {
                if (!cursorLocked || session.Phase != GameMatchPhase.Playing || session.PauseState != SessionPauseState.Running) return;
                // Holding Tab shows the translucent live scoreboard; the combat HUD would paint across it.
                if (input.ScoreboardHeld) return;
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
            combatUi.ReducedMotion = reducedCameraMotion;
            combatUi.ReducedFlash = reducedFlash;
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
                    string respawn = ShellUiCopy.AwaitingSpawn;
                    if (snap != null)
                        for (int i = 0; i < snap.Participants.Count; i++)
                        {
                            var participant = snap.Participants[i];
                            if (!participant.IsHuman) continue;
                            uint due = participant.RespawnDueActiveTick;
                            uint remaining = Serial32.IsNewer(due, snap.ActiveTicksElapsed) ? Serial32.ForwardDistance(due, snap.ActiveTicksElapsed) : 0;
                            if (remaining > 0) respawn = ShellUiCopy.ReturningIn(remaining / 60f);
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
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 14, panel.width - 32, 16), ShellUiCopy.StatusLabel, 11, TacticalUi.Muted, TextAnchor.UpperLeft);
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 34, panel.width - 32, 32), ShellUiCopy.StatusKilled, 26, TacticalUi.Enemy, TextAnchor.UpperLeft);
                    TacticalUi.Fill(new Rect(panel.x + 18, panel.y + 74, 42, 1), TacticalUi.Enemy);
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 82, panel.width - 32, 22), respawn, 15, TacticalUi.Text, TextAnchor.UpperLeft);
                    return;
                }
                // Read the current life again for definite assignment; never retain an authority-owned object.
                if (!session.Lifecycle!.TryGetActorState(session.HumanEntityId, out var human)) return;
                if (human.Health < lastSeenHealth) damagePulseUntil = Time.unscaledTime + CombatHudVisual.DamagePulseSeconds;
                lastSeenHealth = human.Health;
                // ADR-103: damage-scaled edge flash on every hit, under the direction arcs and health vignette.
                float flash = FirstPersonHitFeedback.DamageFlashStrength(damageFlashUntil - Time.unscaledTime, reducedFlash, lastDamageAmount);
                if (flash > 0.01f) TacticalUi.EdgeVignette(combatUi.Width, combatUi.Height, flash * 1.6f, TacticalUi.Enemy);
                DrawHealthVignette(human.Health);
                DrawDamageIndicators(human);
                DrawTacticalMap(a, human, snap);
                DrawAmmoPanel(a, human, snap);
                DrawPickupPrompt();
                DrawAmmoScorePopup(a);
                DrawKillFeed(a);
                DrawScoreFeed(a);
                DrawScoreRibbon(a);
                float cx = combatUi.Width * .5f, cy = combatUi.Height * .5f;
                float adsPresent = actors != null ? actors.LocalAdsPresentationBlend : (input.AimHeld ? 1f : 0f);
                byte opticSlot = actors != null ? actors.LocalVisibleWeaponSlot : human.ActiveWeaponSlot;
                DefinitionId opticId = actors != null ? actors.LocalVisibleWeaponId : human.ActiveWeaponDefinition;
                var opticProfile = actors != null ? actors.LocalVisibleProfile : WeaponVisualProfiles.Resolve(opticId, opticSlot);
                if (AdsOpticVisual.UsesScopedOptic(opticProfile))
                    TacticalUi.AdsScope(combatUi.Width, combatUi.Height, AdsOpticVisual.ScopeMaskOpacity(adsPresent));
                else if (AdsOpticVisual.UsesIronSightOptic(opticProfile))
                    TacticalUi.AdsIronVignette(combatUi.Width, combatUi.Height, AdsOpticVisual.IronVignetteOpacity(adsPresent));
                else if (AdsOpticVisual.UsesCollimatorOptic(opticProfile))
                    TacticalUi.CollimatorDot(cx, cy, AdsOpticVisual.CollimatorDotRadiusPixels(combatUi.Height),
                        AdsOpticVisual.CollimatorDotOpacity(opticProfile, adsPresent));
                // Hip reticle yields once the optic/irons take the aim picture.
                if (!AdsOpticVisual.HideHipReticle(adsPresent))
                {
                    // ADR-112: the gap is the real hip-fire cone edge, so a shot that lands off the crosshair is explainable.
                    const float arm = 6f, w = 1.5f;
                    float gap = HipReticleGap(opticId, combatUi.Height);
                    TacticalUi.ReticleArm(new Rect(cx - gap - arm, cy - w * .5f, arm, w));
                    TacticalUi.ReticleArm(new Rect(cx + gap, cy - w * .5f, arm, w));
                    TacticalUi.ReticleArm(new Rect(cx - w * .5f, cy - gap - arm, w, arm));
                    TacticalUi.ReticleArm(new Rect(cx - w * .5f, cy + gap, w, arm));
                }
                float markerAlpha = FirstPersonHitFeedback.HitMarkerAlpha(hitMarkerUntil - Time.unscaledTime);
                if (markerAlpha > 0.02f) TacticalUi.HitMarker(cx, cy, markerAlpha,
                    CombatHudVisual.HitScale(FirstPersonHitFeedback.HitMarkerSeconds - (hitMarkerUntil - Time.unscaledTime),
                        hitMarkerHeadshot, reducedCameraMotion), hitMarkerHeadshot);
            }
            finally { combatUi.End(); }
        }

        private bool hitboxOverlay;
        private readonly List<HitVolume> overlayVolumes = new List<HitVolume>();

        /// <summary>
        /// ADR-112 development overlay: outlines every authoritative hit capsule of the last tick in screen space (head
        /// red, body yellow) so hit registration can be checked against the drawn soldiers. Debug builds only.
        /// </summary>
        private void DrawHitboxOverlay()
        {
            if (!hitboxOverlay || !Debug.isDebugBuild || session == null || camera == null || Event.current.type != EventType.Repaint) return;
            if (session.CopyHitVolumes(overlayVolumes) == 0) return;
            var saved = GUI.matrix; var savedColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            float focal = Screen.height * .5f / Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            var human = session.HumanEntityId;
            for (int i = 0; i < overlayVolumes.Count; i++)
            {
                var volume = overlayVolumes[i];
                if (volume.EntityId.Equals(human)) continue;
                GUI.color = volume.RegionId == 1 ? new Color(1f, .25f, .2f, .9f) : new Color(1f, .9f, .2f, .75f);
                var a = volume.Capsule.BottomSphereCenter; var b = volume.Capsule.TopSphereCenter;
                var sa = camera.WorldToScreenPoint(new Vector3(a.X, a.Y, a.Z));
                var sb = camera.WorldToScreenPoint(new Vector3(b.X, b.Y, b.Z));
                if (sa.z <= .1f || sb.z <= .1f) continue;
                float ra = volume.Capsule.Radius * focal / sa.z, rb = volume.Capsule.Radius * focal / sb.z;
                OverlayCircle(sa, ra);
                if ((sa - sb).sqrMagnitude < 1f) continue;
                OverlayCircle(sb, rb);
                Vector2 axis = new Vector2(sb.x - sa.x, sb.y - sa.y).normalized;
                Vector2 side = new Vector2(-axis.y, axis.x);
                OverlayLine(new Vector2(sa.x, sa.y) + side * ra, new Vector2(sb.x, sb.y) + side * rb);
                OverlayLine(new Vector2(sa.x, sa.y) - side * ra, new Vector2(sb.x, sb.y) - side * rb);
            }
            GUI.matrix = saved; GUI.color = savedColor;
        }

        private static void OverlayCircle(Vector3 screen, float radius)
        {
            int steps = Mathf.Clamp((int)(radius * .8f), 12, 48);
            Vector2 c = new Vector2(screen.x, screen.y);
            for (int i = 0; i < steps; i++)
            {
                float t0 = i * Mathf.PI * 2f / steps, t1 = (i + 1) * Mathf.PI * 2f / steps;
                OverlayLine(c + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * radius, c + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * radius);
            }
        }

        /// <summary>Screen-space segment (Unity screen y-up), drawn as a rotated 1.5 px bar.</summary>
        private static void OverlayLine(Vector2 from, Vector2 to)
        {
            from.y = Screen.height - from.y; to.y = Screen.height - to.y;
            Vector2 d = to - from; float length = d.magnitude;
            if (length < .5f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(new Rect(from.x, from.y - .75f, length, 1.5f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }

        /// <summary>ADR-112: logical-pixel radius of the weapon's hip-fire cone at the current vertical field of view.</summary>
        private float HipReticleGap(DefinitionId weapon, float logicalHeight)
        {
            const float minimum = 3.5f;
            float spread = session != null ? session.HumanHipSpreadRadians(weapon) : OfflineGameSession.HipSpreadRadians(weapon);
            if (spread <= 0f || camera == null) return minimum;
            float halfFov = camera.fieldOfView * .5f * Mathf.Deg2Rad;
            float gap = Mathf.Tan(spread) / Mathf.Tan(halfFov) * logicalHeight * .5f;
            return Mathf.Clamp(gap, minimum, logicalHeight * .25f);
        }

        /// <summary>ADR-105: "[G] TAKE AK-47" / "[G] TAKE AMMO" below the reticle while a dropped gun is in reach.</summary>
        private void DrawPickupPrompt()
        {
            if (session == null || !session.TryGetHumanPickupPrompt(out var dropped, out bool ammoTopUp)) return;
            if (!input.Bindings.TryGetControl("Pickup", out string control)
                || string.Equals(control, "Unbound", System.StringComparison.OrdinalIgnoreCase)) return;
            var profile = WeaponVisualProfiles.Resolve(dropped.WeaponId, 0);
            string name = string.IsNullOrEmpty(profile.DisplayLabel) ? ShellUiCopy.Rifle : profile.DisplayLabel.ToUpperInvariant();
            string text = ammoTopUp ? ShellUiCopy.PickupAmmo : ShellUiCopy.PickupWeapon(name);
            const float w = 210f, h = 26f;
            float x = combatUi.Width * .5f - w * .5f, y = combatUi.Height * .5f + 64f;
            TacticalUi.CombatPlate(new Rect(x, y, w, h));
            combatUi.Keycap(new Rect(x + 8, y + 6, 26, 14), TacticalUi.ControlLabel(control), TacticalUi.Accent);
            combatUi.Label(new Rect(x + 40, y + 3, w - 48, 20), text, 12, TacticalUi.Text, TextAnchor.MiddleLeft);
        }

        private void DrawAmmoPanel(Rect safe, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            const float w = 152f, h = 62f;
            float x = safe.xMax - w, y = safe.yMax - h;
            TacticalUi.CombatPlate(new Rect(x, y, w, h), accentBottom: true);
            // ADR-085: mesh-traced silhouette of the weapon actually shown in the hands.
            var shownProfile = WeaponVisualProfiles.Resolve(
                actors != null ? actors.LocalVisibleWeaponId : human.ActiveWeaponDefinition,
                actors != null ? actors.LocalVisibleWeaponSlot : human.ActiveWeaponSlot);
            // ADR-102: guns show their own name (AK-47, G17, ...); knife and grenade keep the shell copy.
            string weapon = human.ActiveWeaponSlot switch
            {
                2 => ShellUiCopy.Knife,
                3 => ShellUiCopy.Grenade,
                _ => string.IsNullOrEmpty(shownProfile.DisplayLabel)
                    ? (human.ActiveWeaponSlot == 1 ? ShellUiCopy.Sidearm : ShellUiCopy.Rifle)
                    : shownProfile.DisplayLabel.ToUpperInvariant()
            };
            combatUi.Label(new Rect(x + 8, y + 4, w - 16, 11), weapon, 9, TacticalUi.Muted, TextAnchor.UpperRight);
            TacticalUi.Icon(new Rect(x + 8, y + 3, 64, 13), TacticalUi.WeaponIconName(shownProfile.KillFeedGlyph), TacticalUi.Muted);
            bool knife = human.ActiveWeaponSlot == 2;
            bool grenade = human.ActiveWeaponSlot == 3;
            Color magColor = !knife && human.Magazine == 0 ? TacticalUi.Enemy : TacticalUi.Text;
            // Mag dominates; reserve stays quieter (BFV/Warface hierarchy). Knife shows an em dash; grenade is 1/0.
            string magText = knife ? "--" : (grenade ? human.Magazine.ToString() : human.Magazine.ToString("00"));
            string reserveText = knife ? "--" : (grenade ? human.Reserve.ToString() : human.Reserve.ToString("000"));
            combatUi.Label(new Rect(x + 28, y + 14, 50, 32), magText, 30, magColor, TextAnchor.MiddleRight);
            combatUi.Label(new Rect(x + 80, y + 20, 12, 14), "/", 11, TacticalUi.Muted, TextAnchor.MiddleCenter);
            combatUi.Label(new Rect(x + 92, y + 28, 50, 16), reserveText, 13, TacticalUi.Muted, TextAnchor.MiddleLeft);
            bool reloading = !knife && !grenade && actors != null && actors.Actions.IsReloading(session!.HumanEntityId);
            float trackX = x + 8, trackW = w - 16;
            if (!knife && !grenade)
                TacticalUi.AmmoRounds(new Rect(trackX, y + h - 13, trackW, 8), human.Magazine, magColor);
            if (reloading)
            {
                float phase = Mathf.Clamp01(actors!.Actions.ReloadPhaseAtTick(session.HumanEntityId, snapshot?.CompletedSimulationTick ?? 0));
                combatUi.Label(new Rect(x, y - 14, w, 14), ShellUiCopy.Reloading, 10, TacticalUi.Accent, TextAnchor.MiddleRight);
                Rect ring = new Rect(x + 7, y + 23, 20, 20);
                UiSdf.Arc(ring, 0, Mathf.PI * 2, 2, UiSdf.Style.Solid(TacticalUi.HudEdge));
                if (phase > 0f) UiSdf.Arc(ring, 0, Mathf.PI * 2 * phase, 2, UiSdf.Style.Solid(TacticalUi.Accent));
            }
            else if (!knife && human.Magazine == 0)
            {
                string reload = input.Bindings.TryGetControl("Reload", out string control) ? TacticalUi.ControlLabel(control) : "R";
                string hint = grenade ? ShellUiCopy.Empty : (human.Reserve > 0 ? ShellUiCopy.ReloadHint.Trim() : ShellUiCopy.OutOfAmmo);
                if (!grenade && human.Reserve > 0) combatUi.Keycap(new Rect(x - 24, y - 15, 24, 14), reload, TacticalUi.Accent);
                combatUi.Label(new Rect(x - 20, y - 14, w + 20, 14), hint, 10, TacticalUi.Accent, TextAnchor.MiddleRight);
            }
        }

        private void DrawTacticalMap(Rect safe, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            // ADR-104: circular heading-up minimap with the compass as a ring around it. Drawn in absolute HUD space
            // (no GUI.BeginGroup: IMGUI applies the group offset before GUI.matrix, so rotated glyphs used to land
            // outside the group and vanish). Map content is masked to the disc by the UiSdf circle clip.
            const float radius = CombatHudVisual.MinimapRadius, band = CombatHudVisual.CompassBandWidth;
            var centre = new Vector2(safe.x + radius + band, safe.yMax - radius - band);
            DrawMinimap(centre, human, snapshot);

            // The in-match teammate list was removed: at 15v15 it ran over the HP block. Team status lives on the
            // Tab scoreboard instead.
            float squadX = centre.x + radius + band + 10f;

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

            // ADR-110 armor block to the right of HP: same segmented bar, steel-blue, dimmed once broken.
            ushort armor = 0;
            if (session != null) session.TryGetArmor(session.HumanEntityId, out armor);
            {
                float ax = hx + barW + CombatHudVisual.ArmorBlockOffset;
                var armorInk = armor > 0 ? new Color(.62f, .74f, .86f, 1f) : TacticalUi.Muted;
                combatUi.Label(new Rect(ax, hy - 2, 44, 26), armor.ToString(), 24, armorInk, TextAnchor.MiddleLeft);
                combatUi.Label(new Rect(ax + 42, hy + 8, 60, 11), "AR", 9, TacticalUi.Muted, TextAnchor.MiddleLeft);
                TacticalUi.Fill(new Rect(ax, barY, barW, barH), new Color(.05f, .07f, .08f, .7f));
                TacticalUi.Fill(new Rect(ax, barY, barW, 1), new Color(1f, 1f, 1f, .05f));
                for (int s = 0; s < CombatHudVisual.HealthSegments; s++)
                {
                    float fill = CombatHudVisual.SegmentFill(armor, s, segW);
                    if (fill <= 0f) continue;
                    TacticalUi.Fill(new Rect(ax + s * (segW + gap), barY, fill, barH), armorInk);
                }
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

        private void DrawMinimap(Vector2 centre, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            const float radius = CombatHudVisual.MinimapRadius, band = CombatHudVisual.CompassBandWidth;
            float yaw = human.AimYaw;
            float pxPerMeter = radius / CombatHudVisual.MinimapRangeMeters;
            float outer = radius + band;
            Rect disc = new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f);
            Rect bezel = new Rect(centre.x - outer, centre.y - outer, outer * 2f, outer * 2f);

            // 1. Bezel plate with a soft shadow, then the map disc (lighter centre, darker edge).
            var bezelStyle = UiSdf.Style.Solid(new Color(.045f, .058f, .066f, .86f)).WithShadow(.35f, 8f, 2f);
            if (!UiSdf.Circle(bezel, bezelStyle)) TacticalUi.Fill(bezel, new Color(.045f, .058f, .066f, .7f));
            UiSdf.Circle(disc, UiSdf.Style.Solid(new Color(.09f, .115f, .125f, .82f))
                .WithGradient(new Color(.035f, .046f, .052f, .9f), UiSdf.Gradient.Radial));

            // 2. Map content, masked to the disc.
            UiSdf.BeginCircleClip(centre, radius);
            try
            {
                var rangeRing = UiSdf.Style.Solid(new Color(.6f, .72f, .75f, .09f));
                rangeRing.Stroke = 1f;
                float half = radius * .5f;
                UiSdf.Circle(new Rect(centre.x - half, centre.y - half, half * 2f, half * 2f), rangeRing);
                // Forward view cone (always up on a heading-up map).
                UiSdf.Arc(disc, -35f * Mathf.Deg2Rad, 35f * Mathf.Deg2Rad, radius,
                    UiSdf.Style.Solid(new Color(TacticalUi.Accent.r, TacticalUi.Accent.g, TacticalUi.Accent.b, .09f)));
                DrawMinimapStructures(centre, human, pxPerMeter);
            }
            finally { UiSdf.EndCircleClip(); }

            // 3. Teammates (rim-clamped when out of range) and 4. temporary gunfire reveals.
            float rim = radius - CombatHudVisual.MinimapRimInset;
            if (snapshot != null && session?.Lifecycle != null)
                for (int i = 0; i < snapshot.Participants.Count; i++)
                {
                    var participant = snapshot.Participants[i];
                    if (!participant.Alive || participant.Team != human.Team || participant.IsHuman) continue;
                    if (!session.Lifecycle.TryGetActorState(participant.EntityId, out var ally)) continue;
                    CombatHudVisual.MinimapProject(yaw, human.Feet.X, human.Feet.Z, ally.Feet.X, ally.Feet.Z, pxPerMeter, out float rx, out float dy);
                    if (CombatHudVisual.MinimapClampToRim(ref rx, ref dy, rim))
                    {
                        Rect pip = new Rect(centre.x + rx - 3f, centre.y + dy - 3f, 6f, 6f);
                        if (!UiSdf.Circle(pip, UiSdf.Style.Solid(new Color(TacticalUi.Ally.r, TacticalUi.Ally.g, TacticalUi.Ally.b, .7f))))
                            TacticalUi.Fill(pip, TacticalUi.Ally);
                        continue;
                    }
                    TacticalUi.MapGlyph(centre.x + rx, centre.y + dy, Generation.Icons.IconLibrary.AllyArrow, TacticalUi.Ally, ally.AimYaw - yaw, 12f);
                }
            float now = Time.unscaledTime;
            for (int i = 0; i < gunfireRevealUntil.Length; i++)
            {
                float alpha = CombatHudVisual.GunfireRevealAlpha(gunfireRevealUntil[i] - now);
                if (alpha <= 0.02f) continue;
                CombatHudVisual.MinimapProject(yaw, human.Feet.X, human.Feet.Z, gunfireRevealX[i], gunfireRevealZ[i], pxPerMeter, out float rx, out float dy);
                bool clamped = CombatHudVisual.MinimapClampToRim(ref rx, ref dy, rim);
                var ink = new Color(TacticalUi.Enemy.r, TacticalUi.Enemy.g, TacticalUi.Enemy.b, clamped ? alpha * .7f : alpha);
                TacticalUi.MapGlyph(centre.x + rx, centre.y + dy, Generation.Icons.IconLibrary.EnemyMarker, ink, 0f, clamped ? 9f : 12f);
            }

            // 5. Self: always centred and pointing up.
            if (!TacticalUi.Icon(new Rect(centre.x - 8f, centre.y - 8f, 16f, 16f), Generation.Icons.IconLibrary.AllyArrow, TacticalUi.Accent, shadow: true))
                TacticalUi.Fill(new Rect(centre.x - 3f, centre.y - 3f, 6f, 6f), TacticalUi.Accent);

            // 6. Compass ring: hairlines, ticks and letters turn with the map so N always marks north.
            var hairline = UiSdf.Style.Solid(TacticalUi.HudEdge);
            hairline.Stroke = 1f;
            UiSdf.Circle(bezel, hairline);
            UiSdf.Circle(new Rect(disc.x - .5f, disc.y - .5f, disc.width + 1f, disc.height + 1f),
                new UiSdf.Style { Fill = new Color(1f, 1f, 1f, .14f), Fill2 = new Color(1f, 1f, 1f, .14f), Stroke = 1f, Glint = -1f });
            float headingDeg = Mathf.Repeat(yaw * Mathf.Rad2Deg, 360f);
            for (int bearing = 0; bearing < 360; bearing += 15)
            {
                bool cardinal = CombatHudVisual.IsCardinal(bearing);
                if (cardinal) continue; // letters take the cardinal slots
                float angle = CombatHudVisual.CompassRingAngle(bearing, headingDeg);
                float len = bearing % 45 == 0 ? 5f : 3f;
                var dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                var tickInk = bearing % 45 == 0 ? new Color(TacticalUi.Text.r, TacticalUi.Text.g, TacticalUi.Text.b, .55f)
                    : new Color(TacticalUi.Muted.r, TacticalUi.Muted.g, TacticalUi.Muted.b, .4f);
                TacticalUi.Line(centre + dir * (outer - 2f - len), centre + dir * (outer - 2f), tickInk, 1f);
            }
            for (int c = 0; c < 4; c++)
            {
                int bearing = c * 90;
                float angle = CombatHudVisual.CompassRingAngle(bearing, headingDeg);
                var dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                Vector2 p = centre + dir * (radius + band * .5f);
                bool north = bearing == 0;
                combatUi.Label(new Rect(p.x - 9f, p.y - 7.5f, 18f, 15f), CombatHudVisual.CardinalOrBearing(bearing),
                    north ? 12 : 10, north ? TacticalUi.Accent : TacticalUi.Text, TextAnchor.MiddleCenter);
            }
            if (enemySpawn.HasValue)
            {
                var spawn = enemySpawn.Value;
                float objective = CombatHudVisual.DamageIndicatorAngle(yaw, human.Feet.X, human.Feet.Z, spawn.X, spawn.Z);
                var dir = new Vector2(Mathf.Sin(objective), -Mathf.Cos(objective));
                Vector2 p = centre + dir * (outer + 7f);
                TacticalUi.Icon(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Generation.Icons.IconLibrary.Objective, TacticalUi.Accent, shadow: true);
            }

            // 7. Fixed heading notch and bearing readout above the ring.
            float notchY = centre.y - outer;
            Matrix4x4 prior = GUI.matrix;
            try
            {
                GUI.matrix = prior * Matrix4x4.TRS(new Vector3(centre.x, notchY + 1f, 0f), Quaternion.Euler(0f, 0f, 180f), Vector3.one);
                if (!UiSdf.Shape(new Rect(-5f, -3f, 10f, 7f), UiShapeKind.Triangle, UiSdf.Style.Solid(TacticalUi.Accent)))
                    TacticalUi.Fill(new Rect(-1f, -3f, 2f, 7f), TacticalUi.Accent);
            }
            finally { GUI.matrix = prior; }
            Rect readout = new Rect(centre.x - 18f, notchY - 21f, 36f, 15f);
            TacticalUi.CombatPlate(readout, strong: true);
            combatUi.Label(readout, CombatHudVisual.HeadingDegrees(yaw).ToString("000"), 11, TacticalUi.Text, TextAnchor.MiddleCenter);
        }

        private void DrawMinimapStructures(Vector2 centre, ActorLifecycleState human, float pxPerMeter)
        {
            const float radius = CombatHudVisual.MinimapRadius;
            float yaw = human.AimYaw;
            float turn = -yaw * Mathf.Rad2Deg;
            bool sdf = UiSdf.Available;
            var fill = new Color(.55f, .63f, .65f, .82f);
            var edge = new UiSdf.Style { Fill = new Color(.02f, .03f, .035f, .5f), Fill2 = new Color(.02f, .03f, .035f, .5f), Radius = 1f, Stroke = 1f, Glint = -1f };
            Matrix4x4 prior = GUI.matrix;
            try
            {
                for (int i = 0; i < hudBoxes.Count; i++)
                {
                    var box = hudBoxes[i];
                    if (!box.Gameplay || box.Size.Y < .3f) continue;
                    CombatHudVisual.MinimapProject(yaw, human.Feet.X, human.Feet.Z, box.Center.X, box.Center.Z, pxPerMeter, out float rx, out float dy);
                    float hw = box.Size.X * pxPerMeter * .5f, hh = box.Size.Z * pxPerMeter * .5f;
                    float reach = radius + Mathf.Sqrt(hw * hw + hh * hh);
                    if (rx * rx + dy * dy > reach * reach) continue;
                    if (!sdf && !CombatHudVisual.RectInsideCircle(rx, dy, hw, hh, -yaw, radius)) continue;
                    GUI.matrix = prior * Matrix4x4.TRS(new Vector3(centre.x + rx, centre.y + dy, 0f), Quaternion.Euler(0f, 0f, turn), Vector3.one);
                    Rect r = new Rect(-hw, -hh, hw * 2f, hh * 2f);
                    if (UiSdf.Rect(r, UiSdf.Style.Solid(fill, 1f))) UiSdf.Rect(r, edge);
                    else TacticalUi.Fill(r, fill);
                }
            }
            finally { GUI.matrix = prior; }
        }

        private void DrawHealthVignette(int health)
        {
            // Accessibility: reducedFlash softens peak/pulse; muzzle overlay stays fully suppressed separately.
            float strength = CombatHudVisual.HealthVignette(health, damagePulseUntil > Time.unscaledTime, reducedFlash);
            if (strength <= 0.01f) return;
            if (CombatHudVisual.IsHealthWarning(health) || CombatHudVisual.IsHealthDanger(health))
                strength *= CombatHudVisual.VignetteModulation(Time.unscaledTime, reducedFlash, reducedCameraMotion);
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
                if (award.Kind == LocalScoreAwardKind.Headshot)
                {
                    // ADR-073 feed badge; ADR-097 adds the +20 bonus to the feed row and to the kill totals
                    // (the Headshot award always follows its Kill award in the same drain).
                    PushScoreFeed(ScoreBadgeVisual.HeadshotLabel, award.Points, now);
                    if (award.Points > 0)
                    {
                        ammoScorePoints = unchecked(ammoScorePoints + award.Points);
                        scoreRibbonPoints = unchecked(scoreRibbonPoints + award.Points);
                    }
                    continue;
                }
                if (award.Points == 0) continue;
                bool isKill = award.Kind == LocalScoreAwardKind.Kill;
                string label = isKill
                    ? ScoreBadgeVisual.KillLabelFor(award.KillStreak)
                    : ScoreBadgeVisual.AssistLabel;
                PushScoreFeed(label, award.Points, now);
                ammoScorePoints = award.Points;
                ammoScoreUntil = now + ScoreBadgeVisual.AmmoPopupHoldSeconds;
                if (isKill)
                {
                    scoreRibbonLabel = label;
                    scoreRibbonPoints = award.Points;
                    scoreRibbonUntil = now + ScoreBadgeVisual.RibbonHoldDuration(reducedFlash);
                    scoreRibbonStart = now;
                    scoreRibbonStreak = award.KillStreak;
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
            float w = 260f, h = 40f;
            float x = safe.x + (safe.width - w) * 0.5f;
            float y = safe.y + 78f;
            var banner = new Rect(x, y, w, h);
            TacticalUi.ScoreRibbon(banner, alpha, Time.unscaledTime - scoreRibbonStart, scoreRibbonStreak, reducedCameraMotion, reducedFlash);
            Color ink = TacticalUi.Text;
            ink.a = alpha;
            Color pts = TacticalUi.Accent;
            pts.a = alpha;
            combatUi.Label(new Rect(x, y - 22, w, 16), scoreRibbonLabel, 12, ink, TextAnchor.MiddleCenter);
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
                if (scoreFeedPoints[i] > 0)
                {
                    combatUi.Label(
                        new Rect(r.xMax - 52, r.y, 44, r.height),
                        ScoreBadgeVisual.FormatPoints(scoreFeedPoints[i]),
                        12,
                        pts,
                        TextAnchor.MiddleRight);
                }
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
                byte killerTeam = 0, victimTeam = 0;
                string? killer = null;
                string victim = "Operator";
                for (int j = 0; j < snapshot.Participants.Count; j++)
                {
                    var p = snapshot.Participants[j];
                    if (ev.Instigator.IsValid && p.EntityId.Equals(ev.Instigator)) { killer = p.IsHuman ? KillFeedText.LocalOperator : p.DisplayName; killerTeam = p.Team; }
                    if (p.EntityId.Equals(ev.Target)) { victim = p.IsHuman ? KillFeedText.LocalOperator : p.DisplayName; victimTeam = p.Team; }
                }
                KillFeedCause.Resolve(events, i, out KillFeedWeapon weapon, out bool headshot);
                for (int j = killFeed.Length - 1; j > 0; j--)
                {
                    killFeed[j] = killFeed[j - 1]; killFeedUntil[j] = killFeedUntil[j - 1];
                    killFeedY[j] = killFeedY[j - 1];
                    killFeedKillerTeam[j] = killFeedKillerTeam[j - 1]; killFeedVictimTeam[j] = killFeedVictimTeam[j - 1];
                }
                float bottom=-CombatHudVisual.KillFeedRowHeight-2;
                for(int j=1;j<killFeed.Length;j++)if(killFeedUntil[j]>Time.unscaledTime)bottom=Mathf.Max(bottom,killFeedY[j]);
                killFeedY[0] = bottom+CombatHudVisual.KillFeedRowHeight+2;
                killFeedKillerTeam[0] = killerTeam; killFeedVictimTeam[0] = victimTeam;
                killFeed[0] = KillFeedText.Create(killer, victim, weapon, headshot);
                killFeedUntil[0] = Time.unscaledTime + CombatHudVisual.KillFeedHoldSeconds;
            }
        }

        private void DrawKillFeed(Rect safe)
        {
            float now = Time.unscaledTime;
            float rowH = CombatHudVisual.KillFeedRowHeight;
            const float gap = 2f;
            float rowW = CombatHudVisual.KillFeedRowWidth;
            float targetY = 0f;
            for (int i = killFeed.Length-1; i >= 0; i--)
            {
                float remaining = killFeedUntil[i] - now;
                float alpha = CombatHudVisual.KillFeedAlpha(remaining);
                if (alpha <= 0.02f) continue;
                KillFeedEntry entry = killFeed[i];
                if (string.IsNullOrEmpty(entry.Victim) && !entry.HasKiller) continue;
                float elapsed = CombatHudVisual.KillFeedHoldSeconds - remaining;
                if (Event.current.type == EventType.Repaint)
                    killFeedY[i] = UiMotion.Follow(killFeedY[i], targetY, 20f, Time.unscaledDeltaTime, reducedCameraMotion);
                float offset = UiMotion.SlideOffset(UiMotion.Staggered(elapsed,i,reducedCameraMotion), 1f, reducedCameraMotion);
                Rect r = new Rect(safe.xMax - rowW + offset, safe.y + 62 + killFeedY[i], rowW, rowH);
                targetY += (rowH + gap) * UiMotion.Clamp01(remaining / UiMotion.TransitionSeconds);
                alpha *= UiMotion.EnterAlpha(elapsed);
                Color plate = TacticalUi.HudPlate;
                plate.a *= alpha * 0.8f;
                TacticalUi.Fill(r, plate);
                bool localKiller = KillFeedText.IsLocalKiller(entry);
                bool localVictim = KillFeedText.IsLocalVictim(entry);
                Color rail = localKiller ? TacticalUi.Ally : (localVictim ? TacticalUi.Enemy : TacticalUi.HudEdge);
                rail.a *= alpha;
                TacticalUi.AccentBar(new Rect(r.x, r.y + 2, 2, r.height - 4), rail);

                Color killerPlate = killFeedKillerTeam[i] == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                Color victimPlate = killFeedVictimTeam[i] == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                killerPlate.a = killFeedKillerTeam[i] == 0 ? 0f : alpha * .12f;
                victimPlate.a = killFeedVictimTeam[i] == 0 ? 0f : alpha * .12f;
                TacticalUi.Fill(new Rect(r.x + 4, r.y + 1, CombatHudVisual.KillFeedNameWidth + 4, r.height - 2), killerPlate);

                string left = entry.LeftLabel;
                string right = entry.RightLabel;
                bool env = !entry.HasKiller;
                Color leftInk = localKiller ? TacticalUi.Ally : TacticalUi.Text;
                Color rightInk = localVictim ? TacticalUi.Enemy : TacticalUi.Muted;
                leftInk.a = alpha;
                rightInk.a = alpha;
                Color iconInk = TacticalUi.Muted;
                iconInk.a = alpha * 0.92f;

                float nameW = CombatHudVisual.KillFeedNameWidth;
                float iconX = r.x + 8f + nameW + 4f;
                combatUi.Label(new Rect(r.x + 8, r.y, nameW, r.height), left, 11, leftInk, TextAnchor.MiddleRight);

                if (entry.Weapon != KillFeedWeapon.None)
                {
                    float wIcon = CombatHudVisual.KillFeedWeaponIconWidth;
                    TacticalUi.KillFeedWeaponIcon(new Rect(iconX, r.y + 2f, wIcon, r.height - 4f), entry.Weapon, iconInk);
                    iconX += wIcon + CombatHudVisual.KillFeedIconGap;
                    if (entry.Headshot)
                    {
                        float hIcon = CombatHudVisual.KillFeedHeadshotIconWidth;
                        Color hs = localKiller ? TacticalUi.Ally : TacticalUi.Accent;
                        hs.a = alpha;
                        TacticalUi.KillFeedHeadshotIcon(new Rect(iconX, r.y + 2f, hIcon, r.height - 4f), hs);
                        iconX += hIcon + CombatHudVisual.KillFeedIconGap;
                    }
                }
                else if (env)
                {
                    // No weapon for environment deaths; small muted dash keeps spacing readable.
                    Color dash = TacticalUi.Muted;
                    dash.a = alpha * 0.7f;
                    TacticalUi.Fill(new Rect(iconX + 4f, r.y + r.height * 0.45f, 10f, 1.5f), dash);
                    iconX += 18f;
                }

                float rightX = iconX;
                float rightW = r.xMax - 6f - rightX;
                if (rightW < 40f) rightW = 40f;
                TacticalUi.Fill(new Rect(rightX, r.y + 1, rightW, r.height - 2), victimPlate);
                combatUi.Label(new Rect(rightX, r.y, rightW, r.height), right, 11, rightInk, TextAnchor.MiddleLeft);
            }
        }

        private void DrawAccessibilityOverlays()
        {
            if (muzzleFlashRemaining > 0f && !reducedFlash)
            {
                // Soft lower-center wash near the muzzle, not a full-screen bubble flash.
                float alpha = Mathf.Clamp01(muzzleFlashRemaining / 0.09f) * 0.28f;
                float w = Screen.width;
                float h = Screen.height;
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.92f, 0.68f, alpha);
                GUI.DrawTexture(new Rect(w * 0.28f, h * 0.42f, w * 0.44f, h * 0.38f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.85f, 0.45f, alpha * 0.55f);
                GUI.DrawTexture(new Rect(w * 0.38f, h * 0.52f, w * 0.24f, h * 0.22f), Texture2D.whiteTexture);
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
            scopePip?.Configure(0f, 1f, AdsOpticVisual.LensRadiusFraction);
            scopePip = null;
            analyticFxPresenter = null;
            audioOutput?.Detach();
            audioOutput = null;
        }
    }
}
