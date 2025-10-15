using System.Net;

using MarineLitterMonitor.Server.FileRecording;

namespace MarineLitterMonitor.Server.WebServing;

internal class WebServer : IAsyncDisposable
{
    private WebServer(WebApplication app)
    {
        _app = app;
    }

    private readonly WebApplication _app;

    public static async Task<WebServer> StartAsync(RecordManager recordManager)
    {
        var builder = WebApplication.CreateBuilder();
        // 不处理中止请求
        builder.Services.Configure<ConsoleLifetimeOptions>(opts => opts.SuppressStatusMessages = true);
        // 设置监听地址
        builder.WebHost.UseUrls("http://*:80");

        var app = builder.Build();

        List<IWebModule> modules =
        [
            new IndexModule("/index", "/logs", "/pics"),
            new LogModule("/logs", recordManager),
            new PicModule("/pics", recordManager),
        ];

        modules.ForEach(m => m.Map(app));

        app.MapFallback(() => Results.NotFound("Error 404: 未识别的URL"));
        await app.StartAsync();
        Console.WriteLine("Web Server started!");

        return new WebServer(app);
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("Web Server stoping...");
        await _app.StopAsync();
        await _app.DisposeAsync();
        Console.WriteLine("Web Server stopped");
    }
}