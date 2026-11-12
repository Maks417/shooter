#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Shooter.Generation
{
    /// <summary>Immutable bounded v1 JSON tree. Parsing first applies all CanonicalJson subset guards.</summary>
    public sealed class JsonValue
    {
        private enum ValueKind { Null, Object, Array, String, Number, Boolean }
        private readonly ValueKind kind;
        private readonly object? value;
        private JsonValue(ValueKind kind, object? value) { this.kind = kind; this.value = value; }
        public IReadOnlyDictionary<string, JsonValue> Object => kind == ValueKind.Object ? (IReadOnlyDictionary<string, JsonValue>)value! : throw WrongType();
        public IReadOnlyList<JsonValue> Array => kind == ValueKind.Array ? (IReadOnlyList<JsonValue>)value! : throw WrongType();
        public string String => kind == ValueKind.String ? (string)value! : throw WrongType();
        public int Number => kind == ValueKind.Number ? (int)value! : throw WrongType();
        public bool Boolean => kind == ValueKind.Boolean ? (bool)value! : throw WrongType();
        public bool IsNull => kind == ValueKind.Null;
        private static FormatException WrongType() => new FormatException("JSON value has the wrong type.");

        public static JsonValue Parse(ReadOnlySpan<byte> utf8)
        {
            byte[] canonical = CanonicalJson.Encode(utf8);
            return new TreeParser(Encoding.UTF8.GetString(canonical)).Read();
        }

        // This parser consumes only already-validated canonical text, never arbitrary source.
        private sealed class TreeParser
        {
            private readonly string text;
            private int offset;
            public TreeParser(string text) { this.text = text; }
            public JsonValue Read()
            {
                char token = text[offset];
                if (token == '{')
                {
                    offset++;
                    var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                    if (text[offset] != '}')
                    {
                        while (true)
                        {
                            string key = ReadString(); offset++; // Colon.
                            members.Add(key, Read());
                            if (text[offset] == '}') break;
                            offset++; // Comma.
                        }
                    }
                    offset++;
                    return new JsonValue(ValueKind.Object, new ReadOnlyDictionary<string, JsonValue>(members));
                }
                if (token == '[')
                {
                    offset++;
                    var elements = new List<JsonValue>();
                    if (text[offset] != ']')
                    {
                        while (true)
                        {
                            elements.Add(Read());
                            if (text[offset] == ']') break;
                            offset++;
                        }
                    }
                    offset++;
                    return new JsonValue(ValueKind.Array, elements.AsReadOnly());
                }
                if (token == '"') return new JsonValue(ValueKind.String, ReadString());
                if (token == 'n') { offset += 4; return new JsonValue(ValueKind.Null, null); }
                if (token == 't') { offset += 4; return new JsonValue(ValueKind.Boolean, true); }
                if (token == 'f') { offset += 5; return new JsonValue(ValueKind.Boolean, false); }
                int start = offset++;
                while (offset < text.Length && text[offset] >= '0' && text[offset] <= '9') offset++;
                int number = int.Parse(text.Substring(start, offset - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                return new JsonValue(ValueKind.Number, number);
            }

            private string ReadString()
            {
                offset++; // Quote.
                var value = new StringBuilder();
                while (text[offset] != '"')
                {
                    char c = text[offset++];
                    if (c == '\\')
                    {
                        c = text[offset++];
                        switch (c)
                        {
                            case 'b': c = '\b'; break;
                            case 'f': c = '\f'; break;
                            case 'n': c = '\n'; break;
                            case 'r': c = '\r'; break;
                            case 't': c = '\t'; break;
                            case 'u':
                                c = (char)int.Parse(text.Substring(offset, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                                offset += 4;
                                break;
                        }
                    }
                    value.Append(c);
                }
                offset++;
                return value.ToString();
            }
        }
    }
}
