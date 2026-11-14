#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Game.Contracts;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// Real device adapter for ADR-036 / FIX09. Applies audio gains through the product mixer,
    /// display/quality/FOV/input through <see cref="ProductSessionView"/>, and stages Keep/Revert.
    /// </summary>
    public sealed class ProductClientSettingsApplier : IClientSettingsApplier
    {
        private readonly ProductSessionView view;
        private readonly HashSet<(int Width, int Height)> supportedResolutions;
        private UserSettings? applied;
        private UserSettings? pendingDisplay;
        private UserSettings? confirmedDisplay;

        public ProductClientSettingsApplier(ProductSessionView view, IEnumerable<(int Width, int Height)>? supportedResolutions = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.supportedResolutions = new HashSet<(int, int)>();
            if (supportedResolutions == null)
                CollectDeviceResolutions(this.supportedResolutions);
            else
            {
                foreach (var mode in supportedResolutions)
                    this.supportedResolutions.Add(mode);
            }
            if (this.supportedResolutions.Count == 0)
            {
                this.supportedResolutions.Add((1280, 720));
                this.supportedResolutions.Add((1920, 1080));
            }
        }

        public bool HasPendingDisplayChange => pendingDisplay != null;
        public UserSettings? PendingDisplaySettings => pendingDisplay;
        public UserSettings? AppliedSettings => applied;

        public GameOperationResult Apply(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!IsSupportedResolution(settings.ResolutionWidth, settings.ResolutionHeight))
                return new GameOperationResult(GameErrorCode.InvalidRequest, "unsupported-resolution", "Display mode is not supported.");

            if (applied != null && !settings.SameDisplaySurface(applied))
                return StageDisplayChange(settings);

            ApplyNonDisplay(settings);
            ApplyDisplaySurface(settings);
            applied = settings;
            confirmedDisplay = CloneSettings(settings);
            pendingDisplay = null;
            return GameOperationResult.Ok;
        }

        public GameOperationResult StageDisplayChange(UserSettings candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (!IsSupportedResolution(candidate.ResolutionWidth, candidate.ResolutionHeight))
                return new GameOperationResult(GameErrorCode.InvalidRequest, "unsupported-resolution", "Display mode is not supported.");

            if (confirmedDisplay == null)
                confirmedDisplay = applied != null ? CloneSettings(applied) : CloneSettings(UserSettings.Defaults);

            ApplyNonDisplay(candidate);
            ApplyDisplaySurface(candidate);
            pendingDisplay = candidate;
            applied = candidate;
            return GameOperationResult.Ok;
        }

        public GameOperationResult ConfirmDisplayChange()
        {
            if (pendingDisplay == null)
                return new GameOperationResult(GameErrorCode.Busy, "no-pending-display", "No display change to confirm.");
            confirmedDisplay = CloneSettings(pendingDisplay);
            pendingDisplay = null;
            return GameOperationResult.Ok;
        }

        public GameOperationResult RevertDisplayChange()
        {
            if (pendingDisplay == null)
                return new GameOperationResult(GameErrorCode.Busy, "no-pending-display", "No display change to revert.");

            var rollback = confirmedDisplay ?? UserSettings.Defaults;
            var restored = MergeDisplay(applied ?? UserSettings.Defaults, rollback);
            ApplyNonDisplay(restored);
            ApplyDisplaySurface(restored);
            applied = restored;
            pendingDisplay = null;
            return GameOperationResult.Ok;
        }

        private void ApplyNonDisplay(UserSettings settings)
        {
            view.ApplyUserSettings(settings);
        }

        private static void ApplyDisplaySurface(UserSettings settings)
        {
            int tier = Mathf.Clamp(settings.QualityTier, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(tier, true);
            QualitySettings.vSyncCount = settings.VSync ? 1 : 0;
            Application.targetFrameRate = settings.FpsCap <= 0 ? -1 : settings.FpsCap;
            Screen.SetResolution(settings.ResolutionWidth, settings.ResolutionHeight, ToFullScreenMode(settings.DisplayMode));
        }

        private bool IsSupportedResolution(int width, int height) =>
            supportedResolutions.Contains((width, height));

        private static void CollectDeviceResolutions(HashSet<(int, int)> into)
        {
            var modes = Screen.resolutions;
            if (modes == null) return;
            for (int i = 0; i < modes.Length; i++)
            {
                var m = modes[i];
                if (m.width >= GameContractVersion.MinResolutionWidth && m.height >= GameContractVersion.MinResolutionHeight)
                    into.Add((m.width, m.height));
            }
            into.Add((1280, 720));
            into.Add((1920, 1080));
        }

        private static FullScreenMode ToFullScreenMode(DisplayMode mode) => mode switch
        {
            DisplayMode.Borderless => FullScreenMode.FullScreenWindow,
            DisplayMode.Fullscreen => FullScreenMode.ExclusiveFullScreen,
            _ => FullScreenMode.Windowed
        };

        private static UserSettings MergeDisplay(UserSettings audioSource, UserSettings displaySource) =>
            new UserSettings(
                audioSource.Schema,
                audioSource.MasterVolume,
                audioSource.EffectsVolume,
                audioSource.UiVolume,
                audioSource.QualityTier,
                displaySource.ResolutionWidth,
                displaySource.ResolutionHeight,
                displaySource.DisplayMode,
                displaySource.VSync,
                displaySource.FpsCap,
                audioSource.HipFovDegrees,
                audioSource.MouseSensitivity,
                audioSource.InvertY,
                audioSource.UiScale,
                audioSource.ReducedCameraMotion,
                audioSource.ReducedFlash,
                audioSource.Contrast,
                audioSource.Subtitles,
                CopyBindings(audioSource));

        private static UserSettings CloneSettings(UserSettings source) =>
            new UserSettings(
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
                CopyBindings(source));

        private static InputBinding[] CopyBindings(UserSettings source)
        {
            var copy = new InputBinding[source.Bindings.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source.Bindings[i];
            return copy;
        }
    }
}
