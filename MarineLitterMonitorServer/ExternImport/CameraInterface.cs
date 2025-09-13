using System.Runtime.InteropServices;

namespace MarineLitterMonitor.Server.ExternImport;

internal partial class CameraInterface : IDisposable
{
    #region Import

    private const string LibName = "MarineLitterMonitorDevice";

    [LibraryImport(LibName, EntryPoint = "InitializeCamera")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InitializeCameraNative(out int width, out int height);

    [LibraryImport(LibName, EntryPoint = "SetCameraSize")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCameraSizeNative(int width, int height, out int finalWidth, out int finalHeight);


    [LibraryImport(LibName, EntryPoint = "GetWebcamFrame")]
    private static partial int GetWebcamFrameNative(
        [Out] byte[] buffer, int bufferSize,
        out int outWidth, out int outHeight, out int outChannels,
        [MarshalAs(UnmanagedType.Bool)] bool toRgb);

    [LibraryImport(LibName, EntryPoint = "ReleaseCamera")]
    private static partial void ReleaseCameraNative();

    #endregion


    // Singleton & Dispose
    private CameraInterface()
    {
        if (!InitializeCameraNative(out int width, out int height)) throw new CameraInitializationException();
        Size = (width, height);
    }

    private static CameraInterface? _instance;

    public static CameraInterface GetInstance() => _instance ??= new();

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;

        Disposed = true;
        ReleaseCameraNative();
    }


    // Instance Api
    public (int Width, int Height) Size { get; private set; }

    public bool SetSize(int width, int height)
    {
        var result = SetCameraSizeNative(width, height, out int finalWidth, out int finalHeight);
        Size = (finalWidth, finalHeight);
        return result;
    }

    public int GetFrame([Out] byte[] buffer, bool toRgb = true)
    {
        var result = GetWebcamFrameNative(buffer, buffer.Length, out var outWidth, out var outHeight, out var outChannels, toRgb);
        if ((outWidth, outHeight) != Size)
        {
            throw new UnexpectedFrameParameterException($"Unexpected out size: {outWidth}, {outHeight}. (expected: {Size.Width}, {Size.Height})");
        }
        if (outChannels != 3)
        {
            throw new UnexpectedFrameParameterException($"Unexpected out channels: {outChannels}. (expected: 3)");
        }

        return result;
    }


    // Exceptions

    public class CameraInitializationException() : Exception("Camera initialize failed");

    public class UnexpectedFrameParameterException(string info) : Exception(info);
}