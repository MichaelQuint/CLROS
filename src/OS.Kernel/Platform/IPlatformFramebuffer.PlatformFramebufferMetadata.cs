namespace OS.Kernel.Platform;
public readonly struct PlatformFramebufferMetadata
{
    public PlatformFramebufferMetadata(ulong baseAddress, ulong byteLength, uint width, uint height, uint stridePixels, PlatformFramebufferPixelFormat pixelFormat)
    {
        BaseAddress = baseAddress;
        ByteLength = byteLength;
        Width = width;
        Height = height;
        StridePixels = stridePixels;
        PixelFormat = pixelFormat;
    }

    public ulong BaseAddress { get; }
    public ulong ByteLength { get; }
    public uint Width { get; }
    public uint Height { get; }
    public uint StridePixels { get; }
    public PlatformFramebufferPixelFormat PixelFormat { get; }
}
