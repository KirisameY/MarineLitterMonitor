using MarineLitterMonitor.Server.FileRecording;

namespace MarineLitterMonitor.Server.WebServing;

internal class IndexModule(string indexUri) : IWebModule
{
    public void Map(WebApplication app)
    {
        string? indexStr = null;
        app.MapGet(indexUri, () => Results.Content(indexStr ??= GenerateIndex(), "text/html"));
        app.MapGet("/", () => Results.Redirect(indexUri));
    }

    private string GenerateIndex()
    {
        var builder = new TagStringBuilder();
        using var _ = builder.IndentTag("html", "lang=\"zh\"");
        using (builder.IndentTag("head"))
        {
            builder.AppendTagLine("meta", "charset = \"UTF-8\"");
            builder.AppendTagLine("meta", "name=\"viewport\" content=\"width=device-width\"");
            using (builder.Tag("title")) builder.Append("海洋垃圾监测系统");
        }
        using (builder.IndentTag("body"))
        {
            using (builder.Tag("h1")) builder.Append("Marine Litter Monitor System");
            using (builder.Tag("p")) builder.Append("记录查询入口：");
            using (builder.Tag("p"))
            using (builder.Tag("ul"))
            {
                using (builder.IndentTag("li"))
                using (builder.Tag("a", "href = \"/logs\""))
                    builder.Append("日志记录");

                using (builder.IndentTag("li"))
                using (builder.Tag("a", "href = \"/pics\""))
                    builder.Append("图像记录");
            }
        }

        return builder.ToString();
    }
}