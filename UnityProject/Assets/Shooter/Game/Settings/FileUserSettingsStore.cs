#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>M10 atomic settings file store. Schema=1; corrupt/newer fail without blocking defaults recovery.</summary>
    public sealed class FileUserSettingsStore : IUserSettingsStore
    {
        private readonly string path;
        public FileUserSettingsStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            this.path = path;
        }

        public GameOperationResult Load(out UserSettings settings)
        {
            settings = UserSettings.Defaults;
            if (!File.Exists(path)) return GameOperationResult.Ok;
            try
            {
                string text = File.ReadAllText(path, Encoding.ASCII);
                if (string.IsNullOrWhiteSpace(text))
                    return new GameOperationResult(GameErrorCode.StorageError, "corrupt-settings", "Settings were reset.");
                var parsed = Parse(text, out string? error);
                if (parsed == null)
                {
                    if (error != null && error.StartsWith("newer-", StringComparison.Ordinal))
                        return new GameOperationResult(GameErrorCode.UnsupportedVersion, error, "Settings version unsupported.");
                    return new GameOperationResult(GameErrorCode.StorageError, "corrupt-settings", "Settings were reset.");
                }
                settings = parsed;
                return GameOperationResult.Ok;
            }
            catch (IOException)
            {
                return new GameOperationResult(GameErrorCode.StorageError, "settings-io", "Settings were reset.");
            }
        }

        public GameOperationResult Save(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, Format(settings), Encoding.ASCII);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return GameOperationResult.Ok;
            }
            catch (IOException)
            {
                return new GameOperationResult(GameErrorCode.StorageError, "readonly-store", "Could not save settings.");
            }
        }

        private static string Format(UserSettings s) =>
            string.Format(CultureInfo.InvariantCulture,
                "schema={0};master={1};sfx={2};music={3};quality={4};mouse={5};invertY={6};subtitles={7};uiScale={8}",
                s.Schema, s.MasterVolume, s.SfxVolume, s.MusicVolume, s.QualityTier, s.MouseSensitivity, s.InvertY ? 1 : 0, s.Subtitles ? 1 : 0, s.UiScale);

        private static UserSettings? Parse(string text, out string? error)
        {
            error = null;
            var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in text.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) { error = "corrupt"; return null; }
                map[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
            }
            if (!map.TryGetValue("schema", out var schemaText) || !uint.TryParse(schemaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint schema))
            { error = "corrupt"; return null; }
            if (schema > 1) { error = "newer-" + schema; return null; }
            if (schema != 1) { error = "corrupt"; return null; }
            try
            {
                return new UserSettings(
                    schema,
                    F(map, "master"),
                    F(map, "sfx"),
                    F(map, "music"),
                    byte.Parse(map["quality"], CultureInfo.InvariantCulture),
                    F(map, "mouse"),
                    map["invertY"] == "1",
                    map["subtitles"] == "1",
                    F(map, "uiScale"));
            }
            catch
            {
                error = "corrupt";
                return null;
            }
        }

        private static float F(System.Collections.Generic.Dictionary<string, string> map, string key) =>
            float.Parse(map[key], CultureInfo.InvariantCulture);
    }
}
