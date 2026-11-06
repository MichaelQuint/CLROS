using OS.Kernel.Platform;

namespace OS.Drivers;

public readonly struct DmaBufferAllocation
{
    public readonly DmaAddressRange Range;
    public readonly DmaBufferToken Token;
    public bool IsInitialized => Range.IsInitialized && Token.IsInitialized;

    internal DmaBufferAllocation(DmaAddressRange range, DmaBufferToken token)
    {
        Range = range;
        Token = token;
    }
}
