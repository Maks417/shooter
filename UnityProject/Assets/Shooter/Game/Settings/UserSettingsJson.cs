#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>Bounded UTF-8 JSON codec for shooter.user-settings.v2 (ADR-036). No System.Text.Json dependency.</summary>
    public static class UserSettingsJson
    {
        public static byte[] Encode(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var sb = new StringBuilder(512);
            sb.Append('{');
            WriteNumber(sb, "schema", settings.Schema, first: true);
            WriteString(sb, "schemaId", UserSettings.SchemaId, first: false);
            WriteNumber(sb, "masterVolume", settings.MasterVolume, first: false);
            WriteNumber(sb, "effectsVolume", settings.EffectsVolume, first: false);
            WriteNumber(sb, "uiVolume", settings.UiVolume, first: false);
            WriteNumber(sb, "musicVolume", settings.MusicVolume, first: false);
            WriteNumber(sb, "qualityTier", settings.QualityTier, first: false);
            WriteNumber(sb, "resolutionWidth", settings.ResolutionWidth, first: false);
            WriteNumber(sb, "resolutionHeight", settings.ResolutionHeight, first: false);
            WriteNumber(sb, "displayMode", (int)settings.DisplayMode, first: false);
            WriteBool(sb, "vSync", settings.VSync, first: false);
            WriteNumber(sb, "fpsCap", settings.FpsCap, first: false);
            WriteNumber(sb, "hipFovDegrees", settings.HipFovDegrees, first: false);
            WriteNumber(sb, "mouseSensitivity", settings.MouseSensitivity, first: false);
            WriteBool(sb, "invertY", settings.InvertY, first: false);
            WriteNumber(sb, "uiScale", settings.UiScale, first: false);
            WriteBool(sb, "reducedCameraMotion", settings.ReducedCameraMotion, first: false);
            WriteBool(sb, "reducedFlash", settings.ReducedFlash, first: false);
            WriteNumber(sb, "contrast", settings.Contrast, first: false);
            WriteBool(sb, "subtitles", settings.Subtitles, first: false);
            sb.Append(",\"bindings\":{");
            for (int i = 0; i < settings.Bindings.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteRawString(sb, settings.Bindings[i].Action);
                sb.Append(':');
                WriteRawString(sb, settings.Bindings[i].Control);
            }
            sb.Append("}}");
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            if (bytes.Length > GameContractVersion.MaxSettingsFileBytes)
                throw new ArgumentException("Settings JSON exceeds 64 KiB.", nameof(settings));
            return bytes;
        }

        public static bool TryDecode(byte[] bytes, out UserSettings? settings, out string diagnostic)
        {
            settings = null;
            diagnostic = "ok";
            if (bytes == null || bytes.Length == 0)
            {
                diagnostic = "empty";
                return false;
            }
            if (bytes.Length > GameContractVersion.MaxSettingsFileBytes)
            {
                diagnostic = "oversized";
                return false;
            }
            string text;
            try { text = Encoding.UTF8.GetString(bytes); }
            catch
            {
                diagnostic = "encoding";
                return false;
            }
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);
            return TryDecodeText(text, out settings, out diagnostic);
        }

        public static bool TryDecodeText(string text, out UserSettings? settings, out string diagnostic)
        {
            settings = null;
            diagnostic = "ok";
            if (!string.IsNullOrEmpty(text) && text[0] == '\uFEFF')
                text = text.Substring(1);
            if (string.IsNullOrWhiteSpace(text))
            {
                diagnostic = "empty";
                return false;
            }
            if (Encoding.UTF8.GetByteCount(text) > GameContractVersion.MaxSettingsFileBytes)
            {
                diagnostic = "oversized";
                return false;
            }

            if (text.IndexOf("schema=", StringComparison.Ordinal) >= 0 && text[0] != '{')
                return TryDecodeLegacyV1(text, out settings, out diagnostic);

            if (!TryParseObject(text, out var root, out diagnostic))
                return false;
            if (!TryGetUInt(root, "schema", out uint schema))
            {
                diagnostic = "missing-schema";
                return false;
            }
            if (schema > GameContractVersion.SettingsSchema)
            {
                diagnostic = "newer-" + schema.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            if (schema < GameContractVersion.SettingsSchemaMinSupported)
            {
                diagnostic = "unsupported-schema";
                return false;
            }
            if (schema == 1)
                return TryMigrateObjectV1(root, out settings, out diagnostic);
            return TryBuildV2(root, out settings, out diagnostic);
        }

        private static bool TryDecodeLegacyV1(string text, out UserSettings? settings, out string diagnostic)
        {
            settings = null;
            diagnostic = "ok";
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in text.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    diagnostic = "corrupt";
                    return false;
                }
                map[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
            }
            if (!map.TryGetValue("schema", out var schemaText)
                || !uint.TryParse(schemaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint schema))
            {
                diagnostic = "corrupt";
                return false;
            }
            if (schema > GameContractVersion.SettingsSchema)
            {
                diagnostic = "newer-" + schema.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            if (schema != 1)
            {
                diagnostic = "corrupt";
                return false;
            }
            try
            {
                float master = ParseFloat(map, "master");
                float sfx = ParseFloat(map, "sfx");
                float music = ParseFloat(map, "music");
                byte quality = byte.Parse(map["quality"], CultureInfo.InvariantCulture);
                float mouse = ParseFloat(map, "mouse");
                bool invertY = map["invertY"] == "1";
                bool subtitles = map["subtitles"] == "1";
                float uiScale = ParseFloat(map, "uiScale");
                settings = UserSettingsMigration.FromSchema1(master, sfx, music, quality, mouse, invertY, subtitles, uiScale);
                return true;
            }
            catch
            {
                diagnostic = "corrupt";
                return false;
            }
        }

        private static bool TryMigrateObjectV1(Dictionary<string, object?> root, out UserSettings? settings, out string diagnostic)
        {
            settings = null;
            diagnostic = "ok";
            try
            {
                settings = UserSettingsMigration.FromSchema1(
                    RequireFloat(root, "masterVolume", "master"),
                    RequireFloat(root, "effectsVolume", "sfxVolume", "sfx"),
                    RequireFloat(root, "uiVolume", "musicVolume", "music"),
                    (byte)RequireInt(root, "qualityTier", "quality"),
                    RequireFloat(root, "mouseSensitivity", "mouse"),
                    RequireBool(root, "invertY"),
                    RequireBool(root, "subtitles"),
                    RequireFloat(root, "uiScale"));
                return true;
            }
            catch
            {
                diagnostic = "corrupt";
                return false;
            }
        }

        private static bool TryBuildV2(Dictionary<string, object?> root, out UserSettings? settings, out string diagnostic)
        {
            settings = null;
            diagnostic = "ok";
            try
            {
                if (root.TryGetValue("schemaId", out var sid) && sid is string schemaId
                    && !string.Equals(schemaId, UserSettings.SchemaId, StringComparison.Ordinal))
                {
                    diagnostic = "schema-id";
                    return false;
                }
                var bindings = new List<InputBinding>();
                if (!root.TryGetValue("bindings", out var rawBindings) || rawBindings is not Dictionary<string, object?> bindObj)
                {
                    diagnostic = "missing-bindings";
                    return false;
                }
                foreach (var pair in bindObj)
                {
                    if (pair.Value is not string control)
                    {
                        diagnostic = "binding-type";
                        return false;
                    }
                    bindings.Add(new InputBinding(pair.Key, control));
                }
                settings = new UserSettings(
                    GameContractVersion.SettingsSchema,
                    RequireFloat(root, "masterVolume"),
                    RequireFloat(root, "effectsVolume"),
                    RequireFloat(root, "uiVolume"),
                    (byte)RequireInt(root, "qualityTier"),
                    RequireInt(root, "resolutionWidth"),
                    RequireInt(root, "resolutionHeight"),
                    (DisplayMode)RequireInt(root, "displayMode"),
                    RequireBool(root, "vSync"),
                    RequireInt(root, "fpsCap"),
                    RequireFloat(root, "hipFovDegrees"),
                    RequireFloat(root, "mouseSensitivity"),
                    RequireBool(root, "invertY"),
                    RequireFloat(root, "uiScale"),
                    RequireBool(root, "reducedCameraMotion"),
                    RequireBool(root, "reducedFlash"),
                    RequireFloat(root, "contrast"),
                    RequireBool(root, "subtitles"),
                    bindings,
                    OptionalFloat(root, "musicVolume", UserSettings.DefaultMusicVolume));
                return true;
            }
            catch (ArgumentException ex)
            {
                diagnostic = "invalid:" + ex.ParamName;
                return false;
            }
            catch
            {
                diagnostic = "corrupt";
                return false;
            }
        }

        private static float ParseFloat(Dictionary<string, string> map, string key) =>
            float.Parse(map[key], CultureInfo.InvariantCulture);

        private static float RequireFloat(Dictionary<string, object?> root, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!root.TryGetValue(key, out var value) || value == null) continue;
                if (value is double d) return (float)d;
                if (value is long l) return l;
                if (value is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    return f;
            }
            throw new ArgumentException("missing float", keys[0]);
        }

        /// <summary>ADR-096: additive schema-2 key; files written before it load with the default.</summary>
        private static float OptionalFloat(Dictionary<string, object?> root, string key, float fallback)
        {
            if (!root.TryGetValue(key, out var value) || value == null) return fallback;
            return RequireFloat(root, key);
        }

        private static int RequireInt(Dictionary<string, object?> root, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!root.TryGetValue(key, out var value) || value == null) continue;
                if (value is long l) return checked((int)l);
                if (value is double d) return checked((int)d);
                if (value is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                    return i;
            }
            throw new ArgumentException("missing int", keys[0]);
        }

        private static bool RequireBool(Dictionary<string, object?> root, string key)
        {
            if (!root.TryGetValue(key, out var value) || value == null)
                throw new ArgumentException("missing bool", key);
            if (value is bool b) return b;
            if (value is long l) return l != 0;
            if (value is string s) return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            throw new ArgumentException("bad bool", key);
        }

        private static bool TryGetUInt(Dictionary<string, object?> root, string key, out uint value)
        {
            value = 0;
            if (!root.TryGetValue(key, out var raw) || raw == null) return false;
            if (raw is long l && l >= 0) { value = (uint)l; return true; }
            if (raw is double d && d >= 0) { value = (uint)d; return true; }
            if (raw is string s && uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;
            return false;
        }

        private static void WriteNumber(StringBuilder sb, string key, float value, bool first)
        {
            if (!first) sb.Append(',');
            WriteRawString(sb, key);
            sb.Append(':');
            sb.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
        }

        private static void WriteNumber(StringBuilder sb, string key, long value, bool first)
        {
            if (!first) sb.Append(',');
            WriteRawString(sb, key);
            sb.Append(':');
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteBool(StringBuilder sb, string key, bool value, bool first)
        {
            if (!first) sb.Append(',');
            WriteRawString(sb, key);
            sb.Append(':');
            sb.Append(value ? "true" : "false");
        }

        private static void WriteString(StringBuilder sb, string key, string value, bool first)
        {
            if (!first) sb.Append(',');
            WriteRawString(sb, key);
            sb.Append(':');
            WriteRawString(sb, value);
        }

        private static void WriteRawString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') sb.Append('\\');
                if (c < 32) throw new ArgumentException("Control character in settings JSON.");
                sb.Append(c);
            }
            sb.Append('"');
        }

        internal static bool TryParseObject(string text, out Dictionary<string, object?> obj, out string diagnostic)
        {
            obj = new Dictionary<string, object?>(StringComparer.Ordinal);
            diagnostic = "ok";
            int i = 0;
            SkipWs(text, ref i);
            if (i >= text.Length || text[i] != '{')
            {
                diagnostic = "not-object";
                return false;
            }
            i++;
            SkipWs(text, ref i);
            if (i < text.Length && text[i] == '}')
                return true;
            while (i < text.Length)
            {
                if (!TryParseString(text, ref i, out string key, out diagnostic))
                    return false;
                SkipWs(text, ref i);
                if (i >= text.Length || text[i] != ':')
                {
                    diagnostic = "missing-colon";
                    return false;
                }
                i++;
                if (!TryParseValue(text, ref i, out object? value, out diagnostic))
                    return false;
                obj[key] = value;
                SkipWs(text, ref i);
                if (i >= text.Length)
                {
                    diagnostic = "truncated";
                    return false;
                }
                if (text[i] == '}')
                    return true;
                if (text[i] != ',')
                {
                    diagnostic = "expected-comma";
                    return false;
                }
                i++;
                SkipWs(text, ref i);
            }
            diagnostic = "truncated";
            return false;
        }

        private static bool TryParseValue(string text, ref int i, out object? value, out string diagnostic)
        {
            value = null;
            diagnostic = "ok";
            SkipWs(text, ref i);
            if (i >= text.Length)
            {
                diagnostic = "truncated";
                return false;
            }
            char c = text[i];
            if (c == '"')
            {
                if (!TryParseString(text, ref i, out string s, out diagnostic)) return false;
                value = s;
                return true;
            }
            if (c == '{')
            {
                int start = i;
                if (!TryParseObject(text.Substring(start), out var nested, out diagnostic))
                    return false;
                // advance i over the nested object by re-parsing length from start
                int depth = 0;
                bool inString = false;
                bool escape = false;
                for (int j = start; j < text.Length; j++)
                {
                    char ch = text[j];
                    if (inString)
                    {
                        if (escape) escape = false;
                        else if (ch == '\\') escape = true;
                        else if (ch == '"') inString = false;
                        continue;
                    }
                    if (ch == '"') { inString = true; continue; }
                    if (ch == '{') depth++;
                    else if (ch == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            i = j + 1;
                            value = nested;
                            return true;
                        }
                    }
                }
                diagnostic = "truncated-object";
                return false;
            }
            if (c == 't' && MatchLiteral(text, ref i, "true")) { value = true; return true; }
            if (c == 'f' && MatchLiteral(text, ref i, "false")) { value = false; return true; }
            if (c == 'n' && MatchLiteral(text, ref i, "null")) { value = null; return true; }
            int numStart = i;
            if (c == '-' || c == '+') i++;
            while (i < text.Length && ((text[i] >= '0' && text[i] <= '9') || text[i] == '.' || text[i] == 'e' || text[i] == 'E' || text[i] == '+' || text[i] == '-'))
                i++;
            string num = text.Substring(numStart, i - numStart);
            if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
            {
                value = l;
                return true;
            }
            if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                && !double.IsNaN(d) && !double.IsInfinity(d))
            {
                value = d;
                return true;
            }
            diagnostic = "bad-number";
            return false;
        }

        private static bool TryParseString(string text, ref int i, out string value, out string diagnostic)
        {
            value = "";
            diagnostic = "ok";
            SkipWs(text, ref i);
            if (i >= text.Length || text[i] != '"')
            {
                diagnostic = "not-string";
                return false;
            }
            i++;
            var sb = new StringBuilder();
            while (i < text.Length)
            {
                char c = text[i++];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                if (c == '\\')
                {
                    if (i >= text.Length)
                    {
                        diagnostic = "bad-escape";
                        return false;
                    }
                    sb.Append(text[i++]);
                    continue;
                }
                if (c < 32)
                {
                    diagnostic = "control-char";
                    return false;
                }
                sb.Append(c);
            }
            diagnostic = "truncated-string";
            return false;
        }

        private static bool MatchLiteral(string text, ref int i, string literal)
        {
            if (i + literal.Length > text.Length) return false;
            for (int j = 0; j < literal.Length; j++)
                if (text[i + j] != literal[j]) return false;
            i += literal.Length;
            return true;
        }

        private static void SkipWs(string text, ref int i)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        }
    }
}
