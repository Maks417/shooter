#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Shooter.Game.Contracts;
using UnityEngine.InputSystem;
using InputBinding = Shooter.Game.Contracts.InputBinding;
using UnityInputBinding = UnityEngine.InputSystem.InputBinding;

namespace Shooter.Presentation.Input
{
    /// <summary>
    /// ADR-036 gameplay bindings on a code-built Input System <see cref="InputActionMap"/>.
    /// Each ADR-036 action is one Button action whose single binding uses the schema-2 default control;
    /// applied user controls become Input System binding overrides. No .inputactions asset is used.
    /// </summary>
    public sealed class BoundControlMap : IDisposable
    {
        public const string MapName = "Gameplay";

        private static InteractiveRebind? sharedCapture;
        private static int sharedCaptureLastPollFrame = int.MinValue;

        private readonly Dictionary<string, string> actionToControl =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private InputActionMap? map;

        /// <summary>The code-built action map; created and enabled on first use.</summary>
        public InputActionMap Map => EnsureMap();

        public void Apply(IReadOnlyList<InputBinding> bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            actionToControl.Clear();
            for (int i = 0; i < bindings.Count; i++)
                actionToControl[bindings[i].Action] = bindings[i].Control;
            if (map != null) SyncOverrides(map);
        }

        public void ApplyDefaults() => Apply(UserSettings.DefaultBindings());

        public bool TryGetControl(string action, out string control) =>
            actionToControl.TryGetValue(action, out control!);

        public bool IsPressed(string action)
        {
            InputAction? found = FindAction(action);
            return found != null && found.IsPressed();
        }

        public bool WasPressedThisFrame(string action)
        {
            InputAction? found = FindAction(action);
            return found != null && found.WasPressedThisFrame();
        }

        /// <summary>Input System override JSON for every binding that differs from the schema-2 default.</summary>
        public string SaveOverridesJson() => EnsureMap().SaveBindingOverridesAsJson();

        /// <summary>
        /// Replace all overrides with <paramref name="json"/> (empty restores defaults) and refresh the
        /// ADR-036 token view from the resulting effective paths.
        /// </summary>
        public void LoadOverridesJson(string? json)
        {
            InputActionMap built = EnsureMap();
            built.LoadBindingOverridesFromJson(json ?? string.Empty, removeExisting: true);
            actionToControl.Clear();
            foreach (InputAction action in built.actions)
            {
                string? token = ControlTokens.FromPath(action.bindings[0].effectivePath);
                if (token != null) actionToControl[action.name] = token;
            }
        }

        /// <summary>Current ADR-036 bindings in default-action order.</summary>
        public InputBinding[] ToBindings()
        {
            var defaults = UserSettings.DefaultBindings();
            var result = new List<InputBinding>(defaults.Count);
            for (int i = 0; i < defaults.Count; i++)
            {
                if (actionToControl.TryGetValue(defaults[i].Action, out string? control) && !string.IsNullOrEmpty(control))
                    result.Add(new InputBinding(defaults[i].Action, control));
            }
            return result.ToArray();
        }

        public void Dispose()
        {
            if (map == null) return;
            map.Disable();
            map.Dispose();
            map = null;
        }

        public static bool IsControlPressed(string control)
        {
            var button = ControlTokens.FindCurrentControl(control);
            return button != null && button.isPressed;
        }

        public static bool WasControlPressedThisFrame(string control)
        {
            var button = ControlTokens.FindCurrentControl(control);
            return button != null && button.wasPressedThisFrame;
        }

        /// <summary>
        /// Poll an Input System interactive rebind for the first pressed Key.*/Mouse.* token, or null.
        /// The first poll of a listening period starts a <see cref="InteractiveRebind"/>; a gap of more than one
        /// frame between polls discards the old operation. Escape returns the reserved token so callers cancel.
        /// </summary>
        public static string? TryCapturePressedControlThisFrame()
        {
            int frame = UnityEngine.Time.frameCount;
            if (sharedCapture != null && frame > sharedCaptureLastPollFrame + 1)
            {
                sharedCapture.Dispose();
                sharedCapture = null;
            }
            sharedCaptureLastPollFrame = frame;

            if (WasControlPressedThisFrame(UserSettings.ReservedEscapeControl))
            {
                sharedCapture?.Dispose();
                sharedCapture = null;
                return UserSettings.ReservedEscapeControl;
            }

            sharedCapture ??= InteractiveRebind.Begin();
            if (!sharedCapture.TryTakeResult(out string? token)) return null;
            sharedCapture.Dispose();
            sharedCapture = null;
            return token;
        }

        /// <summary>Stable binding id per ADR-036 action so persisted override JSON survives map rebuilds.</summary>
        public static Guid BindingId(string action)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("shooter.input.gameplay.v1/" + action));
            return new Guid(hash);
        }

        private InputAction? FindAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return null;
            return EnsureMap().FindAction(action, throwIfNotFound: false);
        }

        private InputActionMap EnsureMap()
        {
            if (map != null) return map;
            var built = new InputActionMap(MapName);
            var defaults = UserSettings.DefaultBindings();
            for (int i = 0; i < defaults.Count; i++)
            {
                var action = built.AddAction(defaults[i].Action, InputActionType.Button);
                // Match device-level isPressed when a key is already held as the map enables.
                action.wantsInitialStateCheck = true;
                action.AddBinding(new UnityInputBinding
                {
                    path = ControlTokens.ToPath(defaults[i].Control) ?? string.Empty,
                    id = BindingId(defaults[i].Action)
                });
            }
            SyncOverrides(built);
            built.Enable();
            map = built;
            return built;
        }

        private void SyncOverrides(InputActionMap target)
        {
            foreach (InputAction action in target.actions)
            {
                string defaultPath = action.bindings[0].path;
                string overridePath = actionToControl.TryGetValue(action.name, out string? control)
                    ? ControlTokens.ToPath(control) ?? string.Empty
                    : string.Empty; // Unbound or unsupported token: disabled binding, never pressed.
                if (string.Equals(overridePath, defaultPath, StringComparison.OrdinalIgnoreCase))
                    action.RemoveBindingOverride(0);
                else
                    action.ApplyBindingOverride(0, overridePath);
            }
        }
    }
}
