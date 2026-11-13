#nullable enable
using Shooter.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Presentation.Input
{
    /// <summary>M8 local keyboard/mouse command source (Input System). Edges clear on pause/resume.</summary>
    public sealed class LocalPlayerInput
    {
        private uint sequence;
        private bool fireWasDown;
        private bool reloadWasDown;
        private bool jumpWasDown;
        private float yaw;
        private float pitch;
        public float Sensitivity { get; set; } = 0.12f;
        public float Yaw => yaw;
        public float Pitch => pitch;

        public void SetLook(float yawRadians, float pitchRadians)
        {
            yaw = yawRadians;
            pitch = Mathf.Clamp(pitchRadians, -1.2f, 1.2f);
        }

        public void ClearEdges()
        {
            fireWasDown = reloadWasDown = jumpWasDown = false;
        }

        public InputCommand Sample(EntityId entity, uint tick, byte selectedSlot)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            float mx = 0f, my = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) mx -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) mx += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) my -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) my += 1f;
            }
            float len = Mathf.Sqrt(mx * mx + my * my);
            if (len > 1e-5f) { mx /= len; my /= len; }

            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * Sensitivity * 0.0025f;
                pitch -= delta.y * Sensitivity * 0.0025f;
            }
            pitch = Mathf.Clamp(pitch, -1.2f, 1.2f);
            if (yaw > Mathf.PI) yaw -= Mathf.PI * 2f;
            if (yaw <= -Mathf.PI) yaw += Mathf.PI * 2f;

            bool fireDown = mouse != null && mouse.leftButton.isPressed;
            bool reloadDown = keyboard != null && keyboard.rKey.isPressed;
            bool jumpDown = keyboard != null && keyboard.spaceKey.isPressed;
            var buttons = InputButtons.None;
            if (fireDown) buttons |= InputButtons.FireHeld;
            if (fireDown && !fireWasDown) buttons |= InputButtons.FirePressed;
            if (reloadDown && !reloadWasDown) buttons |= InputButtons.ReloadPressed;
            if (jumpDown && !jumpWasDown) buttons |= InputButtons.JumpPressed;
            if (mouse != null && mouse.rightButton.isPressed) buttons |= InputButtons.AimHeld;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) buttons |= InputButtons.SprintHeld;
            if (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed)) buttons |= InputButtons.CrouchHeld;
            if (keyboard != null && keyboard.digit1Key.isPressed) selectedSlot = 0;
            if (keyboard != null && keyboard.digit2Key.isPressed) selectedSlot = 1;

            fireWasDown = fireDown;
            reloadWasDown = reloadDown;
            jumpWasDown = jumpDown;
            sequence++;
            return new InputCommand(entity, tick, sequence, mx, my, yaw, pitch, buttons, selectedSlot);
        }
    }
}
