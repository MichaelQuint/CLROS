using OS.Std.Text;

namespace OS.Kernel.Runtime;

public enum NativeAotClassConstructorRunnerPreflightStatus
{
    Ready = 0,
    NotInitialized = 1,
    InvalidTypeCount = 2,
    TypeCountExceedsCapacity = 3,
    TypeCountMismatch = 4,
    MissingTargetMetadata = 5,
    TargetMetadataCountMismatch = 6,
    PendingOrReentrantConstructor = 7,
    FailedConstructor = 8,
    InvalidState = 9
}

public readonly struct NativeAotClassConstructorRunnerPreflightResult
{
    private const uint InitializedFlag = 1;

    private NativeAotClassConstructorRunnerPreflightResult(
        uint flags,
        NativeAotClassConstructorRunnerPreflightStatus status,
        int typeCount,
        int targetMetadataCount,
        int occupiedCount,
        int capacity,
        int firstProblemIndex,
        NativeAotClassConstructorState firstProblemState,
        uint firstProblemFailureCode)
    {
        Flags = flags;
        Status = status;
        TypeCount = typeCount;
        TargetMetadataCount = targetMetadataCount;
        OccupiedCount = occupiedCount;
        Capacity = capacity;
        FirstProblemIndex = firstProblemIndex;
        FirstProblemState = firstProblemState;
        FirstProblemFailureCode = firstProblemFailureCode;
    }

    public uint Flags { get; }

    public NativeAotClassConstructorRunnerPreflightStatus Status { get; }

    public int TypeCount { get; }

    public int TargetMetadataCount { get; }

    public int OccupiedCount { get; }

    public int Capacity { get; }

    public int FirstProblemIndex { get; }

    public NativeAotClassConstructorState FirstProblemState { get; }

    public uint FirstProblemFailureCode { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == NativeAotClassConstructorRunnerPreflightStatus.Ready &&
        TypeCount > 0 &&
        TargetMetadataCount == TypeCount &&
        OccupiedCount == TypeCount &&
        Capacity >= TypeCount &&
        FirstProblemIndex == -1;

    internal static NativeAotClassConstructorRunnerPreflightResult Create(
        NativeAotClassConstructorRunnerPreflightStatus status,
        int typeCount,
        int targetMetadataCount,
        int occupiedCount,
        int capacity,
        int firstProblemIndex,
        NativeAotClassConstructorState firstProblemState,
        uint firstProblemFailureCode)
    {
        return new NativeAotClassConstructorRunnerPreflightResult(
            InitializedFlag,
            status,
            typeCount,
            targetMetadataCount,
            occupiedCount,
            capacity,
            firstProblemIndex,
            firstProblemState,
            firstProblemFailureCode);
    }
}

public enum NativeAotClassConstructorRunnerPreflightFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}

