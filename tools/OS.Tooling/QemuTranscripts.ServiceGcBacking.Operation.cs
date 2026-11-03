namespace OS.Tooling;
public static partial class QemuTranscripts
{
    // Original ELF165683B2 and runtime VMR820230c386db5b0106c5430b3e5aee92a7826bf5:
    // GCHeap.Initialize -> initialize_gc -> virtual_alloc -> VirtualReserve.
    // Default region arena is5*hard-limit; libc alignment adds one page, then
    // VirtualReserve trims it and excludes the exact arena from dumps. Shared
    // libc RIP alone is insufficient: require this complete startup sequence.
    private sealed partial class ServiceGcBacking
    {
        private enum Operation
        {
            Revoke,
            Protect,
            Decommit,
            Advise
        }
    }
}
