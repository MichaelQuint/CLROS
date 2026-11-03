using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed partial class HelloMemory
    {
        // Optional quota attribution replays actual committed backing separately
        // from the older virtual-access model. No physical addresses or allocator
        // placement are inferred. Every mutation must join a successful original
        // CALL; the complete protocol and native owner remain authoritative.
        internal sealed partial class DynamicBacking
        {
            internal readonly record struct Origin(int RecordIndex, ulong Start, ulong Bytes, uint Protection, ulong Identity, ulong Rip, bool Anonymous);
        }
    }
}
