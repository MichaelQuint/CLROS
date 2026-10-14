using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;

internal static unsafe class X64PageMapTests
{
    private const string TestName = "m01.014.01.map";

#if !M01_014_MAP_FOCUSED && !M01_014_INVLPG_FOCUSED && !M01_014_PROTECTION_FOCUSED
    [HostFixture(HostLane.Native)]
#endif
    internal static void RunAllM01014()
    {
        Console.WriteLine("TEST_START m01.014");
        int failures = 0;
        failures += RunM01014Map();
        failures += X64InvlpgStubPlanTests.RunM01014Invlpg();
        failures += X64ProtectionProbeStubPlanTests
            .RunM01014Protection();
        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL m01.014 failures=" + failures);
            throw new InvalidOperationException(
                "m01.014 aggregate failures=" + failures);
        }

        Console.WriteLine("CLROS_M01_014_READY");
        Console.WriteLine("TEST_PASS m01.014");
    }

    internal static int RunM01014Map()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase("append only enums pods and defaults",
            TestTypeContracts, ref failures);
        RunCase("target-only invalidation range materializes",
            TestInvalidationPlan, ref failures);
        RunCase("map unmap and remap change one leaf",
            TestMapUnmapRemap, ref failures);
        RunCase("rw nx to ro x preserves physical and cache",
            TestProtectionTransition, ref failures);
        RunCase("duplicate and unchanged operations fail atomically",
            TestDuplicateAndUnchangedFailures, ref failures);
        RunCase("page zero alignment overflow and wx fail",
            TestAddressAndProtectionFailures, ref failures);
        RunCase("storage count capacity alias and record checks",
            TestStorageFailures, ref failures);
        RunCase("missing table large page and pointer failures",
            TestWalkFailures, ref failures);
        RunCase("essential mapping mutation is rejected",
            TestEssentialMappingFailures, ref failures);
        RunCase("source and nostdlib closure remain isolated",
            TestSourceAndNoStdLibBoundary, ref failures);

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
        HostCheck.Run(TestName + " " + name, () =>
        {
            try
            {
                if (test())
                    return true;
                Console.WriteLine(
                    "M01_014_MAP_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "M01_014_MAP_CASE_FAIL "
                    + Normalize(name)
                    + " "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestTypeContracts()
    {
        string[] operationNames =
        [
            "Uninitialized",
            "Map",
            "Unmap",
            "Protect"
        ];
        string[] mapStatusNames =
        [
            "Uninitialized",
            "Success",
            "InvalidRequestState",
            "InvalidOperation",
            "InvalidRootTable",
            "InvalidVirtualAddress",
            "PageZeroRejected",
            "MisalignedVirtualAddress",
            "NonCanonicalVirtualAddress",
            "InvalidPhysicalAddress",
            "InvalidProtection",
            "InvalidCachePolicy",
            "InvalidHardwareStatePolicy",
            "InvalidPageImageCount",
            "TooManyPageImages",
            "PageImageCapacityTooSmall",
            "PageImageCapacityExceedsLimit",
            "MissingPageImages",
            "MisalignedPageImages",
            "PageImageSpanOverflow",
            "InvalidEntryCount",
            "EntryCountMismatch",
            "EntryCapacityTooSmall",
            "EntryCapacityExceedsLimit",
            "MissingEntries",
            "MisalignedEntries",
            "EntrySpanOverflow",
            "StorageOverlap",
            "InvalidPageImageRecord",
            "PageImageOrderInvalid",
            "PageImagePhysicalOverlap",
            "RootPageNotFound",
            "MissingTable",
            "LargePageCollision",
            "TablePointerTargetMissing",
            "MalformedEntry",
            "TargetAlreadyMapped",
            "TargetNotMapped",
            "EssentialMappingMutationRejected",
            "InvalidEssentialRangeCount",
            "TooManyEssentialRanges",
            "EssentialRangeCapacityTooSmall",
            "EssentialRangeCapacityExceedsLimit",
            "MissingEssentialRanges",
            "MisalignedEssentialRanges",
            "EssentialRangeSpanOverflow",
            "EssentialRangeStorageOverlap",
            "InvalidEssentialRange",
            "EssentialRangeOrderInvalid",
            "EntryEncodingFailed",
            "ProtectionRewriteFailed",
            "ProtectionUnchanged",
            "InvalidationPlanFailed",
            "ContractLayoutMismatch"
        ];
        string[] invalidationStatusNames =
        [
            "Uninitialized",
            "Success",
            "InvalidRange",
            "InvalidVirtualStart",
            "PageZeroRejected",
            "MisalignedVirtualStart",
            "InvalidByteLength",
            "ByteLengthNotPageAligned",
            "RangeOverflow",
            "NonCanonicalRange",
            "TooManyPages",
            "InvalidPlanState",
            "DestinationCapacityTooSmall",
            "CapacityExceedsLimit",
            "MissingDestination",
            "MisalignedDestination",
            "DestinationSpanOverflow",
            "InvalidRecord"
        ];

        return HasExactSequentialByteEnum<X64PageMapOperation>(
                operationNames)
            && HasExactSequentialByteEnum<X64PageMapStatus>(
                mapStatusNames)
            && HasExactSequentialByteEnum<X64TlbInvalidationStatus>(
                invalidationStatusNames)
            && (byte)X64PageMapOperation.Uninitialized == 0
            && (byte)X64PageMapOperation.Map == 1
            && (byte)X64PageMapOperation.Unmap == 2
            && (byte)X64PageMapOperation.Protect == 3
            && (byte)X64PageMapStatus.Success == 1
            && (byte)X64PageMapStatus.ContractLayoutMismatch == 53
            && (byte)X64TlbInvalidationStatus.Success == 1
            && (byte)X64TlbInvalidationStatus.InvalidRecord == 17
            && (ulong)Unsafe.SizeOf<X64PageTableImagePage>()
                == X64PageTableImagePage.StructureSize
            && (ulong)Unsafe.SizeOf<X64EssentialMappingRange>()
                == X64EssentialMappingRange.StructureSize
            && (ulong)Unsafe.SizeOf<X64PageMapRequest>()
                == X64PageMapRequest.StructureSize
            && (ulong)Unsafe.SizeOf<X64PageMapResult>()
                == X64PageMapResult.StructureSize
            && (ulong)Unsafe.SizeOf<X64TlbInvalidationRange>()
                == X64TlbInvalidationRange.StructureSize
            && (ulong)Unsafe.SizeOf<X64TlbInvalidationRecord>()
                == X64TlbInvalidationRecord.StructureSize
            && (ulong)Unsafe.SizeOf<X64TlbInvalidationPlanResult>()
                == X64TlbInvalidationPlanResult.StructureSize
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64PageTableImagePage>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64EssentialMappingRange>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64PageMapRequest>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64PageMapResult>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64TlbInvalidationRange>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64TlbInvalidationRecord>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64TlbInvalidationPlanResult>()
            && !default(X64PageMapRequest).IsInitialized
            && !default(X64PageMapResult).Succeeded
            && !default(X64TlbInvalidationRange).IsInitialized
            && !default(X64TlbInvalidationPlanResult).Succeeded
            && X64PageMap.PageSize == 4096
            && X64PageMap.MaximumPageImages == 64
            && X64PageMap.MaximumEntryCount == 32768
            && !X64PageMap.PerformsLiveMapping
            && !X64PageMap.PerformsLiveInvalidation
            && !X64PageMap.AllocatesPageTables
            && !X64PageMap.SplitsLargePages
            && !X64TlbInvalidationPlan.PerformsLiveInvalidation
            && !X64TlbInvalidationPlan.ReloadsCr3;
    }

    private static bool TestInvalidationPlan()
    {
        VirtualAddress start = M01014TestData.Virtual(0x0040_0000);
        X64TlbInvalidationStatus status =
            X64TlbInvalidationPlan.TryCreate(
                start,
                2 * X64TlbInvalidationPlan.PageSize,
                out X64TlbInvalidationRange range);
        X64TlbInvalidationRecord* records =
            stackalloc X64TlbInvalidationRecord[2];
        X64TlbInvalidationPlanResult result =
            X64TlbInvalidationPlan.Materialize(
                range,
                records,
                2);
        if (status != X64TlbInvalidationStatus.Success ||
            !result.Succeeded ||
            result.RequiredRecordCount != 2 ||
            result.WrittenRecordCount != 2 ||
            records[0].Validate()
                != X64TlbInvalidationStatus.Success ||
            records[1].Validate()
                != X64TlbInvalidationStatus.Success ||
            records[0].Ordinal != 0 ||
            records[1].Ordinal != 1 ||
            M01014TestData.Raw(records[0].Target)
                != 0x0040_0000 ||
            M01014TestData.Raw(records[1].Target)
                != 0x0040_1000)
        {
            return false;
        }

        X64TlbInvalidationRecord sentinel = records[0];
        X64TlbInvalidationPlanResult shortCapacity =
            X64TlbInvalidationPlan.Materialize(
                range,
                records,
                1);
        X64TlbInvalidationPlanResult missing =
            X64TlbInvalidationPlan.Materialize(
                range,
                null,
                2);
        X64TlbInvalidationStatus unaligned =
            X64TlbInvalidationPlan.TryCreate(
                M01014TestData.VirtualUnaligned(0x0040_0001),
                4096,
                out _);
        X64TlbInvalidationStatus zeroLength =
            X64TlbInvalidationPlan.TryCreate(
                start,
                0,
                out _);
        X64TlbInvalidationStatus partialPage =
            X64TlbInvalidationPlan.TryCreate(
                start,
                4095,
                out _);
        X64TlbInvalidationStatus high =
            X64TlbInvalidationPlan.TryCreate(
                M01014TestData.Virtual(
                    X64TlbInvalidationPlan
                        .LowCanonicalEndExclusive),
                4096,
                out _);
        X64TlbInvalidationStatus crossingCanonicalBoundary =
            X64TlbInvalidationPlan.TryCreate(
                M01014TestData.Virtual(
                    X64TlbInvalidationPlan
                        .LowCanonicalEndExclusive
                    - X64TlbInvalidationPlan.PageSize),
                2 * X64TlbInvalidationPlan.PageSize,
                out _);
        X64TlbInvalidationStatus maximum =
            X64TlbInvalidationPlan.TryCreate(
                start,
                X64TlbInvalidationPlan.MaximumPageCount
                    * X64TlbInvalidationPlan.PageSize,
                out _);
        X64TlbInvalidationStatus tooMany =
            X64TlbInvalidationPlan.TryCreate(
                start,
                (X64TlbInvalidationPlan.MaximumPageCount + 1)
                    * X64TlbInvalidationPlan.PageSize,
                out _);
        X64TlbInvalidationPlanResult defaultRange =
            X64TlbInvalidationPlan.Materialize(
                default,
                records,
                2);
        X64TlbInvalidationPlanResult oversizedCapacity =
            X64TlbInvalidationPlan.Materialize(
                range,
                records,
                X64TlbInvalidationPlan.MaximumPageCount + 1);
        X64TlbInvalidationPlanResult misalignedDestination =
            X64TlbInvalidationPlan.Materialize(
                range,
                (X64TlbInvalidationRecord*)((byte*)records + 1),
                2);
        X64TlbInvalidationPlanResult overflowingDestination =
            X64TlbInvalidationPlan.Materialize(
                range,
                (X64TlbInvalidationRecord*)
                    0xffff_ffff_ffff_fff8UL,
                2);

        return !shortCapacity.Succeeded
            && shortCapacity.Status
                == X64TlbInvalidationStatus
                    .DestinationCapacityTooSmall
            && records[0].Target.Equals(sentinel.Target)
            && !missing.Succeeded
            && missing.Status
                == X64TlbInvalidationStatus.MissingDestination
            && unaligned
                == X64TlbInvalidationStatus
                    .MisalignedVirtualStart
            && zeroLength
                == X64TlbInvalidationStatus.InvalidByteLength
            && partialPage
                == X64TlbInvalidationStatus
                    .ByteLengthNotPageAligned
            && high
                == X64TlbInvalidationStatus.NonCanonicalRange
            && crossingCanonicalBoundary
                == X64TlbInvalidationStatus.NonCanonicalRange
            && maximum == X64TlbInvalidationStatus.Success
            && tooMany == X64TlbInvalidationStatus.TooManyPages
            && default(X64TlbInvalidationRecord).Validate()
                == X64TlbInvalidationStatus.InvalidRecord
            && defaultRange.Status
                == X64TlbInvalidationStatus.InvalidPlanState
            && oversizedCapacity.Status
                == X64TlbInvalidationStatus.CapacityExceedsLimit
            && misalignedDestination.Status
                == X64TlbInvalidationStatus.MisalignedDestination
            && overflowingDestination.Status
                == X64TlbInvalidationStatus.DestinationSpanOverflow
            && records[0].Target.Equals(sentinel.Target);
    }

    private static bool TestMapUnmapRemap()
    {
        using M01014PageTableFixture fixture = new();
        ulong[] baseline = (ulong[])fixture.Entries.Clone();
        X64PageMapResult map = fixture.Apply(
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context));
        if (!map.Succeeded ||
            map.Request.Operation != X64PageMapOperation.Map ||
            map.ChangedEntryCount != 1 ||
            CountDifferences(baseline, fixture.Entries) != 1 ||
            fixture.Entries[map.TargetEntryIndex]
                != map.ReplacementEntry.RawValue ||
            M01014TestData.Raw(
                map.PageImagePhysicalStart)
                != 0x0010_0000 ||
            M01014TestData.Raw(
                map.PageImagePhysicalEndExclusive)
                != 0x0010_4000 ||
            map.InvalidationRange.PageCount != 1 ||
            !map.InvalidationRange.VirtualStart.Equals(
                fixture.Target))
        {
            return false;
        }

        ulong mappedRaw = map.ReplacementEntry.RawValue;
        ulong[] mappedSnapshot = (ulong[])fixture.Entries.Clone();
        X64PageMapResult unmap = fixture.Apply(
            X64PageMapRequest.CreateUnmap(
                fixture.Root,
                fixture.Target,
                fixture.Context));
        if (!unmap.Succeeded ||
            unmap.PreviousEntry.RawValue != mappedRaw ||
            unmap.ReplacementEntry.RawValue != 0 ||
            CountDifferences(mappedSnapshot, fixture.Entries) != 1)
        {
            return false;
        }

        X64PageMapResult remap = fixture.Apply(
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x00a0_0000),
                PageProtection.Read,
                PageCachePolicy.Uncached,
                fixture.Context));
        X64PageTableEntryResult decoded =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                remap.ReplacementEntry.RawValue,
                fixture.Context);
        return remap.Succeeded
            && decoded.Succeeded
            && M01014TestData.Raw(decoded.PhysicalAddress)
                == 0x00a0_0000
            && decoded.Protection == PageProtection.Read
            && decoded.CachePolicy == PageCachePolicy.Uncached;
    }

    private static bool TestProtectionTransition()
    {
        using M01014PageTableFixture fixture = new();
        X64PageMapResult map = fixture.Apply(
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context));
        if (!map.Succeeded)
            return false;

        ulong[] before = (ulong[])fixture.Entries.Clone();
        X64PageMapResult protect = fixture.Apply(
            X64PageMapRequest.CreateProtect(
                fixture.Root,
                fixture.Target,
                M01014TestData.ReadExecute,
                X64HardwareStatePolicy.Preserve,
                fixture.Context));
        X64PageTableEntryResult oldEntry =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                protect.PreviousEntry.RawValue,
                fixture.Context);
        X64PageTableEntryResult newEntry =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                protect.ReplacementEntry.RawValue,
                fixture.Context);
        return protect.Succeeded
            && CountDifferences(before, fixture.Entries) == 1
            && oldEntry.Succeeded
            && newEntry.Succeeded
            && oldEntry.Protection == M01014TestData.ReadWrite
            && newEntry.Protection == M01014TestData.ReadExecute
            && oldEntry.PhysicalAddress.Equals(
                newEntry.PhysicalAddress)
            && oldEntry.CachePolicy == PageCachePolicy.WriteBack
            && newEntry.CachePolicy == PageCachePolicy.WriteBack
            && (protect.ReplacementEntry.RawValue & (1UL << 1)) == 0
            && (protect.ReplacementEntry.RawValue & (1UL << 63)) == 0;
    }

    private static bool TestDuplicateAndUnchangedFailures()
    {
        using M01014PageTableFixture fixture = new();
        X64PageMapRequest mapRequest =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context);
        if (!fixture.Apply(mapRequest).Succeeded)
            return false;

        ulong[] mapped = (ulong[])fixture.Entries.Clone();
        X64PageMapResult duplicate = fixture.Apply(mapRequest);
        X64PageMapResult unchanged = fixture.Apply(
            X64PageMapRequest.CreateProtect(
                fixture.Root,
                fixture.Target,
                M01014TestData.ReadWrite,
                X64HardwareStatePolicy.Preserve,
                fixture.Context));
        if (duplicate.Succeeded ||
            duplicate.Status != X64PageMapStatus.TargetAlreadyMapped ||
            unchanged.Succeeded ||
            unchanged.Status != X64PageMapStatus.ProtectionUnchanged ||
            CountDifferences(mapped, fixture.Entries) != 0)
        {
            return false;
        }

        if (!fixture.Apply(
                X64PageMapRequest.CreateUnmap(
                    fixture.Root,
                    fixture.Target,
                    fixture.Context)).Succeeded)
        {
            return false;
        }

        ulong[] unmapped = (ulong[])fixture.Entries.Clone();
        X64PageMapResult doubleUnmap = fixture.Apply(
            X64PageMapRequest.CreateUnmap(
                fixture.Root,
                fixture.Target,
                fixture.Context));
        return !doubleUnmap.Succeeded
            && doubleUnmap.Status == X64PageMapStatus.TargetNotMapped
            && CountDifferences(unmapped, fixture.Entries) == 0;
    }

    private static bool TestAddressAndProtectionFailures()
    {
        using M01014PageTableFixture fixture = new();
        X64PageMapRequest zero =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                default,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context);
        X64PageMapRequest unaligned =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                M01014TestData.VirtualUnaligned(0x0040_0001),
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context);
        X64PageMapRequest high =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                M01014TestData.Virtual(
                    X64PageMap.LowCanonicalEndExclusive),
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context);
        PageProtection wx = (PageProtection)(
            (byte)PageProtection.Read |
            (byte)PageProtection.Write |
            (byte)PageProtection.Execute);
        X64PageMapRequest writableExecutable =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                wx,
                PageCachePolicy.WriteBack,
                fixture.Context);
        X64PageMapRequest invalidRoot =
            X64PageMapRequest.CreateMap(
                default,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context);
        X64PageMapRequest invalidPhysical =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                default,
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context);
        X64PageMapRequest unalignedPhysical =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.PhysicalUnaligned(0x0090_0001),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context);
        X64PageMapRequest unknownProtection =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                (PageProtection)0xff,
                PageCachePolicy.WriteBack,
                fixture.Context);
        X64PageMapRequest unknownCache =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                (PageCachePolicy)0xff,
                fixture.Context);
        X64PageMapRequest unknownHardwarePolicy =
            X64PageMapRequest.CreateProtect(
                fixture.Root,
                fixture.Target,
                M01014TestData.ReadExecute,
                (X64HardwareStatePolicy)0xff,
                fixture.Context);

        return zero.Validate() == X64PageMapStatus.PageZeroRejected
            && unaligned.Validate()
                == X64PageMapStatus.MisalignedVirtualAddress
            && high.Validate()
                == X64PageMapStatus.NonCanonicalVirtualAddress
            && writableExecutable.Validate()
                == X64PageMapStatus.InvalidProtection
            && invalidRoot.Validate()
                == X64PageMapStatus.InvalidRootTable
            && invalidPhysical.Validate()
                == X64PageMapStatus.InvalidPhysicalAddress
            && unalignedPhysical.Validate()
                == X64PageMapStatus.InvalidPhysicalAddress
            && unknownProtection.Validate()
                == X64PageMapStatus.InvalidProtection
            && unknownCache.Validate()
                == X64PageMapStatus.InvalidCachePolicy
            && unknownHardwarePolicy.Validate()
                == X64PageMapStatus.InvalidHardwareStatePolicy
            && default(X64PageMapRequest).Validate()
                == X64PageMapStatus.InvalidRequestState
            && CountNonZero(fixture.Entries) == 3;
    }

    private static bool TestStorageFailures()
    {
        using M01014PageTableFixture fixture = new();
        X64PageMapRequest request =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context);
        ulong[] baseline = (ulong[])fixture.Entries.Clone();
        X64PageTableImagePage[] malformedPages =
            (X64PageTableImagePage[])fixture.Pages.Clone();
        malformedPages[1] = default;
        X64PageTableImagePage[] outOfOrderPages =
            (X64PageTableImagePage[])fixture.Pages.Clone();
        X64PageTableImagePage.TryCreate(
            M01014TestData.Physical(0x000f_f000),
            512,
            out outOfOrderPages[1]);
        X64EssentialMappingRange.TryCreate(
            fixture.Target,
            1,
            out X64EssentialMappingRange essential);
        X64EssentialMappingRange[] essentialRanges = [essential];
        X64EssentialMappingRange[] invalidEssential = [default];
        X64EssentialMappingRange.TryCreate(
            M01014TestData.Virtual(
                M01014TestData.Raw(fixture.Target) + 0x1000),
            1,
            out X64EssentialMappingRange laterEssential);
        X64EssentialMappingRange[] outOfOrderEssential =
        [
            laterEssential,
            essential
        ];

        fixed (X64PageTableImagePage* pages = fixture.Pages)
        fixed (X64PageTableImagePage* malformed = malformedPages)
        fixed (X64PageTableImagePage* outOfOrder = outOfOrderPages)
        fixed (ulong* entries = fixture.Entries)
        fixed (X64EssentialMappingRange* essentialPointer =
            essentialRanges)
        fixed (X64EssentialMappingRange* invalidEssentialPointer =
            invalidEssential)
        fixed (X64EssentialMappingRange* outOfOrderEssentialPointer =
            outOfOrderEssential)
        {
            X64PageMapResult missingPages = X64PageMap.Apply(
                request,
                null,
                4,
                4,
                entries,
                2048,
                2048,
                null,
                0,
                0);
            X64PageMapResult shortPageCapacity = X64PageMap.Apply(
                request,
                pages,
                4,
                3,
                entries,
                2048,
                2048,
                null,
                0,
                0);
            X64PageMapResult mismatchedEntries = X64PageMap.Apply(
                request,
                pages,
                4,
                4,
                entries,
                2047,
                2048,
                null,
                0,
                0);
            X64PageMapResult shortEntries = X64PageMap.Apply(
                request,
                pages,
                4,
                4,
                entries,
                2048,
                2047,
                null,
                0,
                0);
            X64PageMapResult missingEssential = X64PageMap.Apply(
                request,
                pages,
                4,
                4,
                entries,
                2048,
                2048,
                null,
                1,
                1);
            X64PageMapResult missingEntries = X64PageMap.Apply(
                request,
                pages,
                4,
                4,
                null,
                2048,
                2048,
                null,
                0,
                0);
            X64PageMapResult oversizedPageCapacity =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    X64PageMap.MaximumPageImages + 1,
                    entries,
                    2048,
                    2048,
                    null,
                    0,
                    0);
            X64PageMapResult oversizedEntryCapacity =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    X64PageMap.MaximumEntryCount + 1,
                    null,
                    0,
                    0);
            X64PageMapResult misalignedPages =
                X64PageMap.Apply(
                    request,
                    (X64PageTableImagePage*)((byte*)pages + 1),
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    null,
                    0,
                    0);
            X64PageMapResult overflowingPageSpan =
                X64PageMap.Apply(
                    request,
                    (X64PageTableImagePage*)
                        0xffff_ffff_ffff_fff8UL,
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    null,
                    0,
                    0);
            X64PageMapResult misalignedEntries =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    (ulong*)((byte*)entries + 1),
                    2048,
                    2048,
                    null,
                    0,
                    0);
            X64PageMapResult overflowingEntrySpan =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    (ulong*)0xffff_ffff_ffff_fff8UL,
                    2048,
                    2048,
                    null,
                    0,
                    0);
            X64PageMapResult aliasedStorage =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    (ulong*)pages,
                    2048,
                    2048,
                    null,
                    0,
                    0);
            X64PageMapResult malformedRecord =
                X64PageMap.Apply(
                    request,
                    malformed,
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    null,
                    0,
                    0);
            X64PageMapResult pageOrder =
                X64PageMap.Apply(
                    request,
                    outOfOrder,
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    null,
                    0,
                    0);
            X64PageMapResult essentialMisaligned =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    (X64EssentialMappingRange*)
                        ((byte*)essentialPointer + 1),
                    1,
                    1);
            X64PageMapResult essentialOverflow =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    (X64EssentialMappingRange*)
                        0xffff_ffff_ffff_fff8UL,
                    1,
                    1);
            X64PageMapResult essentialAlias =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    (X64EssentialMappingRange*)entries,
                    1,
                    1);
            X64PageMapResult invalidEssentialRecord =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    invalidEssentialPointer,
                    1,
                    1);
            X64PageMapResult essentialOrder =
                X64PageMap.Apply(
                    request,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    2048,
                    outOfOrderEssentialPointer,
                    2,
                    2);

            return missingPages.Status
                    == X64PageMapStatus.MissingPageImages
                && shortPageCapacity.Status
                    == X64PageMapStatus.PageImageCapacityTooSmall
                && mismatchedEntries.Status
                    == X64PageMapStatus.EntryCountMismatch
                && shortEntries.Status
                    == X64PageMapStatus.EntryCapacityTooSmall
                && missingEssential.Status
                    == X64PageMapStatus.MissingEssentialRanges
                && missingEntries.Status
                    == X64PageMapStatus.MissingEntries
                && oversizedPageCapacity.Status
                    == X64PageMapStatus
                        .PageImageCapacityExceedsLimit
                && oversizedEntryCapacity.Status
                    == X64PageMapStatus.EntryCapacityExceedsLimit
                && misalignedPages.Status
                    == X64PageMapStatus.MisalignedPageImages
                && overflowingPageSpan.Status
                    == X64PageMapStatus.PageImageSpanOverflow
                && misalignedEntries.Status
                    == X64PageMapStatus.MisalignedEntries
                && overflowingEntrySpan.Status
                    == X64PageMapStatus.EntrySpanOverflow
                && aliasedStorage.Status
                    == X64PageMapStatus.StorageOverlap
                && malformedRecord.Status
                    == X64PageMapStatus.InvalidPageImageRecord
                && malformedRecord.InvalidPageImageIndex == 1
                && pageOrder.Status
                    == X64PageMapStatus.PageImageOrderInvalid
                && pageOrder.InvalidPageImageIndex == 1
                && essentialMisaligned.Status
                    == X64PageMapStatus.MisalignedEssentialRanges
                && essentialOverflow.Status
                    == X64PageMapStatus.EssentialRangeSpanOverflow
                && essentialAlias.Status
                    == X64PageMapStatus.EssentialRangeStorageOverlap
                && invalidEssentialRecord.Status
                    == X64PageMapStatus.InvalidEssentialRange
                && invalidEssentialRecord
                    .InvalidEssentialRangeIndex == 0
                && essentialOrder.Status
                    == X64PageMapStatus.EssentialRangeOrderInvalid
                && essentialOrder.InvalidEssentialRangeIndex == 1
                && CountDifferences(baseline, fixture.Entries) == 0;
        }
    }

    private static bool TestWalkFailures()
    {
        using M01014PageTableFixture missing = new();
        X64PageMapRequest request =
            X64PageMapRequest.CreateMap(
                missing.Root,
                missing.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                missing.Context);
        missing.Entries[0] = 0;
        ulong[] missingBaseline =
            (ulong[])missing.Entries.Clone();
        X64PageMapResult missingTable = missing.Apply(request);

        using M01014PageTableFixture pointerMissing = new();
        pointerMissing.Entries[0] =
            M01014TestData.EncodeTablePointer(
                X64PageTableLevel.Pml4e,
                M01014TestData.Physical(0x0020_0000),
                pointerMissing.Context);
        ulong[] pointerMissingBaseline =
            (ulong[])pointerMissing.Entries.Clone();
        X64PageMapResult targetMissing =
            pointerMissing.Apply(
                X64PageMapRequest.CreateMap(
                    pointerMissing.Root,
                    pointerMissing.Target,
                    M01014TestData.Physical(0x0090_0000),
                    M01014TestData.ReadWrite,
                    PageCachePolicy.WriteBack,
                    pointerMissing.Context));

        using M01014PageTableFixture malformed = new();
        malformed.Entries[0] = 0x81;
        ulong[] malformedBaseline =
            (ulong[])malformed.Entries.Clone();
        X64PageMapResult malformedEntry =
            malformed.Apply(
                X64PageMapRequest.CreateMap(
                    malformed.Root,
                    malformed.Target,
                    M01014TestData.Physical(0x0090_0000),
                    M01014TestData.ReadWrite,
                    PageCachePolicy.WriteBack,
                    malformed.Context));

        using M01014PageTableFixture large = new();
        uint pdIndex = (uint)(
            (M01014TestData.Raw(large.Target) >> 21) & 0x1ff);
        X64PageTableEntryResult largeLeaf =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pde,
                    X64PageTableEntryKind.Leaf,
                    M01014TestData.Physical(0x0200_0000),
                    M01014TestData.ReadWrite,
                    PageCachePolicy.WriteBack),
                large.Context);
        large.Entries[1024 + pdIndex] =
            largeLeaf.Entry.RawValue;
        ulong[] largeBaseline =
            (ulong[])large.Entries.Clone();
        X64PageMapResult collision =
            large.Apply(
                X64PageMapRequest.CreateMap(
                    large.Root,
                    large.Target,
                    M01014TestData.Physical(0x0090_0000),
                    M01014TestData.ReadWrite,
                    PageCachePolicy.WriteBack,
                    large.Context));
        using M01014PageTableFixture oneGiBLarge = new();
        uint pdptIndex = (uint)((
            M01014TestData.Raw(oneGiBLarge.Target) >> 30) & 0x1ff);
        X64PageTableEntryResult oneGiBLeaf =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pdpte,
                    X64PageTableEntryKind.Leaf,
                    M01014TestData.Physical(0x4000_0000),
                    M01014TestData.ReadWrite,
                    PageCachePolicy.WriteBack),
                oneGiBLarge.Context);
        oneGiBLarge.Entries[512 + pdptIndex] =
            oneGiBLeaf.Entry.RawValue;
        ulong[] oneGiBBaseline =
            (ulong[])oneGiBLarge.Entries.Clone();
        X64PageMapResult oneGiBCollision =
            oneGiBLarge.Apply(
                X64PageMapRequest.CreateMap(
                    oneGiBLarge.Root,
                    oneGiBLarge.Target,
                    M01014TestData.Physical(0x0090_0000),
                    M01014TestData.ReadWrite,
                    PageCachePolicy.WriteBack,
                    oneGiBLarge.Context));

        return missingTable.Status == X64PageMapStatus.MissingTable
            && targetMissing.Status
                == X64PageMapStatus.TablePointerTargetMissing
            && malformedEntry.Status
                == X64PageMapStatus.MalformedEntry
            && largeLeaf.Succeeded
            && collision.Status == X64PageMapStatus.LargePageCollision
            && oneGiBLeaf.Succeeded
            && oneGiBCollision.Status
                == X64PageMapStatus.LargePageCollision
            && CountDifferences(
                missingBaseline,
                missing.Entries) == 0
            && CountDifferences(
                pointerMissingBaseline,
                pointerMissing.Entries) == 0
            && CountDifferences(
                malformedBaseline,
                malformed.Entries) == 0
            && CountDifferences(
                largeBaseline,
                large.Entries) == 0
            && CountDifferences(
                oneGiBBaseline,
                oneGiBLarge.Entries) == 0;
    }

    private static bool TestEssentialMappingFailures()
    {
        using M01014PageTableFixture fixture = new();
        X64PageMapResult map = fixture.Apply(
            X64PageMapRequest.CreateMap(
                fixture.Root,
                fixture.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                fixture.Context));
        if (!map.Succeeded)
            return false;

        X64EssentialMappingRange.TryCreate(
            fixture.Target,
            1,
            out X64EssentialMappingRange essential);
        X64EssentialMappingRange[] ranges = [essential];
        ulong[] baseline = (ulong[])fixture.Entries.Clone();
        X64PageMapResult unmap = fixture.Apply(
            X64PageMapRequest.CreateUnmap(
                fixture.Root,
                fixture.Target,
                fixture.Context),
            ranges);
        using M01014PageTableFixture unmapped = new();
        ulong[] unmappedBaseline =
            (ulong[])unmapped.Entries.Clone();
        X64PageMapResult blockedMap = unmapped.Apply(
            X64PageMapRequest.CreateMap(
                unmapped.Root,
                unmapped.Target,
                M01014TestData.Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                unmapped.Context),
            ranges);
        X64PageMapResult protect = fixture.Apply(
            X64PageMapRequest.CreateProtect(
                fixture.Root,
                fixture.Target,
                M01014TestData.ReadExecute,
                X64HardwareStatePolicy.Preserve,
                fixture.Context),
            ranges);
        return unmap.Status
                == X64PageMapStatus.EssentialMappingMutationRejected
            && unmap.InvalidEssentialRangeIndex == 0
            && protect.Status
                == X64PageMapStatus.EssentialMappingMutationRejected
            && blockedMap.Status
                == X64PageMapStatus.EssentialMappingMutationRejected
            && blockedMap.InvalidEssentialRangeIndex == 0
            && CountDifferences(baseline, fixture.Entries) == 0
            && CountDifferences(
                unmappedBaseline,
                unmapped.Entries) == 0;
    }

    private static bool TestSourceAndNoStdLibBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string[] sourcePaths =
        [
            @"src\OS.Kernel\Memory\X64PageMap.cs",
            @"src\OS.Kernel\Memory\X64TlbInvalidationPlan.cs",
            @"src\OS.Kernel\Memory\X64InvlpgStubPlan.cs",
            @"src\OS.Kernel\Memory\X64ProtectionProbeStubPlan.cs"
        ];
        foreach (string relative in sourcePaths)
        {
            string source = File.ReadAllText(
                Path.Combine(root, relative));
            if (source.Contains("System.", StringComparison.Ordinal) ||
                source.Contains("Span<", StringComparison.Ordinal) ||
                source.Contains("throw ", StringComparison.Ordinal) ||
                source.Contains("delegate ", StringComparison.Ordinal) ||
                source.Contains("invlpg", StringComparison.OrdinalIgnoreCase)
                    && relative.EndsWith(
                        "X64PageMap.cs",
                        StringComparison.Ordinal))
            {
                return false;
            }
        }

        return BuildNoStdLibClosure(root);
    }

    private static bool BuildNoStdLibClosure(string root)
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-014-"
            + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            File.Copy(
                Path.Combine(root, "global.json"),
                Path.Combine(workspace, "global.json"));
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
                """
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
                    public struct SByte { }
                    public struct Int16 { }
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
                        public AttributeUsageAttribute(
                            AttributeTargets validOn) { }
                        public bool AllowMultiple { get; set; }
                        public bool Inherited { get; set; }
                    }
                }

                namespace System.Runtime.CompilerServices
                {
                    public sealed class IsReadOnlyAttribute
                        : System.Attribute { }
                }
                """,
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                internal static unsafe class Probe
                {
                    public static byte Run()
                    {
                        OS.Kernel.Memory.X64PageMapRequest request = default;
                        OS.Kernel.Memory.X64PageMapResult result =
                            OS.Kernel.Memory.X64PageMap.Apply(
                                request, null, 0, 0, null, 0, 0,
                                null, 0, 0);
                        return (byte)result.Status;
                    }
                }
                """,
                new UTF8Encoding(false));

            string[] sources =
            [
                @"src\OS.Kernel\Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
                @"src\OS.Kernel\Boot\UefiMemoryType.cs",
                @"src\OS.Kernel\Boot\BootMemoryRegionMaterializer.cs",
                @"src\OS.Kernel\Memory\BootReservedRange.cs",
                @"src\OS.Kernel\Memory\PageProtection.cs",
                @"src\OS.Kernel\Memory\PageCachePolicy.cs",
                @"src\OS.Kernel\Memory\X64PageTableEntry.cs",
                @"src\OS.Kernel\Memory\X64TlbInvalidationPlan.cs",
                @"src\OS.Kernel\Memory\X64PageMap.cs",
                @"src\OS.Kernel\Memory\X64InvlpgStubPlan.cs",
                @"src\OS.Kernel\Memory\X64ProtectionProbeStubPlan.cs"
            ];
            StringBuilder items = new();
            items.AppendLine(
                """    <Compile Include="NoStdLibShim.cs" />""");
            foreach (string relative in sources)
            {
                items.Append("    <Compile Include=\"");
                items.Append(EscapeXml(Path.Combine(root, relative)));
                items.AppendLine("\" />");
            }
            items.AppendLine(
                """    <Compile Include="Probe.cs" />""");

            string project =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.PageMapClosure</AssemblyName>
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
                __ITEMS__
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
                """
                .Replace(
                    "__ITEMS__",
                    items.ToString().TrimEnd(),
                    StringComparison.Ordinal);
            string projectPath = Path.Combine(
                workspace,
                "M01.014.NoStdLib.csproj");
            File.WriteAllText(
                projectPath,
                project,
                new UTF8Encoding(false));

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
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException(
                    "Failed to start M01-014 no-stdlib build.");
            Task<string> stdout =
                process.StandardOutput.ReadToEndAsync();
            Task<string> stderr =
                process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(90000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            Task.WaitAll(stdout, stderr);
            if (!exited || process.ExitCode != 0)
            {
                Console.WriteLine(
                    "M01_014_NOSTDLIB_DIAGNOSTIC "
                    + NormalizeCompilerOutput(
                        stdout.Result
                        + Environment.NewLine
                        + stderr.Result));
                return false;
            }

            string assemblyPath = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.PageMapClosure.dll");
            using FileStream stream = File.OpenRead(assemblyPath);
            using PEReader reader = new(stream);
            return reader.HasMetadata
                && reader.GetMetadataReader()
                    .AssemblyReferences.Count == 0;
        }
        finally
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static int CountDifferences(
        ulong[] first,
        ulong[] second)
    {
        int differences = 0;
        for (int index = 0; index < first.Length; index++)
        {
            if (first[index] != second[index])
                differences++;
        }

        return differences;
    }

    private static int CountNonZero(ulong[] values)
    {
        int count = 0;
        foreach (ulong value in values)
        {
            if (value != 0)
                count++;
        }

        return count;
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string NormalizeCompilerOutput(string value)
    {
        return string.Join(
            " ",
            value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries))
            .Replace(
                HostToolProcess.FindRepositoryRoot(),
                "<repo>",
                StringComparison.Ordinal)
            .Trim();
    }

    private static string Normalize(string value)
    {
        return value
            .Replace(' ', '_')
            .Replace('-', '_');
    }

    private static bool HasExactSequentialByteEnum<T>(
        string[] expectedNames)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
            return false;

        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();
        if (names.Length != expectedNames.Length ||
            values.Length != expectedNames.Length)
        {
            return false;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (!string.Equals(
                    names[index],
                    expectedNames[index],
                    StringComparison.Ordinal) ||
                Convert.ToByte(values[index]) != index)
            {
                return false;
            }
        }

        return true;
    }
}

