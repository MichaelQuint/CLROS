using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using Core = OS.Boot.ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory;

internal static unsafe partial class ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemoryTests
{
    private struct Storage
    {
        public FirstExitOwnedArenaState Arena;
        public FirmwareAdapterAuthority Authority;
        public FirmwareAdapterAuthority OtherAuthority;
        public Capabilities Capabilities;
        public fixed ulong Calls[8];
        public ulong ReturnStatus, SeenHandle, SeenOffset, SeenGuidLow, SeenGuidHigh;
        public uint Reenter;
        public FirmwareAdapterStatus ReenteredStatus;
    }
}
