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
        Headshot = 12,
        KnifeSwing = 13,
        GrenadeThrow = 14,
        GrenadeExplode = 15
    }

    public sealed class SynthVoice
    {
        public SynthVoiceKind Kind { get; }
        /// <summary>Distance/base amplitude before master/effects/UI gains.</summary>
        public float BaseAmplitude { get; }
        public float Pan { get; private set; }
        public float LowPassCoeff { get; }
        public float[] Samples { get; }
        public int SampleRate { get; }
        public int Channels { get; }
        public bool Loop { get; }
        public int FrameIndex { get; private set; }
        private double framePosition;
        private readonly float reflection;
        private readonly float body;
        private readonly float stereoWidth;
        private float leftPanGain, rightPanGain;
        private readonly float sourceX, sourceZ;
        private float bodyL, bodyR;
        private float cachedDt, bodyCoeff, filterCoeff;
        public float Priority { get; }
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
                return framePosition < Samples.Length / Channels + (reflection > 0f ? SampleRate * 0.137 : 0);
            }
        }

        public SynthVoice(SynthVoiceKind kind, float baseAmplitude, float pan, float lowPassCoeff, float[] samples, int sampleRate, bool loop, int channels = CombatAudioBank.Channels,
            float priority = 0f, float reflection = 0f, float body = 0f, float stereoWidth = 1f,
            float sourceX = float.NaN, float sourceZ = float.NaN)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("samples required", nameof(samples));
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (baseAmplitude < 0) throw new ArgumentOutOfRangeException(nameof(baseAmplitude));
            if (channels < 1 || channels > 2) throw new ArgumentOutOfRangeException(nameof(channels));
            Kind = kind;
            BaseAmplitude = baseAmplitude;
            Pan = Math.Clamp(pan, -1f, 1f);
            leftPanGain = (float)Math.Sqrt(1f - Pan);
            rightPanGain = (float)Math.Sqrt(1f + Pan);
            this.sourceX = sourceX;
            this.sourceZ = sourceZ;
            LowPassCoeff = Math.Clamp(lowPassCoeff, 0.02f, 1f);
            Samples = samples;
            SampleRate = sampleRate;
            Channels = channels;
            Loop = loop;
            Priority = priority;
            this.reflection = Math.Clamp(reflection, 0f, 1f);
            this.body = Math.Clamp(body, 0f, 1f);
            this.stereoWidth = Math.Clamp(stereoWidth, 0f, 1f);
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

            float rawL = ReadAt(framePosition, 0);
            float rawR = ReadAt(framePosition, Channels > 1 ? 1 : 0);
            // Two quiet, cross-fed early reflections reuse recorded PCM within the same voice.
            // These are geometry-controlled reflections, not a claim of room-acoustic simulation.
            if (reflection > 0f)
            {
                rawL += reflection * (0.23f * ReadAt(framePosition - SampleRate * 0.061, Channels > 1 ? 1 : 0)
                    + 0.12f * ReadAt(framePosition - SampleRate * 0.137, 0));
                rawR += reflection * (0.23f * ReadAt(framePosition - SampleRate * 0.073, 0)
                    + 0.12f * ReadAt(framePosition - SampleRate * 0.137, Channels > 1 ? 1 : 0));
            }
            // Close gunfire gets a restrained low-frequency body layer derived from its recording.
            if (dt != cachedDt)
            {
                cachedDt = dt;
                bodyCoeff = 1f - (float)Math.Exp(-2.0 * Math.PI * 180.0 * dt);
                filterCoeff = 1f - (float)Math.Pow(1f - LowPassCoeff, SampleRate * dt);
            }
            bodyL += bodyCoeff * (rawL - bodyL);
            bodyR += bodyCoeff * (rawR - bodyR);
            rawL += bodyL * body * 0.28f;
            rawR += bodyR * body * 0.28f;
            // Preserve cutoff when the output device runs at 44.1/96 kHz instead of 48 kHz.
            float filteredL = FilterStateL + filterCoeff * (rawL - FilterStateL);
            float filteredR = FilterStateR + filterCoeff * (rawR - FilterStateR);
            FilterStateL = filteredL;
            FilterStateR = filteredR;

            float amp = BaseAmplitude;

            float mid = (filteredL + filteredR) * 0.5f;
            float side = (filteredL - filteredR) * 0.5f * stereoWidth;
            // Equal-power direction for world emitters; local/ambient stereo retains full width.
            left = (mid + side) * amp * leftPanGain;
            right = (mid - side) * amp * rightPanGain;
            framePosition += SampleRate * (double)dt;
            if (Loop) framePosition %= Samples.Length / Channels;
            FrameIndex = (int)framePosition;
            return 0.5f * (left + right);
        }

        private float ReadAt(double position, int channel)
        {
            int frames = Samples.Length / Channels;
            if (position < 0 || position >= frames) return 0f;
            int index = (int)position;
            int next = Loop ? (index + 1) % frames : Math.Min(index + 1, frames - 1);
            float t = (float)(position - index);
            float value = Samples[index * Channels + channel];
            float result = value + t * (Samples[next * Channels + channel] - value);
            return !Loop ? result * Math.Min(1f, (frames - (float)position) / 64f) : result;
        }

        public void UpdateListener(float yaw, float x, float z)
        {
            if (float.IsNaN(sourceX) || float.IsNaN(sourceZ)) return;
            float dx = sourceX - x, dz = sourceZ - z;
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            Pan = length < 1e-4f ? 0f : Math.Clamp((dx * (float)Math.Cos(yaw) - dz * (float)Math.Sin(yaw)) / length, -1f, 1f);
            leftPanGain = (float)Math.Sqrt(1f - Pan);
            rightPanGain = (float)Math.Sqrt(1f + Pan);
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
        private float limiterGain = 1f;

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
        public int StolenVoices { get; private set; }

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
            => TryPlay(kind, distanceMeters, pan, seed, false, 0f, 0f);

        public bool TryPlay(SynthVoiceKind kind, float distanceMeters, float pan, ulong seed,
            bool localPlayer, float obstruction, float reflection, float sourceX = float.NaN, float sourceZ = float.NaN)
        {
            if (distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            var playKind = NormalizeKind(kind);
            float max = CombatAudioDistance.MaxAudibleMeters(playKind);
            if (max > 0f && distanceMeters > max) return false;

            if (!bank.TryGetClip(playKind, seed, out var samples))
                return false;

            float atten = CombatAudioDistance.Attenuation(playKind, distanceMeters);
            obstruction = Math.Clamp(obstruction, 0f, 1f);
            atten *= 1f - obstruction * 0.65f;
            if (atten <= 1e-4f) return false;
            float lp = CombatAudioDistance.LowPassCoeff(distanceMeters, max > 0f ? max : CombatAudioDistance.GunfireMaxMeters);
            lp *= 1f - obstruction * 0.94f;
            bool loop = playKind == SynthVoiceKind.Ambient;
            bool gun = playKind == SynthVoiceKind.RifleShot || playKind == SynthVoiceKind.PistolShot || playKind == SynthVoiceKind.GrenadeExplode;
            float priority = (localPlayer || playKind == SynthVoiceKind.Ui ? 100f : gun ? 70f : loop ? 0f : 35f)
                + Math.Min(1.5f, atten) * 10f;

            lock (gate)
            {
                int slot = -1;
                for (int i = 0; i < MaxVoices; i++)
                    if (voices[i] == null || !voices[i]!.Active) { slot = i; break; }
                if (slot < 0)
                {
                    float weakest = priority;
                    for (int i = 0; i < MaxVoices; i++)
                        if (voices[i]!.Priority < weakest) { weakest = voices[i]!.Priority; slot = i; }
                    if (slot < 0) { RejectedPlays++; return false; }
                    StolenVoices++;
                }
                voices[slot] = new SynthVoice(playKind, atten, pan, lp, samples, bank.SampleRate, loop, CombatAudioBank.Channels,
                    priority, gun ? reflection : 0f, gun ? Math.Max(0f, 1f - distanceMeters / 18f) : 0f,
                    localPlayer || loop || playKind == SynthVoiceKind.Ui ? 1f : 0.15f + 0.35f * Math.Max(0f, 1f - distanceMeters / Math.Max(1f, max)),
                    localPlayer ? float.NaN : sourceX, localPlayer ? float.NaN : sourceZ);
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

        public void UpdateListener(float yawRadians, float x, float z)
        {
            lock (gate)
                for (int i = 0; i < MaxVoices; i++) voices[i]?.UpdateListener(yawRadians, x, z);
        }

        public void StopAll()
        {
            lock (gate)
            {
                for (int i = 0; i < MaxVoices; i++) voices[i] = null;
                ActiveVoices = 0;
                ambientPlaying = false;
                limiterGain = 1f;
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
                if (v.Kind == SynthVoiceKind.Ambient) ambientPlaying = true;
                left += l * gain;
                right += r * gain;
                active++;
            }
            ActiveVoices = active;
            // Linked stereo limiter: immediate attack, 80 ms release, 1 dB headroom.
            // Preserve the stereo image rather than clipping each channel independently.
            float peak = Math.Max(Math.Abs(left), Math.Abs(right));
            float target = peak > 0.89f ? 0.89f / peak : 1f;
            limiterGain = target < limiterGain ? target : Math.Min(target, limiterGain + dt / 0.08f);
            left *= limiterGain;
            right *= limiterGain;
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