internal static class M01014TestData
{
    internal const ulong PatFixture = 0x0006_0501_0007_0406;
    internal static readonly PageProtection ReadWrite =
        (PageProtection)(
            (byte)PageProtection.Read |
            (byte)PageProtection.Write);
    internal static readonly PageProtection ReadExecute =
        (PageProtection)(
            (byte)PageProtection.Read |
            (byte)PageProtection.Execute);

    internal static X64PageTableEncodingPreconditions Context()
    {
        X64PageTableEntryStatus status =
            X64PatMappingToken.TryCreate(
                PatFixture,
                writeBackIndex: 0,
                uncachedIndex: 3,
                writeCombiningIndex: 4,
                out X64PatMappingToken token);
        if (status != X64PageTableEntryStatus.Success)
            throw new InvalidOperationException("PAT fixture invalid.");

        return X64PageTableEncodingPreconditions.CreateObserved(
            48,
            nxeEnabled: true,
            writeProtectEnabled: true,
            fiveLevelPagingEnabled: false,
            oneGiBPagesSupported: true,
            token);
    }

    internal static PhysicalAddress Physical(ulong value)
    {
        AddressValueStatus status =
            PhysicalAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    value,
                    X64PageMap.PageSize,
                    out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
            throw new InvalidOperationException(
                "Physical fixture invalid: " + status);
        return address;
    }

