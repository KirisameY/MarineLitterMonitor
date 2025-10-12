using Carter;

using Microsoft.AspNetCore.Builder;

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
        builder.Services.AddCarter();

        var app = builder.Build();
        app.MapCarter();

        await app.StartAsync();

        return new WebServer(app);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}