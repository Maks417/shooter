#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Contracts
{
    public static class ContractVersion
    {
        public const string Candidate = "C0-candidate.1";
        public const uint PredictionSchema = 1;
        public const int SimulationHz = 60;
        public const int MaxActors = 64;
        public const int MaxCommands = 256;
        public const int MaxEvents = 4096;
        public const int MaxLeasedBuffers = 8;
        public const int MaxQueryHits = 256;
        public const int MaxPathCorners = 128;
        public const int MaxArtifactBytes = 64 * 1024 * 1024;
    }

    internal static class Require
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
        public static T Enum<T>(T value, string name) where T : struct, Enum
        {
            That(System.Enum.IsDefined(typeof(T), value), name);
            return value;
        }
        public static string Text(string value, string name, int max = 128)
        {
            That(value != null && value.Length > 0 && value.Length <= max, name);
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
        public static void Slot(byte value) => That(value <= 1, nameof(value));
        public static void Buttons(InputButtons value) => That(((ushort)value & ~127) == 0, nameof(value));
        public static void Aim(float yaw, float pitch)
        {
            Finite(yaw, nameof(yaw)); Finite(pitch, nameof(pitch));
            // Compare float32 values in double precision so rounding cannot widen the schema interval.
            That(yaw >= -Math.PI && yaw < Math.PI, nameof(yaw));
            That(Math.Abs((double)pitch) <= 89 * Math.PI / 180, nameof(pitch));
        }
    }

    /// <summary>Modulo comparison is defined only for distances strictly below 2^31.</summary>
    public static class Serial32
    {
        public static bool IsNewer(uint value, uint previous)
        {
            uint delta = unchecked(value - previous);
            Require.That(delta != 0x80000000u, "ambiguous half-range serial");
            return delta != 0 && delta < 0x80000000u;
        }
        public static uint ForwardDistance(uint value, uint previous)
        {
            uint delta = unchecked(value - previous);
            Require.That(delta < 0x80000000u, "serial distance");
            return delta;
        }
    }
}
