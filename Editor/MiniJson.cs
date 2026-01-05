using System.Collections.Generic;
using System.IO;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    /// Minimal JSON serializer/deserializer for manifest.json manipulation
    /// </summary>
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            return json == null ? null : Parser.Parse(json);
        }
        
        public static string Serialize(object obj, bool prettyPrint = false)
        {
            return Serializer.Serialize(obj, prettyPrint);
        }
        
        private sealed class Parser
        {
            private const string WHITE_SPACE = " \t\n\r";
            private const string WORD_BREAK = " \t\n\r{}[],:\"";
            
            private StringReader json;
            
            private Parser(string jsonString)
            {
                json = new StringReader(jsonString);
            }
            
            public static object Parse(string jsonString)
            {
                var instance = new Parser(jsonString);
                return instance.ParseValue();
            }
            
            private void Dispose()
            {
                json.Dispose();
                json = null;
            }
            
            private object ParseValue()
            {
                SkipWhitespace();
                char nextChar = PeekChar();
                
                switch (nextChar)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case '-':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        return ParseNumber();
                    default:
                        string word = ParseWord();
                        if (word == "true") return true;
                        if (word == "false") return false;
                        if (word == "null") return null;
                        return word;
                }
            }
            
            private Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                json.Read(); // {
                
                while (true)
                {
                    SkipWhitespace();
                    char nextChar = PeekChar();
                    
                    if (nextChar == '}')
                    {
                        json.Read();
                        return table;
                    }
                    
                    if (nextChar == ',')
                    {
                        json.Read();
                        continue;
                    }
                    
                    string key = ParseString();
                    SkipWhitespace();
                    json.Read(); // :
                    table[key] = ParseValue();
                }
            }
            
            private List<object> ParseArray()
            {
                var array = new List<object>();
                json.Read(); // [
                
                while (true)
                {
                    SkipWhitespace();
                    char nextChar = PeekChar();
                    
                    if (nextChar == ']')
                    {
                        json.Read();
                        return array;
                    }
                    
                    if (nextChar == ',')
                    {
                        json.Read();
                        continue;
                    }
                    
                    array.Add(ParseValue());
                }
            }
            
            private string ParseString()
            {
                json.Read(); // "
                var sb = new System.Text.StringBuilder();
                
                while (true)
                {
                    if (json.Peek() == -1) break;
                    
                    char c = (char)json.Read();
                    
                    if (c == '"') break;
                    
                    if (c == '\\')
                    {
                        if (json.Peek() == -1) break;
                        c = (char)json.Read();
                        
                        switch (c)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                sb.Append(c);
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                
                return sb.ToString();
            }
            
            private object ParseNumber()
            {
                string number = ParseWord();
                
                if (number.Contains("."))
                {
                    double parsedDouble;
                    double.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsedDouble);
                    return parsedDouble;
                }
                
                long parsedLong;
                long.TryParse(number, out parsedLong);
                return parsedLong;
            }
            
            private string ParseWord()
            {
                var sb = new System.Text.StringBuilder();
                
                while (!IsWordBreak(PeekChar()))
                {
                    sb.Append((char)json.Read());
                    if (json.Peek() == -1) break;
                }
                
                return sb.ToString();
            }
            
            private void SkipWhitespace()
            {
                while (WHITE_SPACE.IndexOf(PeekChar()) != -1)
                {
                    json.Read();
                    if (json.Peek() == -1) break;
                }
            }
            
            private char PeekChar()
            {
                return (char)json.Peek();
            }
            
            private bool IsWordBreak(char c)
            {
                return WHITE_SPACE.IndexOf(c) != -1 || WORD_BREAK.IndexOf(c) != -1;
            }
        }
        
        private sealed class Serializer
        {
            private System.Text.StringBuilder builder;
            private bool prettyPrint;
            private int indentLevel;
            
            private Serializer(bool prettyPrint)
            {
                builder = new System.Text.StringBuilder();
                this.prettyPrint = prettyPrint;
                indentLevel = 0;
            }
            
            public static string Serialize(object obj, bool prettyPrint)
            {
                var instance = new Serializer(prettyPrint);
                instance.SerializeValue(obj);
                return instance.builder.ToString();
            }
            
            private void SerializeValue(object value)
            {
                if (value == null)
                {
                    builder.Append("null");
                }
                else if (value is string)
                {
                    SerializeString((string)value);
                }
                else if (value is bool)
                {
                    builder.Append(((bool)value) ? "true" : "false");
                }
                else if (value is Dictionary<string, object>)
                {
                    SerializeObject((Dictionary<string, object>)value);
                }
                else if (value is List<object>)
                {
                    SerializeArray((List<object>)value);
                }
                else if (value is long || value is int)
                {
                    builder.Append(value);
                }
                else if (value is double || value is float)
                {
                    builder.Append(((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    SerializeString(value.ToString());
                }
            }
            
            private void SerializeObject(Dictionary<string, object> obj)
            {
                builder.Append('{');
                
                if (prettyPrint)
                {
                    builder.Append('\n');
                    indentLevel++;
                }
                
                bool first = true;
                foreach (var kvp in obj)
                {
                    if (!first)
                    {
                        builder.Append(',');
                        if (prettyPrint) builder.Append('\n');
                    }
                    
                    if (prettyPrint) Indent();
                    
                    SerializeString(kvp.Key);
                    builder.Append(':');
                    if (prettyPrint) builder.Append(' ');
                    SerializeValue(kvp.Value);
                    
                    first = false;
                }
                
                if (prettyPrint)
                {
                    builder.Append('\n');
                    indentLevel--;
                    Indent();
                }
                
                builder.Append('}');
            }
            
            private void SerializeArray(List<object> array)
            {
                builder.Append('[');
                
                bool first = true;
                foreach (var item in array)
                {
                    if (!first) builder.Append(',');
                    if (prettyPrint) builder.Append(' ');
                    
                    SerializeValue(item);
                    first = false;
                }
                
                if (prettyPrint) builder.Append(' ');
                builder.Append(']');
            }
            
            private void SerializeString(string str)
            {
                builder.Append('"');
                
                foreach (char c in str)
                {
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                }
                
                builder.Append('"');
            }
            
            private void Indent()
            {
                for (int i = 0; i < indentLevel; i++)
                {
                    builder.Append("  ");
                }
            }
        }
    }
}
