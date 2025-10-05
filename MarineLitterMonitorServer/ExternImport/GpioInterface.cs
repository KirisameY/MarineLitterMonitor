using System.Runtime.InteropServices;

namespace MarineLitterMonitor.Server.ExternImport;

internal sealed partial class GpioInterface : IDisposable
{
    #region Import

    private const string LibName = "MarineLitterMonitorDevice";

    [LibraryImport(LibName, EntryPoint = "gpio_export")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool GpioExportNative(byte pin);

    [LibraryImport(LibName, EntryPoint = "gpio_unexport")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool GpioUnexportNative(byte pin);

    [LibraryImport(LibName, EntryPoint = "gpio_set_direction")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool GpioSetDirectionNative(byte pin, [MarshalAs(UnmanagedType.I1)] bool toOutput);

    [LibraryImport(LibName, EntryPoint = "gpio_write")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool GpioWriteNative(byte pin, [MarshalAs(UnmanagedType.I1)] bool value);

    [LibraryImport(LibName, EntryPoint = "gpio_read")]
    private static partial sbyte GpioReadNative(byte pin);

    #endregion


    #region Multiton & Dispose

    private GpioInterface(byte pin)
    {
        Pin = pin;
        if (!GpioExportNative(pin)) throw new GpioInitializationException(pin);
    }

    private static readonly Dictionary<byte, GpioInterface> Instances = new();

    public static GpioInterface GetInstance(byte pin)
    {
        if (!Instances.TryGetValue(pin, out var instance))
        {
            Instances[pin] = instance = new(pin);
        }

        return instance;
    }

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        GpioUnexportNative(Pin);
        Instances.Remove(Pin);
    }

    #endregion


    #region Instance Api

    public byte Pin { get; init; }
    public bool IsOutput { get; private set; } = false;

    public bool SetDirection(bool toOutput)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        var result = GpioSetDirectionNative(Pin, toOutput);
        if (result) IsOutput = toOutput;
        return result;
    }

    public bool Write(bool value)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (!IsOutput && !SetDirection(true)) return false;
        return GpioWriteNative(Pin, value);
    }

    public bool Read(out bool value)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        value = false;
        if (IsOutput && !SetDirection(false)) return false;
        var result = GpioReadNative(Pin);
        if (result == -1) return false;
        value = result != 0;
        return true;
    }

    #endregion


    // Exceptions
    public class GpioInitializationException(byte pin) : Exception($"Gpio {pin} initialize failed")
    {
        public byte Pin { get; } = pin;
    }
}