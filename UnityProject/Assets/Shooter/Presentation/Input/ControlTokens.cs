#nullable enable
using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace Shooter.Presentation.Input
{
    /// <summary>
    /// Converts frozen ADR-036 control tokens (Key.*/Mouse.Left|Right|Middle) to and from Input System
    /// control paths. The tokens remain the persisted schema-2 vocabulary; the Input System resolves paths.
    /// </summary>
    public static class ControlTokens
    {
        public const string KeyboardPrefix = "<Keyboard>/";
        public const string MousePrefix = "<Mouse>/";

        /// <summary>Input System control path for a token, or null when the token is not a supported key/button.</summary>
        public static string? ToPath(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            if (token!.StartsWith("Key.", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseKey(token.Substring(4), out Key key)) return null;
                string? name = KeyControlName(key);
                return name == null ? null : KeyboardPrefix + name;
            }
            if (token.StartsWith("Mouse.", StringComparison.OrdinalIgnoreCase))
            {
                string button = token.Substring(6);
                if (string.Equals(button, "Left", StringComparison.OrdinalIgnoreCase)) return MousePrefix + "leftButton";
                if (string.Equals(button, "Right", StringComparison.OrdinalIgnoreCase)) return MousePrefix + "rightButton";
                if (string.Equals(button, "Middle", StringComparison.OrdinalIgnoreCase)) return MousePrefix + "middleButton";
            }
            return null;
        }

        /// <summary>Token for an Input System path produced by <see cref="ToPath"/> or a rebinding, or null.</summary>
        public static string? FromPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path!.StartsWith(KeyboardPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string name = path.Substring(KeyboardPrefix.Length);
                if (name.Length == 1 && name[0] >= '0' && name[0] <= '9') return "Key.Digit" + name;
                return TryParseKey(name, out Key key) ? "Key." + key : null;
            }
            if (path.StartsWith(MousePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string name = path.Substring(MousePrefix.Length);
                if (string.Equals(name, "leftButton", StringComparison.OrdinalIgnoreCase)) return "Mouse.Left";
                if (string.Equals(name, "rightButton", StringComparison.OrdinalIgnoreCase)) return "Mouse.Right";
                if (string.Equals(name, "middleButton", StringComparison.OrdinalIgnoreCase)) return "Mouse.Middle";
            }
            return null;
        }

        /// <summary>Token for a concrete control chosen by an interactive rebind, or null when unsupported.</summary>
        public static string? FromControl(InputControl? control)
        {
            if (control is KeyControl key)
                return key.keyCode == Key.None ? null : "Key." + key.keyCode;
            if (control is ButtonControl && control.device is Mouse mouse)
            {
                if (control == mouse.leftButton) return "Mouse.Left";
                if (control == mouse.rightButton) return "Mouse.Right";
                if (control == mouse.middleButton) return "Mouse.Middle";
            }
            return null;
        }

        /// <summary>The button control a token names on the current keyboard/mouse, or null.</summary>
        public static ButtonControl? FindCurrentControl(string? token)
        {
            string? path = ToPath(token);
            if (path == null) return null;
            InputDevice? device = path.StartsWith(KeyboardPrefix, StringComparison.Ordinal)
                ? Keyboard.current
                : Mouse.current;
            if (device == null) return null;
            return InputControlPath.TryFindControl(device, path) as ButtonControl;
        }

        /// <summary>
        /// Keyboard layout control name for a key. Matches the Input System Keyboard layout: the enum name with
        /// a lower-case first letter, except digits ("1") and the meta/AltGr aliases, which use canonical names.
        /// </summary>
        public static string? KeyControlName(Key key)
        {
            if (key == Key.None || !Enum.IsDefined(typeof(Key), key)) return null;
            if (key >= Key.Digit1 && key <= Key.Digit9) return ((int)(key - Key.Digit1) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (key == Key.Digit0) return "0";
            if (key == Key.LeftMeta) return "leftMeta";
            if (key == Key.RightMeta) return "rightMeta";
            if (key == Key.RightAlt) return "rightAlt";
#pragma warning disable CS0618 // IMESelected is a dummy key without a bindable Key control.
            if (key == Key.IMESelected) return null;
#pragma warning restore CS0618
            string name = key.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static bool TryParseKey(string token, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrEmpty(token)) return false;
            if (!Enum.TryParse(token, ignoreCase: true, out Key parsed)) return false;
            if (parsed == Key.None || !Enum.IsDefined(typeof(Key), parsed)) return false;
            key = parsed;
            return true;
        }
    }
}
