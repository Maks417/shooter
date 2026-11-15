#nullable enable
using System;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Deterministic PCM combat cue bank (ADR-055). Mono float samples at a fixed rate; layers are pre-composited.
    /// </summary>
    public sealed class CombatAudioBank
    {
        public const string RecipeId = "combat-audio-bank-v2";
        public const int BankVersion = 2;
        public const int DefaultSampleRate = 48000;
        public const int VariationsPerCue = 4;

        private readonly float[][][] clips; // [kindIndex][variation][sample]

        public int SampleRate { get; }
        public string ContentHashHex { get; }

        public CombatAudioBank(int sampleRate, float[][][] clips, string contentHashHex)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (clips == null) throw new ArgumentNullException(nameof(clips));
            if (string.IsNullOrEmpty(contentHashHex)) throw new ArgumentException("hash required", nameof(contentHashHex));
            SampleRate = sampleRate;
            this.clips = clips;
            ContentHashHex = contentHashHex;
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
            return samples != null && samples.Length > 0;
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
            _ => -1
        };

        public static int KindCount => 11;
    }
}
