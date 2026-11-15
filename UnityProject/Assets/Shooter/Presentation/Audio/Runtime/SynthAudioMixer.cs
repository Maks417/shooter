#nullable enable
using System;

namespace Shooter.Presentation.Audio
{
    public enum SynthVoiceKind : byte
    {
        Shot = 1,
        Impact = 2,
        Footstep = 3,
        Ui = 4,
        RifleShot = 5,
        PistolShot = 6,
        ReloadStart = 7,
        ReloadComplete = 8,
        Death = 9,
        DamageTaken = 10,
        Ambient = 11,
        Headshot = 12
    }

    public sealed class SynthVoice
    {
        public SynthVoiceKind Kind { get; }
        /// <summary>Distance/base amplitude before master/effects/UI gains.</summary>
        public float BaseAmplitude { get; }
        public float Pan { get; }
        public float LowPassCoeff { get; }
        public float[] Samples { get; }
        public int SampleRate { get; }
        public bool Loop { get; }
        public int FrameIndex { get; private set; }
        public float FilterState { get; private set; }
        public float AgeSeconds { get; private set; }
        public float DurationSeconds { get; }

        public bool Active
        {
            get
            {
                if (BaseAmplitude <= 1e-4f) return false;
                if (Loop) return true;
                return FrameIndex < Samples.Length;
            }
        }

        public SynthVoice(SynthVoiceKind kind, float baseAmplitude, float pan, float lowPassCoeff, float[] samples, int sampleRate, bool loop)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("samples required", nameof(samples));
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (baseAmplitude < 0) throw new ArgumentOutOfRangeException(nameof(baseAmplitude));
            Kind = kind;
            BaseAmplitude = baseAmplitude;
            Pan = Math.Clamp(pan, -1f, 1f);
            LowPassCoeff = Math.Clamp(lowPassCoeff, 0.02f, 1f);
            Samples = samples;
            SampleRate = sampleRate;
            Loop = loop;
            DurationSeconds = samples.Length / (float)sampleRate;
        }

        public float ReadSample(float dt, out float left, out float right)
        {
            AgeSeconds += dt;
            if (!Active)
            {
                left = 0f;
                right = 0f;
                return 0f;
            }

            float raw = Samples[FrameIndex];
            FrameIndex++;
            if (Loop && FrameIndex >= Samples.Length) FrameIndex = 0;

            // One-pole low-pass; lower coeff = duller far shots.
            float filtered = FilterState + LowPassCoeff * (raw - FilterState);
            FilterState = filtered;

            float amp = BaseAmplitude;
            // Short fade-out for one-shots near the end.
            if (!Loop && Samples.Length > 8)
            {
                int remain = Samples.Length - FrameIndex;
                if (remain < 64)
                {
                    float fade = Math.Max(0f, remain / 64f);
                    amp *= fade;
                }
            }

            float mono = filtered * amp;
            // Constant-power pan.
            float angle = (Pan + 1f) * 0.25f * (float)Math.PI; // 0..π/2
            left = mono * (float)Math.Cos(angle);
            right = mono * (float)Math.Sin(angle);
            return mono;
        }

        // Legacy envelope helper for older tests that inspect duration-style voices.
        public float EnvelopeAmplitude()
        {
            if (!Active) return 0f;
            if (Loop) return BaseAmplitude;
            float t = Samples.Length <= 1 ? 1f : FrameIndex / (float)Samples.Length;
            return BaseAmplitude * Math.Max(0f, 1f - t);
        }

