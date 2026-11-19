#nullable enable
using System;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Style;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// Pre-shell recovery when packaged content/toolchain is missing or corrupt.
    /// Displays the caller-supplied repair message and a quit return path; no silent map substitution.
    /// </summary>
    public sealed class ContentRecoveryHost : MonoBehaviour
    {
        private string message = "";
        private string detail = "";
        private Action? onRetry;
        private Action? onQuit;
        private bool quitRequested;
        private readonly TacticalUi ui = new TacticalUi();

        public static ContentRecoveryHost Create(string recoveryMessage, string detail, Action? onRetry = null, Action? onQuit = null)
        {
            if (string.IsNullOrWhiteSpace(recoveryMessage)) throw new ArgumentException("recovery message required", nameof(recoveryMessage));
            var host = new GameObject("Content recovery").AddComponent<ContentRecoveryHost>();
            host.message = recoveryMessage;
            host.detail = detail ?? "";
            host.onRetry = onRetry;
            host.onQuit = onQuit;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("SHOOTER_SHELL screen=ContentRecovery matchActive=0");
            return host;
        }

        private void OnGUI()
        {
            ui.Begin(1, "recovery");
            try
            {
                // ADR-123: same backdrop, header, modal frame and footer as the shell pages.
                ui.Backdrop();
                ui.Header(ShellUiCopy.EyebrowRecovery, ShellUiCopy.RecoveryTitle);
                Rect a = ui.Safe;
                Rect panel = new Rect(a.x, a.y + 86, Mathf.Min(760, a.width), Mathf.Min(260, a.height - 200));
                ui.Frame(panel, FrameKind.Modal, rail: true);
                ui.Label(new Rect(panel.x + 24, panel.y + 22, panel.width - 48, 76), message, UiType.Heading);
                ui.Label(new Rect(panel.x + 24, panel.y + 104, panel.width - 48, panel.height - 120), detail, UiType.Lead, TacticalUi.Muted);
                var actions = ShellPage.Actions(new Shooter.Presentation.Hud.HudRect(a.x, a.y, a.width, a.height));
                if (onRetry != null && ui.Button(new Rect(a.x, actions.Y, 180, actions.Height), "recovery.retry", ShellUiCopy.RecoveryRetry, primary: true)) onRetry.Invoke();
                ui.KeyHints("", ShellUiCopy.KeyTab, ShellUiCopy.HintNavigate, ShellUiCopy.KeyEnter, ShellUiCopy.HintSelect);
                if (ui.Button(new Rect(a.x + 192, actions.Y, 210, actions.Height), "recovery.quit", ShellUiCopy.RecoveryQuit))
                {
                    quitRequested = true;
                    onQuit?.Invoke();
                    Application.Quit();
                }
            }
            finally { ui.End(); }
        }

        private void OnDestroy() => _ = quitRequested;
    }
}
