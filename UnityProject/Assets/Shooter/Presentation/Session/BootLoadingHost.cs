#nullable enable
using System;
using System.Collections;
using Shooter.Presentation.Menus;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// Pre-shell insertion screen so cold start is not a blank hitch. Paints at least one frame,
    /// then invokes the continuation that creates Main or recovery and destroys this host.
    /// </summary>
    public sealed class BootLoadingHost : MonoBehaviour
    {
        private readonly TacticalUi ui = new TacticalUi();
        private string phase = InsertionScreenCopy.BootPhase;
        private Action? continueWith;
        private bool finishing;

        public static BootLoadingHost Create(string? phaseText = null)
        {
            var host = new GameObject("Boot loading").AddComponent<BootLoadingHost>();
            host.phase = string.IsNullOrWhiteSpace(phaseText) ? InsertionScreenCopy.BootPhase : phaseText!;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("SHOOTER_SHELL screen=BootLoading matchActive=0");
            return host;
        }

        /// <summary>Paint at least one insertion frame, then run <paramref name="onReady"/> and destroy this host.</summary>
        public void ContinueAfterPaint(Action onReady)
        {
            continueWith = onReady ?? throw new ArgumentNullException(nameof(onReady));
            if (!isActiveAndEnabled)
            {
                Finish();
                return;
            }
            StartCoroutine(PaintThenContinue());
        }

        private IEnumerator PaintThenContinue()
        {
            // Two frames: first layout/repaint of insertion, second so the bar can move once.
            yield return null;
            yield return null;
            Finish();
        }

        private void Finish()
        {
            if (finishing) return;
            finishing = true;
            Action? next = continueWith;
            continueWith = null;
            try { next?.Invoke(); }
            finally
            {
                if (this != null) Destroy(gameObject);
            }
        }

        private void OnGUI()
        {
            ui.Begin(1f, "boot");
            try
            {
                ui.DrawInsertionScreen(
                    InsertionScreenCopy.BootHeader,
                    InsertionScreenCopy.BootTitle,
                    "SHOOTER",
                    InsertionScreenCopy.BootRules,
                    phase,
                    media: null,
                    showCancel: false,
                    reducedMotion: false,
                    InsertionScreenCopy.BootFooter);
            }
            finally { ui.End(); }
        }
    }
}
