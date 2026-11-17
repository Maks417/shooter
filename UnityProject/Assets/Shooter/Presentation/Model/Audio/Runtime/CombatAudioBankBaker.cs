#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Loads stereo found/authored WAV banks when present; otherwise bakes stereo emergency fallback (T089 / ADR-055).
    /// </summary>
    public static class CombatAudioBankBaker
    {
        public static CombatAudioBank Bake(int sampleRate = CombatAudioBank.DefaultSampleRate, string? sampleRoot = null)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            string? root = CombatAudioSamplePaths.ResolveCombatSampleRoot(sampleRoot);
            if (root != null)
            {
                try
                {
                    var loaded = TryLoadFound(root, sampleRate);
                    if (loaded != null) return loaded;
                }
                catch
                {
                    // Fall through to generated emergency bank.
                }
            }
            return BakeGenerated(sampleRate);
        }

        public static CombatAudioBank BakeGenerated(int sampleRate = CombatAudioBank.DefaultSampleRate)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            var clips = new float[CombatAudioBank.KindCount][][];
            for (int k = 0; k < clips.Length; k++)
                clips[k] = BakeVariations(sampleRate, FallbackBaker(k), count: k == CombatAudioBank.AmbientIndex ? 1 : CombatAudioBank.VariationsPerCue);
            string hash = ComputeHash(sampleRate, clips, found: false);
            return new CombatAudioBank(sampleRate, clips, hash, loadedFromFoundSamples: false);
        }

        private static CombatAudioBank? TryLoadFound(string root, int sampleRate)
        {
            string manifestPath = Path.Combine(root, CombatAudioSamplePaths.ManifestFileName);
            if (!File.Exists(manifestPath)) return null;
            var manifest = CombatAudioManifest.ParseFile(manifestPath);
            if (manifest.Channels != CombatAudioBank.Channels) return null;

            var clips = new float[CombatAudioBank.KindCount][][];
            for (int k = 0; k < clips.Length; k++)
                clips[k] = new float[CombatAudioBank.MaxVariationsPerCue][];

            int loaded = 0;
            for (int i = 0; i < manifest.Clips.Count; i++)
            {
                var entry = manifest.Clips[i];
                int kindIndex = CombatAudioBank.KindFolderToIndex(entry.Kind);
                if (kindIndex < 0) continue;
                if (entry.Variation < 0 || entry.Variation >= CombatAudioBank.MaxVariationsPerCue) continue;

                string path = Path.Combine(root, entry.File);
                if (!File.Exists(path)) return null;
                string actualHash = HashFileHex(path);
                if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    return null;

                var wav = WavPcmReader.ReadFile(path);
                float[] stereo = WavPcmReader.ToStereo48k(wav, sampleRate);
                if (stereo.Length < CombatAudioBank.Channels) return null;
                clips[kindIndex][entry.Variation] = stereo;
                loaded++;
            }

            // Compact each cue to the variations actually shipped; a cue missing from the manifest
            // falls back to its generated emergency recipe only. Named-gun reports missing from the manifest are
            // derived from the shipped real rifle/pistol takes after compaction (ADR-102).
            var derive = new bool[clips.Length];
            for (int k = 0; k < clips.Length; k++)
            {
                int present = 0;
                for (int v = 0; v < clips[k].Length; v++)
                    if (clips[k][v] != null && clips[k][v].Length > 0) present++;
                if (present == 0)
                {
                    derive[k] = DerivedFrom(CombatAudioBank.KindAt(k)).source != 0;
                    clips[k] = BakeVariations(sampleRate, FallbackBaker(k), count: k == CombatAudioBank.AmbientIndex ? 1 : CombatAudioBank.VariationsPerCue);
                    continue;
                }
                var compact = new float[present][];
                int w = 0;
                for (int v = 0; v < clips[k].Length; v++)
                    if (clips[k][v] != null && clips[k][v].Length > 0) compact[w++] = clips[k][v];
                clips[k] = compact;
            }

            if (loaded == 0) return null;
            for (int k = 0; k < clips.Length; k++)
            {
                if (!derive[k]) continue;
                var (source, pitch, gain) = DerivedFrom(CombatAudioBank.KindAt(k));
                var takes = clips[CombatAudioBank.KindIndex(source)];
                var derived = new float[takes.Length][];
                for (int v = 0; v < takes.Length; v++) derived[v] = Reshape(takes[v], pitch, gain);
                clips[k] = derived;
            }
            string hash = ComputeHash(sampleRate, clips, found: true);
            return new CombatAudioBank(sampleRate, clips, hash, loadedFromFoundSamples: true);
        }

        private static Func<int, int, float[]> FallbackBaker(int kindIndex) => CombatAudioBank.KindAt(kindIndex) switch
        {
            SynthVoiceKind.RifleShot => BakeRifleShot,
            SynthVoiceKind.PistolShot => BakePistolShot,
            SynthVoiceKind.Impact => BakeImpact,
            SynthVoiceKind.DamageTaken => BakeDamageTaken,
            SynthVoiceKind.Footstep => BakeFootstep,
            SynthVoiceKind.ReloadStart => BakeReloadStart,
            SynthVoiceKind.ReloadComplete => BakeReloadComplete,
            SynthVoiceKind.Death => BakeDeath,
            SynthVoiceKind.Ui => BakeUi,
            SynthVoiceKind.Ambient => BakeAmbient,
            SynthVoiceKind.Headshot => BakeHeadshot,
            SynthVoiceKind.KnifeSwing => BakeKnifeSwing,
            SynthVoiceKind.GrenadeThrow => BakeGrenadeThrow,
            SynthVoiceKind.GrenadeExplode => BakeGrenadeExplode,
            // ADR-096 cues: emergency fallback reuses the nearest existing recipe.
            SynthVoiceKind.ReloadPistol => BakeReloadStart,
            SynthVoiceKind.KnifeHitFlesh => BakeDamageTaken,
            SynthVoiceKind.KnifeHitHard => BakeImpact,
            SynthVoiceKind.GrenadePin => BakeReloadComplete,
            SynthVoiceKind.GrenadeBounce => BakeImpact,
            SynthVoiceKind.GrenadeExplodeFar => BakeGrenadeExplode,
            SynthVoiceKind.DryFire => BakeReloadComplete,
            SynthVoiceKind.WeaponSwitch => BakeReloadStart,
            SynthVoiceKind.HitMarker => BakeUi,
            SynthVoiceKind.KillConfirm => BakeUi,
            SynthVoiceKind.BulletFlyby => BakeKnifeSwing,
            SynthVoiceKind.DistantBattle => BakeRifleShot,
            SynthVoiceKind.UiHover => BakeUi,
            SynthVoiceKind.UiClick => BakeUi,
            SynthVoiceKind.UiBack => BakeUi,
            // ADR-102 v6 emergency fallbacks (the found-sample path derives shots from real takes instead).
            SynthVoiceKind.Ak47Shot => BakeRifleShot,
            SynthVoiceKind.Ak102Shot => BakeRifleShot,
            SynthVoiceKind.M16Shot => BakeRifleShot,
            SynthVoiceKind.Mk14Shot => BakeRifleShot,
            SynthVoiceKind.Tac338Shot => BakeRifleShot,
            SynthVoiceKind.G17Shot => BakePistolShot,
            SynthVoiceKind.EagleShot => BakePistolShot,
            SynthVoiceKind.BoltCycle => BakeReloadComplete,
            SynthVoiceKind.ReloadAk => BakeReloadStart,
            SynthVoiceKind.ReloadBolt => BakeReloadStart,
            _ => BakeImpact
        };

        /// <summary>
        /// Interim character for a named gun without its own recording: the shipped real take of the nearest
        /// class, slowed (deeper, longer tail) or sped up. Source 0 = not derived.
        /// </summary>
        public static (SynthVoiceKind source, float pitch, float gain) DerivedFrom(SynthVoiceKind kind) => kind switch
        {
            SynthVoiceKind.Ak47Shot => (SynthVoiceKind.RifleShot, 0.90f, 1.00f),
            SynthVoiceKind.Ak102Shot => (SynthVoiceKind.RifleShot, 1.07f, 0.95f),
            SynthVoiceKind.M16Shot => (SynthVoiceKind.RifleShot, 1.12f, 0.92f),
            SynthVoiceKind.Mk14Shot => (SynthVoiceKind.RifleShot, 0.84f, 1.00f),
            SynthVoiceKind.Tac338Shot => (SynthVoiceKind.RifleShot, 0.70f, 1.00f),
            SynthVoiceKind.G17Shot => (SynthVoiceKind.PistolShot, 1.06f, 0.95f),
            SynthVoiceKind.EagleShot => (SynthVoiceKind.PistolShot, 0.74f, 1.00f),
            SynthVoiceKind.ReloadAk => (SynthVoiceKind.ReloadStart, 0.94f, 1.00f),
            SynthVoiceKind.ReloadBolt => (SynthVoiceKind.ReloadStart, 0.88f, 1.00f),
            SynthVoiceKind.BoltCycle => (SynthVoiceKind.ReloadComplete, 0.85f, 1.00f),
            _ => (default, 1f, 1f)
        };

        /// <summary>Linear-interpolated varispeed of an interleaved stereo clip, with a peak-safe gain.</summary>
        public static float[] Reshape(float[] stereo, float pitch, float gain)
        {
            if (stereo == null || stereo.Length < CombatAudioBank.Channels) return Array.Empty<float>();
            if (!(pitch > 0.25f && pitch < 4f)) throw new ArgumentOutOfRangeException(nameof(pitch));
            int frames = stereo.Length / CombatAudioBank.Channels;
            int outFrames = Math.Max(1, (int)(frames / pitch));
            var result = new float[outFrames * CombatAudioBank.Channels];
            for (int i = 0; i < outFrames; i++)
            {
                double at = i * (double)pitch;
                int a = (int)at; int b = Math.Min(frames - 1, a + 1); float t = (float)(at - a);
                if (a >= frames) a = frames - 1;
                for (int c = 0; c < CombatAudioBank.Channels; c++)
                {
                    float v = stereo[a * 2 + c] + (stereo[b * 2 + c] - stereo[a * 2 + c]) * t;
                    result[i * 2 + c] = Math.Max(-1f, Math.Min(1f, v * gain));
                }
            }
            return result;
        }

        private static float[][] BakeVariations(int sampleRate, Func<int, int, float[]> bake, int count = CombatAudioBank.VariationsPerCue)
        {
            var vars = new float[count][];
            for (int v = 0; v < count; v++)
                vars[v] = bake(sampleRate, v);
            return vars;
        }

        private static float[] BakeRifleShot(int rate, int variation) =>
            Stereoize(BakeMonoRifle(rate, variation), rate, variation, 0.00035f);

        private static float[] BakePistolShot(int rate, int variation) =>
            Stereoize(BakeMonoPistol(rate, variation), rate, variation, 0.00028f);

        private static float[] BakeImpact(int rate, int variation) =>
            Stereoize(BakeMonoImpact(rate, variation), rate, variation, 0.00022f);

        private static float[] BakeDamageTaken(int rate, int variation) =>
            Stereoize(BakeMonoDamage(rate, variation), rate, variation, 0.0002f);

        private static float[] BakeHeadshot(int rate, int variation) =>
            Stereoize(BakeMonoHeadshot(rate, variation), rate, variation, 0.00025f);

        private static float[] BakeKnifeSwing(int rate, int variation) =>
            Stereoize(BakeMonoKnifeSwing(rate, variation), rate, variation, 0.00055f);

        private static float[] BakeGrenadeThrow(int rate, int variation) =>
            Stereoize(BakeMonoGrenadeThrow(rate, variation), rate, variation, 0.0004f);

        private static float[] BakeGrenadeExplode(int rate, int variation) =>
            Stereoize(BakeMonoGrenadeExplode(rate, variation), rate, variation, 0.0007f);

        private static float[] BakeFootstep(int rate, int variation) =>
            Stereoize(BakeMonoFootstep(rate, variation), rate, variation, 0.0004f);

        private static float[] BakeReloadStart(int rate, int variation) =>
            Stereoize(BakeMonoReloadStart(rate, variation), rate, variation, 0.00018f);

        private static float[] BakeReloadComplete(int rate, int variation) =>
            Stereoize(BakeMonoReloadComplete(rate, variation), rate, variation, 0.00018f);

        private static float[] BakeDeath(int rate, int variation) =>
            Stereoize(BakeMonoDeath(rate, variation), rate, variation, 0.0005f);

        private static float[] BakeUi(int rate, int variation) =>
            Stereoize(BakeMonoUi(rate, variation), rate, variation, 0.0001f);

        private static float[] BakeAmbient(int rate, int variation) =>
            Stereoize(BakeMonoAmbient(rate, variation), rate, variation, 0.0012f);

        private static float[] BakeMonoRifle(int rate, int variation)
        {
            int n = (int)(rate * 0.48f);
            var buf = new float[n];
            uint rng = MixSeed(0xA11F1E01u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.018f, 1.00f, 1800f, 9000f);
            AddNoiseBurst(buf, rate, ref rng, 0.002f, 0.120f, 0.72f, 200f, 2800f);
            AddThump(buf, rate, 0.000f, 0.080f, 68f + variation * 3f, 0.55f);
            AddMetallicClick(buf, rate, ref rng, 0.012f, 0.060f, 2200f + variation * 40f, 0.35f);
            AddNoiseBurst(buf, rate, ref rng, 0.040f, 0.420f, 0.28f, 120f, 1400f);
            NormalizePeak(buf, 0.92f);
            return buf;
        }

        private static float[] BakeMonoPistol(int rate, int variation)
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

        private static float[] BakeMonoImpact(int rate, int variation)
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

        private static float[] BakeMonoDamage(int rate, int variation)
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

        private static float[] BakeMonoHeadshot(int rate, int variation)
        {
            int n = (int)(rate * 0.18f);
            var buf = new float[n];
            uint rng = MixSeed(0xCEA05001u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.012f, 1.00f, 2500f, 12000f);
            AddMetallicClick(buf, rate, ref rng, 0.001f, 0.045f, 3200f + variation * 60f, 0.70f);
            AddThump(buf, rate, 0.000f, 0.050f, 85f + variation * 4f, 0.35f);
            AddNoiseBurst(buf, rate, ref rng, 0.008f, 0.120f, 0.28f, 600f, 4500f);
            NormalizePeak(buf, 0.90f);
            return buf;
        }

        private static float[] BakeMonoFootstep(int rate, int variation)
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

        private static float[] BakeMonoKnifeSwing(int rate, int variation)
        {
            int n = (int)(rate * 0.22f);
            var buf = new float[n];
            uint rng = MixSeed(0xA1FE0001u ^ 0x4B4E4946u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.090f, 0.55f, 1200f, 9000f);
            AddMetallicClick(buf, rate, ref rng, 0.010f, 0.080f, 2400f + variation * 80f, 0.55f);
            AddNoiseBurst(buf, rate, ref rng, 0.040f, 0.140f, 0.28f, 400f, 3500f);
            NormalizePeak(buf, 0.78f);
            return buf;
        }

        private static float[] BakeMonoGrenadeThrow(int rate, int variation)
        {
            int n = (int)(rate * 0.28f);
            var buf = new float[n];
            uint rng = MixSeed(0x7A000001u ^ 0x54485257u, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.120f, 0.45f, 200f, 2800f);
            AddMetallicClick(buf, rate, ref rng, 0.020f, 0.060f, 1200f + variation * 40f, 0.35f);
            AddThump(buf, rate, 0.040f, 0.080f, 70f, 0.22f);
            NormalizePeak(buf, 0.65f);
            return buf;
        }

        private static float[] BakeMonoGrenadeExplode(int rate, int variation)
        {
            int n = (int)(rate * 0.85f);
            var buf = new float[n];
            uint rng = MixSeed(0xB0010001u ^ 0x424F4F4Du, variation);
            AddNoiseBurst(buf, rate, ref rng, 0.000f, 0.040f, 1.00f, 400f, 10000f);
            AddThump(buf, rate, 0.000f, 0.220f, 38f + variation * 3f, 0.85f);
            AddNoiseBurst(buf, rate, ref rng, 0.020f, 0.350f, 0.70f, 80f, 2200f);
            AddNoiseBurst(buf, rate, ref rng, 0.080f, 0.650f, 0.35f, 40f, 900f);
            NormalizePeak(buf, 0.95f);
            return buf;
        }

        private static float[] BakeMonoReloadStart(int rate, int variation)
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

        private static float[] BakeMonoReloadComplete(int rate, int variation)
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

        private static float[] BakeMonoDeath(int rate, int variation)
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

        private static float[] BakeMonoUi(int rate, int variation)
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

        private static float[] BakeMonoAmbient(int rate, int variation)
        {
            int n = rate * 2;
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

        /// <summary>Haas-style stereo width from a mono emergency bake.</summary>
        private static float[] Stereoize(float[] mono, int rate, int variation, float delaySec)
        {
            int delay = Math.Max(1, (int)(delaySec * rate) + variation);
            var stereo = new float[mono.Length * 2];
            for (int i = 0; i < mono.Length; i++)
            {
                float l = mono[i];
                float r = i >= delay ? mono[i - delay] * 0.92f : mono[i] * 0.75f;
                stereo[i * 2] = l;
                stereo[i * 2 + 1] = r;
            }
            return stereo;
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

        private static string HashFileHex(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            return ToHex(hash);
        }

        private static string ComputeHash(int sampleRate, float[][][] clips, bool found)
        {
            using var sha = SHA256.Create();
            var preamble = Encoding.ASCII.GetBytes(
                CombatAudioBank.RecipeId + "|" + CombatAudioBank.BankVersion + "|" + sampleRate + "|ch" + CombatAudioBank.Channels + "|" + (found ? "found" : "gen") + "|");
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
            return ToHex(sha.Hash ?? Array.Empty<byte>());
        }

        private static string ToHex(byte[] hash)
        {
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
