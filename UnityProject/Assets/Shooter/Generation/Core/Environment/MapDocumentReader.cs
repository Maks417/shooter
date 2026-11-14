#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Shooter.Generation.Environment
{
    /// <summary>Reader for map metadata/package sidecars that may contain finite float32 numbers.</summary>
    public sealed class MapJsonValue
    {
        private enum Kind { Null, Object, Array, String, Number, Boolean }
        private readonly Kind kind;
        private readonly object? value;
        private MapJsonValue(Kind kind, object? value) { this.kind = kind; this.value = value; }
        public IReadOnlyDictionary<string, MapJsonValue> Object => kind == Kind.Object ? (IReadOnlyDictionary<string, MapJsonValue>)value! : throw Wrong();
        public IReadOnlyList<MapJsonValue> Array => kind == Kind.Array ? (IReadOnlyList<MapJsonValue>)value! : throw Wrong();
        public string String => kind == Kind.String ? (string)value! : throw Wrong();
        public double Number => kind == Kind.Number ? (double)value! : throw Wrong();
        public int Int32
        {
            get
            {
                double n = Number;
                if (n < int.MinValue || n > int.MaxValue || Math.Floor(n) != n) throw Wrong();
                return (int)n;
            }
        }
        public bool Boolean => kind == Kind.Boolean ? (bool)value! : throw Wrong();
        private static FormatException Wrong() => new FormatException("Map JSON value has the wrong type.");

        public static MapJsonValue Parse(ReadOnlySpan<byte> utf8)
        {
            string text = Encoding.UTF8.GetString(utf8);
            return new Parser(text).Read();
        }

        private sealed class Parser
        {
            private readonly string text;
            private int offset;
            public Parser(string text) { this.text = text; }
            public MapJsonValue Read()
            {
                Space();
                char c = text[offset];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return new MapJsonValue(Kind.String, ReadString());
                if (c == 't') { Expect("true"); return new MapJsonValue(Kind.Boolean, true); }
                if (c == 'f') { Expect("false"); return new MapJsonValue(Kind.Boolean, false); }
                if (c == 'n') { Expect("null"); return new MapJsonValue(Kind.Null, null); }
                return ReadNumber();
            }

            private MapJsonValue ReadObject()
            {
                offset++;
                var members = new Dictionary<string, MapJsonValue>(StringComparer.Ordinal);
                Space();
                if (text[offset] == '}') { offset++; return new MapJsonValue(Kind.Object, new ReadOnlyDictionary<string, MapJsonValue>(members)); }
                while (true)
                {
                    Space();
                    string key = ReadString();
                    Space();
                    if (text[offset++] != ':') throw new FormatException("Expected colon.");
                    members.Add(key, Read());
                    Space();
                    if (text[offset] == '}') { offset++; break; }
                    if (text[offset++] != ',') throw new FormatException("Expected comma.");
                }
                return new MapJsonValue(Kind.Object, new ReadOnlyDictionary<string, MapJsonValue>(members));
            }

            private MapJsonValue ReadArray()
            {
                offset++;
                var items = new List<MapJsonValue>();
                Space();
                if (text[offset] == ']') { offset++; return new MapJsonValue(Kind.Array, items); }
                while (true)
                {
                    items.Add(Read());
                    Space();
                    if (text[offset] == ']') { offset++; break; }
                    if (text[offset++] != ',') throw new FormatException("Expected comma.");
                }
                return new MapJsonValue(Kind.Array, items);
            }

            private string ReadString()
            {
                if (text[offset++] != '"') throw new FormatException("Expected string.");
                var sb = new StringBuilder();
                while (offset < text.Length)
                {
                    char c = text[offset++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (offset >= text.Length) throw new FormatException("Truncated escape.");
                        c = text[offset++];
                        if (c != '"' && c != '\\' && c != '/') throw new FormatException("Unsupported escape.");
                    }
                    if (c < 32 || c > 126) throw new FormatException("Non-printable ASCII.");
                    sb.Append(c);
                }
                throw new FormatException("Unterminated string.");
            }

            private MapJsonValue ReadNumber()
            {
                int start = offset;
                if (text[offset] == '-') offset++;
                if (offset >= text.Length || !char.IsDigit(text[offset])) throw new FormatException("Expected number.");
                if (text[offset] == '0') offset++;
                else while (offset < text.Length && char.IsDigit(text[offset])) offset++;
                if (offset < text.Length && text[offset] == '.')
                {
                    offset++;
                    if (offset >= text.Length || !char.IsDigit(text[offset])) throw new FormatException("Expected fraction.");
                    while (offset < text.Length && char.IsDigit(text[offset])) offset++;
                }
                if (offset < text.Length && (text[offset] == 'e' || text[offset] == 'E'))
                {
                    offset++;
                    if (offset < text.Length && (text[offset] == '+' || text[offset] == '-')) offset++;
                    if (offset >= text.Length || !char.IsDigit(text[offset])) throw new FormatException("Expected exponent.");
                    while (offset < text.Length && char.IsDigit(text[offset])) offset++;
                }
                string slice = text.Substring(start, offset - start);
                if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                    double.IsNaN(value) || double.IsInfinity(value))
                    throw new FormatException("Invalid number.");
                return new MapJsonValue(Kind.Number, value);
            }

            private void Expect(string literal)
            {
                foreach (char c in literal)
                {
                    if (offset >= text.Length || text[offset++] != c) throw new FormatException("Expected " + literal);
                }
            }

            private void Space()
            {
                while (offset < text.Length)
                {
                    char c = text[offset];
                    if (c != ' ' && c != '\n' && c != '\r' && c != '\t') break;
                    offset++;
                }
            }
        }
    }
}
