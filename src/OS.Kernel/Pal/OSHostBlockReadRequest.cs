using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostBlockReadRequest
{
    public readonly OSHostHandle Device;
    public readonly ulong FirstBlock;
    public readonly uint BlockCount;
    public readonly uint BlockSizeBytes;
    public readonly OSHostByteSpan Destination;
    public OSHostBlockReadRequest(OSHostHandle device, ulong firstBlock, uint blockCount, uint blockSizeBytes, OSHostByteSpan destination)
    {
        Device = device;
        FirstBlock = firstBlock;
        BlockCount = blockCount;
        BlockSizeBytes = blockSizeBytes;
        Destination = destination;
    }
}
