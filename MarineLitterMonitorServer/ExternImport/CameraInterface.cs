using System.Runtime.InteropServices;

namespace MarineLitterMonitor.Server.ExternImport;

internal sealed partial class CameraInterface : IDisposable
{
    #region Import

    private const string LibName = "MarineLitterMonitorDevice";

    [LibraryImport(LibName, EntryPoint = "initialize_camera")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool InitializeCameraNative(out int width, out int height);

    [LibraryImport(LibName, EntryPoint = "set_camera_size")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool SetCameraSizeNative(int width, int height, out int finalWidth, out int finalHeight);

    [LibraryImport(LibName, EntryPoint = "get_webcam_frame")]
    private static partial int GetWebcamFrameNative(
        [Out] byte[] buffer, int bufferSize,
        out int outWidth, out int outHeight, out int outChannels);

    [LibraryImport(LibName, EntryPoint = "get_webcam_frame_and_normalized")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool GetWebcamFrameAndNormalizedNative(
        [Out] byte[] buffer, int bufferSize, out int outSize,
        [Out] byte[] nBuffer, int nBufferSize, out int nOutSize,
        out int outWidth, out int outHeight);

    [LibraryImport(LibName, EntryPoint = "release_camera")]
    private static partial void ReleaseCameraNative();

    #endregion


    #region Singleton & Dispose

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
        _instance = null;
    }

    #endregion


    // Instance Api
    public (int Width, int Height) Size { get; private set; }

    public bool SetSize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        var result = SetCameraSizeNative(width, height, out int finalWidth, out int finalHeight);
        Size = (finalWidth, finalHeight);
        return result;
    }

    public int GetFrame([Out] byte[] buffer)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        var result = GetWebcamFrameNative(buffer, buffer.Length, out var outWidth, out var outHeight, out var outChannels);
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

    public (int Size, int NSize) GetFrameAndNormalized([Out] byte[] buffer, [Out] byte[] nBuffer)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        var result = GetWebcamFrameAndNormalizedNative(buffer, buffer.Length, out var outSize,
                                                       nBuffer, nBuffer.Length, out var nOutSize,
                                                       out var outWidth, out var outHeight);
        if ((outWidth, outHeight) != Size)
        {
            throw new UnexpectedFrameParameterException($"Unexpected out size: {outWidth}, {outHeight}. (expected: {Size.Width}, {Size.Height})");
        }

        return (outSize, nOutSize);
    }


    // Exceptions

    public class CameraInitializationException() : Exception("Camera initialize failed");

    public class UnexpectedFrameParameterException(string info) : Exception(info);
}