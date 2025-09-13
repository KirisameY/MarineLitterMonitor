using System.Diagnostics;

using MarineLitterMonitor.Server.ExternImport;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.PixelFormats;

void Exit()
{
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();
    Console.WriteLine();
    Environment.Exit(0);
}

const int width = 640;
const int height = 480;


Console.WriteLine("Hello, world!");

#region cameraInit

CameraInterface cameraInit;

try
{
    cameraInit = CameraInterface.GetInstance();
    cameraInit.SetSize(width, height);
    if (cameraInit.Size != (width, height))
    {
        Console.WriteLine($"Error: camera size is {cameraInit.Size}. (expected: {(width, height)})");
        Exit();
        return;
    }
}
catch (CameraInterface.CameraInitializationException)
{
    Console.WriteLine("Error: camera initialize failed.");
    Exit();
    return;
}

Console.WriteLine("Camera initialized.");

#endregion


try
{
    using var camera = cameraInit;

    byte[] buffer = new byte[width * height * 3];
    // this takes about 4ms
    var bytes = camera.GetFrame(buffer, false);
    Console.WriteLine($"read bytes: {bytes}");

    var img = Image.LoadPixelData<Bgr24>(buffer, width, height);
    img.Save("./save.png");
}
finally
{
    Exit();
}