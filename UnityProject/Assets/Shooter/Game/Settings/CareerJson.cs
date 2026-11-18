#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// Bounded UTF-8 JSON codec for <c>shooter.career.v1</c> (ADR-108), with the Armory codec's rules: ≤64 KiB, newer
    /// schemas are reported (never rewritten), unknown keys are ignored, a missing counter reads as 0 and a negative,
    /// fractional or out-of-range counter makes the document corrupt.
    /// </summary>
    public static class CareerJson
    {
        private static readonly string[] Keys =
        {
            "careerPoints", "matches", "wins", "losses", "draws", "kills", "deaths", "assists", "headshots",
            "scoreTotal", "bestScore", "bestStreak", "activeTicks"
        };

        public static byte[] Encode(CareerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            long[] values = ValuesOf(profile);
            var sb = new StringBuilder(384);
            sb.Append("{\"schema\":").Append(CareerProfile.Schema.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"schemaId\":\"").Append(CareerProfile.SchemaId).Append('"');
            for (int i = 0; i < Keys.Length; i++)
                sb.Append(",\"").Append(Keys[i]).Append("\":").Append(values[i].ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static bool TryDecode(byte[] bytes, out CareerProfile? profile, out string diagnostic)
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
            if (schema > CareerProfile.Schema)
            {
                diagnostic = "newer-" + schema.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            if (schema < CareerProfile.Schema || !(root.TryGetValue("schemaId", out var id) && id is string schemaId && schemaId == CareerProfile.SchemaId))
            {
                diagnostic = "unsupported-schema";
                return false;
            }
            var values = new long[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                if (!root.TryGetValue(Keys[i], out var raw)) continue;
                if (!(raw is long value) || value < 0 || value > CareerProfile.MaxCounter)
                {
                    diagnostic = "bad-" + Keys[i];
                    return false;
                }
                values[i] = value;
            }
            profile = new CareerProfile(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7],
                values[8], values[9], values[10], values[11], values[12]);
            diagnostic = "ok";
            return true;
        }

        private static long[] ValuesOf(CareerProfile p) => new[]
        {
            p.CareerPoints, p.Matches, p.Wins, p.Losses, p.Draws, p.Kills, p.Deaths, p.Assists, p.Headshots,
            p.ScoreTotal, p.BestScore, p.BestStreak, p.ActiveTicks
        };

        /// <summary>Field names in document order (fixtures and schema tests).</summary>
        public static IReadOnlyList<string> CounterKeys => Keys;
    }
}