    internal static PhysicalAddress PhysicalUnaligned(ulong value)
    {
        AddressValueStatus status =
            PhysicalAddress
                .TryCreateFromHardwareRegisterBoundary(
                    value,
                    out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
            throw new InvalidOperationException(
                "Physical fixture invalid: " + status);
        return address;
    }

    internal static VirtualAddress Virtual(ulong value)
    {
        AddressValueStatus status =
            VirtualAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    value,
                    X64PageMap.PageSize,
                    out VirtualAddress address);
        if (status != AddressValueStatus.Success)
            throw new InvalidOperationException(
                "Virtual fixture invalid: " + status);
        return address;
    }

    internal static VirtualAddress VirtualUnaligned(ulong value)
    {
        AddressValueStatus status =
            VirtualAddress
                .TryCreateFromHardwareRegisterBoundary(
                    value,
                    out VirtualAddress address);
        if (status != AddressValueStatus.Success)
            throw new InvalidOperationException(
                "Virtual fixture invalid: " + status);
        return address;
    }

    internal static ulong Raw(PhysicalAddress address)
    {
        if (address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value) != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Physical raw conversion failed.");
        }

        return value;
    }

    internal static ulong Raw(VirtualAddress address)
    {
        if (address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value) != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Virtual raw conversion failed.");
        }

        return value;
    }

    internal static ulong EncodeTablePointer(
        X64PageTableLevel level,
        PhysicalAddress child,
        X64PageTableEncodingPreconditions context)
    {
        X64PageTableEntryResult result =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    level,
                    X64PageTableEntryKind.TablePointer,
                    child,
                    PageProtection.None,
                    PageCachePolicy.WriteBack),
                context);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Table pointer fixture invalid: " + result.Status);
        }

        return result.Entry.RawValue;
    }
}

