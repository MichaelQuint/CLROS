using System;

namespace OS.Storage;

public interface IReadOnlyBlockDevice
{
    BlockDeviceInfo Info
    {
        get;
    }

    BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination);
}
