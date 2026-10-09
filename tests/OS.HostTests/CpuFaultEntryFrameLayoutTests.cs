using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Std.Binary;

internal static class CpuFaultEntryFrameLayoutTests
{

    [HostTest("fault entry frame layout writes and reads page fault frame", HostLane.Fast, 0)]
    private static bool TestPageFaultRoundTrip()
    {
        CpuFaultFrame frame = CreateFrame(
            FaultVectorCatalog.PageFault,
            hasErrorCode: true,
            errorCode: 0x0000000000000002,
            cr2: 0x0000000000001000);
        byte[] storage = CreateStorage();

        CpuFaultEntryFrameLayoutResult writeResult = CpuFaultEntryFrameLayout.TryWrite(
            storage,
            frame,
            codeSegment: 0x0008,
            stackSegment: 0x0010);
        CpuFaultEntryFrameLayoutResult readResult = CpuFaultEntryFrameLayout.TryRead(storage);

        return HostCheck.Result(writeResult.Succeeded
            && writeResult.Status == CpuFaultEntryFrameLayoutStatus.Ok
            && readResult.Succeeded
            && readResult.Status == CpuFaultEntryFrameLayoutStatus.Ok
            && CpuFaultEntryFrameLayout.SlotSize == 8
            && CpuFaultEntryFrameLayout.TotalSize == 200
            && CpuFaultEntryFrameLayout.CsOffset == 72
            && CpuFaultEntryFrameLayout.SsOffset == 80
            && ReadSlot(storage, CpuFaultEntryFrameLayout.VectorOffset) == FaultVectorCatalog.PageFault
            && ReadSlot(storage, CpuFaultEntryFrameLayout.HasErrorCodeOffset) == 1
            && ReadSlot(storage, CpuFaultEntryFrameLayout.ErrorCodeOffset) == 0x0000000000000002
            && ReadSlot(storage, CpuFaultEntryFrameLayout.InstructionPointerOffset) == frame.Rip
            && ReadSlot(storage, CpuFaultEntryFrameLayout.Cr2Offset) == frame.Cr2
            && ReadSlot(storage, CpuFaultEntryFrameLayout.CsOffset) == 0x0008
            && ReadSlot(storage, CpuFaultEntryFrameLayout.SsOffset) == 0x0010
            && storage[CpuFaultEntryFrameLayout.TotalSize] == 0xcc
            && SameFrame(frame, readResult.Frame)
            && readResult.CodeSegment == 0x0008
            && readResult.StackSegment == 0x0010
            && SameFaultText(frame, readResult.Frame));
    }

    [HostTest("fault entry frame layout writes and reads invalid opcode frame", HostLane.Fast, 1)]
    private static bool TestInvalidOpcodeRoundTrip()
    {
        CpuFaultFrame frame = CreateFrame(
            FaultVectorCatalog.InvalidOpcode,
            hasErrorCode: false,
            errorCode: 0,
            cr2: 0);
        byte[] storage = CreateStorage();

        CpuFaultEntryFrameLayoutResult writeResult = CpuFaultEntryFrameLayout.TryWrite(storage, frame);
        CpuFaultEntryFrameLayoutResult readResult = CpuFaultEntryFrameLayout.TryRead(storage);

        return HostCheck.Result(writeResult.Succeeded
            && readResult.Succeeded
            && readResult.Frame.Vector == FaultVectorCatalog.InvalidOpcode
            && !readResult.Frame.HasErrorCode
            && readResult.Frame.ErrorCode == 0
            && ReadSlot(storage, CpuFaultEntryFrameLayout.VectorOffset) == FaultVectorCatalog.InvalidOpcode
            && ReadSlot(storage, CpuFaultEntryFrameLayout.HasErrorCodeOffset) == 0
            && ReadSlot(storage, CpuFaultEntryFrameLayout.ErrorCodeOffset) == 0
            && SameFrame(frame, readResult.Frame)
            && SameFaultText(frame, readResult.Frame));
    }

