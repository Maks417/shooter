#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>Schema 1 → 2 migration helpers (ADR-036). Does not mutate gameplay hashes.</summary>
    public static class UserSettingsMigration
    {
        public static UserSettings FromSchema1(
            float masterVolume,
            float sfxOrEffectsVolume,
            float musicOrUiVolume,
            byte qualityTier,
            float mouseSensitivity,
            bool invertY,
            bool subtitles,
            float uiScale)
        {
            return new UserSettings(
                GameContractVersion.SettingsSchema,
                Clamp01(masterVolume),
                Clamp01(sfxOrEffectsVolume),
                Clamp01(musicOrUiVolume),
                qualityTier > 2 ? (byte)0 : qualityTier,
                1280,
                720,
                DisplayMode.Windowed,
                false,
                60,
                80f,
                Clamp(mouseSensitivity, GameContractVersion.MinMouseSensitivity, GameContractVersion.MaxMouseSensitivity),
                invertY,
                Clamp(uiScale, GameContractVersion.MinUiScale, GameContractVersion.MaxUiScale),
                false,
                false,
                1f,
                subtitles,
                UserSettings.DefaultBindings());
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
