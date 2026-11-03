using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static partial class X64BootIdentityMappingPlanTests
{
    private const string IssueTestName = "m01.012";
    private const string ReadyMarker = "CLROS_M01_012_READY";
    private const ulong PatFixture = 0x0006_0501_0007_0406;
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;
        Console.WriteLine("TEST_START " + IssueTestName);
        Run("m01.012 01 type values layouts and defaults are stable", TestTypeContracts, ref failures);
        Run("m01.012 02 required roles and the guard are complete", TestRequiredRolesAndGuard, ref failures);
        Run("m01.012 03 realistic plans are exact and deterministic", TestRealisticPlan, ref failures);
        Run("m01.012 04 compatible ranges merge with exact union accounting", TestCompatibleMerging, ref failures);
        Run("m01.012 05 policy conflicts report deterministic source indexes", TestPolicyConflicts, ref failures);
        Run("m01.012 06 protection cache and PAT policy fail closed", TestPolicyValidation, ref failures);
        Run("m01.012 07 page zero and guard overlap are excluded", TestGuardAndPageZero, ref failures);
        Run("m01.012 08 address geometry and physical widths are bounded", TestAddressGeometry, ref failures);
        Run("m01.012 09 four-level table-page accounting is exact", TestTablePageAccounting, ref failures);
        Run("m01.012 10 pointer count capacity and aliasing are bounded", TestPointerCore, ref failures);
        Run("m01.012 11 hostile POD bytes and failure atomicity hold", TestHostilePodAndAtomicity, ref failures);
        Run("m01.012 12 source and nostdlib efi firewalls hold", TestSourceAndNoStdLibBoundary, ref failures);
        Run("m01.012 13 canonical efi closures include the planner and exclude host tests", TestEvaluatedEfiBoundary, ref failures);
        Run("m01.012 14 marker ownership and ordering are stable", TestMarkerBoundary, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException("M01-012 x64 boot identity mapping plan tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    internal static bool TestM01030ExactLiveIntervalContract()
    {
        return TestM01030LiveIntervalCanonicalPlan() && TestM01030LiveIntervalPurposeSet() && TestM01030LiveIntervalGuardAndGeometry() && TestM01030LiveIntervalPoliciesAndAtomicity();
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestTypeContracts()
    {
        (string Name, byte Value)[] purposeValues = [("Uninitialized", 0), ("ExecutingImage", 1), ("OwnedStack", 2), ("BootHandoff", 3), ("SerialStub", 4), ("AcpiScratch", 5), ("FramebufferMetadata", 6), ("BootStackGuard", 7), ("AdditionalRequired", 8), ("CompositeCodeAndCallGate", 9), ("CompositeOwnedStack", 10), ("ProtectionProbePage", 11), ("CompleteTableAndStateArena", 12), ("RuntimeStateAndCanaryPage", 13), ("OwnedRootPage", 14)];
        (string Name, byte Value)[] statusValues = [("Uninitialized", 0), ("Success", 1), ("InvalidSourceCount", 2), ("TooManySources", 3), ("SourceCapacityTooSmall", 4), ("CapacityExceedsLimit", 5), ("MissingSource", 6), ("MissingDestination", 7), ("MisalignedSource", 8), ("MisalignedDestination", 9), ("SourceSpanOverflow", 10), ("DestinationSpanOverflow", 11), ("SourceDestinationOverlap", 12), ("ContractLayoutMismatch", 13), ("InvalidRequirementState", 14), ("InvalidPurpose", 15), ("InvalidPhysicalStart", 16), ("InvalidPhysicalEnd", 17), ("EmptyOrReversedRange", 18), ("MisalignedRange", 19), ("GeometryOverflow", 20), ("GeometryMismatch", 21), ("InvalidProtection", 22), ("WritableExecutableRejected", 23), ("InvalidCachePolicy", 24), ("InvalidGuardPolicy", 25), ("MultipleGuards", 26), ("MissingGuard", 27), ("GuardOverlap", 28), ("MissingRequiredPurpose", 29), ("PageZeroMappingRejected", 30), ("NonCanonicalIdentityRange", 31), ("PhysicalAddressOutsideWidth", 32), ("IncompatibleProtectionOverlap", 33), ("IncompatibleCacheOverlap", 34), ("DestinationCapacityTooSmall", 35), ("TooManyOutputRanges", 36), ("UnionPageCountOverflow", 37), ("UnionByteCountOverflow", 38), ("TablePageCountOverflow", 39), ("TableByteCountOverflow", 40), ("TableCapacityTooSmall", 41), ("InvalidPreconditions", 42), ("PageWalkCacheNotWriteBack", 43), ("LeafEncodingRejected", 44), ("OutputConstructionFailure", 45), ("AccountingMismatch", 46)];
        X64BootIdentityMappingRequirement requirement = default;
        X64BootIdentityMappingRange range = default;
        X64BootIdentityMappingPlanResult result = default;
        return HasExactByteEnum<X64BootIdentityMappingPurpose>(purposeValues) && HasExactByteEnum<X64BootIdentityMappingStatus>(statusValues) && IsReadOnlySequentialUnmanaged<X64BootIdentityMappingRequirement>() && IsReadOnlySequentialUnmanaged<X64BootIdentityMappingRange>() && IsReadOnlySequentialUnmanaged<X64BootIdentityMappingPlanResult>() && (ulong)Unsafe.SizeOf<X64BootIdentityMappingRequirement>() == X64BootIdentityMappingRequirement.StructureSize && (ulong)Unsafe.SizeOf<X64BootIdentityMappingRange>() == X64BootIdentityMappingRange.StructureSize && (ulong)Unsafe.SizeOf<X64BootIdentityMappingPlanResult>() == X64BootIdentityMappingPlanResult.StructureSize && HasExactFieldOffsets() && !requirement.IsInitialized && requirement.Purpose == X64BootIdentityMappingPurpose.Uninitialized && !requirement.PhysicalStart.IsValid && !requirement.PhysicalEndExclusive.IsValid && requirement.PageCount == 0 && requirement.ByteLength == 0 && requirement.Protection == PageProtection.None && requirement.CachePolicy == PageCachePolicy.Uninitialized && !range.IsInitialized && !range.PhysicalStart.IsValid && !range.PhysicalEndExclusive.IsValid && !range.VirtualStart.IsValid && !range.VirtualEndExclusive.IsValid && range.PageCount == 0 && range.ByteLength == 0 && range.ContributingPurposeMask == 0 && range.Protection == PageProtection.None && range.CachePolicy == PageCachePolicy.Uninitialized && !result.IsInitialized && !result.Succeeded && result.Status == X64BootIdentityMappingStatus.Uninitialized && result.AddressStatus == AddressValueStatus.Uninitialized && result.PageTableEntryStatus == X64PageTableEntryStatus.Uninitialized && result.InvalidSourceIndex == 0 && result.ConflictingSourceIndex == 0 && result.GuardSourceIndex == 0 && X64BootIdentityMappingPlanResult.NoIndex == ~0UL && X64BootIdentityMappingPlan.PageSize == 4096 && X64BootIdentityMappingPlan.MaximumSourceCount == 256 && X64BootIdentityMappingPlan.MaximumDestinationCount == 512 && X64BootIdentityMappingPlan.RequiredPointerAlignment == 8 && X64BootIdentityMappingPlan.LowCanonicalEndExclusive == 0x0000_8000_0000_0000 && X64BootIdentityMappingPlan.RequiredPurposeMask == 0x7e && X64BootIdentityMappingPlan.RequiredLiveIntervalPurposeMask == 0x7e00 && X64BootIdentityMappingPlan.RequiredLiveIntervalPurposeCount == 7 && X64BootIdentityMappingPlan.UsesOnly4KiBLeaves && !X64BootIdentityMappingPlan.PerformsLiveMapping && !X64BootIdentityMappingPlan.AllocatesPageTables && !X64BootIdentityMappingPlan.PopulatesPageTables;
    }

    private static bool TestM01030LiveIntervalCanonicalPlan()
    {
        X64BootIdentityMappingRequirement[] source = LiveIntervalRequirements();
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[16];
        X64BootIdentityMappingPlanResult result = PlanLiveInterval(source, output);
        X64BootIdentityMappingRequirement[] reversed = source.Reverse().ToArray();
        X64BootIdentityMappingRange[] reversedOutput = new X64BootIdentityMappingRange[16];
        X64BootIdentityMappingPlanResult reversedResult = PlanLiveInterval(reversed, reversedOutput);
        X64BootIdentityMappingRange[] legacyOutput = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingPlanResult legacy = Plan(CompleteRequirements(), legacyOutput);
        X64BootIdentityMappingRange[] legacyExactOutput = new X64BootIdentityMappingRange[16];
        X64BootIdentityMappingPlanResult legacyRejectsExactProfile = Plan(source, legacyExactOutput);
        const ulong guardMask = 1UL << (byte)X64BootIdentityMappingPurpose.BootStackGuard;
        const ulong codeMask = 1UL << (byte)X64BootIdentityMappingPurpose.CompositeCodeAndCallGate;
        const ulong stackMask = 1UL << (byte)X64BootIdentityMappingPurpose.CompositeOwnedStack;
        const ulong compatibleUnionMask = (1UL << (byte)X64BootIdentityMappingPurpose.ProtectionProbePage) | (1UL << (byte)X64BootIdentityMappingPurpose.CompleteTableAndStateArena) | (1UL << (byte)X64BootIdentityMappingPurpose.RuntimeStateAndCanaryPage) | (1UL << (byte)X64BootIdentityMappingPurpose.OwnedRootPage);
        bool guardAbsent = true;
        for (ulong index = 0; index < result.OutputRangeCount; index++)
        {
            X64BootIdentityMappingRange range = output[(int)index];
            if (range.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong start) != AddressValueStatus.Success || range.PhysicalEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong endExclusive) != AddressValueStatus.Success || (start < 0x5000 && 0x4000 < endExclusive) || (range.ContributingPurposeMask & guardMask) != 0)
            {
                guardAbsent = false;
                break;
            }
        }

        PageProtection readWrite = Combine(PageProtection.Read, PageProtection.Write);
        PageProtection readExecute = Combine(PageProtection.Read, PageProtection.Execute);
        return legacy.Succeeded && legacy.RequiredPurposeMask == 0x7e && legacy.RequiredPurposeMask == X64BootIdentityMappingPlan.RequiredPurposeMask && legacyRejectsExactProfile.Status == X64BootIdentityMappingStatus.InvalidPurpose && legacyRejectsExactProfile.RequiredPurposeMask == 0x7e && legacyRejectsExactProfile.InvalidSourceIndex == 1 && legacyRejectsExactProfile.OutputRangeCount == 0 && result.Succeeded && result.SourceCount == 7 && result.ValidatedSourceCount == 7 && result.SourceCapacity == 7 && result.RequiredPurposeMask == 0x7e00 && result.RequiredPurposeMask == X64BootIdentityMappingPlan.RequiredLiveIntervalPurposeMask && result.ObservedPurposeMask == 0x7e80 && result.ObservedPurposeMask == (result.RequiredPurposeMask | guardMask) && result.MissingPurposeMask == 0 && result.GuardSourceIndex == 0 && result.InvalidSourceIndex == X64BootIdentityMappingPlanResult.NoIndex && result.ConflictingSourceIndex == X64BootIdentityMappingPlanResult.NoIndex && result.RequiredDestinationCapacity == 3 && result.OutputRangeCount == 3 && result.UnionPageCount == 9 && result.UnionByteLength == 9 * 4096UL && result.TotalTablePageCount == 4 && result.TotalTableByteLength == 4 * 4096UL && RangeEquals(output[0], 0x2000, 0x4000, 2, codeMask, readExecute, PageCachePolicy.WriteBack) && RangeEquals(output[1], 0x5000, 0x7000, 2, stackMask, readWrite, PageCachePolicy.WriteBack) && RangeEquals(output[2], 0x8000, 0xd000, 5, compatibleUnionMask, readWrite, PageCachePolicy.WriteBack) && !output[3].IsInitialized && guardAbsent && reversedResult.Succeeded && reversedResult.RequiredPurposeMask == 0x7e00 && reversedResult.ObservedPurposeMask == 0x7e80 && reversedResult.GuardSourceIndex == 6 && reversedResult.OutputRangeCount == 3 && OutputEquals(output, result.OutputRangeCount, reversedOutput, reversedResult.OutputRangeCount);
    }

    private static bool TestM01030LiveIntervalPurposeSet()
    {
        X64BootIdentityMappingRequirement[] complete = LiveIntervalRequirements();
        X64BootIdentityMappingPurpose[] mappedPurposes = LiveIntervalMappedPurposes();
        for (int mappedIndex = 0; mappedIndex < mappedPurposes.Length; mappedIndex++)
        {
            int sourceIndex = mappedIndex + 1;
            X64BootIdentityMappingRequirement[] absent = complete.Where((_, index) => index != sourceIndex).ToArray();
            if (!LiveIntervalFailureIsAtomic(absent, X64BootIdentityMappingStatus.InvalidSourceCount, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex))
            {
                return false;
            }

            int duplicateMappedIndex = (mappedIndex + 1) % mappedPurposes.Length;
            int duplicateSourceIndex = duplicateMappedIndex + 1;
            X64BootIdentityMappingRequirement[] missingAndDuplicate = [.. complete];
            missingAndDuplicate[sourceIndex] = complete[duplicateSourceIndex];
            if (!LiveIntervalFailureIsAtomic(missingAndDuplicate, X64BootIdentityMappingStatus.MissingRequiredPurpose, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex, 1UL << (byte)mappedPurposes[mappedIndex]))
            {
                return false;
            }

            X64BootIdentityMappingRequirement[] extra = [.. complete, complete[sourceIndex]];
            if (!LiveIntervalFailureIsAtomic(extra, X64BootIdentityMappingStatus.InvalidSourceCount, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex))
            {
                return false;
            }
        }

        byte[] forbiddenPurposeValues = [0, 1, 2, 3, 4, 5, 6, 8, 15, byte.MaxValue];
        PageProtection readExecute = Combine(PageProtection.Read, PageProtection.Execute);
        foreach (byte rawPurpose in forbiddenPurposeValues)
        {
            X64BootIdentityMappingRequirement[] candidate = [.. complete];
            candidate[1] = Requirement((X64BootIdentityMappingPurpose)rawPurpose, 0x2000, 0x4000, readExecute, PageCachePolicy.WriteBack);
            if (!LiveIntervalFailureIsAtomic(candidate, X64BootIdentityMappingStatus.InvalidPurpose, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
            {
                return false;
            }
        }

        X64BootIdentityMappingRequirement[] extraGuard = [.. complete, Guard(0xe000, 0xf000)];
        return LiveIntervalFailureIsAtomic(extraGuard, X64BootIdentityMappingStatus.InvalidSourceCount, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex);
    }

    private static bool TestM01030LiveIntervalGuardAndGeometry()
    {
        X64BootIdentityMappingRequirement[] complete = LiveIntervalRequirements();
        PageProtection readExecute = Combine(PageProtection.Read, PageProtection.Execute);
        X64BootIdentityMappingRequirement[] absentGuard = complete.Skip(1).ToArray();
        if (!LiveIntervalFailureIsAtomic(absentGuard, X64BootIdentityMappingStatus.InvalidSourceCount, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] missingGuard = [.. complete];
        missingGuard[0] = complete[1];
        if (!LiveIntervalFailureIsAtomic(missingGuard, X64BootIdentityMappingStatus.MissingGuard, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex, expectedMissingPurposeMask: 0))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] duplicateGuard = [.. complete];
        duplicateGuard[1] = complete[0];
        if (!LiveIntervalFailureIsAtomic(duplicateGuard, X64BootIdentityMappingStatus.MultipleGuards, expectedInvalidSourceIndex: 1, expectedConflictingSourceIndex: 0))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] defaultGuard = [.. complete];
        defaultGuard[0] = default;
        if (!LiveIntervalFailureIsAtomic(defaultGuard, X64BootIdentityMappingStatus.InvalidRequirementState, expectedInvalidSourceIndex: 0, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] reservedGuard = [.. complete];
        reservedGuard[0] = OverwriteByte(reservedGuard[0], byteOffset: 36, replacement: 1);
        if (!LiveIntervalFailureIsAtomic(reservedGuard, X64BootIdentityMappingStatus.InvalidRequirementState, expectedInvalidSourceIndex: 0, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] presentGuard = [.. complete];
        presentGuard[0] = Requirement(X64BootIdentityMappingPurpose.BootStackGuard, 0x4000, 0x5000, PageProtection.Read, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(presentGuard, X64BootIdentityMappingStatus.InvalidGuardPolicy, expectedInvalidSourceIndex: 0, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] invalidGuardStart = [.. complete];
        invalidGuardStart[0] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.BootStackGuard, PhysicalUnchecked(0), Physical(0x1000), 1, 4096, PageProtection.None, PageCachePolicy.Uninitialized);
        if (!LiveIntervalFailureIsAtomic(invalidGuardStart, X64BootIdentityMappingStatus.InvalidPhysicalStart, expectedInvalidSourceIndex: 0, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] invalidGuardEnd = [.. complete];
        invalidGuardEnd[0] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.BootStackGuard, Physical(0x4000), PhysicalUnchecked(0), 1, 4096, PageProtection.None, PageCachePolicy.Uninitialized);
        if (!LiveIntervalFailureIsAtomic(invalidGuardEnd, X64BootIdentityMappingStatus.InvalidPhysicalEnd, expectedInvalidSourceIndex: 0, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] guardOverlap = [.. complete];
        guardOverlap[0] = Guard(0x3000, 0x5000);
        if (!LiveIntervalFailureIsAtomic(guardOverlap, X64BootIdentityMappingStatus.GuardOverlap, expectedInvalidSourceIndex: 0, expectedConflictingSourceIndex: 1, expectedMissingPurposeMask: 0))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] defaultRange = [.. complete];
        defaultRange[1] = default;
        if (!LiveIntervalFailureIsAtomic(defaultRange, X64BootIdentityMappingStatus.InvalidRequirementState, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] reservedRange = [.. complete];
        reservedRange[1] = OverwriteByte(reservedRange[1], byteOffset: 39, replacement: 1);
        if (!LiveIntervalFailureIsAtomic(reservedRange, X64BootIdentityMappingStatus.InvalidRequirementState, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] pageZero = [.. complete];
        pageZero[1] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, PhysicalUnchecked(0), Physical(0x2000), 2, 2 * 4096UL, readExecute, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(pageZero, X64BootIdentityMappingStatus.PageZeroMappingRejected, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] invalidEnd = [.. complete];
        invalidEnd[1] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, Physical(0x2000), PhysicalUnchecked(0), 2, 2 * 4096UL, readExecute, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(invalidEnd, X64BootIdentityMappingStatus.InvalidPhysicalEnd, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] reversed = [.. complete];
        reversed[1] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, Physical(0x4000), Physical(0x2000), 2, 2 * 4096UL, readExecute, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(reversed, X64BootIdentityMappingStatus.EmptyOrReversedRange, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] unaligned = [.. complete];
        unaligned[1] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, Physical(0x2001), Physical(0x4000), 2, 0x1fff, readExecute, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(unaligned, X64BootIdentityMappingStatus.MisalignedRange, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] zeroGeometry = [.. complete];
        zeroGeometry[1] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, Physical(0x2000), Physical(0x4000), 0, 0, readExecute, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(zeroGeometry, X64BootIdentityMappingStatus.GeometryMismatch, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] geometryMismatch = [.. complete];
        geometryMismatch[1] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, Physical(0x2000), Physical(0x4000), 2, 4096, readExecute, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(geometryMismatch, X64BootIdentityMappingStatus.GeometryMismatch, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] geometryOverflow = [.. complete];
        geometryOverflow[1] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, Physical(0x2000), Physical(0x4000), ~0UL, 4096, readExecute, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(geometryOverflow, X64BootIdentityMappingStatus.GeometryOverflow, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] nonCanonical = [.. complete];
        nonCanonical[1] = Requirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, X64BootIdentityMappingPlan.LowCanonicalEndExclusive, X64BootIdentityMappingPlan.LowCanonicalEndExclusive + 4096, readExecute, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(nonCanonical, X64BootIdentityMappingStatus.NonCanonicalIdentityRange, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] outsideWidth = [.. complete];
        outsideWidth[1] = Requirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, 1UL << 36, (1UL << 36) + 4096, readExecute, PageCachePolicy.WriteBack);
        return LiveIntervalFailureIsAtomic(outsideWidth, X64BootIdentityMappingStatus.PhysicalAddressOutsideWidth, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex, physicalAddressWidth: 36);
    }

    private static bool TestM01030LiveIntervalPoliciesAndAtomicity()
    {
        X64BootIdentityMappingRequirement[] complete = LiveIntervalRequirements();
        (PageProtection Protection, X64BootIdentityMappingStatus Status)[] badProtections = [(PageProtection.None, X64BootIdentityMappingStatus.InvalidProtection), (PageProtection.Write, X64BootIdentityMappingStatus.InvalidProtection), (PageProtection.Execute, X64BootIdentityMappingStatus.InvalidProtection), (Combine(PageProtection.Write, PageProtection.Execute), X64BootIdentityMappingStatus.WritableExecutableRejected), (Combine(Combine(PageProtection.Read, PageProtection.Write), PageProtection.Execute), X64BootIdentityMappingStatus.WritableExecutableRejected), ((PageProtection)0x80, X64BootIdentityMappingStatus.InvalidProtection)];
        foreach ((PageProtection protection, X64BootIdentityMappingStatus status) in badProtections)
        {
            X64BootIdentityMappingRequirement[] candidate = [.. complete];
            candidate[1] = Requirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, 0x2000, 0x4000, protection, PageCachePolicy.WriteBack);
            if (!LiveIntervalFailureIsAtomic(candidate, status, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
            {
                return false;
            }
        }

        PageCachePolicy[] badCachePolicies = [PageCachePolicy.Uninitialized, (PageCachePolicy)byte.MaxValue];
        PageProtection readExecute = Combine(PageProtection.Read, PageProtection.Execute);
        foreach (PageCachePolicy cachePolicy in badCachePolicies)
        {
            X64BootIdentityMappingRequirement[] candidate = [.. complete];
            candidate[1] = Requirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, 0x2000, 0x4000, readExecute, cachePolicy);
            if (!LiveIntervalFailureIsAtomic(candidate, X64BootIdentityMappingStatus.InvalidCachePolicy, expectedInvalidSourceIndex: 1, X64BootIdentityMappingPlanResult.NoIndex))
            {
                return false;
            }
        }

        X64BootIdentityMappingRequirement[] protectionConflict = [.. complete];
        protectionConflict[5] = Requirement(X64BootIdentityMappingPurpose.RuntimeStateAndCanaryPage, 0xa000, 0xb000, PageProtection.Read, PageCachePolicy.WriteBack);
        if (!LiveIntervalFailureIsAtomic(protectionConflict, X64BootIdentityMappingStatus.IncompatibleProtectionOverlap, expectedInvalidSourceIndex: 5, expectedConflictingSourceIndex: 4, expectedMissingPurposeMask: 0))
        {
            return false;
        }

        X64BootIdentityMappingRequirement[] cacheConflict = [.. complete];
        cacheConflict[6] = Requirement(X64BootIdentityMappingPurpose.OwnedRootPage, 0xc000, 0xd000, Combine(PageProtection.Read, PageProtection.Write), PageCachePolicy.Uncached);
        if (!LiveIntervalFailureIsAtomic(cacheConflict, X64BootIdentityMappingStatus.IncompatibleCacheOverlap, expectedInvalidSourceIndex: 6, expectedConflictingSourceIndex: 4, expectedMissingPurposeMask: 0))
        {
            return false;
        }

        if (!LiveIntervalFailureIsAtomic(complete, X64BootIdentityMappingStatus.DestinationCapacityTooSmall, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex, expectedMissingPurposeMask: 0, declaredDestinationCapacity: 2))
        {
            return false;
        }

        return LiveIntervalFailureIsAtomic(complete, X64BootIdentityMappingStatus.TableCapacityTooSmall, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex, expectedMissingPurposeMask: 0, availableTablePageCapacity: 3);
    }

    private static bool TestRequiredRolesAndGuard()
    {
        X64BootIdentityMappingRequirement[] complete = CompleteRequirements();
        X64BootIdentityMappingRange[] destination = new X64BootIdentityMappingRange[32];
        for (byte rawPurpose = 1; rawPurpose <= 6; rawPurpose++)
        {
            X64BootIdentityMappingPurpose purpose = (X64BootIdentityMappingPurpose)rawPurpose;
            X64BootIdentityMappingRequirement[] missing = complete.Where(requirement => requirement.Purpose != purpose).ToArray();
            X64BootIdentityMappingPlanResult result = Plan(missing, destination);
            ulong expectedMissing = 1UL << rawPurpose;
            if (result.Status != X64BootIdentityMappingStatus.MissingRequiredPurpose || result.MissingPurposeMask != expectedMissing || result.InvalidSourceIndex != X64BootIdentityMappingPlanResult.NoIndex || result.OutputRangeCount != 0)
            {
                return false;
            }
        }

        X64BootIdentityMappingRequirement[] noGuard = complete.Where(requirement => requirement.Purpose != X64BootIdentityMappingPurpose.BootStackGuard).ToArray();
        X64BootIdentityMappingPlanResult missingGuard = Plan(noGuard, destination);
        X64BootIdentityMappingRequirement[] twoGuards = [.. complete, Guard(0xc000, 0xd000)];
        X64BootIdentityMappingPlanResult multipleGuards = Plan(twoGuards, destination);
        X64BootIdentityMappingRequirement[] malformedGuard = [.. complete];
        malformedGuard[0] = Requirement(X64BootIdentityMappingPurpose.BootStackGuard, 0x1000, 0x2000, PageProtection.Read, PageCachePolicy.WriteBack);
        X64BootIdentityMappingPlanResult badGuard = Plan(malformedGuard, destination);
        return missingGuard.Status == X64BootIdentityMappingStatus.MissingGuard && missingGuard.GuardSourceIndex == X64BootIdentityMappingPlanResult.NoIndex && multipleGuards.Status == X64BootIdentityMappingStatus.MultipleGuards && multipleGuards.InvalidSourceIndex == (ulong)(twoGuards.Length - 1) && multipleGuards.GuardSourceIndex == 0 && badGuard.Status == X64BootIdentityMappingStatus.InvalidGuardPolicy && badGuard.InvalidSourceIndex == 0;
    }

    private static bool TestRealisticPlan()
    {
        X64BootIdentityMappingRequirement[] source = CompleteRequirements();
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingPlanResult result = Plan(source, output);
        X64BootIdentityMappingRequirement[] reversed = source.Reverse().ToArray();
        X64BootIdentityMappingRange[] reversedOutput = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingPlanResult reversedResult = Plan(reversed, reversedOutput);
        PageProtection readWrite = Combine(PageProtection.Read, PageProtection.Write);
        PageProtection readExecute = Combine(PageProtection.Read, PageProtection.Execute);
        ulong imageMask = 1UL << (byte)X64BootIdentityMappingPurpose.ExecutingImage;
        ulong stackMask = 1UL << (byte)X64BootIdentityMappingPurpose.OwnedStack;
        ulong handoffMask = 1UL << (byte)X64BootIdentityMappingPurpose.BootHandoff;
        ulong serialMask = 1UL << (byte)X64BootIdentityMappingPurpose.SerialStub;
        ulong acpiMask = 1UL << (byte)X64BootIdentityMappingPurpose.AcpiScratch;
        ulong framebufferMask = 1UL << (byte)X64BootIdentityMappingPurpose.FramebufferMetadata;
        return result.Succeeded && result.Status == X64BootIdentityMappingStatus.Success && result.SourceCount == (ulong)source.Length && result.ValidatedSourceCount == (ulong)source.Length && result.SourceCapacity == (ulong)source.Length && result.DestinationCapacity == (ulong)output.Length && result.RequiredDestinationCapacity == 5 && result.OutputRangeCount == 5 && result.AvailableTablePageCapacity == 512 && result.RequiredPurposeMask == X64BootIdentityMappingPlan.RequiredPurposeMask && result.ObservedPurposeMask == 0xfe && result.MissingPurposeMask == 0 && result.InvalidSourceIndex == X64BootIdentityMappingPlanResult.NoIndex && result.ConflictingSourceIndex == X64BootIdentityMappingPlanResult.NoIndex && result.GuardSourceIndex == 0 && result.UnionPageCount == 9 && result.UnionByteLength == 9 * 4096UL && result.Pml4PageCount == 1 && result.PdptPageCount == 1 && result.PdPageCount == 1 && result.PtPageCount == 1 && result.TotalTablePageCount == 4 && result.TotalTableByteLength == 4 * 4096UL && RangeEquals(output[0], 0x2000, 0x4000, 2, imageMask, readExecute, PageCachePolicy.WriteBack) && RangeEquals(output[1], 0x4000, 0x7000, 3, imageMask | stackMask, readWrite, PageCachePolicy.WriteBack) && RangeEquals(output[2], 0x7000, 0x8000, 1, handoffMask, PageProtection.Read, PageCachePolicy.WriteBack) && RangeEquals(output[3], 0x8000, 0x9000, 1, serialMask, readExecute, PageCachePolicy.WriteBack) && RangeEquals(output[4], 0x9000, 0xb000, 2, acpiMask | framebufferMask, PageProtection.Read, PageCachePolicy.WriteBack) && OutputEquals(output, result.OutputRangeCount, reversedOutput, reversedResult.OutputRangeCount) && reversedResult.Succeeded && reversedResult.GuardSourceIndex == (ulong)(reversed.Length - 1) && reversedResult.UnionPageCount == result.UnionPageCount && reversedResult.TotalTablePageCount == result.TotalTablePageCount;
    }

    private static unsafe bool TestCompatibleMerging()
    {
        X64BootIdentityMappingRequirement[] source = [.. CompleteRequirements(), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x10000, 0x13000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x10000, 0x13000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x11000, 0x12000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x12000, 0x15000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x15000, 0x16000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x16000, 0x17000, PageProtection.Read, PageCachePolicy.Uncached)];
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingPlanResult result = Plan(source, output);
        ulong additionalMask = 1UL << (byte)X64BootIdentityMappingPurpose.AdditionalRequired;
        X64BootIdentityMappingRequirement[] canonicalTransitive = [.. CompleteRequirements(), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x20000, 0x22000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x21000, 0x24000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x23000, 0x26000)];
        X64BootIdentityMappingRequirement[] unorderedTransitive = [..CompleteRequirements(), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x20000, 0x22000), // The farther interval intentionally precedes the bridge.
        Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x23000, 0x26000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x21000, 0x24000)];
        X64BootIdentityMappingRange[] canonicalOutput = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingRange[] unorderedOutput = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingPlanResult canonicalResult = Plan(canonicalTransitive, canonicalOutput);
        X64BootIdentityMappingPlanResult unorderedResult = Plan(unorderedTransitive, unorderedOutput);
        bool transitiveOutputByteIdentical;
        fixed (X64BootIdentityMappingRange* canonicalPointer = canonicalOutput)
        fixed (X64BootIdentityMappingRange* unorderedPointer = unorderedOutput)
        {
            ulong[] canonicalBytes = CopyBytesAsUlongs(canonicalPointer, canonicalOutput.Length * sizeof(X64BootIdentityMappingRange));
            transitiveOutputByteIdentical = EqualBytes(canonicalBytes, unorderedPointer, unorderedOutput.Length * sizeof(X64BootIdentityMappingRange));
        }

        return result.Succeeded && result.OutputRangeCount == 7 && result.RequiredDestinationCapacity == 7 && result.UnionPageCount == 16 && result.UnionByteLength == 16 * 4096UL && RangeEquals(output[5], 0x10000, 0x16000, 6, additionalMask, PageProtection.Read, PageCachePolicy.WriteBack) && RangeEquals(output[6], 0x16000, 0x17000, 1, additionalMask, PageProtection.Read, PageCachePolicy.Uncached) && canonicalResult.Succeeded && unorderedResult.Succeeded && canonicalResult.RequiredDestinationCapacity == unorderedResult.RequiredDestinationCapacity && canonicalResult.OutputRangeCount == unorderedResult.OutputRangeCount && canonicalResult.UnionPageCount == unorderedResult.UnionPageCount && canonicalResult.UnionByteLength == unorderedResult.UnionByteLength && canonicalResult.Pml4PageCount == unorderedResult.Pml4PageCount && canonicalResult.PdptPageCount == unorderedResult.PdptPageCount && canonicalResult.PdPageCount == unorderedResult.PdPageCount && canonicalResult.PtPageCount == unorderedResult.PtPageCount && canonicalResult.TotalTablePageCount == unorderedResult.TotalTablePageCount && canonicalResult.TotalTableByteLength == unorderedResult.TotalTableByteLength && transitiveOutputByteIdentical && RangeEquals(unorderedOutput[5], 0x20000, 0x26000, 6, additionalMask, PageProtection.Read, PageCachePolicy.WriteBack);
    }

    private static bool TestPolicyConflicts()
    {
        X64BootIdentityMappingRequirement[] baseRequirements = CompleteRequirements();
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[32];
        PageProtection[] protections = [PageProtection.Read, Combine(PageProtection.Read, PageProtection.Write), Combine(PageProtection.Read, PageProtection.Execute)];
        for (int left = 0; left < protections.Length; left++)
        {
            for (int right = left + 1; right < protections.Length; right++)
            {
                X64BootIdentityMappingRequirement[] candidate = [.. baseRequirements];
                candidate[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, 0x7000, 0x8000, protections[left], PageCachePolicy.WriteBack);
                candidate = [.. candidate, Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x7000, 0x8000, protections[right], PageCachePolicy.WriteBack)];
                X64BootIdentityMappingPlanResult matrix = Plan(candidate, output);
                if (matrix.Status != X64BootIdentityMappingStatus.IncompatibleProtectionOverlap || matrix.InvalidSourceIndex != 8 || matrix.ConflictingSourceIndex != 4)
                {
                    return false;
                }
            }
        }

        PageCachePolicy[] cachePolicies = [PageCachePolicy.WriteBack, PageCachePolicy.Uncached, PageCachePolicy.WriteCombining];
        for (int left = 0; left < cachePolicies.Length; left++)
        {
            for (int right = left + 1; right < cachePolicies.Length; right++)
            {
                X64BootIdentityMappingRequirement[] candidate = [.. baseRequirements];
                candidate[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, 0x7000, 0x8000, PageProtection.Read, cachePolicies[left]);
                candidate = [.. candidate, Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x7000, 0x8000, PageProtection.Read, cachePolicies[right])];
                X64BootIdentityMappingPlanResult matrix = Plan(candidate, output);
                if (matrix.Status != X64BootIdentityMappingStatus.IncompatibleCacheOverlap || matrix.InvalidSourceIndex != 8 || matrix.ConflictingSourceIndex != 4)
                {
                    return false;
                }
            }
        }

        X64BootIdentityMappingRequirement[] protectionConflict = [.. baseRequirements, Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x3000, 0x4000, PageProtection.Read, PageCachePolicy.WriteBack)];
        X64BootIdentityMappingPlanResult protection = Plan(protectionConflict, output);
        X64BootIdentityMappingRequirement[] cacheConflict = [.. baseRequirements, Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x9000, 0xa000, PageProtection.Read, PageCachePolicy.Uncached)];
        X64BootIdentityMappingPlanResult cache = Plan(cacheConflict, output);
        X64BootIdentityMappingRequirement[] reversed = protectionConflict.Reverse().ToArray();
        X64BootIdentityMappingPlanResult reversedResult = Plan(reversed, output);
        return protection.Status == X64BootIdentityMappingStatus.IncompatibleProtectionOverlap && protection.InvalidSourceIndex == 8 && protection.ConflictingSourceIndex == 1 && protection.OutputRangeCount == 0 && cache.Status == X64BootIdentityMappingStatus.IncompatibleCacheOverlap && cache.InvalidSourceIndex == 8 && cache.ConflictingSourceIndex == 6 && cache.OutputRangeCount == 0 && reversedResult.Status == X64BootIdentityMappingStatus.IncompatibleProtectionOverlap && reversedResult.InvalidSourceIndex == 7 && reversedResult.ConflictingSourceIndex == 0;
    }

    private static bool TestPolicyValidation()
    {
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingRequirement[] source = CompleteRequirements();
        PageProtection readWrite = Combine(PageProtection.Read, PageProtection.Write);
        PageProtection readExecute = Combine(PageProtection.Read, PageProtection.Execute);
        for (int raw = 0; raw <= byte.MaxValue; raw++)
        {
            PageProtection protection = (PageProtection)(byte)raw;
            bool allowed = protection == PageProtection.Read || protection == readWrite || protection == readExecute;
            X64BootIdentityMappingRequirement[] candidate = [.. source];
            candidate[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, 0x7000, 0x8000, protection, PageCachePolicy.WriteBack);
            X64BootIdentityMappingPlanResult result = Plan(candidate, output);
            if (allowed != result.Succeeded)
            {
                return false;
            }

            if (!allowed)
            {
                X64BootIdentityMappingStatus expected = (((byte)protection & (byte)PageProtection.Write) != 0 && ((byte)protection & (byte)PageProtection.Execute) != 0) ? X64BootIdentityMappingStatus.WritableExecutableRejected : X64BootIdentityMappingStatus.InvalidProtection;
                if (result.Status != expected || result.InvalidSourceIndex != 4)
                {
                    return false;
                }
            }
        }

        for (int raw = 0; raw <= byte.MaxValue; raw++)
        {
            PageCachePolicy policy = (PageCachePolicy)(byte)raw;
            bool allowed = policy == PageCachePolicy.WriteBack || policy == PageCachePolicy.Uncached || policy == PageCachePolicy.WriteCombining;
            X64BootIdentityMappingRequirement[] candidate = [.. source];
            candidate[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, 0x7000, 0x8000, PageProtection.Read, policy);
            X64BootIdentityMappingPlanResult result = Plan(candidate, output);
            if (allowed != result.Succeeded)
            {
                return false;
            }

            if (!allowed && (result.Status != X64BootIdentityMappingStatus.InvalidCachePolicy || result.InvalidSourceIndex != 4))
            {
                return false;
            }
        }

        X64BootIdentityMappingPlanResult defaultContext = Plan(source, output, availableTablePageCapacity: 512, preconditions: default);
        X64PatMappingToken.TryCreate(PatFixture, writeBackIndex: 0, uncachedIndex: X64PatMappingToken.UnsupportedIndex, writeCombiningIndex: X64PatMappingToken.UnsupportedIndex, out X64PatMappingToken limitedPat);
        X64BootIdentityMappingRequirement[] requiresWc = [.. source];
        requiresWc[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, 0x7000, 0x8000, PageProtection.Read, PageCachePolicy.WriteCombining);
        X64BootIdentityMappingPlanResult unsupportedWc = Plan(requiresWc, output, availableTablePageCapacity: 512, preconditions: Context(pat: limitedPat));
        const ulong pageWalkNotWriteBack = 0x0006_0501_0007_0404;
        X64PageTableEntryStatus highPatStatus = X64PatMappingToken.TryCreate(pageWalkNotWriteBack, writeBackIndex: 6, uncachedIndex: 3, writeCombiningIndex: 4, out X64PatMappingToken highWriteBack);
        X64BootIdentityMappingPlanResult badPageWalk = Plan(source, output, availableTablePageCapacity: 512, preconditions: Context(pat: highWriteBack));
        X64PageTableEncodingPreconditions malformedPat = OverwriteByte(Context(), byteOffset: 0, replacement: 0xff);
        X64BootIdentityMappingPlanResult badPat = Plan(source, output, 512, malformedPat);
        X64PageTableEncodingPreconditions mismatchedPatIndex = OverwriteByte(Context(), byteOffset: 9, replacement: 7);
        X64BootIdentityMappingPlanResult badPatIndex = Plan(source, output, 512, mismatchedPatIndex);
        return defaultContext.Status == X64BootIdentityMappingStatus.InvalidPreconditions && defaultContext.PageTableEntryStatus == X64PageTableEntryStatus.PreconditionsNotInitialized && unsupportedWc.Status == X64BootIdentityMappingStatus.LeafEncodingRejected && unsupportedWc.PageTableEntryStatus == X64PageTableEntryStatus.CachePolicyNotSupported && highPatStatus == X64PageTableEntryStatus.Success && badPageWalk.Status == X64BootIdentityMappingStatus.PageWalkCacheNotWriteBack && badPageWalk.PageTableEntryStatus == X64PageTableEntryStatus.PatMemoryTypeMismatch && badPat.Status == X64BootIdentityMappingStatus.InvalidPreconditions && badPat.PageTableEntryStatus == X64PageTableEntryStatus.InvalidPatEntry && badPatIndex.Status == X64BootIdentityMappingStatus.InvalidPreconditions && badPatIndex.PageTableEntryStatus == X64PageTableEntryStatus.PatMemoryTypeMismatch;
    }

    private static bool TestGuardAndPageZero()
    {
        X64BootIdentityMappingRequirement[] source = CompleteRequirements();
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingRequirement[] guardOverlap = [.. source];
        guardOverlap[0] = Guard(0x1000, 0x3000);
        X64BootIdentityMappingPlanResult overlap = Plan(guardOverlap, output);
        X64BootIdentityMappingRequirement[] mappedPageZero = [.. source];
        mappedPageZero[1] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.ExecutingImage, PhysicalUnchecked(0), Physical(0x2000), 2, 2 * 4096UL, Combine(PageProtection.Read, PageProtection.Execute), PageCachePolicy.WriteBack);
        X64BootIdentityMappingPlanResult zero = Plan(mappedPageZero, output);
        X64BootIdentityMappingPlanResult complete = Plan(source, output);
        bool guardAbsent = true;
        for (ulong index = 0; index < complete.OutputRangeCount; index++)
        {
            X64BootIdentityMappingRange range = output[(int)index];
            if (RawEquals(range.PhysicalStart, 0x1000) || RawEquals(range.VirtualStart, 0x1000) || (range.ContributingPurposeMask & (1UL << (byte)X64BootIdentityMappingPurpose.BootStackGuard)) != 0)
            {
                guardAbsent = false;
                break;
            }
        }

        return overlap.Status == X64BootIdentityMappingStatus.GuardOverlap && overlap.InvalidSourceIndex == 0 && overlap.ConflictingSourceIndex == 1 && zero.Status == X64BootIdentityMappingStatus.PageZeroMappingRejected && zero.InvalidSourceIndex == 1 && complete.Succeeded && guardAbsent;
    }

    private static bool TestAddressGeometry()
    {
        X64BootIdentityMappingRequirement[] source = CompleteRequirements();
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingRequirement[] unaligned = [.. source];
        unaligned[4] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.BootHandoff, Physical(0x7001), Physical(0x8000), 1, 0x0fff, PageProtection.Read, PageCachePolicy.WriteBack);
        X64BootIdentityMappingPlanResult unalignedResult = Plan(unaligned, output);
        X64BootIdentityMappingRequirement[] reversed = [.. source];
        reversed[4] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.BootHandoff, Physical(0x8000), Physical(0x7000), 1, 4096, PageProtection.Read, PageCachePolicy.WriteBack);
        X64BootIdentityMappingPlanResult reversedResult = Plan(reversed, output);
        X64BootIdentityMappingRequirement[] mismatch = [.. source];
        mismatch[4] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.BootHandoff, Physical(0x7000), Physical(0x8000), 2, 4096, PageProtection.Read, PageCachePolicy.WriteBack);
        X64BootIdentityMappingPlanResult mismatchResult = Plan(mismatch, output);
        X64BootIdentityMappingRequirement[] overflow = [.. source];
        overflow[4] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.BootHandoff, Physical(0x7000), Physical(0x8000), ~0UL, 4096, PageProtection.Read, PageCachePolicy.WriteBack);
        X64BootIdentityMappingPlanResult overflowResult = Plan(overflow, output);
        X64BootIdentityMappingRequirement[] zeroGeometry = [.. source];
        zeroGeometry[4] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.BootHandoff, Physical(0x7000), Physical(0x8000), 0, 0, PageProtection.Read, PageCachePolicy.WriteBack);
        X64BootIdentityMappingPlanResult zeroGeometryResult = Plan(zeroGeometry, output);
        X64BootIdentityMappingRequirement[] invalidEnd = [.. source];
        invalidEnd[4] = new X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose.BootHandoff, Physical(0x7000), PhysicalUnchecked(0), 1, 4096, PageProtection.Read, PageCachePolicy.WriteBack);
        X64BootIdentityMappingPlanResult invalidEndResult = Plan(invalidEnd, output);
        X64BootIdentityMappingRequirement[] at36 = [.. source];
        at36[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, (1UL << 36) - 4096, 1UL << 36);
        X64BootIdentityMappingPlanResult valid36 = Plan(at36, output, 512, Context(36));
        X64BootIdentityMappingRequirement[] outside36 = [.. source];
        outside36[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, 1UL << 36, (1UL << 36) + 4096);
        X64BootIdentityMappingPlanResult bad36 = Plan(outside36, output, 512, Context(36));
        X64BootIdentityMappingRequirement[] atCanonical = [.. source];
        atCanonical[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, X64BootIdentityMappingPlan.LowCanonicalEndExclusive - 4096, X64BootIdentityMappingPlan.LowCanonicalEndExclusive);
        X64BootIdentityMappingPlanResult validCanonical = Plan(atCanonical, output, 512, Context(48));
        X64BootIdentityMappingPlanResult valid52 = Plan(atCanonical, output, 512, Context(52));
        X64BootIdentityMappingRequirement[] aboveCanonical = [.. source];
        aboveCanonical[4] = Requirement(X64BootIdentityMappingPurpose.BootHandoff, X64BootIdentityMappingPlan.LowCanonicalEndExclusive, X64BootIdentityMappingPlan.LowCanonicalEndExclusive + 4096);
        X64BootIdentityMappingPlanResult badCanonical = Plan(aboveCanonical, output, 512, Context(52));
        return unalignedResult.Status == X64BootIdentityMappingStatus.MisalignedRange && unalignedResult.InvalidSourceIndex == 4 && reversedResult.Status == X64BootIdentityMappingStatus.EmptyOrReversedRange && mismatchResult.Status == X64BootIdentityMappingStatus.GeometryMismatch && overflowResult.Status == X64BootIdentityMappingStatus.GeometryOverflow && zeroGeometryResult.Status == X64BootIdentityMappingStatus.GeometryMismatch && zeroGeometryResult.AddressStatus == AddressValueStatus.ZeroLength && invalidEndResult.Status == X64BootIdentityMappingStatus.InvalidPhysicalEnd && invalidEndResult.AddressStatus == AddressValueStatus.InvalidAddress && valid36.Succeeded && bad36.Status == X64BootIdentityMappingStatus.PhysicalAddressOutsideWidth && validCanonical.Succeeded && valid52.Succeeded && badCanonical.Status == X64BootIdentityMappingStatus.NonCanonicalIdentityRange;
    }

    private static bool TestTablePageAccounting()
    {
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[64];
        X64BootIdentityMappingRequirement[] twoMiB = [.. CompleteRequirements(), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x001f_f000, 0x0020_0000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x0020_0000, 0x0020_1000)];
        X64BootIdentityMappingPlanResult acrossTwoMiB = Plan(twoMiB, output);
        X64BootIdentityMappingRequirement[] oneGiB = [.. CompleteRequirements(), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x3fff_f000, 0x4000_0000), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x4000_0000, 0x4000_1000)];
        X64BootIdentityMappingPlanResult acrossOneGiB = Plan(oneGiB, output);
        const ulong boundary512GiB = 0x0000_0080_0000_0000;
        X64BootIdentityMappingRequirement[] fiveHundredTwelveGiB = [.. CompleteRequirements(), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, boundary512GiB - 4096, boundary512GiB), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, boundary512GiB, boundary512GiB + 4096)];
        X64BootIdentityMappingPlanResult across512GiB = Plan(fiveHundredTwelveGiB, output);
        X64BootIdentityMappingRequirement[] tableArena = [.. CompleteRequirements(), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x0020_0000, 0x0020_8000, Combine(PageProtection.Read, PageProtection.Write), PageCachePolicy.WriteBack)];
        X64BootIdentityMappingPlanResult arena = Plan(tableArena, output);
        X64BootIdentityMappingPlanResult exactCapacity = Plan(fiveHundredTwelveGiB, output, availableTablePageCapacity: 9);
        bool shortTableDestinationPreserved = FailurePreservesDestination(fiveHundredTwelveGiB, destinationLength: 64, declaredDestinationCapacity: 64, availableTablePageCapacity: 8, poison: 0xa6, out X64BootIdentityMappingPlanResult shortCapacity);
        return acrossTwoMiB.Succeeded && acrossTwoMiB.Pml4PageCount == 1 && acrossTwoMiB.PdptPageCount == 1 && acrossTwoMiB.PdPageCount == 1 && acrossTwoMiB.PtPageCount == 2 && acrossTwoMiB.TotalTablePageCount == 5 && acrossOneGiB.Succeeded && acrossOneGiB.Pml4PageCount == 1 && acrossOneGiB.PdptPageCount == 1 && acrossOneGiB.PdPageCount == 2 && acrossOneGiB.PtPageCount == 3 && acrossOneGiB.TotalTablePageCount == 7 && across512GiB.Succeeded && across512GiB.Pml4PageCount == 1 && across512GiB.PdptPageCount == 2 && across512GiB.PdPageCount == 3 && across512GiB.PtPageCount == 3 && across512GiB.TotalTablePageCount == 9 && across512GiB.TotalTableByteLength == 9 * 4096UL && arena.Succeeded && arena.UnionPageCount == 17 && arena.PtPageCount == 2 && arena.TotalTablePageCount == 5 && exactCapacity.Succeeded && exactCapacity.AvailableTablePageCapacity == 9 && shortCapacity.Status == X64BootIdentityMappingStatus.TableCapacityTooSmall && shortCapacity.TotalTablePageCount == 9 && shortCapacity.OutputRangeCount == 0 && shortTableDestinationPreserved;
    }

    private static unsafe bool TestPointerCore()
    {
        X64BootIdentityMappingRequirement[] source = CompleteRequirements();
        X64BootIdentityMappingRange[] destination = new X64BootIdentityMappingRange[64];
        X64PageTableEncodingPreconditions context = Context();
        X64BootIdentityMappingPlanResult zeroCount = X64BootIdentityMappingPlan.Create(null, 0, 0, null, 0, 0, context);
        X64BootIdentityMappingPlanResult tooMany = X64BootIdentityMappingPlan.Create(null, X64BootIdentityMappingPlan.MaximumSourceCount + 1, X64BootIdentityMappingPlan.MaximumSourceCount + 1, null, 0, 0, context);
        X64BootIdentityMappingPlanResult shortSourceCapacity;
        X64BootIdentityMappingPlanResult excessiveSourceCapacity;
        X64BootIdentityMappingPlanResult excessiveDestinationCapacity;
        X64BootIdentityMappingPlanResult missingSource;
        X64BootIdentityMappingPlanResult missingDestination;
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        fixed (X64BootIdentityMappingRange* destinationPointer = destination)
        {
            shortSourceCapacity = X64BootIdentityMappingPlan.Create(sourcePointer, (ulong)source.Length, (ulong)source.Length - 1, destinationPointer, (ulong)destination.Length, 512, context);
            excessiveSourceCapacity = X64BootIdentityMappingPlan.Create(sourcePointer, (ulong)source.Length, X64BootIdentityMappingPlan.MaximumSourceCount + 1, destinationPointer, (ulong)destination.Length, 512, context);
            excessiveDestinationCapacity = X64BootIdentityMappingPlan.Create(sourcePointer, (ulong)source.Length, (ulong)source.Length, destinationPointer, X64BootIdentityMappingPlan.MaximumDestinationCount + 1, 512, context);
            missingSource = X64BootIdentityMappingPlan.Create(null, (ulong)source.Length, (ulong)source.Length, destinationPointer, (ulong)destination.Length, 512, context);
            missingDestination = X64BootIdentityMappingPlan.Create(sourcePointer, (ulong)source.Length, (ulong)source.Length, null, (ulong)destination.Length, 512, context);
        }

        byte* misalignedSourceStorage = stackalloc byte[sizeof(X64BootIdentityMappingRequirement) + 8];
        byte* misalignedDestinationStorage = stackalloc byte[sizeof(X64BootIdentityMappingRange) + 8];
        X64BootIdentityMappingPlanResult misalignedSource = X64BootIdentityMappingPlan.Create((X64BootIdentityMappingRequirement*)(misalignedSourceStorage + 1), 1, 1, (X64BootIdentityMappingRange*)AlignPointer(misalignedDestinationStorage, X64BootIdentityMappingPlan.RequiredPointerAlignment), 1, 4, context);
        X64BootIdentityMappingPlanResult misalignedDestination = X64BootIdentityMappingPlan.Create((X64BootIdentityMappingRequirement*)AlignPointer(misalignedSourceStorage, X64BootIdentityMappingPlan.RequiredPointerAlignment), 1, 1, (X64BootIdentityMappingRange*)(misalignedDestinationStorage + 1), 1, 4, context);
        nuint nearMaximum = unchecked((nuint)(~0UL - 7));
        X64BootIdentityMappingPlanResult sourceOverflow = X64BootIdentityMappingPlan.Create((X64BootIdentityMappingRequirement*)nearMaximum, 1, 1, (X64BootIdentityMappingRange*)0x1000, 1, 4, context);
        X64BootIdentityMappingPlanResult destinationOverflow = X64BootIdentityMappingPlan.Create((X64BootIdentityMappingRequirement*)0x1000, 1, 1, (X64BootIdentityMappingRange*)nearMaximum, 1, 4, context);
        byte* overlapStorage = stackalloc byte[1024];
        void* alignedOverlap = AlignPointer(overlapStorage, X64BootIdentityMappingPlan.RequiredPointerAlignment);
        X64BootIdentityMappingPlanResult overlap = X64BootIdentityMappingPlan.Create((X64BootIdentityMappingRequirement*)alignedOverlap, 1, 8, (X64BootIdentityMappingRange*)alignedOverlap, 8, 4, context);
        ulong[] interiorAliasSnapshot;
        X64BootIdentityMappingPlanResult emptyInteriorDestination;
        X64BootIdentityMappingPlanResult partialSpanOverlap;
        bool emptyInteriorSourceAndDestinationUnchanged;
        bool partialOverlapSourceUnchanged;
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        {
            interiorAliasSnapshot = CopyBytesAsUlongs(sourcePointer, source.Length * sizeof(X64BootIdentityMappingRequirement));
            emptyInteriorDestination = X64BootIdentityMappingPlan.Create(sourcePointer, (ulong)source.Length, (ulong)source.Length, (X64BootIdentityMappingRange*)(sourcePointer + 1), 0, 512, context);
            emptyInteriorSourceAndDestinationUnchanged = EqualBytes(interiorAliasSnapshot, sourcePointer, source.Length * sizeof(X64BootIdentityMappingRequirement));
            partialSpanOverlap = X64BootIdentityMappingPlan.Create(sourcePointer, (ulong)source.Length, (ulong)source.Length, (X64BootIdentityMappingRange*)((byte*)sourcePointer + 8), 1, 512, context);
            partialOverlapSourceUnchanged = EqualBytes(interiorAliasSnapshot, sourcePointer, source.Length * sizeof(X64BootIdentityMappingRequirement));
        }

        X64BootIdentityMappingRequirement[] maximumSource = new X64BootIdentityMappingRequirement[X64BootIdentityMappingPlan.MaximumSourceCount];
        Array.Copy(source, maximumSource, source.Length);
        for (int index = source.Length; index < maximumSource.Length; index++)
        {
            maximumSource[index] = Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, 0x10000, 0x11000);
        }

        X64BootIdentityMappingRange[] maximumDestination = new X64BootIdentityMappingRange[X64BootIdentityMappingPlan.MaximumDestinationCount];
        X64BootIdentityMappingPlanResult maximum = Plan(maximumSource, maximumDestination);
        X64BootIdentityMappingRange[] exactDestination = new X64BootIdentityMappingRange[5];
        X64BootIdentityMappingPlanResult exactDestinationResult = Plan(source, exactDestination);
        bool shortDestinationPreserved = FailurePreservesDestination(source, destinationLength: 32, declaredDestinationCapacity: 4, availableTablePageCapacity: 512, poison: 0xb7, out X64BootIdentityMappingPlanResult shortDestinationResult);
        return zeroCount.Status == X64BootIdentityMappingStatus.InvalidSourceCount && tooMany.Status == X64BootIdentityMappingStatus.TooManySources && shortSourceCapacity.Status == X64BootIdentityMappingStatus.SourceCapacityTooSmall && excessiveSourceCapacity.Status == X64BootIdentityMappingStatus.CapacityExceedsLimit && excessiveDestinationCapacity.Status == X64BootIdentityMappingStatus.CapacityExceedsLimit && missingSource.Status == X64BootIdentityMappingStatus.MissingSource && missingDestination.Status == X64BootIdentityMappingStatus.MissingDestination && misalignedSource.Status == X64BootIdentityMappingStatus.MisalignedSource && misalignedDestination.Status == X64BootIdentityMappingStatus.MisalignedDestination && sourceOverflow.Status == X64BootIdentityMappingStatus.SourceSpanOverflow && destinationOverflow.Status == X64BootIdentityMappingStatus.DestinationSpanOverflow && overlap.Status == X64BootIdentityMappingStatus.SourceDestinationOverlap && emptyInteriorDestination.Status == X64BootIdentityMappingStatus.DestinationCapacityTooSmall && emptyInteriorDestination.RequiredDestinationCapacity == 5 && emptyInteriorDestination.OutputRangeCount == 0 && emptyInteriorSourceAndDestinationUnchanged && partialSpanOverlap.Status == X64BootIdentityMappingStatus.SourceDestinationOverlap && partialSpanOverlap.OutputRangeCount == 0 && partialOverlapSourceUnchanged && maximum.Succeeded && maximum.SourceCount == X64BootIdentityMappingPlan.MaximumSourceCount && exactDestinationResult.Succeeded && exactDestinationResult.OutputRangeCount == 5 && shortDestinationResult.Status == X64BootIdentityMappingStatus.DestinationCapacityTooSmall && shortDestinationResult.RequiredDestinationCapacity == 5 && shortDestinationResult.OutputRangeCount == 0 && shortDestinationPreserved;
    }

    private static unsafe bool TestHostilePodAndAtomicity()
    {
        X64BootIdentityMappingRequirement[] source = CompleteRequirements();
        X64BootIdentityMappingRange[] output = new X64BootIdentityMappingRange[32];
        X64BootIdentityMappingRequirement[] badInit = [.. source];
        badInit[4] = OverwriteByte(badInit[4], byteOffset: 32, replacement: 2);
        X64BootIdentityMappingPlanResult badInitResult = Plan(badInit, output);
        for (int offset = 36; offset < 40; offset++)
        {
            X64BootIdentityMappingRequirement[] badReserved = [.. source];
            badReserved[4] = OverwriteByte(badReserved[4], offset, 1);
            X64BootIdentityMappingPlanResult result = Plan(badReserved, output);
            if (result.Status != X64BootIdentityMappingStatus.InvalidRequirementState || result.InvalidSourceIndex != 4)
            {
                return false;
            }
        }

        X64BootIdentityMappingRequirement[] badPurpose = [.. source];
        badPurpose[4] = OverwriteByte(badPurpose[4], byteOffset: 33, replacement: 0xff);
        X64BootIdentityMappingPlanResult badPurposeResult = Plan(badPurpose, output);
        X64PageTableEncodingPreconditions malformedInit = OverwriteByte(Context(), byteOffset: 16, replacement: 2);
        X64BootIdentityMappingPlanResult malformedContext = Plan(source, output, 512, malformedInit);
        X64PageTableEncodingPreconditions malformedReserved = OverwriteByte(Context(), byteOffset: 22, replacement: 1);
        X64BootIdentityMappingPlanResult reservedContext = Plan(source, output, 512, malformedReserved);
        X64BootIdentityMappingRequirement[] badLast = [.. source];
        badLast[^1] = default;
        X64BootIdentityMappingRange[] failureDestination = new X64BootIdentityMappingRange[32];
        fixed (X64BootIdentityMappingRange* failurePointer = failureDestination)
        {
            FillBytes(failurePointer, failureDestination.Length * sizeof(X64BootIdentityMappingRange), 0xa5);
        }

        ulong[] beforeFailure;
        fixed (X64BootIdentityMappingRange* failurePointer = failureDestination)
        {
            beforeFailure = CopyBytesAsUlongs(failurePointer, failureDestination.Length * sizeof(X64BootIdentityMappingRange));
        }

        X64BootIdentityMappingPlanResult badLastResult = Plan(badLast, failureDestination);
        bool failureAtomic;
        fixed (X64BootIdentityMappingRange* failurePointer = failureDestination)
        {
            failureAtomic = EqualBytes(beforeFailure, failurePointer, failureDestination.Length * sizeof(X64BootIdentityMappingRange));
        }

        ulong[] sourceSnapshot;
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        {
            sourceSnapshot = CopyBytesAsUlongs(sourcePointer, source.Length * sizeof(X64BootIdentityMappingRequirement));
        }

        X64BootIdentityMappingRange[] canaryStorage = new X64BootIdentityMappingRange[7];
        fixed (X64BootIdentityMappingRange* canaryPointer = canaryStorage)
        {
            FillBytes(canaryPointer, canaryStorage.Length * sizeof(X64BootIdentityMappingRange), 0x5a);
        }

        ulong[] firstCanary;
        ulong[] lastCanary;
        fixed (X64BootIdentityMappingRange* canaryPointer = canaryStorage)
        {
            firstCanary = CopyBytesAsUlongs(canaryPointer, sizeof(X64BootIdentityMappingRange));
            lastCanary = CopyBytesAsUlongs(canaryPointer + 6, sizeof(X64BootIdentityMappingRange));
        }

        X64BootIdentityMappingPlanResult canaryResult;
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        fixed (X64BootIdentityMappingRange* canaryPointer = canaryStorage)
        {
            canaryResult = X64BootIdentityMappingPlan.Create(sourcePointer, (ulong)source.Length, (ulong)source.Length, canaryPointer + 1, 5, 512, Context());
        }

        bool sourceUnchanged;
        bool canariesUnchanged;
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        fixed (X64BootIdentityMappingRange* canaryPointer = canaryStorage)
        {
            sourceUnchanged = EqualBytes(sourceSnapshot, sourcePointer, source.Length * sizeof(X64BootIdentityMappingRequirement));
            canariesUnchanged = EqualBytes(firstCanary, canaryPointer, sizeof(X64BootIdentityMappingRange)) && EqualBytes(lastCanary, canaryPointer + 6, sizeof(X64BootIdentityMappingRange));
        }

        X64BootIdentityMappingRequirement canonicalRequirement = source[4];
        X64BootIdentityMappingRequirement forgedRequirement = OverwriteByte(canonicalRequirement, byteOffset: 36, replacement: 1);
        X64BootIdentityMappingPlanResult successResult = Plan(source, output);
        bool allResultReservedBytesRejected = true;
        for (int offset = 4; offset < 8; offset++)
        {
            X64BootIdentityMappingPlanResult forgedResult = OverwriteByte(successResult, byteOffset: offset, replacement: 1);
            if (forgedResult.IsInitialized || forgedResult.Succeeded)
            {
                allResultReservedBytesRejected = false;
                break;
            }
        }

        bool allRangeReservedBytesRejected = true;
        for (int offset = 59; offset < 64; offset++)
        {
            X64BootIdentityMappingRange forgedRange = OverwriteByte(output[0], byteOffset: offset, replacement: 1);
            if (forgedRange.IsInitialized)
            {
                allRangeReservedBytesRejected = false;
                break;
            }
        }

        return badInitResult.Status == X64BootIdentityMappingStatus.InvalidRequirementState && badInitResult.InvalidSourceIndex == 4 && badPurposeResult.Status == X64BootIdentityMappingStatus.InvalidPurpose && badPurposeResult.InvalidSourceIndex == 4 && malformedContext.Status == X64BootIdentityMappingStatus.InvalidPreconditions && malformedContext.PageTableEntryStatus == X64PageTableEntryStatus.InvalidPreconditionState && reservedContext.Status == X64BootIdentityMappingStatus.InvalidPreconditions && reservedContext.PageTableEntryStatus == X64PageTableEntryStatus.InvalidPreconditionState && badLastResult.Status == X64BootIdentityMappingStatus.InvalidRequirementState && badLastResult.InvalidSourceIndex == (ulong)(badLast.Length - 1) && failureAtomic && canaryResult.Succeeded && sourceUnchanged && canariesUnchanged && canonicalRequirement.IsInitialized && !forgedRequirement.IsInitialized && successResult.IsInitialized && successResult.Succeeded && output[0].IsInitialized && allResultReservedBytesRejected && allRangeReservedBytesRejected;
    }

    private static bool TestSourceAndNoStdLibBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string boot = Path.Combine(root, "src", "OS.Kernel", "Boot");
        string memory = Path.Combine(root, "src", "OS.Kernel", "Memory");
        string addressPath = Path.Combine(boot, "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
        string protectionPath = Path.Combine(memory, "PageProtection.cs");
        string cachePath = Path.Combine(memory, "PageCachePolicy.cs");
        string entryPath = Path.Combine(memory, "X64PageTableEntry.cs");
        string planPath = Path.Combine(boot, "X64BootIdentityMappingPlan.cs");
        string[] paths = [addressPath, protectionPath, cachePath, entryPath, planPath];
        if (paths.Any(path => !File.Exists(path)))
        {
            return false;
        }

        string core = RemoveComments(File.ReadAllText(planPath));
        string[] forbiddenFragments = ["using System;", "System.FlagsAttribute", "[Flags", "Span<", "ReadOnlySpan<", "stackalloc", "throw ", "try {", "catch ", "finally", "typeof(", "dynamic ", "EfiEntry", "BootSequence", "RuntimeExport", "ulong.MaxValue"];
        if (forbiddenFragments.Any(fragment => core.Contains(fragment, StringComparison.Ordinal)) || Regex.IsMatch(core, @"\b(string|String|object|Object)\b", RegexOptions.CultureInvariant) || !core.Contains("~0UL", StringComparison.Ordinal) || !core.Contains("static unsafe class X64BootIdentityMappingPlan", StringComparison.Ordinal) || !core.Contains("X64BootIdentityMappingRequirement* source", StringComparison.Ordinal) || !core.Contains("X64BootIdentityMappingRange* destination", StringComparison.Ordinal) || !core.Contains("sourceCapacity", StringComparison.Ordinal) || !core.Contains("destinationCapacity", StringComparison.Ordinal) || !core.Contains("availableTablePageCapacity", StringComparison.Ordinal) || !core.Contains("sizeof(X64BootIdentityMappingRequirement)", StringComparison.Ordinal) || !core.Contains("sizeof(X64BootIdentityMappingRange)", StringComparison.Ordinal))
        {
            return false;
        }

        string[] productionFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories).Where(path => !HasDirectorySegment(path, "bin") && !HasDirectorySegment(path, "obj")).ToArray();
        string[] planDeclarations = productionFiles.Where(path => Regex.IsMatch(RemoveComments(File.ReadAllText(path)), @"\bclass\s+X64BootIdentityMappingPlan\b", RegexOptions.CultureInvariant)).ToArray();
        if (planDeclarations.Length != 1 || !Path.GetFullPath(planDeclarations[0]).Equals(Path.GetFullPath(planPath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        NoStdLibBuildResult build = BuildNoStdLibClosure(root, addressPath, protectionPath, cachePath, entryPath, planPath);
        if (build.TimedOut || build.ExitCode != 0 || !HasZeroAssemblyReferences(build.AssemblyBytes))
        {
            Console.WriteLine("M01_012_NOSTDLIB_DIAGNOSTIC " + NormalizeOutput(build.Output));
            return false;
        }

        return true;
    }

    private static bool TestEvaluatedEfiBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string productionPath = Path.Combine(root, "src", "OS.Kernel", "Boot", "X64BootIdentityMappingPlan.cs");
        string protectionPath = Path.Combine(root, "src", "OS.Kernel", "Memory", "PageProtection.cs");
        string cachePath = Path.Combine(root, "src", "OS.Kernel", "Memory", "PageCachePolicy.cs");
        string entryPath = Path.Combine(root, "src", "OS.Kernel", "Memory", "X64PageTableEntry.cs");
        string ownerPath = Path.Combine(root, "tests", "OS.HostTests", "X64BootIdentityMappingPlanTests.cs");
        string efiProject = Path.Combine(root, "src", "OS", "OS.csproj");
        if (!File.Exists(productionPath) || !File.Exists(protectionPath) || !File.Exists(cachePath) || !File.Exists(entryPath) || !File.Exists(ownerPath) || !File.Exists(efiProject))
        {
            Console.WriteLine("M01_012_EFI_BOUNDARY_DIAGNOSTIC missing_path");
            return false;
        }

        if (!BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(@"..\OS.Kernel\Memory\PageProtection.cs", @"Memory\PageProtection.cs") || !BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(@"..\OS.Kernel\Memory\PageCachePolicy.cs", @"Memory\PageCachePolicy.cs") || !BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(@"..\OS.Kernel\Memory\X64PageTableEntry.cs", @"Memory\X64PageTableEntry.cs") || !BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(@"..\OS.Kernel\Boot\X64BootIdentityMappingPlan.cs", @"Boot\X64BootIdentityMappingPlan.cs") || !EvaluatedEfiClosure(root, efiProject, "SingleMinimalApi", [protectionPath, cachePath, entryPath, productionPath], [ownerPath]))
        {
            Console.WriteLine("M01_012_EFI_BOUNDARY_DIAGNOSTIC efi_single");
            return false;
        }

        if (!EvaluatedEfiClosure(root, efiProject, "MultiMinimalApi", [protectionPath, cachePath, entryPath, productionPath], [ownerPath]))
        {
            Console.WriteLine("M01_012_EFI_BOUNDARY_DIAGNOSTIC efi_multi");
            return false;
        }

        return true;
    }

    private static bool TestMarkerBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string ownerPath = Path.Combine(root, "tests", "OS.HostTests", "X64BootIdentityMappingPlanTests.cs");
        if (!File.Exists(ownerPath))
        {
            Console.WriteLine("M01_012_MARKER_DIAGNOSTIC missing_owner");
            return false;
        }

        string owner = File.ReadAllText(ownerPath);
        int failureGate = owner.IndexOf("if (failures != 0)", StringComparison.Ordinal);
        int aggregateFailure = owner.IndexOf("Console.WriteLine(\"TEST_FAIL \" + IssueTestName", StringComparison.Ordinal);
        int aggregateThrow = owner.IndexOf("\"M01-012 x64 boot identity mapping plan tests failed:", StringComparison.Ordinal);
        int readyWrite = owner.IndexOf("Console.WriteLine(ReadyMarker)", StringComparison.Ordinal);
        int aggregatePass = owner.IndexOf("Console.WriteLine(\"TEST_PASS \" + IssueTestName)", StringComparison.Ordinal);
        if (CountOccurrences(owner, "\"" + ReadyMarker + "\"") != 1 || CountOccurrences(owner, "Console.WriteLine(\"TEST_START \" + IssueTestName)") != 1 || CountOccurrences(owner, "Console.WriteLine(\"TEST_FAIL \" + IssueTestName") != 1 || CountOccurrences(owner, "Console.WriteLine(\"TEST_PASS \" + IssueTestName)") != 1 || failureGate < 0 || aggregateFailure <= failureGate || aggregateThrow <= aggregateFailure || readyWrite <= aggregateThrow || aggregatePass <= readyWrite || owner.Contains("OS_" + "BOOT_OK", StringComparison.Ordinal) || owner.Contains("OS_" + "SMOKE_PASS", StringComparison.Ordinal) || owner.Contains("OS_" + "SMOKE_FAIL", StringComparison.Ordinal))
        {
            Console.WriteLine("M01_012_MARKER_DIAGNOSTIC marker_order" + " ready_literals=" + CountOccurrences(owner, "\"" + ReadyMarker + "\"") + " start_writes=" + CountOccurrences(owner, "Console.WriteLine(\"TEST_START \" + IssueTestName)") + " fail_writes=" + CountOccurrences(owner, "Console.WriteLine(\"TEST_FAIL \" + IssueTestName") + " pass_writes=" + CountOccurrences(owner, "Console.WriteLine(\"TEST_PASS \" + IssueTestName") + " indexes=" + failureGate + "," + aggregateFailure + "," + aggregateThrow + "," + readyWrite + "," + aggregatePass);
            return false;
        }

        string[] scanRoots = ["src", "std", "apps", "tools", "tests", "scripts", Path.Combine(".gitea", "workflows")];
        foreach (string relativeRoot in scanRoots)
        {
            string scanRoot = Path.Combine(root, relativeRoot);
            if (!Directory.Exists(scanRoot))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFullPath(file).Equals(Path.GetFullPath(ownerPath), StringComparison.OrdinalIgnoreCase) || HasDirectorySegment(file, "bin") || HasDirectorySegment(file, "obj") || HasDirectorySegment(file, "artifacts") || !IsExecutableSource(file))
                {
                    continue;
                }

                if (File.ReadAllText(file).Contains(ReadyMarker, StringComparison.Ordinal))
                {
                    Console.WriteLine("M01_012_MARKER_DIAGNOSTIC duplicate=" + Path.GetRelativePath(root, file));
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasExactFieldOffsets()
    {
        return Offset<X64BootIdentityMappingRequirement>("_physicalStart") == 0 && Offset<X64BootIdentityMappingRequirement>("_physicalEndExclusive") == 8 && Offset<X64BootIdentityMappingRequirement>("_pageCount") == 16 && Offset<X64BootIdentityMappingRequirement>("_byteLength") == 24 && Offset<X64BootIdentityMappingRequirement>("_isInitialized") == 32 && Offset<X64BootIdentityMappingRequirement>("_purpose") == 33 && Offset<X64BootIdentityMappingRequirement>("_protection") == 34 && Offset<X64BootIdentityMappingRequirement>("_cachePolicy") == 35 && Offset<X64BootIdentityMappingRequirement>("_reserved") == 36 && Offset<X64BootIdentityMappingRange>("_physicalStart") == 0 && Offset<X64BootIdentityMappingRange>("_physicalEndExclusive") == 8 && Offset<X64BootIdentityMappingRange>("_virtualStart") == 16 && Offset<X64BootIdentityMappingRange>("_virtualEndExclusive") == 24 && Offset<X64BootIdentityMappingRange>("_pageCount") == 32 && Offset<X64BootIdentityMappingRange>("_byteLength") == 40 && Offset<X64BootIdentityMappingRange>("_contributingPurposeMask") == 48 && Offset<X64BootIdentityMappingRange>("_isInitialized") == 56 && Offset<X64BootIdentityMappingRange>("_protection") == 57 && Offset<X64BootIdentityMappingRange>("_cachePolicy") == 58 && Offset<X64BootIdentityMappingRange>("_reserved0") == 59 && Offset<X64BootIdentityMappingRange>("_reserved1") == 60 && Offset<X64BootIdentityMappingPlanResult>("_isInitialized") == 0 && Offset<X64BootIdentityMappingPlanResult>("_status") == 1 && Offset<X64BootIdentityMappingPlanResult>("_addressStatus") == 2 && Offset<X64BootIdentityMappingPlanResult>("_pageTableEntryStatus") == 3 && Offset<X64BootIdentityMappingPlanResult>("_reserved") == 4 && Offset<X64BootIdentityMappingPlanResult>("_sourceCount") == 8 && Offset<X64BootIdentityMappingPlanResult>("_validatedSourceCount") == 16 && Offset<X64BootIdentityMappingPlanResult>("_sourceCapacity") == 24 && Offset<X64BootIdentityMappingPlanResult>("_destinationCapacity") == 32 && Offset<X64BootIdentityMappingPlanResult>("_requiredDestinationCapacity") == 40 && Offset<X64BootIdentityMappingPlanResult>("_outputRangeCount") == 48 && Offset<X64BootIdentityMappingPlanResult>("_availableTablePageCapacity") == 56 && Offset<X64BootIdentityMappingPlanResult>("_requiredPurposeMask") == 64 && Offset<X64BootIdentityMappingPlanResult>("_observedPurposeMask") == 72 && Offset<X64BootIdentityMappingPlanResult>("_missingPurposeMask") == 80 && Offset<X64BootIdentityMappingPlanResult>("_invalidSourceIndex") == 88 && Offset<X64BootIdentityMappingPlanResult>("_conflictingSourceIndex") == 96 && Offset<X64BootIdentityMappingPlanResult>("_guardSourceIndex") == 104 && Offset<X64BootIdentityMappingPlanResult>("_unionPageCount") == 112 && Offset<X64BootIdentityMappingPlanResult>("_unionByteLength") == 120 && Offset<X64BootIdentityMappingPlanResult>("_pml4PageCount") == 128 && Offset<X64BootIdentityMappingPlanResult>("_pdptPageCount") == 136 && Offset<X64BootIdentityMappingPlanResult>("_pdPageCount") == 144 && Offset<X64BootIdentityMappingPlanResult>("_ptPageCount") == 152 && Offset<X64BootIdentityMappingPlanResult>("_totalTablePageCount") == 160 && Offset<X64BootIdentityMappingPlanResult>("_totalTableByteLength") == 168;
    }

    private static X64BootIdentityMappingRequirement Requirement(X64BootIdentityMappingPurpose purpose, ulong start, ulong endExclusive, PageProtection protection, PageCachePolicy cachePolicy)
    {
        ulong byteLength = endExclusive - start;
        return new X64BootIdentityMappingRequirement(purpose, Physical(start), Physical(endExclusive), byteLength / X64BootIdentityMappingPlan.PageSize, byteLength, protection, cachePolicy);
    }

    private static X64BootIdentityMappingRequirement Requirement(X64BootIdentityMappingPurpose purpose, ulong start, ulong endExclusive)
    {
        return Requirement(purpose, start, endExclusive, PageProtection.Read, PageCachePolicy.WriteBack);
    }

    private static X64BootIdentityMappingRequirement Guard(ulong start, ulong endExclusive)
    {
        return Requirement(X64BootIdentityMappingPurpose.BootStackGuard, start, endExclusive, PageProtection.None, PageCachePolicy.Uninitialized);
    }

    private static X64BootIdentityMappingRequirement[] CompleteRequirements()
    {
        return [Guard(0x1000, 0x2000), Requirement(X64BootIdentityMappingPurpose.ExecutingImage, 0x2000, 0x4000, Combine(PageProtection.Read, PageProtection.Execute), PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.ExecutingImage, 0x4000, 0x5000, Combine(PageProtection.Read, PageProtection.Write), PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.OwnedStack, 0x5000, 0x7000, Combine(PageProtection.Read, PageProtection.Write), PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.BootHandoff, 0x7000, 0x8000), Requirement(X64BootIdentityMappingPurpose.SerialStub, 0x8000, 0x9000, Combine(PageProtection.Read, PageProtection.Execute), PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.AcpiScratch, 0x9000, 0xa000), Requirement(X64BootIdentityMappingPurpose.FramebufferMetadata, 0xa000, 0xb000)];
    }

    private static X64BootIdentityMappingPurpose[] LiveIntervalMappedPurposes()
    {
        return [X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, X64BootIdentityMappingPurpose.CompositeOwnedStack, X64BootIdentityMappingPurpose.ProtectionProbePage, X64BootIdentityMappingPurpose.CompleteTableAndStateArena, X64BootIdentityMappingPurpose.RuntimeStateAndCanaryPage, X64BootIdentityMappingPurpose.OwnedRootPage];
    }

    private static X64BootIdentityMappingRequirement[] LiveIntervalRequirements()
    {
        PageProtection readWrite = Combine(PageProtection.Read, PageProtection.Write);
        PageProtection readExecute = Combine(PageProtection.Read, PageProtection.Execute);
        return [Guard(0x4000, 0x5000), Requirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, 0x2000, 0x4000, readExecute, PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.CompositeOwnedStack, 0x5000, 0x7000, readWrite, PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.ProtectionProbePage, 0x8000, 0x9000, readWrite, PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.CompleteTableAndStateArena, 0x9000, 0xd000, readWrite, PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.RuntimeStateAndCanaryPage, 0xa000, 0xb000, readWrite, PageCachePolicy.WriteBack), Requirement(X64BootIdentityMappingPurpose.OwnedRootPage, 0xc000, 0xd000, readWrite, PageCachePolicy.WriteBack)];
    }

    private static unsafe X64BootIdentityMappingPlanResult Plan(X64BootIdentityMappingRequirement[] source, X64BootIdentityMappingRange[] destination, ulong availableTablePageCapacity = 512, ulong? sourceCapacity = null, ulong? destinationCapacity = null)
    {
        return Plan(source, destination, availableTablePageCapacity, Context(), sourceCapacity, destinationCapacity);
    }

    private static unsafe X64BootIdentityMappingPlanResult Plan(X64BootIdentityMappingRequirement[] source, X64BootIdentityMappingRange[] destination, ulong availableTablePageCapacity, X64PageTableEncodingPreconditions preconditions, ulong? sourceCapacity = null, ulong? destinationCapacity = null)
    {
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        fixed (X64BootIdentityMappingRange* destinationPointer = destination)
        {
            return X64BootIdentityMappingPlan.Create(sourcePointer, (ulong)source.Length, sourceCapacity ?? (ulong)source.Length, destinationPointer, destinationCapacity ?? (ulong)destination.Length, availableTablePageCapacity, preconditions);
        }
    }

    private static unsafe X64BootIdentityMappingPlanResult PlanLiveInterval(X64BootIdentityMappingRequirement[] source, X64BootIdentityMappingRange[] destination, ulong availableTablePageCapacity = 512, byte physicalAddressWidth = 48, ulong? sourceCapacity = null, ulong? destinationCapacity = null)
    {
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        fixed (X64BootIdentityMappingRange* destinationPointer = destination)
        {
            return X64BootIdentityMappingPlan.CreateLiveInterval(sourcePointer, (ulong)source.Length, sourceCapacity ?? (ulong)source.Length, destinationPointer, destinationCapacity ?? (ulong)destination.Length, availableTablePageCapacity, Context(physicalAddressWidth));
        }
    }

    private static unsafe bool LiveIntervalFailureIsAtomic(X64BootIdentityMappingRequirement[] source, X64BootIdentityMappingStatus expectedStatus, ulong expectedInvalidSourceIndex, ulong expectedConflictingSourceIndex, ulong expectedMissingPurposeMask = X64BootIdentityMappingPlan.RequiredLiveIntervalPurposeMask, byte physicalAddressWidth = 48, ulong declaredDestinationCapacity = 16, ulong availableTablePageCapacity = 512)
    {
        X64BootIdentityMappingRange[] destination = new X64BootIdentityMappingRange[16];
        ulong[] sourceBefore;
        ulong[] destinationBefore;
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        fixed (X64BootIdentityMappingRange* destinationPointer = destination)
        {
            sourceBefore = CopyBytesAsUlongs(sourcePointer, source.Length * sizeof(X64BootIdentityMappingRequirement));
            FillBytes(destinationPointer, destination.Length * sizeof(X64BootIdentityMappingRange), 0xa5);
            destinationBefore = CopyBytesAsUlongs(destinationPointer, destination.Length * sizeof(X64BootIdentityMappingRange));
        }

        X64BootIdentityMappingPlanResult result = PlanLiveInterval(source, destination, availableTablePageCapacity, physicalAddressWidth, destinationCapacity: declaredDestinationCapacity);
        bool sourceUnchanged;
        bool destinationUnchanged;
        fixed (X64BootIdentityMappingRequirement* sourcePointer = source)
        fixed (X64BootIdentityMappingRange* destinationPointer = destination)
        {
            sourceUnchanged = EqualBytes(sourceBefore, sourcePointer, source.Length * sizeof(X64BootIdentityMappingRequirement));
            destinationUnchanged = EqualBytes(destinationBefore, destinationPointer, destination.Length * sizeof(X64BootIdentityMappingRange));
        }

        bool passed = result.Status == expectedStatus && result.RequiredPurposeMask == 0x7e00 && result.MissingPurposeMask == expectedMissingPurposeMask && result.InvalidSourceIndex == expectedInvalidSourceIndex && result.ConflictingSourceIndex == expectedConflictingSourceIndex && result.OutputRangeCount == 0 && sourceUnchanged && destinationUnchanged;
        if (!passed)
        {
            Console.WriteLine("M01_030_PLANNER_DIAGNOSTIC expected=" + expectedStatus + " actual=" + result.Status + " expectedInvalid=" + expectedInvalidSourceIndex + " actualInvalid=" + result.InvalidSourceIndex + " expectedConflict=" + expectedConflictingSourceIndex + " actualConflict=" + result.ConflictingSourceIndex + " expectedMissing=0x" + expectedMissingPurposeMask.ToString("X") + " actualMissing=0x" + result.MissingPurposeMask.ToString("X") + " sourceUnchanged=" + sourceUnchanged + " destinationUnchanged=" + destinationUnchanged);
        }

        return passed;
    }

    private static unsafe bool FailurePreservesDestination(X64BootIdentityMappingRequirement[] source, int destinationLength, ulong declaredDestinationCapacity, ulong availableTablePageCapacity, byte poison, out X64BootIdentityMappingPlanResult result)
    {
        X64BootIdentityMappingRange[] destination = new X64BootIdentityMappingRange[destinationLength];
        ulong[] before;
        fixed (X64BootIdentityMappingRange* destinationPointer = destination)
        {
            FillBytes(destinationPointer, destination.Length * sizeof(X64BootIdentityMappingRange), poison);
            before = CopyBytesAsUlongs(destinationPointer, destination.Length * sizeof(X64BootIdentityMappingRange));
        }

        result = Plan(source, destination, availableTablePageCapacity, Context(), destinationCapacity: declaredDestinationCapacity);
        fixed (X64BootIdentityMappingRange* destinationPointer = destination)
        {
            return EqualBytes(before, destinationPointer, destination.Length * sizeof(X64BootIdentityMappingRange));
        }
    }

    private static PageProtection Combine(PageProtection left, PageProtection right)
    {
        return (PageProtection)((byte)left | (byte)right);
    }

    private static PhysicalAddress PhysicalUnchecked(ulong value)
    {
        return Unsafe.As<ulong, PhysicalAddress>(ref value);
    }

    private static unsafe void* AlignPointer(void* pointer, ulong alignment)
    {
        nuint raw = (nuint)pointer;
        nuint mask = (nuint)(alignment - 1);
        return (void*)((raw + mask) & ~mask);
    }

    private static bool RangeEquals(X64BootIdentityMappingRange range, ulong start, ulong endExclusive, ulong pageCount, ulong purposeMask, PageProtection protection, PageCachePolicy cachePolicy)
    {
        ulong byteLength = endExclusive - start;
        return range.IsInitialized && RawEquals(range.PhysicalStart, start) && RawEquals(range.PhysicalEndExclusive, endExclusive) && RawEquals(range.VirtualStart, start) && RawEquals(range.VirtualEndExclusive, endExclusive) && range.PageCount == pageCount && range.ByteLength == byteLength && range.ContributingPurposeMask == purposeMask && range.Protection == protection && range.CachePolicy == cachePolicy;
    }

    private static bool OutputEquals(X64BootIdentityMappingRange[] left, ulong leftCount, X64BootIdentityMappingRange[] right, ulong rightCount)
    {
        if (leftCount != rightCount || leftCount > (ulong)left.Length || rightCount > (ulong)right.Length)
        {
            return false;
        }

        for (ulong index = 0; index < leftCount; index++)
        {
            X64BootIdentityMappingRange leftRange = left[(int)index];
            X64BootIdentityMappingRange rightRange = right[(int)index];
            if (!leftRange.IsInitialized || !rightRange.IsInitialized || !RawSame(leftRange.PhysicalStart, rightRange.PhysicalStart) || !RawSame(leftRange.PhysicalEndExclusive, rightRange.PhysicalEndExclusive) || !RawSame(leftRange.VirtualStart, rightRange.VirtualStart) || !RawSame(leftRange.VirtualEndExclusive, rightRange.VirtualEndExclusive) || leftRange.PageCount != rightRange.PageCount || leftRange.ByteLength != rightRange.ByteLength || leftRange.ContributingPurposeMask != rightRange.ContributingPurposeMask || leftRange.Protection != rightRange.Protection || leftRange.CachePolicy != rightRange.CachePolicy)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RawSame(PhysicalAddress left, PhysicalAddress right)
    {
        return left.TryGetRawValueForHardwareRegisterBoundary(out ulong leftRaw) == AddressValueStatus.Success && right.TryGetRawValueForHardwareRegisterBoundary(out ulong rightRaw) == AddressValueStatus.Success && leftRaw == rightRaw;
    }

    private static bool RawSame(VirtualAddress left, VirtualAddress right)
    {
        return left.TryGetRawValueForHardwareRegisterBoundary(out ulong leftRaw) == AddressValueStatus.Success && right.TryGetRawValueForHardwareRegisterBoundary(out ulong rightRaw) == AddressValueStatus.Success && leftRaw == rightRaw;
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

    private static X64PatMappingToken ValidPat()
    {
        X64PageTableEntryStatus status = X64PatMappingToken.TryCreate(PatFixture, writeBackIndex: 0, uncachedIndex: 3, writeCombiningIndex: 4, out X64PatMappingToken token);
        if (status != X64PageTableEntryStatus.Success)
        {
            throw new InvalidOperationException("The M01-012 PAT fixture is invalid.");
        }

        return token;
    }

    private static X64PageTableEncodingPreconditions Context(byte physicalAddressWidth = 48, X64PatMappingToken pat = default)
    {
        return X64PageTableEncodingPreconditions.CreateObserved(physicalAddressWidth, nxeEnabled: true, writeProtectEnabled: true, fiveLevelPagingEnabled: false, oneGiBPagesSupported: true, pat.IsInitialized ? pat : ValidPat());
    }

    private static PhysicalAddress Physical(ulong value)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateFromHardwareRegisterBoundary(value, out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Test physical address was invalid: 0x" + value.ToString("X16"));
        }

        return address;
    }

    private static VirtualAddress Virtual(ulong value)
    {
        AddressValueStatus status = VirtualAddress.TryCreateFromHardwareRegisterBoundary(value, out VirtualAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Test virtual address was invalid: 0x" + value.ToString("X16"));
        }

        return address;
    }

    private static bool RawEquals(PhysicalAddress address, ulong expected)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(out ulong actual) == AddressValueStatus.Success && actual == expected;
    }

    private static bool RawEquals(VirtualAddress address, ulong expected)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(out ulong actual) == AddressValueStatus.Success && actual == expected;
    }

    private static unsafe T OverwriteByte<T>(T value, int byteOffset, byte replacement)
        where T : unmanaged
    {
        if (byteOffset < 0 || byteOffset >= sizeof(T))
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        ((byte*)&value)[byteOffset] = replacement;
        return value;
    }

    private static unsafe ulong[] CopyBytesAsUlongs(void* source, int byteCount)
    {
        if ((byteCount & 7) != 0)
        {
            throw new InvalidOperationException("Test byte snapshot must be ulong aligned.");
        }

        int count = byteCount / 8;
        ulong[] result = new ulong[count];
        ulong* values = (ulong*)source;
        for (int index = 0; index < count; index++)
        {
            result[index] = values[index];
        }

        return result;
    }

    private static unsafe void FillBytes(void* destination, int byteCount, byte value)
    {
        byte* bytes = (byte*)destination;
        for (int index = 0; index < byteCount; index++)
        {
            bytes[index] = value;
        }
    }

    private static unsafe bool EqualBytes(ulong[] expected, void* actual, int byteCount)
    {
        if (expected.Length * 8 != byteCount)
        {
            return false;
        }

        ulong* values = (ulong*)actual;
        for (int index = 0; index < expected.Length; index++)
        {
            if (values[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool EvaluatedEfiClosure(string repositoryRoot, string projectPath, string workloadProfile, IReadOnlyList<string> requiredPaths, IReadOnlyList<string> forbiddenPaths)
    {
        ProcessStartInfo start = DotnetStart(repositoryRoot);
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add("-getItem:Compile,ProjectReference,Reference");
        start.ArgumentList.Add("-p:Configuration=Release");
        start.ArgumentList.Add("-p:OSWorkloadProfile=" + workloadProfile);
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        ProcessResult result = RunBounded(start, 60_000);
        if (result.TimedOut || result.ExitCode != 0)
        {
            Console.WriteLine("M01_012_EFI_EVAL_DIAGNOSTIC profile=" + workloadProfile + " " + NormalizeOutput(result.Output));
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
            JsonElement items = document.RootElement.GetProperty("Items");
            JsonElement compile = items.GetProperty("Compile");
            JsonElement projectReferences = items.GetProperty("ProjectReference");
            JsonElement references = items.GetProperty("Reference");
            if (compile.ValueKind != JsonValueKind.Array || compile.GetArrayLength() == 0 || projectReferences.ValueKind != JsonValueKind.Array || projectReferences.GetArrayLength() != 0 || references.ValueKind != JsonValueKind.Array || references.GetArrayLength() != 0)
            {
                Console.WriteLine("M01_012_EFI_EVAL_DIAGNOSTIC profile=" + workloadProfile + " malformed_items");
                return false;
            }

            bool efiEntry = false;
            bool minimalRuntime = false;
            HashSet<string> required = new(requiredPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            HashSet<string> foundRequired = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement item in compile.EnumerateArray())
            {
                string identity = item.GetProperty("Identity").GetString() ?? "";
                string fullPath = item.GetProperty("FullPath").GetString() ?? "";
                if (identity.Length == 0 || fullPath.Length == 0 || !Path.IsPathFullyQualified(fullPath))
                {
                    Console.WriteLine("M01_012_EFI_EVAL_DIAGNOSTIC profile=" + workloadProfile + " malformed_compile_item");
                    return false;
                }

                string canonicalPath = Path.GetFullPath(fullPath);
                efiEntry |= fullPath.EndsWith(Path.Combine("Boot", "EfiEntry.cs"), StringComparison.OrdinalIgnoreCase);
                minimalRuntime |= fullPath.EndsWith(Path.Combine("Runtime", "MinimalRuntime.cs"), StringComparison.OrdinalIgnoreCase);
                if (required.Contains(canonicalPath))
                    foundRequired.Add(canonicalPath);
                foreach (string forbiddenPath in forbiddenPaths)
                {
                    if (canonicalPath.Equals(Path.GetFullPath(forbiddenPath), StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("M01_012_EFI_EVAL_DIAGNOSTIC profile=" + workloadProfile + " forbidden=" + Path.GetFileName(forbiddenPath));
                        return false;
                    }
                }
            }

            if (!efiEntry || !minimalRuntime || foundRequired.Count != required.Count)
            {
                Console.WriteLine("M01_012_EFI_EVAL_DIAGNOSTIC profile=" + workloadProfile + " anchors=" + efiEntry + "," + minimalRuntime);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("M01_012_EFI_EVAL_DIAGNOSTIC profile=" + workloadProfile + " parse=" + ex.GetType().Name);
            return false;
        }
    }

    private static NoStdLibBuildResult BuildNoStdLibClosure(string repositoryRoot, string addressPath, string protectionPath, string cachePath, string entryPath, string planPath)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "clros-m01-012-" + Guid.NewGuid().ToString("N"));
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
                    public struct Int32 { }
                    public struct UInt32 { }
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
                """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(Path.Combine(workspace, "Probe.cs"), """
                namespace CLROS.M01
                {
                    public static unsafe class Probe
                    {
                        public static ulong Run()
                        {
                            OS.Kernel.Memory.X64PageTableEntryStatus patStatus =
                                OS.Kernel.Memory.X64PatMappingToken.TryCreate(
                                    0x0006050100070406UL,
                                    0,
                                    3,
                                    4,
                                    out OS.Kernel.Memory.X64PatMappingToken pat);
                            OS.Boot.AddressValueStatus startStatus =
                                OS.Boot.PhysicalAddress
                                    .TryCreateFromHardwareRegisterBoundary(
                                        0x1000,
                                        out OS.Boot.PhysicalAddress start);
                            OS.Boot.AddressValueStatus endStatus =
                                OS.Boot.PhysicalAddress
                                    .TryCreateFromHardwareRegisterBoundary(
                                        0x2000,
                                        out OS.Boot.PhysicalAddress end);
                            if (patStatus !=
                                    OS.Kernel.Memory
                                        .X64PageTableEntryStatus.Success ||
                                startStatus !=
                                    OS.Boot.AddressValueStatus.Success ||
                                endStatus !=
                                    OS.Boot.AddressValueStatus.Success)
                            {
                                return 0;
                            }

                            OS.Kernel.Memory.X64PageTableEncodingPreconditions
                                context =
                                    OS.Kernel.Memory
                                        .X64PageTableEncodingPreconditions
                                        .CreateObserved(
                                            48,
                                            true,
                                            true,
                                            false,
                                            true,
                                            pat);
                            OS.Kernel.Boot
                                .X64BootIdentityMappingRequirement* source =
                                    stackalloc OS.Kernel.Boot
                                        .X64BootIdentityMappingRequirement[1];
                            OS.Kernel.Boot.X64BootIdentityMappingRange*
                                destination =
                                    stackalloc OS.Kernel.Boot
                                        .X64BootIdentityMappingRange[1];
                            source[0] =
                                new OS.Kernel.Boot
                                    .X64BootIdentityMappingRequirement(
                                        OS.Kernel.Boot
                                            .X64BootIdentityMappingPurpose
                                            .BootStackGuard,
                                        start,
                                        end,
                                        1,
                                        4096,
                                        OS.Kernel.Memory
                                            .PageProtection.None,
                                        OS.Kernel.Memory
                                            .PageCachePolicy.Uninitialized);
                            OS.Kernel.Boot
                                .X64BootIdentityMappingPlanResult result =
                                    OS.Kernel.Boot
                                        .X64BootIdentityMappingPlan.Create(
                                            source,
                                            1,
                                            1,
                                            destination,
                                            1,
                                            4,
                                            context);
                            return result.RequiredPurposeMask;
                        }
                    }
                }
                """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            string project = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.X64BootIdentityMappingPlanCompileClosure</AssemblyName>
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
                    <Compile Include="__PROTECTION__" Link="Memory\PageProtection.cs" />
                    <Compile Include="__CACHE__" Link="Memory\PageCachePolicy.cs" />
                    <Compile Include="__ENTRY__/X64PageTableEntry.X64PageTableLevel.cs" Link="Memory/X64PageTableEntry.X64PageTableLevel.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.X64PageTableEntryKind.cs" Link="Memory/X64PageTableEntry.X64PageTableEntryKind.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.X64HardwareStatePolicy.cs" Link="Memory/X64PageTableEntry.X64HardwareStatePolicy.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.X64PageTableEntryStatus.cs" Link="Memory/X64PageTableEntry.X64PageTableEntryStatus.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.X64PatMappingToken.cs" Link="Memory/X64PageTableEntry.X64PatMappingToken.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.X64PageTableEncodingPreconditions.cs" Link="Memory/X64PageTableEntry.X64PageTableEncodingPreconditions.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.X64PageTableEntryRequest.cs" Link="Memory/X64PageTableEntry.X64PageTableEntryRequest.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.X64PageTableEntryResult.cs" Link="Memory/X64PageTableEntry.X64PageTableEntryResult.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.X64PageTableEntryBatchResult.cs" Link="Memory/X64PageTableEntry.X64PageTableEntryBatchResult.cs" />
                <Compile Include="__ENTRY__/X64PageTableEntry.cs" Link="Memory/X64PageTableEntry.cs" />
                    <Compile Include="__PLAN__/X64BootIdentityMappingPlan.X64BootIdentityMappingPurpose.cs" Link="Boot/X64BootIdentityMappingPlan.X64BootIdentityMappingPurpose.cs" />
                <Compile Include="__PLAN__/X64BootIdentityMappingPlan.X64BootIdentityMappingStatus.cs" Link="Boot/X64BootIdentityMappingPlan.X64BootIdentityMappingStatus.cs" />
                <Compile Include="__PLAN__/X64BootIdentityMappingPlan.X64BootIdentityMappingRequirement.cs" Link="Boot/X64BootIdentityMappingPlan.X64BootIdentityMappingRequirement.cs" />
                <Compile Include="__PLAN__/X64BootIdentityMappingPlan.X64BootIdentityMappingRange.cs" Link="Boot/X64BootIdentityMappingPlan.X64BootIdentityMappingRange.cs" />
                <Compile Include="__PLAN__/X64BootIdentityMappingPlan.X64BootIdentityMappingPlanResult.cs" Link="Boot/X64BootIdentityMappingPlan.X64BootIdentityMappingPlanResult.cs" />
                <Compile Include="__PLAN__/X64BootIdentityMappingPlan.cs" Link="Boot/X64BootIdentityMappingPlan.cs" />
                <Compile Include="__PLAN__/X64BootIdentityMappingPlan.RequirementView.cs" Link="Boot/X64BootIdentityMappingPlan.RequirementView.cs" />
                <Compile Include="__PLAN__/X64BootIdentityMappingPlan.MergedRange.cs" Link="Boot/X64BootIdentityMappingPlan.MergedRange.cs" />
                <Compile Include="__PLAN__/X64BootIdentityMappingPlan.PlannerState.cs" Link="Boot/X64BootIdentityMappingPlan.PlannerState.cs" />
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
                """.Replace("__ADDRESS__", EscapeXml(Path.GetDirectoryName(addressPath)!), StringComparison.Ordinal).Replace("__PROTECTION__", EscapeXml(protectionPath), StringComparison.Ordinal).Replace("__CACHE__", EscapeXml(cachePath), StringComparison.Ordinal).Replace("__ENTRY__", EscapeXml(Path.GetDirectoryName(entryPath)!), StringComparison.Ordinal).Replace("__PLAN__", EscapeXml(Path.GetDirectoryName(planPath)!), StringComparison.Ordinal);
            string projectPath = Path.Combine(workspace, "M01.012.NoStdLib.csproj");
            File.WriteAllText(projectPath, project, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            ProcessStartInfo start = DotnetStart(workspace);
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
            ProcessResult process = RunBounded(start, 90_000);
            string assemblyPath = Path.Combine(workspace, "out", "CLROS.M01.X64BootIdentityMappingPlanCompileClosure.dll");
            byte[] assembly = File.Exists(assemblyPath) ? File.ReadAllBytes(assemblyPath) : [];
            return new NoStdLibBuildResult(process.ExitCode, process.TimedOut, process.Output, assembly);
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static ProcessStartInfo DotnetStart(string workingDirectory)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";
        return start;
    }

    private static ProcessResult RunBounded(ProcessStartInfo start, int timeoutMilliseconds)
    {
        using Process process = new()
        {
            StartInfo = start
        };
        if (!process.Start())
        {
            return new ProcessResult(-1, timedOut: false, "", "Process did not start.");
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(timeoutMilliseconds);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Timeout remains the primary result.
                }
            }
        }

        bool stopped = exited || process.WaitForExit(5_000);
        bool streamsCompleted = Task.WaitAll([stdout, stderr], millisecondsTimeout: 5_000);
        string standardOutput = streamsCompleted ? stdout.GetAwaiter().GetResult() : "";
        string standardError = streamsCompleted ? stderr.GetAwaiter().GetResult() : "Output streams did not close within the bounded timeout.";
        return new ProcessResult(stopped ? process.ExitCode : -1, timedOut: !exited || !stopped || !streamsCompleted, standardOutput, standardError);
    }

    private static bool HasZeroAssemblyReferences(byte[] assembly)
    {
        if (assembly.Length == 0)
        {
            return false;
        }

        using MemoryStream stream = new(assembly, writable: false);
        using PEReader peReader = new(stream);
        return peReader.HasMetadata && peReader.GetMetadataReader().AssemblyReferences.Count == 0;
    }

    private static string RemoveComments(string source)
    {
        return Regex.Replace(source, @"//.*?$|/\*.*?\*/", "", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    private static bool HasDirectorySegment(string path, string segment)
    {
        string marker = Path.DirectorySeparatorChar + segment + Path.DirectorySeparatorChar;
        return path.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExecutableSource(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string NormalizeOutput(string output)
    {
        string normalized = output.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 1200 ? normalized : normalized[..1200];
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string fullWorkspace = Path.GetFullPath(workspace);
        string expectedPrefix = Path.TrimEndingDirectorySeparator(tempRoot) + Path.DirectorySeparatorChar;
        string leaf = Path.GetFileName(fullWorkspace);
        if (!fullWorkspace.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) || !leaf.StartsWith("clros-m01-012-", StringComparison.Ordinal) || leaf.Length <= "clros-m01-012-".Length)
        {
            throw new InvalidOperationException("Refusing to delete an unexpected M01-012 workspace.");
        }

        if (Directory.Exists(fullWorkspace))
        {
            Directory.Delete(fullWorkspace, recursive: true);
        }
    }
}
