namespace OS.Kernel.Platform;

public readonly struct PlatformFramebufferResult
{
    public const uint BytesPerPixel = 4;
    private PlatformFramebufferResult(bool succeeded, PlatformFramebufferStatus status, FirmwareServicesMode mode, PlatformFramebufferSourceStatus sourceStatus, PlatformFramebufferMetadata metadata, ulong requiredByteLength, ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        SourceStatus = sourceStatus;
        Metadata = metadata;
        RequiredByteLength = requiredByteLength;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded
    {
        get;
    }
    public PlatformFramebufferStatus Status
    {
        get;
    }
    public FirmwareServicesMode Mode
    {
        get;
    }
    public FirmwareServicesMode FirmwareMode => Mode;
    public string Operation => PlatformFramebufferOperations.CaptureSnapshot;
    public PlatformFramebufferSourceStatus SourceStatus
    {
        get;
    }
    public PlatformFramebufferMetadata Metadata
    {
        get;
    }
    public ulong BaseAddress => Metadata.BaseAddress;
    public ulong ByteLength => Metadata.ByteLength;
    public uint Width => Metadata.Width;
    public uint Height => Metadata.Height;
    public uint StridePixels => Metadata.StridePixels;
    public PlatformFramebufferPixelFormat PixelFormat => Metadata.PixelFormat;
    public ulong RequiredByteLength
    {
        get;
    }
    public ulong FirmwareStatus
    {
        get;
    }

    public static PlatformFramebufferResult Capture(FirmwareCallResult guardResult, PlatformFramebufferSourceResult sourceResult)
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
        PlatformFramebufferStatus metadataStatus = ValidateMetadata(metadata, out ulong requiredByteLength);
        if (metadataStatus != PlatformFramebufferStatus.Success)
        {
            return new PlatformFramebufferResult(false, metadataStatus, guardResult.Mode, sourceResult.Status, metadata, requiredByteLength, sourceResult.FirmwareStatus);
        }

        return new PlatformFramebufferResult(true, PlatformFramebufferStatus.Success, guardResult.Mode, sourceResult.Status, metadata, requiredByteLength, sourceResult.FirmwareStatus);
    }

    public static PlatformFramebufferResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformFramebufferResult(false, PlatformFramebufferStatus.FirmwareServicesUnavailable, guardResult.Mode, PlatformFramebufferSourceStatus.Unknown, default, 0, 0);
    }

    private static PlatformFramebufferResult SourceFailure(FirmwareCallResult guardResult, PlatformFramebufferSourceResult sourceResult)
    {
        PlatformFramebufferStatus status = sourceResult.Status == PlatformFramebufferSourceStatus.Unavailable ? PlatformFramebufferStatus.SourceUnavailable : PlatformFramebufferStatus.SourceFailed;
        return new PlatformFramebufferResult(false, status, guardResult.Mode, sourceResult.Status, sourceResult.Metadata, 0, sourceResult.FirmwareStatus);
    }

    private static PlatformFramebufferStatus ValidateMetadata(PlatformFramebufferMetadata metadata, out ulong requiredByteLength)
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
        return pixelFormat is PlatformFramebufferPixelFormat.Rgbx8888 or PlatformFramebufferPixelFormat.Bgrx8888;
    }

    private static bool TryCalculateRequiredByteLength(PlatformFramebufferMetadata metadata, out ulong requiredByteLength)
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
