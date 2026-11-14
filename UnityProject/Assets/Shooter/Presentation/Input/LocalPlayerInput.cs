#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Presentation.Input
{
    /// <summary>One render-frame input sample, independent from the number of simulation ticks.</summary>
    public readonly struct FrameInputSample
    {
        public readonly float MoveX;
        public readonly float MoveY;
        public readonly float LookDeltaX;
        public readonly float LookDeltaY;
        public readonly bool Fire;
        public readonly bool Reload;
        public readonly bool Jump;
        public readonly bool Aim;
        public readonly bool Sprint;
        public readonly bool Crouch;
        public readonly bool SelectRifle;
        public readonly bool SelectPistol;

        public FrameInputSample(
            float moveX,
            float moveY,
            float lookDeltaX,
            float lookDeltaY,
            bool fire = false,
            bool reload = false,
            bool jump = false,
            bool aim = false,
            bool sprint = false,
            bool crouch = false,
            bool selectRifle = false,
            bool selectPistol = false)
        {
            MoveX = moveX;
            MoveY = moveY;
            LookDeltaX = lookDeltaX;
            LookDeltaY = lookDeltaY;
            Fire = fire;
            Reload = reload;
            Jump = jump;
            Aim = aim;
            Sprint = sprint;
            Crouch = crouch;
            SelectRifle = selectRifle;
            SelectPistol = selectPistol;
        }
    }

    /// <summary>
    /// M8 local keyboard/mouse command source. Hardware is sampled once per render frame;
    /// tick consumption retains edges across zero-tick frames and consumes each edge once.
    /// Applied ADR-036 bindings drive gameplay sampling; Escape remains reserved for shell recovery.
    /// </summary>
    public sealed class LocalPlayerInput
    {
        // Largest convenient float below the exact Game-v1 +/-89 degree double boundary.
        public const float MaxPitchRadians = 1.5533429f;
        public const float MaxYawRadians = 3.1415925f;
        private readonly BoundControlMap bindings = new BoundControlMap();
        private uint sequence;
        private bool fireWasDown;
        private bool reloadWasDown;
        private bool jumpWasDown;
        private bool pendingFire;
        private bool pendingReload;
        private bool pendingJump;
        private bool gameplayEnabled;
        private bool blockFireUntilRelease = true;
        private float moveX;
        private float moveY;
        private bool fireHeld;
        private bool aimHeld;
        private bool sprintHeld;
        private bool crouchHeld;
        private byte selectedSlot;
        private float yaw;
        private float pitch;
        private float sensitivityDegrees = 0.1f;
        private bool invertY;

        public LocalPlayerInput()
        {
            bindings.ApplyDefaults();
        }

        /// <summary>Degrees of yaw/pitch per mouse delta unit.</summary>
        public float Sensitivity
        {
            get => sensitivityDegrees;
            set
            {
                if (!float.IsFinite(value) || value < 0.01f || value > 1f)
                    throw new System.ArgumentOutOfRangeException(nameof(value));
                sensitivityDegrees = value;
            }
        }

        /// <summary>When true, mouse Y is inverted for pitch.</summary>
        public bool InvertY
        {
            get => invertY;
            set => invertY = value;
        }

        public BoundControlMap Bindings => bindings;
        public float Yaw => yaw;
        public float Pitch => pitch;
        public byte SelectedSlot => selectedSlot;
        public bool AimHeld => aimHeld;
        public bool ScoreboardHeld => bindings.IsPressed("Scoreboard");
        public bool PausePressedThisFrame =>
            bindings.WasPressedThisFrame("Pause")
            || BoundControlMap.WasControlPressedThisFrame(UserSettings.ReservedEscapeControl);

        public void ApplyBindings(IReadOnlyList<InputBinding> map)
        {
            bindings.Apply(map ?? throw new System.ArgumentNullException(nameof(map)));
        }

        public void ResetForLife(float yawRadians, float pitchRadians, byte activeSlot)
        {
            yaw = NormalizeYaw(yawRadians);
            pitch = Mathf.Clamp(pitchRadians, -MaxPitchRadians, MaxPitchRadians);
            selectedSlot = activeSlot > 1 ? (byte)0 : activeSlot;
            ClearCapturedInput();
        }

        public void ClearCapturedInput()
        {
            pendingFire = pendingReload = pendingJump = false;
            moveX = moveY = 0f;
            fireHeld = aimHeld = sprintHeld = crouchHeld = false;
            gameplayEnabled = false;
            blockFireUntilRelease = true;
        }

        public void CaptureFrame(bool acceptsGameplayInput)
        {
            float mx = 0f, my = 0f;
            if (bindings.IsPressed("MoveLeft")) mx -= 1f;
            if (bindings.IsPressed("MoveRight")) mx += 1f;
            if (bindings.IsPressed("MoveBack")) my -= 1f;
            if (bindings.IsPressed("MoveForward")) my += 1f;

            var mouse = Mouse.current;
            Vector2 look = mouse == null ? Vector2.zero : mouse.delta.ReadValue();
            CaptureFrame(new FrameInputSample(
                mx,
                my,
                look.x,
                look.y,
                bindings.IsPressed("Fire"),
                bindings.IsPressed("Reload"),
                bindings.IsPressed("Jump"),
                bindings.IsPressed("Aim"),
                bindings.IsPressed("Sprint"),
                bindings.IsPressed("Crouch"),
                bindings.IsPressed("Slot1"),
                bindings.IsPressed("Slot2")),
                acceptsGameplayInput);
        }

        public void CaptureFrame(FrameInputSample frame, bool acceptsGameplayInput)
        {
            bool wasEnabled = gameplayEnabled;
            gameplayEnabled = acceptsGameplayInput;
            if (!acceptsGameplayInput)
            {
                pendingFire = pendingReload = pendingJump = false;
                moveX = moveY = 0f;
                fireHeld = aimHeld = sprintHeld = crouchHeld = false;
                fireWasDown = frame.Fire;
                reloadWasDown = frame.Reload;
                jumpWasDown = frame.Jump;
                blockFireUntilRelease = true;
                return;
            }

            float mx = frame.MoveX;
            float my = frame.MoveY;
            float len = Mathf.Sqrt(mx * mx + my * my);
            if (len > 1f) { mx /= len; my /= len; }
            moveX = Mathf.Clamp(mx, -1f, 1f);
            moveY = Mathf.Clamp(my, -1f, 1f);

            // Ignore the recapture frame's look delta and edges. This prevents cursor warp and
            // a menu click still held during focus return from entering authority.
            if (wasEnabled)
            {
                float lookY = invertY ? -frame.LookDeltaY : frame.LookDeltaY;
                yaw = NormalizeYaw(yaw + frame.LookDeltaX * sensitivityDegrees * Mathf.Deg2Rad);
                pitch = Mathf.Clamp(
                    pitch - lookY * sensitivityDegrees * Mathf.Deg2Rad,
                    -MaxPitchRadians,
                    MaxPitchRadians);
            }

            if (!frame.Fire) blockFireUntilRelease = false;
            bool acceptedFire = frame.Fire && !blockFireUntilRelease;
            pendingFire |= wasEnabled && acceptedFire && !fireWasDown;
            pendingReload |= wasEnabled && frame.Reload && !reloadWasDown;
            pendingJump |= wasEnabled && frame.Jump && !jumpWasDown;
            fireHeld = acceptedFire;
            aimHeld = frame.Aim;
            sprintHeld = frame.Sprint;
            crouchHeld = frame.Crouch;
            if (wasEnabled && frame.SelectRifle) selectedSlot = 0;
            if (wasEnabled && frame.SelectPistol) selectedSlot = 1;
            fireWasDown = acceptedFire;
            reloadWasDown = frame.Reload;
            jumpWasDown = frame.Jump;
        }

        public InputCommand ConsumeTick(EntityId entity, uint tick)
        {
            var buttons = InputButtons.None;
            if (fireHeld) buttons |= InputButtons.FireHeld;
            if (pendingFire) buttons |= InputButtons.FirePressed;
            if (pendingReload) buttons |= InputButtons.ReloadPressed;
            if (pendingJump) buttons |= InputButtons.JumpPressed;
            if (aimHeld) buttons |= InputButtons.AimHeld;
            if (sprintHeld) buttons |= InputButtons.SprintHeld;
            if (crouchHeld) buttons |= InputButtons.CrouchHeld;
            pendingFire = pendingReload = pendingJump = false;
            sequence++;
            return new InputCommand(entity, tick, sequence, moveX, moveY, yaw, pitch, buttons, selectedSlot);
        }

        private static float NormalizeYaw(float value)
        {
            double normalized = value;
            double twoPi = System.Math.PI * 2.0;
            normalized -= System.Math.Floor((normalized + System.Math.PI) / twoPi) * twoPi;
            return Mathf.Clamp((float)normalized, -MaxYawRadians, MaxYawRadians);
        }
    }
}
