using OS.Std.Text;

namespace OS.Kernel.Runtime;
public static class NativeAotClassConstructorStateDiagnosticFormatter
{
    public static NativeAotClassConstructorStateDiagnosticFormatResult FormatResult(in NativeAotClassConstructorStateTableResult result, Span<byte> destination)
    {
        NativeAotClassConstructorStateDiagnosticFormatStatus validation = Validate(in result);
        if (validation != NativeAotClassConstructorStateDiagnosticFormatStatus.Success)
        {
            return NativeAotClassConstructorStateDiagnosticFormatResult.Fail(validation, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return NativeAotClassConstructorStateDiagnosticFormatResult.Fail(NativeAotClassConstructorStateDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("NATIVEAOT_CCTOR_STATE status="u8, destination, ref offset) || !TryAppendBytes(GetTableStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" moduleId="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.Key.ModuleId, destination, ref offset) || !TryAppendBytes(" typeId="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.Key.TypeId, destination, ref offset) || !TryAppendBytes(" previousState="u8, destination, ref offset) || !TryAppendBytes(GetStateName(result.PreviousState), destination, ref offset) || !TryAppendBytes(" state="u8, destination, ref offset) || !TryAppendBytes(GetStateName(result.State), destination, ref offset) || !TryAppendBytes(" failureCode="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.FailureCode, destination, ref offset) || !TryAppendBytes(" slotIndex="u8, destination, ref offset) || !TryAppendInt64Decimal(result.SlotIndex, destination, ref offset) || !TryAppendBytes(" duplicateSlotIndex="u8, destination, ref offset) || !TryAppendInt64Decimal(result.DuplicateSlotIndex, destination, ref offset) || !TryAppendBytes(" occupiedCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.OccupiedCount, destination, ref offset) || !TryAppendBytes(" capacity="u8, destination, ref offset) || !TryAppendInt64Decimal(result.Capacity, destination, ref offset))
        {
            return NativeAotClassConstructorStateDiagnosticFormatResult.Fail(NativeAotClassConstructorStateDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new NativeAotClassConstructorStateDiagnosticFormatResult(NativeAotClassConstructorStateDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static NativeAotClassConstructorStateDiagnosticFormatResult QueryRequiredBytes(in NativeAotClassConstructorStateTableResult result)
    {
        NativeAotClassConstructorStateDiagnosticFormatStatus validation = Validate(in result);
        if (validation != NativeAotClassConstructorStateDiagnosticFormatStatus.Success)
        {
            return NativeAotClassConstructorStateDiagnosticFormatResult.Fail(validation, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new NativeAotClassConstructorStateDiagnosticFormatResult(NativeAotClassConstructorStateDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static NativeAotClassConstructorStateDiagnosticFormatStatus Validate(in NativeAotClassConstructorStateTableResult result)
    {
        if (!result.IsInitialized || GetTableStatusName(result.Status).Length == 0 || GetStateName(result.PreviousState).Length == 0 || GetStateName(result.State).Length == 0 || result.SlotIndex < -1 || result.DuplicateSlotIndex < -1 || result.OccupiedCount < 0 || result.Capacity < 0 || result.OccupiedCount > result.Capacity)
        {
            return NativeAotClassConstructorStateDiagnosticFormatStatus.InvalidResult;
        }

        return NativeAotClassConstructorStateDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in NativeAotClassConstructorStateTableResult result)
    {
        return "NATIVEAOT_CCTOR_STATE status="u8.Length + GetTableStatusName(result.Status).Length + " moduleId="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.Key.ModuleId) + " typeId="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.Key.TypeId) + " previousState="u8.Length + GetStateName(result.PreviousState).Length + " state="u8.Length + GetStateName(result.State).Length + " failureCode="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.FailureCode) + " slotIndex="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.SlotIndex) + " duplicateSlotIndex="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.DuplicateSlotIndex) + " occupiedCount="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.OccupiedCount) + " capacity="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.Capacity);
    }

    private static ReadOnlySpan<byte> GetTableStatusName(NativeAotClassConstructorStateTableStatus status)
    {
        return status switch
        {
            NativeAotClassConstructorStateTableStatus.Success => "Success"u8,
            NativeAotClassConstructorStateTableStatus.InvalidKey => "InvalidKey"u8,
            NativeAotClassConstructorStateTableStatus.MissingKey => "MissingKey"u8,
            NativeAotClassConstructorStateTableStatus.DuplicateKey => "DuplicateKey"u8,
            NativeAotClassConstructorStateTableStatus.CapacityExceeded => "CapacityExceeded"u8,
            NativeAotClassConstructorStateTableStatus.ReentryCycle => "ReentryCycle"u8,
            NativeAotClassConstructorStateTableStatus.PreviouslyFailed => "PreviouslyFailed"u8,
            NativeAotClassConstructorStateTableStatus.AlreadyInitialized => "AlreadyInitialized"u8,
            NativeAotClassConstructorStateTableStatus.NotInitializing => "NotInitializing"u8,
            NativeAotClassConstructorStateTableStatus.FailureCodeRequired => "FailureCodeRequired"u8,
            NativeAotClassConstructorStateTableStatus.InvalidState => "InvalidState"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetStateName(NativeAotClassConstructorState state)
    {
        return state switch
        {
            NativeAotClassConstructorState.Uninitialized => "Uninitialized"u8,
            NativeAotClassConstructorState.Initializing => "Initializing"u8,
            NativeAotClassConstructorState.Initialized => "Initialized"u8,
            NativeAotClassConstructorState.Failed => "Failed"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
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
}