internal sealed unsafe class M01014PageTableFixture : IDisposable
{
    internal X64PageTableImagePage[] Pages { get; }

    internal ulong[] Entries { get; }

    internal X64PageTableEncodingPreconditions Context { get; }

    internal PhysicalAddress Root { get; }

    internal VirtualAddress Target { get; }

    internal M01014PageTableFixture(
        ulong target = 0x0040_0000,
        ulong tablePhysicalStart = 0x0010_0000)
    {
        Context = M01014TestData.Context();
        Root = M01014TestData.Physical(tablePhysicalStart);
        Target = M01014TestData.Virtual(target);
        PhysicalAddress pdpt =
            M01014TestData.Physical(
                tablePhysicalStart + 0x1000);
        PhysicalAddress pd =
            M01014TestData.Physical(
                tablePhysicalStart + 0x2000);
        PhysicalAddress pt =
            M01014TestData.Physical(
                tablePhysicalStart + 0x3000);
        Pages = new X64PageTableImagePage[4];
        X64PageTableImagePage.TryCreate(
            Root, 0, out Pages[0]);
        X64PageTableImagePage.TryCreate(
            pdpt, 512, out Pages[1]);
        X64PageTableImagePage.TryCreate(
            pd, 1024, out Pages[2]);
        X64PageTableImagePage.TryCreate(
            pt, 1536, out Pages[3]);
        Entries = new ulong[2048];

        ulong rawTarget = M01014TestData.Raw(Target);
        uint pml4Index = (uint)((rawTarget >> 39) & 0x1ff);
        uint pdptIndex = (uint)((rawTarget >> 30) & 0x1ff);
        uint pdIndex = (uint)((rawTarget >> 21) & 0x1ff);
        Entries[pml4Index] =
            M01014TestData.EncodeTablePointer(
                X64PageTableLevel.Pml4e,
                pdpt,
                Context);
        Entries[512 + pdptIndex] =
            M01014TestData.EncodeTablePointer(
                X64PageTableLevel.Pdpte,
                pd,
                Context);
        Entries[1024 + pdIndex] =
            M01014TestData.EncodeTablePointer(
                X64PageTableLevel.Pde,
                pt,
                Context);
    }

    internal X64PageMapResult Apply(
        X64PageMapRequest request,
        X64EssentialMappingRange[]? essential = null)
    {
        essential ??= [];
        fixed (X64PageTableImagePage* pages = Pages)
        fixed (ulong* entries = Entries)
        fixed (X64EssentialMappingRange* ranges = essential)
        {
            return X64PageMap.Apply(
                request,
                pages,
                (ulong)Pages.Length,
                (ulong)Pages.Length,
                entries,
                (ulong)Entries.Length,
                (ulong)Entries.Length,
                ranges,
                (ulong)essential.Length,
                (ulong)essential.Length);
        }
    }

    public void Dispose()
    {
    }
}
