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
        public int Channels { get; }
        public bool Loop { get; }
        public int FrameIndex { get; private set; }
        public float FilterStateL { get; private set; }
        public float FilterStateR { get; private set; }
        public float AgeSeconds { get; private set; }
        public float DurationSeconds { get; }

        public bool Active
        {
            get
            {
                if (BaseAmplitude <= 1e-4f) return false;
                if (Loop) return true;
                return FrameIndex * Channels < Samples.Length;
            }
        }

        public SynthVoice(SynthVoiceKind kind, float baseAmplitude, float pan, float lowPassCoeff, float[] samples, int sampleRate, bool loop, int channels = CombatAudioBank.Channels)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("samples required", nameof(samples));
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (baseAmplitude < 0) throw new ArgumentOutOfRangeException(nameof(baseAmplitude));
            if (channels < 1 || channels > 2) throw new ArgumentOutOfRangeException(nameof(channels));
            Kind = kind;
            BaseAmplitude = baseAmplitude;
            Pan = Math.Clamp(pan, -1f, 1f);
            LowPassCoeff = Math.Clamp(lowPassCoeff, 0.02f, 1f);
            Samples = samples;
            SampleRate = sampleRate;
            Channels = channels;
            Loop = loop;
            DurationSeconds = (samples.Length / (float)channels) / sampleRate;
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

            int baseIndex = FrameIndex * Channels;
            float rawL = Samples[baseIndex];
            float rawR = Channels > 1 ? Samples[baseIndex + 1] : rawL;
            FrameIndex++;
            if (Loop && FrameIndex * Channels >= Samples.Length) FrameIndex = 0;

            float filteredL = FilterStateL + LowPassCoeff * (rawL - FilterStateL);
            float filteredR = FilterStateR + LowPassCoeff * (rawR - FilterStateR);
            FilterStateL = filteredL;
            FilterStateR = filteredR;

            float amp = BaseAmplitude;
            int totalFrames = Samples.Length / Channels;
            if (!Loop && totalFrames > 8)
            {
                int remain = totalFrames - FrameIndex;
                if (remain < 64)
                {
                    float fade = Math.Max(0f, remain / 64f);
                    amp *= fade;
                }
            }

            // Mild directional balance on top of authored stereo (do not mono-collapse).
            float leftGain = 1f - 0.4f * Math.Max(0f, Pan);
            float rightGain = 1f - 0.4f * Math.Max(0f, -Pan);
            left = filteredL * amp * leftGain;
            right = filteredR * amp * rightGain;
            return 0.5f * (left + right);
        }

        public float EnvelopeAmplitude()
        {
            if (!Active) return 0f;
            if (Loop) return BaseAmplitude;
            int totalFrames = Math.Max(1, Samples.Length / Channels);
            float t = FrameIndex / (float)totalFrames;
            return BaseAmplitude * Math.Max(0f, 1f - t);
        }

        public void Advance(float dt)
        {
            AgeSeconds += dt;
        }
    }

    /// <summary>
    /// Stereo sample-bank mixer (ADR-055 / T089). Found PCM with per-kind distance atten, mild azimuth balance, HF rolloff.
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
            float max = CombatAudioDistance.MaxAudibleMeters(playKind);
            if (max > 0f && distanceMeters > max) return false;

            if (!bank.TryGetClip(playKind, seed, out var samples))
                return false;

            float atten = CombatAudioDistance.Attenuation(playKind, distanceMeters);
            if (atten <= 1e-4f) return false;
            float lp = CombatAudioDistance.LowPassCoeff(distanceMeters, max > 0f ? max : CombatAudioDistance.GunfireMaxMeters);
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
                voices[slot] = new SynthVoice(playKind, atten, pan, lp, samples, bank.SampleRate, loop, CombatAudioBank.Channels);
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
