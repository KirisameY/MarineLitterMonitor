using MarineLitterMonitor.Server.FileRecording;

namespace MarineLitterMonitor.Server.WebServing;

internal class LogModule(string uri, RecordManager recordManager) : IWebModule
{
    public void Map(WebApplication app)
    {
        var group = app.MapGroup(uri);
        group.MapGet("/", () => Results.Content(GenerateIndexPage(), "text/html"));
        group.MapGet(@"{date:regex(^\d{{4}}_\d{{2}}_\d{{2}}$)}", (string date) =>
        {
            if (DateOnly.TryParseExact(date, "yyyy_MM_dd", out DateOnly dateOnly))
                return Results.Content(GenerateDayLogPage(dateOnly), "text/html");
            return Results.Redirect(uri);
        });
        group.MapFallback(() => Results.Redirect(uri));
    }

    private string GenerateIndexPage()
    {
        var logDates = recordManager.ReadLogs().ToArray();

        var builder = new TagStringBuilder();
        using (builder.IndentTag("html", "lang=\"zh\""))
        {
            using (builder.IndentTag("head"))
            {
                builder.AppendTagLine("meta", "charset = \"UTF-8\"");
                builder.AppendTagLine("meta", "name=\"viewport\" content=\"width=device-width\"");
                using (builder.Tag("title")) builder.Append("日志索引页");
            }
            using (builder.IndentTag("body"))
            {
                using (builder.Tag("h1")) builder.Append("Marine Litter Monitor System/Logs");

                using (builder.Tag("p"))
                using (builder.Tag("a", "href = \"/\""))
                    builder.Append("返回首页");

                if (logDates.Length > 0)
                {
                    using (builder.Tag("p")) builder.Append("查找到以下日期的记录：");

                    using (builder.Tag("p"))
                    using (builder.Tag("ul"))
                    {
                        foreach (var logDate in logDates)
                        {
                            using (builder.IndentTag("li"))
                            using (builder.Tag("a", $"href = \"{uri}/{logDate:yyyy_MM_dd}\""))
                                builder.Append($"{logDate:D}");
                        }
                    }
                }
                else
                {
                    using (builder.Tag("p")) builder.Append("未找到任何记录。");
                }
            }
        }

        return builder.ToString();
    }

    private string GenerateDayLogPage(DateOnly date)
    {
        var logs = recordManager.ReadDay(date);

        var builder = new TagStringBuilder();
        using (builder.IndentTag("html", "lang=\"zh\""))
        {
            using (builder.IndentTag("head"))
            {
                builder.AppendTagLine("meta", "charset = \"UTF-8\"");
                builder.AppendTagLine("meta", "name=\"viewport\" content=\"width=device-width\"");
                using (builder.Tag("title")) builder.Append($"日志：{date:yy/MM/dd}");
            }
            using (builder.IndentTag("body"))
            {
                using (builder.Tag("h1")) builder.Append($"Marine Litter Monitor System/Logs/{date:yyyy_MM_dd}");

                using (builder.Tag("p"))
                {
                    using (builder.Tag("a", "href = \"/\""))
                        builder.Append("返回首页");
                    builder.Append("  ");
                    using (builder.Tag("a", $"href = \"{uri}\""))
                        builder.Append("返回索引");
                }

                if (logs is not null && logs.Count > 0)
                {
                    using (builder.Tag("p")) builder.Append($"查找到 {date:D} 的记录：");

                    using var t0 = builder.Tag("p");
                    using var t1 = builder.Tag("ul");
                    foreach (var record in logs)
                    {
                        using var t2 = builder.IndentTag("li");
                        builder.AppendLine($"{record.Time:HH:mm:ss}:");

                        using var t3 = builder.IndentTag("ol");
                        foreach (var entry in record.Entries)
                        {
                            using (builder.Tag("li"))
                            {
                                builder.Append($"{entry.Label} ({entry.Confidence:P1})");
                            }
                            builder.AppendLine();
                        }
                    }
                }
                else
                {
                    using (builder.Tag("p")) builder.Append($"未找到 {date:D} 的任何记录。");
                }
            }
        }

        return builder.ToString();
    }
}