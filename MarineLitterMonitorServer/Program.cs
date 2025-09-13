using MarineLitterMonitor.Server.ExternImport;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

void BeforeExit()
{
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();
    Console.WriteLine();
}

const int width = 640;
const int height = 480;
const byte outputPin = 24;


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
        BeforeExit();
        return 0;
    }
}
catch (CameraInterface.CameraInitializationException)
{
    Console.WriteLine("Error: camera initialize failed.");
    BeforeExit();
    return 0;
}

Console.WriteLine("Camera initialized.");

#endregion


try
{
    using var camera = cameraInit;

    byte[] buffer = new byte[width * height * 3];
    byte[] nBuffer = new byte[width * height * 3 * 4];
    // this takes about 4ms
    var (bytes, nBytes) = camera.GetFrameAndNormalized(buffer, nBuffer);
    Console.WriteLine($"read bytes: {bytes}");
    Console.WriteLine($"read nBytes: {nBytes}");

    var img = Image.LoadPixelData<Bgr24>(buffer, width, height);
    img.Save("./save.png");

    // using (var gpioOut = GpioInterface.GetInstance(outputPin))
    // {
    //     gpioOut.Write(true);
    //     Console.WriteLine("Any key to stop");
    //     Console.ReadKey();
    //     gpioOut.Write(false);
    // }
}
finally
{
    BeforeExit();
}
return 0;