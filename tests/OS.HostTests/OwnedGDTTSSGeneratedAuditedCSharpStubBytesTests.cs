using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Diagnostics;

internal static unsafe partial class OwnedGDTTSSGeneratedAuditedCSharpStubBytesTests
{
    private const string TestName = "m02.009";
    private const string ExpectedSha256 = "424C98FE039EB46B66EC8C3D8BB954EC7BBB24E6434817079F9B2E17478B8504";
    private const string SuccessMarker = "CLROS_M02_009_READY gdtr=true tr=true gdtBytes=40 tssBytes=104 " + "gdtrLimit=39 trSelector=24 ownedCom1=true terminalHalt=true " + "postExitBootServices=false\r\nTEST_PASS m02.009\r\n";
    private const string FailureMarker = "M02_OWNED_GDT_TSS_VERIFY_FAILED\r\n" + "TEST_FAIL m02.009 gdtr_or_tr_mismatch\r\n";
    private const string HostReadyMarker = "CLROS_M02_009_HOST_READY contextBytes=168 stubBytes=2987 " + "fnv1a64=C9C04DEE967D640A " + "sha256=424C98FE039EB46B66EC8C3D8BB954EC7BBB24E6434817079F9B2E17478B8504 " + "guestExecution=false lgdt=false ltr=false ownedCom1=false terminalHalt=false";
    private const ulong ContextAddress = 0x0020_0000;
    private const ulong StubAddress = 0x0030_0000;
    private const byte Sentinel = 0xa5;
    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;
        RunCase("public value contracts are exact POD", TestValueContracts, ref failures);
        RunCase("context layout is independently exact", TestExactContext, ref failures);
        RunCase("stub decoder audits every byte and operation", TestExactStubAudit, ref failures);
        RunCase("frozen fingerprints are exact", TestFrozenFingerprints, ref failures);
        RunCase("span overloads preserve the pointer contract", TestSpanOverloads, ref failures);
        RunCase("every context and stub mutation is rejected", TestEveryMutation, ref failures);
        RunCase("pointer and modeled-address boundaries reject atomically", TestStorageBoundaries, ref failures);
        RunCase("actual and modeled overlap reject atomically", TestOverlapAndAdjacency, ref failures);
        RunCase("pointer paths allocate no managed memory", TestZeroAllocation, ref failures);
        RunCase("source boundary remains host-only and no-stdlib ready", TestSourceBoundary, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + TestName + " host-tests failures=" + failures);
            throw new InvalidOperationException("M02-009 owned GDT/TSS terminal stub host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
        Console.WriteLine("TEST_PASS " + TestName);
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

                Console.WriteLine("M02_009_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine("M02_009_CASE_FAIL " + Normalize(name) + " " + exception.GetType().Name + ": " + exception.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestValueContracts()
    {
        OwnedGdtTssTerminalStubResult defaultResult = default;
        Array values = Enum.GetValues(typeof(OwnedGdtTssTerminalStubStatus));
        if (Enum.GetUnderlyingType(typeof(OwnedGdtTssTerminalStubStatus)) != typeof(byte) || values.Length != 28)
        {
            return false;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if ((byte)(OwnedGdtTssTerminalStubStatus)values.GetValue(index)! != index)
            {
                return false;
            }
        }

        return Unsafe.SizeOf<OwnedGdtTssTerminalContext>() == 168 && !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedGdtTssTerminalContext>() && !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedGdtTssTerminalStubResult>() && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.GdtrByteCount == 10 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.GdtByteCount == 40 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssByteCount == 104 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.GdtrLimit == 39 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.NullSelector == 0 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.KernelCodeSelector == 8 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.KernelDataSelector == 16 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssSelector == 24 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.LoadGdtrOffset == 0 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ReadBackGdtrOffset == 10 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ReadBackTrSelectorOffset == 20 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextReservedOffset == 22 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.GdtOffset == 24 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssOffset == 64 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount == 168 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssAccessByteOffset == 29 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.AvailableTssAccess == 0x89 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.BusyTssAccess == 0x8b && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssIoMapBaseOffset == 102 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssIoMapBase == 104 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.RequiredAlignment == 16 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrologueByteCount == 67 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.SuccessMarkerByteCount == 170 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.FailureMarkerByteCount == 72 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.SuccessSerialOffset == 67 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.SuccessTerminalOffset == 2111 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.FailureSerialOffset == 2115 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.FailureTerminalOffset == 2983 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount == 2987 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubFnv1A64 == 0xc9c0_4dee_967d_640aUL && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Com1DataPort == 0x03f8 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Com1LineStatusPort == 0x03fd && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Com1TransmitterHoldingRegisterEmptyMask == 0x20 && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.UsesWindowsX64Abi && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextPassedInRcx && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.UsesOnlyVolatileRegisters && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TouchesRsp && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.UsesStack && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.HasCalls && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.HasReturns && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.HasExternalRelocations && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.UsesAddressDependentMarkerPointers && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.MarkersAreInlineImmediateBytes && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.RequiresWritableGdt && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TerminalHalt && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.HostTestsInvokeBytes && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PerformsLiveDescriptorLoad && !defaultResult.IsInitialized && !defaultResult.Succeeded && !defaultResult.ContextPrepared && !defaultResult.StubEmitted && !defaultResult.ReadBackValidated;
    }

    private static bool TestExactContext()
    {
        byte* storage = stackalloc byte[200];
        byte* context = Align16(storage);
        Fill(context, 184, Sentinel);
        OwnedGdtTssTerminalStubResult result = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(context, ContextAddress, 168);
        if (!result.ContextPrepared || result.ModeledContextAddress != ContextAddress || result.ContextCapacity != 168 || result.ContextBytesWritten != 168 || result.ContextBytesCompared != 168 || result.FirstMismatchOffset != -1 || context[168] != Sentinel)
        {
            return false;
        }

        byte* expected = stackalloc byte[168];
        BuildExpectedContext(expected, ContextAddress);
        if (!new ReadOnlySpan<byte>(context, 168).SequenceEqual(new ReadOnlySpan<byte>(expected, 168)))
        {
            return false;
        }

        ulong gdtBase = ReadU64(context + 2);
        ulong tssBase = DecodeTssBase(context + 24 + 24);
        if (ReadU16(context) != 39 || gdtBase != ContextAddress + 24 || ReadU16(context + 10) != 0 || ReadU64(context + 12) != 0 || ReadU16(context + 20) != 0 || ReadU16(context + 22) != 0 || tssBase != ContextAddress + 64)
        {
            return false;
        }

        for (int index = 0; index < 104; index++)
        {
            byte expectedTss = index == 102 ? (byte)104 : (byte)0;
            if (context[64 + index] != expectedTss)
            {
                return false;
            }
        }

        return context[24 + 29] == 0x89 && context[64 + 102] == 104 && context[64 + 103] == 0;
    }

    private static bool TestExactStubAudit()
    {
        byte* contextRaw = stackalloc byte[184];
        byte* stubRaw = stackalloc byte[3003];
        byte* context = Align16(contextRaw);
        byte* stub = Align16(stubRaw);
        if (!PrepareAndEmit(context, stub))
        {
            return false;
        }

        StubAudit audit = AuditStub(new ReadOnlySpan<byte>(stub, 2987));
        return audit.Valid && audit.BytesAudited == 2987 && audit.Instructions == 1718 && audit.ConditionalBranches == 246 && audit.TerminalBranches == 2 && audit.InInstructions == 242 && audit.OutInstructions == 242 && audit.CliInstructions == 3 && audit.HltInstructions == 2 && audit.SuccessMarker == SuccessMarker && audit.FailureMarker == FailureMarker;
    }

    private static bool TestFrozenFingerprints()
    {
        byte* contextRaw = stackalloc byte[184];
        byte* stubRaw = stackalloc byte[3003];
        byte* context = Align16(contextRaw);
        byte* stub = Align16(stubRaw);
        if (!PrepareAndEmit(context, stub))
        {
            return false;
        }

        ReadOnlySpan<byte> bytes = new(stub, 2987);
        ulong fingerprint = IndependentFnv1A64(bytes);
        string sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        return fingerprint == 0xc9c0_4dee_967d_640aUL && sha256 == ExpectedSha256;
    }

    private static bool TestSpanOverloads()
    {
        byte* storage = stackalloc byte[3400];
        byte* context = Align16(storage);
        byte* stub = context + 176;
        Span<byte> contextSpan = new(context, 168);
        Span<byte> stubSpan = new(stub, 2987);
        OwnedGdtTssTerminalStubResult prepared = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(contextSpan, ContextAddress);
        OwnedGdtTssTerminalStubResult emitted = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Emit(stubSpan, StubAddress, contextSpan, ContextAddress);
        OwnedGdtTssTerminalStubResult validated = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ValidateReadBack(stubSpan, StubAddress, contextSpan, ContextAddress);
        return prepared.ContextPrepared && emitted.StubEmitted && validated.ReadBackValidated;
    }

    private static bool TestEveryMutation()
    {
        byte* contextRaw = stackalloc byte[184];
        byte* stubRaw = stackalloc byte[3003];
        byte* scratchRaw = stackalloc byte[3003];
        byte* context = Align16(contextRaw);
        byte* stub = Align16(stubRaw);
        byte* scratch = Align16(scratchRaw);
        if (!PrepareAndEmit(context, stub))
        {
            return false;
        }

        for (int index = 0; index < 168; index++)
        {
            Fill(scratch, 2987, Sentinel);
            context[index] ^= 0xff;
            OwnedGdtTssTerminalStubResult rejected = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Emit(scratch, StubAddress, 2987, context, ContextAddress, 168);
            context[index] ^= 0xff;
            if (rejected.Status != OwnedGdtTssTerminalStubStatus.ContextByteMismatch || rejected.FirstMismatchOffset != index || rejected.ContextBytesCompared != index + 1 || !IsAll(scratch, 2987, Sentinel))
            {
                return false;
            }
        }

        for (int index = 0; index < 2987; index++)
        {
            stub[index] ^= 0xff;
            OwnedGdtTssTerminalStubResult rejected = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ValidateReadBack(stub, StubAddress, 2987, context, ContextAddress, 168);
            stub[index] ^= 0xff;
            if (rejected.Status != OwnedGdtTssTerminalStubStatus.StubByteMismatch || rejected.FirstMismatchOffset != index || rejected.StubBytesCompared != index + 1)
            {
                return false;
            }
        }

        return OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ValidateReadBack(stub, StubAddress, 2987, context, ContextAddress, 168).ReadBackValidated;
    }

    private static bool TestStorageBoundaries()
    {
        byte* contextRaw = stackalloc byte[184];
        byte* stubRaw = stackalloc byte[3003];
        byte* context = Align16(contextRaw);
        byte* stub = Align16(stubRaw);
        Fill(context, 168, Sentinel);
        Fill(stub, 2987, Sentinel);
        bool validCanonical = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.IsCanonicalX64Address(0) && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.IsCanonicalX64Address(0x0000_7fff_ffff_ffff) && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.IsCanonicalX64Address(0xffff_8000_0000_0000) && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.IsCanonicalX64Address(ulong.MaxValue) && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.IsCanonicalX64Address(0x0000_8000_0000_0000) && !OwnedGDTTSSGeneratedAuditedCSharpStubBytes.IsCanonicalX64Address(0xffff_7fff_ffff_ffff);
        if (!validCanonical)
        {
            return false;
        }

        if (!ExpectPrepareStatus(null, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.MissingContext) || !ExpectPrepareStatus(context, ContextAddress, 167, OwnedGdtTssTerminalStubStatus.ContextCapacityTooSmall) || !ExpectPrepareStatus(context + 1, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.MisalignedContextPointer) || !ExpectPrepareStatus((byte*)0x0000_8000_0000_0000UL, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.NonCanonicalContextPointer) || !ExpectPrepareStatus((byte*)0xffff_ffff_ffff_fff0UL, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.ContextPointerSpanOverflow) || !ExpectPrepareStatus((byte*)0x0000_7fff_ffff_ff80UL, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.NonCanonicalContextPointerSpan) || !ExpectPrepareStatus(context, 0, 168, OwnedGdtTssTerminalStubStatus.NullContextAddress) || !ExpectPrepareStatus(context, ContextAddress + 1, 168, OwnedGdtTssTerminalStubStatus.MisalignedContextAddress) || !ExpectPrepareStatus(context, 0x0000_8000_0000_0000UL, 168, OwnedGdtTssTerminalStubStatus.NonCanonicalContextAddress) || !ExpectPrepareStatus(context, 0xffff_ffff_ffff_fff0UL, 168, OwnedGdtTssTerminalStubStatus.ContextAddressSpanOverflow) || !ExpectPrepareStatus(context, 0x0000_7fff_ffff_ff80UL, 168, OwnedGdtTssTerminalStubStatus.NonCanonicalContextAddressSpan) || !IsAll(context, 168, Sentinel))
        {
            return false;
        }

        if (!OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(context, ContextAddress, 168).ContextPrepared)
        {
            return false;
        }

        return ExpectEmitStatus(null, StubAddress, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.MissingStub) && ExpectEmitStatus(stub, StubAddress, 2986, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.StubCapacityTooSmall) && ExpectEmitStatus(stub + 1, StubAddress, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.MisalignedStubPointer) && ExpectEmitStatus((byte*)0x0000_8000_0000_0000UL, StubAddress, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.NonCanonicalStubPointer) && ExpectEmitStatus((byte*)0xffff_ffff_ffff_fff0UL, StubAddress, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.StubPointerSpanOverflow) && ExpectEmitStatus((byte*)0x0000_7fff_ffff_f800UL, StubAddress, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.NonCanonicalStubPointerSpan) && ExpectEmitStatus(stub, 0, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.NullStubAddress) && ExpectEmitStatus(stub, StubAddress + 1, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.MisalignedStubAddress) && ExpectEmitStatus(stub, 0x0000_8000_0000_0000UL, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.NonCanonicalStubAddress) && ExpectEmitStatus(stub, 0xffff_ffff_ffff_fff0UL, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.StubAddressSpanOverflow) && ExpectEmitStatus(stub, 0x0000_7fff_ffff_f800UL, 2987, context, ContextAddress, 168, OwnedGdtTssTerminalStubStatus.NonCanonicalStubAddressSpan) && IsAll(stub, 2987, Sentinel);
    }

    private static bool TestOverlapAndAdjacency()
    {
        byte* storageRaw = stackalloc byte[3400];
        byte* separateRaw = stackalloc byte[3003];
        byte* storage = Align16(storageRaw);
        byte* context = storage;
        byte* adjacentStub = storage + 176;
        byte* separateStub = Align16(separateRaw);
        if (!OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(context, ContextAddress, 168).ContextPrepared)
        {
            return false;
        }

        Fill(separateStub, 2987, Sentinel);
        OwnedGdtTssTerminalStubResult actualOverlap = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Emit(context + 16, StubAddress, 2987, context, ContextAddress, 168);
        OwnedGdtTssTerminalStubResult modeledOverlap = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Emit(separateStub, ContextAddress + 16, 2987, context, ContextAddress, 168);
        if (actualOverlap.Status != OwnedGdtTssTerminalStubStatus.StubOverlapsContextPointer || modeledOverlap.Status != OwnedGdtTssTerminalStubStatus.StubOverlapsContextAddress || !IsAll(separateStub, 2987, Sentinel))
        {
            return false;
        }

        ulong adjacentStubAddress = ContextAddress + 176;
        OwnedGdtTssTerminalStubResult adjacent = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Emit(adjacentStub, adjacentStubAddress, 2987, context, ContextAddress, 168);
        return adjacent.StubEmitted && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ValidateReadBack(adjacentStub, adjacentStubAddress, 2987, context, ContextAddress, 168).ReadBackValidated;
    }

    private static bool TestZeroAllocation()
    {
        byte* contextRaw = stackalloc byte[184];
        byte* stubRaw = stackalloc byte[3003];
        byte* context = Align16(contextRaw);
        byte* stub = Align16(stubRaw);
        RunPointerPath(context, stub);
        long before = GC.GetAllocatedBytesForCurrentThread();
        OwnedGdtTssTerminalStubResult result = default;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            result = RunPointerPath(context, stub);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return result.ReadBackValidated && after == before;
    }

    private static bool TestSourceBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, @"src\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs"));
        string noHost = RemoveNoStdLibHostBlocks(source);
        string[] forbidden = ["delegate*", "GetDelegateForFunctionPointer", "DynamicInvoke(", "Marshal.", "VirtualProtect", "mprotect", "EfiEntry", "BootSequence", "Probes", "OpCodes.Calli", "stackalloc", "new byte[", "throw ", "Console.", "DllImport"];
        return source.Contains("#if !NOSTDLIB_EFI", StringComparison.Ordinal) && source.Contains("Span<byte>", StringComparison.Ordinal) && source.Contains("ReadOnlySpan<byte>", StringComparison.Ordinal) && !noHost.Contains("Span<", StringComparison.Ordinal) && !source.Contains(SuccessMarker, StringComparison.Ordinal) && !source.Contains(FailureMarker, StringComparison.Ordinal) && forbidden.All(token => !source.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static OwnedGdtTssTerminalStubResult RunPointerPath(byte* context, byte* stub)
    {
        OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(context, ContextAddress, 168);
        OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Emit(stub, StubAddress, 2987, context, ContextAddress, 168);
        return OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ValidateReadBack(stub, StubAddress, 2987, context, ContextAddress, 168);
    }

    private static bool PrepareAndEmit(byte* context, byte* stub)
    {
        return OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(context, ContextAddress, 168).ContextPrepared && OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Emit(stub, StubAddress, 2987, context, ContextAddress, 168).StubEmitted;
    }

    private static bool ExpectPrepareStatus(byte* context, ulong modeledAddress, ulong capacity, OwnedGdtTssTerminalStubStatus expected)
    {
        OwnedGdtTssTerminalStubResult result = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(context, modeledAddress, capacity);
        return result.IsInitialized && !result.Succeeded && result.Status == expected && result.ContextBytesWritten == 0;
    }

    private static bool ExpectEmitStatus(byte* stub, ulong modeledStubAddress, ulong stubCapacity, byte* context, ulong modeledContextAddress, ulong contextCapacity, OwnedGdtTssTerminalStubStatus expected)
    {
        OwnedGdtTssTerminalStubResult result = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.Emit(stub, modeledStubAddress, stubCapacity, context, modeledContextAddress, contextCapacity);
        return result.IsInitialized && !result.Succeeded && result.Status == expected && result.StubBytesWritten == 0;
    }

    private static StubAudit AuditStub(ReadOnlySpan<byte> bytes)
    {
        int offset = 0;
        int instructions = 0;
        int conditionalBranches = 0;
        int terminalBranches = 0;
        int inInstructions = 0;
        int outInstructions = 0;
        int cliInstructions = 0;
        int hltInstructions = 0;
        bool valid = true;
        Expect(bytes, ref offset, ref valid, 0xfa);
        instructions++;
        cliInstructions++;
        Expect(bytes, ref offset, ref valid, 0x0f, 0x01, 0x11);
        instructions++;
        Expect(bytes, ref offset, ref valid, 0x66, 0xb8, 0x18, 0x00);
        instructions++;
        Expect(bytes, ref offset, ref valid, 0x0f, 0x00, 0xd8);
        instructions++;
        Expect(bytes, ref offset, ref valid, 0x0f, 0x01, 0x41, 0x0a);
        instructions++;
        Expect(bytes, ref offset, ref valid, 0x0f, 0x00, 0x49, 0x14);
        instructions++;
        Expect(bytes, ref offset, ref valid, 0x66, 0x8b, 0x01);
        instructions++;
        Expect(bytes, ref offset, ref valid, 0x66, 0x3b, 0x41, 0x0a);
        instructions++;
        AuditNearJne(bytes, ref offset, 2115, ref valid);
        instructions++;
        conditionalBranches++;
        Expect(bytes, ref offset, ref valid, 0x48, 0x8b, 0x41, 0x02);
        instructions++;
        Expect(bytes, ref offset, ref valid, 0x48, 0x3b, 0x41, 0x0c);
        instructions++;
        AuditNearJne(bytes, ref offset, 2115, ref valid);
        instructions++;
        conditionalBranches++;
        Expect(bytes, ref offset, ref valid, 0x66, 0x83, 0x79, 0x14, 0x18);
        instructions++;
        AuditNearJne(bytes, ref offset, 2115, ref valid);
        instructions++;
        conditionalBranches++;
        Expect(bytes, ref offset, ref valid, 0x80, 0x79, 0x35, 0x8b);
        instructions++;
        AuditNearJne(bytes, ref offset, 2115, ref valid);
        instructions++;
        conditionalBranches++;
        valid &= offset == 67;
        string success = AuditSerialBlock(bytes, ref offset, 170, ref valid, ref instructions, ref conditionalBranches, ref inInstructions, ref outInstructions);
        valid &= offset == 2111;
        AuditTerminal(bytes, ref offset, 2111, ref valid, ref instructions, ref terminalBranches, ref cliInstructions, ref hltInstructions);
        valid &= offset == 2115;
        string failure = AuditSerialBlock(bytes, ref offset, 72, ref valid, ref instructions, ref conditionalBranches, ref inInstructions, ref outInstructions);
        valid &= offset == 2983;
        AuditTerminal(bytes, ref offset, 2983, ref valid, ref instructions, ref terminalBranches, ref cliInstructions, ref hltInstructions);
        valid &= offset == bytes.Length;
        return new StubAudit(valid, offset, instructions, conditionalBranches, terminalBranches, inInstructions, outInstructions, cliInstructions, hltInstructions, success, failure);
    }

    private static string AuditSerialBlock(ReadOnlySpan<byte> bytes, ref int offset, int markerLength, ref bool valid, ref int instructions, ref int conditionalBranches, ref int inInstructions, ref int outInstructions)
    {
        Expect(bytes, ref offset, ref valid, 0x66, 0xba, 0xfd, 0x03);
        instructions++;
        byte[] marker = new byte[markerLength];
        for (int index = 0; index < markerLength; index++)
        {
            int pollOffset = offset;
            Expect(bytes, ref offset, ref valid, 0xec);
            instructions++;
            inInstructions++;
            Expect(bytes, ref offset, ref valid, 0xa8, 0x20);
            instructions++;
            Expect(bytes, ref offset, ref valid, 0x74);
            int displacementOffset = offset;
            Expect(bytes, ref offset, ref valid, 0xfb);
            valid &= displacementOffset + 1 + (sbyte)bytes[displacementOffset] == pollOffset;
            instructions++;
            conditionalBranches++;
            Expect(bytes, ref offset, ref valid, 0xb2, 0xf8);
            instructions++;
            Expect(bytes, ref offset, ref valid, 0xb0);
            instructions++;
            if (offset >= bytes.Length)
            {
                valid = false;
                marker[index] = 0;
            }
            else
            {
                marker[index] = bytes[offset++];
            }

            Expect(bytes, ref offset, ref valid, 0xee);
            instructions++;
            outInstructions++;
            Expect(bytes, ref offset, ref valid, 0xb2, 0xfd);
            instructions++;
        }

        return Encoding.ASCII.GetString(marker);
    }

    private static void AuditNearJne(ReadOnlySpan<byte> bytes, ref int offset, int expectedTarget, ref bool valid)
    {
        Expect(bytes, ref offset, ref valid, 0x0f, 0x85);
        if (offset + 4 > bytes.Length)
        {
            valid = false;
            offset = bytes.Length;
            return;
        }

        int displacement = bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);
        offset += 4;
        valid &= offset + displacement == expectedTarget;
    }

