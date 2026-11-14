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
            const float s = 1f;
            GUI.Box(new Rect(40 * s, 40 * s, 720 * s, 280 * s), "Content recovery");
            GUI.Label(new Rect(60 * s, 90 * s, 680 * s, 60 * s), message);
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(60 * s, 150 * s, 680 * s, 60 * s), detail);
            if (onRetry != null && GUI.Button(new Rect(60 * s, 230 * s, 160 * s, 36 * s), "Retry"))
                onRetry.Invoke();
            if (GUI.Button(new Rect(240 * s, 230 * s, 160 * s, 36 * s), "Quit"))
            {
                quitRequested = true;
                onQuit?.Invoke();
                Application.Quit();
            }
        }

        private void OnDestroy() => _ = quitRequested;
    }
}
