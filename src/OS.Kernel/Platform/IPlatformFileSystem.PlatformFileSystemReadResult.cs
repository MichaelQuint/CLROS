namespace OS.Kernel.Platform;
public readonly struct PlatformFileSystemReadResult
{
    public const int MaximumPathLength = 128;
    private PlatformFileSystemReadResult(bool succeeded, PlatformFileSystemStatus status, FirmwareServicesMode mode, int pathLength, int bytesRead, int requiredBytes, int availableBytes, ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        PathLength = pathLength;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded { get; }
    public PlatformFileSystemStatus Status { get; }
    public FirmwareServicesMode Mode { get; }
    public string Operation => PlatformFileSystemOperations.ReadFile;
    public int PathLength { get; }
    public int BytesRead { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public ulong FirmwareStatus { get; }

    public static PlatformFileSystemReadResult Read(FirmwareCallResult guardResult, ReadOnlySpan<byte> relativePath, PlatformFileSystemSourceReadResult sourceResult, int availableBytes)
    {
        if (!guardResult.Succeeded)
        {
            return FirmwareServicesUnavailable(guardResult);
        }

        if (!IsSafeRelativeAsciiPath(relativePath))
        {
            return InvalidPath(guardResult, relativePath.Length, availableBytes);
        }

        if (!sourceResult.Succeeded)
        {
            return SourceFailure(guardResult, relativePath.Length, sourceResult, availableBytes);
        }

        return new PlatformFileSystemReadResult(true, PlatformFileSystemStatus.Success, guardResult.Mode, relativePath.Length, sourceResult.BytesRead, sourceResult.RequiredBytes, availableBytes, sourceResult.FirmwareStatus);
    }

    public static PlatformFileSystemReadResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformFileSystemReadResult(false, PlatformFileSystemStatus.FirmwareServicesUnavailable, guardResult.Mode, 0, 0, 0, 0, 0);
    }

    public static PlatformFileSystemReadResult InvalidPath(FirmwareCallResult guardResult, int pathLength, int availableBytes)
    {
        return new PlatformFileSystemReadResult(false, PlatformFileSystemStatus.InvalidPath, guardResult.Mode, pathLength, 0, 0, availableBytes, 0);
    }

    private static PlatformFileSystemReadResult SourceFailure(FirmwareCallResult guardResult, int pathLength, PlatformFileSystemSourceReadResult sourceResult, int availableBytes)
    {
        PlatformFileSystemStatus status = sourceResult.Status switch
        {
            PlatformFileSystemSourceStatus.DestinationTooSmall => PlatformFileSystemStatus.DestinationTooSmall,
            PlatformFileSystemSourceStatus.Unavailable => PlatformFileSystemStatus.SourceUnavailable,
            PlatformFileSystemSourceStatus.NotFound => PlatformFileSystemStatus.NotFound,
            _ => PlatformFileSystemStatus.SourceFailed
        };
        return new PlatformFileSystemReadResult(false, status, guardResult.Mode, pathLength, sourceResult.BytesRead, sourceResult.RequiredBytes, availableBytes, sourceResult.FirmwareStatus);
    }

    public static bool IsSafeRelativeAsciiPath(ReadOnlySpan<byte> relativePath)
    {
        if (relativePath.IsEmpty || relativePath.Length > MaximumPathLength)
        {
            return false;
        }

        if (IsSeparator(relativePath[0]))
        {
            return false;
        }

        int segmentLength = 0;
        int dotsInSegment = 0;
        for (int index = 0; index < relativePath.Length; index++)
        {
            byte value = relativePath[index];
            if (value < 0x21 || value > 0x7e || value == (byte)':')
            {
                return false;
            }

            if (IsSeparator(value))
            {
                if (IsUnsafeSegment(segmentLength, dotsInSegment))
                {
                    return false;
                }

                segmentLength = 0;
                dotsInSegment = 0;
                continue;
            }

            segmentLength++;
            if (value == (byte)'.')
            {
                dotsInSegment++;
            }
        }

        return !IsUnsafeSegment(segmentLength, dotsInSegment);
    }

    private static bool IsUnsafeSegment(int segmentLength, int dotsInSegment)
    {
        return segmentLength == 0 || (dotsInSegment == segmentLength && (segmentLength == 1 || segmentLength == 2));
    }

    private static bool IsSeparator(byte value)
    {
        return value == (byte)'/' || value == (byte)'\\';
    }
}