    private static void AuditTerminal(ReadOnlySpan<byte> bytes, ref int offset, int expectedTarget, ref bool valid, ref int instructions, ref int terminalBranches, ref int cliInstructions, ref int hltInstructions)
    {
        Expect(bytes, ref offset, ref valid, 0xfa);
        instructions++;
        cliInstructions++;
        Expect(bytes, ref offset, ref valid, 0xf4);
        instructions++;
        hltInstructions++;
        Expect(bytes, ref offset, ref valid, 0xeb);
        int displacementOffset = offset;
        Expect(bytes, ref offset, ref valid, 0xfc);
        valid &= displacementOffset + 1 + (sbyte)bytes[displacementOffset] == expectedTarget;
        instructions++;
        terminalBranches++;
    }

    private static void Expect(ReadOnlySpan<byte> bytes, ref int offset, ref bool valid, params byte[] expected)
    {
        for (int index = 0; index < expected.Length; index++)
        {
            if (offset >= bytes.Length || bytes[offset] != expected[index])
            {
                valid = false;
            }

            if (offset < bytes.Length)
            {
                offset++;
            }
        }
    }

    private static void BuildExpectedContext(byte* destination, ulong address)
    {
        Fill(destination, 168, 0);
        destination[0] = 39;
        WriteU64(destination + 2, address + 24);
        byte[] code = [0xff, 0xff, 0, 0, 0, 0x9a, 0xaf, 0];
        byte[] data = [0xff, 0xff, 0, 0, 0, 0x92, 0xcf, 0];
        for (int index = 0; index < 8; index++)
        {
            destination[24 + 8 + index] = code[index];
            destination[24 + 16 + index] = data[index];
        }

        ulong tss = address + 64;
        byte* descriptor = destination + 24 + 24;
        descriptor[0] = 103;
        descriptor[2] = (byte)tss;
        descriptor[3] = (byte)(tss >> 8);
        descriptor[4] = (byte)(tss >> 16);
        descriptor[5] = 0x89;
        descriptor[7] = (byte)(tss >> 24);
        descriptor[8] = (byte)(tss >> 32);
        descriptor[9] = (byte)(tss >> 40);
        descriptor[10] = (byte)(tss >> 48);
        descriptor[11] = (byte)(tss >> 56);
        destination[64 + 102] = 104;
    }

