using System.Runtime.InteropServices;
using OS.Kernel.Platform;

namespace OS.Boot;

public enum FirmwareEntropySeedStatus
{
    NotStarted = 0,
    Success,
    InvalidSource,
    InvalidRequestLength,
    ScratchTooSmall,
    DestinationTooSmall,
    AliasedBuffers,
    AlreadyPublished,
    InvalidPublication,
    FirmwareServicesUnavailable,
    SourceUnavailable,
    SourceFailed,
    WrongLength,
    InvalidSourceResult,
    ReadbackMismatch
}

// The bytes stay in caller-owned storage. This POD contains no seed or pointer.
public readonly struct FirmwareEntropySeedPublication
{
    internal FirmwareEntropySeedPublication(int retainedBytes) => RetainedBytes = retainedBytes;
    public int RetainedBytes { get; }
    public bool IsPublished => RetainedBytes is 32 or 64;
}

public readonly struct FirmwareEntropySeedResult
{
    internal FirmwareEntropySeedResult(FirmwareEntropySeedStatus status, int requestedBytes,
        PlatformRandomResult sourceResult = default)
    {
        Status = status;
        RequestedBytes = requestedBytes;
        SourceResult = sourceResult;
    }

    public FirmwareEntropySeedStatus Status { get; }
    public int RequestedBytes { get; }
    public PlatformRandomResult SourceResult { get; }
    public bool Succeeded => Status == FirmwareEntropySeedStatus.Success;
}

/// <summary>
/// Retains one 32- or 64-byte guarded firmware-source result before shutdown.
/// Both complete buffer spans and the publication must be disjoint and stable
/// throughout the call. The provider must obey its bounded synchronous span
/// contract and return normally; exceptions, hostile providers and concurrent
/// mutation are not contained here. Use GuardedPlatformRandom for firmware.
/// Admission/source/pre-copy failures leave destination and publication intact;
/// scratch can change on source failure. A late readback failure does not roll
/// back copied bytes, but cannot publish success. Success clears the requested
/// scratch slice before publishing; this is not a physical secure-erasure claim.
/// No entropy-quality test, fallback, retry, pool or post-firmware call is supplied.
/// </summary>
public static class AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown
{
    public const int PayloadCapacity = 64;

    public static FirmwareEntropySeedResult Capture(IPlatformRandom? source, int requestedBytes,
        Span<byte> scratch, Span<byte> destination, ref FirmwareEntropySeedPublication publication)
    {
        if (publication.RetainedBytes != 0)
            return Fail(publication.IsPublished ? FirmwareEntropySeedStatus.AlreadyPublished
                : FirmwareEntropySeedStatus.InvalidPublication, requestedBytes);
        if (source is null)
            return Fail(FirmwareEntropySeedStatus.InvalidSource, requestedBytes);
        if (requestedBytes is not (32 or 64))
            return Fail(FirmwareEntropySeedStatus.InvalidRequestLength, requestedBytes);
        if (scratch.Length < PayloadCapacity)
            return Fail(FirmwareEntropySeedStatus.ScratchTooSmall, requestedBytes);
        if (destination.Length < PayloadCapacity)
            return Fail(FirmwareEntropySeedStatus.DestinationTooSmall, requestedBytes);

        Span<byte> publicationBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref publication, 1));
        if (scratch.Overlaps(destination) || scratch.Overlaps(publicationBytes)
            || destination.Overlaps(publicationBytes))
            return Fail(FirmwareEntropySeedStatus.AliasedBuffers, requestedBytes);

        Span<byte> requestedScratch = scratch.Slice(0, requestedBytes);
        PlatformRandomResult result = source.FillBytes(requestedScratch);
        if (!result.Succeeded)
        {
            FirmwareEntropySeedStatus status = result.Status switch
            {
                PlatformRandomStatus.FirmwareServicesUnavailable => FirmwareEntropySeedStatus.FirmwareServicesUnavailable,
                PlatformRandomStatus.SourceUnavailable => FirmwareEntropySeedStatus.SourceUnavailable,
                PlatformRandomStatus.SourceFailed => FirmwareEntropySeedStatus.SourceFailed,
                PlatformRandomStatus.ShortRead => FirmwareEntropySeedStatus.WrongLength,
                _ => FirmwareEntropySeedStatus.InvalidSourceResult
            };
            return new(status, requestedBytes, result);
        }
        if (result.Status != PlatformRandomStatus.Success
            || result.Mode != FirmwareServicesMode.BootServicesAvailable)
            return new(FirmwareEntropySeedStatus.InvalidSourceResult, requestedBytes, result);
        if (result.RequestedBytes != requestedBytes || result.BytesWritten != requestedBytes)
            return new(FirmwareEntropySeedStatus.WrongLength, requestedBytes, result);

        requestedScratch.CopyTo(destination);
        for (int i = 0; i < requestedBytes; i++)
            if (destination[i] != requestedScratch[i])
                return new(FirmwareEntropySeedStatus.ReadbackMismatch, requestedBytes, result);

        requestedScratch.Clear();
        publication = new FirmwareEntropySeedPublication(requestedBytes);
        return new(FirmwareEntropySeedStatus.Success, requestedBytes, result);
    }

    private static FirmwareEntropySeedResult Fail(FirmwareEntropySeedStatus status, int requestedBytes) =>
        new(status, requestedBytes);
}
