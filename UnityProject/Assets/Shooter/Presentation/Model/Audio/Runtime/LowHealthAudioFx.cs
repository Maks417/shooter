#nullable enable
using System;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// ADR-103 local damage audio, synthesised live on the audio thread (no samples, no allocation):
    /// a sub-bass "lub-dub" heartbeat below 40 HP whose rate follows health, a short body thud on every hit the local
    /// player takes, and a one-pole low-pass over the whole combat mix while critical (muffled hearing).
    /// The main thread only writes the volatile targets; <see cref="Process"/> runs inside the output callback.
    /// </summary>
    public sealed class LowHealthAudioFx
    {
        public const float HeartbeatHz = 52f;
        public const float ThudHz = 68f;
        public const float CriticalCutoffHz = 2500f;
        public const float BeatDecaySeconds = 0.09f;
        public const float ThudDecaySeconds = 0.16f;

        private volatile float targetHeartGain;
        private volatile float targetRateHz = 1.1f;
        private volatile float targetMuffle;
        private volatile float effectsGain = 1f;
        private volatile int pendingThudMilli;

        private double beatPhase;
        private double beatAge = 10, secondBeatAge = 10, thudAge = 10;
        private double toneTime;
        private float thudAmp;
        private float heartGain, muffle;
        private float lowL, lowR;
        private bool secondPending;

        /// <summary>
        /// Main thread: severity 0..1 (0 = above 40 HP), heartbeat rate and the effects-volume gain.
        /// Reduced flash keeps the heartbeat (it is an audio cue) but muffling is skipped when severity is 0.
        /// </summary>
        public void SetState(float severity, float rateHz, float effectsVolume)
        {
            float s = Clamp01(severity);
            targetHeartGain = s <= 0f ? 0f : 0.18f + 0.32f * s;
            targetRateHz = rateHz > 0.2f && rateHz < 4f ? rateHz : 1.1f;
            targetMuffle = s >= 0.375f ? Clamp01((s - 0.375f) / 0.625f) : 0f; // starts at 25 HP
            effectsGain = Clamp01(effectsVolume);
        }

        /// <summary>Main thread: queue a body thud for a hit of this size (HP).</summary>
        public void Hit(int damage)
        {
            int milli = (int)(Clamp01(0.35f + Math.Max(0, damage) / 80f) * 1000f);
            if (milli > pendingThudMilli) pendingThudMilli = milli;
        }

        public void Reset()
        {
            targetHeartGain = 0f; targetMuffle = 0f; pendingThudMilli = 0;
        }

        /// <summary>Audio thread: post-process an interleaved buffer in place.</summary>
        public void Process(float[] data, int channels, int sampleRate)
        {
            if (data == null || channels < 1 || sampleRate < 8000) return;
            int thud = pendingThudMilli;
            if (thud > 0) { pendingThudMilli = 0; thudAmp = thud / 1000f; thudAge = 0; }
            float heartTarget = targetHeartGain * effectsGain;
            float muffleTarget = targetMuffle;
            float rate = targetRateHz;
            double dt = 1.0 / sampleRate;
            float smooth = (float)(1.0 - Math.Exp(-dt / 0.08));
            int frames = data.Length / channels;
            for (int f = 0; f < frames; f++)
            {
                heartGain += (heartTarget - heartGain) * smooth;
                muffle += (muffleTarget - muffle) * smooth;
                toneTime += dt;
                beatAge += dt; secondBeatAge += dt; thudAge += dt;
                beatPhase += rate * dt;
                if (beatPhase >= 1.0) { beatPhase -= 1.0; beatAge = 0; secondPending = true; }
                if (secondPending && beatPhase >= 0.2) { secondPending = false; secondBeatAge = 0; }

                double tone = 0;
                if (heartGain > 1e-4f)
                {
                    double s1 = Math.Sin(2 * Math.PI * HeartbeatHz * beatAge) * Envelope(beatAge, BeatDecaySeconds);
                    double s2 = 0.65 * Math.Sin(2 * Math.PI * (HeartbeatHz * 0.92) * secondBeatAge) * Envelope(secondBeatAge, BeatDecaySeconds);
                    tone += heartGain * (s1 + s2);
                }
                if (thudAge < ThudDecaySeconds * 6)
                {
                    double pitch = ThudHz * (1.0 + 0.6 * Math.Exp(-thudAge / 0.02)); // short downward sweep
                    tone += thudAmp * effectsGain * 0.55 * Math.Sin(2 * Math.PI * pitch * thudAge) * Envelope(thudAge, ThudDecaySeconds);
                }

                // Muffle: one-pole low-pass blended in as health drops below 25 HP (cutoff ~2.5 kHz).
                float a = (float)(1.0 - Math.Exp(-2.0 * Math.PI * CriticalCutoffHz * dt));
                int i = f * channels;
                float l = data[i];
                lowL += (l - lowL) * a;
                l = l + (lowL - l) * muffle * 0.85f;
                data[i] = Clip(l + (float)tone);
                if (channels > 1)
                {
                    float r = data[i + 1];
                    lowR += (r - lowR) * a;
                    r = r + (lowR - r) * muffle * 0.85f;
                    data[i + 1] = Clip(r + (float)tone);
                    for (int c = 2; c < channels; c++) data[i + c] = Clip(data[i + c] + (float)tone);
                }
            }
            if (toneTime > 3600) toneTime = 0;
        }

        /// <summary>Fast attack (3 ms), exponential decay.</summary>
        public static double Envelope(double age, double decay)
        {
            if (age < 0 || age > decay * 8) return 0;
            double attack = Math.Min(1.0, age / 0.003);
            return attack * Math.Exp(-age / decay);
        }

        private static float Clip(float v) => v > 1f ? 1f : v < -1f ? -1f : v;
        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
