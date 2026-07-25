using OS.Kernel.Runtime;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public enum DiagnosticStubInvocationMarkerFormatStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    InvalidStatus = 2,
    MarkerTooLong = 3
}

public readonly struct DiagnosticStubInvocationMarkerFormatResult
{
    private const uint InitializedFlag = 1;

    public DiagnosticStubInvocationMarkerFormatResult(
        DiagnosticStubInvocationMarkerFormatStatus status,
        int bytesWritten,
        int requiredBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public DiagnosticStubInvocationMarkerFormatStatus Status { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == DiagnosticStubInvocationMarkerFormatStatus.Success &&
        RequiredBytes != 0 &&
        BytesWritten == RequiredBytes;
}

public static class DiagnosticStubInvocationReadinessFormatter
{
    public const int MaximumMarkerBytes = 512;

    private static ReadOnlySpan<byte> ReadyMarker => "DIAGNOSTIC_STUB_INVOCATION_READY"u8;
    private static ReadOnlySpan<byte> NotReadyPrefix => "DIAGNOSTIC_STUB_INVOCATION_NOT_READY "u8;

    public static DiagnosticStubInvocationMarkerFormatResult TryFormatReadyMarker(
        Span<byte> destination)
    {
        int requiredBytes = ReadyMarker.Length;
        if (destination.Length < requiredBytes)
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        AsciiTextBuilderResult writeResult =
            AsciiTextBuilder.AppendBytes(destination, 0, ReadyMarker);
        if (!writeResult.Succeeded)
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        return new DiagnosticStubInvocationMarkerFormatResult(
            DiagnosticStubInvocationMarkerFormatStatus.Success,
            requiredBytes,
            requiredBytes);
    }

    public static DiagnosticStubInvocationMarkerFormatResult TryFormatNotReadyMarker(
        DiagnosticStubInvocationReadinessResult readiness,
        Span<byte> destination)
    {
        DiagnosticStubInvocationMarkerFormatStatus validation = Validate(readiness);
        if (validation != DiagnosticStubInvocationMarkerFormatStatus.Success)
        {
            return new DiagnosticStubInvocationMarkerFormatResult(validation, 0, 0);
        }

        int requiredBytes = CountNotReadyMarkerBytes(readiness);
        if (requiredBytes > MaximumMarkerBytes)
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.MarkerTooLong,
                0,
                requiredBytes);
        }

        if (destination.Length < requiredBytes)
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        int offset = 0;
        Write(NotReadyPrefix, destination, ref offset);
        Write("status="u8, destination, ref offset);
        Write(GetName(readiness.Status), destination, ref offset);
        Write(" initialized="u8, destination, ref offset);
        Write(readiness.IsInitialized ? "true"u8 : "false"u8, destination, ref offset);
        Write(" pageTransition="u8, destination, ref offset);
        Write(GetName(readiness.PageTransitionStatus), destination, ref offset);
        Write(" backend="u8, destination, ref offset);
        Write(GetName(readiness.BackendContractStatus), destination, ref offset);
        Write(" payloadBase="u8, destination, ref offset);
        if (!TryWriteUInt64Decimal((ulong)readiness.PayloadBaseAddress, destination, ref offset))
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        Write(" payloadEnd="u8, destination, ref offset);
        if (!TryWriteUInt64Decimal((ulong)readiness.PayloadEndAddress, destination, ref offset))
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        Write(" bytes="u8, destination, ref offset);
        if (!TryWriteUInt64Decimal((ulong)readiness.BytesMaterialized, destination, ref offset))
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        Write(" out8="u8, destination, ref offset);
        if (!TryWriteUInt64Decimal((ulong)readiness.Out8StubAddress, destination, ref offset))
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        Write(" in8="u8, destination, ref offset);
        if (!TryWriteUInt64Decimal((ulong)readiness.In8StubAddress, destination, ref offset))
        {
            return new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.DestinationTooSmall,
                0,
                requiredBytes);
        }

        return new DiagnosticStubInvocationMarkerFormatResult(
            DiagnosticStubInvocationMarkerFormatStatus.Success,
            offset,
            requiredBytes);
    }

    public static DiagnosticStubInvocationMarkerFormatResult QueryNotReadyMarkerRequiredBytes(
        DiagnosticStubInvocationReadinessResult readiness)
    {
        DiagnosticStubInvocationMarkerFormatStatus validation = Validate(readiness);
        if (validation != DiagnosticStubInvocationMarkerFormatStatus.Success)
        {
            return new DiagnosticStubInvocationMarkerFormatResult(validation, 0, 0);
        }

        int requiredBytes = CountNotReadyMarkerBytes(readiness);
        return requiredBytes > MaximumMarkerBytes
            ? new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.MarkerTooLong,
                0,
                requiredBytes)
            : new DiagnosticStubInvocationMarkerFormatResult(
                DiagnosticStubInvocationMarkerFormatStatus.Success,
                0,
                requiredBytes);
    }

    private static DiagnosticStubInvocationMarkerFormatStatus Validate(
        DiagnosticStubInvocationReadinessResult readiness)
    {
        return GetName(readiness.Status).Length != 0 &&
            GetName(readiness.PageTransitionStatus).Length != 0 &&
            GetName(readiness.BackendContractStatus).Length != 0
                ? DiagnosticStubInvocationMarkerFormatStatus.Success
                : DiagnosticStubInvocationMarkerFormatStatus.InvalidStatus;
    }

    private static int CountNotReadyMarkerBytes(DiagnosticStubInvocationReadinessResult readiness)
    {
        return NotReadyPrefix.Length
            + "status="u8.Length
            + GetName(readiness.Status).Length
            + " initialized="u8.Length
            + (readiness.IsInitialized ? "true"u8.Length : "false"u8.Length)
            + " pageTransition="u8.Length
            + GetName(readiness.PageTransitionStatus).Length
            + " backend="u8.Length
            + GetName(readiness.BackendContractStatus).Length
            + " payloadBase="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.PayloadBaseAddress)
            + " payloadEnd="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.PayloadEndAddress)
            + " bytes="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.BytesMaterialized)
            + " out8="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.Out8StubAddress)
            + " in8="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits((ulong)readiness.In8StubAddress);
    }

    private static ReadOnlySpan<byte> GetName(DiagnosticStubInvocationReadinessStatus status)
    {
        return status switch
        {
            DiagnosticStubInvocationReadinessStatus.Ready => "Ready"u8,
            DiagnosticStubInvocationReadinessStatus.PageTransitionNotReady => "PageTransitionNotReady"u8,
            DiagnosticStubInvocationReadinessStatus.BackendContractNotReady => "BackendContractNotReady"u8,
            DiagnosticStubInvocationReadinessStatus.Out8AddressOutsideRegion => "Out8AddressOutsideRegion"u8,
            DiagnosticStubInvocationReadinessStatus.In8AddressOutsideRegion => "In8AddressOutsideRegion"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(ExecutableStubPageTransitionPlanStatus status)
    {
        return status switch
        {
            ExecutableStubPageTransitionPlanStatus.Success => "Success"u8,
            ExecutableStubPageTransitionPlanStatus.InvalidMemoryPlan => "InvalidMemoryPlan"u8,
            ExecutableStubPageTransitionPlanStatus.UnmaterializedPayload => "UnmaterializedPayload"u8,
            ExecutableStubPageTransitionPlanStatus.UnsafeProtectionState => "UnsafeProtectionState"u8,
            ExecutableStubPageTransitionPlanStatus.InvalidRange => "InvalidRange"u8,
            ExecutableStubPageTransitionPlanStatus.RangeOverflow => "RangeOverflow"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(Com1PortIoBackendContractStatus status)
    {
        return status switch
        {
            Com1PortIoBackendContractStatus.Success => "Success"u8,
            Com1PortIoBackendContractStatus.BackendReadinessFailed => "BackendReadinessFailed"u8,
            Com1PortIoBackendContractStatus.StubBundlePlanFailed => "StubBundlePlanFailed"u8,
            Com1PortIoBackendContractStatus.MissingOut8StubSlot => "MissingOut8StubSlot"u8,
            Com1PortIoBackendContractStatus.MissingIn8StubSlot => "MissingIn8StubSlot"u8,
            Com1PortIoBackendContractStatus.InvalidPayloadBaseAddress => "InvalidPayloadBaseAddress"u8,
            Com1PortIoBackendContractStatus.AddressOverflow => "AddressOverflow"u8,
            Com1PortIoBackendContractStatus.InvalidCom1BasePort => "InvalidCom1BasePort"u8,
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

    private static bool TryWriteUInt64Decimal(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
