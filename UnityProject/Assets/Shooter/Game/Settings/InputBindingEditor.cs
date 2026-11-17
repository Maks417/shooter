#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// Pure staged rebind helper for FIX09/T069. Detects conflicts, supports swap/cancel,
    /// and refuses reserved Escape / Pause mutation. Presentation owns key capture UI.
    /// </summary>
    public sealed class InputBindingEditor
    {
        private readonly List<InputBinding> working = new List<InputBinding>(GameContractVersion.MaxSettingsBindings);
        private string? listeningAction;
        private string? pendingControl;
        private string? conflictAction;

        public InputBindingEditor(IReadOnlyList<InputBinding> initial)
        {
            ReplaceWorking(initial);
        }

        public IReadOnlyList<InputBinding> Working => working;
        public bool IsListening => listeningAction != null;
        public string? ListeningAction => listeningAction;
        public bool HasConflict => conflictAction != null;
        public string? ConflictAction => conflictAction;
        public string? PendingControl => pendingControl;

        public void Load(IReadOnlyList<InputBinding> bindings)
        {
            CancelListen();
            ReplaceWorking(bindings);
        }

        public void RestoreDefaults()
        {
            CancelListen();
            ReplaceWorking(UserSettings.DefaultBindings());
        }

        public bool TryBeginListen(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            if (string.Equals(action, "Pause", StringComparison.Ordinal)) return false;
            if (!TryFindIndex(action, out _)) return false;
            listeningAction = action;
            pendingControl = null;
            conflictAction = null;
            return true;
        }

        public void CancelListen()
        {
            listeningAction = null;
            pendingControl = null;
            conflictAction = null;
        }

        /// <summary>
        /// Offer a captured control for the listening action. Returns false when ignored
        /// (not listening / reserved Escape / empty). True with HasConflict means swap/cancel required.
        /// </summary>
        public bool TryOfferControl(string control)
        {
            if (listeningAction == null) return false;
            if (string.IsNullOrEmpty(control)) return false;
            if (string.Equals(control, UserSettings.ReservedEscapeControl, StringComparison.OrdinalIgnoreCase))
                return false;

            if (TryFindControlOwner(control, out int ownerIndex)
                && !string.Equals(working[ownerIndex].Action, listeningAction, StringComparison.Ordinal))
            {
                pendingControl = control;
                conflictAction = working[ownerIndex].Action;
                return true;
            }

            ApplyControl(listeningAction, control);
            CancelListen();
            return true;
        }

        public bool TrySwapConflict()
        {
            if (listeningAction == null || pendingControl == null || conflictAction == null) return false;
            if (!TryFindIndex(listeningAction, out int listenIndex)) return false;
            if (!TryFindIndex(conflictAction, out int conflictIndex)) return false;

            string previous = working[listenIndex].Control;
            working[listenIndex] = new InputBinding(listeningAction, pendingControl);
            working[conflictIndex] = new InputBinding(conflictAction, previous);
            CancelListen();
            return true;
        }

        public bool TryCancelConflict()
        {
            if (!HasConflict) return false;
            pendingControl = null;
            conflictAction = null;
            // Remain listening for another key.
            return true;
        }

        public InputBinding[] ToArray()
        {
            var copy = new InputBinding[working.Count];
            for (int i = 0; i < working.Count; i++) copy[i] = working[i];
            return copy;
        }

        public UserSettings WithBindings(UserSettings source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new UserSettings(
                source.Schema,
                source.MasterVolume,
                source.EffectsVolume,
                source.UiVolume,
                source.QualityTier,
                source.ResolutionWidth,
                source.ResolutionHeight,
                source.DisplayMode,
                source.VSync,
                source.FpsCap,
                source.HipFovDegrees,
                source.MouseSensitivity,
                source.InvertY,
                source.UiScale,
                source.ReducedCameraMotion,
                source.ReducedFlash,
                source.Contrast,
                source.Subtitles,
                ToArray(),
                source.MusicVolume);
        }

        private void ApplyControl(string action, string control)
        {
            if (!TryFindIndex(action, out int index)) return;
            working[index] = new InputBinding(action, control);
        }

        private void ReplaceWorking(IReadOnlyList<InputBinding> bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            working.Clear();
            for (int i = 0; i < bindings.Count; i++) working.Add(bindings[i]);
        }

        private bool TryFindIndex(string action, out int index)
        {
            for (int i = 0; i < working.Count; i++)
            {
                if (string.Equals(working[i].Action, action, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        private bool TryFindControlOwner(string control, out int index)
        {
            for (int i = 0; i < working.Count; i++)
            {
                if (string.Equals(working[i].Control, control, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }
    }
}
