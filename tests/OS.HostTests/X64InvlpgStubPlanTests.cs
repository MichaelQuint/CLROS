using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OS.Boot;
using OS.Kernel.Memory;

internal static unsafe class X64InvlpgStubPlanTests
{
    private const string TestName = "m01.014.02.invlpg";
    private const ulong CodePageAddress = 0x0060_0000;
    private const ulong TargetAddress = 0x0040_0000;
    private const ulong OtherTargetAddress = 0x0041_0000;
    private const ulong EntryOffset = 0x100;
    private const byte Sentinel = 0xa5;
    private const string ExpectedByteHash =
        "FEF29CD9A7B4B2A9C53BC69C7893BC37FFFACECF2093CD943C5F43139C2CF53F";

    internal static int RunM01014Invlpg()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "append only pod contracts and defaults",
            TestTypeContractsAndDefaults,
            ref failures);
        RunCase(
            "loader code page geometry and exact bytes",
            TestGeometryEmissionAndReadback,
            ref failures);
        RunCase(
            "geometry and destination failures are atomic",
            TestGeometryAndDestinationFailuresAreAtomic,
            ref failures);
        RunCase(
            "readback failures are deterministic",
            TestReadbackFailures,
            ref failures);
        RunCase(
            "one shot rx invocation plan binds exact target",
            TestInvocationPlan,
            ref failures);
        RunCase(
            "invocation failures reject unsafe or malformed input",
            TestInvocationFailures,
            ref failures);
        RunCase(
            "host tests never execute emitted bytes",
            TestNoLiveInvocationBoundary,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " failures=" + failures);
            return failures;
        }

        Console.WriteLine("TEST_PASS " + TestName);
        return 0;
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {
        try
        {
            if (test())
                return;

            failures++;
            Console.WriteLine(
                "M01_014_INVLPG_CASE_FAIL " + Normalize(name));
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "M01_014_INVLPG_CASE_FAIL "
                + Normalize(name)
                + " "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static bool TestTypeContractsAndDefaults()
    {
        string[] statusNames =
        [
            "Uninitialized",
            "Success",
            "InvalidTargetRecord",
            "TargetOrdinalMustBeZero",
            "InvalidCodePage",
            "InvalidCodePageLength",
            "NonCanonicalCodePage",
            "InvalidEntryOffset",
            "MisalignedEntry",
            "EntryOutsideCodePage",
            "MissingDestination",
            "MisalignedDestination",
            "DestinationCapacityTooSmall",
            "DestinationCapacityExceedsPage",
            "DestinationSpanOverflow",
            "DestinationAddressMismatch",
            "MissingSource",
            "MisalignedSource",
            "SourceCapacityTooSmall",
            "SourceCapacityExceedsPage",
            "SourceSpanOverflow",
            "SourceAddressMismatch",
            "InvalidExpectedPlan",
            "ExpectedPlanNotMaterialized",
            "ByteReadbackMismatch",
            "InvalidInvocationRequest",
            "InvocationCountMustBeOne",
            "InvocationEntryMismatch",
            "InvocationTargetMismatch",
            "InvalidCodeProtection",
            "WritableExecutableCodeRejected",
            "ContractLayoutMismatch"
        ];
        string[] phaseNames =
        [
            "Uninitialized",
            "Materialized",
            "ReadBackVerified"
        ];
        X64InvlpgStubPlanResult result = default;
        X64InvlpgInvocationRequest request = default;
        X64InvlpgInvocationPlanResult invocation = default;

        return HasExactSequentialByteEnum<X64InvlpgStubPlanStatus>(
                statusNames)
            && HasExactSequentialByteEnum<X64InvlpgStubPhase>(
                phaseNames)
            && (ulong)Unsafe.SizeOf<X64InvlpgStubPlanResult>()
                == X64InvlpgStubPlanResult.StructureSize
            && (ulong)Unsafe.SizeOf<X64InvlpgInvocationRequest>()
                == X64InvlpgInvocationRequest.StructureSize
            && (ulong)Unsafe.SizeOf<X64InvlpgInvocationPlanResult>()
                == X64InvlpgInvocationPlanResult.StructureSize
            && (ulong)Unsafe.SizeOf<X64TlbInvalidationRecord>()
                == X64TlbInvalidationRecord.StructureSize
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64InvlpgStubPlanResult>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64InvlpgInvocationRequest>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64InvlpgInvocationPlanResult>()
            && !result.IsInitialized
            && !result.Succeeded
            && !result.ReadBackVerified
            && result.Status
                == X64InvlpgStubPlanStatus.Uninitialized
            && result.Phase == X64InvlpgStubPhase.Uninitialized
            && !request.IsInitialized
            && request.Validate()
                == X64InvlpgStubPlanStatus.InvalidInvocationRequest
            && !invocation.IsInitialized
            && !invocation.Succeeded
            && invocation.Status
                == X64InvlpgStubPlanStatus.Uninitialized
            && X64InvlpgStubPlan.PageSize == 4096
            && X64InvlpgStubPlan.EntryAlignment == 16
            && X64InvlpgStubPlan.RequiredPointerAlignment == 16
            && X64InvlpgStubPlan.RequiredByteLength == 4
            && X64InvlpgStubPlan.AuditedInstructionCount == 2
            && X64InvlpgStubPlan.MaximumInvocationCount == 1;
    }

    private static bool TestGeometryEmissionAndReadback()
    {
        using AlignedCodePage page = new();
        page.Fill(Sentinel);
        VirtualAddress codePage = Virtual(CodePageAddress);
        X64TlbInvalidationRecord target = Record(TargetAddress);
        BootMemoryRegionRecord loaderCode = new(
            (uint)UefiMemoryType.LoaderCode,
            CodePageAddress,
            CodePageAddress,
            pageCount: 1,
            attribute: 0,
            byteLength: X64InvlpgStubPlan.PageSize,
            physicalEndExclusive:
                CodePageAddress + X64InvlpgStubPlan.PageSize);

        X64InvlpgStubPlanResult emitted =
            X64InvlpgStubPlan.Emit(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                codePage,
                EntryOffset,
                target);
        if (!emitted.Succeeded ||
            emitted.ReadBackVerified ||
            emitted.Phase != X64InvlpgStubPhase.Materialized ||
            !emitted.CodePageStart.Equals(codePage) ||
            Raw(emitted.CodePageEndExclusive)
                != CodePageAddress + X64InvlpgStubPlan.PageSize ||
            Raw(emitted.EntryAddress)
                != CodePageAddress + EntryOffset ||
            emitted.EntryOffset != EntryOffset ||
            !emitted.Target.Target.Equals(target.Target) ||
            emitted.Target.Ordinal != 0 ||
            emitted.AvailableCapacity
                != X64InvlpgStubPlan.PageSize ||
            emitted.RequiredByteCount
                != X64InvlpgStubPlan.RequiredByteLength ||
            emitted.BytesWrittenOrVerified
                != X64InvlpgStubPlan.RequiredByteLength ||
            emitted.InstructionCount
                != X64InvlpgStubPlan.AuditedInstructionCount)
        {
            return false;
        }

        byte* entry = page.Pointer + EntryOffset;
        byte[] bytes =
        [
            entry[0],
            entry[1],
            entry[2],
            entry[3]
        ];
        X64InvlpgStubPlanResult verified =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                emitted);

        return loaderCode.UefiType == (uint)UefiMemoryType.LoaderCode
            && loaderCode.PhysicalStart == CodePageAddress
            && loaderCode.VirtualStart == CodePageAddress
            && loaderCode.PageCount == 1
            && loaderCode.ByteLength == X64InvlpgStubPlan.PageSize
            && loaderCode.PhysicalEndExclusive
                == CodePageAddress + X64InvlpgStubPlan.PageSize
            && codePage.IsAligned(X64InvlpgStubPlan.PageSize)
            && emitted.EntryAddress.IsAligned(
                X64InvlpgStubPlan.EntryAlignment)
            && bytes.AsSpan().SequenceEqual(
                new byte[] { 0x0f, 0x01, 0x39, 0xc3 })
            && Convert.ToHexString(SHA256.HashData(bytes))
                == ExpectedByteHash
            && page.IsSentinelExcept(
                EntryOffset,
                X64InvlpgStubPlan.RequiredByteLength)
            && verified.Succeeded
            && verified.ReadBackVerified
            && verified.Phase
                == X64InvlpgStubPhase.ReadBackVerified
            && verified.EntryAddress.Equals(emitted.EntryAddress)
            && verified.Target.Target.Equals(target.Target)
            && verified.BytesWrittenOrVerified == 4;
    }

    private static bool TestGeometryAndDestinationFailuresAreAtomic()
    {
        using AlignedCodePage page = new();
        VirtualAddress codePage = Virtual(CodePageAddress);
        X64TlbInvalidationRecord target = Record(TargetAddress);
        X64TlbInvalidationRecord ordinalOne =
            Record(TargetAddress, pageCount: 2, recordIndex: 1);

        if (!EmitFailsAtomically(
                page,
                default,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                codePage,
                EntryOffset,
                X64InvlpgStubPlanStatus.InvalidTargetRecord) ||
            !EmitFailsAtomically(
                page,
                ordinalOne,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                codePage,
                EntryOffset,
                X64InvlpgStubPlanStatus.TargetOrdinalMustBeZero) ||
            !EmitFailsAtomically(
                page,
                target,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                default,
                EntryOffset,
                X64InvlpgStubPlanStatus.InvalidCodePage) ||
            !EmitFailsAtomically(
                page,
                target,
                CodePageAddress + 1,
                X64InvlpgStubPlan.PageSize,
                VirtualUnaligned(CodePageAddress + 1),
                EntryOffset,
                X64InvlpgStubPlanStatus.InvalidCodePage) ||
            !EmitFailsAtomically(
                page,
                target,
                X64TlbInvalidationPlan.LowCanonicalEndExclusive,
                X64InvlpgStubPlan.PageSize,
                Virtual(
                    X64TlbInvalidationPlan
                        .LowCanonicalEndExclusive),
                EntryOffset,
                X64InvlpgStubPlanStatus.NonCanonicalCodePage) ||
            !EmitFailsAtomically(
                page,
                target,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                codePage,
                1,
                X64InvlpgStubPlanStatus.MisalignedEntry) ||
            !EmitFailsAtomically(
                page,
                target,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                codePage,
                X64InvlpgStubPlan.PageSize,
                X64InvlpgStubPlanStatus.EntryOutsideCodePage))
        {
            return false;
        }

        page.Fill(Sentinel);
        X64InvlpgStubPlanResult missing =
            X64InvlpgStubPlan.Emit(
                null,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                codePage,
                EntryOffset,
                target);
        if (missing.Status
                != X64InvlpgStubPlanStatus.MissingDestination ||
            !page.IsAll(Sentinel))
        {
            return false;
        }

        if (!EmitFailsAtomically(
                page,
                target,
                CodePageAddress,
                X64InvlpgStubPlan.RequiredByteLength - 1,
                codePage,
                EntryOffset,
                X64InvlpgStubPlanStatus
                    .DestinationCapacityTooSmall) ||
            !EmitFailsAtomically(
                page,
                target,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize + 1,
                codePage,
                EntryOffset,
                X64InvlpgStubPlanStatus
                    .DestinationCapacityExceedsPage) ||
            !EmitFailsAtomically(
                page,
                target,
                CodePageAddress + X64InvlpgStubPlan.PageSize,
                X64InvlpgStubPlan.PageSize,
                codePage,
                EntryOffset,
                X64InvlpgStubPlanStatus
                    .DestinationAddressMismatch))
        {
            return false;
        }

        page.Fill(Sentinel);
        X64InvlpgStubPlanResult misaligned =
            X64InvlpgStubPlan.Emit(
                page.Pointer + 1,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                codePage,
                EntryOffset,
                target);
        X64InvlpgStubPlanResult overflow =
            X64InvlpgStubPlan.Emit(
                (byte*)0xffff_ffff_ffff_fff0UL,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                codePage,
                EntryOffset,
                target);

        return misaligned.Status
                == X64InvlpgStubPlanStatus.MisalignedDestination
            && overflow.Status
                == X64InvlpgStubPlanStatus.DestinationSpanOverflow
            && page.IsAll(Sentinel);
    }

    private static bool TestReadbackFailures()
    {
        using AlignedCodePage page = new();
        page.Fill(Sentinel);
        X64InvlpgStubPlanResult emitted = EmitValid(page);
        if (!emitted.Succeeded)
            return false;

        X64InvlpgStubPlanResult missing =
            X64InvlpgStubPlan.ValidateReadback(
                null,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                emitted);
        X64InvlpgStubPlanResult shortCapacity =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.RequiredByteLength - 1,
                emitted);
        X64InvlpgStubPlanResult oversized =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize + 1,
                emitted);
        X64InvlpgStubPlanResult misaligned =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer + 1,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                emitted);
        X64InvlpgStubPlanResult wrongAddress =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress + X64InvlpgStubPlan.PageSize,
                X64InvlpgStubPlan.PageSize,
                emitted);
        X64InvlpgStubPlanResult overflow =
            X64InvlpgStubPlan.ValidateReadback(
                (byte*)0xffff_ffff_ffff_fff0UL,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                emitted);
        X64InvlpgStubPlanResult invalidExpected =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                default);

        byte* entry = page.Pointer + EntryOffset;
        byte original = entry[2];
        entry[2] ^= 0xff;
        X64InvlpgStubPlanResult mismatch =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                emitted);
        entry[2] = original;
        X64InvlpgStubPlanResult verified =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                emitted);
        X64InvlpgStubPlanResult repeated =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                verified);

        return missing.Status == X64InvlpgStubPlanStatus.MissingSource
            && shortCapacity.Status
                == X64InvlpgStubPlanStatus.SourceCapacityTooSmall
            && oversized.Status
                == X64InvlpgStubPlanStatus.SourceCapacityExceedsPage
            && misaligned.Status
                == X64InvlpgStubPlanStatus.MisalignedSource
            && wrongAddress.Status
                == X64InvlpgStubPlanStatus.SourceAddressMismatch
            && overflow.Status
                == X64InvlpgStubPlanStatus.SourceSpanOverflow
            && invalidExpected.Status
                == X64InvlpgStubPlanStatus
                    .ExpectedPlanNotMaterialized
            && mismatch.Status
                == X64InvlpgStubPlanStatus.ByteReadbackMismatch
            && verified.ReadBackVerified
            && repeated.Status
                == X64InvlpgStubPlanStatus
                    .ExpectedPlanNotMaterialized;
    }

    private static bool TestInvocationPlan()
    {
        using AlignedCodePage page = new();
        X64InvlpgStubPlanResult emitted = EmitValid(page);
        X64InvlpgStubPlanResult verified =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                emitted);
        X64InvlpgInvocationRequest request =
            X64InvlpgInvocationRequest.Create(
                verified.EntryAddress,
                verified.Target,
                ReadExecute,
                invocationCount: 1);
        X64InvlpgInvocationPlanResult invocation =
            X64InvlpgStubPlan.CreateInvocationPlan(
                verified,
                request);

        return verified.ReadBackVerified
            && request.IsInitialized
            && request.Validate()
                == X64InvlpgStubPlanStatus.Success
            && request.InvocationCount == 1
            && request.CodeProtection == ReadExecute
            && request.EntryAddress.Equals(verified.EntryAddress)
            && request.Target.Target.Equals(verified.Target.Target)
            && request.Target.Ordinal == 0
            && invocation.Succeeded
            && invocation.Status == X64InvlpgStubPlanStatus.Success
            && invocation.PlannedInvocationCount == 1
            && invocation.FunctionPointerAddress
                == Raw(verified.EntryAddress)
            && invocation.Request.EntryAddress.Equals(
                request.EntryAddress)
            && !X64InvlpgStubPlan.PerformsLiveInvalidation
            && !X64InvlpgStubPlan.ReloadsCr3
            && !X64InvlpgStubPlan.InvokesStub
            && !X64InvlpgStubPlan.HostTestsInvokeBytes;
    }

    private static bool TestInvocationFailures()
    {
        using AlignedCodePage page = new();
        X64InvlpgStubPlanResult emitted = EmitValid(page);
        X64InvlpgStubPlanResult verified =
            X64InvlpgStubPlan.ValidateReadback(
                page.Pointer,
                CodePageAddress,
                X64InvlpgStubPlan.PageSize,
                emitted);
        VirtualAddress otherEntry =
            VirtualUnaligned(Raw(verified.EntryAddress) + 16);
        VirtualAddress unalignedEntry =
            VirtualUnaligned(Raw(verified.EntryAddress) + 1);
        X64TlbInvalidationRecord otherTarget =
            Record(OtherTargetAddress);
        PageProtection writableExecutable =
            (PageProtection)(
                (byte)PageProtection.Read |
                (byte)PageProtection.Write |
                (byte)PageProtection.Execute);

        X64InvlpgInvocationPlanResult zeroCount = Plan(
            verified,
            verified.EntryAddress,
            verified.Target,
            ReadExecute,
            0);
        X64InvlpgInvocationPlanResult twoCalls = Plan(
            verified,
            verified.EntryAddress,
            verified.Target,
            ReadExecute,
            2);
        X64InvlpgInvocationPlanResult wrongEntry = Plan(
            verified,
            otherEntry,
            verified.Target,
            ReadExecute,
            1);
        X64InvlpgInvocationPlanResult unaligned = Plan(
            verified,
            unalignedEntry,
            verified.Target,
            ReadExecute,
            1);
        X64InvlpgInvocationPlanResult wrongTarget = Plan(
            verified,
            verified.EntryAddress,
            otherTarget,
            ReadExecute,
            1);
        X64InvlpgInvocationPlanResult malformedTarget = Plan(
            verified,
            verified.EntryAddress,
            default,
            ReadExecute,
            1);
        X64InvlpgInvocationPlanResult wx = Plan(
            verified,
            verified.EntryAddress,
            verified.Target,
            writableExecutable,
            1);
        X64InvlpgInvocationPlanResult readWrite = Plan(
            verified,
            verified.EntryAddress,
            verified.Target,
            ReadWrite,
            1);
        X64InvlpgInvocationPlanResult nonExecutable = Plan(
            verified,
            verified.EntryAddress,
            verified.Target,
            PageProtection.Read,
            1);
        X64InvlpgInvocationPlanResult defaultRequest =
            X64InvlpgStubPlan.CreateInvocationPlan(
                verified,
                default);
        X64InvlpgInvocationPlanResult materializedOnly =
            X64InvlpgStubPlan.CreateInvocationPlan(
                emitted,
                X64InvlpgInvocationRequest.Create(
                    emitted.EntryAddress,
                    emitted.Target,
                    ReadExecute,
                    1));
        X64InvlpgInvocationPlanResult defaultStub =
            X64InvlpgStubPlan.CreateInvocationPlan(
                default,
                default);

        return zeroCount.Status
                == X64InvlpgStubPlanStatus.InvocationCountMustBeOne
            && twoCalls.Status
                == X64InvlpgStubPlanStatus.InvocationCountMustBeOne
            && wrongEntry.Status
                == X64InvlpgStubPlanStatus.InvocationEntryMismatch
            && unaligned.Status
                == X64InvlpgStubPlanStatus.InvalidInvocationRequest
            && wrongTarget.Status
                == X64InvlpgStubPlanStatus.InvocationTargetMismatch
            && malformedTarget.Status
                == X64InvlpgStubPlanStatus.InvalidInvocationRequest
            && wx.Status
                == X64InvlpgStubPlanStatus
                    .WritableExecutableCodeRejected
            && readWrite.Status
                == X64InvlpgStubPlanStatus.InvalidCodeProtection
            && nonExecutable.Status
                == X64InvlpgStubPlanStatus.InvalidCodeProtection
            && defaultRequest.Status
                == X64InvlpgStubPlanStatus.InvalidInvocationRequest
            && materializedOnly.Status
                == X64InvlpgStubPlanStatus.InvalidExpectedPlan
            && defaultStub.Status
                == X64InvlpgStubPlanStatus.InvalidExpectedPlan
            && !zeroCount.Succeeded
            && zeroCount.FunctionPointerAddress == 0
            && zeroCount.PlannedInvocationCount == 0
            && !wx.Succeeded
            && wx.FunctionPointerAddress == 0
            && wx.PlannedInvocationCount == 0;
    }

    private static bool TestNoLiveInvocationBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                root,
                @"src\OS.Kernel\Memory\X64InvlpgStubPlan.cs"));

        return X64InvlpgStubPlan.UsesWindowsX64Abi
            && X64InvlpgStubPlan.TargetPassedInRcx
            && !X64InvlpgStubPlan.TouchesNonvolatileRegisters
            && !X64InvlpgStubPlan.TouchesXmm6ThroughXmm15
            && !X64InvlpgStubPlan.HasExternalRelocations
            && !X64InvlpgStubPlan.PerformsLiveInvalidation
            && !X64InvlpgStubPlan.ReloadsCr3
            && !X64InvlpgStubPlan.InvokesStub
            && !X64InvlpgStubPlan.HostTestsInvokeBytes
            && !source.Contains(
                "delegate*",
                StringComparison.Ordinal)
            && !source.Contains(
                "GetDelegateForFunctionPointer",
                StringComparison.Ordinal)
            && !source.Contains(
                "DynamicInvoke(",
                StringComparison.Ordinal)
            && !source.Contains(
                ".Invoke(",
                StringComparison.Ordinal)
            && !source.Contains(
                "OpCodes.Calli",
                StringComparison.Ordinal);
    }

    private static X64InvlpgStubPlanResult EmitValid(
        AlignedCodePage page)
    {
        page.Fill(Sentinel);
        return X64InvlpgStubPlan.Emit(
            page.Pointer,
            CodePageAddress,
            X64InvlpgStubPlan.PageSize,
            Virtual(CodePageAddress),
            EntryOffset,
            Record(TargetAddress));
    }

    private static bool EmitFailsAtomically(
        AlignedCodePage page,
        X64TlbInvalidationRecord target,
        ulong modeledAddress,
        ulong capacity,
        VirtualAddress codePage,
        ulong entryOffset,
        X64InvlpgStubPlanStatus expectedStatus)
    {
        page.Fill(Sentinel);
        X64InvlpgStubPlanResult result =
            X64InvlpgStubPlan.Emit(
                page.Pointer,
                modeledAddress,
                capacity,
                codePage,
                entryOffset,
                target);
        return !result.Succeeded
            && result.Status == expectedStatus
            && result.Phase == X64InvlpgStubPhase.Uninitialized
            && result.BytesWrittenOrVerified == 0
            && page.IsAll(Sentinel);
    }

    private static X64InvlpgInvocationPlanResult Plan(
        X64InvlpgStubPlanResult verified,
        VirtualAddress entry,
        X64TlbInvalidationRecord target,
        PageProtection protection,
        ulong count)
    {
        return X64InvlpgStubPlan.CreateInvocationPlan(
            verified,
            X64InvlpgInvocationRequest.Create(
                entry,
                target,
                protection,
                count));
    }

    private static X64TlbInvalidationRecord Record(
        ulong targetAddress,
        ulong pageCount = 1,
        ulong recordIndex = 0)
    {
        X64TlbInvalidationStatus create =
            X64TlbInvalidationPlan.TryCreate(
                Virtual(targetAddress),
                pageCount * X64TlbInvalidationPlan.PageSize,
                out X64TlbInvalidationRange range);
        if (create != X64TlbInvalidationStatus.Success ||
            recordIndex >= pageCount)
        {
            throw new InvalidOperationException(
                "Invalid invalidation record fixture.");
        }

        X64TlbInvalidationRecord* records =
            stackalloc X64TlbInvalidationRecord[(int)pageCount];
        X64TlbInvalidationPlanResult result =
            X64TlbInvalidationPlan.Materialize(
                range,
                records,
                pageCount);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Invalidation materialization failed: "
                + result.Status);
        }

        return records[recordIndex];
    }

    private static VirtualAddress Virtual(ulong value)
    {
        AddressValueStatus status =
            VirtualAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    value,
                    X64TlbInvalidationPlan.PageSize,
                    out VirtualAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Virtual fixture invalid: " + status);
        }

        return address;
    }

    private static VirtualAddress VirtualUnaligned(ulong value)
    {
        AddressValueStatus status =
            VirtualAddress.TryCreateFromHardwareRegisterBoundary(
                value,
                out VirtualAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Virtual fixture invalid: " + status);
        }

        return address;
    }

    private static ulong Raw(VirtualAddress address)
    {
        if (address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value) != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Virtual raw conversion failed.");
        }

        return value;
    }

    private static bool HasExactSequentialByteEnum<T>(
        string[] expectedNames)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
            return false;

        string[] actualNames = Enum.GetNames<T>();
        T[] actualValues = Enum.GetValues<T>();
        if (actualNames.Length != expectedNames.Length ||
            actualValues.Length != expectedNames.Length)
        {
            return false;
        }

        for (int index = 0; index < expectedNames.Length; index++)
        {
            if (!string.Equals(
                    actualNames[index],
                    expectedNames[index],
                    StringComparison.Ordinal) ||
                Convert.ToByte(actualValues[index]) != index)
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string value)
    {
        return value
            .Replace(' ', '_')
            .Replace('-', '_');
    }

    private static readonly PageProtection ReadExecute =
        (PageProtection)(
            (byte)PageProtection.Read |
            (byte)PageProtection.Execute);

    private static readonly PageProtection ReadWrite =
        (PageProtection)(
            (byte)PageProtection.Read |
            (byte)PageProtection.Write);

    private sealed unsafe class AlignedCodePage : IDisposable
    {
        private readonly byte* _allocation;

        internal AlignedCodePage()
        {
            _allocation = (byte*)NativeMemory.AlignedAlloc(
                (nuint)(
                    X64InvlpgStubPlan.PageSize
                    + X64InvlpgStubPlan.RequiredPointerAlignment),
                (nuint)X64InvlpgStubPlan.RequiredPointerAlignment);
            if (_allocation == null)
            {
                throw new OutOfMemoryException(
                    "Unable to allocate INVLPG host fixture.");
            }
        }

        internal byte* Pointer => _allocation;

        internal void Fill(byte value)
        {
            new Span<byte>(
                _allocation,
                checked((int)X64InvlpgStubPlan.PageSize))
                .Fill(value);
        }

        internal bool IsAll(byte value)
        {
            ReadOnlySpan<byte> bytes = new(
                _allocation,
                checked((int)X64InvlpgStubPlan.PageSize));
            foreach (byte current in bytes)
            {
                if (current != value)
                    return false;
            }

            return true;
        }

        internal bool IsSentinelExcept(
            ulong offset,
            ulong length)
        {
            ReadOnlySpan<byte> bytes = new(
                _allocation,
                checked((int)X64InvlpgStubPlan.PageSize));
            for (int index = 0; index < bytes.Length; index++)
            {
                bool inside =
                    (ulong)index >= offset &&
                    (ulong)index < offset + length;
                if (!inside && bytes[index] != Sentinel)
                    return false;
            }

            return true;
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(_allocation);
        }
    }
}
