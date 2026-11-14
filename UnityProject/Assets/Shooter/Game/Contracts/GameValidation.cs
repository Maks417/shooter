#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Game.Contracts
{
    public static class GameContractVersion
    {
        public const string Current = "Game-v1";
        public const uint RuleVersion = 1;
        public const uint SettingsSchema = 2;
        public const uint SettingsSchemaMinSupported = 1;
        public const int SimulationHz = 60;
        public const int ScoreLimit = 150;
        public const int TimeLimitActiveTicks = 54000;
        public const int MaxParticipants = 12;
        public const int MaxMaps = 16;
        public const int MaxModesPerMap = 8;
        public const int MaxSpawnSlots = 128;
        public const int MaxDisplayName = 32;
        public const int MaxMapId = 64;
        public const int MaxDescription = 256;
        public const int MaxActiveActors = 12;
        public const int MaxCosmeticCorpses = 4;
        public const int RespawnDelayActiveTicks = 180;
        public const int SpawnRetryDeferTicks = 30;
        public const int SpawnFailureTimeoutTicks = 600;
        public const int TeamSizesMin = 5;
        public const int TeamSizesMax = 6;
        public const int MaxSettingsFileBytes = 65536;
        public const int MaxSettingsBindings = 32;
        public const int MaxBindingToken = 64;
        public const int DisplayConfirmRealtimeSeconds = 15;
        public const int MinResolutionWidth = 1280;
        public const int MinResolutionHeight = 720;
        public const float MinMouseSensitivity = 0.01f;
        public const float MaxMouseSensitivity = 1f;
        public const float MinHipFovDegrees = 70f;
        public const float MaxHipFovDegrees = 100f;
        public const float MinUiScale = 1f;
        public const float MaxUiScale = 1.5f;
        public const float MinContrast = 0.8f;
        public const float MaxContrast = 1.5f;
    }

    internal static class GameRequire
    {
        public static void That([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string name)
        {
            if (!condition) throw new ArgumentException("Invalid " + name, name);
        }

        public static float Finite(float value, string name)
        {
            That(!float.IsNaN(value) && !float.IsInfinity(value), name);
            return value;
        }

        public static T EnumDefined<T>(T value, string name) where T : struct, Enum
        {
            That(Enum.IsDefined(typeof(T), value), name);
            return value;
        }

        public static string AsciiId(string value, string name, int max)
        {
            That(value != null && value.Length >= 1 && value.Length <= max, name);
            foreach (char c in value)
            {
                That(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-', name);
            }
            return value;
        }

        public static string Display(string value, string name, int max)
        {
            That(value != null && value.Length >= 1 && value.Length <= max, name);
            foreach (char c in value) That(c >= 32 && c <= 126, name);
            return value;
        }

        public static T[] Copy<T>(IReadOnlyList<T> values, int max, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            That(values.Count <= max, name);
            var copy = new T[values.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = values[i];
            return copy;
        }
    }
}