    private static ulong DecodeTssBase(byte* descriptor)
    {
        return descriptor[2] | ((ulong)descriptor[3] << 8) | ((ulong)descriptor[4] << 16) | ((ulong)descriptor[7] << 24) | ((ulong)descriptor[8] << 32) | ((ulong)descriptor[9] << 40) | ((ulong)descriptor[10] << 48) | ((ulong)descriptor[11] << 56);
    }

    private static ulong IndependentFnv1A64(ReadOnlySpan<byte> bytes)
    {
        ulong fingerprint = 14_695_981_039_346_656_037UL;
        foreach (byte value in bytes)
        {
            fingerprint ^= value;
            fingerprint *= 1_099_511_628_211UL;
        }

        return fingerprint;
    }

    private static byte* Align16(byte* pointer)
    {
        return (byte*)(((ulong)pointer + 15UL) & ~15UL);
    }

    private static void Fill(byte* destination, int length, byte value)
    {
        for (int index = 0; index < length; index++)
        {
            destination[index] = value;
        }
    }

    private static bool IsAll(byte* source, int length, byte value)
    {
        for (int index = 0; index < length; index++)
        {
            if (source[index] != value)
            {
                return false;
            }
        }

        return true;
    }

    private static ushort ReadU16(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static ulong ReadU64(byte* source)
    {
        ulong value = 0;
        for (int index = 0; index < 8; index++)
        {
            value |= (ulong)source[index] << (index * 8);
        }

        return value;
    }

    private static void WriteU64(byte* destination, ulong value)
    {
        for (int index = 0; index < 8; index++)
        {
            destination[index] = (byte)(value >> (index * 8));
        }
    }

    private static string RemoveNoStdLibHostBlocks(string source)
    {
        const string open = "#if !NOSTDLIB_EFI";
        const string close = "#endif";
        while (true)
        {
            int start = source.IndexOf(open, StringComparison.Ordinal);
            if (start < 0)
            {
                return source;
            }

            int end = source.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0)
            {
                return source;
            }

            source = source.Remove(start, end + close.Length - start);
        }
    }

    private static string Normalize(string value)
    {
        return value.Replace(' ', '_').ToUpperInvariant();
    }
}