    [HostTest("fault entry frame layout rejects inconsistent error-code metadata", HostLane.Fast, 2)]
    private static bool TestRejectsInconsistentErrorCodeMetadata()
    {
        byte[] storage = CreateStorage();
        CpuFaultFrame pageFaultWithoutErrorCode = CreateFrame(
            FaultVectorCatalog.PageFault,
            hasErrorCode: false,
            errorCode: 0,
            cr2: 0x0000000000001000);
        CpuFaultFrame invalidOpcodeWithErrorCode = CreateFrame(
            FaultVectorCatalog.InvalidOpcode,
            hasErrorCode: true,
            errorCode: 1,
            cr2: 0);

        CpuFaultEntryFrameLayoutResult pageFaultWriteResult =
            CpuFaultEntryFrameLayout.TryWrite(storage, pageFaultWithoutErrorCode);
        CpuFaultEntryFrameLayoutResult invalidOpcodeWriteResult =
            CpuFaultEntryFrameLayout.TryWrite(storage, invalidOpcodeWithErrorCode);

        byte[] encoded = new byte[CpuFaultEntryFrameLayout.TotalSize];
        WriteSlot(encoded, CpuFaultEntryFrameLayout.VectorOffset, FaultVectorCatalog.PageFault);
        WriteSlot(encoded, CpuFaultEntryFrameLayout.HasErrorCodeOffset, 0);
        CpuFaultEntryFrameLayoutResult readResult = CpuFaultEntryFrameLayout.TryRead(encoded);

        return HostCheck.Result(!pageFaultWriteResult.Succeeded
            && pageFaultWriteResult.Status == CpuFaultEntryFrameLayoutStatus.InconsistentErrorCodeMetadata
            && !invalidOpcodeWriteResult.Succeeded
            && invalidOpcodeWriteResult.Status == CpuFaultEntryFrameLayoutStatus.InconsistentErrorCodeMetadata
            && !readResult.Succeeded
            && readResult.Status == CpuFaultEntryFrameLayoutStatus.InconsistentErrorCodeMetadata
            && readResult.Vector == FaultVectorCatalog.PageFault);
    }

    [HostTest("fault entry frame layout rejects undersized buffers", HostLane.Fast, 3)]
    private static bool TestRejectsUndersizedBuffers()
    {
        CpuFaultFrame frame = CreateFrame(
            FaultVectorCatalog.PageFault,
            hasErrorCode: true,
            errorCode: 2,
            cr2: 0x0000000000001000);
        byte[] storage = new byte[CpuFaultEntryFrameLayout.TotalSize - 1];
        storage.AsSpan().Fill(0xa5);

        CpuFaultEntryFrameLayoutResult writeResult = CpuFaultEntryFrameLayout.TryWrite(storage, frame);
        CpuFaultEntryFrameLayoutResult readResult = CpuFaultEntryFrameLayout.TryRead(storage);

        return HostCheck.Result(!writeResult.Succeeded
            && writeResult.Status == CpuFaultEntryFrameLayoutStatus.BufferTooSmall
            && writeResult.RequiredBytes == CpuFaultEntryFrameLayout.TotalSize
            && !readResult.Succeeded
            && readResult.Status == CpuFaultEntryFrameLayoutStatus.BufferTooSmall
            && readResult.RequiredBytes == CpuFaultEntryFrameLayout.TotalSize
            && AllBytesEqual(storage, 0xa5));
    }

