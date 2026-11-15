#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using UnityEngine;
using UnityEngine.InputSystem;
using EntityId = Shooter.Contracts.EntityId;
using InputBinding = Shooter.Game.Contracts.InputBinding;

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
        public readonly bool SelectKnife;
        public readonly bool SelectGrenade;
        public readonly bool Slide;
        /// <summary>Press edges reported by the device layer (Input System WasPressedThisFrame); catches clicks shorter than a frame.</summary>
        public readonly bool FireEdge;
        public readonly bool ReloadEdge;
        public readonly bool JumpEdge;
        /// <summary>Last-weapon quick-switch press edge (default Q).</summary>
        public readonly bool SelectLastWeapon;

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
            bool selectPistol = false,
            bool slide = false,
            bool fireEdge = false,
            bool reloadEdge = false,
            bool jumpEdge = false,
            bool selectLastWeapon = false,
            bool selectKnife = false,
            bool selectGrenade = false)
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
            SelectKnife = selectKnife;
            SelectGrenade = selectGrenade;
            Slide = slide;
            FireEdge = fireEdge;
            ReloadEdge = reloadEdge;
            JumpEdge = jumpEdge;
            SelectLastWeapon = selectLastWeapon;
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
        private bool slideWasDown;
        private bool pendingSlide;
        private bool pendingFire;
        private bool pendingReload;
        private bool pendingJump;
        /// <summary>Remaining ticks for which a semi-auto press edge stays asserted (press buffer).</summary>
        private int fireBufferTicks;
        /// <summary>Longer than a pistol cooldown (10 ticks) would double-fire, so keep this below it.</summary>
        public const int FirePressBufferTicks = 8;
        private const byte PistolSlot = 1;
        private const byte KnifeSlot = 2;
        private const byte GrenadeSlot = 3;
        private const byte MaxSlot = 3;
        private bool gameplayEnabled;
        private bool blockFireUntilRelease = true;
        private float moveX;
        private float moveY;
        private bool fireHeld;
        private bool aimHeld;
        private bool sprintHeld;
        private bool crouchHeld;
        private byte selectedSlot;
        /// <summary>Previous active weapon slot for LastWeapon quick-switch (ADR-056).</summary>
        private byte lastSlot = 1;
        /// <summary>One grenade per life; cleared after a local throw so Digit4 cannot re-select empty hands.</summary>
        private bool grenadeAvailable = true;
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

        /// <summary>Call after the local human's grenade leaves the hand this life.</summary>
        public void NotifyGrenadeSpent()
        {
            grenadeAvailable = false;
            if (selectedSlot == GrenadeSlot)
            {
                selectedSlot = lastSlot == GrenadeSlot ? (byte)0 : lastSlot;
                lastSlot = GrenadeSlot;
            }
        }

        public void ResetForLife(float yawRadians, float pitchRadians, byte activeSlot)
        {
            yaw = NormalizeYaw(yawRadians);
            pitch = Mathf.Clamp(pitchRadians, -MaxPitchRadians, MaxPitchRadians);
            grenadeAvailable = true;
            selectedSlot = activeSlot > MaxSlot ? (byte)0 : activeSlot;
            // Prefer the previous gun slot so the first Q after spawn/respawn remains useful.
            lastSlot = selectedSlot == 0 ? (byte)1 : (byte)0;
            ClearCapturedInput();
        }

        public void ClearCapturedInput()
        {
            pendingFire = pendingReload = pendingJump = pendingSlide = false;
            fireBufferTicks = 0;
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
                bindings.IsPressed("Slot2"),
                bindings.IsPressed("Slide"),
                bindings.WasPressedThisFrame("Fire"),
                bindings.WasPressedThisFrame("Reload"),
                bindings.WasPressedThisFrame("Jump"),
                bindings.WasPressedThisFrame("LastWeapon"),
                bindings.IsPressed("Slot3"),
                bindings.IsPressed("Slot4")),
                acceptsGameplayInput);
        }

        public void CaptureFrame(FrameInputSample frame, bool acceptsGameplayInput)
        {
            bool wasEnabled = gameplayEnabled;
            gameplayEnabled = acceptsGameplayInput;
            if (!acceptsGameplayInput)
            {
                pendingFire = pendingReload = pendingJump = pendingSlide = false;
                fireBufferTicks = 0;
                moveX = moveY = 0f;
                fireHeld = aimHeld = sprintHeld = crouchHeld = false;
                fireWasDown = frame.Fire;
                reloadWasDown = frame.Reload;
                jumpWasDown = frame.Jump;
                slideWasDown = frame.Slide;
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
            bool fireEdge = (acceptedFire && !fireWasDown) || (frame.FireEdge && !blockFireUntilRelease);
            pendingFire |= wasEnabled && fireEdge;
            pendingReload |= wasEnabled && ((frame.Reload && !reloadWasDown) || frame.ReloadEdge);
            pendingJump |= wasEnabled && ((frame.Jump && !jumpWasDown) || frame.JumpEdge);
            pendingSlide |= wasEnabled && frame.Sprint && frame.Slide && !slideWasDown;
            fireHeld = acceptedFire;
            aimHeld = frame.Aim;
            sprintHeld = frame.Sprint;
            crouchHeld = frame.Crouch;
            if (wasEnabled)
            {
                // Absolute slots win over LastWeapon in the same frame; update previous only on a real change.
                if (frame.SelectRifle) SelectAbsolute(0);
                else if (frame.SelectPistol) SelectAbsolute(1);
                else if (frame.SelectKnife) SelectAbsolute(KnifeSlot);
                else if (frame.SelectGrenade && grenadeAvailable) SelectAbsolute(GrenadeSlot);
                else if (frame.SelectLastWeapon)
                {
                    byte previous = selectedSlot;
                    selectedSlot = lastSlot;
                    lastSlot = previous;
                }
            }
            fireWasDown = acceptedFire;
            reloadWasDown = frame.Reload;
            jumpWasDown = frame.Jump;
            slideWasDown = frame.Slide;
        }

        public InputCommand ConsumeTick(EntityId entity, uint tick) => ConsumeLocalTick(entity, tick).Command;

        public LocalPlayerCommand ConsumeLocalTick(EntityId entity, uint tick)
        {
            bool slide = pendingSlide;
            var buttons = InputButtons.None;
            if (fireHeld) buttons |= InputButtons.FireHeld;
            // Buffer only the semi-automatic pistol slot: on the automatic rifle a buffered edge
            // after a quick tap would read as a fresh press once fire is released (double shot).
            if (pendingFire)
                fireBufferTicks = selectedSlot == PistolSlot ? FirePressBufferTicks : 1;
            if (fireBufferTicks > 0) { buttons |= InputButtons.FirePressed; fireBufferTicks--; }
            if (pendingReload) buttons |= InputButtons.ReloadPressed;
            if (pendingJump) buttons |= InputButtons.JumpPressed;
            if (aimHeld) buttons |= InputButtons.AimHeld;
            if (sprintHeld) buttons |= InputButtons.SprintHeld;
            if (crouchHeld) buttons |= InputButtons.CrouchHeld;
            pendingFire = pendingReload = pendingJump = pendingSlide = false;
            sequence++;
            return new LocalPlayerCommand(
                new InputCommand(entity, tick, sequence, moveX, moveY, yaw, pitch, buttons, selectedSlot), slide);
        }

        private void SelectAbsolute(byte slot)
        {
            if (selectedSlot != slot) { lastSlot = selectedSlot; selectedSlot = slot; }
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
