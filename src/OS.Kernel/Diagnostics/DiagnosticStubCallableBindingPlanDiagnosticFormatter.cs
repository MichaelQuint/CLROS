using OS.Kernel.Runtime;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;
public static class DiagnosticStubCallableBindingPlanDiagnosticFormatter
{
    public const bool UsesSharedAsciiTextBuilder = true;
    public const bool UsesCallerProvidedDestination = true;
    public const bool AllocatesManagedStorage = false;
    public const bool CreatesManagedDelegate = false;
    public const bool InvokesCallable = false;
    public const bool PerformsPortIo = false;
    public const bool WritesSerial = false;
    public const bool StartsKestrel = false;
    private static ReadOnlySpan<byte> MarkerPrefix => "DIAGNOSTIC_STUB_CALLABLE_BINDING status="u8;

    public static DiagnosticStubCallableBindingPlanDiagnosticFormatResult QueryRequiredBytes(DiagnosticStubCallableBindingPlanResult plan)
    {
        DiagnosticStubCallableBindingPlanDiagnosticFormatStatus validation = Validate(plan);
        if (validation != DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success)
        {
            return DiagnosticStubCallableBindingPlanDiagnosticFormatResult.Fail(validation, requiredBytes: 0, availableBytes: 0);
        }

        return new DiagnosticStubCallableBindingPlanDiagnosticFormatResult(DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success, bytesWritten: 0, requiredBytes: CountMarkerBytes(plan), availableBytes: 0);
    }

    public static DiagnosticStubCallableBindingPlanDiagnosticFormatResult FormatResult(DiagnosticStubCallableBindingPlanResult plan, Span<byte> destination)
    {
        DiagnosticStubCallableBindingPlanDiagnosticFormatStatus validation = Validate(plan);
        if (validation != DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success)
        {
            return DiagnosticStubCallableBindingPlanDiagnosticFormatResult.Fail(validation, requiredBytes: 0, availableBytes: destination.Length);
        }

        int requiredBytes = CountMarkerBytes(plan);
        if (destination.Length < requiredBytes)
        {
            return DiagnosticStubCallableBindingPlanDiagnosticFormatResult.Fail(DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.DestinationTooSmall, requiredBytes, destination.Length);
        }

        int offset = 0;
        if (!TryAppendMarker(plan, destination, ref offset))
        {
            return DiagnosticStubCallableBindingPlanDiagnosticFormatResult.Fail(DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.DestinationTooSmall, requiredBytes, destination.Length);
        }

        return new DiagnosticStubCallableBindingPlanDiagnosticFormatResult(DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success, offset, requiredBytes, destination.Length);
    }

    private static DiagnosticStubCallableBindingPlanDiagnosticFormatStatus Validate(DiagnosticStubCallableBindingPlanResult plan)
    {
        if (!plan.IsInitialized || GetName(plan.Status).Length == 0 || GetName(plan.InvocationStatus).Length == 0 || GetName(plan.PageTransitionStatus).Length == 0 || GetName(plan.BackendContractStatus).Length == 0 || GetName(plan.MaterializerStatus).Length == 0)
        {
            return DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.InvalidResult;
        }

        return DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success;
    }

