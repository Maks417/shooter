#nullable enable
using System;
using System.IO;
using System.Text;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Minimal BCL WAV reader for PCM16 / IEEE float mono or stereo (T089).
    /// </summary>
    public static class WavPcmReader
    {
        public readonly struct WavClip
        {
            public WavClip(int sampleRate, int channels, float[] interleaved)
            {
                SampleRate = sampleRate;
                Channels = channels;
                Interleaved = interleaved;
            }

            public int SampleRate { get; }
            public int Channels { get; }
            public float[] Interleaved { get; }
            public int FrameCount => Channels <= 0 ? 0 : Interleaved.Length / Channels;
        }

        public static WavClip ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path required", nameof(path));
            return Read(File.ReadAllBytes(path));
        }

        public static WavClip Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44) throw new InvalidDataException("WAV too small");
            if (Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF") throw new InvalidDataException("missing RIFF");
            if (Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE") throw new InvalidDataException("missing WAVE");

            int offset = 12;
            int audioFormat = -1;
            int channels = -1;
            int sampleRate = -1;
            int bitsPerSample = -1;
            int dataOffset = -1;
            int dataSize = -1;

            while (offset + 8 <= bytes.Length)
            {
                string id = Encoding.ASCII.GetString(bytes, offset, 4);
                int size = BitConverter.ToInt32(bytes, offset + 4);
                int payload = offset + 8;
                if (size < 0 || payload + size > bytes.Length) throw new InvalidDataException("corrupt chunk");

                if (id == "fmt ")
                {
                    if (size < 16) throw new InvalidDataException("fmt too small");
                    audioFormat = BitConverter.ToInt16(bytes, payload);
                    channels = BitConverter.ToInt16(bytes, payload + 2);
                    sampleRate = BitConverter.ToInt32(bytes, payload + 4);
                    bitsPerSample = BitConverter.ToInt16(bytes, payload + 14);
                }
                else if (id == "data")
                {
                    dataOffset = payload;
                    dataSize = size;
                    break;
                }

                offset = payload + size + (size & 1);
            }

            if (audioFormat != 1 && audioFormat != 3) throw new InvalidDataException("unsupported WAV format " + audioFormat);
            if (channels < 1 || channels > 2) throw new InvalidDataException("unsupported channel count " + channels);
            if (sampleRate < 8000) throw new InvalidDataException("sample rate too low");
            if (dataOffset < 0 || dataSize <= 0) throw new InvalidDataException("missing data chunk");

            float[] interleaved;
            if (audioFormat == 1 && bitsPerSample == 16)
            {
                int frames = dataSize / (2 * channels);
                interleaved = new float[frames * channels];
                int p = dataOffset;
                for (int i = 0; i < interleaved.Length; i++)
                {
                    short s = BitConverter.ToInt16(bytes, p);
                    p += 2;
                    interleaved[i] = s / 32768f;
                }
            }
            else if (audioFormat == 3 && bitsPerSample == 32)
            {
                int frames = dataSize / (4 * channels);
                interleaved = new float[frames * channels];
                Buffer.BlockCopy(bytes, dataOffset, interleaved, 0, frames * channels * 4);
            }
            else throw new InvalidDataException("unsupported bits " + bitsPerSample);

            return new WavClip(sampleRate, channels, interleaved);
        }

        /// <summary>Convert to interleaved stereo float at the target rate (duplicate mono; linear resample if needed).</summary>
        public static float[] ToStereo48k(WavClip clip, int targetRate = CombatAudioBank.DefaultSampleRate)
        {
            if (clip.Interleaved == null || clip.Interleaved.Length == 0) return Array.Empty<float>();
            float[] stereo = clip.Channels == 2
                ? clip.Interleaved
                : MonoToStereo(clip.Interleaved);

            if (clip.SampleRate == targetRate) return (float[])stereo.Clone();
            return ResampleInterleaved(stereo, 2, clip.SampleRate, targetRate);
        }

        private static float[] MonoToStereo(float[] mono)
        {
            var stereo = new float[mono.Length * 2];
            for (int i = 0; i < mono.Length; i++)
            {
                stereo[i * 2] = mono[i];
                stereo[i * 2 + 1] = mono[i];
            }
            return stereo;
        }

        private static float[] ResampleInterleaved(float[] src, int channels, int srcRate, int dstRate)
        {
            int srcFrames = src.Length / channels;
            int dstFrames = (int)((long)srcFrames * dstRate / srcRate);
            if (dstFrames < 1) return Array.Empty<float>();
            var dst = new float[dstFrames * channels];
            for (int f = 0; f < dstFrames; f++)
            {
                double srcPos = f * (double)srcRate / dstRate;
                int i0 = (int)srcPos;
                int i1 = Math.Min(i0 + 1, srcFrames - 1);
                float t = (float)(srcPos - i0);
                for (int c = 0; c < channels; c++)
                {
                    float a = src[i0 * channels + c];
                    float b = src[i1 * channels + c];
                    dst[f * channels + c] = a + (b - a) * t;
                }
            }
            return dst;
        }
    }
}
