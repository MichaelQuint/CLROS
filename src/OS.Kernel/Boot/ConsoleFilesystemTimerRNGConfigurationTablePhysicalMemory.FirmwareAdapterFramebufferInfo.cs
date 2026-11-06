using OS.Kernel.Platform;

namespace OS.Boot;

public struct FirmwareAdapterFramebufferInfo
{
    public ulong BaseAddress, ByteLength;
    public uint Width, Height, StridePixels, PixelFormat;
}
