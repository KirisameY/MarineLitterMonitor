using System.Net;

namespace MarineLitterMonitor.Server.WebServing;

internal class WebServer : IAsyncDisposable
{
    private WebServer(WebApplication app)
    {
        _app = app;
    }

    private readonly WebApplication _app;

    public static async Task<WebServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        // 不处理中止请求
        builder.Services.Configure<ConsoleLifetimeOptions>(opts => opts.SuppressStatusMessages = true);
        // 设置监听地址
        builder.WebHost.UseUrls("http://*:80");

        var app = builder.Build();
        app.MapGet("/test/{name}/{location}", (string name, string location) =>
        {
            return $"你好, 来自 {location} 的 {name}！";
        });
        app.MapGet("/test/{name}", (string name) =>
        {
            return Results.Redirect($"/test/{WebUtility.UrlEncode(name)}/%E9%85%B1%E6%B5%B7%E5%B8%A6"); // 酱海带
        });
        app.MapGet("/test", () =>
        {
            return Results.Redirect("/test/%E5%8F%B2%E5%90%9B/%E9%85%B1%E6%B5%B7%E5%B8%A6"); // 史君/酱海带
        });

        List<IWebModule> modules =
        [
            new IndexModule("/index"),
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