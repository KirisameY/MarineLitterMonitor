using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        builder.Services.AddCarter();

        var app = builder.Build();
        app.MapCarter();

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