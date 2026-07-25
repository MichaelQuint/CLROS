using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public enum CpuFaultEntryFrameLayoutStatus
{
    Ok = 0,
    BufferTooSmall = 1,
    UnknownVector = 2,
    InvalidHasErrorCodeFlag = 3,
    InconsistentErrorCodeMetadata = 4
}

public readonly struct CpuFaultEntryFrameLayoutResult
{
    private CpuFaultEntryFrameLayoutResult(
        bool succeeded,
        CpuFaultEntryFrameLayoutStatus status,
        CpuFaultFrame frame,
        ushort codeSegment,
        ushort stackSegment,
        int vector,
        int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Frame = frame;
        CodeSegment = codeSegment;
        StackSegment = stackSegment;
        Vector = vector;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }

    public CpuFaultEntryFrameLayoutStatus Status { get; }

    public CpuFaultFrame Frame { get; }

    public ushort CodeSegment { get; }

    public ushort StackSegment { get; }

    public int Vector { get; }

    public int RequiredBytes { get; }

    public static CpuFaultEntryFrameLayoutResult Success(
        in CpuFaultFrame frame,
        ushort codeSegment,
        ushort stackSegment)
    {
        return new CpuFaultEntryFrameLayoutResult(
            true,
            CpuFaultEntryFrameLayoutStatus.Ok,
            frame,
            codeSegment,
            stackSegment,
            frame.Vector,
            CpuFaultEntryFrameLayout.TotalSize);
    }

    public static CpuFaultEntryFrameLayoutResult Fail(
        CpuFaultEntryFrameLayoutStatus status,
        int vector = 0)
    {
        return new CpuFaultEntryFrameLayoutResult(
            false,
            status,
            default,
            0,
            0,
            vector,
            CpuFaultEntryFrameLayout.TotalSize);
    }
}

public static class CpuFaultEntryFrameLayout
{
    public const int SlotSize = sizeof(ulong);
    public const int VectorOffset = 0 * SlotSize;
    public const int HasErrorCodeOffset = 1 * SlotSize;
    public const int ErrorCodeOffset = 2 * SlotSize;
    public const int InstructionPointerOffset = 3 * SlotSize;
    public const int StackPointerOffset = 4 * SlotSize;
    public const int BasePointerOffset = 5 * SlotSize;
    public const int FlagsOffset = 6 * SlotSize;
    public const int Cr2Offset = 7 * SlotSize;
    public const int Cr3Offset = 8 * SlotSize;

    // CS and SS are preserved as 16-bit selectors in full 64-bit slots so this
    // byte layout can map directly to future x64 fault-stub stack slots.
    public const int CsOffset = 9 * SlotSize;
    public const int SsOffset = 10 * SlotSize;

    public const int RaxOffset = 11 * SlotSize;
    public const int RbxOffset = 12 * SlotSize;
    public const int RcxOffset = 13 * SlotSize;
    public const int RdxOffset = 14 * SlotSize;
    public const int RsiOffset = 15 * SlotSize;
    public const int RdiOffset = 16 * SlotSize;
    public const int R8Offset = 17 * SlotSize;
    public const int R9Offset = 18 * SlotSize;
    public const int R10Offset = 19 * SlotSize;
    public const int R11Offset = 20 * SlotSize;
    public const int R12Offset = 21 * SlotSize;
    public const int R13Offset = 22 * SlotSize;
    public const int R14Offset = 23 * SlotSize;
    public const int R15Offset = 24 * SlotSize;
    public const int TotalSize = 25 * SlotSize;

    public static CpuFaultEntryFrameLayoutResult TryWrite(
        Span<byte> destination,
        in CpuFaultFrame frame)
    {
        return TryWrite(destination, frame, codeSegment: 0, stackSegment: 0);
    }

