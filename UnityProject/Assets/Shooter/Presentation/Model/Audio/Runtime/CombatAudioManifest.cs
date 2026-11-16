#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Provenance manifest for found/authored stereo combat WAV clips (ADR-055 / T089). Hand-parsed JSON.
    /// </summary>
    public sealed class CombatAudioManifest
    {
        public string BankId { get; }
        public int SampleRate { get; }
        public int Channels { get; }
        public IReadOnlyList<ClipEntry> Clips { get; }

        public CombatAudioManifest(string bankId, int sampleRate, int channels, IReadOnlyList<ClipEntry> clips)
        {
            BankId = bankId ?? throw new ArgumentNullException(nameof(bankId));
            SampleRate = sampleRate;
            Channels = channels;
            Clips = clips ?? throw new ArgumentNullException(nameof(clips));
        }

        public readonly struct ClipEntry
        {
            public ClipEntry(string kind, int variation, string file, string sha256, string source, string license)
            {
                Kind = kind;
                Variation = variation;
                File = file;
                Sha256 = sha256;
                Source = source;
                License = license;
            }

            public string Kind { get; }
            public int Variation { get; }
            public string File { get; }
            public string Sha256 { get; }
            public string Source { get; }
            public string License { get; }
        }

        public static CombatAudioManifest ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path required", nameof(path));
            return Parse(File.ReadAllText(path));
        }

        public static CombatAudioManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("json required", nameof(json));
            string bankId = ReadString(json, "bankId") ?? CombatAudioBank.RecipeId;
            int sampleRate = ReadInt(json, "sampleRate") ?? CombatAudioBank.DefaultSampleRate;
            int channels = ReadInt(json, "channels") ?? CombatAudioBank.Channels;

            var clips = new List<ClipEntry>();
            int clipsKey = json.IndexOf("\"clips\"", StringComparison.Ordinal);
            if (clipsKey < 0) throw new InvalidDataException("manifest missing clips");
            int arrStart = json.IndexOf('[', clipsKey);
            int arrEnd = json.LastIndexOf(']');
            if (arrStart < 0 || arrEnd <= arrStart) throw new InvalidDataException("manifest clips array");
            string arr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            int pos = 0;
            while (pos < arr.Length)
            {
                int objStart = arr.IndexOf('{', pos);
                if (objStart < 0) break;
                int objEnd = arr.IndexOf('}', objStart);
                if (objEnd < 0) break;
                string obj = arr.Substring(objStart, objEnd - objStart + 1);
                string? kind = ReadString(obj, "kind");
                string? file = ReadString(obj, "file");
                string? sha = ReadString(obj, "sha256");
                string? source = ReadString(obj, "source") ?? "";
                string? license = ReadString(obj, "license") ?? "";
                int variation = ReadInt(obj, "variation") ?? 0;
                if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(file) || string.IsNullOrEmpty(sha))
                    throw new InvalidDataException("clip entry missing kind/file/sha256");
                clips.Add(new ClipEntry(kind, variation, file, sha, source, license));
                pos = objEnd + 1;
            }
            if (clips.Count == 0) throw new InvalidDataException("manifest has no clips");
            return new CombatAudioManifest(bankId, sampleRate, channels, clips);
        }

        private static string? ReadString(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return null;
            int colon = json.IndexOf(':', i + needle.Length);
            if (colon < 0) return null;
            int q0 = json.IndexOf('"', colon + 1);
            if (q0 < 0) return null;
            var sb = new StringBuilder();
            for (int p = q0 + 1; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '\\' && p + 1 < json.Length)
                {
                    sb.Append(json[p + 1]);
                    p++;
                    continue;
                }
                if (c == '"') return sb.ToString();
                sb.Append(c);
            }
            return null;
        }

        private static int? ReadInt(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return null;
            int colon = json.IndexOf(':', i + needle.Length);
            if (colon < 0) return null;
            int p = colon + 1;
            while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
            int end = p;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            if (end == p) return null;
            if (int.TryParse(json.Substring(p, end - p), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
            return null;
        }
    }
}
