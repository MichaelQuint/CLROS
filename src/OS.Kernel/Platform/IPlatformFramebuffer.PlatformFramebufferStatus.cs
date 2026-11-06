namespace OS.Kernel.Platform;

public enum PlatformFramebufferStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    SourceUnavailable = 3,
    SourceFailed = 4,
    InvalidBaseAddress = 5,
    InvalidByteLength = 6,
    InvalidDimensions = 7,
    InvalidStride = 8,
    UnsupportedPixelFormat = 9,
    InvalidBufferSize = 10,
    ArithmeticOverflow = 11
}
