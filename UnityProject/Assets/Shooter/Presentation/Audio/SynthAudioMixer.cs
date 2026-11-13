#nullable enable
using System;

namespace Shooter.Presentation.Audio
{
    public enum SynthVoiceKind : byte { Shot = 1, Impact = 2, Footstep = 3 }

    public sealed class SynthVoice
    {
        public SynthVoiceKind Kind { get; }
        public float FrequencyHz { get; }
        public float Amplitude { get; private set; }
        public float DurationSeconds { get; }
        public float AgeSeconds { get; private set; }
        public bool Active => AgeSeconds < DurationSeconds && Amplitude > 1e-4f;
        public SynthVoice(SynthVoiceKind kind, float frequencyHz, float amplitude, float durationSeconds)
        {
            if (frequencyHz <= 0 || amplitude < 0 || durationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(frequencyHz));
            Kind = kind; FrequencyHz = frequencyHz; Amplitude = amplitude; DurationSeconds = durationSeconds;
        }
        public void Advance(float dt)
        {
            AgeSeconds += dt;
            float t = Math.Min(1f, AgeSeconds / DurationSeconds);
            Amplitude *= Math.Max(0f, 1f - t);
        }
    }

    /// <summary>Analytic oscillator/noise envelopes. No recorded samples. ≤24 voices, peak limited.</summary>
    public sealed class SynthAudioMixer
    {
        public const int MaxVoices = 24;
        public const float PeakLimit = 1f;
        public const float CpuBudgetMs = 0.5f;

        private readonly SynthVoice?[] voices = new SynthVoice[MaxVoices];
        public int ActiveVoices { get; private set; }
        public float MasterVolume { get; private set; } = 1f;
        public float LastMixCpuMs { get; private set; }

        public void SetMasterVolume(float volume)
        {
            if (volume < 0 || volume > 1) throw new ArgumentOutOfRangeException(nameof(volume));
            MasterVolume = volume;
        }

        public bool TryPlay(SynthVoiceKind kind, float distanceMeters, ulong seed)
        {
            if (distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            float atten = 1f / (1f + distanceMeters * 0.05f);
            float freq = kind switch
            {
                SynthVoiceKind.Shot => 180f + (seed % 40),
                SynthVoiceKind.Impact => 90f + (seed % 30),
                _ => 120f + (seed % 20)
            };
            float dur = kind == SynthVoiceKind.Footstep ? 0.08f : 0.12f;
            int slot = -1;
            for (int i = 0; i < MaxVoices; i++)
                if (voices[i] == null || !voices[i]!.Active) { slot = i; break; }
            if (slot < 0) return false;
            voices[slot] = new SynthVoice(kind, freq, atten * MasterVolume, dur);
            return true;
        }

        public float Mix(float dt)
        {
            if (dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt));
            long start = DateTime.UtcNow.Ticks;
            float sample = 0;
            int active = 0;
            for (int i = 0; i < MaxVoices; i++)
            {
                var v = voices[i];
                if (v == null || !v.Active) { voices[i] = null; continue; }
                v.Advance(dt);
                // Analytic tone + cheap hash noise
                double phase = v.AgeSeconds * v.FrequencyHz * Math.PI * 2;
                float tone = (float)Math.Sin(phase) * v.Amplitude;
                float noise = ((v.AgeSeconds * 12345.678f) % 1f - 0.5f) * 0.15f * v.Amplitude;
                sample += tone + noise;
                active++;
            }
            ActiveVoices = active;
            if (sample > PeakLimit) sample = PeakLimit;
            if (sample < -PeakLimit) sample = -PeakLimit;
            LastMixCpuMs = (DateTime.UtcNow.Ticks - start) / 10000f;
            return sample * MasterVolume;
        }
    }
}
