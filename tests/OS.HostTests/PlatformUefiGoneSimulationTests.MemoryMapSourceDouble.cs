using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class MemoryMapSourceDouble : IPlatformMemoryMapSource
    {
        private readonly UefiMemoryMapSnapshot _snapshot;
        public MemoryMapSourceDouble(UefiMemoryMapSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public int CaptureCount { get; private set; }

        public UefiMemoryMapSnapshot CaptureSnapshot()
        {
            CaptureCount++;
            return _snapshot;
        }
    }
}
