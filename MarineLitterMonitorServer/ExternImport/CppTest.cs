using System.Runtime.InteropServices;

namespace MarineLitterMonitor.Server.ExternImport;

public static partial class CppTest
{
    [LibraryImport("MarineLitterMonitorDevice", EntryPoint = "mlmd_test_add_1")]
    private static partial int NativeTestAdd1(int i);

    public static void PrintTestAdd1(int i)
    {
        Console.WriteLine($"{i} + 1 is: {NativeTestAdd1(i)}");
    }
}