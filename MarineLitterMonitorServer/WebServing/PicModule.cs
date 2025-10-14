using MarineLitterMonitor.Server.FileRecording;

namespace MarineLitterMonitor.Server.WebServing;

internal class PicModule(string uri, RecordManager recordManager) : IWebModule
{
    public void Map(WebApplication app)
    {
        var group = app.MapGroup(uri);
        group.MapGet("/", () => Results.Redirect($"{uri}/1"));
        group.MapGet(@"/{page:regex(^\d+$)}", (string page) =>
        {
            var html = GenerateIndexPage(int.Parse(page));
            return Results.Content(html, "text/html");
        });
        group.MapGet(@"/{time:regex(^\d{{4}}-\d{{2}}-\d{{2}}_\d{{2}}-\d{{2}}-\d{{2}}$)}", (string time) =>
        {
            var datetime = DateTime.ParseExact(time, "yyyy-MM-dd_HH-mm-ss", null);
            var picBytes = recordManager.ReadPic(datetime);
            if (picBytes is null) return Results.Empty;
            return Results.File(picBytes, "image/jpeg");
        });
        group.MapFallback(() => Results.Redirect(uri));
    }

    private string GenerateIndexPage(int page)
    {
        const int perPage = 16;

        page = Math.Max(page, 1);
        var picDates = recordManager.ReadPics().OrderDescending().ToArray();

        var builder = new TagStringBuilder();
        using (builder.IndentTag("html", "lang=\"zh\""))
        {
            using (builder.IndentTag("head"))
            {
                builder.AppendTagLine("meta", "charset = \"UTF-8\"");
                builder.AppendTagLine("meta", "name=\"viewport\" content=\"width=device-width\"");
                using (builder.Tag("title")) builder.Append($"照片页({page})");
            }
            using (builder.IndentTag("body"))
            {
                using (builder.Tag("h1")) builder.Append($"Marine Litter Monitor System/Pics/{page}");

                using (builder.Tag("p"))
                using (builder.Tag("a", "href = \"/\""))
                    builder.Append("返回首页");

                if (picDates.Length > 0)
                {
                    using (builder.Tag("p")) builder.Append($"查找到以下时刻的照片（第 {page} 页）：");

                    using (builder.Tag("p"))
                    using (builder.Tag("ul"))
                    {
                        foreach (var logDate in picDates.Skip((page - 1) * perPage).Take(perPage))
                        {
                            using var _ = builder.IndentTag("li");
                            using (builder.Tag("p")) builder.Append($"{logDate:D} {logDate:T}");
                            builder.AppendLine();
                            builder.AppendTagLine("img", $"src=\"{uri}/{logDate:yyyy-MM-dd_HH-mm-ss}\" "
                                                + $"alt=\"Capture in {logDate:D} {logDate:T}\"");
                        }
                    }
                }
                else
                {
                    using (builder.Tag("p")) builder.Append($"该页无任何记录（第 {page} 页）。");
                }

                var prevActive = page > 1;
                var nextActive = page * perPage < picDates.Length;

                const string prevText = "< 上一页  ";
                const string nextText = "  下一页 >";

                using var t = builder.IndentTag("p");

                if (prevActive)
                {
                    using (builder.Tag("a", $"href=\"{uri}/{page - 1}\"")) builder.Append(prevText);
                }
                else builder.Append(prevText);

                builder.Append(page.ToString());

                if (nextActive)
                {
                    using (builder.Tag("a", $"href=\"{uri}/{page + 1}\"")) builder.Append(nextText);
                }
                else builder.Append(nextText);
            }
        }

        return builder.ToString();
    }
}