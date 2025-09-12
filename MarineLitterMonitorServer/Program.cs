using MarineLitterMonitor.Server.ExternImport;

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

try
{
    byte[] buffer = new byte[width * height * 3];
    var bytes = CameraInteractions.GetWebcamFrame(buffer, buffer.Length, out int outWidth, out int outHeight, out int channels);
    Console.WriteLine($"out: width = {outWidth}, height = {outHeight}, channels = {channels}");
    Console.WriteLine($"read bytes: {bytes}");
}
finally
{
    CameraInteractions.ReleaseCamera();
    Exit();
}