    public static CpuFaultEntryFrameLayoutResult TryWrite(
        Span<byte> destination,
        in CpuFaultFrame frame,
        ushort codeSegment,
        ushort stackSegment)
    {
        if (destination.Length < TotalSize)
        {
            return CpuFaultEntryFrameLayoutResult.Fail(
                CpuFaultEntryFrameLayoutStatus.BufferTooSmall,
                frame.Vector);
        }

        CpuFaultEntryFrameLayoutStatus status = ValidateFrameMetadata(
            frame.Vector,
            frame.HasErrorCode);

        if (status != CpuFaultEntryFrameLayoutStatus.Ok)
        {
            return CpuFaultEntryFrameLayoutResult.Fail(status, frame.Vector);
        }

        WriteSlot(destination, VectorOffset, (ulong)frame.Vector);
        WriteSlot(destination, HasErrorCodeOffset, frame.HasErrorCode ? 1UL : 0UL);
        WriteSlot(destination, ErrorCodeOffset, frame.ErrorCode);
        WriteSlot(destination, InstructionPointerOffset, frame.Rip);
        WriteSlot(destination, StackPointerOffset, frame.Rsp);
        WriteSlot(destination, BasePointerOffset, frame.Rbp);
        WriteSlot(destination, FlagsOffset, frame.Rflags);
        WriteSlot(destination, Cr2Offset, frame.Cr2);
        WriteSlot(destination, Cr3Offset, frame.Cr3);
        WriteSlot(destination, CsOffset, codeSegment);
        WriteSlot(destination, SsOffset, stackSegment);
        WriteSlot(destination, RaxOffset, frame.Rax);
        WriteSlot(destination, RbxOffset, frame.Rbx);
        WriteSlot(destination, RcxOffset, frame.Rcx);
        WriteSlot(destination, RdxOffset, frame.Rdx);
        WriteSlot(destination, RsiOffset, frame.Rsi);
        WriteSlot(destination, RdiOffset, frame.Rdi);
        WriteSlot(destination, R8Offset, frame.R8);
        WriteSlot(destination, R9Offset, frame.R9);
        WriteSlot(destination, R10Offset, frame.R10);
        WriteSlot(destination, R11Offset, frame.R11);
        WriteSlot(destination, R12Offset, frame.R12);
        WriteSlot(destination, R13Offset, frame.R13);
        WriteSlot(destination, R14Offset, frame.R14);
        WriteSlot(destination, R15Offset, frame.R15);

        return CpuFaultEntryFrameLayoutResult.Success(
            frame,
            codeSegment,
            stackSegment);
    }

    public static CpuFaultEntryFrameLayoutResult TryRead(ReadOnlySpan<byte> source)
    {
        if (source.Length < TotalSize)
        {
            return CpuFaultEntryFrameLayoutResult.Fail(
                CpuFaultEntryFrameLayoutStatus.BufferTooSmall);
        }

        ulong vectorValue = ReadSlot(source, VectorOffset);
        if (vectorValue > byte.MaxValue)
        {
            return CpuFaultEntryFrameLayoutResult.Fail(
                CpuFaultEntryFrameLayoutStatus.UnknownVector,
                byte.MaxValue + 1);
        }

        int vector = (int)vectorValue;
        ulong hasErrorCodeValue = ReadSlot(source, HasErrorCodeOffset);
        if (hasErrorCodeValue > 1)
        {
            return CpuFaultEntryFrameLayoutResult.Fail(
                CpuFaultEntryFrameLayoutStatus.InvalidHasErrorCodeFlag,
                vector);
        }

        bool hasErrorCode = hasErrorCodeValue != 0;
        CpuFaultEntryFrameLayoutStatus status = ValidateFrameMetadata(
            vector,
            hasErrorCode);

        if (status != CpuFaultEntryFrameLayoutStatus.Ok)
        {
            return CpuFaultEntryFrameLayoutResult.Fail(status, vector);
        }

        CpuFaultFrame frame = new CpuFaultFrame(
            vector,
            hasErrorCode,
            ReadSlot(source, ErrorCodeOffset),
            ReadSlot(source, InstructionPointerOffset),
            ReadSlot(source, StackPointerOffset),
            ReadSlot(source, BasePointerOffset),
            ReadSlot(source, FlagsOffset),
            ReadSlot(source, Cr2Offset),
            ReadSlot(source, Cr3Offset),
            ReadSlot(source, RaxOffset),
            ReadSlot(source, RbxOffset),
            ReadSlot(source, RcxOffset),
            ReadSlot(source, RdxOffset),
            ReadSlot(source, RsiOffset),
            ReadSlot(source, RdiOffset),
            ReadSlot(source, R8Offset),
            ReadSlot(source, R9Offset),
            ReadSlot(source, R10Offset),
            ReadSlot(source, R11Offset),
            ReadSlot(source, R12Offset),
            ReadSlot(source, R13Offset),
            ReadSlot(source, R14Offset),
            ReadSlot(source, R15Offset));

        return CpuFaultEntryFrameLayoutResult.Success(
            frame,
            (ushort)ReadSlot(source, CsOffset),
            (ushort)ReadSlot(source, SsOffset));
    }

    private static CpuFaultEntryFrameLayoutStatus ValidateFrameMetadata(
        int vector,
        bool hasErrorCode)
    {
        if (vector < 0 || vector > byte.MaxValue)
        {
            return CpuFaultEntryFrameLayoutStatus.UnknownVector;
        }

        if (!FaultVectorCatalog.TryGet((byte)vector, out FaultVectorInfo info))
        {
            return CpuFaultEntryFrameLayoutStatus.UnknownVector;
        }

        return info.PushesErrorCode == hasErrorCode
            ? CpuFaultEntryFrameLayoutStatus.Ok
            : CpuFaultEntryFrameLayoutStatus.InconsistentErrorCodeMetadata;
    }

    private static ulong ReadSlot(ReadOnlySpan<byte> source, int offset)
    {
        return EndianPrimitives.ReadUInt64LittleEndian(source.Slice(offset, SlotSize));
    }

    private static void WriteSlot(Span<byte> destination, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, SlotSize), value);
    }
}
