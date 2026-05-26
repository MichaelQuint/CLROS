using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public enum TssIstPlanStatus
{
    Ok = 0,
    OutputBufferTooSmall = 1,
    InvalidIstIndex = 2,
    NullDoubleFaultIstStackTop = 3,
    UnalignedDoubleFaultIstStackTop = 4,
    InvalidStackSize = 5,
    InvalidGuardSize = 6
}

public readonly struct TssIstStackPlan
{
    public TssIstStackPlan(ulong stackTop, ulong stackSizeBytes, ulong guardSizeBytes)
    {
        StackTop = stackTop;
        StackSizeBytes = stackSizeBytes;
        GuardSizeBytes = guardSizeBytes;
    }

    public ulong StackTop { get; }

    public ulong StackSizeBytes { get; }

    public ulong GuardSizeBytes { get; }
}

public readonly struct TssIstPlanResult
{
    private TssIstPlanResult(
        bool succeeded,
        TssIstPlanStatus status,
        byte istIndex,
        int actualBytes,
        int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        IstIndex = istIndex;
        ActualBytes = actualBytes;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }

    public TssIstPlanStatus Status { get; }

    public byte IstIndex { get; }

    public int ActualBytes { get; }

    public int RequiredBytes { get; }

    public static TssIstPlanResult Success(byte istIndex)
    {
        return new TssIstPlanResult(
            true,
            TssIstPlanStatus.Ok,
            istIndex,
            TssIstPlan.MinimumTssByteLength,
            TssIstPlan.MinimumTssByteLength);
    }

    public static TssIstPlanResult Fail(
        TssIstPlanStatus status,
        byte istIndex,
        int actualBytes,
        int requiredBytes = TssIstPlan.MinimumTssByteLength)
    {
        return new TssIstPlanResult(
            false,
            status,
            istIndex,
            actualBytes,
            requiredBytes);
    }
}

public static class TssIstPlan
{
    public const int MinimumTssByteLength = 104;
    public const int Rsp0Offset = 4;
    public const int Ist1Offset = 36;
    public const int StackPointerByteLength = 8;
    public const int StackAlignment = 16;
    public const ulong PageSize = 4096;
    public const byte MinimumIstIndex = 1;
    public const byte MaximumIstIndex = 7;

    public static bool IsValidIstIndex(byte istIndex)
    {
        return istIndex >= MinimumIstIndex && istIndex <= MaximumIstIndex;
    }

    public static int GetIstOffset(byte istIndex)
    {
        if (!IsValidIstIndex(istIndex))
        {
            return -1;
        }

        return Ist1Offset + ((istIndex - MinimumIstIndex) * StackPointerByteLength);
    }

    public static TssIstPlanResult Validate(
        byte doubleFaultIstIndex,
        ulong doubleFaultIstStackTop,
        ulong doubleFaultStackSizeBytes,
        ulong doubleFaultGuardSizeBytes,
        int outputBufferLength)
    {
        if (outputBufferLength < MinimumTssByteLength)
        {
            return TssIstPlanResult.Fail(
                TssIstPlanStatus.OutputBufferTooSmall,
                doubleFaultIstIndex,
                outputBufferLength);
        }

        if (!IsValidIstIndex(doubleFaultIstIndex))
        {
            return TssIstPlanResult.Fail(
                TssIstPlanStatus.InvalidIstIndex,
                doubleFaultIstIndex,
                outputBufferLength);
        }

        if (doubleFaultIstStackTop == 0)
        {
            return TssIstPlanResult.Fail(
                TssIstPlanStatus.NullDoubleFaultIstStackTop,
                doubleFaultIstIndex,
                outputBufferLength);
        }

        if ((doubleFaultIstStackTop & (StackAlignment - 1UL)) != 0)
        {
            return TssIstPlanResult.Fail(
                TssIstPlanStatus.UnalignedDoubleFaultIstStackTop,
                doubleFaultIstIndex,
                outputBufferLength);
        }

        if (!IsValidPageMultiple(doubleFaultStackSizeBytes))
        {
            return TssIstPlanResult.Fail(
                TssIstPlanStatus.InvalidStackSize,
                doubleFaultIstIndex,
                outputBufferLength);
        }

        if (!IsValidPageMultiple(doubleFaultGuardSizeBytes))
        {
            return TssIstPlanResult.Fail(
                TssIstPlanStatus.InvalidGuardSize,
                doubleFaultIstIndex,
                outputBufferLength);
        }

        return TssIstPlanResult.Success(doubleFaultIstIndex);
    }

    public static TssIstPlanResult Write(
        ulong rsp0StackTop,
        byte doubleFaultIstIndex,
        ulong doubleFaultIstStackTop,
        Span<byte> destination)
    {
        return Write(
            rsp0StackTop,
            doubleFaultIstIndex,
            new TssIstStackPlan(
                doubleFaultIstStackTop,
                stackSizeBytes: PageSize,
                guardSizeBytes: PageSize),
            destination);
    }

    public static TssIstPlanResult Write(
        ulong rsp0StackTop,
        byte doubleFaultIstIndex,
        TssIstStackPlan doubleFaultIstStack,
        Span<byte> destination)
    {
        TssIstPlanResult validation = Validate(
            doubleFaultIstIndex,
            doubleFaultIstStack.StackTop,
            doubleFaultIstStack.StackSizeBytes,
            doubleFaultIstStack.GuardSizeBytes,
            destination.Length);

        if (!validation.Succeeded)
        {
            return validation;
        }

        Span<byte> tssBytes = destination.Slice(0, MinimumTssByteLength);
        tssBytes.Clear();

        EndianPrimitives.WriteUInt64LittleEndian(
            tssBytes.Slice(Rsp0Offset, StackPointerByteLength),
            rsp0StackTop);

        int doubleFaultIstOffset = GetIstOffset(doubleFaultIstIndex);
        EndianPrimitives.WriteUInt64LittleEndian(
            tssBytes.Slice(doubleFaultIstOffset, StackPointerByteLength),
            doubleFaultIstStack.StackTop);

        return TssIstPlanResult.Success(doubleFaultIstIndex);
    }

    private static bool IsValidPageMultiple(ulong value)
    {
        return value != 0 && (value % PageSize) == 0;
    }
}
