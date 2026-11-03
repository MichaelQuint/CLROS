using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OS.Kernel.Memory;

// Reference boundary:
// - SharpOS Cr3Accessor.cs was used only to cross-check the minimal CR3
//   opcodes and raw-value preservation requirement.
// - SharpOS BootAsmGenerator.cs influenced deterministic exact-byte auditing.
// - SharpOS PagingValidation.cs supplied missing-map/alignment/readback
//   negative-test categories only.
// The CLROS implementation, recovery graph, decoder, and tests are independent.
internal static unsafe partial class X64ReversibleCr3TransitionStubPlanTests
{
    private const string TestName = "m01.013.03.transition";
    private const string ExpectedStubSha256 = "F1D94771DE8C8436CCDA1358A15F51DDE683AC3C77CBB0DC4A85B084888CA484";
    private const ulong OwnedCr3 = 0x0000_0000_0020_0000UL;
    private const ulong FirmwareCr3 = 0x0000_0000_1234_5018UL;
    private const ulong OriginalCr4 = (1UL << 5) | X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit;
    private const ulong OriginalRflags = 0x202;
    private const ulong StackStart = 0x0000_0000_0080_0000UL;
    private const ulong StackEnd = StackStart + 0x1000;
    private const ulong EntryRsp = StackStart + 0x888;
    private const ulong CanaryAddress = 0x0000_7fff_ffff_f000UL;
    private const ulong InitialCanary = 0x434c_524f_535f_494eUL;
    private const ulong UpdatedCanary = 0x434c_524f_535f_4f4bUL;
    private const ulong EntryAddress = 0x0000_0000_0040_0000UL;
    internal static int RunM01013Transition()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;
        RunCase(1, "append only enums and exact pod layout", TestEnumsAndPodLayout, ref failures);
        RunCase(2, "exact bytes hash branches and abi decode", TestExactBytesHashAndDecode, ref failures);
        RunCase(3, "materialization readback bounds and atomicity", TestMaterializationAndBounds, ref failures);
        RunCase(4, "truncated oversized and malformed bytes fail", TestMalformedReadback, ref failures);
        RunCase(5, "pge clear and set success preserve raw state", TestSuccessfulEvidence, ref failures);
        RunCase(6, "architectural and runtime failures classify", TestArchitecturalAndRuntimeFailures, ref failures);
        RunCase(7, "callback canary rsp and terminal failures classify", TestCallbackCanaryRspAndTerminalFailures, ref failures);
        RunCase(8, "default and hostile pod bytes fail closed", TestDefaultAndHostilePodFailures, ref failures);
        RunCase(9, "one shot recovery and final flags ordering", TestOneShotRecoveryAndFinalFlagsOrdering, ref failures);
        RunCase(10, "source firewall and no host privileged execution", TestSourceAndHostExecutionBoundary, ref failures);
        RunCase(11, "nostdlib efi exact compile closure", TestNoStdLibCompileClosure, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + TestName + " failures=" + failures);
            return failures;
        }

        Console.WriteLine("TEST_PASS " + TestName);
        return 0;
    }

    private static void RunCase(int number, string name, Func<bool> test, ref int failures)
    {
        string caseName = TestName + "." + number.ToString("00");
        HostCheck.Run("" + caseName, test, ref failures);
    }

    private static bool TestEnumsAndPodLayout()
    {
        return HasExactByteEnum<X64ReversibleCr3TransitionStubPlanStatus>([("Uninitialized", 0), ("Success", 1), ("MissingDestination", 2), ("DestinationCapacityTooSmall", 3), ("CapacityExceedsLimit", 4), ("MisalignedDestination", 5), ("InvalidEntryAddress", 6), ("MisalignedEntryAddress", 7), ("EntryAddressOverflow", 8), ("InternalEncodingFailure", 9), ("MissingSource", 10), ("InvalidSourceCount", 11), ("TruncatedBytePlan", 12), ("OversizedBytePlan", 13), ("SourceCapacityTooSmall", 14), ("MisalignedSource", 15), ("SourceSpanOverflow", 16), ("ByteMismatch", 17), ("MissingState", 18), ("InvalidTerminalKind", 19), ("Timeout", 20), ("Reset", 21), ("TripleFault", 22), ("InvalidRuntimeState", 23), ("FirmwareCallbackObserved", 24), ("InvalidOwnedCr3", 25), ("InvalidStackRange", 26), ("InvalidCanary", 27), ("PcideNotSupported", 28), ("La57NotSupported", 29), ("PgeClearReadbackMismatch", 30), ("OwnedCr3ReadbackMismatch", 31), ("FirmwareCr3ReadbackMismatch", 32), ("Cr4RestoreReadbackMismatch", 33), ("RflagsRestoreReadbackMismatch", 34), ("InterruptStateMismatch", 35), ("CanaryBeforeMismatch", 36), ("CanaryAfterMismatch", 37), ("RspEscape", 38), ("RuntimeReportedFailure", 39), ("CheckpointMismatch", 40), ("RawCr3BitLoss", 41), ("RawCr4BitLoss", 42), ("DefaultRuntimeResult", 43), ("DestinationSpanOverflow", 44), ("InvalidFirmwareCr3", 45), ("PostRestoreCanaryMismatch", 46)]) && HasExactByteEnum<X64ReversibleCr3TransitionRuntimeStatus>([("Uninitialized", 0), ("Entered", 1), ("Success", 2), ("InvalidState", 3), ("FirmwareCallbackForbidden", 4), ("InvalidOwnedCr3", 5), ("InvalidStackRange", 6), ("InvalidCanary", 7), ("RspOutsideExpectedRange", 8), ("InterruptDisableReadbackMismatch", 9), ("La57NotSupported", 10), ("PcideNotSupported", 11), ("PgeClearReadbackMismatch", 12), ("OwnedCr3ReadbackMismatch", 13), ("CanaryBeforeMismatch", 14), ("CanaryAfterMismatch", 15), ("FirmwareCr3ReadbackMismatch", 16), ("Cr4RestoreReadbackMismatch", 17), ("RflagsRestoreReadbackMismatch", 18), ("RspRestoreMismatch", 19), ("PostRestoreCanaryMismatch", 20)]) && HasExactByteEnum<X64ReversibleCr3TransitionCheckpoint>([("Uninitialized", 0), ("EntryValidated", 1), ("InterruptsDisabled", 2), ("PgeCleared", 3), ("OwnedCr3Loaded", 4), ("CanaryUpdated", 5), ("FirmwareCr3Restored", 6), ("Cr4Restored", 7), ("RflagsRestored", 8), ("Returned", 9)]) && HasExactByteEnum<X64ReversibleCr3TransitionTerminalKind>([("Uninitialized", 0), ("Returned", 1), ("Timeout", 2), ("Reset", 3), ("TripleFault", 4)]) && sizeof(X64ReversibleCr3TransitionRuntimeState) == 184 && X64ReversibleCr3TransitionRuntimeState.StructureSize == 184 && Offset<X64ReversibleCr3TransitionRuntimeState>("_ownedCr3Raw") == 8 && Offset<X64ReversibleCr3TransitionRuntimeState>("_originalRflags") == 56 && Offset<X64ReversibleCr3TransitionRuntimeState>("_originalCr3Raw") == 64 && Offset<X64ReversibleCr3TransitionRuntimeState>("_originalCr4Raw") == 72 && Offset<X64ReversibleCr3TransitionRuntimeState>("_clearedCr4Readback") == 80 && Offset<X64ReversibleCr3TransitionRuntimeState>("_ownedCr3Readback") == 88 && Offset<X64ReversibleCr3TransitionRuntimeState>("_canaryBefore") == 96 && Offset<X64ReversibleCr3TransitionRuntimeState>("_canaryAfter") == 104 && Offset<X64ReversibleCr3TransitionRuntimeState>("_firmwareCr3Readback") == 112 && Offset<X64ReversibleCr3TransitionRuntimeState>("_restoredCr4Readback") == 120 && Offset<X64ReversibleCr3TransitionRuntimeState>("_cliRflagsReadback") == 128 && Offset<X64ReversibleCr3TransitionRuntimeState>("_restoredRflagsReadback") == 136 && Offset<X64ReversibleCr3TransitionRuntimeState>("_entryRsp") == 144 && Offset<X64ReversibleCr3TransitionRuntimeState>("_exitRsp") == 152 && Offset<X64ReversibleCr3TransitionRuntimeState>("_postRestoreCanaryReadback") == 160 && Offset<X64ReversibleCr3TransitionRuntimeState>("_firmwareCallbackCount") == 168 && Offset<X64ReversibleCr3TransitionRuntimeState>("_runtimeStatus") == 172 && Offset<X64ReversibleCr3TransitionRuntimeState>("_checkpoint") == 173 && Offset<X64ReversibleCr3TransitionRuntimeState>("_reserved2") == 176 && sizeof(X64ReversibleCr3TransitionStubPlanResult) == 72 && X64ReversibleCr3TransitionStubPlanResult.StructureSize == 72 && Offset<X64ReversibleCr3TransitionStubPlanResult>("_bytesWritten") == 8 && Offset<X64ReversibleCr3TransitionStubPlanResult>("_externalPatchCount") == 56 && Offset<X64ReversibleCr3TransitionStubPlanResult>("_instructionCount") == 64 && sizeof(X64ReversibleCr3TransitionEvidenceResult) == 216 && X64ReversibleCr3TransitionEvidenceResult.StructureSize == 216 && Offset<X64ReversibleCr3TransitionEvidenceResult>("_expectedValue") == 8 && Offset<X64ReversibleCr3TransitionEvidenceResult>("_observedValue") == 16 && Offset<X64ReversibleCr3TransitionEvidenceResult>("_runtimeStateAddress") == 24 && Offset<X64ReversibleCr3TransitionEvidenceResult>("_state") == 32 && X64ReversibleCr3TransitionStubPlan.RequiredByteLength == 1981 && X64ReversibleCr3TransitionStubPlan.AuditedInstructionCount == 485 && X64ReversibleCr3TransitionStubPlan.EntryAlignment == 16 && X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit == 2 && X64ReversibleCr3TransitionStubPlan.LowCanonicalExclusiveCeiling == 0x0000_8000_0000_0000UL && X64ReversibleCr3TransitionStubPlan.ExternalPatchCount == 0 && !X64ReversibleCr3TransitionStubPlan.HasExternalRelocations && X64ReversibleCr3TransitionStubPlan.UsesWindowsX64Abi && !X64ReversibleCr3TransitionStubPlan.InvokesCallbacks && !X64ReversibleCr3TransitionStubPlan.TouchesNonvolatileGeneralRegisters && !X64ReversibleCr3TransitionStubPlan.TouchesXmm6ThroughXmm15 && X64ReversibleCr3TransitionStubPlan.PreservesStackPointer && X64ReversibleCr3TransitionStubPlan.PreservesShadowSpace && X64ReversibleCr3TransitionStubPlan.MaximumSimultaneousPushBytes == 8;
    }

    private static bool TestExactBytesHashAndDecode()
    {
        using StubBuffer buffer = new();
        X64ReversibleCr3TransitionStubPlanResult emitted = X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, buffer.Capacity, EntryAddress);
        if (!emitted.Succeeded)
        {
            Console.WriteLine("M01_013_TRANSITION_MEASURED bytes=" + emitted.SourceCount + " instructions=" + emitted.InstructionCount);
            return false;
        }

        byte[] bytes = buffer.Copy(X64ReversibleCr3TransitionStubPlan.RequiredByteLength);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(hash, ExpectedStubSha256, StringComparison.Ordinal))
        {
            Console.WriteLine("M01_013_TRANSITION_SHA256 " + hash);
            return false;
        }

        if (!TryDecode(bytes, out List<DecodedInstruction> instructions) || (ulong)instructions.Count != X64ReversibleCr3TransitionStubPlan.AuditedInstructionCount)
        {
            return false;
        }

        HashSet<int> starts = instructions.Select(item => item.Offset).ToHashSet();
        foreach (DecodedInstruction instruction in instructions)
        {
            if (instruction.BranchTarget is int target && (target < 0 || target >= bytes.Length || !starts.Contains(target)))
            {
                return false;
            }
        }

        List<DecodedInstruction> writesCr3 = instructions.Where(item => item.Kind == InstructionKind.WriteCr3).ToList();
        List<DecodedInstruction> readsCr3 = instructions.Where(item => item.Kind == InstructionKind.ReadCr3).ToList();
        List<DecodedInstruction> writesCr4 = instructions.Where(item => item.Kind == InstructionKind.WriteCr4).ToList();
        List<DecodedInstruction> readsCr4 = instructions.Where(item => item.Kind == InstructionKind.ReadCr4).ToList();
        List<DecodedInstruction> popfqs = instructions.Where(item => item.Kind == InstructionKind.Popfq).ToList();
        List<DecodedInstruction> pushfqs = instructions.Where(item => item.Kind == InstructionKind.Pushfq).ToList();
        int returnCount = instructions.Count(item => item.Kind == InstructionKind.Ret);
        DecodedInstruction entryJump = instructions[0];
        DecodedInstruction last = instructions[^1];
        return entryJump.Kind == InstructionKind.Jump && entryJump.BranchTarget is int main && main > writesCr4[0].Offset && writesCr3.Count == 2 && readsCr3.Count == 3 && writesCr4.Count == 2 && readsCr4.Count == 3 && popfqs.Count == returnCount + 1 && pushfqs.Count == 3 && instructions.Count(item => item.Kind == InstructionKind.PushR8) == returnCount - 1 && instructions.Count(item => item.Kind == InstructionKind.Cli) == 1 && instructions.All(item => item.Kind != InstructionKind.Sti && item.Kind != InstructionKind.Call && item.Kind != InstructionKind.IndirectControl) && instructions.All(item => !item.TouchesNonvolatile) && instructions.All(item => !item.TouchesXmm) && writesCr3[0].Offset < writesCr4[0].Offset && main < writesCr4[1].Offset && writesCr4[1].Offset < writesCr3[1].Offset && last.Kind == InstructionKind.Jump && last.BranchTarget == 5 && instructions[^1].Kind == InstructionKind.Jump && HasFinalFlagsTailBeforeCleanupReturn(instructions, bytes) && AllReturnsRestoreCapturedEntryFlags(instructions) && HasPostRestoreCanaryStoreBetween(instructions, readsCr3[0].Offset, writesCr4[0].Offset);
    }

    private static bool TestMaterializationAndBounds()
    {
        using StubBuffer buffer = new();
        buffer.Fill(0xa5);
        X64ReversibleCr3TransitionStubPlanResult emitted = X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, buffer.Capacity, EntryAddress);
        if (!emitted.Succeeded || emitted.RequiredBytes != X64ReversibleCr3TransitionStubPlan.RequiredByteLength || emitted.BytesWritten != emitted.RequiredBytes || emitted.InstructionCount != X64ReversibleCr3TransitionStubPlan.AuditedInstructionCount || emitted.InvalidByteIndex != X64ReversibleCr3TransitionStubPlanResult.NoIndex || emitted.HasExternalRelocations || emitted.ExternalPatchCount != 0 || !buffer.AllEqual(emitted.BytesWritten, buffer.Capacity, 0xa5))
        {
            return false;
        }

        X64ReversibleCr3TransitionStubPlanResult readback = X64ReversibleCr3TransitionStubPlan.Validate(buffer.Pointer, emitted.BytesWritten, buffer.Capacity, EntryAddress);
        if (!readback.Succeeded)
        {
            return false;
        }

        buffer.Fill(0x5a);
        byte[] before = buffer.Copy(buffer.Capacity);
        X64ReversibleCr3TransitionStubPlanResult shortResult = X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, X64ReversibleCr3TransitionStubPlan.RequiredByteLength - 1, EntryAddress);
        X64ReversibleCr3TransitionStubPlanResult tooLarge = X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, X64ReversibleCr3TransitionStubPlan.MaximumByteCapacity + 1, EntryAddress);
        byte* overflowPointer = (byte*)(~0UL - (X64ReversibleCr3TransitionStubPlan.RequiredPointerAlignment - 1));
        X64ReversibleCr3TransitionStubPlanResult overflow = X64ReversibleCr3TransitionStubPlan.Emit(overflowPointer, 32, EntryAddress);
        return shortResult.Status == X64ReversibleCr3TransitionStubPlanStatus.DestinationCapacityTooSmall && tooLarge.Status == X64ReversibleCr3TransitionStubPlanStatus.CapacityExceedsLimit && overflow.Status == X64ReversibleCr3TransitionStubPlanStatus.DestinationSpanOverflow && buffer.SequenceEqual(before) && X64ReversibleCr3TransitionStubPlan.Emit(null, buffer.Capacity, EntryAddress).Status == X64ReversibleCr3TransitionStubPlanStatus.MissingDestination && X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer + 1, buffer.Capacity - 1, EntryAddress).Status == X64ReversibleCr3TransitionStubPlanStatus.MisalignedDestination && X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, buffer.Capacity, 0).Status == X64ReversibleCr3TransitionStubPlanStatus.InvalidEntryAddress && X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, buffer.Capacity, EntryAddress + 1).Status == X64ReversibleCr3TransitionStubPlanStatus.MisalignedEntryAddress && X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, buffer.Capacity, ~0UL - 15).Status == X64ReversibleCr3TransitionStubPlanStatus.EntryAddressOverflow;
    }

    private static bool TestMalformedReadback()
    {
        using StubBuffer buffer = new();
        X64ReversibleCr3TransitionStubPlanResult emitted = X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, buffer.Capacity, EntryAddress);
        if (!emitted.Succeeded)
        {
            return false;
        }

        ulong required = X64ReversibleCr3TransitionStubPlan.RequiredByteLength;
        if (X64ReversibleCr3TransitionStubPlan.Validate(buffer.Pointer, 0, buffer.Capacity, EntryAddress).Status != X64ReversibleCr3TransitionStubPlanStatus.InvalidSourceCount || X64ReversibleCr3TransitionStubPlan.Validate(buffer.Pointer, required - 1, buffer.Capacity, EntryAddress).Status != X64ReversibleCr3TransitionStubPlanStatus.TruncatedBytePlan || X64ReversibleCr3TransitionStubPlan.Validate(buffer.Pointer, required + 1, buffer.Capacity, EntryAddress).Status != X64ReversibleCr3TransitionStubPlanStatus.OversizedBytePlan || X64ReversibleCr3TransitionStubPlan.Validate(buffer.Pointer, required, required - 1, EntryAddress).Status != X64ReversibleCr3TransitionStubPlanStatus.SourceCapacityTooSmall || X64ReversibleCr3TransitionStubPlan.Validate(null, required, required, EntryAddress).Status != X64ReversibleCr3TransitionStubPlanStatus.MissingSource || X64ReversibleCr3TransitionStubPlan.Validate(buffer.Pointer + 1, required, required, EntryAddress).Status != X64ReversibleCr3TransitionStubPlanStatus.MisalignedSource)
        {
            return false;
        }

        foreach (ulong index in new[]
        {
            0UL,
            required / 2,
            required - 1
        }

        )
        {
            byte original = buffer.Pointer[index];
            buffer.Pointer[index] ^= 0x5a;
            X64ReversibleCr3TransitionStubPlanResult malformed = X64ReversibleCr3TransitionStubPlan.Validate(buffer.Pointer, required, buffer.Capacity, EntryAddress);
            buffer.Pointer[index] = original;
            if (malformed.Status != X64ReversibleCr3TransitionStubPlanStatus.ByteMismatch || malformed.InvalidByteIndex != index)
            {
                return false;
            }
        }

        return X64ReversibleCr3TransitionStubPlan.Validate((byte*)(~0UL - 15), required, required, EntryAddress).Status == X64ReversibleCr3TransitionStubPlanStatus.SourceSpanOverflow;
    }

    private static bool TestSuccessfulEvidence()
    {
        foreach (bool pgeSet in new[]
        {
            false,
            true
        }

        )
        {
            foreach (bool interruptsEnabled in new[]
            {
                false,
                true
            }

            )
            {
                ulong cr4 = pgeSet ? OriginalCr4 : OriginalCr4 & ~X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit;
                ulong rflags = interruptsEnabled ? OriginalRflags : OriginalRflags & ~X64ReversibleCr3TransitionStubPlan.RflagsInterruptEnableBit;
                X64ReversibleCr3TransitionRuntimeState state = Observed(originalCr4: cr4, originalRflags: rflags);
                X64ReversibleCr3TransitionEvidenceResult evidence = X64ReversibleCr3TransitionStubPlan.Evaluate(&state, X64ReversibleCr3TransitionTerminalKind.Returned);
                if (!evidence.Succeeded || evidence.State.OriginalCr3Raw != FirmwareCr3 || evidence.State.FirmwareCr3Readback != FirmwareCr3 || evidence.State.RestoredCr4Readback != cr4 || evidence.State.PostRestoreCanaryReadback != UpdatedCanary || evidence.State.EntryRsp != evidence.State.ExitRsp)
                {
                    return false;
                }
            }
        }

        X64ReversibleCr3TransitionRuntimeState arithmeticFlagsDiffer = Observed(cliRflags: (OriginalRflags & ~X64ReversibleCr3TransitionStubPlan.RflagsInterruptEnableBit) ^ 1UL);
        return X64ReversibleCr3TransitionStubPlan.Evaluate(&arithmeticFlagsDiffer, X64ReversibleCr3TransitionTerminalKind.Returned).Succeeded && X64ReversibleCr3TransitionStubPlan.IsValidOwnedCr3(OwnedCr3) && !X64ReversibleCr3TransitionStubPlan.IsValidOwnedCr3(OwnedCr3 | 0x8) && X64ReversibleCr3TransitionStubPlan.IsValidFirmwareRawCr3(FirmwareCr3) && X64ReversibleCr3TransitionStubPlan.IsValidFirmwareRawCr3(FirmwareCr3 & ~0x18UL) && !X64ReversibleCr3TransitionStubPlan.IsValidFirmwareRawCr3(FirmwareCr3 | 1);
    }

    private static bool TestArchitecturalAndRuntimeFailures()
    {
        (string Name, X64ReversibleCr3TransitionStubPlanStatus Actual, X64ReversibleCr3TransitionStubPlanStatus Expected)[] checks = [("owned low bits", Status(Observed(ownedCr3: OwnedCr3 | 0x8)), X64ReversibleCr3TransitionStubPlanStatus.InvalidOwnedCr3), ("owned high bits", Status(Observed(ownedCr3: 1UL << 52)), X64ReversibleCr3TransitionStubPlanStatus.InvalidOwnedCr3), ("firmware low bits", Status(Observed(originalCr3: FirmwareCr3 | 1)), X64ReversibleCr3TransitionStubPlanStatus.InvalidFirmwareCr3), ("firmware high bits", Status(Observed(originalCr3: FirmwareCr3 | (1UL << 52))), X64ReversibleCr3TransitionStubPlanStatus.InvalidFirmwareCr3), ("same root", Status(Observed(originalCr3: OwnedCr3 | 0x18)), X64ReversibleCr3TransitionStubPlanStatus.InvalidOwnedCr3), ("pcide", Status(Observed(originalCr4: OriginalCr4 | X64ReversibleCr3TransitionStubPlan.Cr4PcideBit)), X64ReversibleCr3TransitionStubPlanStatus.PcideNotSupported), ("la57", Status(Observed(originalCr4: OriginalCr4 | X64ReversibleCr3TransitionStubPlan.Cr4FiveLevelPagingBit)), X64ReversibleCr3TransitionStubPlanStatus.La57NotSupported), ("pge readback", Status(Observed(clearedCr4: 0)), X64ReversibleCr3TransitionStubPlanStatus.PgeClearReadbackMismatch), ("owned readback", Status(Observed(ownedReadback: OwnedCr3 + 0x1000)), X64ReversibleCr3TransitionStubPlanStatus.OwnedCr3ReadbackMismatch), ("firmware readback", Status(Observed(firmwareReadback: FirmwareCr3 & ~0x18UL)), X64ReversibleCr3TransitionStubPlanStatus.RawCr3BitLoss), ("cr4 readback", Status(Observed(restoredCr4: OriginalCr4 ^ 1UL)), X64ReversibleCr3TransitionStubPlanStatus.RawCr4BitLoss), ("cli flags", Status(Observed(cliRflags: OriginalRflags)), X64ReversibleCr3TransitionStubPlanStatus.InterruptStateMismatch), ("cli reserved bit", Status(Observed(cliRflags: 0)), X64ReversibleCr3TransitionStubPlanStatus.InterruptStateMismatch), ("original reserved bit", Status(Observed(originalRflags: OriginalRflags & ~X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit, cliRflags: 0, restoredRflags: OriginalRflags & ~X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit)), X64ReversibleCr3TransitionStubPlanStatus.InterruptStateMismatch), ("restored flags", Status(Observed(restoredRflags: OriginalRflags ^ 1)), X64ReversibleCr3TransitionStubPlanStatus.RflagsRestoreReadbackMismatch), ("runtime firmware cr3", RuntimeFailure(X64ReversibleCr3TransitionRuntimeStatus.FirmwareCr3ReadbackMismatch), X64ReversibleCr3TransitionStubPlanStatus.FirmwareCr3ReadbackMismatch), ("runtime cr4", RuntimeFailure(X64ReversibleCr3TransitionRuntimeStatus.Cr4RestoreReadbackMismatch), X64ReversibleCr3TransitionStubPlanStatus.Cr4RestoreReadbackMismatch), ("runtime flags", RuntimeFailure(X64ReversibleCr3TransitionRuntimeStatus.RflagsRestoreReadbackMismatch), X64ReversibleCr3TransitionStubPlanStatus.RflagsRestoreReadbackMismatch), ("runtime pge", RuntimeFailure(X64ReversibleCr3TransitionRuntimeStatus.PgeClearReadbackMismatch), X64ReversibleCr3TransitionStubPlanStatus.PgeClearReadbackMismatch)];
        foreach (var check in checks)
        {
            if (check.Actual == check.Expected)
            {
                continue;
            }

            Console.WriteLine("M01_013_TRANSITION_STATUS_MISMATCH " + check.Name.Replace(' ', '_') + " expected=" + check.Expected + " actual=" + check.Actual);
            return false;
        }

        return true;
    }

    private static bool TestCallbackCanaryRspAndTerminalFailures()
    {
        X64ReversibleCr3TransitionRuntimeState valid = Observed();
        return Status(Observed(callbackCount: 1)) == X64ReversibleCr3TransitionStubPlanStatus.FirmwareCallbackObserved && Status(Observed(canaryBefore: InitialCanary ^ 1)) == X64ReversibleCr3TransitionStubPlanStatus.CanaryBeforeMismatch && Status(Observed(canaryAfter: InitialCanary)) == X64ReversibleCr3TransitionStubPlanStatus.CanaryAfterMismatch && Status(Observed(postRestoreCanary: InitialCanary)) == X64ReversibleCr3TransitionStubPlanStatus.PostRestoreCanaryMismatch && Status(Observed(expectedCanary: UpdatedCanary)) == X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary && Status(Observed(expectedCanary: 0)) == X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary && Status(Observed(replacementCanary: 0)) == X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary && Status(Observed(entryRsp: EntryRsp + 8, exitRsp: EntryRsp + 8)) == X64ReversibleCr3TransitionStubPlanStatus.RspEscape && Status(Observed(exitRsp: EntryRsp + 16)) == X64ReversibleCr3TransitionStubPlanStatus.RspEscape && Status(Observed(entryRsp: StackStart, exitRsp: StackStart)) == X64ReversibleCr3TransitionStubPlanStatus.RspEscape && EvaluateStatus(&valid, X64ReversibleCr3TransitionTerminalKind.Timeout) == X64ReversibleCr3TransitionStubPlanStatus.Timeout && EvaluateStatus(&valid, X64ReversibleCr3TransitionTerminalKind.Reset) == X64ReversibleCr3TransitionStubPlanStatus.Reset && EvaluateStatus(&valid, X64ReversibleCr3TransitionTerminalKind.TripleFault) == X64ReversibleCr3TransitionStubPlanStatus.TripleFault && EvaluateStatus(&valid, X64ReversibleCr3TransitionTerminalKind.Uninitialized) == X64ReversibleCr3TransitionStubPlanStatus.InvalidTerminalKind && X64ReversibleCr3TransitionStubPlan.Evaluate(null, X64ReversibleCr3TransitionTerminalKind.Returned).Status == X64ReversibleCr3TransitionStubPlanStatus.MissingState && X64ReversibleCr3TransitionStubPlan.Evaluate(null, X64ReversibleCr3TransitionTerminalKind.Timeout).IsInitialized && TestStorageLayoutAndCanonicalBounds();
    }

    private static bool TestStorageLayoutAndCanonicalBounds()
    {
        const ulong ceiling = X64ReversibleCr3TransitionStubPlan.LowCanonicalExclusiveCeiling;
        const ulong stateSize = X64ReversibleCr3TransitionRuntimeState.StructureSize;
        if (!X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStateAddress(ceiling - stateSize) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStateAddress(ceiling - stateSize + 8) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStateAddress(ceiling) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStateAddress(unchecked(0UL - stateSize)) || !X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x1000, 0x2000, 0x3000, 0x4000) || !X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x1000, ceiling - 8, 0x3000, 0x4000) || !X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x1000, 0x2000, ceiling - 0x1000, ceiling) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x1000, ceiling, 0x3000, 0x4000) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x1000, 0x2000, ceiling - 0x1000, ceiling + 0x10) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x1000, 0x1000, 0x3000, 0x4000) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x1000, 0x1080, 0x3000, 0x4000) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x1000, 0x3000, 0x3000, 0x4000) || X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(0x3000, 0x5000, 0x3000, 0x4000))
        {
            return false;
        }

        X64ReversibleCr3TransitionRuntimeState state = default;
        ulong stateAddress = (ulong)(&state);
        state = Observed(canaryAddress: stateAddress);
        if (EvaluateStatus(&state, X64ReversibleCr3TransitionTerminalKind.Returned) != X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary)
        {
            return false;
        }

        state = Observed(canaryAddress: stateAddress + stateSize - 8);
        if (EvaluateStatus(&state, X64ReversibleCr3TransitionTerminalKind.Returned) != X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary)
        {
            return false;
        }

        state = Observed(canaryAddress: stateAddress + stateSize);
        if (!X64ReversibleCr3TransitionStubPlan.Evaluate(&state, X64ReversibleCr3TransitionTerminalKind.Returned).Succeeded)
        {
            return false;
        }

        ulong overlappingStackStart = stateAddress & ~0xfUL;
        ulong overlappingStackEnd = overlappingStackStart + 0x1000;
        state = Observed(stackStart: overlappingStackStart, stackEnd: overlappingStackEnd, entryRsp: overlappingStackStart + 0x88, exitRsp: overlappingStackStart + 0x88);
        if (EvaluateStatus(&state, X64ReversibleCr3TransitionTerminalKind.Returned) != X64ReversibleCr3TransitionStubPlanStatus.InvalidRuntimeState)
        {
            return false;
        }

        ulong boundaryCanary = (stateAddress + stateSize + 15) & ~0xfUL;
        ulong boundaryStackStart = boundaryCanary + 16;
        ulong boundaryStackEnd = boundaryStackStart + 0x1000;
        state = Observed(stackStart: boundaryStackStart, stackEnd: boundaryStackEnd, canaryAddress: boundaryCanary + 8, entryRsp: boundaryStackStart + 0x88, exitRsp: boundaryStackStart + 0x88);
        if (!X64ReversibleCr3TransitionStubPlan.Evaluate(&state, X64ReversibleCr3TransitionTerminalKind.Returned).Succeeded)
        {
            return false;
        }

        state = Observed(stackStart: boundaryStackStart, stackEnd: boundaryStackEnd, canaryAddress: boundaryStackStart, entryRsp: boundaryStackStart + 0x88, exitRsp: boundaryStackStart + 0x88);
        if (EvaluateStatus(&state, X64ReversibleCr3TransitionTerminalKind.Returned) != X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary)
        {
            return false;
        }

        state = Observed(stackStart: 0x3000, stackEnd: 0x4000, canaryAddress: 0x2000, entryRsp: 0x3888, exitRsp: 0x3888);
        X64ReversibleCr3TransitionEvidenceResult rebound = X64ReversibleCr3TransitionStubPlan.Evaluate(&state, 0x1000, X64ReversibleCr3TransitionTerminalKind.Returned);
        return rebound.Succeeded && rebound.RuntimeStateAddress == 0x1000 && X64ReversibleCr3TransitionStubPlan.Evaluate(&state, 0, X64ReversibleCr3TransitionTerminalKind.Returned).Status == X64ReversibleCr3TransitionStubPlanStatus.InvalidRuntimeState && X64ReversibleCr3TransitionStubPlan.Evaluate(&state, ceiling, X64ReversibleCr3TransitionTerminalKind.Returned).Status == X64ReversibleCr3TransitionStubPlanStatus.InvalidRuntimeState;
    }

    private static bool TestDefaultAndHostilePodFailures()
    {
        X64ReversibleCr3TransitionRuntimeState defaultState = default;
        X64ReversibleCr3TransitionStubPlanResult defaultPlan = default;
        X64ReversibleCr3TransitionEvidenceResult defaultEvidence = default;
        if (defaultState.IsInitialized || defaultPlan.IsInitialized || defaultPlan.Succeeded || defaultEvidence.IsInitialized || defaultEvidence.Succeeded || Status(Observed(runtimeStatus: X64ReversibleCr3TransitionRuntimeStatus.Uninitialized)) != X64ReversibleCr3TransitionStubPlanStatus.DefaultRuntimeResult)
        {
            return false;
        }

        X64ReversibleCr3TransitionRuntimeState state = Observed();
        foreach (int offset in new[]
        {
            0,
            7,
            172,
            173,
            174,
            176
        }

        )
        {
            X64ReversibleCr3TransitionRuntimeState hostile = FlipByte(state, offset);
            if (hostile.IsInitialized || X64ReversibleCr3TransitionStubPlan.Evaluate(&hostile, X64ReversibleCr3TransitionTerminalKind.Returned).Status != X64ReversibleCr3TransitionStubPlanStatus.InvalidRuntimeState)
            {
                return false;
            }
        }

        using StubBuffer buffer = new();
        X64ReversibleCr3TransitionStubPlanResult plan = X64ReversibleCr3TransitionStubPlan.Emit(buffer.Pointer, buffer.Capacity, EntryAddress);
        X64ReversibleCr3TransitionEvidenceResult evidence = X64ReversibleCr3TransitionStubPlan.Evaluate(&state, X64ReversibleCr3TransitionTerminalKind.Returned);
        if (!plan.Succeeded || !evidence.Succeeded)
        {
            return false;
        }

        foreach (int offset in new[]
        {
            1,
            2,
            4,
            8,
            16,
            24,
            32,
            40,
            48,
            56,
            64
        }

        )
        {
            X64ReversibleCr3TransitionStubPlanResult hostile = FlipByte(plan, offset);
            if (hostile.Succeeded)
            {
                return false;
            }
        }

        foreach (int offset in new[]
        {
            1,
            2,
            3,
            4,
            5,
            6,
            8,
            24,
            31,
            204,
            205
        }

        )
        {
            X64ReversibleCr3TransitionEvidenceResult hostile = FlipByte(evidence, offset);
            if (hostile.Succeeded)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestOneShotRecoveryAndFinalFlagsOrdering()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "src", "OS.Kernel", "Memory", "X64ReversibleCr3TransitionStubPlan.cs");
        string source = File.ReadAllText(path);
        string compact = Regex.Replace(RemoveComments(source), @"\s+", " ", RegexOptions.CultureInvariant);
        string[] requiredZeroOffsets = ["StateOriginalRflagsOffset", "StateOriginalCr3RawOffset", "StateOriginalCr4RawOffset", "StateClearedCr4ReadbackOffset", "StateOwnedCr3ReadbackOffset", "StateCanaryBeforeOffset", "StateCanaryAfterOffset", "StateFirmwareCr3ReadbackOffset", "StateRestoredCr4ReadbackOffset", "StateCliRflagsReadbackOffset", "StateRestoredRflagsReadbackOffset", "StateEntryRspOffset", "StateExitRspOffset", "StatePostRestoreCanaryReadbackOffset"];
        return requiredZeroOffsets.All(offset => Regex.IsMatch(compact, @"EmitRequireStateQwordZero\s*\(\s*ref writer,\s*" + Regex.Escape(offset) + @"\s*\)", RegexOptions.CultureInvariant)) && Count(compact, "StateRuntimeStatusOffset") >= 4 && Count(compact, "StateCheckpointOffset") >= 10 && compact.IndexOf("StubBuilder measurement", StringComparison.Ordinal) < compact.IndexOf("StubBuilder writer", StringComparison.Ordinal) && compact.Contains("StatePostRestoreCanaryReadbackOffset", StringComparison.Ordinal) && compact.Contains("writer.PatchRel32(normalCleanup, cleanupRestoreCr3)", StringComparison.Ordinal) && compact.Contains("EmitMovRegFromState( ref writer, Register.Rax, StateOriginalRflagsOffset); EmitPushRax(ref writer); EmitPopfq(ref writer); EmitMovEaxFromR9d(ref writer); EmitRet(ref writer);", StringComparison.Ordinal) && compact.Contains("writer.PatchRel32(jumpToMain, main); EmitPushfq(ref writer); EmitPopR8(ref writer); EmitTestRegReg", StringComparison.Ordinal) && compact.Contains("StateOriginalRflagsOffset, Register.R8", StringComparison.Ordinal) && compact.IndexOf("EmitMovRegImmediate64( ref writer, Register.R11, LowCanonicalExclusiveCeiling)", StringComparison.Ordinal) < compact.IndexOf("StateRuntimeStatusOffset, (byte)X64ReversibleCr3TransitionRuntimeStatus.Entered", StringComparison.Ordinal) && compact.Contains("EmitMovEaxImmediate(ref writer, (uint)failureStatus); EmitPushR8(ref writer); EmitPopfq(ref writer); EmitRet(ref writer);", StringComparison.Ordinal);
    }

    private static bool TestSourceAndHostExecutionBoundary()
    {
        string root = FindRepositoryRoot();
        string productionPath = Path.Combine(root, "src", "OS.Kernel", "Memory", "X64ReversibleCr3TransitionStubPlan.cs");
        string testPath = Path.Combine(root, "tests", "OS.HostTests", "X64ReversibleCr3TransitionStubPlanTests.cs");
        string production = RemoveComments(File.ReadAllText(productionPath));
        string tests = RemoveComments(File.ReadAllText(testPath));
        string[] forbiddenProduction = ["Span<", "ReadOnlySpan<", "string ", "throw ", "System.", "delegate*", "Marshal.", "Reflection", "List<", "new byte[", "IEnumerable<", "Array."];
        string[] forbiddenHostExecution = ["GetDelegateFor" + "FunctionPointer", "cal" + "li", "delegate* " + "unmanaged", "Dynamic" + "Method", "Create" + "Delegate", "Invoke" + "Privileged"];
        MethodInfo[] publicMethods = typeof(X64ReversibleCr3TransitionStubPlan).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        return forbiddenProduction.All(value => !production.Contains(value, StringComparison.Ordinal)) && forbiddenHostExecution.All(value => !tests.Contains(value, StringComparison.Ordinal)) && publicMethods.All(method => !method.Name.Contains("Execute", StringComparison.Ordinal) && !method.Name.Contains("Invoke", StringComparison.Ordinal)) && Count(production, "EmitCli(ref writer);") == 1 && Count(production, "EmitMovCr3FromRax(ref writer);") == 2 && Count(production, "EmitMovCr4FromRax(ref writer);") == 2 && !production.Contains("EmitSti", StringComparison.Ordinal) && !production.Contains("EmitCall", StringComparison.Ordinal) && !production.Contains("Register.Rbx", StringComparison.Ordinal) && !production.Contains("Register.Rbp", StringComparison.Ordinal) && !production.Contains("Register.Rsi", StringComparison.Ordinal) && !production.Contains("Register.Rdi", StringComparison.Ordinal) && !production.Contains("Register.R12", StringComparison.Ordinal) && !production.Contains("Register.R13", StringComparison.Ordinal) && !production.Contains("Register.R14", StringComparison.Ordinal) && !production.Contains("Register.R15", StringComparison.Ordinal);
    }

    private static bool TestNoStdLibCompileClosure()
    {
        string root = FindRepositoryRoot();
        string productionPath = Path.Combine(root, "src", "OS.Kernel", "Memory", "X64ReversibleCr3TransitionStubPlan.cs");
        NoStdLibBuildResult result = BuildNoStdLibClosure(root, productionPath);
        bool succeeded = !result.TimedOut && result.ExitCode == 0 && !result.Output.Contains(": error ", StringComparison.Ordinal) && HasZeroAssemblyReferences(result.AssemblyBytes);
        if (!succeeded)
        {
            Console.WriteLine("M01_013_TRANSITION_NOSTDLIB " + Normalize(result.Output));
        }

        return succeeded;
    }

    private static X64ReversibleCr3TransitionRuntimeState Observed(ulong ownedCr3 = OwnedCr3, ulong stackStart = StackStart, ulong stackEnd = StackEnd, ulong canaryAddress = CanaryAddress, ulong expectedCanary = InitialCanary, ulong replacementCanary = UpdatedCanary, ulong originalRflags = OriginalRflags, ulong originalCr3 = FirmwareCr3, ulong originalCr4 = OriginalCr4, ulong? clearedCr4 = null, ulong? ownedReadback = null, ulong? canaryBefore = null, ulong? canaryAfter = null, ulong? firmwareReadback = null, ulong? restoredCr4 = null, ulong? cliRflags = null, ulong? restoredRflags = null, ulong entryRsp = EntryRsp, ulong? exitRsp = null, ulong? postRestoreCanary = null, uint callbackCount = 0, X64ReversibleCr3TransitionRuntimeStatus runtimeStatus = X64ReversibleCr3TransitionRuntimeStatus.Success, X64ReversibleCr3TransitionCheckpoint checkpoint = X64ReversibleCr3TransitionCheckpoint.Returned)
    {
        return X64ReversibleCr3TransitionRuntimeState.CreateObserved(ownedCr3, stackStart, stackEnd, canaryAddress, expectedCanary, replacementCanary, originalRflags, originalCr3, originalCr4, clearedCr4 ?? (originalCr4 & ~X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit), ownedReadback ?? ownedCr3, canaryBefore ?? expectedCanary, canaryAfter ?? replacementCanary, firmwareReadback ?? originalCr3, restoredCr4 ?? originalCr4, cliRflags ?? (originalRflags & ~X64ReversibleCr3TransitionStubPlan.RflagsInterruptEnableBit), restoredRflags ?? originalRflags, entryRsp, exitRsp ?? entryRsp, postRestoreCanary ?? replacementCanary, callbackCount, runtimeStatus, checkpoint);
    }

    private static X64ReversibleCr3TransitionStubPlanStatus Status(X64ReversibleCr3TransitionRuntimeState state)
    {
        return EvaluateStatus(&state, X64ReversibleCr3TransitionTerminalKind.Returned);
    }

    private static X64ReversibleCr3TransitionStubPlanStatus RuntimeFailure(X64ReversibleCr3TransitionRuntimeStatus status)
    {
        X64ReversibleCr3TransitionRuntimeState state = Observed(runtimeStatus: status);
        return EvaluateStatus(&state, X64ReversibleCr3TransitionTerminalKind.Returned);
    }

    private static X64ReversibleCr3TransitionStubPlanStatus EvaluateStatus(X64ReversibleCr3TransitionRuntimeState* state, X64ReversibleCr3TransitionTerminalKind terminal)
    {
        return X64ReversibleCr3TransitionStubPlan.Evaluate(state, terminal).Status;
    }

    private static bool HasFinalFlagsTailBeforeCleanupReturn(List<DecodedInstruction> instructions, byte[] bytes)
    {
        List<DecodedInstruction> returns = instructions.Where(item => item.Kind == InstructionKind.Ret).ToList();
        DecodedInstruction cleanupReturn = returns[0];
        int index = instructions.IndexOf(cleanupReturn);
        if (index < 4)
        {
            return false;
        }

        return instructions[index - 2].Kind == InstructionKind.Popfq && instructions[index - 3].Kind == InstructionKind.PushRax && instructions[index - 4].Kind == InstructionKind.LoadState && instructions[index - 4].StateOffset == X64ReversibleCr3TransitionStubPlan.StateOriginalRflagsOffset && cleanupReturn.Offset + cleanupReturn.Length <= bytes.Length;
    }

    private static bool AllReturnsRestoreCapturedEntryFlags(List<DecodedInstruction> instructions)
    {
        if (instructions.Count < 4 || instructions[0].Kind != InstructionKind.Jump || instructions[0].BranchTarget is not int mainOffset)
        {
            return false;
        }

        Dictionary<int, int> byOffset = [];
        for (int index = 0; index < instructions.Count; index++)
        {
            byOffset[instructions[index].Offset] = index;
        }

        if (!byOffset.TryGetValue(mainOffset, out int mainIndex) || mainIndex + 1 >= instructions.Count || instructions[mainIndex].Kind != InstructionKind.Pushfq || instructions[mainIndex + 1].Kind != InstructionKind.PopR8)
        {
            return false;
        }

        int captureIndex = mainIndex + 1;
        List<int>[] successors = new List<int>[instructions.Count];
        List<int>[] predecessors = new List<int>[instructions.Count];
        for (int index = 0; index < instructions.Count; index++)
        {
            successors[index] = [];
            predecessors[index] = [];
        }

        for (int index = 0; index < instructions.Count; index++)
        {
            DecodedInstruction instruction = instructions[index];
            if (instruction.Kind is InstructionKind.Jump or InstructionKind.ConditionalBranch)
            {
                if (instruction.BranchTarget is not int targetOffset || !byOffset.TryGetValue(targetOffset, out int targetIndex))
                {
                    return false;
                }

                successors[index].Add(targetIndex);
            }

            if (instruction.Kind != InstructionKind.Jump && instruction.Kind != InstructionKind.Ret && index + 1 < instructions.Count)
            {
                successors[index].Add(index + 1);
            }
        }

        for (int index = 0; index < successors.Length; index++)
        {
            foreach (int successor in successors[index])
            {
                predecessors[successor].Add(index);
            }
        }

        bool[] reachable = Reachable(successors, -1);
        if (!reachable[captureIndex])
        {
            return false;
        }

        foreach (int returnIndex in Enumerable.Range(0, instructions.Count).Where(index => instructions[index].Kind == InstructionKind.Ret))
        {
            if (!reachable[returnIndex] || Reachable(successors, captureIndex)[returnIndex])
            {
                return false;
            }
        }

        bool[] restoresEntryFlags = new bool[instructions.Count];
        for (int index = 0; index < instructions.Count; index++)
        {
            if (instructions[index].Kind != InstructionKind.Popfq)
            {
                continue;
            }

            bool fromR8 = index >= 1 && instructions[index - 1].Kind == InstructionKind.PushR8;
            bool fromState = index >= 2 && instructions[index - 1].Kind == InstructionKind.PushRax && instructions[index - 2].Kind == InstructionKind.LoadState && instructions[index - 2].StateOffset == X64ReversibleCr3TransitionStubPlan.StateOriginalRflagsOffset;
            if ((!fromR8 && !fromState) || predecessors[index].Count != 1 || predecessors[index][0] != index - 1 || predecessors[index - 1].Count != 1 || predecessors[index - 1][0] != index - 2)
            {
                return false;
            }

            restoresEntryFlags[index] = true;
        }

        int[] incomingMasks = new int[instructions.Count];
        Queue<int> queue = new();
        incomingMasks[0] = 1;
        queue.Enqueue(0);
        while (queue.Count != 0)
        {
            int index = queue.Dequeue();
            int outgoingMask = incomingMasks[index];
            if (restoresEntryFlags[index])
            {
                outgoingMask = 2;
            }
            else if (instructions[index].MutatesFlags)
            {
                outgoingMask = 1;
            }

            foreach (int successor in successors[index])
            {
                int combined = incomingMasks[successor] | outgoingMask;
                if (combined != incomingMasks[successor])
                {
                    incomingMasks[successor] = combined;
                    queue.Enqueue(successor);
                }
            }
        }

        return Enumerable.Range(0, instructions.Count).Where(index => instructions[index].Kind == InstructionKind.Ret).All(index => incomingMasks[index] == 2);
    }

    private static bool[] Reachable(List<int>[] successors, int blockedIndex)
    {
        bool[] visited = new bool[successors.Length];
        if (blockedIndex == 0)
        {
            return visited;
        }

        Queue<int> queue = new();
        visited[0] = true;
        queue.Enqueue(0);
        while (queue.Count != 0)
        {
            int index = queue.Dequeue();
            foreach (int successor in successors[index])
            {
                if (successor == blockedIndex || visited[successor])
                {
                    continue;
                }

                visited[successor] = true;
                queue.Enqueue(successor);
            }
        }

        return visited;
    }

    private static bool HasPostRestoreCanaryStoreBetween(List<DecodedInstruction> instructions, int afterOffset, int beforeOffset)
    {
        return instructions.Any(item => item.Offset > afterOffset && item.Offset < beforeOffset && item.Kind == InstructionKind.StoreState && item.StateOffset == X64ReversibleCr3TransitionStubPlan.StatePostRestoreCanaryReadbackOffset);
    }

    private static bool TryDecode(byte[] bytes, out List<DecodedInstruction> instructions)
    {
        instructions = [];
        int offset = 0;
        while (offset < bytes.Length)
        {
            if (!TryDecodeOne(bytes, offset, out DecodedInstruction decoded))
            {
                return false;
            }

            instructions.Add(decoded);
            offset += decoded.Length;
        }

        return offset == bytes.Length;
    }

    private static bool TryDecodeOne(byte[] bytes, int offset, out DecodedInstruction decoded)
    {
        decoded = default;
        int index = offset;
        bool operand16 = false;
        byte rex = 0;
        if (index < bytes.Length && bytes[index] == 0x66)
        {
            operand16 = true;
            index++;
        }

        if (index < bytes.Length && bytes[index] >= 0x40 && bytes[index] <= 0x4f)
        {
            rex = bytes[index++];
        }

        if (index >= bytes.Length)
        {
            return false;
        }

        byte opcode = bytes[index++];
        InstructionKind kind = InstructionKind.Other;
        int? branchTarget = null;
        uint? stateOffset = null;
        bool touchesNonvolatile = false;
        bool touchesXmm = false;
        bool mutatesFlags = false;
        if (opcode == 0xe9)
        {
            if (!TryReadInt32(bytes, index, out int displacement))
                return false;
            index += 4;
            kind = InstructionKind.Jump;
            branchTarget = index + displacement;
        }
        else if (opcode == 0xe8)
        {
            if (!TryReadInt32(bytes, index, out int displacement))
                return false;
            index += 4;
            kind = InstructionKind.Call;
            branchTarget = index + displacement;
        }
        else if (opcode == 0x0f)
        {
            if (index >= bytes.Length)
                return false;
            byte second = bytes[index++];
            if (second >= 0x80 && second <= 0x8f)
            {
                if (!TryReadInt32(bytes, index, out int displacement))
                    return false;
                index += 4;
                kind = InstructionKind.ConditionalBranch;
                branchTarget = index + displacement;
            }
            else if ((second == 0x20 || second == 0x22) && index < bytes.Length)
            {
                byte modrm = bytes[index++];
                kind = (second, modrm) switch
                {
                    (0x20, 0xd8) => InstructionKind.ReadCr3,
                    (0x22, 0xd8) => InstructionKind.WriteCr3,
                    (0x20, 0xe0) => InstructionKind.ReadCr4,
                    (0x22, 0xe0) => InstructionKind.WriteCr4,
                    _ => InstructionKind.Other
                };
            }
            else
            {
                touchesXmm = true;
                return false;
            }
        }
        else if (opcode == 0xc3)
        {
            kind = InstructionKind.Ret;
        }
        else if (opcode == 0x9c)
        {
            kind = InstructionKind.Pushfq;
        }
        else if (opcode == 0x9d)
        {
            kind = InstructionKind.Popfq;
        }
        else if (opcode == 0xfa)
        {
            kind = InstructionKind.Cli;
            mutatesFlags = true;
        }
        else if (opcode == 0xfb)
        {
            kind = InstructionKind.Sti;
        }
        else if (opcode == 0x50)
        {
            kind = (rex & 1) == 0 ? InstructionKind.PushRax : InstructionKind.PushR8;
        }
        else if (opcode == 0x58)
        {
            kind = (rex & 1) == 0 ? InstructionKind.PopRax : InstructionKind.PopR8;
        }
        else if (opcode >= 0xb8 && opcode <= 0xbf)
        {
            int immediateLength = (rex & 8) != 0 ? 8 : 4;
            if (index + immediateLength > bytes.Length)
                return false;
            int register = (opcode - 0xb8) + ((rex & 1) != 0 ? 8 : 0);
            touchesNonvolatile = IsNonvolatile(register);
            index += immediateLength;
        }
        else if (opcode == 0xa9 || opcode == 0x25)
        {
            if (index + 4 > bytes.Length)
                return false;
            index += 4;
            mutatesFlags = true;
        }
        else if (opcode is 0x80 or 0x81 or 0x83 or 0x85 or 0x88 or 0x89 or 0x8b or 0x29 or 0x39 or 0xc1 or 0xc6 or 0xf7)
        {
            if (index >= bytes.Length)
                return false;
            int modrmOffset = index;
            byte modrm = bytes[index++];
            int mod = modrm >> 6;
            int reg = ((modrm >> 3) & 7) + ((rex & 4) != 0 ? 8 : 0);
            int rm = (modrm & 7) + ((rex & 1) != 0 ? 8 : 0);
            if (mod != 3 && (modrm & 7) == 4)
                return false;
            if (mod == 2)
            {
                if (!TryReadUInt32(bytes, index, out uint displacement))
                    return false;
                index += 4;
                if (rm == 10)
                {
                    stateOffset = displacement;
                    if (opcode is 0x89 or 0x88 or 0xc6)
                        kind = InstructionKind.StoreState;
                    else if (opcode is 0x8b)
                        kind = InstructionKind.LoadState;
                }
            }
            else if (mod == 1)
            {
                if (index >= bytes.Length)
                    return false;
                index++;
            }

            bool regIsOperand = opcode is 0x85 or 0x88 or 0x89 or 0x8b or 0x29 or 0x39;
            touchesNonvolatile = (regIsOperand && IsNonvolatile(reg)) || IsNonvolatile(rm);
            mutatesFlags = opcode is 0x80 or 0x81 or 0x83 or 0x85 or 0x29 or 0x39 or 0xc1 or 0xf7;
            if (opcode == 0x80 || opcode == 0x83 || opcode == 0xc1 || opcode == 0xc6)
            {
                if (index >= bytes.Length)
                    return false;
                index++;
            }
            else if (opcode == 0x81 || opcode == 0xf7)
            {
                int immediateLength = operand16 ? 2 : 4;
                if (index + immediateLength > bytes.Length)
                    return false;
                index += immediateLength;
            }

            if (opcode == 0xff && (((modrm >> 3) & 7) is 2 or 3 or 4 or 5))
            {
                kind = InstructionKind.IndirectControl;
            }

            _ = modrmOffset;
        }
        else
        {
            return false;
        }

        decoded = new DecodedInstruction(offset, index - offset, kind, branchTarget, stateOffset, touchesNonvolatile, touchesXmm, mutatesFlags);
        return true;
    }

    private static bool IsNonvolatile(int register)
    {
        return register is 3 or 5 or 6 or 7 or 12 or 13 or 14 or 15;
    }

    private static bool TryReadUInt32(byte[] bytes, int offset, out uint value)
    {
        value = 0;
        if (offset < 0 || offset + 4 > bytes.Length)
            return false;
        value = (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        return true;
    }

    private static bool TryReadInt32(byte[] bytes, int offset, out int value)
    {
        bool success = TryReadUInt32(bytes, offset, out uint raw);
        value = unchecked((int)raw);
        return success;
    }

    private static bool HasExactByteEnum<T>((string Name, byte Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
            return false;
        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();
        if (names.Length != expected.Length || values.Length != expected.Length)
            return false;
        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(names[index], expected[index].Name, StringComparison.Ordinal) || Convert.ToByte(values[index]) != expected[index].Value)
                return false;
        }

        return true;
    }

    private static long Offset<T>(string fieldName)
        where T : struct
    {
        return Marshal.OffsetOf<T>(fieldName).ToInt64();
    }

    private static T OverwriteByte<T>(T value, int offset, byte replacement)
        where T : unmanaged
    {
        if (offset < 0 || offset >= sizeof(T))
            throw new ArgumentOutOfRangeException(nameof(offset));
        ((byte*)&value)[offset] = replacement;
        return value;
    }

    private static T FlipByte<T>(T value, int offset)
        where T : unmanaged
    {
        if (offset < 0 || offset >= sizeof(T))
            throw new ArgumentOutOfRangeException(nameof(offset));
        ((byte*)&value)[offset] ^= 0xff;
        return value;
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string RemoveComments(string source)
    {
        return Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    private static string Normalize(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CLROS.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find CLROS.slnx from transition tests.");
    }

    private static NoStdLibBuildResult BuildNoStdLibClosure(string repositoryRoot, string productionPath)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "clros-m01-013-transition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            File.Copy(Path.Combine(repositoryRoot, "global.json"), Path.Combine(workspace, "global.json"));
            File.WriteAllText(Path.Combine(workspace, "NoStdLibShim.cs"), """
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType : Object { }
                    public abstract class Enum : ValueType { }
                    public class Attribute : Object { }
                    public abstract class Delegate : Object { }
                    public abstract class MulticastDelegate : Delegate { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Byte { }
                    public struct UInt16 { }
                    public struct Int32 { }
                    public struct UInt32 { }
                    public struct Int64 { }
                    public struct UInt64 { }
                    public struct IntPtr { }
                    public struct UIntPtr { }
                    public enum AttributeTargets { }
                    public sealed class AttributeUsageAttribute : Attribute
                    {
                        public AttributeUsageAttribute(AttributeTargets value) { }
                        public bool AllowMultiple { get; set; }
                        public bool Inherited { get; set; }
                    }
                }
                namespace System.Runtime.CompilerServices
                {
                    public sealed class IsReadOnlyAttribute : System.Attribute { }
                }
                """, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(workspace, "Probe.cs"), """
                namespace CLROS.M01
                {
                    public static unsafe class Probe
                    {
                        public static ulong Run()
                        {
                            byte* raw = stackalloc byte[4096 + 15];
                            byte* code = (byte*)(((ulong)raw + 15) & ~15UL);
                            OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlanResult
                                emitted = OS.Kernel.Memory
                                    .X64ReversibleCr3TransitionStubPlan.Emit(
                                        code, 4096, 0x400000);
                            OS.Kernel.Memory
                                .X64ReversibleCr3TransitionRuntimeState state =
                                    OS.Kernel.Memory
                                    .X64ReversibleCr3TransitionRuntimeState
                                    .CreateObserved(
                                        0x200000, 0x800000, 0x801000,
                                        0x900000, 1, 2, 0x202, 0x12345018,
                                        0xa0, 0x20, 0x200000, 1, 2,
                                        0x12345018, 0xa0, 2, 0x202,
                                        0x800888, 0x800888, 2, 0,
                                        OS.Kernel.Memory
                                            .X64ReversibleCr3TransitionRuntimeStatus
                                            .Success,
                                        OS.Kernel.Memory
                                            .X64ReversibleCr3TransitionCheckpoint
                                            .Returned);
                            OS.Kernel.Memory
                                .X64ReversibleCr3TransitionEvidenceResult evidence =
                                    OS.Kernel.Memory
                                    .X64ReversibleCr3TransitionStubPlan.Evaluate(
                                        &state,
                                        OS.Kernel.Memory
                                            .X64ReversibleCr3TransitionTerminalKind
                                            .Returned);
                            return emitted.Succeeded && evidence.Succeeded
                                ? evidence.State.OriginalCr3Raw
                                : 0;
                        }
                    }
                }
                """, new UTF8Encoding(false));
            string project = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.TransitionCompileClosure</AssemblyName>
                    <TargetFramework>net11.0</TargetFramework>
                    <LangVersion>14.0</LangVersion>
                    <OutputType>Library</OutputType>
                    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                    <ImplicitUsings>disable</ImplicitUsings>
                    <Nullable>disable</Nullable>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                    <NoStdLib>true</NoStdLib>
                    <NoConfig>true</NoConfig>
                    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                    <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
                    <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
                    <EnableNETAnalyzers>false</EnableNETAnalyzers>
                    <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                    <DefineConstants>NOSTDLIB_EFI</DefineConstants>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <Deterministic>true</Deterministic>
                    <DebugType>none</DebugType>
                    <DebugSymbols>false</DebugSymbols>
                    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                    <OutputPath>$(MSBuildProjectDirectory)\out\</OutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="NoStdLibShim.cs" />
                    <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionStubPlanStatus.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionStubPlanStatus.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionRuntimeStatus.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionRuntimeStatus.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionCheckpoint.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionCheckpoint.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionTerminalKind.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionTerminalKind.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionRuntimeState.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionRuntimeState.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionStubPlanResult.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionStubPlanResult.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionEvidenceResult.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.X64ReversibleCr3TransitionEvidenceResult.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.Register.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.Register.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.Condition.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.Condition.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.StubBuilderMode.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.StubBuilderMode.cs" />
                <Compile Include="__PRODUCTION__/X64ReversibleCr3TransitionStubPlan.StubBuilder.cs" Link="Memory/X64ReversibleCr3TransitionStubPlan.StubBuilder.cs" />
                    <Compile Include="Probe.cs" />
                  </ItemGroup>
                  <Target Name="CustomizeReferences"
                          BeforeTargets="BeforeCompile"
                          AfterTargets="FindReferenceAssembliesForReferences">
                    <ItemGroup>
                      <ReferencePathWithRefAssemblies
                          Remove="@(ReferencePathWithRefAssemblies)" />
                      <ReferencePath Remove="@(ReferencePath)" />
                    </ItemGroup>
                  </Target>
                </Project>
                """.Replace("__PRODUCTION__", EscapeXml(Path.GetDirectoryName(productionPath)!), StringComparison.Ordinal);
            string projectPath = Path.Combine(workspace, "M01.013.Transition.NoStdLib.csproj");
            File.WriteAllText(projectPath, project, new UTF8Encoding(false));
            ProcessStartInfo start = new("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workspace
            };
            foreach (string argument in new[]
            {
                "build",
                projectPath,
                "--nologo",
                "-m:1",
                "--no-dependencies",
                "--no-incremental",
                "--disable-build-servers",
                "-v:minimal",
                "-p:RestoreIgnoreFailedSources=true",
                "-p:NuGetAudit=false"
            }

            )
            {
                start.ArgumentList.Add(argument);
            }

            start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            start.Environment["DOTNET_NOLOGO"] = "1";
            start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("failed to start no-stdlib compiler");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(90_000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            Task.WaitAll(stdout, stderr);
            string output = stdout.Result + Environment.NewLine + stderr.Result;
            string assemblyPath = Path.Combine(workspace, "out", "CLROS.M01.TransitionCompileClosure.dll");
            return new NoStdLibBuildResult(exited ? process.ExitCode : -1, !exited, output, File.Exists(assemblyPath) ? File.ReadAllBytes(assemblyPath) : []);
        }
        finally
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch
            {
                // Cleanup is not product evidence.
            }
        }
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static bool HasZeroAssemblyReferences(byte[] assembly)
    {
        if (assembly.Length == 0)
            return false;
        using MemoryStream stream = new(assembly, writable: false);
        using PEReader reader = new(stream);
        return reader.HasMetadata && reader.GetMetadataReader().AssemblyReferences.Count == 0;
    }
}
