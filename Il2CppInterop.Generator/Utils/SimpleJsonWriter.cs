using static Il2CppInterop.Generator.Utils.SimpleJsonWriter;

namespace Il2CppInterop.Generator.Utils;

internal class SimpleJsonWriter : JsonValue, JsonObject, JsonArray
{
    public interface JsonValue
    {
        public JsonArray Array();
        public JsonObject Object();
        public void Value(string value);
    }

    public interface JsonObject : IDisposable
    {
        public JsonValue Property(string name);

    }

    public interface JsonArray : JsonValue, IDisposable { }

    public static JsonValue Create(StreamWriter writer) => Create(writer, 0);

    public static JsonValue Create(StreamWriter writer, int startIndentation) =>
        new SimpleJsonWriter(writer, startIndentation);

    private readonly StreamWriter _writer;
    private int _indent;
    private bool _newline = true, _comma = false;
    private readonly Stack<string> _closeStack = new();

    private SimpleJsonWriter(StreamWriter writer, int startIndentation)
    {
        _writer = writer;
        _indent = startIndentation;
    }

    public JsonArray Array()
    {
        Open("[", "]");
        return this;
    }

    public JsonObject Object()
    {
        Open("{", "}");
        return this;
    }

    public JsonValue Property(string name)
    {
        Comma();
        Indent();
        name = Encode(name, true);
        _writer.Write(name);
        _writer.Write(": ");
        return this;
    }

    public void Value(string value)
    {
        if (value == null)
            _writer.Write("null");
        else
        {
            value = Encode(value, true);
            _writer.Write(value);
        }
        _comma = true;
    }

    private string Encode(string value, bool addDoubleQuotes = false)
    {
#if NETSTANDARD
        return System.Web.HttpUtility.JavaScriptStringEncode(value, addDoubleQuotes);
#else
        // Stolen from https://github.com/mono/mono/blob/89f1d3cc22fd3b0848ecedbd6215b0bdfeea9477/mcs/class/System.Web/System.Web/HttpUtility.cs#L528
        if (string.IsNullOrEmpty(value))
            return addDoubleQuotes ? "\"\"" : string.Empty;

        var len = value.Length;
        var needEncode = false;
        char c;
        for (var i = 0; i < len; i++)
        {
            c = value[i];

            if (c >= 0 && c <= 31 || c == 34 || c == 39 || c == 60 || c == 62 || c == 92)
            {
                needEncode = true;
                break;
            }
        }

        if (!needEncode)
            return addDoubleQuotes ? "\"" + value + "\"" : value;

        var sb = new System.Text.StringBuilder();
        if (addDoubleQuotes)
            sb.Append('"');

        for (var i = 0; i < len; i++)
        {
            c = value[i];
            if (c >= 0 && c <= 7 || c == 11 || c >= 14 && c <= 31 || c == 39 || c == 60 || c == 62)
                sb.AppendFormat("\\u{0:x4}", (int)c);
            else switch ((int)c)
                {
                    case 8:
                        sb.Append("\\b");
                        break;

                    case 9:
                        sb.Append("\\t");
                        break;

                    case 10:
                        sb.Append("\\n");
                        break;

                    case 12:
                        sb.Append("\\f");
                        break;

                    case 13:
                        sb.Append("\\r");
                        break;

                    case 34:
                        sb.Append("\\\"");
                        break;

                    case 92:
                        sb.Append("\\\\");
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
        }

        if (addDoubleQuotes)
            sb.Append('"');

        return sb.ToString();
#endif
    }

    private void Comma()
    {
        if (!_comma) return;
        _comma = false;
        _writer.WriteLine(',');
        _newline = true;
    }

    private void Indent()
    {
        if (!_newline) return;
        _writer.Write(new string(' ', _indent * 2));
        _newline = false;
    }

    private void Open(string open, string close)
    {
        Comma();
        Indent();
        _writer.WriteLine(open);
        _newline = true;
        _indent++;
        _closeStack.Push(close);
    }

    public void Dispose()
    {
        var close = _closeStack.Pop();
        _indent--;
        Indent();
        _writer.Write(close);
        _comma = true;
    }
}
