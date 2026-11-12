#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Generation
{
    /// <summary>
    /// Bounded canonical JSON v1 subset encoder, not a recipe/schema validator.
    /// Accepts ASCII strings/keys, signed int32 numbers, booleans, null and containers.
    /// Callers must validate recipe schemas and unit/range rules before hashing output.
    /// </summary>
    public static class CanonicalJson
    {
        public const int MaxBytes = 1024 * 1024;
        public const int MaxDepth = 64;
        public const int MaxNodes = 65536;

        public static byte[] Encode(ReadOnlySpan<byte> utf8)
        {
            if (utf8.Length > MaxBytes) throw new ArgumentException("Canonical JSON input exceeds 1 MiB.", nameof(utf8));
            string source;
            try { source = new UTF8Encoding(false, true).GetString(utf8); }
            catch (DecoderFallbackException error) { throw new FormatException("Invalid UTF-8 JSON.", error); }
            return Encoding.UTF8.GetBytes(new Parser(source).Encode());
        }

        private sealed class Parser
        {
            private readonly string source;
            private int position;
            private int nodes;
            public Parser(string source) { this.source = source; }

            public string Encode()
            {
                string result = Value(0);
                Space();
                if (position != source.Length) throw Invalid("Trailing JSON data");
                return result;
            }

            private string Value(int depth)
            {
                if (depth > MaxDepth) throw Invalid("JSON nesting limit exceeded");
                if (++nodes > MaxNodes) throw Invalid("JSON node limit exceeded");
                Space();
                if (position == source.Length) throw Invalid("Expected JSON value");
                char next = source[position];
                if (next == '{') return Object(depth);
                if (next == '[') return Array(depth);
                if (next == '"') return Quote(String());
                if (next == 't') return Literal("true");
                if (next == 'f') return Literal("false");
                if (next == 'n') return Literal("null");
                if (next == '-' || Digit(next)) return Integer();
                throw Invalid("Unexpected JSON token");
            }

            private string Object(int depth)
            {
                position++;
                var members = new SortedDictionary<string, string>(StringComparer.Ordinal);
                Space();
                if (Take('}')) return "{}";
                while (true)
                {
                    Space();
                    if (position == source.Length || source[position] != '"') throw Invalid("Expected object key");
                    string key = String();
                    // Check decoded keys before insertion; escaped aliases must also reject.
                    if (members.ContainsKey(key)) throw Invalid("Duplicate object key");
                    Space();
                    Expect(':');
                    members.Add(key, Value(depth + 1));
                    Space();
                    if (Take('}')) break;
                    Expect(',');
                }
                var result = new StringBuilder("{");
                foreach (var member in members)
                {
                    if (result.Length > 1) result.Append(',');
                    result.Append(Quote(member.Key)).Append(':').Append(member.Value);
                    CheckSize(result);
                }
                result.Append('}');
                CheckSize(result);
                return result.ToString();
            }

            private string Array(int depth)
            {
                position++;
                var result = new StringBuilder("[");
                Space();
                if (Take(']')) return "[]";
                while (true)
                {
                    if (result.Length > 1) result.Append(',');
                    result.Append(Value(depth + 1));
                    CheckSize(result);
                    Space();
                    if (Take(']')) break;
                    Expect(',');
                }
                result.Append(']');
                CheckSize(result);
                return result.ToString();
            }

            private string String()
            {
                Expect('"');
                var result = new StringBuilder();
                while (position < source.Length)
                {
                    char c = source[position++];
                    if (c == '"') return result.ToString();
                    if (c < 32) throw Invalid("Unescaped control character");
                    if (c == '\\')
                    {
                        if (position == source.Length) throw Invalid("Truncated string escape");
                        c = source[position++];
                        switch (c)
                        {
                            case '"': case '\\': case '/': break;
                            case 'b': c = '\b'; break;
                            case 'f': c = '\f'; break;
                            case 'n': c = '\n'; break;
                            case 'r': c = '\r'; break;
                            case 't': c = '\t'; break;
                            case 'u':
                                int code = 0;
                                for (int i = 0; i < 4; i++)
                                {
                                    if (position == source.Length) throw Invalid("Truncated Unicode escape");
                                    char hex = source[position++];
                                    int digit = hex >= '0' && hex <= '9' ? hex - '0' : hex >= 'a' && hex <= 'f' ? hex - 'a' + 10 : hex >= 'A' && hex <= 'F' ? hex - 'A' + 10 : -1;
                                    if (digit < 0) throw Invalid("Invalid Unicode escape");
                                    code = code * 16 + digit;
                                }
                                c = (char)code;
                                break;
                            default: throw Invalid("Unknown string escape");
                        }
                    }
                    if (c > 127) throw Invalid("Non-ASCII string outside canonical v1 subset");
                    result.Append(c);
                }
                throw Invalid("Unterminated string");
            }

            private string Integer()
            {
                int start = position;
                Take('-');
                if (position == source.Length || !Digit(source[position])) throw Invalid("Expected integer");
                if (Take('0'))
                {
                    if (position < source.Length && Digit(source[position])) throw Invalid("Leading integer zero");
                }
                else
                {
                    while (position < source.Length && Digit(source[position])) position++;
                }
                if (position < source.Length && (source[position] == '.' || source[position] == 'e' || source[position] == 'E'))
                    throw Invalid("Only int32 JSON numbers are allowed");
                if (!int.TryParse(source.Substring(start, position - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                    throw Invalid("Integer outside int32 range");
                return value.ToString(CultureInfo.InvariantCulture);
            }

            private string Literal(string literal)
            {
                foreach (char c in literal) Expect(c);
                return literal;
            }

            private static string Quote(string text)
            {
                var result = new StringBuilder("\"");
                foreach (char c in text)
                {
                    switch (c)
                    {
                        case '"': result.Append("\\\""); break;
                        case '\\': result.Append("\\\\"); break;
                        case '\b': result.Append("\\b"); break;
                        case '\f': result.Append("\\f"); break;
                        case '\n': result.Append("\\n"); break;
                        case '\r': result.Append("\\r"); break;
                        case '\t': result.Append("\\t"); break;
                        default:
                            if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else result.Append(c);
                            break;
                    }
                }
                result.Append('"');
                CheckSize(result);
                return result.ToString();
            }

            private void Space()
            {
                while (position < source.Length && (source[position] == ' ' || source[position] == '\t' || source[position] == '\r' || source[position] == '\n')) position++;
            }
            private static bool Digit(char c) => c >= '0' && c <= '9';
            private bool Take(char c)
            {
                if (position == source.Length || source[position] != c) return false;
                position++;
                return true;
            }
            private void Expect(char c) { if (!Take(c)) throw Invalid("Expected '" + c + "'"); }
            private FormatException Invalid(string reason) => new FormatException(reason + " at character " + position.ToString(CultureInfo.InvariantCulture) + ".");
            private static void CheckSize(StringBuilder value)
            {
                if (value.Length > MaxBytes) throw new FormatException("Canonical JSON output exceeds 1 MiB.");
            }
        }
    }
}
