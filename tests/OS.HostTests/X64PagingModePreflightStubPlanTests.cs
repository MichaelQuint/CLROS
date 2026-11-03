using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OS.Boot;
using OS.Kernel.Memory;

// Reference boundary:
// - SharpOS Cr3Accessor.cs, local snapshot SHA-256
//   088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47,
//   was used only to cross-check MOV RAX,CR3 = 0F 20 D8.
// - SharpOS BootAsmGenerator.cs, local snapshot SHA-256
//   B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500,
//   influenced the independent exact-byte/semantic-plan audit pattern.
// - SharpOS PagingValidation.cs, local snapshot SHA-256
//   B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6,
//   supplied negative-test categories only.
// No reference generator, Iced dependency, live privileged path, or copied
// test implementation enters CLROS.
internal static unsafe partial class X64PagingModePreflightStubPlanTests
{
    private const string TestName = "m01.013.02.preflight";
    private const string ExpectedStubSha256 = "71993B5134F7A4936718FD406EBF68CDA7E82476EE15B3F9824A5B6600FF9171";
    private const ulong ValidCr0 = X64PagingModePreflightStubPlan.Cr0PagingBit | X64PagingModePreflightStubPlan.Cr0WriteProtectBit;
    private const ulong ValidCr3 = 0x0000000012345018UL;
    private const ulong ValidCr4 = X64PagingModePreflightStubPlan.Cr4PaeBit;
    private const ulong ValidEfer = X64PagingModePreflightStubPlan.EferLongModeEnableBit | X64PagingModePreflightStubPlan.EferLongModeActiveBit | X64PagingModePreflightStubPlan.EferNoExecuteEnableBit;
    private const ulong ValidRflags = 0x202;
    private const ulong ArenaStart = 0x001f0000;
    private const ulong ArenaLength = 0x00040000;
    private const ulong OwnedRoot = 0x00200000;
    internal static int RunM01013Preflight()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;
        RunCase("enum and pod contract", TestEnumAndPodContract, ref failures);
        RunCase("exact byte stream and hash", TestExactByteStreamAndHash, ref failures);
        RunCase("independent semantic plan and ABI", TestSemanticPlanAndAbi, ref failures);
        RunCase("materialize and exact readback", TestMaterializeAndReadback, ref failures);
        RunCase("stub bounds capacity alias and atomicity", TestStubBoundaryFailures, ref failures);
        RunCase("positive architectural observations", TestPositiveEvaluations, ref failures);
        RunCase("architectural prerequisite failures", TestArchitecturalFailures, ref failures);
        RunCase("owned root and arena failures", TestRootAndArenaFailures, ref failures);
        RunCase("evaluation pointer core failures", TestEvaluationPointerFailures, ref failures);
        RunCase("hostile pod and default failures", TestHostilePodAndDefaults, ref failures);
        RunCase("source and nostdlib efi closure", TestSourceAndNoStdLibBoundary, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + TestName + " failures=" + failures);
            return failures;
        }

        Console.WriteLine("TEST_PASS " + TestName);
        return 0;
    }

    private static void RunCase(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run(TestName + " " + name, () =>
        {
            try
            {
                if (test())
                {
                    return true;
                }

                Console.WriteLine("M01_013_PREFLIGHT_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("M01_013_PREFLIGHT_CASE_FAIL " + Normalize(name) + " " + ex.GetType().Name + " " + Normalize(ex.Message));
                throw;
            }
        }, ref failures);
    }

    private static bool TestEnumAndPodContract()
    {
        (string, byte)[] expectedInstructionKinds = [("Uninitialized", 0), ("PreserveNonvolatileRbx", 1), ("CopyObservationPointer", 2), ("CaptureRflags", 3), ("MoveRflagsToVolatile", 4), ("StoreRflags", 5), ("ClearCompletion", 6), ("ClearReserved", 7), ("ReadCr0", 8), ("StoreCr0", 9), ("ReadCr3", 10), ("StoreCr3", 11), ("ReadCr4", 12), ("StoreCr4", 13), ("SelectEferMsr", 14), ("ReadEferMsr", 15), ("ShiftEferHigh", 16), ("CombineEfer", 17), ("StoreEfer", 18), ("SelectMaximumExtendedLeaf", 19), ("ExecuteCpuidMaximumLeaf", 20), ("StoreMaximumExtendedLeaf", 21), ("CompareAddressSizesLeaf", 22), ("BranchBelowAddressSizesLeaf", 23), ("SelectAddressSizesLeaf", 24), ("ExecuteCpuidAddressSizes", 25), ("StoreAddressSizes", 26), ("BranchPastUnsupportedPath", 27), ("ClearUnsupportedAddressSizes", 28), ("StoreUnsupportedAddressSizes", 29), ("StoreCompletion", 30), ("RestoreNonvolatileRbx", 31), ("Return", 32)];
        (string, byte)[] expectedRegisters = [("Uninitialized", 0), ("None", 1), ("Rax", 2), ("Rbx", 3), ("Rcx", 4), ("Rdx", 5), ("R8", 6), ("Rflags", 7), ("Cr0", 8), ("Cr3", 9), ("Cr4", 10), ("Ia32Efer", 11), ("CpuidEaxEbxEcxEdx", 12), ("ObservationMemory", 13), ("Stack", 14)];
        (string, byte)[] expectedOperations = [("Uninitialized", 0), ("Materialize", 1), ("Validate", 2)];
        (string, byte)[] expectedStatuses = [("Uninitialized", 0), ("Success", 1), ("ContractLayoutMismatch", 2), ("InvalidInstructionIndex", 3), ("InvalidByteIndex", 4), ("InvalidStubByteCount", 5), ("StubCapacityTooSmall", 6), ("StubCapacityTooLarge", 7), ("MissingStubBuffer", 8), ("MisalignedStubBuffer", 9), ("StubBufferSpanOverflow", 10), ("InvalidEntryOffset", 11), ("MisalignedEntry", 12), ("EntrySpanOverflow", 13), ("MissingObservationStorage", 14), ("ObservationStorageSizeTooSmall", 15), ("ObservationStorageSizeTooLarge", 16), ("MisalignedObservationStorage", 17), ("ObservationStorageSpanOverflow", 18), ("MissingStateStorage", 19), ("StateStorageSizeTooSmall", 20), ("StateStorageSizeTooLarge", 21), ("MisalignedStateStorage", 22), ("StateStorageSpanOverflow", 23), ("StubObservationOverlap", 24), ("StubStateOverlap", 25), ("ObservationStateOverlap", 26), ("StubByteMismatch", 27), ("StubOpcodeMismatch", 28), ("StubSemanticPlanMismatch", 29), ("InvalidRequestCount", 30), ("InvalidObservationCount", 31), ("InvalidStateCount", 32), ("RequestCapacityTooSmall", 33), ("ObservationCapacityTooSmall", 34), ("StateCapacityTooSmall", 35), ("CapacityExceedsLimit", 36), ("MissingRequest", 37), ("MissingObservation", 38), ("MissingState", 39), ("MisalignedRequest", 40), ("MisalignedObservation", 41), ("MisalignedState", 42), ("RequestSpanOverflow", 43), ("ObservationSpanOverflow", 44), ("StateSpanOverflow", 45), ("RequestObservationOverlap", 46), ("RequestStateOverlap", 47), ("EvaluationObservationStateOverlap", 48), ("RequestNotInitialized", 49), ("InvalidRequestState", 50), ("ProbeDisabled", 51), ("ObservationNotInitialized", 52), ("InvalidObservationState", 53), ("CpuidAddressLeafUnavailable", 54), ("UnsupportedPhysicalAddressWidth", 55), ("PagingNotEnabled", 56), ("WriteProtectNotEnabled", 57), ("PaeNotEnabled", 58), ("LongModeNotEnabled", 59), ("LongModeNotActive", 60), ("NxeNotEnabled", 61), ("FiveLevelPagingNotSupported", 62), ("PcideNotSupported", 63), ("InvalidRflags", 64), ("InvalidFirmwareCr3Bits", 65), ("InvalidFirmwareCr3Base", 66), ("InvalidOwnedRoot", 67), ("MisalignedOwnedRoot", 68), ("InvalidTableArena", 69), ("MisalignedTableArena", 70), ("TableArenaLengthZero", 71), ("TableArenaLengthUnaligned", 72), ("TableArenaOverflow", 73), ("TableArenaOutsidePhysicalWidth", 74), ("OwnedRootOutsideArena", 75), ("OwnedRootPageOutsideArena", 76), ("OwnedRootOutsidePhysicalWidth", 77)];
        bool offsets = Offset<X64PagingModePreflightInstruction>("_operand") == 0 && Offset<X64PagingModePreflightInstruction>("_byteOffset") == 8 && Offset<X64PagingModePreflightInstruction>("_byteLength") == 12 && Offset<X64PagingModePreflightInstruction>("_isInitialized") == 14 && Offset<X64PagingModePreflightInstruction>("_kind") == 15 && Offset<X64PagingModePreflightInstruction>("_destination") == 16 && Offset<X64PagingModePreflightInstruction>("_source") == 17 && Offset<X64PagingModePreflightInstruction>("_stackDepthSlotsAfter") == 18 && Offset<X64PagingModePreflightInstruction>("_reserved0") == 19 && Offset<X64PagingModePreflightInstruction>("_reserved1") == 20 && Offset<X64PagingModePreflightObservation>("_cr0") == 0 && Offset<X64PagingModePreflightObservation>("_rawCr3") == 8 && Offset<X64PagingModePreflightObservation>("_rawCr4") == 16 && Offset<X64PagingModePreflightObservation>("_ia32Efer") == 24 && Offset<X64PagingModePreflightObservation>("_cpuidMaximumExtendedLeaf") == 32 && Offset<X64PagingModePreflightObservation>("_cpuidAddressSizesEax") == 36 && Offset<X64PagingModePreflightObservation>("_rflags") == 40 && Offset<X64PagingModePreflightObservation>("_completionSignature") == 48 && Offset<X64PagingModePreflightObservation>("_reserved") == 52 && Offset<X64PagingModePreflightRequest>("_ownedRoot") == 0 && Offset<X64PagingModePreflightRequest>("_tableArenaStart") == 8 && Offset<X64PagingModePreflightRequest>("_tableArenaByteLength") == 16 && Offset<X64PagingModePreflightRequest>("_isInitialized") == 24 && Offset<X64PagingModePreflightRequest>("_probeEnabled") == 25 && Offset<X64PagingModePreflightRequest>("_reserved0") == 26 && Offset<X64PagingModePreflightRequest>("_reserved1") == 28 && Offset<X64PagingModePreflightResult>("_ownedRoot") == 0 && Offset<X64PagingModePreflightResult>("_tableArenaStart") == 8 && Offset<X64PagingModePreflightResult>("_tableArenaEndExclusive") == 16 && Offset<X64PagingModePreflightResult>("_cr0") == 24 && Offset<X64PagingModePreflightResult>("_rawFirmwareCr3") == 32 && Offset<X64PagingModePreflightResult>("_rawCr4") == 40 && Offset<X64PagingModePreflightResult>("_ia32Efer") == 48 && Offset<X64PagingModePreflightResult>("_rflags") == 56 && Offset<X64PagingModePreflightResult>("_physicalAddressMask") == 64 && Offset<X64PagingModePreflightResult>("_isInitialized") == 72 && Offset<X64PagingModePreflightResult>("_status") == 73 && Offset<X64PagingModePreflightResult>("_physicalAddressWidth") == 74 && Offset<X64PagingModePreflightResult>("_reserved0") == 87 && Offset<X64PagingModePreflightResult>("_reserved1") == 88 && Offset<X64PagingModePreflightStubResult>("_declaredByteCount") == 0 && Offset<X64PagingModePreflightStubResult>("_bufferCapacity") == 8 && Offset<X64PagingModePreflightStubResult>("_entryOffset") == 16 && Offset<X64PagingModePreflightStubResult>("_entryAddress") == 24 && Offset<X64PagingModePreflightStubResult>("_bytesProcessed") == 32 && Offset<X64PagingModePreflightStubResult>("_mismatchOffset") == 40 && Offset<X64PagingModePreflightStubResult>("_isInitialized") == 48 && Offset<X64PagingModePreflightStubResult>("_status") == 49 && Offset<X64PagingModePreflightStubResult>("_operation") == 50 && Offset<X64PagingModePreflightStubResult>("_exactBytes") == 51 && Offset<X64PagingModePreflightStubResult>("_reserved") == 52;
        return HasExactByteEnum<X64PagingModePreflightInstructionKind>(expectedInstructionKinds) && HasExactByteEnum<X64PagingModePreflightRegister>(expectedRegisters) && HasExactByteEnum<X64PagingModePreflightOperation>(expectedOperations) && HasExactByteEnum<X64PagingModePreflightStatus>(expectedStatuses) && IsReadOnlySequentialUnmanaged<X64PagingModePreflightInstruction>() && IsReadOnlySequentialUnmanaged<X64PagingModePreflightObservation>() && IsReadOnlySequentialUnmanaged<X64PagingModePreflightRequest>() && IsReadOnlySequentialUnmanaged<X64PagingModePreflightResult>() && IsReadOnlySequentialUnmanaged<X64PagingModePreflightStubResult>() && sizeof(X64PagingModePreflightInstruction) == (int)X64PagingModePreflightInstruction.StructureSize && sizeof(X64PagingModePreflightObservation) == (int)X64PagingModePreflightObservation.StructureSize && sizeof(X64PagingModePreflightRequest) == (int)X64PagingModePreflightRequest.StructureSize && sizeof(X64PagingModePreflightResult) == (int)X64PagingModePreflightResult.StructureSize && sizeof(X64PagingModePreflightStubResult) == (int)X64PagingModePreflightStubResult.StructureSize && offsets && !default(X64PagingModePreflightInstruction).IsInitialized && !default(X64PagingModePreflightObservation).IsInitialized && !default(X64PagingModePreflightRequest).IsInitialized && !default(X64PagingModePreflightResult).IsInitialized && !default(X64PagingModePreflightResult).Succeeded && !default(X64PagingModePreflightStubResult).IsInitialized && !default(X64PagingModePreflightStubResult).Succeeded && X64PagingModePreflightStubPlan.RequiredByteLength == 111 && X64PagingModePreflightStubPlan.InstructionCount == 32 && X64PagingModePreflightStubPlan.StubPageByteLength == 4096 && X64PagingModePreflightStubPlan.RequiredEntryAlignment == 16 && X64PagingModePreflightStubPlan.RequiredDataAlignment == 8 && !X64PagingModePreflightStubPlan.HostMethodsExecutePrivilegedInstructions && !X64PagingModePreflightStubPlan.HostMethodsInvokeEmittedBytes && X64PagingModePreflightStubPlan.EmittedStubReadsControlRegisters && !X64PagingModePreflightStubPlan.EmittedStubWritesControlRegisters && X64PagingModePreflightStubPlan.EmittedStubReadsIa32Efer && X64PagingModePreflightStubPlan.EmittedStubExecutesCpuid && !X64PagingModePreflightStubPlan.UsesXmmRegisters && !X64PagingModePreflightStubPlan.PerformsLiveCr3Load;
    }

    private static bool TestExactByteStreamAndHash()
    {
        byte[] expected = ExpectedBytes();
        if (expected.Length != (int)X64PagingModePreflightStubPlan.RequiredByteLength)
        {
            return false;
        }

        byte[] actual = new byte[expected.Length];
        for (ulong index = 0; index < (ulong)actual.Length; index++)
        {
            if (X64PagingModePreflightStubPlan.TryGetExpectedByte(index, out actual[(int)index]) != X64PagingModePreflightStatus.Success)
            {
                return false;
            }
        }

        byte sentinel;
        string hash = Convert.ToHexString(SHA256.HashData(actual));
        return actual.AsSpan().SequenceEqual(expected) && hash == ExpectedStubSha256 && X64PagingModePreflightStubPlan.TryGetExpectedByte((ulong)actual.Length, out sentinel) == X64PagingModePreflightStatus.InvalidByteIndex && sentinel == 0 && Matches(actual, 0, [0x53]) && Matches(actual, 1, [0x49, 0x89, 0xc8]) && Matches(actual, 4, [0x41, 0xc7, 0x40, 0x30, 0x00, 0x00, 0x00, 0x00]) && Matches(actual, 12, [0x41, 0xc7, 0x40, 0x34, 0x00, 0x00, 0x00, 0x00]) && Matches(actual, 20, [0x9c, 0x58]) && Matches(actual, 22, [0x49, 0x89, 0x40, 0x28]) && Matches(actual, 26, [0x0f, 0x20, 0xc0]) && Matches(actual, 32, [0x0f, 0x20, 0xd8]) && Matches(actual, 39, [0x0f, 0x20, 0xe0]) && Matches(actual, 46, [0xb9, 0x80, 0x00, 0x00, 0xc0]) && Matches(actual, 51, [0x0f, 0x32]) && Matches(actual, 64, [0xb8, 0x00, 0x00, 0x00, 0x80]) && Matches(actual, 69, [0x0f, 0xa2]) && Matches(actual, 75, [0x3d, 0x08, 0x00, 0x00, 0x80]) && Matches(actual, 80, [0x72, 0x0d]) && 82 == 82 && 82 + 13 == 95 && Matches(actual, 82, [0xb8, 0x08, 0x00, 0x00, 0x80]) && Matches(actual, 87, [0x0f, 0xa2]) && Matches(actual, 93, [0xeb, 0x06]) && 95 + 6 == 101 && Matches(actual, 101, [0x41, 0xc7, 0x40, 0x30, 0x50, 0x4c, 0x46, 0x31]) && Matches(actual, 109, [0x5b, 0xc3]) && CountSequence(actual, [0x0f, 0x20, 0xd8]) == 1 && CountSequence(actual, [0x0f, 0x22, 0xd8]) == 0 && CountSequence(actual, [0x0f, 0xa2]) == 2 && CountSequence(actual, [0x0f, 0x32]) == 1;
    }

    private static bool TestSemanticPlanAndAbi()
    {
        if (X64PagingModePreflightStubPlan.ValidateSemanticPlan() != X64PagingModePreflightStatus.Success)
        {
            return false;
        }

        ulong cursor = 0;
        int preserveRbx = 0;
        int restoreRbx = 0;
        int cpuid = 0;
        int writes = 0;
        int firstObservationWrite = -1;
        int lastObservationWrite = -1;
        for (ulong index = 0; index < X64PagingModePreflightStubPlan.InstructionCount; index++)
        {
            if (X64PagingModePreflightStubPlan.TryGetInstruction(index, out X64PagingModePreflightInstruction instruction) != X64PagingModePreflightStatus.Success || !instruction.IsInitialized || instruction.ByteOffset != cursor || instruction.ByteLength == 0 || instruction.StackDepthSlotsAfter > 2)
            {
                return false;
            }

            cursor += instruction.ByteLength;
            preserveRbx += instruction.Kind == X64PagingModePreflightInstructionKind.PreserveNonvolatileRbx ? 1 : 0;
            restoreRbx += instruction.Kind == X64PagingModePreflightInstructionKind.RestoreNonvolatileRbx ? 1 : 0;
            cpuid += instruction.Kind == X64PagingModePreflightInstructionKind.ExecuteCpuidMaximumLeaf || instruction.Kind == X64PagingModePreflightInstructionKind.ExecuteCpuidAddressSizes ? 1 : 0;
            if (instruction.Destination == X64PagingModePreflightRegister.ObservationMemory)
            {
                writes++;
                firstObservationWrite = firstObservationWrite < 0 ? (int)index : firstObservationWrite;
                lastObservationWrite = (int)index;
            }

            if (TouchesForbiddenNonvolatile(instruction))
            {
                return false;
            }
        }

        X64PagingModePreflightStubPlan.TryGetInstruction(0, out X64PagingModePreflightInstruction first);
        X64PagingModePreflightStubPlan.TryGetInstruction(2, out X64PagingModePreflightInstruction clearCompletion);
        X64PagingModePreflightStubPlan.TryGetInstruction(3, out X64PagingModePreflightInstruction clearReserved);
        X64PagingModePreflightStubPlan.TryGetInstruction(4, out X64PagingModePreflightInstruction flagsCapture);
        X64PagingModePreflightStubPlan.TryGetInstruction(6, out X64PagingModePreflightInstruction flagsStore);
        X64PagingModePreflightStubPlan.TryGetInstruction(21, out X64PagingModePreflightInstruction firstFlagMutation);
        X64PagingModePreflightStubPlan.TryGetInstruction(29, out X64PagingModePreflightInstruction completion);
        X64PagingModePreflightStubPlan.TryGetInstruction(30, out X64PagingModePreflightInstruction restore);
        X64PagingModePreflightStubPlan.TryGetInstruction(31, out X64PagingModePreflightInstruction last);
        X64PagingModePreflightInstruction invalid;
        return cursor == X64PagingModePreflightStubPlan.RequiredByteLength && preserveRbx == 1 && restoreRbx == 1 && cpuid == 2 && writes == 11 && firstObservationWrite == 2 && lastObservationWrite == 29 && first.Kind == X64PagingModePreflightInstructionKind.PreserveNonvolatileRbx && first.Source == X64PagingModePreflightRegister.Rbx && first.StackDepthSlotsAfter == 1 && clearCompletion.Kind == X64PagingModePreflightInstructionKind.ClearCompletion && clearCompletion.ByteOffset == 4 && clearCompletion.Destination == X64PagingModePreflightRegister.ObservationMemory && clearReserved.Kind == X64PagingModePreflightInstructionKind.ClearReserved && clearReserved.ByteOffset == 12 && clearReserved.Destination == X64PagingModePreflightRegister.ObservationMemory && flagsCapture.Kind == X64PagingModePreflightInstructionKind.CaptureRflags && flagsCapture.ByteOffset == 20 && flagsCapture.StackDepthSlotsAfter == 2 && flagsStore.Operand == X64PagingModePreflightStubPlan.ObservationRflagsOffset && flagsStore.ByteOffset == 22 && flagsStore.ByteOffset < firstFlagMutation.ByteOffset && completion.Kind == X64PagingModePreflightInstructionKind.StoreCompletion && completion.Operand == X64PagingModePreflightObservation.CompletionSignature && restore.Kind == X64PagingModePreflightInstructionKind.RestoreNonvolatileRbx && restore.Destination == X64PagingModePreflightRegister.Rbx && restore.StackDepthSlotsAfter == 0 && last.Kind == X64PagingModePreflightInstructionKind.Return && last.StackDepthSlotsAfter == 0 && X64PagingModePreflightStubPlan.TryGetInstruction(X64PagingModePreflightStubPlan.InstructionCount, out invalid) == X64PagingModePreflightStatus.InvalidInstructionIndex && !invalid.IsInitialized && !Enum.GetNames<X64PagingModePreflightRegister>().Any(name => name.Contains("Xmm", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TestMaterializeAndReadback()
    {
        void* allocation = NativeMemory.AlignedAlloc((nuint)X64PagingModePreflightStubPlan.StubPageByteLength, (nuint)X64PagingModePreflightStubPlan.StubPageByteLength);
        if (allocation == null)
        {
            return false;
        }

        try
        {
            byte* page = (byte*)allocation;
            Fill(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0xcc);
            X64PagingModePreflightObservation observation = ValidObservation(48);
            X64PagingModePreflightResult state = default;
            byte[] beforeObservation = BytesOf(observation);
            byte[] beforeState = BytesOf(state);
            const ulong entryOffset = 32;
            X64PagingModePreflightStubResult materialized = X64PagingModePreflightStubPlan.TryMaterialize(page, X64PagingModePreflightStubPlan.StubPageByteLength, entryOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize);
            X64PagingModePreflightStubResult validated = X64PagingModePreflightStubPlan.TryValidateMaterialized(page, X64PagingModePreflightStubPlan.StubPageByteLength, entryOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize);
            bool untouched = true;
            for (ulong index = 0; index < X64PagingModePreflightStubPlan.StubPageByteLength; index++)
            {
                bool inside = index >= entryOffset && index < entryOffset + X64PagingModePreflightStubPlan.RequiredByteLength;
                if (!inside && page[index] != 0xcc)
                {
                    untouched = false;
                    break;
                }
            }

            bool exact = true;
            for (ulong index = 0; index < X64PagingModePreflightStubPlan.RequiredByteLength; index++)
            {
                X64PagingModePreflightStubPlan.TryGetExpectedByte(index, out byte expected);
                if (page[entryOffset + index] != expected)
                {
                    exact = false;
                    break;
                }
            }

            bool allOpcodeAndPrefixBytesClassified = true;
            foreach (int opcodeOffset in OpcodeAndPrefixOffsets())
            {
                page[entryOffset + (ulong)opcodeOffset] ^= 1;
                X64PagingModePreflightStubResult classified = X64PagingModePreflightStubPlan.TryValidateMaterialized(page, X64PagingModePreflightStubPlan.StubPageByteLength, entryOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize);
                page[entryOffset + (ulong)opcodeOffset] ^= 1;
                if (classified.Status != X64PagingModePreflightStatus.StubOpcodeMismatch || classified.BytesProcessed != (ulong)opcodeOffset || classified.MismatchOffset != (ulong)opcodeOffset)
                {
                    allOpcodeAndPrefixBytesClassified = false;
                    break;
                }
            }

            page[entryOffset] ^= 1;
            X64PagingModePreflightStubResult opcodeMismatch = X64PagingModePreflightStubPlan.TryValidateMaterialized(page, X64PagingModePreflightStubPlan.StubPageByteLength, entryOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize);
            page[entryOffset] ^= 1;
            page[entryOffset + 33] ^= 1;
            X64PagingModePreflightStubResult secondOpcodeMismatch = X64PagingModePreflightStubPlan.TryValidateMaterialized(page, X64PagingModePreflightStubPlan.StubPageByteLength, entryOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize);
            page[entryOffset + 33] ^= 1;
            page[entryOffset + 28] ^= 1;
            X64PagingModePreflightStubResult modRmMismatch = X64PagingModePreflightStubPlan.TryValidateMaterialized(page, X64PagingModePreflightStubPlan.StubPageByteLength, entryOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize);
            page[entryOffset + 28] ^= 1;
            page[entryOffset + 105] ^= 1;
            X64PagingModePreflightStubResult immediateMismatch = X64PagingModePreflightStubPlan.TryValidateMaterialized(page, X64PagingModePreflightStubPlan.StubPageByteLength, entryOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize);
            return materialized.Succeeded && materialized.Status == X64PagingModePreflightStatus.Success && materialized.Operation == X64PagingModePreflightOperation.Materialize && materialized.DeclaredByteCount == X64PagingModePreflightStubPlan.RequiredByteLength && materialized.BufferCapacity == X64PagingModePreflightStubPlan.StubPageByteLength && materialized.EntryOffset == entryOffset && materialized.EntryAddress == (ulong)page + entryOffset && materialized.BytesProcessed == X64PagingModePreflightStubPlan.RequiredByteLength && materialized.MismatchOffset == X64PagingModePreflightStubResult.NoMismatchOffset && validated.Succeeded && validated.Operation == X64PagingModePreflightOperation.Validate && exact && untouched && allOpcodeAndPrefixBytesClassified && BytesOf(observation).AsSpan().SequenceEqual(beforeObservation) && BytesOf(state).AsSpan().SequenceEqual(beforeState) && !opcodeMismatch.Succeeded && opcodeMismatch.Status == X64PagingModePreflightStatus.StubOpcodeMismatch && opcodeMismatch.BytesProcessed == 0 && opcodeMismatch.MismatchOffset == 0 && !secondOpcodeMismatch.Succeeded && secondOpcodeMismatch.Status == X64PagingModePreflightStatus.StubOpcodeMismatch && secondOpcodeMismatch.BytesProcessed == 33 && secondOpcodeMismatch.MismatchOffset == 33 && !modRmMismatch.Succeeded && modRmMismatch.Status == X64PagingModePreflightStatus.StubByteMismatch && modRmMismatch.BytesProcessed == 28 && modRmMismatch.MismatchOffset == 28 && !immediateMismatch.Succeeded && immediateMismatch.Status == X64PagingModePreflightStatus.StubByteMismatch && immediateMismatch.BytesProcessed == 105 && immediateMismatch.MismatchOffset == 105;
        }
        finally
        {
            NativeMemory.AlignedFree(allocation);
        }
    }

    private static bool TestStubBoundaryFailures()
    {
        void* allocation = NativeMemory.AlignedAlloc((nuint)X64PagingModePreflightStubPlan.StubPageByteLength, (nuint)X64PagingModePreflightStubPlan.StubPageByteLength);
        if (allocation == null)
        {
            return false;
        }

        try
        {
            byte* page = (byte*)allocation;
            Fill(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0xa5);
            byte[] baseline = Copy(page, X64PagingModePreflightStubPlan.StubPageByteLength);
            X64PagingModePreflightObservation observation = ValidObservation(48);
            X64PagingModePreflightResult state = default;
            byte[] observationBefore = BytesOf(observation);
            byte[] stateBefore = BytesOf(state);
            bool ok = StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength - 1, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.InvalidStubByteCount) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength + 1, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.InvalidStubByteCount) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength - 1, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.StubCapacityTooSmall) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength + 1, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.StubCapacityTooLarge) && StubFailure(null, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.MissingStubBuffer) && StubFailure(page + 1, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.MisalignedStubBuffer) && StubFailure((byte*)0xfffffffffffff000UL, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.StubBufferSpanOverflow) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, X64PagingModePreflightStubPlan.StubPageByteLength, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.InvalidEntryOffset) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 1, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.MisalignedEntry) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 4000, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.EntrySpanOverflow) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, null, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.MissingObservationStorage) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize - 1, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.ObservationStorageSizeTooSmall) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize + 1, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.ObservationStorageSizeTooLarge) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, (X64PagingModePreflightObservation*)((byte*)&observation + 1), X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.MisalignedObservationStorage) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, (X64PagingModePreflightObservation*)0xfffffffffffffff8UL, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.ObservationStorageSpanOverflow) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, null, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.MissingStateStorage) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize - 1, X64PagingModePreflightStatus.StateStorageSizeTooSmall) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize + 1, X64PagingModePreflightStatus.StateStorageSizeTooLarge) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, (X64PagingModePreflightResult*)((byte*)&state + 1), X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.MisalignedStateStorage) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, (X64PagingModePreflightResult*)0xfffffffffffffff8UL, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.StateStorageSpanOverflow) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, (X64PagingModePreflightObservation*)(page + 256), X64PagingModePreflightObservation.StructureSize, &state, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.StubObservationOverlap) && StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, (X64PagingModePreflightResult*)(page + 256), X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.StubStateOverlap);
            byte* shared = stackalloc byte[192];
            void* aligned = Align(shared, 8);
            var sharedObservation = (X64PagingModePreflightObservation*)aligned;
            var overlappingState = (X64PagingModePreflightResult*)aligned;
            ok &= StubFailure(page, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, sharedObservation, X64PagingModePreflightObservation.StructureSize, overlappingState, X64PagingModePreflightResult.StructureSize, X64PagingModePreflightStatus.ObservationStateOverlap);
            return ok && Equal(page, baseline, X64PagingModePreflightStubPlan.StubPageByteLength) && BytesOf(observation).AsSpan().SequenceEqual(observationBefore) && BytesOf(state).AsSpan().SequenceEqual(stateBefore);
        }
        finally
        {
            NativeMemory.AlignedFree(allocation);
        }
    }

    private static bool TestPositiveEvaluations()
    {
        foreach (byte width in new byte[]
        {
            36,
            48,
            52
        }

        )
        {
            X64PagingModePreflightResult result = Evaluate(Request(enabled: true), ValidObservation(width));
            if (!result.Succeeded || result.Status != X64PagingModePreflightStatus.Success || result.PhysicalAddressWidth != width || result.PhysicalAddressMask != ((1UL << width) - 1) || result.Cr0 != ValidCr0 || result.RawFirmwareCr3 != ValidCr3 || result.RawCr4 != ValidCr4 || result.Ia32Efer != ValidEfer || result.Rflags != ValidRflags || !result.ProbeEnabled || !result.PagingEnabled || !result.WriteProtectEnabled || !result.PaeEnabled || !result.LongModeEnabled || !result.LongModeActive || !result.NxeEnabled || result.La57Enabled || result.PcideEnabled || !result.CpuidAddressLeafAvailable || !result.OwnedRootAligned || !result.OwnedRootInsideArena || !RawEquals(result.OwnedRoot, OwnedRoot) || !RawEquals(result.TableArenaStart, ArenaStart) || !RawEquals(result.TableArenaEndExclusive, ArenaStart + ArenaLength))
            {
                return false;
            }
        }

        foreach (ulong lowBits in new ulong[]
        {
            0,
            X64PagingModePreflightStubPlan.Cr3PageWriteThroughBit,
            X64PagingModePreflightStubPlan.Cr3PageCacheDisableBit,
            X64PagingModePreflightStubPlan.Cr3AllowedLowBits
        }

        )
        {
            ulong rawCr3 = 0x0000000012345000UL | lowBits;
            X64PagingModePreflightResult cr3Result = Evaluate(Request(enabled: true), Observation(rawCr3: rawCr3));
            if (!cr3Result.Succeeded || cr3Result.RawFirmwareCr3 != rawCr3)
            {
                return false;
            }
        }

        X64PagingModePreflightObservation pgeObservation = X64PagingModePreflightObservation.CreateObserved(ValidCr0, ValidCr3, ValidCr4 | X64PagingModePreflightStubPlan.Cr4PgeBit, ValidEfer, X64PagingModePreflightStubPlan.CpuidAddressSizesLeaf, (uint)(48 | (48 << 8)), ValidRflags);
        X64PagingModePreflightResult pge = Evaluate(Request(enabled: true), pgeObservation);
        return pge.Succeeded && (pge.RawCr4 & X64PagingModePreflightStubPlan.Cr4PgeBit) != 0;
    }

    private static bool TestArchitecturalFailures()
    {
        return EvaluateStatus(Observation(cr0: ValidCr0 & ~X64PagingModePreflightStubPlan.Cr0PagingBit), X64PagingModePreflightStatus.PagingNotEnabled) && EvaluateStatus(Observation(cr0: ValidCr0 & ~X64PagingModePreflightStubPlan.Cr0WriteProtectBit), X64PagingModePreflightStatus.WriteProtectNotEnabled) && EvaluateStatus(Observation(cr4: ValidCr4 & ~X64PagingModePreflightStubPlan.Cr4PaeBit), X64PagingModePreflightStatus.PaeNotEnabled) && EvaluateStatus(Observation(efer: ValidEfer & ~X64PagingModePreflightStubPlan.EferLongModeEnableBit), X64PagingModePreflightStatus.LongModeNotEnabled) && EvaluateStatus(Observation(efer: ValidEfer & ~X64PagingModePreflightStubPlan.EferLongModeActiveBit), X64PagingModePreflightStatus.LongModeNotActive) && EvaluateStatus(Observation(efer: ValidEfer & ~X64PagingModePreflightStubPlan.EferNoExecuteEnableBit), X64PagingModePreflightStatus.NxeNotEnabled) && EvaluateStatus(Observation(cr4: ValidCr4 | X64PagingModePreflightStubPlan.Cr4La57Bit), X64PagingModePreflightStatus.FiveLevelPagingNotSupported) && EvaluateStatus(Observation(cr4: ValidCr4 | X64PagingModePreflightStubPlan.Cr4PcideBit), X64PagingModePreflightStatus.PcideNotSupported) && EvaluateStatus(Observation(maximumLeaf: 0x80000007), X64PagingModePreflightStatus.CpuidAddressLeafUnavailable) && EvaluateStatus(Observation(width: 35), X64PagingModePreflightStatus.UnsupportedPhysicalAddressWidth) && EvaluateStatus(Observation(width: 53), X64PagingModePreflightStatus.UnsupportedPhysicalAddressWidth) && EvaluateStatus(Observation(rflags: 0x200), X64PagingModePreflightStatus.InvalidRflags) && EvaluateStatus(Observation(rawCr3: ValidCr3 | 1), X64PagingModePreflightStatus.InvalidFirmwareCr3Bits) && EvaluateStatus(Observation(rawCr3: ValidCr3 | (1UL << 5)), X64PagingModePreflightStatus.InvalidFirmwareCr3Bits) && EvaluateStatus(Observation(rawCr3: ValidCr3 | (1UL << 11)), X64PagingModePreflightStatus.InvalidFirmwareCr3Bits) && EvaluateStatus(Observation(rawCr3: ValidCr3 | (1UL << 40), width: 36), X64PagingModePreflightStatus.InvalidFirmwareCr3Bits) && EvaluateStatus(Observation(rawCr3: X64PagingModePreflightStubPlan.Cr3AllowedLowBits), X64PagingModePreflightStatus.InvalidFirmwareCr3Base);
    }

    private static bool TestRootAndArenaFailures()
    {
        X64PagingModePreflightObservation valid = ValidObservation(48);
        return EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(default, Physical(ArenaStart), ArenaLength), valid, X64PagingModePreflightStatus.InvalidOwnedRoot) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(OwnedRoot + 1), Physical(ArenaStart), ArenaLength), valid, X64PagingModePreflightStatus.MisalignedOwnedRoot) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(OwnedRoot), default, ArenaLength), valid, X64PagingModePreflightStatus.InvalidTableArena) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(OwnedRoot), Physical(ArenaStart + 1), ArenaLength), valid, X64PagingModePreflightStatus.MisalignedTableArena) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(OwnedRoot), Physical(ArenaStart), 0), valid, X64PagingModePreflightStatus.TableArenaLengthZero) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(OwnedRoot), Physical(ArenaStart), ArenaLength + 1), valid, X64PagingModePreflightStatus.TableArenaLengthUnaligned) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(0xfffffffffffff000UL), Physical(0xfffffffffffff000UL), 4096), valid, X64PagingModePreflightStatus.TableArenaOverflow) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(0x0001000000000000UL), Physical(0x0001000000000000UL), 4096), valid, X64PagingModePreflightStatus.TableArenaOutsidePhysicalWidth) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(0x0001000000000000UL), Physical(ArenaStart), ArenaLength), valid, X64PagingModePreflightStatus.OwnedRootOutsidePhysicalWidth) && EvaluateStatus(X64PagingModePreflightRequest.CreateEnabled(Physical(ArenaStart - 4096), Physical(ArenaStart), ArenaLength), valid, X64PagingModePreflightStatus.OwnedRootOutsideArena) && EvaluateStatus(X64PagingModePreflightRequest.CreateDisabled(Physical(OwnedRoot), Physical(ArenaStart), ArenaLength), valid, X64PagingModePreflightStatus.ProbeDisabled);
    }

    private static bool TestEvaluationPointerFailures()
    {
        X64PagingModePreflightRequest request = Request(enabled: true);
        X64PagingModePreflightObservation observation = ValidObservation(48);
        X64PagingModePreflightResult state = default;
        bool ok = EvaluatePointerStatus(&request, 0, 1, &observation, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.InvalidRequestCount) && EvaluatePointerStatus(&request, 1, 1, &observation, 0, 1, &state, 1, 1, X64PagingModePreflightStatus.InvalidObservationCount) && EvaluatePointerStatus(&request, 1, 1, &observation, 1, 1, &state, 0, 1, X64PagingModePreflightStatus.InvalidStateCount) && EvaluatePointerStatus(&request, 1, 2, &observation, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.CapacityExceedsLimit) && EvaluatePointerStatus(&request, 1, 0, &observation, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.RequestCapacityTooSmall) && EvaluatePointerStatus(&request, 1, 1, &observation, 1, 0, &state, 1, 1, X64PagingModePreflightStatus.ObservationCapacityTooSmall) && EvaluatePointerStatus(&request, 1, 1, &observation, 1, 1, &state, 1, 0, X64PagingModePreflightStatus.StateCapacityTooSmall) && EvaluatePointerStatus(null, 1, 1, &observation, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.MissingRequest) && EvaluatePointerStatus(&request, 1, 1, null, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.MissingObservation) && EvaluatePointerStatus(&request, 1, 1, &observation, 1, 1, null, 1, 1, X64PagingModePreflightStatus.MissingState) && EvaluatePointerStatus((X64PagingModePreflightRequest*)0xfffffffffffffff8UL, 1, 1, &observation, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.RequestSpanOverflow) && EvaluatePointerStatus(&request, 1, 1, (X64PagingModePreflightObservation*)0xfffffffffffffff8UL, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.ObservationSpanOverflow) && EvaluatePointerStatus(&request, 1, 1, &observation, 1, 1, (X64PagingModePreflightResult*)0xfffffffffffffff8UL, 1, 1, X64PagingModePreflightStatus.StateSpanOverflow);
        ok &= EvaluatePointerStatus((X64PagingModePreflightRequest*)((byte*)&request + 1), 1, 1, &observation, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.MisalignedRequest);
        ok &= EvaluatePointerStatus(&request, 1, 1, (X64PagingModePreflightObservation*)((byte*)&observation + 1), 1, 1, &state, 1, 1, X64PagingModePreflightStatus.MisalignedObservation);
        ok &= EvaluatePointerStatus(&request, 1, 1, &observation, 1, 1, (X64PagingModePreflightResult*)((byte*)&state + 1), 1, 1, X64PagingModePreflightStatus.MisalignedState);
        byte* shared = stackalloc byte[256];
        void* aligned = Align(shared, 8);
        var requestAlias = (X64PagingModePreflightRequest*)aligned;
        var observationAlias = (X64PagingModePreflightObservation*)aligned;
        var stateAlias = (X64PagingModePreflightResult*)aligned;
        ok &= EvaluatePointerStatus(requestAlias, 1, 1, observationAlias, 1, 1, &state, 1, 1, X64PagingModePreflightStatus.RequestObservationOverlap);
        ok &= EvaluatePointerStatus(requestAlias, 1, 1, &observation, 1, 1, stateAlias, 1, 1, X64PagingModePreflightStatus.RequestStateOverlap);
        ok &= EvaluatePointerStatus(&request, 1, 1, observationAlias, 1, 1, stateAlias, 1, 1, X64PagingModePreflightStatus.EvaluationObservationStateOverlap);
        X64PagingModePreflightResult canary = Evaluate(request, observation);
        byte[] before = BytesOf(canary);
        X64PagingModePreflightResult returned = X64PagingModePreflightStubPlan.Evaluate(&request, 0, 1, &observation, 1, 1, &canary, 1, 1);
        return ok && returned.Status == X64PagingModePreflightStatus.InvalidRequestCount && BytesOf(canary).AsSpan().SequenceEqual(before);
    }

    private static bool TestHostilePodAndDefaults()
    {
        X64PagingModePreflightRequest request = Request(enabled: true);
        X64PagingModePreflightObservation observation = ValidObservation(48);
        X64PagingModePreflightResult success = Evaluate(request, observation);
        X64PagingModePreflightRequest hostileRequestFlag = OverwriteByte(request, 24, 2);
        X64PagingModePreflightRequest hostileRequestEnabled = OverwriteByte(request, 25, 2);
        X64PagingModePreflightRequest hostileRequestReserved = OverwriteByte(request, 26, 1);
        X64PagingModePreflightObservation hostileCompletion = OverwriteByte(observation, 48, 0);
        X64PagingModePreflightObservation hostileObservationReserved = OverwriteByte(observation, 52, 1);
        X64PagingModePreflightObservation zeroWithCompletion = OverwriteByte(default(X64PagingModePreflightObservation), 48, 1);
        X64PagingModePreflightObservation zeroWithReserved = OverwriteByte(default(X64PagingModePreflightObservation), 52, 1);
        X64PagingModePreflightResult hostileResultStatus = OverwriteByte(success, 73, 255);
        X64PagingModePreflightResult hostileResultFlag = OverwriteByte(success, 75, 2);
        X64PagingModePreflightResult hostileResultReserved0 = OverwriteByte(success, 87, 1);
        X64PagingModePreflightResult hostileResultReserved1 = OverwriteByte(success, 88, 1);
        X64PagingModePreflightResult hostileResultWidth = OverwriteByte(success, 74, 35);
        X64PagingModePreflightResult hostileResultMask = OverwriteByte(success, 64, 0xfe);
        X64PagingModePreflightResult hostileResultCr0 = OverwriteByte(success, 27, 0);
        X64PagingModePreflightResult hostileResultRawCr3 = OverwriteByte(success, 32, 0x19);
        X64PagingModePreflightResult hostileResultRawCr4 = OverwriteByte(success, 42, 2);
        X64PagingModePreflightResult hostileResultEfer = OverwriteByte(success, 49, 0x09);
        X64PagingModePreflightResult hostileResultRflags = OverwriteByte(success, 56, 0);
        X64PagingModePreflightResult hostileResultRoot = OverwriteByte(success, 0, 1);
        X64PagingModePreflightResult hostileResultArenaStart = OverwriteByte(success, 8, 1);
        X64PagingModePreflightResult hostileResultArenaEnd = OverwriteByte(success, 18, 0x1f);
        X64PagingModePreflightInstruction instruction;
        X64PagingModePreflightStubPlan.TryGetInstruction(0, out instruction);
        X64PagingModePreflightInstruction hostileInstruction = OverwriteByte(instruction, 19, 1);
        X64PagingModePreflightStubResult validStub = MaterializeForPodTest(ref observation, ref success);
        X64PagingModePreflightStubResult hostileStubStatus = OverwriteByte(validStub, 49, 255);
        X64PagingModePreflightStubResult hostileStubOperation = OverwriteByte(validStub, 50, 255);
        X64PagingModePreflightStubResult hostileStubExact = OverwriteByte(validStub, 51, 2);
        X64PagingModePreflightStubResult hostileStubReserved = OverwriteByte(validStub, 52, 1);
        X64PagingModePreflightStubResult hostileStubDeclaredByteCount = OverwriteUInt64(validStub, 0, X64PagingModePreflightStubPlan.RequiredByteLength - 1);
        X64PagingModePreflightStubResult hostileStubCapacity = OverwriteUInt64(validStub, 8, X64PagingModePreflightStubPlan.StubPageByteLength - 1);
        X64PagingModePreflightStubResult hostileStubMisalignedEntryOffset = OverwriteUInt64(validStub, 16, 1);
        X64PagingModePreflightStubResult hostileStubOutsideEntryOffset = OverwriteUInt64(validStub, 16, X64PagingModePreflightStubPlan.StubPageByteLength);
        X64PagingModePreflightStubResult hostileStubZeroEntryAddress = OverwriteUInt64(validStub, 24, 0);
        X64PagingModePreflightStubResult hostileStubMisalignedEntryAddress = OverwriteUInt64(validStub, 24, validStub.EntryAddress + 1);
        X64PagingModePreflightStubResult hostileStubAddressUnderflow = OverwriteUInt64(OverwriteUInt64(validStub, 16, 32), 24, 16);
        X64PagingModePreflightStubResult hostileStubZeroBufferBase = OverwriteUInt64(OverwriteUInt64(validStub, 16, 16), 24, 16);
        X64PagingModePreflightStubResult hostileStubBufferSpanOverflow = OverwriteUInt64(validStub, 24, ~0UL - 15);
        ulong lateEntryOffset = X64PagingModePreflightStubPlan.StubPageByteLength - X64PagingModePreflightStubPlan.RequiredEntryAlignment;
        X64PagingModePreflightStubResult hostileStubEntrySpan = OverwriteUInt64(OverwriteUInt64(validStub, 16, lateEntryOffset), 24, validStub.EntryAddress + lateEntryOffset);
        X64PagingModePreflightStubResult hostileStubBytesProcessed = OverwriteUInt64(validStub, 32, X64PagingModePreflightStubPlan.RequiredByteLength - 1);
        X64PagingModePreflightStubResult hostileStubMismatchOffset = OverwriteUInt64(validStub, 40, 0);
        return EvaluateStatus(default, observation, X64PagingModePreflightStatus.RequestNotInitialized) && EvaluateStatus(hostileRequestFlag, observation, X64PagingModePreflightStatus.InvalidRequestState) && EvaluateStatus(hostileRequestEnabled, observation, X64PagingModePreflightStatus.InvalidRequestState) && EvaluateStatus(hostileRequestReserved, observation, X64PagingModePreflightStatus.InvalidRequestState) && EvaluateStatus(request, default, X64PagingModePreflightStatus.ObservationNotInitialized) && EvaluateStatus(request, hostileCompletion, X64PagingModePreflightStatus.InvalidObservationState) && EvaluateStatus(request, hostileObservationReserved, X64PagingModePreflightStatus.InvalidObservationState) && EvaluateStatus(request, zeroWithCompletion, X64PagingModePreflightStatus.InvalidObservationState) && EvaluateStatus(request, zeroWithReserved, X64PagingModePreflightStatus.InvalidObservationState) && success.IsInitialized && success.Succeeded && !hostileResultStatus.IsInitialized && !hostileResultStatus.Succeeded && !hostileResultFlag.IsInitialized && !hostileResultReserved0.IsInitialized && !hostileResultReserved1.IsInitialized && !hostileResultWidth.IsInitialized && !hostileResultWidth.Succeeded && !hostileResultMask.IsInitialized && !hostileResultMask.Succeeded && !hostileResultCr0.IsInitialized && !hostileResultCr0.Succeeded && !hostileResultRawCr3.IsInitialized && !hostileResultRawCr3.Succeeded && !hostileResultRawCr4.IsInitialized && !hostileResultRawCr4.Succeeded && !hostileResultEfer.IsInitialized && !hostileResultEfer.Succeeded && !hostileResultRflags.IsInitialized && !hostileResultRflags.Succeeded && !hostileResultRoot.IsInitialized && !hostileResultRoot.Succeeded && !hostileResultArenaStart.IsInitialized && !hostileResultArenaStart.Succeeded && !hostileResultArenaEnd.IsInitialized && !hostileResultArenaEnd.Succeeded && instruction.IsInitialized && !hostileInstruction.IsInitialized && validStub.IsInitialized && validStub.Succeeded && !hostileStubStatus.IsInitialized && !hostileStubStatus.Succeeded && !hostileStubOperation.IsInitialized && !hostileStubExact.IsInitialized && !hostileStubReserved.IsInitialized && !hostileStubDeclaredByteCount.Succeeded && !hostileStubCapacity.Succeeded && !hostileStubMisalignedEntryOffset.Succeeded && !hostileStubOutsideEntryOffset.Succeeded && !hostileStubZeroEntryAddress.Succeeded && !hostileStubMisalignedEntryAddress.Succeeded && !hostileStubAddressUnderflow.Succeeded && !hostileStubZeroBufferBase.Succeeded && !hostileStubBufferSpanOverflow.Succeeded && !hostileStubEntrySpan.Succeeded && !hostileStubBytesProcessed.Succeeded && !hostileStubMismatchOffset.Succeeded;
    }

    private static bool TestSourceAndNoStdLibBoundary()
    {
        string root = FindRepositoryRoot();
        string productionPath = Path.Combine(root, "src", "OS.Kernel", "Memory", "X64PagingModePreflightStubPlan.cs");
        string addressPath = Path.Combine(root, "src", "OS.Kernel", "Boot", "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
        if (!File.Exists(productionPath) || !File.Exists(addressPath))
        {
            return false;
        }

        string source = RemoveComments(File.ReadAllText(productionPath));
        string[] forbidden = ["using System;", "System.FlagsAttribute", "[Flags", "Span<", "ReadOnlySpan<", "stackalloc", "throw ", "try {", "catch ", "finally", "typeof(", "dynamic ", "string ", "object ", "List<", "Dictionary<", "RuntimeExport", "EfiEntry", "BootSequence", "Probes", "ulong.MaxValue", "delegate*"];
        if (forbidden.Any(fragment => source.Contains(fragment, StringComparison.Ordinal)) || !source.Contains("public static unsafe class X64PagingModePreflightStubPlan", StringComparison.Ordinal) || !source.Contains("X64PagingModePreflightRequest* request", StringComparison.Ordinal) || !source.Contains("X64PagingModePreflightObservation* observation", StringComparison.Ordinal) || !source.Contains("X64PagingModePreflightResult* state", StringComparison.Ordinal) || !source.Contains("requestCapacity", StringComparison.Ordinal) || !source.Contains("observationCapacity", StringComparison.Ordinal) || !source.Contains("stateCapacity", StringComparison.Ordinal) || !source.Contains("~0UL", StringComparison.Ordinal) || !source.Contains("public const bool UsesXmmRegisters = false;", StringComparison.Ordinal) || source.Contains("0x0f, 0x22, 0xd8", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] declarations = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories).Where(path => !HasDirectorySegment(path, "bin") && !HasDirectorySegment(path, "obj")).Where(path => Regex.IsMatch(RemoveComments(File.ReadAllText(path)), @"\bclass\s+X64PagingModePreflightStubPlan\b", RegexOptions.CultureInvariant)).ToArray();
        if (declarations.Length != 1 || !Path.GetFullPath(declarations[0]).Equals(Path.GetFullPath(productionPath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        NoStdLibBuildResult build = BuildNoStdLibClosure(root, addressPath, productionPath);
        if (build.TimedOut || build.ExitCode != 0 || !HasZeroAssemblyReferences(build.AssemblyBytes))
        {
            Console.WriteLine("M01_013_PREFLIGHT_NOSTDLIB_DIAGNOSTIC " + Normalize(build.Output));
            return false;
        }

        return true;
    }

    private static X64PagingModePreflightObservation ValidObservation(byte width)
    {
        return Observation(width: width);
    }

    private static X64PagingModePreflightObservation Observation(ulong? cr0 = null, ulong? rawCr3 = null, ulong? cr4 = null, ulong? efer = null, uint? maximumLeaf = null, byte width = 48, ulong? rflags = null)
    {
        return X64PagingModePreflightObservation.CreateObserved(cr0 ?? ValidCr0, rawCr3 ?? ValidCr3, cr4 ?? ValidCr4, efer ?? ValidEfer, maximumLeaf ?? X64PagingModePreflightStubPlan.CpuidAddressSizesLeaf, (uint)(width | (48 << 8)), rflags ?? ValidRflags);
    }

    private static X64PagingModePreflightRequest Request(bool enabled)
    {
        return enabled ? X64PagingModePreflightRequest.CreateEnabled(Physical(OwnedRoot), Physical(ArenaStart), ArenaLength) : X64PagingModePreflightRequest.CreateDisabled(Physical(OwnedRoot), Physical(ArenaStart), ArenaLength);
    }

    private static X64PagingModePreflightResult Evaluate(X64PagingModePreflightRequest request, X64PagingModePreflightObservation observation)
    {
        X64PagingModePreflightResult state = default;
        X64PagingModePreflightResult result = X64PagingModePreflightStubPlan.Evaluate(&request, 1, 1, &observation, 1, 1, &state, 1, 1);
        if (!SameBytes(result, state))
        {
            throw new InvalidOperationException("caller-owned state did not match returned evidence");
        }

        return result;
    }

    private static bool EvaluateStatus(X64PagingModePreflightObservation observation, X64PagingModePreflightStatus expected)
    {
        return EvaluateStatus(Request(enabled: true), observation, expected);
    }

    private static bool EvaluateStatus(X64PagingModePreflightRequest request, X64PagingModePreflightObservation observation, X64PagingModePreflightStatus expected)
    {
        X64PagingModePreflightResult result = Evaluate(request, observation);
        return result.IsInitialized && !result.Succeeded && result.Status == expected;
    }

    private static bool EvaluatePointerStatus(X64PagingModePreflightRequest* request, ulong requestCount, ulong requestCapacity, X64PagingModePreflightObservation* observation, ulong observationCount, ulong observationCapacity, X64PagingModePreflightResult* state, ulong stateCount, ulong stateCapacity, X64PagingModePreflightStatus expected)
    {
        X64PagingModePreflightResult result = X64PagingModePreflightStubPlan.Evaluate(request, requestCount, requestCapacity, observation, observationCount, observationCapacity, state, stateCount, stateCapacity);
        return result.IsInitialized && !result.Succeeded && result.Status == expected;
    }

    private static bool StubFailure(byte* buffer, ulong bufferCapacity, ulong entryOffset, ulong declaredByteCount, X64PagingModePreflightObservation* observation, ulong observationByteLength, X64PagingModePreflightResult* state, ulong stateByteLength, X64PagingModePreflightStatus expected)
    {
        X64PagingModePreflightStubResult result = X64PagingModePreflightStubPlan.TryMaterialize(buffer, bufferCapacity, entryOffset, declaredByteCount, observation, observationByteLength, state, stateByteLength);
        return result.IsInitialized && !result.Succeeded && result.Status == expected && result.BytesProcessed == 0 && !result.ExactBytes;
    }

    private static X64PagingModePreflightStubResult MaterializeForPodTest(ref X64PagingModePreflightObservation observation, ref X64PagingModePreflightResult state)
    {
        void* allocation = NativeMemory.AlignedAlloc((nuint)X64PagingModePreflightStubPlan.StubPageByteLength, (nuint)X64PagingModePreflightStubPlan.StubPageByteLength);
        if (allocation == null)
        {
            return default;
        }

        try
        {
            fixed (X64PagingModePreflightObservation* observationPointer = &observation)
            fixed (X64PagingModePreflightResult* statePointer = &state)
            {
                return X64PagingModePreflightStubPlan.TryMaterialize((byte*)allocation, X64PagingModePreflightStubPlan.StubPageByteLength, 0, X64PagingModePreflightStubPlan.RequiredByteLength, observationPointer, X64PagingModePreflightObservation.StructureSize, statePointer, X64PagingModePreflightResult.StructureSize);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(allocation);
        }
    }

    private static bool TouchesForbiddenNonvolatile(X64PagingModePreflightInstruction instruction)
    {
        // The semantic register vocabulary has no RBP, RSI, RDI, R12-R15, or
        // XMM entries. RBX is the sole nonvolatile touched and has one
        // explicit preserve/restore pair.
        if (instruction.Source == X64PagingModePreflightRegister.Rbx || instruction.Destination == X64PagingModePreflightRegister.Rbx)
        {
            return instruction.Kind != X64PagingModePreflightInstructionKind.PreserveNonvolatileRbx && instruction.Kind != X64PagingModePreflightInstructionKind.RestoreNonvolatileRbx;
        }

        return false;
    }

    private static byte[] ExpectedBytes()
    {
        return [0x53, 0x49, 0x89, 0xc8, 0x41, 0xc7, 0x40, 0x30, 0x00, 0x00, 0x00, 0x00, 0x41, 0xc7, 0x40, 0x34, 0x00, 0x00, 0x00, 0x00, 0x9c, 0x58, 0x49, 0x89, 0x40, 0x28, 0x0f, 0x20, 0xc0, 0x49, 0x89, 0x00, 0x0f, 0x20, 0xd8, 0x49, 0x89, 0x40, 0x08, 0x0f, 0x20, 0xe0, 0x49, 0x89, 0x40, 0x10, 0xb9, 0x80, 0x00, 0x00, 0xc0, 0x0f, 0x32, 0x48, 0xc1, 0xe2, 0x20, 0x48, 0x09, 0xd0, 0x49, 0x89, 0x40, 0x18, 0xb8, 0x00, 0x00, 0x00, 0x80, 0x0f, 0xa2, 0x41, 0x89, 0x40, 0x20, 0x3d, 0x08, 0x00, 0x00, 0x80, 0x72, 0x0d, 0xb8, 0x08, 0x00, 0x00, 0x80, 0x0f, 0xa2, 0x41, 0x89, 0x40, 0x24, 0xeb, 0x06, 0x31, 0xc0, 0x41, 0x89, 0x40, 0x24, 0x41, 0xc7, 0x40, 0x30, 0x50, 0x4c, 0x46, 0x31, 0x5b, 0xc3];
    }

    private static int[] OpcodeAndPrefixOffsets()
    {
        return [0, 1, 2, 4, 5, 12, 13, 20, 21, 22, 23, 26, 27, 29, 30, 32, 33, 35, 36, 39, 40, 42, 43, 46, 51, 52, 53, 54, 57, 58, 60, 61, 64, 69, 70, 71, 72, 75, 80, 82, 87, 88, 89, 90, 93, 95, 97, 98, 101, 102, 109, 110];
    }

    private static bool Matches(byte[] source, int offset, byte[] expected)
    {
        return offset >= 0 && offset + expected.Length <= source.Length && source.AsSpan(offset, expected.Length).SequenceEqual(expected);
    }

    private static int CountSequence(byte[] source, byte[] expected)
    {
        int count = 0;
        for (int index = 0; index + expected.Length <= source.Length; index++)
        {
            if (source.AsSpan(index, expected.Length).SequenceEqual(expected))
            {
                count++;
            }
        }

        return count;
    }

    private static PhysicalAddress Physical(ulong value)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateFromHardwareRegisterBoundary(value, out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("invalid test physical address 0x" + value.ToString("X16"));
        }

        return address;
    }

    private static bool RawEquals(PhysicalAddress address, ulong expected)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(out ulong actual) == AddressValueStatus.Success && actual == expected;
    }

    private static bool HasExactByteEnum<T>((string Name, byte Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
        {
            return false;
        }

        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();
        if (names.Length != expected.Length || values.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(names[index], expected[index].Name, StringComparison.Ordinal) || Convert.ToByte(values[index]) != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReadOnlySequentialUnmanaged<T>()
        where T : unmanaged
    {
        Type type = typeof(T);
        StructLayoutAttribute? layout = type.StructLayoutAttribute;
        return type.IsValueType && !RuntimeHelpers.IsReferenceOrContainsReferences<T>() && type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false) && layout is not null && layout.Value == LayoutKind.Sequential;
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
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        ((byte*)&value)[offset] = replacement;
        return value;
    }

    private static T OverwriteUInt64<T>(T value, int offset, ulong replacement)
        where T : unmanaged
    {
        if (offset < 0 || offset > sizeof(T) - sizeof(ulong))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        for (int index = 0; index < sizeof(ulong); index++)
        {
            ((byte*)&value)[offset + index] = (byte)(replacement >> (index * 8));
        }

        return value;
    }

    private static byte[] BytesOf<T>(T value)
        where T : unmanaged
    {
        byte[] bytes = new byte[sizeof(T)];
        fixed (byte* destination = bytes)
        {
            byte* source = (byte*)&value;
            for (int index = 0; index < bytes.Length; index++)
            {
                destination[index] = source[index];
            }
        }

        return bytes;
    }

    private static bool SameBytes<T>(T left, T right)
        where T : unmanaged
    {
        byte* leftBytes = (byte*)&left;
        byte* rightBytes = (byte*)&right;
        for (int index = 0; index < sizeof(T); index++)
        {
            if (leftBytes[index] != rightBytes[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void Fill(byte* destination, ulong length, byte value)
    {
        for (ulong index = 0; index < length; index++)
        {
            destination[index] = value;
        }
    }

    private static byte[] Copy(byte* source, ulong length)
    {
        byte[] copy = new byte[(int)length];
        for (ulong index = 0; index < length; index++)
        {
            copy[(int)index] = source[index];
        }

        return copy;
    }

    private static bool Equal(byte* source, byte[] expected, ulong length)
    {
        if ((ulong)expected.Length != length)
        {
            return false;
        }

        for (ulong index = 0; index < length; index++)
        {
            if (source[index] != expected[(int)index])
            {
                return false;
            }
        }

        return true;
    }

    private static void* Align(void* pointer, ulong alignment)
    {
        ulong raw = (ulong)pointer;
        return (void*)((raw + alignment - 1) & ~(alignment - 1));
    }

    private static string RemoveComments(string source)
    {
        return Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    private static bool HasDirectorySegment(string path, string segment)
    {
        string marker = Path.DirectorySeparatorChar + segment + Path.DirectorySeparatorChar;
        return path.Contains(marker, StringComparison.OrdinalIgnoreCase);
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
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find CLROS.slnx from the focused test base.");
    }

    private static NoStdLibBuildResult BuildNoStdLibClosure(string repositoryRoot, string addressPath, string productionPath)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "clros-m01-013-preflight-" + Guid.NewGuid().ToString("N"));
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
                    public abstract class Delegate : Object { }
                    public abstract class MulticastDelegate : Delegate { }
                    public class Attribute : Object { }
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
                        public AttributeUsageAttribute(AttributeTargets validOn) { }
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
                            OS.Boot.AddressValueStatus rootStatus =
                                OS.Boot.PhysicalAddress
                                    .TryCreateAlignedFromHardwareRegisterBoundary(
                                        0x200000,
                                        4096,
                                        out OS.Boot.PhysicalAddress root);
                            OS.Boot.AddressValueStatus arenaStatus =
                                OS.Boot.PhysicalAddress
                                    .TryCreateAlignedFromHardwareRegisterBoundary(
                                        0x1f0000,
                                        4096,
                                        out OS.Boot.PhysicalAddress arena);
                            if (rootStatus != OS.Boot.AddressValueStatus.Success
                                || arenaStatus != OS.Boot.AddressValueStatus.Success)
                            {
                                return 0;
                            }

                            OS.Kernel.Memory.X64PagingModePreflightRequest request =
                                OS.Kernel.Memory.X64PagingModePreflightRequest
                                    .CreateEnabled(root, arena, 0x40000);
                            OS.Kernel.Memory.X64PagingModePreflightObservation observation =
                                OS.Kernel.Memory.X64PagingModePreflightObservation
                                    .CreateObserved(
                                        (1UL << 31) | (1UL << 16),
                                        0x12345018,
                                        1UL << 5,
                                        (1UL << 8) | (1UL << 10) | (1UL << 11),
                                        0x80000008,
                                        48,
                                        0x202);
                            OS.Kernel.Memory.X64PagingModePreflightResult state = default;
                            OS.Kernel.Memory.X64PagingModePreflightResult result =
                                OS.Kernel.Memory.X64PagingModePreflightStubPlan.Evaluate(
                                    &request,
                                    1,
                                    1,
                                    &observation,
                                    1,
                                    1,
                                    &state,
                                    1,
                                    1);
                            byte* stub = stackalloc byte[4096];
                            OS.Kernel.Memory.X64PagingModePreflightStubResult bytes =
                                OS.Kernel.Memory.X64PagingModePreflightStubPlan
                                    .TryMaterialize(
                                        stub,
                                        4096,
                                        0,
                                        OS.Kernel.Memory
                                            .X64PagingModePreflightStubPlan
                                            .RequiredByteLength,
                                        &observation,
                                        OS.Kernel.Memory
                                            .X64PagingModePreflightObservation
                                            .StructureSize,
                                        &state,
                                        OS.Kernel.Memory
                                            .X64PagingModePreflightResult
                                            .StructureSize);
                            return result.Succeeded && bytes.Succeeded
                                ? result.RawFirmwareCr3
                                : 0;
                        }
                    }
                }
                """, new UTF8Encoding(false));
            string project = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.PagingModePreflightCompileClosure</AssemblyName>
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
                    <Compile Include="__ADDRESS__/AddressValueStatus.cs" Link="Boot/AddressValueStatus.cs" />
                <Compile Include="__ADDRESS__/PhysicalAddress.cs" Link="Boot/PhysicalAddress.cs" />
                <Compile Include="__ADDRESS__/VirtualAddress.cs" Link="Boot/VirtualAddress.cs" />
                <Compile Include="__ADDRESS__/DeviceAddress.cs" Link="Boot/DeviceAddress.cs" />
                <Compile Include="__ADDRESS__/AddressValueOperations.cs" Link="Boot/AddressValueOperations.cs" />
                    <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightInstructionKind.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightInstructionKind.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightRegister.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightRegister.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightOperation.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightOperation.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightStatus.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightStatus.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightInstruction.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightInstruction.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightObservation.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightObservation.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightRequest.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightRequest.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightResult.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightResult.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.X64PagingModePreflightStubResult.cs" Link="Memory/X64PagingModePreflightStubPlan.X64PagingModePreflightStubResult.cs" />
                <Compile Include="__PREFLIGHT__/X64PagingModePreflightStubPlan.cs" Link="Memory/X64PagingModePreflightStubPlan.cs" />
                    <Compile Include="Probe.cs" />
                  </ItemGroup>
                  <Target Name="CustomizeReferences"
                          BeforeTargets="BeforeCompile"
                          AfterTargets="FindReferenceAssembliesForReferences">
                    <ItemGroup>
                      <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                      <ReferencePath Remove="@(ReferencePath)" />
                    </ItemGroup>
                  </Target>
                </Project>
                """.Replace("__ADDRESS__", EscapeXml(Path.GetDirectoryName(addressPath)!), StringComparison.Ordinal).Replace("__PREFLIGHT__", EscapeXml(Path.GetDirectoryName(productionPath)!), StringComparison.Ordinal);
            string projectPath = Path.Combine(workspace, "M01.013.Preflight.NoStdLib.csproj");
            File.WriteAllText(projectPath, project, new UTF8Encoding(false));
            ProcessStartInfo start = new("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workspace
            };
            start.ArgumentList.Add("build");
            start.ArgumentList.Add(projectPath);
            start.ArgumentList.Add("--nologo");
            start.ArgumentList.Add("-m:1");
            start.ArgumentList.Add("--no-dependencies");
            start.ArgumentList.Add("--no-incremental");
            start.ArgumentList.Add("--disable-build-servers");
            start.ArgumentList.Add("-v:minimal");
            start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
            start.ArgumentList.Add("-p:NuGetAudit=false");
            start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            start.Environment["DOTNET_NOLOGO"] = "1";
            start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
            start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
            start.Environment["NUGET_XMLDOC_MODE"] = "skip";
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("failed to start nostdlib compiler");
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
            string assemblyPath = Path.Combine(workspace, "out", "CLROS.M01.PagingModePreflightCompileClosure.dll");
            byte[] assembly = File.Exists(assemblyPath) ? File.ReadAllBytes(assemblyPath) : [];
            return new NoStdLibBuildResult(exited ? process.ExitCode : -1, !exited, output, assembly);
        }
        finally
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch
            {
                // A failed cleanup is not product evidence and must not hide
                // the compile result.
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
        {
            return false;
        }

        using MemoryStream stream = new(assembly, writable: false);
        using PEReader reader = new(stream);
        if (!reader.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = reader.GetMetadataReader();
        return metadata.AssemblyReferences.Count == 0;
    }
}
