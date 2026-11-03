using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class UnguardedConsole : IPlatformConsole
    {
        public int WriteCount { get; private set; }

        public PlatformConsoleResult WriteLine(string line)
        {
            WriteCount++;
            return PlatformConsoleResult.Written(FirmwareCallResult.Allowed(PlatformConsoleOperations.OutputString, FirmwareServicesMode.BootServicesAvailable), line);
        }
    }
}
