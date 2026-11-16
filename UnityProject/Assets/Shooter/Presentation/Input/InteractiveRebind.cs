#nullable enable
using System;
using UnityEngine.InputSystem;

namespace Shooter.Presentation.Input
{
    /// <summary>
    /// One listening period for the controls menu, driven by
    /// <see cref="InputActionRebindingExtensions.PerformInteractiveRebinding"/> on a private, never-enabled
    /// scratch action. Candidates are restricted to keyboard keys and the left/right/middle mouse buttons so the
    /// result always maps to an ADR-036 token. Escape is excluded here and stays with the shell's reserved
    /// Escape path; events are not suppressed, matching the previous device polling.
    /// </summary>
    public sealed class InteractiveRebind : IDisposable
    {
        private readonly InputAction scratch;
        private InputActionRebindingExtensions.RebindingOperation? operation;
        private string? result;
        private bool resultReady;

        private InteractiveRebind()
        {
            scratch = new InputAction("Rebind", InputActionType.Button, expectedControlType: "Button");
            scratch.AddBinding(string.Empty);
        }

        public bool IsRunning => operation != null && operation.started && !operation.completed && !operation.canceled;

        public static InteractiveRebind Begin()
        {
            var rebind = new InteractiveRebind();
            rebind.Start();
            return rebind;
        }

        /// <summary>True once a supported control was chosen; the token is consumed by this call.</summary>
        public bool TryTakeResult(out string? token)
        {
            token = null;
            if (!resultReady) return false;
            resultReady = false;
            token = result;
            result = null;
            if (token != null) return true;
            // Unsupported control (no ADR-036 token): keep listening with a fresh operation.
            operation?.Dispose();
            operation = null;
            Start();
            return false;
        }

        public void Dispose()
        {
            if (operation != null)
            {
                if (operation.started && !operation.completed && !operation.canceled) operation.Cancel();
                operation.Dispose();
                operation = null;
            }
            scratch.Dispose();
        }

        private void Start()
        {
            operation = scratch.PerformInteractiveRebinding(0)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath(ControlTokens.MousePrefix + "leftButton")
                .WithControlsHavingToMatchPath(ControlTokens.MousePrefix + "rightButton")
                .WithControlsHavingToMatchPath(ControlTokens.MousePrefix + "middleButton")
                .WithControlsExcluding(ControlTokens.KeyboardPrefix + "anyKey")
                .WithControlsExcluding(ControlTokens.KeyboardPrefix + "escape")
                .WithMatchingEventsBeingSuppressed(false)
                .OnComplete(op =>
                {
                    result = ControlTokens.FromControl(op.selectedControl);
                    resultReady = true;
                })
                .Start();
        }
    }
}
