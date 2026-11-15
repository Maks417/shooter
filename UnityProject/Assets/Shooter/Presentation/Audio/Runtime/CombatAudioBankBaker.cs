#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Offline/runtime baker for ADR-055 layered combat PCM. Pure BCL; deterministic for a given recipe id + rate.
    /// Layers (transient, body, mech, LFE, tail) are composited into one mono clip per variation.
    /// </summary>
    public static class CombatAudioBankBaker
    {
        public static CombatAudioBank Bake(int sampleRate = CombatAudioBank.DefaultSampleRate)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            var clips = new float[CombatAudioBank.KindCount][][];
            clips[0] = BakeVariations(sampleRate, BakeRifleShot);
            clips[1] = BakeVariations(sampleRate, BakePistolShot);
            clips[2] = BakeVariations(sampleRate, BakeImpact);
            clips[3] = BakeVariations(sampleRate, BakeDamageTaken);
            clips[4] = BakeVariations(sampleRate, BakeFootstep);
            clips[5] = BakeVariations(sampleRate, BakeReloadStart);
            clips[6] = BakeVariations(sampleRate, BakeReloadComplete);
            clips[7] = BakeVariations(sampleRate, BakeDeath);
            clips[8] = BakeVariations(sampleRate, BakeUi);
            clips[9] = BakeVariations(sampleRate, BakeAmbient, count: 1);
            clips[10] = BakeVariations(sampleRate, BakeHeadshot);

            string hash = ComputeHash(sampleRate, clips);
            return new CombatAudioBank(sampleRate, clips, hash);
        }

        private static float[][] BakeVariations(int sampleRate, Func<int, int, float[]> bake, int count = CombatAudioBank.VariationsPerCue)
        {
            var vars = new float[count][];
            for (int v = 0; v < count; v++)
                vars[v] = bake(sampleRate, v);
            return vars;
        }

        private static float[] BakeRifleShot(int rate, int variation)
        {
            int n = (int)(rate * 0.48f);
            var buf = new float[n];
            uint rng = MixSeed(0xA11F1E01u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.018f, 1.00f, 1800f, 9000f); // transient / muzzle
            AddNoiseBurst(buf, rate, ref rng, 0.002f, 0.120f, 0.72f, 200f, 2800f);   // body
            AddThump(buf, rate, 0.000f, 0.080f, 68f + variation * 3f, 0.55f);         // LFE
            AddMetallicClick(buf, rate, ref rng, 0.012f, 0.060f, 2200f + variation * 40f, 0.35f); // mech
            AddNoiseBurst(buf, rate, ref rng, 0.040f, 0.420f, 0.28f, 120f, 1400f);  // tail
            NormalizePeak(buf, 0.92f);
            return buf;
        }

        private static float[] BakePistolShot(int rate, int variation)
        {
            int n = (int)(rate * 0.34f);
            var buf = new float[n];
            uint rng = MixSeed(0xB1570101u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.012f, 0.95f, 2200f, 11000f);
            AddNoiseBurst(buf, rate, ref rng, 0.001f, 0.070f, 0.55f, 400f, 3600f);
            AddThump(buf, rate, 0.000f, 0.045f, 95f + variation * 4f, 0.38f);
            AddMetallicClick(buf, rate, ref rng, 0.008f, 0.050f, 2800f + variation * 50f, 0.42f);
            AddNoiseBurst(buf, rate, ref rng, 0.030f, 0.280f, 0.18f, 180f, 1600f);
            NormalizePeak(buf, 0.88f);
            return buf;
        }

        private static float[] BakeImpact(int rate, int variation)
        {
            int n = (int)(rate * 0.22f);
            var buf = new float[n];
            uint rng = MixSeed(0x1A7AC701u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.025f, 0.85f, 800f, 6000f);
            AddThump(buf, rate, 0.000f, 0.090f, 110f + variation * 8f, 0.45f);
            AddNoiseBurst(buf, rate, ref rng, 0.010f, 0.180f, 0.22f, 200f, 2200f);
            NormalizePeak(buf, 0.80f);
            return buf;
        }

        private static float[] BakeDamageTaken(int rate, int variation)
        {
            int n = (int)(rate * 0.16f);
            var buf = new float[n];
            uint rng = MixSeed(0xD4A4A6E1u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.020f, 0.90f, 500f, 4000f);
            AddThump(buf, rate, 0.000f, 0.070f, 70f + variation * 5f, 0.65f);
            AddMetallicClick(buf, rate, ref rng, 0.004f, 0.040f, 1600f, 0.25f);
            NormalizePeak(buf, 0.85f);
            return buf;
        }

        /// <summary>Sharper, higher crack than body Impact/DamageTaken (gameplay region 1 = head).</summary>
        private static float[] BakeHeadshot(int rate, int variation)
        {
            int n = (int)(rate * 0.18f);
            var buf = new float[n];
            uint rng = MixSeed(0xCEA05001u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.012f, 1.00f, 2500f, 12000f); // bright transient
            AddMetallicClick(buf, rate, ref rng, 0.001f, 0.045f, 3200f + variation * 60f, 0.70f);
            AddThump(buf, rate, 0.000f, 0.050f, 85f + variation * 4f, 0.35f);
            AddNoiseBurst(buf, rate, ref rng, 0.008f, 0.120f, 0.28f, 600f, 4500f);
            NormalizePeak(buf, 0.90f);
            return buf;
        }

        private static float[] BakeFootstep(int rate, int variation)
        {
            int n = (int)(rate * 0.11f);
            var buf = new float[n];
            uint rng = MixSeed(0xF0075701u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.035f, 0.55f, 80f, 900f);
            AddThump(buf, rate, 0.000f, 0.050f, 55f + variation * 4f, 0.40f);
            AddNoiseBurst(buf, rate, ref rng, 0.015f, 0.080f, 0.18f, 200f, 1800f);
            NormalizePeak(buf, 0.55f);
            return buf;
        }

        private static float[] BakeReloadStart(int rate, int variation)
        {
            int n = (int)(rate * 0.28f);
            var buf = new float[n];
            uint rng = MixSeed(0x5E10AD01u, variation);
            AddMetallicClick(buf, rate, ref rng, 0.000f, 0.080f, 1400f + variation * 30f, 0.55f);
            AddNoiseBurst(buf, rate, ref rng, 0.040f, 0.160f, 0.25f, 300f, 2500f);
            AddMetallicClick(buf, rate, ref rng, 0.120f, 0.100f, 1900f, 0.35f);
            NormalizePeak(buf, 0.70f);
            return buf;
        }

        private static float[] BakeReloadComplete(int rate, int variation)
        {
            int n = (int)(rate * 0.32f);
            var buf = new float[n];
            uint rng = MixSeed(0x5E10AC01u, variation);
            AddMetallicClick(buf, rate, ref rng, 0.000f, 0.070f, 1700f + variation * 25f, 0.50f);
            AddNoiseBurst(buf, rate, ref rng, 0.050f, 0.120f, 0.30f, 250f, 2200f);
            AddMetallicClick(buf, rate, ref rng, 0.160f, 0.090f, 2400f, 0.60f);
            AddThump(buf, rate, 0.180f, 0.060f, 90f, 0.25f);
            NormalizePeak(buf, 0.72f);
            return buf;
        }

        private static float[] BakeDeath(int rate, int variation)
        {
            int n = (int)(rate * 0.45f);
            var buf = new float[n];
            uint rng = MixSeed(0xDE47A001u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.080f, 0.50f, 100f, 1200f);
            AddThump(buf, rate, 0.020f, 0.200f, 45f + variation * 3f, 0.55f);
            AddNoiseBurst(buf, rate, ref rng, 0.100f, 0.300f, 0.20f, 80f, 800f);
            NormalizePeak(buf, 0.65f);
            return buf;
        }

        private static float[] BakeUi(int rate, int variation)
        {
            int n = (int)(rate * 0.07f);
            var buf = new float[n];
            float f0 = 520f + variation * 35f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = 1f - t / 0.07f;
                if (env < 0f) env = 0f;
                env *= env;
                buf[i] = (float)(Math.Sin(2 * Math.PI * f0 * t) * 0.35 * env
                    + Math.Sin(2 * Math.PI * f0 * 1.5 * t) * 0.12 * env);
            }
            NormalizePeak(buf, 0.50f);
            return buf;
        }

        private static float[] BakeAmbient(int rate, int variation)
        {
            int n = rate * 2; // 2 s loop
            var buf = new float[n];
            uint rng = MixSeed(0xA4B1E001u, variation);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float wind = (float)Math.Sin(2 * Math.PI * 0.15 * t) * 0.02f;
                float hiss = (NextFloat(ref rng) - 0.5f) * 0.018f;
                float hum = (float)Math.Sin(2 * Math.PI * 55 * t) * 0.008f;
                buf[i] = wind + hiss + hum;
            }
            // Crossfade edges for seamless loop
            int fade = rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                float a = buf[i];
                float b = buf[n - fade + i];
                buf[i] = b * (1f - w) + a * w;
                buf[n - fade + i] = a * (1f - w) + b * w;
            }
            NormalizePeak(buf, 0.12f);
            return buf;
        }

        private static void AddNoiseBurst(float[] buf, int rate, ref uint rng, float startSec, float durSec, float amp, float lowHz, float highHz)
        {
            int start = (int)(startSec * rate);
            int len = Math.Max(1, (int)(durSec * rate));
            float lp = 0f;
            float hp = 0f;
            float hpCoeff = Math.Clamp(highHz / (rate * 0.5f), 0.01f, 0.95f);
            float lpCoeff = Math.Clamp(lowHz / (rate * 0.5f), 0.01f, 0.95f);
            for (int i = 0; i < len; i++)
            {
                int idx = start + i;
                if (idx < 0 || idx >= buf.Length) continue;
                float t = i / (float)len;
                float env = (float)Math.Exp(-4.2 * t) * (1f - t * 0.15f);
                float n = NextFloat(ref rng) * 2f - 1f;
                // crude band shaping
                lp += lpCoeff * (n - lp);
                hp += hpCoeff * ((n - lp) - hp);
                buf[idx] += hp * amp * env;
            }
        }

        private static void AddThump(float[] buf, int rate, float startSec, float durSec, float freqHz, float amp)
        {
            int start = (int)(startSec * rate);
            int len = Math.Max(1, (int)(durSec * rate));
            for (int i = 0; i < len; i++)
            {
                int idx = start + i;
                if (idx < 0 || idx >= buf.Length) continue;
                float t = i / (float)rate;
                float env = (float)Math.Exp(-12.0 * (i / (float)len));
                float pitch = freqHz * (1f - 0.35f * (i / (float)len));
                buf[idx] += (float)(Math.Sin(2 * Math.PI * pitch * t) * amp * env);
            }
        }

        private static void AddMetallicClick(float[] buf, int rate, ref uint rng, float startSec, float durSec, float freqHz, float amp)
        {
            int start = (int)(startSec * rate);
            int len = Math.Max(1, (int)(durSec * rate));
            for (int i = 0; i < len; i++)
            {
                int idx = start + i;
                if (idx < 0 || idx >= buf.Length) continue;
                float t = i / (float)rate;
                float env = (float)Math.Exp(-18.0 * (i / (float)len));
                float tone = (float)Math.Sin(2 * Math.PI * freqHz * t);
                float over = (float)Math.Sin(2 * Math.PI * freqHz * 2.7 * t) * 0.4f;
                float n = (NextFloat(ref rng) * 2f - 1f) * 0.25f;
                buf[idx] += (tone + over + n) * amp * env;
            }
        }

        private static void NormalizePeak(float[] buf, float targetPeak)
        {
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float a = Math.Abs(buf[i]);
                if (a > peak) peak = a;
            }
            if (peak < 1e-8f) return;
            float g = targetPeak / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        private static string ComputeHash(int sampleRate, float[][][] clips)
        {
            using var sha = SHA256.Create();
            var preamble = Encoding.ASCII.GetBytes(CombatAudioBank.RecipeId + "|" + CombatAudioBank.BankVersion + "|" + sampleRate + "|");
            sha.TransformBlock(preamble, 0, preamble.Length, null, 0);
            var scratch = new byte[4];
            for (int k = 0; k < clips.Length; k++)
            {
                if (clips[k] == null) continue;
                for (int v = 0; v < clips[k].Length; v++)
                {
                    var s = clips[k][v];
                    if (s == null) continue;
                    WriteIntLe(scratch, s.Length);
                    sha.TransformBlock(scratch, 0, 4, null, 0);
                    // Stride sample for speed while remaining sensitive to recipe changes.
                    for (int i = 0; i < s.Length; i += 64)
                    {
                        WriteFloatLe(scratch, s[i]);
                        sha.TransformBlock(scratch, 0, 4, null, 0);
                    }
                    if (s.Length > 0)
                    {
                        WriteFloatLe(scratch, s[s.Length - 1]);
                        sha.TransformBlock(scratch, 0, 4, null, 0);
                    }
                }
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hash = sha.Hash ?? Array.Empty<byte>();
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        private static void WriteIntLe(byte[] dst, int value)
        {
            dst[0] = (byte)value;
            dst[1] = (byte)(value >> 8);
            dst[2] = (byte)(value >> 16);
            dst[3] = (byte)(value >> 24);
        }

        private static void WriteFloatLe(byte[] dst, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                dst[0] = bytes[0];
                dst[1] = bytes[1];
                dst[2] = bytes[2];
                dst[3] = bytes[3];
            }
            else
            {
                dst[0] = bytes[3];
                dst[1] = bytes[2];
                dst[2] = bytes[1];
                dst[3] = bytes[0];
            }
        }

        private static uint MixSeed(uint tag, int variation) => tag ^ ((uint)variation * 0x9E3779B9u) ^ 0xA5A5A5A5u;

        private static float NextFloat(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
