using System.Runtime.InteropServices;

namespace MarineLitterMonitor.Server.ExternImport;

public static partial class CameraInteractions
{
    private const string DllName = "MarineLitterMonitorDevice";

    [LibraryImport(DllName, EntryPoint = "InitializeCamera")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InitializeCamera(int width, int height);

    [LibraryImport(DllName, EntryPoint = "GetWebcamFrame")]
    public static partial int GetWebcamFrame(
        [Out] byte[] buffer, int bufferSize,
        out int outWidth, out int outHeight, out int outChannels,
        [MarshalAs(UnmanagedType.Bool)] bool toRgb);

    [LibraryImport(DllName, EntryPoint = "ReleaseCamera")]
    public static partial void ReleaseCamera();
}