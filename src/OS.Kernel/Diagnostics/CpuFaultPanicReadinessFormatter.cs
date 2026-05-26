using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public enum CpuFaultPanicReadinessMarkerFormatStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    InvalidStatus = 2,
    MarkerTooLong = 3
}

public readonly struct CpuFaultPanicReadinessMarkerFormatResult
{
    private const uint InitializedFlag = 1;

    public CpuFaultPanicReadinessMarkerFormatResult(
        CpuFaultPanicReadinessMarkerFormatStatus status,
        int bytesWritten,
        int requiredBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public CpuFaultPanicReadinessMarkerFormatStatus Status { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == CpuFaultPanicReadinessMarkerFormatStatus.Success &&
        RequiredBytes != 0 &&
        BytesWritten == RequiredBytes;
}

public static class CpuFaultPanicReadinessFormatter
{
    public const int MaximumMarkerBytes = 512;

    private static ReadOnlySpan<byte> ReadyMarker => "CPU_FAULT_PANIC_READY"u8;
    private static ReadOnlySpan<byte> NotReadyPrefix => "CPU_FAULT_PANIC_NOT_READY "u8;
    private static ReadOnlySpan<byte> PanicReadinessFailedName => "PanicReadinessFailed"u8;

    public static CpuFaultPanicReadinessMarkerFormatResult TryFormatReadyMarker(
        Span<byte> destination)
    {
        int requiredBytes = ReadyMarker.Length;
        if (destination.Length < requiredBytes)
        {
            return new CpuFaultPanicReadinessMarkerFormatResult(
                CpuFaultPanicReadinessMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        AsciiTextBuilderResult writeResult =
            AsciiTextBuilder.AppendBytes(destination, 0, ReadyMarker);
        if (!writeResult.Succeeded)
        {
            return new CpuFaultPanicReadinessMarkerFormatResult(
                CpuFaultPanicReadinessMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        return new CpuFaultPanicReadinessMarkerFormatResult(
            CpuFaultPanicReadinessMarkerFormatStatus.Success,
            requiredBytes,
            requiredBytes);
    }

    public static CpuFaultPanicReadinessMarkerFormatResult TryFormatNotReadyMarker(
        CpuFaultPanicReadinessResult readiness,
        Span<byte> destination)
    {
        CpuFaultPanicReadinessMarkerFormatStatus validation = Validate(readiness);
        if (validation != CpuFaultPanicReadinessMarkerFormatStatus.Success)
        {
            return new CpuFaultPanicReadinessMarkerFormatResult(validation, 0, 0);
        }

        int requiredBytes = CountNotReadyMarkerBytes(readiness);
        if (requiredBytes > MaximumMarkerBytes)
        {
            return new CpuFaultPanicReadinessMarkerFormatResult(
                CpuFaultPanicReadinessMarkerFormatStatus.MarkerTooLong,
                0,
                requiredBytes);
        }

        if (destination.Length < requiredBytes)
        {
            return new CpuFaultPanicReadinessMarkerFormatResult(
                CpuFaultPanicReadinessMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        int offset = 0;
        Write(NotReadyPrefix, destination, ref offset);
        Write("status="u8, destination, ref offset);
        Write(PanicReadinessFailedName, destination, ref offset);
        Write(" initialized="u8, destination, ref offset);
        Write(readiness.IsInitialized ? "true"u8 : "false"u8, destination, ref offset);
        Write(" readiness="u8, destination, ref offset);
        Write(GetName(readiness.Status), destination, ref offset);
        Write(" frame="u8, destination, ref offset);
        Write(GetName(readiness.FrameStatus), destination, ref offset);
        Write(" reporter="u8, destination, ref offset);
        Write(GetName(readiness.ReporterStatus), destination, ref offset);
        Write(" vector="u8, destination, ref offset);
        WriteSigned(readiness.Vector, destination, ref offset);
        Write(" requiredFrameBytes="u8, destination, ref offset);
        WriteSigned(readiness.RequiredFrameBytes, destination, ref offset);
        Write(" formatted="u8, destination, ref offset);
        WriteSigned(readiness.BytesFormatted, destination, ref offset);
        Write(" flushed="u8, destination, ref offset);
        WriteSigned(readiness.BytesFlushed, destination, ref offset);

        return new CpuFaultPanicReadinessMarkerFormatResult(
            CpuFaultPanicReadinessMarkerFormatStatus.Success,
            offset,
            requiredBytes);
    }

    public static CpuFaultPanicReadinessMarkerFormatResult QueryNotReadyMarkerRequiredBytes(
        CpuFaultPanicReadinessResult readiness)
    {
        CpuFaultPanicReadinessMarkerFormatStatus validation = Validate(readiness);
        if (validation != CpuFaultPanicReadinessMarkerFormatStatus.Success)
        {
            return new CpuFaultPanicReadinessMarkerFormatResult(validation, 0, 0);
        }

        int requiredBytes = CountNotReadyMarkerBytes(readiness);
        return requiredBytes > MaximumMarkerBytes
            ? new CpuFaultPanicReadinessMarkerFormatResult(
                CpuFaultPanicReadinessMarkerFormatStatus.MarkerTooLong,
                0,
                requiredBytes)
            : new CpuFaultPanicReadinessMarkerFormatResult(
                CpuFaultPanicReadinessMarkerFormatStatus.Success,
                0,
                requiredBytes);
    }

    private static CpuFaultPanicReadinessMarkerFormatStatus Validate(
        CpuFaultPanicReadinessResult readiness)
    {
        return GetName(readiness.Status).Length != 0 &&
            GetName(readiness.FrameStatus).Length != 0 &&
            GetName(readiness.ReporterStatus).Length != 0
                ? CpuFaultPanicReadinessMarkerFormatStatus.Success
                : CpuFaultPanicReadinessMarkerFormatStatus.InvalidStatus;
    }

    private static int CountNotReadyMarkerBytes(CpuFaultPanicReadinessResult readiness)
    {
        return NotReadyPrefix.Length
            + "status="u8.Length
            + PanicReadinessFailedName.Length
            + " initialized="u8.Length
            + (readiness.IsInitialized ? "true"u8.Length : "false"u8.Length)
            + " readiness="u8.Length
            + GetName(readiness.Status).Length
            + " frame="u8.Length
            + GetName(readiness.FrameStatus).Length
            + " reporter="u8.Length
            + GetName(readiness.ReporterStatus).Length
            + " vector="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(readiness.Vector)
            + " requiredFrameBytes="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(readiness.RequiredFrameBytes)
            + " formatted="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(readiness.BytesFormatted)
            + " flushed="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(readiness.BytesFlushed);
    }

    private static ReadOnlySpan<byte> GetName(CpuFaultPanicReadinessStatus status)
    {
        return status switch
        {
            CpuFaultPanicReadinessStatus.Ready => "Ready"u8,
            CpuFaultPanicReadinessStatus.FaultFrameInvalid => "FaultFrameInvalid"u8,
            CpuFaultPanicReadinessStatus.PanicReportFailed => "PanicReportFailed"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(CpuFaultEntryFrameLayoutStatus status)
    {
        return status switch
        {
            CpuFaultEntryFrameLayoutStatus.Ok => "Ok"u8,
            CpuFaultEntryFrameLayoutStatus.BufferTooSmall => "BufferTooSmall"u8,
            CpuFaultEntryFrameLayoutStatus.UnknownVector => "UnknownVector"u8,
            CpuFaultEntryFrameLayoutStatus.InvalidHasErrorCodeFlag => "InvalidHasErrorCodeFlag"u8,
            CpuFaultEntryFrameLayoutStatus.InconsistentErrorCodeMetadata =>
                "InconsistentErrorCodeMetadata"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(PanicSerialReporterStatus status)
    {
        return status switch
        {
            PanicSerialReporterStatus.Ok => "Ok"u8,
            PanicSerialReporterStatus.BufferOverflow => "BufferOverflow"u8,
            PanicSerialReporterStatus.InvalidAscii => "InvalidAscii"u8,
            PanicSerialReporterStatus.SinkFailure => "SinkFailure"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static void Write(ReadOnlySpan<byte> source, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, source);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }

    private static void WriteSigned(int value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }
}