        public void Advance(float dt)
        {
            // Kept for API compatibility; ReadSample advances frames.
            AgeSeconds += dt;
        }
    }

    /// <summary>
    /// Sample-bank mixer (ADR-055). Plays generated PCM cues with distance atten, stereo pan, HF rolloff.
    /// ≤24 voices, peak limited. Device-rate Fill is allocation-free under the mixer lock.
    /// </summary>
    public sealed class SynthAudioMixer
    {
        public const int MaxVoices = 24;
        public const float PeakLimit = 1f;
        public const float CpuBudgetMs = 0.5f;

        private readonly object gate = new object();
        private readonly SynthVoice?[] voices = new SynthVoice[MaxVoices];
        private readonly CombatAudioBank bank;
        private bool ambientPlaying;

        public CombatAudioBank Bank => bank;
        public int ActiveVoices { get; private set; }
        public float MasterVolume { get; private set; } = 1f;
        public float EffectsVolume { get; private set; } = 1f;
        public float UiVolume { get; private set; } = 0.7f;
        public bool MasterMuted { get; private set; }
        public bool EffectsMuted { get; private set; }
        public bool UiMuted { get; private set; }
        public float LastMixCpuMs { get; private set; }
        public int RejectedPlays { get; private set; }

        public SynthAudioMixer() : this(CombatAudioBankBaker.Bake()) { }

        public SynthAudioMixer(CombatAudioBank bank)
        {
            this.bank = bank ?? throw new ArgumentNullException(nameof(bank));
        }

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

        public bool TryPlay(SynthVoiceKind kind, float distanceMeters, ulong seed) =>
            TryPlay(kind, distanceMeters, 0f, seed);

        public bool TryPlay(SynthVoiceKind kind, float distanceMeters, float pan, ulong seed)
        {
            if (distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            var playKind = NormalizeKind(kind);
            if (!bank.TryGetClip(playKind, seed, out var samples))
                return false;

            float atten = 1f / (1f + distanceMeters * 0.05f);
            if (playKind == SynthVoiceKind.Ambient) atten *= 0.35f;
            // Far cues get stronger low-pass (smaller coeff).
            float far = Math.Clamp(distanceMeters / 80f, 0f, 1f);
            float lp = 1f - far * 0.85f;
            if (lp < 0.08f) lp = 0.08f;
            bool loop = playKind == SynthVoiceKind.Ambient;

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
                voices[slot] = new SynthVoice(playKind, atten, pan, lp, samples, bank.SampleRate, loop);
                if (loop) ambientPlaying = true;
                return true;
            }
        }

        public bool EnsureAmbient(ulong seed)
        {
            lock (gate)
            {
                if (ambientPlaying)
                {
                    for (int i = 0; i < MaxVoices; i++)
                    {
                        var v = voices[i];
                        if (v != null && v.Kind == SynthVoiceKind.Ambient && v.Active) return true;
                    }
                    ambientPlaying = false;
                }
            }
            return TryPlay(SynthVoiceKind.Ambient, 0f, 0f, seed);
        }

        public void StopAll()
        {
            lock (gate)
            {
                for (int i = 0; i < MaxVoices; i++) voices[i] = null;
                ActiveVoices = 0;
                ambientPlaying = false;
            }
        }

        /// <summary>Single-sample mix for pure waveform tests. Prefer <see cref="Fill"/> for device output.</summary>
        public float Mix(float dt)
        {
            if (dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt));
            lock (gate)
            {
                long start = DateTime.UtcNow.Ticks;
                MixUnlocked(dt, out float left, out float right);
                LastMixCpuMs = (DateTime.UtcNow.Ticks - start) / 10000f;
                return 0.5f * (left + right);
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
                    MixUnlocked(dt, out float left, out float right);
                    int baseIndex = f * channels;
                    if (channels == 1)
                    {
                        buffer[baseIndex] = 0.5f * (left + right);
                    }
                    else
                    {
                        buffer[baseIndex] = left;
                        buffer[baseIndex + 1] = right;
                        for (int c = 2; c < channels; c++)
                            buffer[baseIndex + c] = 0.5f * (left + right);
                    }
                }
                LastMixCpuMs = (DateTime.UtcNow.Ticks - start) / 10000f;
            }
        }

        private void MixUnlocked(float dt, out float left, out float right)
        {
            left = 0f;
            right = 0f;
            int active = 0;
            ambientPlaying = false;
            for (int i = 0; i < MaxVoices; i++)
            {
                var v = voices[i];
                if (v == null || !v.Active) { voices[i] = null; continue; }
                float gain = ChannelGainUnlocked(v.Kind);
                v.ReadSample(dt, out float l, out float r);
                if (!v.Active) { voices[i] = null; continue; }
                if (v.Kind == SynthVoiceKind.Ambient) ambientPlaying = true;
                left += l * gain;
                right += r * gain;
                active++;
            }
            ActiveVoices = active;
            if (left > PeakLimit) left = PeakLimit;
            if (left < -PeakLimit) left = -PeakLimit;
            if (right > PeakLimit) right = PeakLimit;
            if (right < -PeakLimit) right = -PeakLimit;
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

        private static SynthVoiceKind NormalizeKind(SynthVoiceKind kind) => kind switch
        {
            SynthVoiceKind.Shot => SynthVoiceKind.RifleShot,
            _ => kind
        };
    }
}
