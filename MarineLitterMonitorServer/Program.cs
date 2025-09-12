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
if (!CameraInteractions.InitializeCamera(width, height))
{
    Console.WriteLine("Error: camera initialize failed.");
    Exit();
}
Console.WriteLine("Camera initialized.");

try
{
    byte[] buffer = new byte[width * height * 3];
    // this takes about 4ms
    var bytes = CameraInteractions.GetWebcamFrame(buffer, buffer.Length, out int outWidth, out int outHeight, out int channels, false);
    Console.WriteLine($"out: width = {outWidth}, height = {outHeight}, channels = {channels}");
    Console.WriteLine($"read bytes: {bytes}");

    var img = Image.LoadPixelData<Bgr24>(buffer, width, height);
    img.Save("./save.png");
}
finally
{
    CameraInteractions.ReleaseCamera();
    Exit();
}