#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Game.Contracts;
using UnityEngine.InputSystem;
using InputBinding = Shooter.Game.Contracts.InputBinding;

namespace Shooter.Presentation.Input
{
    /// <summary>
    /// Resolves ADR-036 control tokens (Key.*/Mouse.*) against the Input System for applied bindings.
    /// </summary>
    public sealed class BoundControlMap
    {
        private readonly Dictionary<string, string> actionToControl =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public void Apply(IReadOnlyList<InputBinding> bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            actionToControl.Clear();
            for (int i = 0; i < bindings.Count; i++)
                actionToControl[bindings[i].Action] = bindings[i].Control;
        }

        public void ApplyDefaults() => Apply(UserSettings.DefaultBindings());

        public bool TryGetControl(string action, out string control) =>
            actionToControl.TryGetValue(action, out control!);

        public bool IsPressed(string action)
        {
            if (!actionToControl.TryGetValue(action, out string? control) || control == null)
                return false;
            return IsControlPressed(control);
        }

        public bool WasPressedThisFrame(string action)
        {
            if (!actionToControl.TryGetValue(action, out string? control) || control == null)
                return false;
            return WasControlPressedThisFrame(control);
        }

        public static bool IsControlPressed(string control)
        {
            if (string.IsNullOrEmpty(control)) return false;
            if (control.StartsWith("Key.", StringComparison.OrdinalIgnoreCase))
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return false;
                Key parsed = ParseKey(control.Substring(4));
                if (parsed == Key.None) return false;
                var key = keyboard[parsed];
                return key != null && key.isPressed;
            }
            if (control.StartsWith("Mouse.", StringComparison.OrdinalIgnoreCase))
            {
                var mouse = Mouse.current;
                if (mouse == null) return false;
                return ParseMouseButton(control.Substring(6)) switch
                {
                    0 => mouse.leftButton.isPressed,
                    1 => mouse.rightButton.isPressed,
                    2 => mouse.middleButton.isPressed,
                    _ => false
                };
            }
            return false;
        }

        public static bool WasControlPressedThisFrame(string control)
        {
            if (string.IsNullOrEmpty(control)) return false;
            if (control.StartsWith("Key.", StringComparison.OrdinalIgnoreCase))
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return false;
                Key parsed = ParseKey(control.Substring(4));
                if (parsed == Key.None) return false;
                var key = keyboard[parsed];
                return key != null && key.wasPressedThisFrame;
            }
            if (control.StartsWith("Mouse.", StringComparison.OrdinalIgnoreCase))
            {
                var mouse = Mouse.current;
                if (mouse == null) return false;
                return ParseMouseButton(control.Substring(6)) switch
                {
                    0 => mouse.leftButton.wasPressedThisFrame,
                    1 => mouse.rightButton.wasPressedThisFrame,
                    2 => mouse.middleButton.wasPressedThisFrame,
                    _ => false
                };
            }
            return false;
        }

        /// <summary>Capture the first pressed Key.*/Mouse.* token this frame, or null.</summary>
        public static string? TryCapturePressedControlThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (var key in keyboard.allKeys)
                {
                    if (key == null || !key.wasPressedThisFrame) continue;
                    if (key.keyCode == Key.Escape) return UserSettings.ReservedEscapeControl;
                    return "Key." + key.keyCode;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) return "Mouse.Left";
                if (mouse.rightButton.wasPressedThisFrame) return "Mouse.Right";
                if (mouse.middleButton.wasPressedThisFrame) return "Mouse.Middle";
            }
            return null;
        }

        private static Key ParseKey(string token)
        {
            if (string.Equals(token, "Digit1", StringComparison.OrdinalIgnoreCase)) return Key.Digit1;
            if (string.Equals(token, "Digit2", StringComparison.OrdinalIgnoreCase)) return Key.Digit2;
            if (string.Equals(token, "LeftShift", StringComparison.OrdinalIgnoreCase)) return Key.LeftShift;
            if (string.Equals(token, "RightShift", StringComparison.OrdinalIgnoreCase)) return Key.RightShift;
            if (string.Equals(token, "LeftCtrl", StringComparison.OrdinalIgnoreCase)) return Key.LeftCtrl;
            if (string.Equals(token, "RightCtrl", StringComparison.OrdinalIgnoreCase)) return Key.RightCtrl;
            if (string.Equals(token, "LeftAlt", StringComparison.OrdinalIgnoreCase)) return Key.LeftAlt;
            if (string.Equals(token, "RightAlt", StringComparison.OrdinalIgnoreCase)) return Key.RightAlt;
            if (Enum.TryParse(token, ignoreCase: true, out Key parsed)) return parsed;
            return Key.None;
        }

        private static int ParseMouseButton(string token)
        {
            if (string.Equals(token, "Left", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(token, "Right", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(token, "Middle", StringComparison.OrdinalIgnoreCase)) return 2;
            return -1;
        }
    }
}