public readonly struct NativeAotClassConstructorRunnerPreflightFormatResult
{
    private const uint InitializedFlag = 1;

    public NativeAotClassConstructorRunnerPreflightFormatResult(
        NativeAotClassConstructorRunnerPreflightFormatStatus status,
        NativeAotClassConstructorRunnerPreflightStatus preflightStatus,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        PreflightStatus = preflightStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeAotClassConstructorRunnerPreflightFormatStatus Status { get; }

    public NativeAotClassConstructorRunnerPreflightStatus PreflightStatus { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == NativeAotClassConstructorRunnerPreflightFormatStatus.Success &&
        RequiredBytes > 0 &&
        (BytesWritten == 0 ||
            (BytesWritten == RequiredBytes &&
            RequiredBytes <= AvailableBytes));

    public static NativeAotClassConstructorRunnerPreflightFormatResult Fail(
        NativeAotClassConstructorRunnerPreflightFormatStatus status,
        NativeAotClassConstructorRunnerPreflightStatus preflightStatus,
        int requiredBytes,
        int availableBytes)
    {
        return new NativeAotClassConstructorRunnerPreflightFormatResult(
            status,
            preflightStatus,
            bytesWritten: 0,
            requiredBytes,
            availableBytes);
    }
}

public static class NativeAotClassConstructorRunnerPreflight
{
    public const bool ExecutesConstructors = false;
    public const bool UsesReflection = false;
    public const bool UsesExceptionControlFlow = false;
    public const bool RequiresTargetMetadata = true;

    public static NativeAotClassConstructorRunnerPreflightResult Validate(
        ReadOnlySpan<NativeAotClassConstructorStateEntry> stateEntries,
        int typeCount,
        int targetMetadataCount)
    {
        int occupiedCount = CountOccupied(stateEntries);

        if (stateEntries.Length == 0 || typeCount == 0 || targetMetadataCount == 0)
        {
            return Create(
                NativeAotClassConstructorRunnerPreflightStatus.NotInitialized,
                typeCount,
                targetMetadataCount,
                occupiedCount,
                stateEntries.Length);
        }

        if (typeCount < 0 || targetMetadataCount < 0)
        {
            return Create(
                NativeAotClassConstructorRunnerPreflightStatus.InvalidTypeCount,
                typeCount,
                targetMetadataCount,
                occupiedCount,
                stateEntries.Length);
        }

        if (typeCount > stateEntries.Length)
        {
            return Create(
                NativeAotClassConstructorRunnerPreflightStatus.TypeCountExceedsCapacity,
                typeCount,
                targetMetadataCount,
                occupiedCount,
                stateEntries.Length);
        }

        if (targetMetadataCount < typeCount)
        {
            return Create(
                NativeAotClassConstructorRunnerPreflightStatus.MissingTargetMetadata,
                typeCount,
                targetMetadataCount,
                occupiedCount,
                stateEntries.Length);
        }

        if (targetMetadataCount != typeCount)
        {
            return Create(
                NativeAotClassConstructorRunnerPreflightStatus.TargetMetadataCountMismatch,
                typeCount,
                targetMetadataCount,
                occupiedCount,
                stateEntries.Length);
        }

        if (occupiedCount != typeCount)
        {
            return Create(
                NativeAotClassConstructorRunnerPreflightStatus.TypeCountMismatch,
                typeCount,
                targetMetadataCount,
                occupiedCount,
                stateEntries.Length);
        }

        for (int index = 0; index < stateEntries.Length; index++)
        {
            NativeAotClassConstructorStateEntry entry = stateEntries[index];
            if (!entry.IsOccupied)
            {
                continue;
            }

            if (entry.State == NativeAotClassConstructorState.Initializing)
            {
                return Create(
                    NativeAotClassConstructorRunnerPreflightStatus.PendingOrReentrantConstructor,
                    typeCount,
                    targetMetadataCount,
                    occupiedCount,
                    stateEntries.Length,
                    index,
                    entry.State,
                    entry.FailureCode);
            }

            if (entry.State == NativeAotClassConstructorState.Failed)
            {
                return Create(
                    NativeAotClassConstructorRunnerPreflightStatus.FailedConstructor,
                    typeCount,
                    targetMetadataCount,
                    occupiedCount,
                    stateEntries.Length,
                    index,
                    entry.State,
                    entry.FailureCode);
            }

            if (entry.State != NativeAotClassConstructorState.Uninitialized &&
                entry.State != NativeAotClassConstructorState.Initialized)
            {
                return Create(
                    NativeAotClassConstructorRunnerPreflightStatus.InvalidState,
                    typeCount,
                    targetMetadataCount,
                    occupiedCount,
                    stateEntries.Length,
                    index,
                    entry.State,
                    entry.FailureCode);
            }
        }

        return Create(
            NativeAotClassConstructorRunnerPreflightStatus.Ready,
            typeCount,
            targetMetadataCount,
            occupiedCount,
            stateEntries.Length);
    }

    private static int CountOccupied(ReadOnlySpan<NativeAotClassConstructorStateEntry> entries)
    {
        int count = 0;
        for (int index = 0; index < entries.Length; index++)
        {
            if (entries[index].IsOccupied)
            {
                count++;
            }
        }

        return count;
    }

    private static NativeAotClassConstructorRunnerPreflightResult Create(
        NativeAotClassConstructorRunnerPreflightStatus status,
        int typeCount,
        int targetMetadataCount,
        int occupiedCount,
        int capacity,
        int firstProblemIndex = -1,
        NativeAotClassConstructorState firstProblemState = NativeAotClassConstructorState.Uninitialized,
        uint firstProblemFailureCode = 0)
    {
        return NativeAotClassConstructorRunnerPreflightResult.Create(
            status,
            typeCount,
            targetMetadataCount,
            occupiedCount,
            capacity,
            firstProblemIndex,
            firstProblemState,
            firstProblemFailureCode);
    }
}

public static class NativeAotClassConstructorRunnerPreflightDiagnosticFormatter
{
    public static NativeAotClassConstructorRunnerPreflightFormatResult FormatResult(
        in NativeAotClassConstructorRunnerPreflightResult result,
        Span<byte> destination)
    {
        NativeAotClassConstructorRunnerPreflightFormatStatus validation = Validate(in result);
        if (validation != NativeAotClassConstructorRunnerPreflightFormatStatus.Success)
        {
            return NativeAotClassConstructorRunnerPreflightFormatResult.Fail(
                validation,
                result.Status,
                requiredBytes: 0,
                destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return NativeAotClassConstructorRunnerPreflightFormatResult.Fail(
                NativeAotClassConstructorRunnerPreflightFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status="u8, destination, ref offset) ||
            !TryAppendBytes(GetPreflightStatusName(result.Status), destination, ref offset) ||
            !TryAppendBytes(" initialized="u8, destination, ref offset) ||
            !TryAppendBool(result.IsInitialized, destination, ref offset) ||
            !TryAppendBytes(" executesConstructors="u8, destination, ref offset) ||
            !TryAppendBool(
                NativeAotClassConstructorRunnerPreflight.ExecutesConstructors,
                destination,
                ref offset) ||
            !TryAppendBytes(" typeCount="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.TypeCount, destination, ref offset) ||
            !TryAppendBytes(" targetMetadataCount="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.TargetMetadataCount, destination, ref offset) ||
            !TryAppendBytes(" occupiedCount="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.OccupiedCount, destination, ref offset) ||
            !TryAppendBytes(" capacity="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.Capacity, destination, ref offset) ||
            !TryAppendBytes(" firstProblemIndex="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.FirstProblemIndex, destination, ref offset) ||
            !TryAppendBytes(" firstProblemState="u8, destination, ref offset) ||
            !TryAppendBytes(GetStateName(result.FirstProblemState), destination, ref offset) ||
            !TryAppendBytes(" firstProblemFailureCode="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.FirstProblemFailureCode, destination, ref offset))
        {
            return NativeAotClassConstructorRunnerPreflightFormatResult.Fail(
                NativeAotClassConstructorRunnerPreflightFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        return new NativeAotClassConstructorRunnerPreflightFormatResult(
            NativeAotClassConstructorRunnerPreflightFormatStatus.Success,
            result.Status,
            bytesWritten: offset,
            requiredBytes,
            availableBytes: destination.Length);
    }

    public static NativeAotClassConstructorRunnerPreflightFormatResult QueryRequiredBytes(
        in NativeAotClassConstructorRunnerPreflightResult result)
    {
        NativeAotClassConstructorRunnerPreflightFormatStatus validation = Validate(in result);
        if (validation != NativeAotClassConstructorRunnerPreflightFormatStatus.Success)
        {
            return NativeAotClassConstructorRunnerPreflightFormatResult.Fail(
                validation,
                result.Status,
                requiredBytes: 0,
                availableBytes: 0);
        }

        return new NativeAotClassConstructorRunnerPreflightFormatResult(
            NativeAotClassConstructorRunnerPreflightFormatStatus.Success,
            result.Status,
            bytesWritten: 0,
            requiredBytes: GetRequiredBytes(in result),
            availableBytes: 0);
    }

    private static NativeAotClassConstructorRunnerPreflightFormatStatus Validate(
        in NativeAotClassConstructorRunnerPreflightResult result)
    {
        if (!result.IsInitialized ||
            GetPreflightStatusName(result.Status).Length == 0 ||
            GetStateName(result.FirstProblemState).Length == 0 ||
            result.Capacity < 0 ||
            result.OccupiedCount < 0 ||
            result.OccupiedCount > result.Capacity ||
            result.FirstProblemIndex < -1 ||
            result.FirstProblemIndex >= result.Capacity)
        {
            return NativeAotClassConstructorRunnerPreflightFormatStatus.InvalidResult;
        }

        return NativeAotClassConstructorRunnerPreflightFormatStatus.Success;
    }

    private static int GetRequiredBytes(in NativeAotClassConstructorRunnerPreflightResult result)
    {
        return "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status="u8.Length
            + GetPreflightStatusName(result.Status).Length
            + " initialized="u8.Length
            + GetBoolByteCount(result.IsInitialized)
            + " executesConstructors="u8.Length
            + GetBoolByteCount(NativeAotClassConstructorRunnerPreflight.ExecutesConstructors)
            + " typeCount="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.TypeCount)
            + " targetMetadataCount="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.TargetMetadataCount)
            + " occupiedCount="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.OccupiedCount)
            + " capacity="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.Capacity)
            + " firstProblemIndex="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.FirstProblemIndex)
            + " firstProblemState="u8.Length
            + GetStateName(result.FirstProblemState).Length
            + " firstProblemFailureCode="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.FirstProblemFailureCode);
    }

    private static ReadOnlySpan<byte> GetPreflightStatusName(
        NativeAotClassConstructorRunnerPreflightStatus status)
    {
        return status switch
        {
            NativeAotClassConstructorRunnerPreflightStatus.Ready => "Ready"u8,
            NativeAotClassConstructorRunnerPreflightStatus.NotInitialized => "NotInitialized"u8,
            NativeAotClassConstructorRunnerPreflightStatus.InvalidTypeCount => "InvalidTypeCount"u8,
            NativeAotClassConstructorRunnerPreflightStatus.TypeCountExceedsCapacity =>
                "TypeCountExceedsCapacity"u8,
            NativeAotClassConstructorRunnerPreflightStatus.TypeCountMismatch =>
                "TypeCountMismatch"u8,
            NativeAotClassConstructorRunnerPreflightStatus.MissingTargetMetadata =>
                "MissingTargetMetadata"u8,
            NativeAotClassConstructorRunnerPreflightStatus.TargetMetadataCountMismatch =>
                "TargetMetadataCountMismatch"u8,
            NativeAotClassConstructorRunnerPreflightStatus.PendingOrReentrantConstructor =>
                "PendingOrReentrantConstructor"u8,
            NativeAotClassConstructorRunnerPreflightStatus.FailedConstructor =>
                "FailedConstructor"u8,
            NativeAotClassConstructorRunnerPreflightStatus.InvalidState => "InvalidState"u8,
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

    private static int GetBoolByteCount(bool value)
    {
        return value ? "true"u8.Length : "false"u8.Length;
    }

    private static bool TryAppendBytes(
        ReadOnlySpan<byte> value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendBool(bool value, Span<byte> destination, ref int offset)
    {
        return TryAppendBytes(value ? "true"u8 : "false"u8, destination, ref offset);
    }

    private static bool TryAppendInt64Decimal(
        long value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendUInt64Decimal(
        ulong value,
        Span<byte> destination,
        ref int offset)
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
