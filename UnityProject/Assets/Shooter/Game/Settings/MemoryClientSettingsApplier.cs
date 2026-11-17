#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// Pure test/fake applier for FIX09 software path. Records applied audio/display/input fields.
    /// T059/T061 replace composition with real device adapters; do not edit Presentation from T060.
    /// </summary>
    public sealed class MemoryClientSettingsApplier : IClientSettingsApplier
    {
        private readonly HashSet<(int Width, int Height)> supportedResolutions;
        private UserSettings? applied;
        private UserSettings? pendingDisplay;
        private UserSettings? confirmedDisplay;

        public MemoryClientSettingsApplier(IEnumerable<(int Width, int Height)>? supportedResolutions = null)
        {
            this.supportedResolutions = new HashSet<(int, int)>();
            if (supportedResolutions == null)
            {
                this.supportedResolutions.Add((1280, 720));
                this.supportedResolutions.Add((1920, 1080));
            }
            else
            {
                foreach (var mode in supportedResolutions)
                    this.supportedResolutions.Add(mode);
            }
        }

        public bool HasPendingDisplayChange => pendingDisplay != null;
        public UserSettings? PendingDisplaySettings => pendingDisplay;
        public UserSettings? AppliedSettings => applied;
        public int ApplyCount { get; private set; }
        public int RevertCount { get; private set; }

        public GameOperationResult Apply(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!supportedResolutions.Contains((settings.ResolutionWidth, settings.ResolutionHeight)))
                return new GameOperationResult(GameErrorCode.InvalidRequest, "unsupported-resolution", "Display mode is not supported.");

            if (applied != null && !settings.SameDisplaySurface(applied))
                return StageDisplayChange(settings);

            applied = settings;
            confirmedDisplay = CloneDisplay(settings);
            ApplyCount++;
            return GameOperationResult.Ok;
        }

        public GameOperationResult StageDisplayChange(UserSettings candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (!supportedResolutions.Contains((candidate.ResolutionWidth, candidate.ResolutionHeight)))
                return new GameOperationResult(GameErrorCode.InvalidRequest, "unsupported-resolution", "Display mode is not supported.");
            if (confirmedDisplay == null)
                confirmedDisplay = applied != null ? CloneDisplay(applied) : CloneDisplay(UserSettings.Defaults);
            pendingDisplay = candidate;
            applied = candidate;
            ApplyCount++;
            return GameOperationResult.Ok;
        }

        public GameOperationResult ConfirmDisplayChange()
        {
            if (pendingDisplay == null)
                return new GameOperationResult(GameErrorCode.Busy, "no-pending-display", "No display change to confirm.");
            confirmedDisplay = CloneDisplay(pendingDisplay);
            pendingDisplay = null;
            return GameOperationResult.Ok;
        }

        public GameOperationResult RevertDisplayChange()
        {
            if (pendingDisplay == null)
                return new GameOperationResult(GameErrorCode.Busy, "no-pending-display", "No display change to revert.");
            var rollback = confirmedDisplay ?? UserSettings.Defaults;
            if (applied == null)
                applied = CloneDisplay(rollback);
            else
            {
                applied = new UserSettings(
                    applied.Schema,
                    applied.MasterVolume,
                    applied.EffectsVolume,
                    applied.UiVolume,
                    applied.QualityTier,
                    rollback.ResolutionWidth,
                    rollback.ResolutionHeight,
                    rollback.DisplayMode,
                    rollback.VSync,
                    rollback.FpsCap,
                    applied.HipFovDegrees,
                    applied.MouseSensitivity,
                    applied.InvertY,
                    applied.UiScale,
                    applied.ReducedCameraMotion,
                    applied.ReducedFlash,
                    applied.Contrast,
                    applied.Subtitles,
                    CopyBindings(applied),
                    applied.MusicVolume);
            }
            pendingDisplay = null;
            RevertCount++;
            return GameOperationResult.Ok;
        }

        private static UserSettings CloneDisplay(UserSettings source) =>
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
                CopyBindings(source),
                source.MusicVolume);

        private static InputBinding[] CopyBindings(UserSettings source)
        {
            var copy = new InputBinding[source.Bindings.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source.Bindings[i];
            return copy;
        }
    }
}
