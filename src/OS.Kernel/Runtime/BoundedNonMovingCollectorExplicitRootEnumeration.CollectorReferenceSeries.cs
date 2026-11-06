using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;

public readonly struct CollectorReferenceSeries
{
    internal CollectorReferenceSeries(ulong offset, ulong bytes)
    {
        OffsetFromReference = offset;
        Bytes = bytes;
    }

    public ulong OffsetFromReference
    {
        get;
    }
    public ulong Bytes
    {
        get;
    }
}
