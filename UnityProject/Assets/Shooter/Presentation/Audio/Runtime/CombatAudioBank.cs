#nullable enable
using System;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Deterministic PCM combat cue bank (ADR-055 / T089). Interleaved stereo float samples at a fixed rate.
    /// </summary>
    public sealed class CombatAudioBank
    {
        public const string RecipeId = "combat-audio-bank-v4-stereo";
        public const int BankVersion = 4;
        public const int DefaultSampleRate = 48000;
        public const int Channels = 2;
        public const int VariationsPerCue = 4;

        private readonly float[][][] clips; // [kindIndex][variation][interleaved L,R…]

        public int SampleRate { get; }
        public string ContentHashHex { get; }
        public bool LoadedFromFoundSamples { get; }

        public CombatAudioBank(int sampleRate, float[][][] clips, string contentHashHex, bool loadedFromFoundSamples = false)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (clips == null) throw new ArgumentNullException(nameof(clips));
            if (string.IsNullOrEmpty(contentHashHex)) throw new ArgumentException("hash required", nameof(contentHashHex));
            SampleRate = sampleRate;
            this.clips = clips;
            ContentHashHex = contentHashHex;
            LoadedFromFoundSamples = loadedFromFoundSamples;
        }

        public bool TryGetClip(SynthVoiceKind kind, ulong seed, out float[] samples)
        {
            int kindIndex = KindIndex(kind);
            if (kindIndex < 0 || kindIndex >= clips.Length || clips[kindIndex] == null || clips[kindIndex].Length == 0)
            {
                samples = Array.Empty<float>();
                return false;
            }
            var vars = clips[kindIndex];
            int variation = (int)(seed % (ulong)vars.Length);
            samples = vars[variation];
            return samples != null && samples.Length >= Channels;
        }

        public static int KindIndex(SynthVoiceKind kind) => kind switch
        {
            SynthVoiceKind.Shot => 0,
            SynthVoiceKind.RifleShot => 0,
            SynthVoiceKind.PistolShot => 1,
            SynthVoiceKind.Impact => 2,
            SynthVoiceKind.DamageTaken => 3,
            SynthVoiceKind.Footstep => 4,
            SynthVoiceKind.ReloadStart => 5,
            SynthVoiceKind.ReloadComplete => 6,
            SynthVoiceKind.Death => 7,
            SynthVoiceKind.Ui => 8,
            SynthVoiceKind.Ambient => 9,
            SynthVoiceKind.Headshot => 10,
            SynthVoiceKind.KnifeSwing => 11,
            SynthVoiceKind.GrenadeThrow => 12,
            SynthVoiceKind.GrenadeExplode => 13,
            _ => -1
        };

        public static string KindFolderName(SynthVoiceKind kind) => KindIndex(kind) switch
        {
            0 => "rifle_shot",
            1 => "pistol_shot",
            2 => "impact",
            3 => "damage_taken",
            4 => "footstep",
            5 => "reload_start",
            6 => "reload_complete",
            7 => "death",
            8 => "ui",
            9 => "ambient",
            10 => "headshot",
            11 => "knife_swing",
            12 => "grenade_throw",
            13 => "grenade_explode",
            _ => "unknown"
        };

        public static int KindCount => 14;
    }
}
