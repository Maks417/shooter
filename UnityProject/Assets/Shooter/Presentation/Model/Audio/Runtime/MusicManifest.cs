#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// <c>Content/Audio/music/manifest.json</c> (ADR-096): one OGG per <see cref="MusicCue"/> slot with sha256,
    /// optional loop points and a source note. Music streams through the engine decoder, not the SFX mixer.
    /// </summary>
    public sealed class MusicManifest
    {
        public const string RelativeMusicFolder = "Content/Audio/music";
        public const string ManifestFileName = "manifest.json";

        public sealed class Track
        {
            public MusicCue Cue { get; }
            public string File { get; }
            public string Sha256 { get; }
            /// <summary>Loop region start in seconds (0 = start of file).</summary>
            public float LoopStartSeconds { get; }
            /// <summary>Loop region end in seconds (0 = end of file).</summary>
            public float LoopEndSeconds { get; }
            public string Source { get; }

            public Track(MusicCue cue, string file, string sha256, float loopStartSeconds, float loopEndSeconds, string source)
            {
                if (cue == MusicCue.None) throw new ArgumentException("cue required", nameof(cue));
                if (string.IsNullOrEmpty(file) || file.IndexOfAny(new[] { '/', '\\' }) >= 0 || file.Contains(".."))
                    throw new ArgumentException("plain file name required", nameof(file));
                if (string.IsNullOrEmpty(sha256) || sha256.Length != 64) throw new ArgumentException("sha256 required", nameof(sha256));
                if (!(loopStartSeconds >= 0f) || !(loopEndSeconds >= 0f) || (loopEndSeconds > 0f && loopEndSeconds <= loopStartSeconds))
                    throw new ArgumentException("invalid loop region");
                Cue = cue;
                File = file;
                Sha256 = sha256;
                LoopStartSeconds = loopStartSeconds;
                LoopEndSeconds = loopEndSeconds;
                Source = source ?? "";
            }
        }

        private readonly Dictionary<MusicCue, Track> tracks;

        public string BankId { get; }
        public IReadOnlyCollection<Track> Tracks => tracks.Values;

        public MusicManifest(string bankId, IEnumerable<Track> entries)
        {
            BankId = bankId ?? throw new ArgumentNullException(nameof(bankId));
            tracks = new Dictionary<MusicCue, Track>();
            foreach (var t in entries)
            {
                if (tracks.ContainsKey(t.Cue)) throw new InvalidDataException("duplicate music cue " + t.Cue);
                tracks.Add(t.Cue, t);
            }
        }

        public bool TryGet(MusicCue cue, out Track track) => tracks.TryGetValue(cue, out track!);

        public static string CueSlotName(MusicCue cue) => cue switch
        {
            MusicCue.MenuTheme => "menu_theme",
            MusicCue.MatchCalm => "match_calm",
            MusicCue.MatchTension => "match_tension",
            MusicCue.VictoryStinger => "victory_stinger",
            MusicCue.DefeatStinger => "defeat_stinger",
            MusicCue.DrawStinger => "draw_stinger",
            MusicCue.ResultsLoop => "results_loop",
            _ => ""
        };

        public static MusicCue ParseCue(string slot)
        {
            for (var c = MusicCue.MenuTheme; c <= MusicCue.ResultsLoop; c++)
                if (string.Equals(CueSlotName(c), slot, StringComparison.Ordinal)) return c;
            return MusicCue.None;
        }

        public static MusicManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("json required", nameof(json));
            string bankId = CombatAudioManifest.ReadString(json, "bankId") ?? "music-bank";
            int key = json.IndexOf("\"tracks\"", StringComparison.Ordinal);
            if (key < 0) throw new InvalidDataException("music manifest missing tracks");
            int arrStart = json.IndexOf('[', key);
            int arrEnd = json.LastIndexOf(']');
            if (arrStart < 0 || arrEnd <= arrStart) throw new InvalidDataException("music manifest tracks array");
            string arr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            var list = new List<Track>();
            int pos = 0;
            while (pos < arr.Length)
            {
                int objStart = arr.IndexOf('{', pos);
                if (objStart < 0) break;
                int objEnd = arr.IndexOf('}', objStart);
                if (objEnd < 0) break;
                string obj = arr.Substring(objStart, objEnd - objStart + 1);
                var cue = ParseCue(CombatAudioManifest.ReadString(obj, "cue") ?? "");
                // Unknown slots are ignored so a newer manifest still loads in an older build.
                if (cue != MusicCue.None)
                    list.Add(new Track(cue,
                        CombatAudioManifest.ReadString(obj, "file") ?? "",
                        CombatAudioManifest.ReadString(obj, "sha256") ?? "",
                        ReadFloat(obj, "loopStartSeconds") ?? 0f,
                        ReadFloat(obj, "loopEndSeconds") ?? 0f,
                        CombatAudioManifest.ReadString(obj, "source") ?? ""));
                pos = objEnd + 1;
            }
            return new MusicManifest(bankId, list);
        }

        /// <summary>Loads and verifies every listed file's hash. Tracks with a missing file or bad hash are dropped.</summary>
        public static MusicManifest? LoadVerified(string folder, ICollection<string>? problems = null)
        {
            string path = Path.Combine(folder, ManifestFileName);
            if (!System.IO.File.Exists(path)) return null;
            var parsed = Parse(System.IO.File.ReadAllText(path));
            var ok = new List<Track>();
            foreach (var t in parsed.Tracks)
            {
                string file = Path.Combine(folder, t.File);
                if (!System.IO.File.Exists(file)) { problems?.Add(t.File + ": missing"); continue; }
                if (!string.Equals(HashFileHex(file), t.Sha256, StringComparison.OrdinalIgnoreCase)) { problems?.Add(t.File + ": sha256 mismatch"); continue; }
                ok.Add(t);
            }
            return new MusicManifest(parsed.BankId, ok);
        }

        /// <summary>Music folder beside the combat bank (editor repo root, packaged player, or override).</summary>
        public static string? ResolveFolder(string? explicitRoot = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitRoot) && Directory.Exists(explicitRoot)) return Path.GetFullPath(explicitRoot);
            string? env = Environment.GetEnvironmentVariable("SHOOTER_MUSIC_ROOT");
            if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env)) return Path.GetFullPath(env);
            string? combat = CombatAudioSamplePaths.ResolveCombatSampleRoot();
            if (combat == null) return null;
            string sibling = Path.Combine(Path.GetDirectoryName(combat) ?? combat, "music");
            return Directory.Exists(sibling) ? sibling : null;
        }

        private static float? ReadFloat(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return null;
            int colon = json.IndexOf(':', i + needle.Length);
            if (colon < 0) return null;
            int p = colon + 1;
            while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
            int end = p;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.' || json[end] == 'e' || json[end] == 'E' || json[end] == '+')) end++;
            if (end == p) return null;
            return float.TryParse(json.Substring(p, end - p), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : (float?)null;
        }

        private static string HashFileHex(string path)
        {
            using var sha = SHA256.Create();
            using var stream = System.IO.File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
