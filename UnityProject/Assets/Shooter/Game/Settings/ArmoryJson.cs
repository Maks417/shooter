#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// Bounded UTF-8 JSON codec for <c>shooter.armory.v1</c> (ADR-107). Same rules as the settings codec: ≤64 KiB,
    /// newer schemas are reported (never rewritten), unknown top-level keys are ignored, malformed ids are corrupt.
    /// </summary>
    public static class ArmoryJson
    {
        public static byte[] Encode(ArmoryProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var sb = new StringBuilder(256);
            sb.Append("{\"schema\":").Append(ArmoryProfile.Schema.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"schemaId\":\"").Append(ArmoryProfile.SchemaId).Append('"');
            sb.Append(",\"primary\":\"").Append(profile.Primary).Append('"');
            sb.Append(",\"sidearm\":\"").Append(profile.Sidearm).Append('"');
            sb.Append(",\"sights\":{");
            bool first = true;
            foreach (var pair in profile.Sights)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(pair.Key).Append("\":\"").Append(pair.Value).Append('"');
            }
            sb.Append("}}");
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            if (bytes.Length > GameContractVersion.MaxSettingsFileBytes)
                throw new ArgumentException("Armory JSON exceeds 64 KiB.", nameof(profile));
            return bytes;
        }

        public static bool TryDecode(byte[] bytes, out ArmoryProfile? profile, out string diagnostic)
        {
            profile = null;
            if (bytes == null || bytes.Length == 0) { diagnostic = "empty"; return false; }
            if (bytes.Length > GameContractVersion.MaxSettingsFileBytes) { diagnostic = "oversized"; return false; }
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (ArgumentException) { diagnostic = "encoding"; return false; }
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            if (!UserSettingsJson.TryParseObject(text, out var root, out diagnostic)) return false;
            if (!root.TryGetValue("schema", out var schemaValue) || !(schemaValue is long schema))
            {
                diagnostic = "missing-schema";
                return false;
            }
            if (schema > ArmoryProfile.Schema)
            {
                diagnostic = "newer-" + schema.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            if (schema < ArmoryProfile.Schema || !(root.TryGetValue("schemaId", out var id) && id is string schemaId && schemaId == ArmoryProfile.SchemaId))
            {
                diagnostic = "unsupported-schema";
                return false;
            }
            if (!(root.TryGetValue("primary", out var p) && p is string primary && ArmoryProfile.IsId(primary))
                || !(root.TryGetValue("sidearm", out var s) && s is string sidearm && ArmoryProfile.IsId(sidearm)))
            {
                diagnostic = "bad-weapon-id";
                return false;
            }
            var sights = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetValue("sights", out var sightValue))
            {
                if (!(sightValue is Dictionary<string, object?> map) || map.Count > ArmoryProfile.MaxSights)
                {
                    diagnostic = "bad-sights";
                    return false;
                }
                foreach (var pair in map)
                {
                    if (!ArmoryProfile.IsId(pair.Key) || !(pair.Value is string sight) || !ArmoryProfile.IsId(sight))
                    {
                        diagnostic = "bad-sights";
                        return false;
                    }
                    sights[pair.Key] = sight;
                }
            }
            profile = new ArmoryProfile(primary, sidearm, sights);
            diagnostic = "ok";
            return true;
        }
    }
}
