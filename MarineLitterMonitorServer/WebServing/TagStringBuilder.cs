using System.Net;
using System.Text;

namespace MarineLitterMonitor.Server.WebServing;

public class TagStringBuilder(string indent = "    ")
{
    private readonly StringBuilder _builder = new();

    public char this[int i] => _builder[i];
    public int Length => _builder.Length;
    public int Capacity => _builder.Capacity;
    public int MaxCapacity => _builder.MaxCapacity;

    public override string ToString()
    {
        return _builder.ToString();
    }

    #region Indent & Tag

    public ushort IndentLevel { get; set; }
    private bool _indented = false;

    public TagStringBuilder IncreaseIndent(ushort level = 1)
    {
        IndentLevel += level;
        return this;
    }

    public TagStringBuilder DecreaseIndent(ushort level = 1)
    {
        IndentLevel -= level;
        return this;
    }

    public Indented Indent(ushort level = 1)
    {
        IncreaseIndent(level);
        return new Indented(this, level);
    }

    public Tagged Tag(string tagName, string? properties = null)
    {
        var tagHead = string.IsNullOrWhiteSpace(properties) ? $"<{tagName}>" : $"<{tagName} {properties}>";
        Append(tagHead);
        return new Tagged(this, tagName);
    }

    public IndentTagged IndentTag(string tagName, string? properties = null)
    {
        var tagHead = string.IsNullOrWhiteSpace(properties) ? $"<{tagName}>" : $"<{tagName} {properties}>";
        AppendLine(tagHead);
        IncreaseIndent();
        return new IndentTagged(this, tagName);
    }

    public readonly struct Indented(TagStringBuilder builder, ushort level) : IDisposable
    {
        public void Dispose() => builder.DecreaseIndent(level);
    }

    public readonly struct Tagged(TagStringBuilder builder, string tagName) : IDisposable
    {
        public void Dispose() => builder.Append($"</{tagName}>");
    }

    public readonly struct IndentTagged(TagStringBuilder builder, string tagName) : IDisposable
    {
        public void Dispose()
        {
            builder.DecreaseIndent();
            if (builder._indented) builder.AppendLine(); // 若最后一行未换行
            builder.AppendLine($"</{tagName}>");
        }
    }

    #endregion

    #region Append

    private bool TryIndent()
    {
        if (_indented) return false;

        _builder.AppendJoin("", Enumerable.Repeat(indent, IndentLevel));
        _indented = true;
        return true;
    }

    public TagStringBuilder Append(string str)
    {
        TryIndent();
        _builder.Append(str);
        return this;
    }

    public TagStringBuilder AppendTag(string tagName) => Append($"<{tagName}/>");
    public TagStringBuilder AppendTag(string tagName, string properties) => Append($"<{tagName} {properties}/>");

    public TagStringBuilder AppendEncoded(string text) => Append(WebUtility.HtmlEncode(text));

    public TagStringBuilder AppendLine()
    {
        TryIndent();
        _builder.AppendLine();
        _indented = false;
        return this;
    }

    public TagStringBuilder AppendLine(string str)
    {
        TryIndent();
        _builder.AppendLine(str);
        _indented = false;
        return this;
    }

    public TagStringBuilder AppendTagLine(string tagName) => AppendLine($"<{tagName}/>");
    public TagStringBuilder AppendTagLine(string tagName, string properties) => AppendLine($"<{tagName} {properties}/>");

    public TagStringBuilder AppendEncodedLine(string text) => AppendLine(WebUtility.HtmlEncode(text));

    #endregion
}