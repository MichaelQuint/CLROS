namespace OS.Kernel.Platform;

public interface IPlatformFramebuffer
{
    PlatformFramebufferResult CaptureSnapshot();
}

public interface IPlatformFramebufferSource
{
    PlatformFramebufferSourceResult CaptureSnapshot();
}

public static class PlatformFramebufferOperations
{
    public const string CaptureSnapshot = "Framebuffer.CaptureSnapshot";
}

public enum PlatformFramebufferPixelFormat
{
    Unknown = 0,
    Rgbx8888 = 1,
    Bgrx8888 = 2,
    Bitmask = 3,
    BltOnly = 4
}

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

public enum PlatformFramebufferSourceStatus
{
    Unknown = 0,
    Success = 1,
    Unavailable = 2,
    FirmwareError = 3
}

public readonly struct PlatformFramebufferMetadata
{
    public PlatformFramebufferMetadata(
        ulong baseAddress,
        ulong byteLength,
        uint width,
        uint height,
        uint stridePixels,
        PlatformFramebufferPixelFormat pixelFormat)
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

public readonly struct PlatformFramebufferSourceResult
{
    private PlatformFramebufferSourceResult(
        bool succeeded,
        PlatformFramebufferSourceStatus status,
        PlatformFramebufferMetadata metadata,
        ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        Metadata = metadata;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded { get; }

    public PlatformFramebufferSourceStatus Status { get; }

    public PlatformFramebufferMetadata Metadata { get; }

    public ulong FirmwareStatus { get; }

    public static PlatformFramebufferSourceResult Captured(PlatformFramebufferMetadata metadata)
    {
        return new PlatformFramebufferSourceResult(
            true,
            PlatformFramebufferSourceStatus.Success,
            metadata,
            0);
    }

    public static PlatformFramebufferSourceResult Unavailable()
    {
        return new PlatformFramebufferSourceResult(
            false,
            PlatformFramebufferSourceStatus.Unavailable,
            default,
            0);
    }

    public static PlatformFramebufferSourceResult FirmwareError(ulong firmwareStatus)
    {
        return new PlatformFramebufferSourceResult(
            false,
            PlatformFramebufferSourceStatus.FirmwareError,
            default,
            firmwareStatus);
    }
}

public readonly struct PlatformFramebufferResult
{
    public const uint BytesPerPixel = 4;

    private PlatformFramebufferResult(
        bool succeeded,
        PlatformFramebufferStatus status,
        FirmwareServicesMode mode,
        PlatformFramebufferSourceStatus sourceStatus,
        PlatformFramebufferMetadata metadata,
        ulong requiredByteLength,
        ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        SourceStatus = sourceStatus;
        Metadata = metadata;
        RequiredByteLength = requiredByteLength;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded { get; }

    public PlatformFramebufferStatus Status { get; }

    public FirmwareServicesMode Mode { get; }

    public FirmwareServicesMode FirmwareMode => Mode;

    public string Operation => PlatformFramebufferOperations.CaptureSnapshot;

    public PlatformFramebufferSourceStatus SourceStatus { get; }

    public PlatformFramebufferMetadata Metadata { get; }

    public ulong BaseAddress => Metadata.BaseAddress;

    public ulong ByteLength => Metadata.ByteLength;

    public uint Width => Metadata.Width;

    public uint Height => Metadata.Height;

    public uint StridePixels => Metadata.StridePixels;

    public PlatformFramebufferPixelFormat PixelFormat => Metadata.PixelFormat;

    public ulong RequiredByteLength { get; }

    public ulong FirmwareStatus { get; }

    public static PlatformFramebufferResult Capture(
        FirmwareCallResult guardResult,
        PlatformFramebufferSourceResult sourceResult)
    {
        if (!guardResult.Succeeded)
        {
            return FirmwareServicesUnavailable(guardResult);
        }

        if (!sourceResult.Succeeded)
        {
            return SourceFailure(guardResult, sourceResult);
        }

        PlatformFramebufferMetadata metadata = sourceResult.Metadata;
        PlatformFramebufferStatus metadataStatus = ValidateMetadata(
            metadata,
            out ulong requiredByteLength);

        if (metadataStatus != PlatformFramebufferStatus.Success)
        {
            return new PlatformFramebufferResult(
                false,
                metadataStatus,
                guardResult.Mode,
                sourceResult.Status,
                metadata,
                requiredByteLength,
                sourceResult.FirmwareStatus);
        }

        return new PlatformFramebufferResult(
            true,
            PlatformFramebufferStatus.Success,
            guardResult.Mode,
            sourceResult.Status,
            metadata,
            requiredByteLength,
            sourceResult.FirmwareStatus);
    }

    public static PlatformFramebufferResult FirmwareServicesUnavailable(
        FirmwareCallResult guardResult)
    {
        return new PlatformFramebufferResult(
            false,
            PlatformFramebufferStatus.FirmwareServicesUnavailable,
            guardResult.Mode,
            PlatformFramebufferSourceStatus.Unknown,
            default,
            0,
            0);
    }

    private static PlatformFramebufferResult SourceFailure(
        FirmwareCallResult guardResult,
        PlatformFramebufferSourceResult sourceResult)
    {
        PlatformFramebufferStatus status =
            sourceResult.Status == PlatformFramebufferSourceStatus.Unavailable
                ? PlatformFramebufferStatus.SourceUnavailable
                : PlatformFramebufferStatus.SourceFailed;

        return new PlatformFramebufferResult(
            false,
            status,
            guardResult.Mode,
            sourceResult.Status,
            sourceResult.Metadata,
            0,
            sourceResult.FirmwareStatus);
    }

    private static PlatformFramebufferStatus ValidateMetadata(
        PlatformFramebufferMetadata metadata,
        out ulong requiredByteLength)
    {
        requiredByteLength = 0;

        if (metadata.BaseAddress == 0)
        {
            return PlatformFramebufferStatus.InvalidBaseAddress;
        }

        if (metadata.ByteLength == 0)
        {
            return PlatformFramebufferStatus.InvalidByteLength;
        }

        if (metadata.Width == 0 || metadata.Height == 0)
        {
            return PlatformFramebufferStatus.InvalidDimensions;
        }

        if (metadata.StridePixels < metadata.Width)
        {
            return PlatformFramebufferStatus.InvalidStride;
        }

        if (!IsSupportedPixelFormat(metadata.PixelFormat))
        {
            return PlatformFramebufferStatus.UnsupportedPixelFormat;
        }

        if (!TryCalculateRequiredByteLength(metadata, out requiredByteLength))
        {
            return PlatformFramebufferStatus.ArithmeticOverflow;
        }

        if (metadata.ByteLength < requiredByteLength)
        {
            return PlatformFramebufferStatus.InvalidBufferSize;
        }

        return PlatformFramebufferStatus.Success;
    }

    private static bool IsSupportedPixelFormat(PlatformFramebufferPixelFormat pixelFormat)
    {
        return pixelFormat is PlatformFramebufferPixelFormat.Rgbx8888
            or PlatformFramebufferPixelFormat.Bgrx8888;
    }

    private static bool TryCalculateRequiredByteLength(
        PlatformFramebufferMetadata metadata,
        out ulong requiredByteLength)
    {
        requiredByteLength = 0;

        ulong strideBytes = (ulong)metadata.StridePixels * BytesPerPixel;
        if (metadata.Height > ulong.MaxValue / strideBytes)
        {
            return false;
        }

        requiredByteLength = strideBytes * metadata.Height;
        return true;
    }
}
