#nullable enable
using System.Globalization;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Session
{
    /// <summary>M8 product presentation: FPS camera, HUD, synth cues. Authority remains OfflineGameSession.</summary>
    public sealed class ProductSessionView : MonoBehaviour
    {
        private OfflineGameSession? session;
        private LocalPlayerInput input = new LocalPlayerInput();
        private readonly HudLayout hud = new HudLayout();
        private readonly SynthAudioMixer mixer = new SynthAudioMixer();
        private Camera? camera;
        private byte weaponSlot;
        private bool cursorLocked = true;

        public static ProductSessionView Create(OfflineGameSession session)
        {
            var root = new GameObject("Product session view").AddComponent<ProductSessionView>();
            root.Initialize(session);
            return root;
        }

        private void Initialize(OfflineGameSession session)
        {
            this.session = session ?? throw new System.ArgumentNullException(nameof(session));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("Required locked URP Lit shader missing; no art fallback");

            camera = new GameObject("Product camera").AddComponent<Camera>();
            camera.transform.SetParent(transform, false);
            camera.fieldOfView = hud.Settings.HipFov;
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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("SHOOTER_PRODUCT_VIEW gpu=" + SystemInfo.graphicsDeviceName + " profile=" + QualitySettings.names[profile]);
        }

        private void Update()
        {
            if (session == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (session.PauseState == SessionPauseState.Running && session.Phase == GameMatchPhase.Playing)
                {
                    session.Pause();
                    input.ClearEdges();
                    cursorLocked = false;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else if (session.PauseState == SessionPauseState.Paused)
                {
                    session.Resume();
                    input.ClearEdges();
                    cursorLocked = true;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            if (session.Lifecycle != null && session.Lifecycle.TryGetActorState(session.HumanEntityId, out var human))
            {
                if (camera != null)
                {
                    float eye = human.Alive ? 1.65f : 0.4f;
                    camera.transform.position = new Vector3(human.Feet.X, human.Feet.Y + eye, human.Feet.Z);
                    camera.transform.rotation = Quaternion.Euler(human.AimPitch * Mathf.Rad2Deg, human.AimYaw * Mathf.Rad2Deg, 0f);
                    bool ads = Mouse.current != null && Mouse.current.rightButton.isPressed;
                    camera.fieldOfView = ads ? 55f : hud.Settings.HipFov;
                }
                if (session.Phase == GameMatchPhase.Playing && session.PauseState == SessionPauseState.Running && human.Alive && cursorLocked)
                    input.SetLook(human.AimYaw, human.AimPitch);
            }

            mixer.Mix(Time.unscaledDeltaTime);
        }

        public InputCommand? SampleHuman(uint tick)
        {
            if (session == null || !session.HumanEntityId.IsValid) return null;
            if (session.PauseState != SessionPauseState.Running || session.Phase != GameMatchPhase.Playing) return null;
            if (!cursorLocked) return null;
            return input.Sample(session.HumanEntityId, tick, weaponSlot);
        }

        public void NotifyCombat(CombatEventKind kind, float distanceMeters, ulong seed)
        {
            var voice = kind switch
            {
                CombatEventKind.ShotAccepted => SynthVoiceKind.Shot,
                CombatEventKind.Impact => SynthVoiceKind.Impact,
                CombatEventKind.DamageApplied => SynthVoiceKind.Impact,
                _ => SynthVoiceKind.Footstep
            };
            mixer.TryPlay(voice, distanceMeters, seed);
        }

        private void OnGUI()
        {
            if (session == null) return;
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
            if (session.PauseState == SessionPauseState.Paused)
                GUI.Label(new Rect(Screen.width * 0.5f - 80, Screen.height * 0.5f - 20, 200, 40), "PAUSED (Esc)", GuiStyle());
            var results = session.TryAcquireResults();
            if (results != null)
                GUI.Label(new Rect(24, 80, 900, 24),
                    "Result " + results.Outcome + " / " + results.Reason + "  " + results.Team1Kills + "-" + results.Team2Kills,
                    GuiStyle());
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
    }
}
