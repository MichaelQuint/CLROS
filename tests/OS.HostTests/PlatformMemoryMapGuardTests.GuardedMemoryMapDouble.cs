using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;

internal static partial class PlatformMemoryMapGuardTests
{
    private sealed class GuardedMemoryMapDouble : IPlatformMemoryMap
    {
        private readonly IPlatformFirmware _firmware;
        private readonly UefiMemoryMapSnapshot _snapshot;
        public GuardedMemoryMapDouble(IPlatformFirmware firmware, UefiMemoryMapSnapshot snapshot)
        {
            _firmware = firmware;
            _snapshot = snapshot;
        }

        public PlatformMemoryMapResult CopySnapshot()
        {
            FirmwareCallResult guardResult = _firmware.RequireBootServices(PlatformMemoryMapOperations.GetMemoryMap);
            return PlatformMemoryMapResult.Copied(guardResult, _snapshot);
        }
    }
}
