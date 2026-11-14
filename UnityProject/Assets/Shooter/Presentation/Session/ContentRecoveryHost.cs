#nullable enable
using System;
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
                ui.Backdrop();
                ui.Header("OPERATIONS / RECOVERY", "Game content needs attention");
                Rect a = ui.Safe;
                ui.Label(new Rect(a.x, a.y + 108, a.width, 88), message, 22);
                ui.Label(new Rect(a.x, a.y + 208, a.width, 120), detail, 18, TacticalUi.Muted);
                if (onRetry != null && ui.Button(new Rect(a.x, a.yMax - 60, 180, 44), "recovery.retry", "RETRY", primary: true)) onRetry.Invoke();
                if (ui.Button(new Rect(a.x + 192, a.yMax - 60, 210, 44), "recovery.quit", "QUIT TO DESKTOP"))
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