    [HostTest("fault entry frame layout rejects unknown vectors", HostLane.Fast, 4)]
    private static bool TestRejectsUnknownVectors()
    {
        const int unknownVector = 32;
        byte[] storage = CreateStorage();
        CpuFaultFrame frame = CreateFrame(
            unknownVector,
            hasErrorCode: false,
            errorCode: 0,
            cr2: 0);

        CpuFaultEntryFrameLayoutResult writeResult = CpuFaultEntryFrameLayout.TryWrite(storage, frame);

        byte[] encoded = new byte[CpuFaultEntryFrameLayout.TotalSize];
        WriteSlot(encoded, CpuFaultEntryFrameLayout.VectorOffset, unknownVector);
        WriteSlot(encoded, CpuFaultEntryFrameLayout.HasErrorCodeOffset, 0);
        CpuFaultEntryFrameLayoutResult readResult = CpuFaultEntryFrameLayout.TryRead(encoded);

        return HostCheck.Result(!writeResult.Succeeded
            && writeResult.Status == CpuFaultEntryFrameLayoutStatus.UnknownVector
            && writeResult.Vector == unknownVector
            && !readResult.Succeeded
            && readResult.Status == CpuFaultEntryFrameLayoutStatus.UnknownVector
            && readResult.Vector == unknownVector);
    }

    private static CpuFaultFrame CreateFrame(
        int vector,
        bool hasErrorCode,
        ulong errorCode,
        ulong cr2)
    {
        return new CpuFaultFrame(
            vector,
            hasErrorCode,
            errorCode,
            rip: 0x0000000010002000,
            rsp: 0x0000000000700000,
            rbp: 0x0000000000700100,
            rflags: 0x0000000000000202,
            cr2,
            cr3: 0x00000000001AA000,
            rax: 0x1111111111111111,
            rbx: 0x2222222222222222,
            rcx: 0x3333333333333333,
            rdx: 0x4444444444444444,
            rsi: 0x5555555555555555,
            rdi: 0x6666666666666666,
            r8: 0x8888888888888888,
            r9: 0x9999999999999999,
            r10: 0xAAAAAAAAAAAAAAAA,
            r11: 0xBBBBBBBBBBBBBBBB,
            r12: 0xCCCCCCCCCCCCCCCC,
            r13: 0xDDDDDDDDDDDDDDDD,
            r14: 0xEEEEEEEEEEEEEEEE,
            r15: 0xFFFFFFFFFFFFFFFF);
    }

    private static byte[] CreateStorage()
    {
        byte[] storage = new byte[CpuFaultEntryFrameLayout.TotalSize + 1];
        storage.AsSpan().Fill(0xcc);
        return storage;
    }

    private static bool SameFrame(in CpuFaultFrame expected, in CpuFaultFrame actual)
    {
        return actual.Vector == expected.Vector
            && actual.HasErrorCode == expected.HasErrorCode
            && actual.ErrorCode == expected.ErrorCode
            && actual.Rip == expected.Rip
            && actual.Rsp == expected.Rsp
            && actual.Rbp == expected.Rbp
            && actual.Rflags == expected.Rflags
            && actual.Cr2 == expected.Cr2
            && actual.Cr3 == expected.Cr3
            && actual.Rax == expected.Rax
            && actual.Rbx == expected.Rbx
            && actual.Rcx == expected.Rcx
            && actual.Rdx == expected.Rdx
            && actual.Rsi == expected.Rsi
            && actual.Rdi == expected.Rdi
            && actual.R8 == expected.R8
            && actual.R9 == expected.R9
            && actual.R10 == expected.R10
            && actual.R11 == expected.R11
            && actual.R12 == expected.R12
            && actual.R13 == expected.R13
            && actual.R14 == expected.R14
            && actual.R15 == expected.R15;
    }

    private static bool SameFaultText(in CpuFaultFrame expected, in CpuFaultFrame actual)
    {
        return PanicFormatter.FormatFault("idt", "round-trip", expected)
            == PanicFormatter.FormatFault("idt", "round-trip", actual);
    }

    private static ulong ReadSlot(ReadOnlySpan<byte> bytes, int offset)
    {
        return EndianPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, CpuFaultEntryFrameLayout.SlotSize));
    }

    private static void WriteSlot(Span<byte> bytes, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(bytes.Slice(offset, CpuFaultEntryFrameLayout.SlotSize), value);
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> bytes, byte expected)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected)
            {
                return false;
            }
        }

        return true;
    }
}
