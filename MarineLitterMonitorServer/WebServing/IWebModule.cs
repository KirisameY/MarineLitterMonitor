namespace MarineLitterMonitor.Server.WebServing;

internal interface IWebModule
{
    void Map(WebApplication app);
}

internal static class WebModuleExtensions
{
    public static void MapModule(this WebApplication app, IWebModule module)
    {
        module.Map(app);
    }
}