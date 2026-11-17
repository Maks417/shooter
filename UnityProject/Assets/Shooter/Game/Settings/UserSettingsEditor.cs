#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// Staged Apply/Cancel/defaults working copy. Pure; presentation binds UI widgets to Draft
    /// and calls Apply/Cancel. Device binding remains on <see cref="IClientSettingsApplier"/>.
    /// </summary>
    public sealed class UserSettingsEditor
    {
        private UserSettings applied;
        private UserSettings draft;

        public UserSettingsEditor(UserSettings? initial = null)
        {
            applied = initial ?? UserSettings.Defaults;
            draft = Clone(applied);
        }

        public UserSettings Applied => applied;
        public UserSettings Draft => draft;
        public bool IsDirty => !EqualsSettings(applied, draft);

        public void Cancel() => draft = Clone(applied);

        public void RestoreDefaults() => draft = Clone(UserSettings.Defaults);

        public void SetDraft(UserSettings settings)
        {
            draft = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public GameOperationResult Apply(IUserSettingsStore store, IClientSettingsApplier applier)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (applier == null) throw new ArgumentNullException(nameof(applier));

            bool displayChanged = applied == null || !draft.SameDisplaySurface(applied);
            // Persist confirmed display only. Pending surface stays in memory/device until Keep,
            // so focus-loss timeout and app restart roll back to the last confirmed mode.
            UserSettings confirmedSurface = applied ?? UserSettings.Defaults;
            UserSettings persistable = displayChanged
                ? WithDisplaySurface(draft, confirmedSurface)
                : draft;

            GameOperationResult applyResult = displayChanged
                ? applier.StageDisplayChange(draft)
                : applier.Apply(draft);
            if (!applyResult.Succeeded) return applyResult;

            var save = store.Save(persistable);
            if (!save.Succeeded)
            {
                // Keep applied device preview; surface nonblocking save failure.
                applied = draft;
                return save;
            }

            applied = draft;
            draft = Clone(applied);
            if (displayChanged)
                return applyResult;
            return GameOperationResult.Ok;
        }

        public GameOperationResult ConfirmDisplay(IClientSettingsApplier applier) =>
            ConfirmDisplay(store: null, applier);

        public GameOperationResult ConfirmDisplay(IUserSettingsStore? store, IClientSettingsApplier applier)
        {
            if (applier == null) throw new ArgumentNullException(nameof(applier));
            var result = applier.ConfirmDisplayChange();
            if (!result.Succeeded) return result;
            if (applier.AppliedSettings != null)
            {
                applied = applier.AppliedSettings;
                draft = Clone(applied);
            }
            if (store != null)
            {
                var save = store.Save(applied);
                if (!save.Succeeded) return save;
            }
            return GameOperationResult.Ok;
        }

        public GameOperationResult RevertDisplay(IClientSettingsApplier applier)
        {
            if (applier == null) throw new ArgumentNullException(nameof(applier));
            var result = applier.RevertDisplayChange();
            if (result.Succeeded && applier.AppliedSettings != null)
            {
                applied = applier.AppliedSettings;
                draft = Clone(applied);
            }
            return result;
        }

        private static UserSettings WithDisplaySurface(UserSettings source, UserSettings displaySource)
        {
            if (displaySource == null) throw new ArgumentNullException(nameof(displaySource));
            return new UserSettings(
                source.Schema,
                source.MasterVolume,
                source.EffectsVolume,
                source.UiVolume,
                source.QualityTier,
                displaySource.ResolutionWidth,
                displaySource.ResolutionHeight,
                displaySource.DisplayMode,
                displaySource.VSync,
                displaySource.FpsCap,
                source.HipFovDegrees,
                source.MouseSensitivity,
                source.InvertY,
                source.UiScale,
                source.ReducedCameraMotion,
                source.ReducedFlash,
                source.Contrast,
                source.Subtitles,
                CopyBindings(source),
                source.MusicVolume);
        }

        private static UserSettings Clone(UserSettings source)
        {
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
                CopyBindings(source),
                source.MusicVolume);
        }

        private static InputBinding[] CopyBindings(UserSettings source)
        {
            var bindings = new InputBinding[source.Bindings.Count];
            for (int i = 0; i < bindings.Length; i++) bindings[i] = source.Bindings[i];
            return bindings;
        }

        private static bool EqualsSettings(UserSettings a, UserSettings b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Schema != b.Schema) return false;
            if (a.MasterVolume != b.MasterVolume || a.EffectsVolume != b.EffectsVolume || a.UiVolume != b.UiVolume) return false;
            if (a.QualityTier != b.QualityTier) return false;
            if (!a.SameDisplaySurface(b)) return false;
            if (a.HipFovDegrees != b.HipFovDegrees || a.MouseSensitivity != b.MouseSensitivity || a.InvertY != b.InvertY) return false;
            if (a.UiScale != b.UiScale || a.ReducedCameraMotion != b.ReducedCameraMotion || a.ReducedFlash != b.ReducedFlash) return false;
            if (a.Contrast != b.Contrast || a.Subtitles != b.Subtitles) return false;
            if (a.Bindings.Count != b.Bindings.Count) return false;
            for (int i = 0; i < a.Bindings.Count; i++)
                if (!a.Bindings[i].Equals(b.Bindings[i])) return false;
            return true;
        }
    }

    /// <summary>15-second real-time Keep/Revert clock. Runs even while gameplay is paused.</summary>
    public sealed class DisplayChangeConfirmClock
    {
        private float remaining;
        public bool Pending => remaining > 0f;
        public float RemainingSeconds => Math.Max(0f, remaining);

        public void Begin(float seconds = GameContractVersion.DisplayConfirmRealtimeSeconds)
        {
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            remaining = seconds;
        }

        public void Clear() => remaining = 0f;

        /// <summary>Advance wall clock. Returns true when timeout or focus loss forces revert.</summary>
        public bool Tick(float realDeltaSeconds, bool focusLost, out bool shouldRevert)
        {
            shouldRevert = false;
            if (!Pending) return false;
            if (focusLost)
            {
                remaining = 0f;
                shouldRevert = true;
                return true;
            }
            if (realDeltaSeconds < 0f) realDeltaSeconds = 0f;
            remaining -= realDeltaSeconds;
            if (remaining <= 0f)
            {
                remaining = 0f;
                shouldRevert = true;
                return true;
            }
            return false;
        }
    }
}
