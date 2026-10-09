using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Std.Binary;

internal static class TssIstPlanTests
{

    [HostTest("tss ist plan encodes double fault ist", HostLane.Fast, 0)]
    private static bool TestEncodesDoubleFaultIst()
    {
        byte[] bytes = CreatePoisonedBuffer(TssIstPlan.MinimumTssByteLength + 4);
        byte doubleFaultIstIndex = 2;
        ulong doubleFaultIstStackTop = 0xffff80000000a000UL;

        TssIstPlanResult result = TssIstPlan.Write(
            rsp0StackTop: 0xffff800000003000UL,
            doubleFaultIstIndex,
            doubleFaultIstStackTop,
            bytes);

        int doubleFaultIstOffset = TssIstPlan.GetIstOffset(doubleFaultIstIndex);

        return HostCheck.Result(result.Succeeded
            && result.Status == TssIstPlanStatus.Ok
            && result.IstIndex == doubleFaultIstIndex
            && result.ActualBytes == TssIstPlan.MinimumTssByteLength
            && result.RequiredBytes == TssIstPlan.MinimumTssByteLength
            && TssIstPlan.GetIstOffset(1) == TssIstPlan.Ist1Offset
            && doubleFaultIstOffset == TssIstPlan.Ist1Offset + TssIstPlan.StackPointerByteLength
            && EndianPrimitives.ReadUInt64LittleEndian(
                bytes.AsSpan(doubleFaultIstOffset, TssIstPlan.StackPointerByteLength)) == doubleFaultIstStackTop
            && EndianPrimitives.ReadUInt64LittleEndian(
                bytes.AsSpan(TssIstPlan.Ist1Offset, TssIstPlan.StackPointerByteLength)) == 0
            && bytes[0] == 0
            && bytes[TssIstPlan.MinimumTssByteLength - 1] == 0
            && bytes[TssIstPlan.MinimumTssByteLength] == 0xcc);
    }

    [HostTest("tss ist plan encodes rsp0", HostLane.Fast, 1)]
    private static bool TestEncodesRsp0()
    {
        byte[] bytes = CreatePoisonedBuffer(TssIstPlan.MinimumTssByteLength);
        ulong rsp0StackTop = 0xffff800000005000UL;

        TssIstPlanResult result = TssIstPlan.Write(
            rsp0StackTop,
            doubleFaultIstIndex: 1,
            doubleFaultIstStackTop: 0xffff80000000c000UL,
            bytes);

        return HostCheck.Result(result.Succeeded
            && TssIstPlan.Rsp0Offset == 4
            && TssIstPlan.Ist1Offset == 36
            && TssIstPlan.MinimumTssByteLength == 104
            && EndianPrimitives.ReadUInt64LittleEndian(
                bytes.AsSpan(TssIstPlan.Rsp0Offset, TssIstPlan.StackPointerByteLength)) == rsp0StackTop
            && bytes[TssIstPlan.Rsp0Offset] == 0x00
            && bytes[TssIstPlan.Rsp0Offset + 1] == 0x50
            && bytes[TssIstPlan.Rsp0Offset + 2] == 0x00
            && bytes[TssIstPlan.Rsp0Offset + 3] == 0x00
            && bytes[TssIstPlan.Rsp0Offset + 4] == 0x00
            && bytes[TssIstPlan.Rsp0Offset + 5] == 0x80
            && bytes[TssIstPlan.Rsp0Offset + 6] == 0xff
            && bytes[TssIstPlan.Rsp0Offset + 7] == 0xff);
    }

    [HostTest("tss ist plan rejects invalid inputs", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidInputs()
    {
        byte[] bytes = new byte[TssIstPlan.MinimumTssByteLength];

        TssIstPlanResult smallBuffer = TssIstPlan.Write(
            rsp0StackTop: 0xffff800000001000UL,
            doubleFaultIstIndex: 1,
            doubleFaultIstStackTop: 0xffff800000002000UL,
            new byte[TssIstPlan.MinimumTssByteLength - 1]);

        TssIstPlanResult invalidLowIstIndex = TssIstPlan.Write(
            rsp0StackTop: 0xffff800000001000UL,
            doubleFaultIstIndex: 0,
            doubleFaultIstStackTop: 0xffff800000002000UL,
            bytes);

        TssIstPlanResult invalidHighIstIndex = TssIstPlan.Write(
            rsp0StackTop: 0xffff800000001000UL,
            doubleFaultIstIndex: 8,
            doubleFaultIstStackTop: 0xffff800000002000UL,
            bytes);

        TssIstPlanResult nullDoubleFaultStackTop = TssIstPlan.Write(
            rsp0StackTop: 0xffff800000001000UL,
            doubleFaultIstIndex: 1,
            doubleFaultIstStackTop: 0,
            bytes);

        TssIstPlanResult unalignedDoubleFaultStackTop = TssIstPlan.Write(
            rsp0StackTop: 0xffff800000001000UL,
            doubleFaultIstIndex: 1,
            doubleFaultIstStackTop: 0xffff800000002008UL,
            bytes);

        TssIstPlanResult invalidStackSize = TssIstPlan.Write(
            rsp0StackTop: 0xffff800000001000UL,
            doubleFaultIstIndex: 1,
            new TssIstStackPlan(
                stackTop: 0xffff800000002000UL,
                stackSizeBytes: 2048,
                guardSizeBytes: TssIstPlan.PageSize),
            bytes);

        TssIstPlanResult invalidGuardSize = TssIstPlan.Write(
            rsp0StackTop: 0xffff800000001000UL,
            doubleFaultIstIndex: 1,
            new TssIstStackPlan(
                stackTop: 0xffff800000002000UL,
                stackSizeBytes: TssIstPlan.PageSize,
                guardSizeBytes: 0),
            bytes);

        return HostCheck.Result(!smallBuffer.Succeeded
            && smallBuffer.Status == TssIstPlanStatus.OutputBufferTooSmall
            && smallBuffer.ActualBytes == TssIstPlan.MinimumTssByteLength - 1
            && smallBuffer.RequiredBytes == TssIstPlan.MinimumTssByteLength
            && !invalidLowIstIndex.Succeeded
            && invalidLowIstIndex.Status == TssIstPlanStatus.InvalidIstIndex
            && invalidLowIstIndex.IstIndex == 0
            && !invalidHighIstIndex.Succeeded
            && invalidHighIstIndex.Status == TssIstPlanStatus.InvalidIstIndex
            && invalidHighIstIndex.IstIndex == 8
            && !nullDoubleFaultStackTop.Succeeded
            && nullDoubleFaultStackTop.Status == TssIstPlanStatus.NullDoubleFaultIstStackTop
            && !unalignedDoubleFaultStackTop.Succeeded
            && unalignedDoubleFaultStackTop.Status == TssIstPlanStatus.UnalignedDoubleFaultIstStackTop
            && !invalidStackSize.Succeeded
            && invalidStackSize.Status == TssIstPlanStatus.InvalidStackSize
            && !invalidGuardSize.Succeeded
            && invalidGuardSize.Status == TssIstPlanStatus.InvalidGuardSize
            && TssIstPlan.GetIstOffset(0) == -1
            && TssIstPlan.GetIstOffset(8) == -1);
    }

    private static byte[] CreatePoisonedBuffer(int length)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, (byte)0xcc);
        return bytes;
    }
}
