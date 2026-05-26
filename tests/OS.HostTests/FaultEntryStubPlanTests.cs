using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class FaultEntryStubPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "fault entry stub plan encodes invalid opcode frame handoff",
            TestEncodesInvalidOpcodeFrameHandoff,
            ref failures);
        Run(
            "fault entry stub plan encodes page fault frame handoff",
            TestEncodesPageFaultFrameHandoff,
            ref failures);
        Run(
            "fault entry stub plan rejects unsupported vector metadata",
            TestRejectsUnsupportedMetadataAndSmallBuffers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Fault entry stub plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestEncodesInvalidOpcodeFrameHandoff()
    {
        byte[] bytes = CreatePoisonedBuffer(FaultEntryStubPlan.InvalidOpcodeRequiredByteLength + 1);

        FaultEntryStubPlanResult result = FaultEntryStubPlan.EmitInvalidOpcode(bytes);

        return result.Succeeded
            && result.Status == FaultEntryStubPlanStatus.Ok
            && result.Vector == FaultVectorCatalog.InvalidOpcode
            && !result.PushesErrorCode
            && result.PushesSyntheticErrorCode
            && result.BytesWritten == FaultEntryStubPlan.InvalidOpcodeRequiredByteLength
            && result.RequiredBytes == FaultEntryStubPlan.InvalidOpcodeRequiredByteLength
            && FaultEntryStubPlan.GetRequiredByteLength(FaultVectorCatalog.InvalidOpcode)
                == FaultEntryStubPlan.InvalidOpcodeRequiredByteLength
            && FaultEntryStubPlan.Validate(
                FaultVectorCatalog.InvalidOpcode,
                pushesErrorCode: false,
                bytes.Length) == FaultEntryStubPlanStatus.Ok
            && PushMatches(
                bytes,
                FaultEntryStubPlan.InvalidOpcodeSyntheticErrorCodePushOffset,
                FaultEntryStubPlan.SyntheticErrorCodeImmediate)
            && PushMatches(
                bytes,
                FaultEntryStubPlan.InvalidOpcodeHasErrorCodePushOffset,
                FaultEntryStubPlan.NoErrorCodeImmediate)
            && PushMatches(
                bytes,
                FaultEntryStubPlan.InvalidOpcodeVectorPushOffset,
                FaultVectorCatalog.InvalidOpcode)
            && TerminalMatches(bytes, FaultEntryStubPlan.InvalidOpcodeTerminalOffset)
            && bytes[FaultEntryStubPlan.InvalidOpcodeRequiredByteLength] == 0xcc;
    }

    private static bool TestEncodesPageFaultFrameHandoff()
    {
        byte[] bytes = CreatePoisonedBuffer(FaultEntryStubPlan.PageFaultRequiredByteLength + 1);
        FaultVectorCatalog.TryGet(FaultVectorCatalog.PageFault, out FaultVectorInfo pageFault);

        FaultEntryStubPlanResult result = FaultEntryStubPlan.Emit(pageFault, bytes);

        return result.Succeeded
            && result.Status == FaultEntryStubPlanStatus.Ok
            && result.Vector == FaultVectorCatalog.PageFault
            && result.PushesErrorCode
            && !result.PushesSyntheticErrorCode
            && result.BytesWritten == FaultEntryStubPlan.PageFaultRequiredByteLength
            && result.RequiredBytes == FaultEntryStubPlan.PageFaultRequiredByteLength
            && FaultEntryStubPlan.GetRequiredByteLength(FaultVectorCatalog.PageFault)
                == FaultEntryStubPlan.PageFaultRequiredByteLength
            && FaultEntryStubPlan.Validate(pageFault, bytes.Length) == FaultEntryStubPlanStatus.Ok
            && PushMatches(
                bytes,
                FaultEntryStubPlan.PageFaultHasErrorCodePushOffset,
                FaultEntryStubPlan.HasErrorCodeImmediate)
            && PushMatches(
                bytes,
                FaultEntryStubPlan.PageFaultVectorPushOffset,
                FaultVectorCatalog.PageFault)
            && TerminalMatches(bytes, FaultEntryStubPlan.PageFaultTerminalOffset)
            && bytes[FaultEntryStubPlan.PageFaultRequiredByteLength] == 0xcc;
    }

    private static bool TestRejectsUnsupportedMetadataAndSmallBuffers()
    {
        byte[] validBytes = CreatePoisonedBuffer(FaultEntryStubPlan.MaximumRequiredByteLength);
        byte[] shortInvalidOpcodeBytes = CreatePoisonedBuffer(
            FaultEntryStubPlan.InvalidOpcodeRequiredByteLength - 1);
        byte[] shortPageFaultBytes = CreatePoisonedBuffer(
            FaultEntryStubPlan.PageFaultRequiredByteLength - 1);
        FaultVectorCatalog.TryGet(FaultVectorCatalog.DivideError, out FaultVectorInfo unsupportedVector);
        FaultVectorInfo invalidOpcodeWithErrorCode = new FaultVectorInfo(
            FaultVectorCatalog.InvalidOpcode,
            "#UD",
            "Invalid Opcode",
            pushesErrorCode: true,
            "unexpected",
            string.Empty);

        FaultEntryStubPlanResult unsupportedVectorResult =
            FaultEntryStubPlan.Emit(unsupportedVector, validBytes);
        FaultEntryStubPlanResult unsupportedErrorCodeResult =
            FaultEntryStubPlan.Emit(invalidOpcodeWithErrorCode, validBytes);
        FaultEntryStubPlanResult shortInvalidOpcodeResult =
            FaultEntryStubPlan.EmitInvalidOpcode(shortInvalidOpcodeBytes);
        FaultEntryStubPlanResult shortPageFaultResult =
            FaultEntryStubPlan.EmitPageFault(shortPageFaultBytes);

        return !unsupportedVectorResult.Succeeded
            && unsupportedVectorResult.Status == FaultEntryStubPlanStatus.UnsupportedVectorMetadata
            && unsupportedVectorResult.BytesWritten == 0
            && unsupportedVectorResult.RequiredBytes == 0
            && validBytes[0] == 0xcc
            && !unsupportedErrorCodeResult.Succeeded
            && unsupportedErrorCodeResult.Status == FaultEntryStubPlanStatus.UnsupportedErrorCodeMetadata
            && unsupportedErrorCodeResult.Vector == FaultVectorCatalog.InvalidOpcode
            && unsupportedErrorCodeResult.PushesErrorCode
            && unsupportedErrorCodeResult.RequiredBytes == FaultEntryStubPlan.InvalidOpcodeRequiredByteLength
            && validBytes[0] == 0xcc
            && !shortInvalidOpcodeResult.Succeeded
            && shortInvalidOpcodeResult.Status == FaultEntryStubPlanStatus.OutputBufferTooSmall
            && shortInvalidOpcodeResult.RequiredBytes == FaultEntryStubPlan.InvalidOpcodeRequiredByteLength
            && shortInvalidOpcodeBytes[0] == 0xcc
            && !shortPageFaultResult.Succeeded
            && shortPageFaultResult.Status == FaultEntryStubPlanStatus.OutputBufferTooSmall
            && shortPageFaultResult.RequiredBytes == FaultEntryStubPlan.PageFaultRequiredByteLength
            && shortPageFaultBytes[0] == 0xcc
            && FaultEntryStubPlan.Validate(
                FaultVectorCatalog.DivideError,
                pushesErrorCode: false,
                validBytes.Length) == FaultEntryStubPlanStatus.UnsupportedVectorMetadata
            && FaultEntryStubPlan.Validate(
                FaultVectorCatalog.InvalidOpcode,
                pushesErrorCode: true,
                validBytes.Length) == FaultEntryStubPlanStatus.UnsupportedErrorCodeMetadata
            && FaultEntryStubPlan.Validate(
                FaultVectorCatalog.PageFault,
                pushesErrorCode: true,
                FaultEntryStubPlan.PageFaultRequiredByteLength - 1)
                == FaultEntryStubPlanStatus.OutputBufferTooSmall;
    }

    private static bool PushMatches(ReadOnlySpan<byte> bytes, int offset, byte immediate)
    {
        return bytes[offset] == FaultEntryStubPlan.PushImmediate8Opcode
            && bytes[offset + 1] == immediate;
    }

    private static bool TerminalMatches(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes[offset] == FaultEntryStubPlan.TwoByteOpcodePrefix
            && bytes[offset + 1] == FaultEntryStubPlan.Ud2Opcode
            && bytes[offset + 2] == FaultEntryStubPlan.RetOpcode;
    }

    private static byte[] CreatePoisonedBuffer(int length)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, (byte)0xcc);
        return bytes;
    }
}
