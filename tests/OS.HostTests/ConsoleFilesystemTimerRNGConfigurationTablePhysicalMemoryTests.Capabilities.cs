using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using Core = OS.Boot.ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory;

internal static unsafe partial class ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemoryTests
{
    private struct Capabilities
    {
        public FirmwareConsoleCapability Console;
        public FirmwareMemoryMapCapability Map;
        public FirmwareFileSystemCapability Files;
        public FirmwareTimerCapability Timer;
        public FirmwareRandomCapability Random;
        public FirmwareConfigurationTableCapability Tables;
        public FirmwarePhysicalMemoryCapability Memory;
        public FirmwareFramebufferCapability Framebuffer;
    }
}
