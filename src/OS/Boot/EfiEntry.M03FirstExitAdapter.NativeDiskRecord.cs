#if M16_025_PROBE
using OS.Storage;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Boot;

#pragma warning disable 649 // Eight contiguous caller-owned records are written through admitted native pointers.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {

        // One bounded pump can complete/copy and submit its next request. Keep
        // their original order without performing serial I/O inside callbacks.
        private struct NativeDiskRecord
        {
            internal Block.Observation Observation;
            internal fixed byte Bytes[512];
        }


    }
}
#pragma warning restore 649
#endif
