#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Deterministic UTF-8 JSON for map metadata/package sidecar documents.
    /// Extends the P02 CanonicalJson subset with finite float32 decimals required by ADR-035 metadata.
    /// Does not modify locked CanonicalJson.
    /// </summary>
    public static class MapDocumentJson
    {
        public const int MaxBytes = 1024 * 1024;

        public static byte[] EncodeObject(IReadOnlyDictionary<string, object?> members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in members) sorted[pair.Key] = pair.Value;
            var sb = new StringBuilder();
            WriteValue(sb, sorted, 0);
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            if (bytes.Length > MaxBytes) throw new ArgumentException("Map document exceeds 1 MiB.");
            return bytes;
        }

        private static void WriteValue(StringBuilder sb, object? value, int depth)
        {
            if (depth > 64) throw new ArgumentException("JSON nesting limit exceeded.");
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    if (l < int.MinValue || l > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value), "Integer outside int32.");
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    WriteFloat(sb, f);
                    break;
                case double d:
                    if (d < float.MinValue || d > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
                    WriteFloat(sb, (float)d);
                    break;
                case IReadOnlyDictionary<string, object?> obj:
                {
                    SortedDictionary<string, object?> sorted;
                    if (obj is SortedDictionary<string, object?> existing) sorted = existing;
                    else
                    {
                        sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                        foreach (var pair in obj) sorted[pair.Key] = pair.Value;
                    }
                    sb.Append('{');
                    bool first = true;
                    foreach (var pair in sorted)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, pair.Key);
                        sb.Append(':');
                        WriteValue(sb, pair.Value, depth + 1);
                    }
                    sb.Append('}');
                    break;
                }
                case IReadOnlyList<object?> list:
                {
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteValue(sb, list[i], depth + 1);
                    }
                    sb.Append(']');
                    break;
                }
                default:
                    throw new ArgumentException("Unsupported map document value type: " + value.GetType().FullName);
            }
        }

        private static void WriteFloat(StringBuilder sb, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), "Non-finite float.");
            // Round-trip float32; trim redundant exponent form while staying invariant.
            string text = value.ToString("G9", CultureInfo.InvariantCulture);
            if (text.IndexOf('.') < 0 && text.IndexOf('E') < 0 && text.IndexOf('e') < 0)
                text += ".0";
            sb.Append(text);
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            sb.Append('"');
            foreach (char c in value)
            {
                if (c < 32 || c > 126) throw new ArgumentException("Non-printable ASCII string.");
                if (c == '"' || c == '\\') sb.Append('\\');
                sb.Append(c);
            }
            sb.Append('"');
        }
    }
}