    private static int CountMarkerBytes(DiagnosticStubCallableBindingPlanResult plan)
    {
        return MarkerPrefix.Length + GetName(plan.Status).Length + " initialized="u8.Length + GetBoolName(plan.IsInitialized).Length + " invocation="u8.Length + GetName(plan.InvocationStatus).Length + " pageTransition="u8.Length + GetName(plan.PageTransitionStatus).Length + " backend="u8.Length + GetName(plan.BackendContractStatus).Length + " materializer="u8.Length + GetName(plan.MaterializerStatus).Length + " basePort="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(plan.ConfiguredBasePort) + " payloadBase="u8.Length + AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.PayloadBaseAddress) + " payloadEnd="u8.Length + AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.PayloadEndAddress) + " payloadBytes="u8.Length + AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.PayloadByteLength) + " bytes="u8.Length + AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.BytesMaterialized) + " out8Offset="u8.Length + AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.Out8StubOffset) + " in8Offset="u8.Length + AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.In8StubOffset) + " out8="u8.Length + AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.Out8StubAddress) + " in8="u8.Length + AsciiDecimalFormatter.CountUInt64Digits((ulong)plan.In8StubAddress) + " out8Bytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(plan.Out8BytesCopied) + " in8Bytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(plan.In8BytesCopied) + " createsDelegate="u8.Length + GetBoolName(plan.CreatesManagedDelegate).Length + " invokesCallable="u8.Length + GetBoolName(plan.InvokesCallable).Length + " performsPortIo="u8.Length + GetBoolName(plan.PerformsPortIo).Length + " writesSerial="u8.Length + GetBoolName(plan.WritesSerial).Length;
    }

    private static bool TryAppendMarker(DiagnosticStubCallableBindingPlanResult plan, Span<byte> destination, ref int offset)
    {
        return TryAppendBytes(MarkerPrefix, destination, ref offset) && TryAppendBytes(GetName(plan.Status), destination, ref offset) && TryAppendBytes(" initialized="u8, destination, ref offset) && TryAppendBytes(GetBoolName(plan.IsInitialized), destination, ref offset) && TryAppendBytes(" invocation="u8, destination, ref offset) && TryAppendBytes(GetName(plan.InvocationStatus), destination, ref offset) && TryAppendBytes(" pageTransition="u8, destination, ref offset) && TryAppendBytes(GetName(plan.PageTransitionStatus), destination, ref offset) && TryAppendBytes(" backend="u8, destination, ref offset) && TryAppendBytes(GetName(plan.BackendContractStatus), destination, ref offset) && TryAppendBytes(" materializer="u8, destination, ref offset) && TryAppendBytes(GetName(plan.MaterializerStatus), destination, ref offset) && TryAppendBytes(" basePort="u8, destination, ref offset) && TryAppendUInt64Decimal(plan.ConfiguredBasePort, destination, ref offset) && TryAppendBytes(" payloadBase="u8, destination, ref offset) && TryAppendUInt64Decimal((ulong)plan.PayloadBaseAddress, destination, ref offset) && TryAppendBytes(" payloadEnd="u8, destination, ref offset) && TryAppendUInt64Decimal((ulong)plan.PayloadEndAddress, destination, ref offset) && TryAppendBytes(" payloadBytes="u8, destination, ref offset) && TryAppendUInt64Decimal((ulong)plan.PayloadByteLength, destination, ref offset) && TryAppendBytes(" bytes="u8, destination, ref offset) && TryAppendUInt64Decimal((ulong)plan.BytesMaterialized, destination, ref offset) && TryAppendBytes(" out8Offset="u8, destination, ref offset) && TryAppendUInt64Decimal((ulong)plan.Out8StubOffset, destination, ref offset) && TryAppendBytes(" in8Offset="u8, destination, ref offset) && TryAppendUInt64Decimal((ulong)plan.In8StubOffset, destination, ref offset) && TryAppendBytes(" out8="u8, destination, ref offset) && TryAppendUInt64Decimal((ulong)plan.Out8StubAddress, destination, ref offset) && TryAppendBytes(" in8="u8, destination, ref offset) && TryAppendUInt64Decimal((ulong)plan.In8StubAddress, destination, ref offset) && TryAppendBytes(" out8Bytes="u8, destination, ref offset) && TryAppendInt64Decimal(plan.Out8BytesCopied, destination, ref offset) && TryAppendBytes(" in8Bytes="u8, destination, ref offset) && TryAppendInt64Decimal(plan.In8BytesCopied, destination, ref offset) && TryAppendBytes(" createsDelegate="u8, destination, ref offset) && TryAppendBytes(GetBoolName(plan.CreatesManagedDelegate), destination, ref offset) && TryAppendBytes(" invokesCallable="u8, destination, ref offset) && TryAppendBytes(GetBoolName(plan.InvokesCallable), destination, ref offset) && TryAppendBytes(" performsPortIo="u8, destination, ref offset) && TryAppendBytes(GetBoolName(plan.PerformsPortIo), destination, ref offset) && TryAppendBytes(" writesSerial="u8, destination, ref offset) && TryAppendBytes(GetBoolName(plan.WritesSerial), destination, ref offset);
    }

    private static ReadOnlySpan<byte> GetName(DiagnosticStubCallableBindingPlanStatus status)
    {
        return status switch
        {
            DiagnosticStubCallableBindingPlanStatus.Ready => "Ready"u8,
            DiagnosticStubCallableBindingPlanStatus.InvocationReadinessNotReady => "InvocationReadinessNotReady"u8,
            DiagnosticStubCallableBindingPlanStatus.BackendContractNotReady => "BackendContractNotReady"u8,
            DiagnosticStubCallableBindingPlanStatus.BackendContractMismatch => "BackendContractMismatch"u8,
            DiagnosticStubCallableBindingPlanStatus.MaterializerNotReady => "MaterializerNotReady"u8,
            DiagnosticStubCallableBindingPlanStatus.Out8BytesNotMaterialized => "Out8BytesNotMaterialized"u8,
            DiagnosticStubCallableBindingPlanStatus.In8BytesNotMaterialized => "In8BytesNotMaterialized"u8,
            DiagnosticStubCallableBindingPlanStatus.Out8CallableOutsidePayload => "Out8CallableOutsidePayload"u8,
            DiagnosticStubCallableBindingPlanStatus.In8CallableOutsidePayload => "In8CallableOutsidePayload"u8,
            DiagnosticStubCallableBindingPlanStatus.CallableRangesOverlap => "CallableRangesOverlap"u8,
            DiagnosticStubCallableBindingPlanStatus.InvalidCom1BasePort => "InvalidCom1BasePort"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
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

    private static ReadOnlySpan<byte> GetName(ExecutableStubBundleMaterializerStatus status)
    {
        return status switch
        {
            ExecutableStubBundleMaterializerStatus.Success => "Success"u8,
            ExecutableStubBundleMaterializerStatus.InvalidBundlePlan => "InvalidBundlePlan"u8,
            ExecutableStubBundleMaterializerStatus.InvalidMemoryPlan => "InvalidMemoryPlan"u8,
            ExecutableStubBundleMaterializerStatus.InconsistentMemoryPlan => "InconsistentMemoryPlan"u8,
            ExecutableStubBundleMaterializerStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            ExecutableStubBundleMaterializerStatus.MissingSlot => "MissingSlot"u8,
            ExecutableStubBundleMaterializerStatus.SourceTooSmall => "SourceTooSmall"u8,
            ExecutableStubBundleMaterializerStatus.SlotOutOfBounds => "SlotOutOfBounds"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetBoolName(bool value)
    {
        return value ? "true"u8 : "false"u8;
    }

    private static bool TryAppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendUInt64Decimal(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendInt64Decimal(long value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
