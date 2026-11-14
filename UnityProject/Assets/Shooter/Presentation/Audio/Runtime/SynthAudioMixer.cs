#nullable enable
using System;

namespace Shooter.Presentation.Audio
{
    public enum SynthVoiceKind : byte { Shot = 1, Impact = 2, Footstep = 3, Ui = 4 }

    public sealed class SynthVoice
    {
        public SynthVoiceKind Kind { get; }
        public float FrequencyHz { get; }
        /// <summary>Distance/base amplitude before master/effects/UI gains.</summary>
        public float BaseAmplitude { get; }
        public float DurationSeconds { get; }
        public float AgeSeconds { get; private set; }
        public bool Active => AgeSeconds < DurationSeconds && BaseAmplitude > 1e-4f;
        public SynthVoice(SynthVoiceKind kind, float frequencyHz, float baseAmplitude, float durationSeconds)
        {
            if (frequencyHz <= 0 || baseAmplitude < 0 || durationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(frequencyHz));
            Kind = kind; FrequencyHz = frequencyHz; BaseAmplitude = baseAmplitude; DurationSeconds = durationSeconds;
        }
        public void Advance(float dt)
        {
            AgeSeconds += dt;
        }

        public float EnvelopeAmplitude()
        {
            float t = Math.Min(1f, AgeSeconds / DurationSeconds);
            return BaseAmplitude * Math.Max(0f, 1f - t);
        }
    }

    /// <summary>
    /// Analytic oscillator/noise envelopes. No recorded samples. ≤24 voices, peak limited.
    /// Device-rate Fill is allocation-free and safe under a mixer lock for the audio callback.
    /// </summary>
    public sealed class SynthAudioMixer
    {
        public const int MaxVoices = 24;
        public const float PeakLimit = 1f;
        public const float CpuBudgetMs = 0.5f;

        private readonly object gate = new object();
        private readonly SynthVoice?[] voices = new SynthVoice[MaxVoices];

        public int ActiveVoices { get; private set; }
        public float MasterVolume { get; private set; } = 1f;
        public float EffectsVolume { get; private set; } = 1f;
        public float UiVolume { get; private set; } = 0.7f;
        public bool MasterMuted { get; private set; }
        public bool EffectsMuted { get; private set; }
        public bool UiMuted { get; private set; }
        public float LastMixCpuMs { get; private set; }
        public int RejectedPlays { get; private set; }

        public void SetMasterVolume(float volume)
        {
            if (volume < 0 || volume > 1) throw new ArgumentOutOfRangeException(nameof(volume));
            lock (gate) MasterVolume = volume;
        }

        /// <summary>
        /// Apply channel gains/mutes to subsequent and existing voices (gains are sampled at mix time).
        /// Prefer <see cref="UserSettingsAudioBinding.ApplyGains"/> from ADR-036 UserSettings schema 2.
        /// </summary>
        public void SetChannelGains(float master, float effects, float ui, bool masterMuted = false, bool effectsMuted = false, bool uiMuted = false)
        {
            if (master < 0 || master > 1) throw new ArgumentOutOfRangeException(nameof(master));
            if (effects < 0 || effects > 1) throw new ArgumentOutOfRangeException(nameof(effects));
            if (ui < 0 || ui > 1) throw new ArgumentOutOfRangeException(nameof(ui));
            lock (gate)
            {
                MasterVolume = master;
                EffectsVolume = effects;
                UiVolume = ui;
                MasterMuted = masterMuted;
                EffectsMuted = effectsMuted;
                UiMuted = uiMuted;
            }
        }

        public bool TryPlay(SynthVoiceKind kind, float distanceMeters, ulong seed)
        {
            if (distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            float atten = 1f / (1f + distanceMeters * 0.05f);
            float freq = kind switch
            {
                SynthVoiceKind.Shot => 180f + (seed % 40),
                SynthVoiceKind.Impact => 90f + (seed % 30),
                SynthVoiceKind.Ui => 440f + (seed % 80),
                _ => 120f + (seed % 20)
            };
            float dur = kind switch
            {
                SynthVoiceKind.Footstep => 0.08f,
                SynthVoiceKind.Ui => 0.05f,
                _ => 0.12f
            };
            lock (gate)
            {
                int slot = -1;
                for (int i = 0; i < MaxVoices; i++)
                    if (voices[i] == null || !voices[i]!.Active) { slot = i; break; }
                if (slot < 0)
                {
                    RejectedPlays++;
                    return false;
                }
                voices[slot] = new SynthVoice(kind, freq, atten, dur);
                return true;
            }
        }

        public void StopAll()
        {
            lock (gate)
            {
                for (int i = 0; i < MaxVoices; i++) voices[i] = null;
                ActiveVoices = 0;
            }
        }

        /// <summary>Single-sample mix for pure waveform tests. Prefer <see cref="Fill"/> for device output.</summary>
        public float Mix(float dt)
        {
            if (dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt));
            lock (gate)
            {
                long start = DateTime.UtcNow.Ticks;
                float sample = MixUnlocked(dt);
                LastMixCpuMs = (DateTime.UtcNow.Ticks - start) / 10000f;
                return sample;
            }
        }

        /// <summary>
        /// Device-rate synthesis into an interleaved PCM buffer. No allocations; no authority access.
        /// Intended for Unity audio callbacks.
        /// </summary>
        public void Fill(float[] buffer, int channels, int sampleRate)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (buffer.Length % channels != 0) throw new ArgumentException("buffer length must be a multiple of channels.", nameof(buffer));
            float dt = 1f / sampleRate;
            int frames = buffer.Length / channels;
            lock (gate)
            {
                long start = DateTime.UtcNow.Ticks;
                for (int f = 0; f < frames; f++)
                {
                    float sample = MixUnlocked(dt);
                    int baseIndex = f * channels;
                    for (int c = 0; c < channels; c++)
                        buffer[baseIndex + c] = sample;
                }
                LastMixCpuMs = (DateTime.UtcNow.Ticks - start) / 10000f;
            }
        }

        private float MixUnlocked(float dt)
        {
            float sample = 0;
            int active = 0;
            for (int i = 0; i < MaxVoices; i++)
            {
                var v = voices[i];
                if (v == null || !v.Active) { voices[i] = null; continue; }
                v.Advance(dt);
                float amp = v.EnvelopeAmplitude() * ChannelGainUnlocked(v.Kind);
                if (amp <= 1e-6f)
                {
                    if (!v.Active) voices[i] = null;
                    else active++;
                    continue;
                }
                double phase = v.AgeSeconds * v.FrequencyHz * Math.PI * 2;
                float tone = (float)Math.Sin(phase) * amp;
                float noise = ((v.AgeSeconds * 12345.678f) % 1f - 0.5f) * 0.15f * amp;
                sample += tone + noise;
                active++;
            }
            ActiveVoices = active;
            if (sample > PeakLimit) sample = PeakLimit;
            if (sample < -PeakLimit) sample = -PeakLimit;
            return sample;
        }

        private float ChannelGainUnlocked(SynthVoiceKind kind)
        {
            if (MasterMuted || MasterVolume <= 0f) return 0f;
            if (kind == SynthVoiceKind.Ui)
            {
                if (UiMuted || UiVolume <= 0f) return 0f;
                return MasterVolume * UiVolume;
            }
            if (EffectsMuted || EffectsVolume <= 0f) return 0f;
            return MasterVolume * EffectsVolume;
        }
    }
}
