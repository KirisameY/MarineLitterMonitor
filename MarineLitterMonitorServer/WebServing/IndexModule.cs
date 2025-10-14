namespace MarineLitterMonitor.Server.WebServing;

internal class IndexModule(string indexUri) : IWebModule
{
    public void Map(WebApplication app)
    {
        app.MapGet(indexUri, () => Results.Content(GenerateIndex(), "text/html"));
        app.MapGet("/", () => Results.Redirect(indexUri));
    }

    private string GenerateIndex() => """
        <html lang='zh'>
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width" />
              <title>海洋垃圾监测系统</title>
            </head>
            <body>
                <h1>Marine Litter Monitor System</h1>
                <p>Hello, world!</p>
            </body>
        </html>
        """;
}