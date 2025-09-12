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
        [Out] byte[] buffer, // [Out] 表示数据从native流向managed
        int bufferSize,
        out int outWidth,
        out int outHeight,
        out int outChannels);

    [LibraryImport(DllName, EntryPoint = "ReleaseCamera")]
    public static partial void ReleaseCamera();
}