using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static unsafe class PageZeroMappingPolicyTests
{
    private const string IssueTestName = "m01.015";
    private const string ReadyMarker = "CLROS_M01_015_READY";
    private const int RequestStructureSizeOffset = 0;
    private const int RequestStructureVersionOffset = 4;
    private const int RequestOwnedDiagnosticsReadyOffset = 8;
    private const int RequestReservedOffset = 12;
    private const int PolicyStatusOffset = 7;
    private const int PolicyOperationOffset = 8;
    private const int PolicyAbsentAtLevelOffset = 10;
    private const int PolicyReserved0Offset = 11;
    private const int PolicyIntentOffset = 16;
    private const int PolicyIntentStructureSizeOffset =
        PolicyIntentOffset;
    private const int PolicyIntentStructureVersionOffset =
        PolicyIntentOffset + 4;
    private const int PolicyIntentInitializedOffset =
        PolicyIntentOffset + 6;
    private const int PolicyPageImageCountOffset = 48;
    private const int PolicyTerminalPageIndexOffset = 88;
    private const int PolicyTerminalEntryIndexOffset = 96;
    private const int PolicyInspectedEntryCountOffset = 104;
    private const int PolicyTerminalRawEntryOffset = 112;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + IssueTestName);
        int failures = 0;

        RunCase(
            "types enums layouts defaults",
            TestTypeContracts,
            ref failures);
        RunCase(
            "raw mapping intents reject page zero and wrap",
            TestMappingIntents,
            ref failures);
        RunCase(
            "table audit proves absence at every level",
            TestAbsenceAtEveryLevel,
            ref failures);
        RunCase(
            "table audit rejects present and malformed entries",
            TestPresentAndMalformedEntries,
            ref failures);
        RunCase(
            "pointer count capacity and alias failures are bounded",
            TestStorageBoundary,
            ref failures);
        RunCase(
            "mapping rejection and audits never mutate images",
            TestNonMutation,
            ref failures);
        RunCase(
            "null fault plan remains disabled and non executing",
            TestNullFaultPlan,
            ref failures);
        RunCase(
            "source and nostdlib closure remain isolated",
            TestSourceAndNoStdLibBoundary,
            ref failures);
        RunCase(
            "efi and marker ownership remain isolated",
            TestEfiAndMarkerBoundary,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL "
                + IssueTestName
                + " failures="
                + failures);
            throw new InvalidOperationException(
                "M01-015 page-zero policy failures=" + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS " + IssueTestName);
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
                "M01_015_CASE_FAIL " + Normalize(name));
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "M01_015_CASE_FAIL "
                + Normalize(name)
                + " "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static bool TestTypeContracts()
    {
        string[] operationNames =
        [
            "Uninitialized",
            "MappingIntent",
            "TableImageAudit"
        ];
        string[] policyStatusNames =
        [
            "Uninitialized",
            "Success",
            "InvalidIntentState",
            "InvalidPresentFlag",
            "ZeroLength",
            "PresentRangeCoversPageZero",
            "RangeWrapsThroughPageZero",
            "MisalignedRangeStart",
            "MisalignedByteLength",
            "NonCanonicalRange",
            "ContractLayoutMismatch",
            "InvalidRootTable",
            "InvalidPreconditions",
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
            "RootPageNotFound",
            "MalformedEntry",
            "TablePointerTargetMissing",
            "PageZeroPresent"
        ];
        string[] planStatusNames =
        [
            "Uninitialized",
            "ReadyDisabled",
            "InvalidRequestState",
            "PageZeroPolicyUnavailable",
            "EnabledByDefaultRejected",
            "ExecutionAttemptedRejected",
            "ExecutionRequestedRejected",
            "OwnedDiagnosticsUnavailable",
            "PageFaultHandlerUnavailable"
        ];
        string[] accessNames =
        [
            "Uninitialized",
            "Read"
        ];

        return HasExactSequentialByteEnum
                <PageZeroMappingPolicyOperation>(operationNames)
            && HasExactSequentialByteEnum
                <PageZeroMappingPolicyStatus>(policyStatusNames)
            && HasExactSequentialByteEnum
                <NullPageFaultProbePlanStatus>(planStatusNames)
            && HasExactSequentialByteEnum
                <NullPageFaultAccessKind>(accessNames)
            && (ulong)Unsafe.SizeOf<PageZeroMappingIntent>()
                == PageZeroMappingIntent.StructureSize
            && (ulong)Unsafe.SizeOf<PageZeroMappingPolicyResult>()
                == PageZeroMappingPolicyResult.StructureSize
            && (ulong)Unsafe.SizeOf<NullPageFaultProbeRequest>()
                == NullPageFaultProbeRequest.StructureSize
            && (ulong)Unsafe.SizeOf<NullPageFaultProbePlanResult>()
                == NullPageFaultProbePlanResult.StructureSize
            && HasFieldOffset<NullPageFaultProbeRequest>(
                "_structureSize",
                RequestStructureSizeOffset)
            && HasFieldOffset<NullPageFaultProbeRequest>(
                "_structureVersion",
                RequestStructureVersionOffset)
            && HasFieldOffset<NullPageFaultProbeRequest>(
                "_ownedDiagnosticsReady",
                RequestOwnedDiagnosticsReadyOffset)
            && HasFieldOffset<NullPageFaultProbeRequest>(
                "_reserved",
                RequestReservedOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_status",
                PolicyStatusOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_operation",
                PolicyOperationOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_absentAtLevel",
                PolicyAbsentAtLevelOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_reserved0",
                PolicyReserved0Offset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_intent",
                PolicyIntentOffset)
            && HasFieldOffset<PageZeroMappingIntent>(
                "_structureSize",
                PolicyIntentStructureSizeOffset - PolicyIntentOffset)
            && HasFieldOffset<PageZeroMappingIntent>(
                "_structureVersion",
                PolicyIntentStructureVersionOffset - PolicyIntentOffset)
            && HasFieldOffset<PageZeroMappingIntent>(
                "_isInitialized",
                PolicyIntentInitializedOffset - PolicyIntentOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_pageImageCount",
                PolicyPageImageCountOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_terminalPageIndex",
                PolicyTerminalPageIndexOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_terminalEntryIndex",
                PolicyTerminalEntryIndexOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_inspectedEntryCount",
                PolicyInspectedEntryCountOffset)
            && HasFieldOffset<PageZeroMappingPolicyResult>(
                "_terminalRawEntry",
                PolicyTerminalRawEntryOffset)
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences
                    <PageZeroMappingIntent>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences
                    <PageZeroMappingPolicyResult>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences
                    <NullPageFaultProbeRequest>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences
                    <NullPageFaultProbePlanResult>()
            && !default(PageZeroMappingIntent).IsInitialized
            && !default(PageZeroMappingPolicyResult).Succeeded
            && !default(PageZeroMappingPolicyResult)
                .ProvesPageZeroAbsentInSuppliedImage
            && !default(NullPageFaultProbeRequest).IsInitialized
            && !default(NullPageFaultProbePlanResult).Succeeded
            && PageZeroMappingPolicy.PageSize == 4096
            && PageZeroMappingPolicy.MaximumPageImages == 64
            && PageZeroMappingPolicy.MaximumEntryCount == 32768
            && PageZeroMappingPolicy.RequiredPointerAlignment == 8
            && PageZeroMappingPolicy.LowCanonicalEndExclusive ==
                0x0000_8000_0000_0000UL
            && PageZeroMappingPolicyResult.NoIndex == ~0UL
            && !PageZeroMappingPolicy.PerformsLiveMutation
            && !PageZeroMappingPolicy.WritesTableImage
            && !PageZeroMappingPolicy.ReadsLivePageTables
            && !PageZeroMappingPolicy.ProvesInstalledRoot
            && NullPageFaultProbePlan.ExpectedPageFaultVector == 14
            && NullPageFaultProbePlan.ExpectedErrorCode == 0
            && NullPageFaultProbePlan.CheckedErrorCodeMask == 0x1f
            && NullPageFaultProbePlan.ExpectedCr2Raw == 0
            && !NullPageFaultProbePlan.EnabledByDefault
            && !NullPageFaultProbePlan.PerformsLiveFault
            && !NullPageFaultProbePlan.DereferencesPageZero
            && !NullPageFaultProbePlan.EmitsExecutableBytes
            && !NullPageFaultProbePlan.ExecutesProbe
            && !NullPageFaultProbePlan.IsGuestEvidence
            && NullPageFaultProbePlan.ExecutionDeferredToM02014;
    }

    private static bool TestMappingIntents()
    {
        PageZeroMappingPolicyResult exactPresent =
            ValidateIntent(0, 0x1000, present: true);
        PageZeroMappingPolicyResult partialFirst =
            ValidateIntent(1, 1, present: true);
        PageZeroMappingPolicyResult partialEnd =
            ValidateIntent(1, 0x0fff, present: true);
        PageZeroMappingPolicyResult lastByte =
            ValidateIntent(0x0fff, 1, present: true);
        PageZeroMappingPolicyResult crossing =
            ValidateIntent(0x0fff, 2, present: true);
        PageZeroMappingPolicyResult firstNonzeroPage =
            ValidateIntent(0x1000, 0x1000, present: true);
        PageZeroMappingPolicyResult absentZero =
            ValidateIntent(0, 0x1000, present: false);
        PageZeroMappingPolicyResult zeroLength =
            ValidateIntent(0x1000, 0, present: true);
        PageZeroMappingPolicyResult misalignedStart =
            ValidateIntent(0x1001, 0x1000, present: true);
        PageZeroMappingPolicyResult misalignedLength =
            ValidateIntent(0x1000, 1, present: true);
        PageZeroMappingPolicyResult wrappingOne =
            ValidateIntent(~0UL, 1, present: true);
        PageZeroMappingPolicyResult wrappingPage =
            ValidateIntent(~0UL - 0x0fff, 0x1000, present: true);
        PageZeroMappingPolicyResult upperCrossing =
            ValidateIntent(
                PageZeroMappingPolicy.LowCanonicalEndExclusive
                    - 0x1000,
                0x2000,
                present: true);
        PageZeroMappingPolicyResult finalCanonicalPage =
            ValidateIntent(
                PageZeroMappingPolicy.LowCanonicalEndExclusive
                    - 0x1000,
                0x1000,
                present: true);
        PageZeroMappingPolicyResult firstNonCanonicalPage =
            ValidateIntent(
                PageZeroMappingPolicy.LowCanonicalEndExclusive,
                0x1000,
                present: true);
        PageZeroMappingPolicyResult absentWrap =
            ValidateIntent(~0UL, 1, present: false);
        PageZeroMappingPolicyResult hostilePresent =
            ValidateHostilePresentFlag();

        return exactPresent.Status ==
                PageZeroMappingPolicyStatus
                    .PresentRangeCoversPageZero
            && partialFirst.Status ==
                PageZeroMappingPolicyStatus
                    .PresentRangeCoversPageZero
            && partialEnd.Status ==
                PageZeroMappingPolicyStatus
                    .PresentRangeCoversPageZero
            && lastByte.Status ==
                PageZeroMappingPolicyStatus
                    .PresentRangeCoversPageZero
            && crossing.Status ==
                PageZeroMappingPolicyStatus
                    .PresentRangeCoversPageZero
            && firstNonzeroPage.Succeeded
            && firstNonzeroPage.Operation ==
                PageZeroMappingPolicyOperation.MappingIntent
            && !firstNonzeroPage
                .ProvesPageZeroAbsentInSuppliedImage
            && absentZero.Succeeded
            && !absentZero
                .ProvesPageZeroAbsentInSuppliedImage
            && zeroLength.Status ==
                PageZeroMappingPolicyStatus.ZeroLength
            && misalignedStart.Status ==
                PageZeroMappingPolicyStatus.MisalignedRangeStart
            && misalignedLength.Status ==
                PageZeroMappingPolicyStatus.MisalignedByteLength
            && wrappingOne.Status ==
                PageZeroMappingPolicyStatus
                    .RangeWrapsThroughPageZero
            && wrappingPage.Status ==
                PageZeroMappingPolicyStatus
                    .RangeWrapsThroughPageZero
            && upperCrossing.Status ==
                PageZeroMappingPolicyStatus.NonCanonicalRange
            && finalCanonicalPage.Succeeded
            && finalCanonicalPage.Intent.RawVirtualStart ==
                PageZeroMappingPolicy.LowCanonicalEndExclusive
                    - 0x1000
            && finalCanonicalPage.Intent.ByteLength == 0x1000
            && finalCanonicalPage.Intent.IsPresentMapping
            && finalCanonicalPage.RootPageIndex ==
                PageZeroMappingPolicyResult.NoIndex
            && finalCanonicalPage.TerminalPageIndex ==
                PageZeroMappingPolicyResult.NoIndex
            && finalCanonicalPage.TerminalEntryIndex ==
                PageZeroMappingPolicyResult.NoIndex
            && finalCanonicalPage.PageImageCount == 0
            && finalCanonicalPage.EntryCount == 0
            && firstNonCanonicalPage.Status ==
                PageZeroMappingPolicyStatus.NonCanonicalRange
            && absentWrap.Status ==
                PageZeroMappingPolicyStatus
                    .RangeWrapsThroughPageZero
            && hostilePresent.Status ==
                PageZeroMappingPolicyStatus.InvalidPresentFlag
            && !default(PageZeroMappingPolicyResult).Succeeded;
    }

    private static bool TestAbsenceAtEveryLevel()
    {
        for (int absentDepth = 0; absentDepth < 4; absentDepth++)
        {
            using M01015PageZeroFixture fixture = new();
            fixture.LinkThrough(absentDepth);
            ulong[] entriesBefore = (ulong[])fixture.Entries.Clone();
            X64PageTableImagePage[] pagesBefore =
                (X64PageTableImagePage[])fixture.Pages.Clone();

            PageZeroMappingPolicyResult result = fixture.Audit();
            X64PageTableLevel expectedLevel =
                (X64PageTableLevel)(4 - absentDepth);
            if (!result.ProvesPageZeroAbsentInSuppliedImage ||
                result.Status != PageZeroMappingPolicyStatus.Success ||
                result.Operation !=
                    PageZeroMappingPolicyOperation.TableImageAudit ||
                result.EntryStatus !=
                    X64PageTableEntryStatus.Success ||
                result.AbsentAtLevel != expectedLevel ||
                result.RootPageIndex != 0 ||
                result.TerminalPageIndex !=
                    (ulong)absentDepth ||
                result.TerminalEntryIndex !=
                    (ulong)absentDepth * 512 ||
                result.InspectedEntryCount !=
                    (ulong)absentDepth + 1 ||
                result.TerminalRawEntry != 0 ||
                result.PresentLeafPageSize != 0 ||
                CountDifferences(entriesBefore, fixture.Entries) != 0 ||
                CountDifferences(pagesBefore, fixture.Pages) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestPresentAndMalformedEntries()
    {
        using M01015PageZeroFixture oneGiB = new();
        oneGiB.LinkThrough(1);
        oneGiB.Entries[512] = 0x81;
        PageZeroMappingPolicyResult oneGiBResult = oneGiB.Audit();

        using M01015PageZeroFixture twoMiB = new();
        twoMiB.LinkThrough(2);
        twoMiB.Entries[1024] = 0x81;
        PageZeroMappingPolicyResult twoMiBResult = twoMiB.Audit();

        using M01015PageZeroFixture fourKiB = new();
        fourKiB.LinkThrough(3);
        fourKiB.Entries[1536] = 1;
        PageZeroMappingPolicyResult fourKiBResult = fourKiB.Audit();

        using M01015PageZeroFixture ordinaryLeaf = new();
        ordinaryLeaf.LinkThrough(3);
        ordinaryLeaf.Entries[1536] =
            ordinaryLeaf.EncodeLeaf(
                X64PageTableLevel.Pte,
                0x0080_0000);
        PageZeroMappingPolicyResult ordinaryLeafResult =
            ordinaryLeaf.Audit();

        using M01015PageZeroFixture malformedAbsent = new();
        malformedAbsent.Entries[0] = 2;
        PageZeroMappingPolicyResult malformedAbsentResult =
            malformedAbsent.Audit();

        using M01015PageZeroFixture invalidLargeAtPml4 = new();
        invalidLargeAtPml4.Entries[0] = 0x81;
        PageZeroMappingPolicyResult invalidLargeResult =
            invalidLargeAtPml4.Audit();

        using M01015PageZeroFixture missingChild = new();
        missingChild.Entries[0] =
            missingChild.EncodeTablePointer(
                X64PageTableLevel.Pml4e,
                0x0020_0000);
        PageZeroMappingPolicyResult missingChildResult =
            missingChild.Audit();

        return IsPresentLeaf(
                oneGiBResult,
                X64PageTableEntry.PageSize1GiB,
                512,
                2)
            && IsPresentLeaf(
                twoMiBResult,
                X64PageTableEntry.PageSize2MiB,
                1024,
                3)
            && IsPresentLeaf(
                fourKiBResult,
                X64PageTableEntry.PageSize4KiB,
                1536,
                4)
            && IsPresentLeaf(
                ordinaryLeafResult,
                X64PageTableEntry.PageSize4KiB,
                1536,
                4)
            && malformedAbsentResult.Status ==
                PageZeroMappingPolicyStatus.MalformedEntry
            && malformedAbsentResult.EntryStatus ==
                X64PageTableEntryStatus
                    .NonCanonicalUnmappedEntry
            && invalidLargeResult.Status ==
                PageZeroMappingPolicyStatus.MalformedEntry
            && invalidLargeResult.EntryStatus ==
                X64PageTableEntryStatus.InvalidLargePage
            && missingChildResult.Status ==
                PageZeroMappingPolicyStatus
                    .TablePointerTargetMissing
            && !oneGiBResult
                .ProvesPageZeroAbsentInSuppliedImage
            && !twoMiBResult
                .ProvesPageZeroAbsentInSuppliedImage
            && !fourKiBResult
                .ProvesPageZeroAbsentInSuppliedImage;
    }

    private static bool TestStorageBoundary()
    {
        using M01015PageZeroFixture fixture = new();
        PageZeroMappingPolicyResult invalidRoot;
        PageZeroMappingPolicyResult invalidPreconditions;
        PageZeroMappingPolicyResult zeroPages;
        PageZeroMappingPolicyResult tooManyPages;
        PageZeroMappingPolicyResult shortPageCapacity;
        PageZeroMappingPolicyResult excessivePageCapacity;
        PageZeroMappingPolicyResult missingPages;
        PageZeroMappingPolicyResult misalignedPages;
        PageZeroMappingPolicyResult pageSpanOverflow;
        PageZeroMappingPolicyResult zeroEntries;
        PageZeroMappingPolicyResult entryMismatch;
        PageZeroMappingPolicyResult shortEntryCapacity;
        PageZeroMappingPolicyResult excessiveEntryCapacity;
        PageZeroMappingPolicyResult missingEntries;
        PageZeroMappingPolicyResult misalignedEntries;
        PageZeroMappingPolicyResult entrySpanOverflow;
        PageZeroMappingPolicyResult aliasedStorage;

        fixed (X64PageTableImagePage* pages = fixture.Pages)
        fixed (ulong* entries = fixture.Entries)
        {
            invalidRoot = PageZeroMappingPolicy.AuditTableImage(
                default,
                fixture.Context,
                pages,
                4,
                4,
                entries,
                2048,
                2048);
            invalidPreconditions =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    default,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    2048);
            zeroPages = PageZeroMappingPolicy.AuditTableImage(
                fixture.Root,
                fixture.Context,
                pages,
                0,
                4,
                entries,
                2048,
                2048);
            tooManyPages = PageZeroMappingPolicy.AuditTableImage(
                fixture.Root,
                fixture.Context,
                pages,
                PageZeroMappingPolicy.MaximumPageImages + 1,
                PageZeroMappingPolicy.MaximumPageImages + 1,
                entries,
                2048,
                2048);
            shortPageCapacity =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    pages,
                    4,
                    3,
                    entries,
                    2048,
                    2048);
            excessivePageCapacity =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    pages,
                    4,
                    PageZeroMappingPolicy.MaximumPageImages + 1,
                    entries,
                    2048,
                    2048);
            missingPages = PageZeroMappingPolicy.AuditTableImage(
                fixture.Root,
                fixture.Context,
                null,
                4,
                4,
                entries,
                2048,
                2048);
            misalignedPages =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    (X64PageTableImagePage*)
                        ((byte*)pages + 1),
                    4,
                    4,
                    entries,
                    2048,
                    2048);
            pageSpanOverflow =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    (X64PageTableImagePage*)
                        (~0UL -
                            X64PageTableImagePage.StructureSize
                            + 1),
                    1,
                    1,
                    entries,
                    512,
                    512);
            zeroEntries = PageZeroMappingPolicy.AuditTableImage(
                fixture.Root,
                fixture.Context,
                pages,
                4,
                4,
                entries,
                0,
                2048);
            entryMismatch = PageZeroMappingPolicy.AuditTableImage(
                fixture.Root,
                fixture.Context,
                pages,
                4,
                4,
                entries,
                2047,
                2048);
            shortEntryCapacity =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    2047);
            excessiveEntryCapacity =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    pages,
                    4,
                    4,
                    entries,
                    2048,
                    PageZeroMappingPolicy.MaximumEntryCount + 1);
            missingEntries = PageZeroMappingPolicy.AuditTableImage(
                fixture.Root,
                fixture.Context,
                pages,
                4,
                4,
                null,
                2048,
                2048);
            misalignedEntries =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    pages,
                    4,
                    4,
                    (ulong*)((byte*)entries + 1),
                    2048,
                    2048);
            entrySpanOverflow =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    pages,
                    1,
                    1,
                    (ulong*)(~0UL - 7),
                    512,
                    512);
            aliasedStorage =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    pages,
                    4,
                    4,
                    (ulong*)pages,
                    2048,
                    2048);
        }

        using M01015PageZeroFixture defaultRecord = new();
        defaultRecord.Pages[0] = default;
        PageZeroMappingPolicyResult defaultRecordResult =
            defaultRecord.Audit();

        using M01015PageZeroFixture wrongOrder = new();
        (wrongOrder.Pages[0], wrongOrder.Pages[1]) =
            (wrongOrder.Pages[1], wrongOrder.Pages[0]);
        PageZeroMappingPolicyResult wrongOrderResult =
            wrongOrder.Audit(root: wrongOrder.Pages[1].PhysicalStart);

        using M01015PageZeroFixture descendingPhysical = new();
        descendingPhysical.Pages[0] =
            M01015TestData.Page(0x0010_1000, 0);
        descendingPhysical.Pages[1] =
            M01015TestData.Page(0x0010_0000, 512);
        PageZeroMappingPolicyResult descendingResult =
            descendingPhysical.Audit(
                root: descendingPhysical.Pages[0].PhysicalStart);

        using M01015PageZeroFixture duplicatePhysical = new();
        duplicatePhysical.Pages[1] =
            M01015TestData.Page(0x0010_0000, 512);
        PageZeroMappingPolicyResult duplicateResult =
            duplicatePhysical.Audit();

        using M01015PageZeroFixture missingRoot = new();
        PageZeroMappingPolicyResult missingRootResult =
            missingRoot.Audit(
                root: M01015TestData.Physical(0x0040_0000));

        return invalidRoot.Status ==
                PageZeroMappingPolicyStatus.InvalidRootTable
            && invalidPreconditions.Status ==
                PageZeroMappingPolicyStatus.InvalidPreconditions
            && zeroPages.Status ==
                PageZeroMappingPolicyStatus.InvalidPageImageCount
            && tooManyPages.Status ==
                PageZeroMappingPolicyStatus.TooManyPageImages
            && shortPageCapacity.Status ==
                PageZeroMappingPolicyStatus
                    .PageImageCapacityTooSmall
            && excessivePageCapacity.Status ==
                PageZeroMappingPolicyStatus
                    .PageImageCapacityExceedsLimit
            && missingPages.Status ==
                PageZeroMappingPolicyStatus.MissingPageImages
            && misalignedPages.Status ==
                PageZeroMappingPolicyStatus.MisalignedPageImages
            && pageSpanOverflow.Status ==
                PageZeroMappingPolicyStatus.PageImageSpanOverflow
            && zeroEntries.Status ==
                PageZeroMappingPolicyStatus.InvalidEntryCount
            && entryMismatch.Status ==
                PageZeroMappingPolicyStatus.EntryCountMismatch
            && shortEntryCapacity.Status ==
                PageZeroMappingPolicyStatus
                    .EntryCapacityTooSmall
            && excessiveEntryCapacity.Status ==
                PageZeroMappingPolicyStatus
                    .EntryCapacityExceedsLimit
            && missingEntries.Status ==
                PageZeroMappingPolicyStatus.MissingEntries
            && misalignedEntries.Status ==
                PageZeroMappingPolicyStatus.MisalignedEntries
            && entrySpanOverflow.Status ==
                PageZeroMappingPolicyStatus.EntrySpanOverflow
            && aliasedStorage.Status ==
                PageZeroMappingPolicyStatus.StorageOverlap
            && defaultRecordResult.Status ==
                PageZeroMappingPolicyStatus.InvalidPageImageRecord
            && wrongOrderResult.Status ==
                PageZeroMappingPolicyStatus.InvalidPageImageRecord
            && descendingResult.Status ==
                PageZeroMappingPolicyStatus.PageImageOrderInvalid
            && duplicateResult.Status ==
                PageZeroMappingPolicyStatus.PageImageOrderInvalid
            && missingRootResult.Status ==
                PageZeroMappingPolicyStatus.RootPageNotFound;
    }

    private static bool TestNonMutation()
    {
        using M01015PageZeroFixture fixture = new();
        fixture.LinkThrough(3);
        ulong[] before = (ulong[])fixture.Entries.Clone();
        X64PageTableImagePage[] pagesBefore =
            (X64PageTableImagePage[])fixture.Pages.Clone();

        fixture.Entries[1536] = 1;
        ulong[] presentBefore = (ulong[])fixture.Entries.Clone();
        PageZeroMappingPolicyResult rejected = fixture.Audit();
        bool auditUnchanged =
            rejected.Status ==
                PageZeroMappingPolicyStatus.PageZeroPresent
            && CountDifferences(presentBefore, fixture.Entries) == 0
            && CountDifferences(pagesBefore, fixture.Pages) == 0;

        Array.Copy(before, fixture.Entries, before.Length);
        X64PageMapRequest zeroRequest =
            X64PageMapRequest.CreateMap(
                fixture.Root,
                default,
                M01015TestData.Physical(0x0080_0000),
                PageProtection.Read,
                PageCachePolicy.WriteBack,
                fixture.Context);
        X64PageMapResult mapResult = fixture.Apply(zeroRequest);
        bool mapUnchanged =
            mapResult.Status == X64PageMapStatus.PageZeroRejected
            && CountDifferences(before, fixture.Entries) == 0
            && CountDifferences(pagesBefore, fixture.Pages) == 0;

        X64PageTableImagePage[] pagesWithTail =
            new X64PageTableImagePage[5];
        Array.Copy(fixture.Pages, pagesWithTail, 4);
        ulong[] entriesWithTail = new ulong[2560];
        Array.Copy(before, entriesWithTail, before.Length);
        for (int index = 2048; index < entriesWithTail.Length; index++)
        {
            entriesWithTail[index] =
                0xC15_0000_0000_0000UL + (ulong)index;
        }

        X64PageTableImagePage[] tailPagesBefore =
            (X64PageTableImagePage[])pagesWithTail.Clone();
        ulong[] tailEntriesBefore =
            (ulong[])entriesWithTail.Clone();
        PageZeroMappingPolicyResult tailAudit;
        PageZeroMappingPolicyResult rejectedCapacity;
        fixed (X64PageTableImagePage* pages = pagesWithTail)
        fixed (ulong* entries = entriesWithTail)
        {
            tailAudit = PageZeroMappingPolicy.AuditTableImage(
                fixture.Root,
                fixture.Context,
                pages,
                pageImageCount: 4,
                pageImageCapacity: 5,
                entries: entries,
                entryCount: 2048,
                entryCapacity: 2560);
            rejectedCapacity =
                PageZeroMappingPolicy.AuditTableImage(
                    fixture.Root,
                    fixture.Context,
                    pages,
                    pageImageCount: 4,
                    pageImageCapacity: 3,
                    entries: entries,
                    entryCount: 2048,
                    entryCapacity: 2560);
        }

        bool tailAndFailureUnchanged =
            tailAudit.ProvesPageZeroAbsentInSuppliedImage
            && rejectedCapacity.Status ==
                PageZeroMappingPolicyStatus
                    .PageImageCapacityTooSmall
            && CountDifferences(
                    tailPagesBefore,
                    pagesWithTail) == 0
            && CountDifferences(
                    tailEntriesBefore,
                    entriesWithTail) == 0;

        return auditUnchanged
            && mapUnchanged
            && tailAndFailureUnchanged;
    }

    private static bool TestNullFaultPlan()
    {
        using M01015PageZeroFixture fixture = new();
        fixture.LinkThrough(3);
        PageZeroMappingPolicyResult absence = fixture.Audit();

        NullPageFaultProbeRequest disabled =
            NullPageFaultProbeRequest.CreateDisabled(
                absence,
                callerAttestsOwnedDiagnosticsReady: true,
                callerAttestsPageFaultHandlerReady: true);
        NullPageFaultProbePlanResult ready =
            NullPageFaultProbePlan.Plan(disabled);

        NullPageFaultProbePlanResult noPolicy =
            NullPageFaultProbePlan.Plan(
                NullPageFaultProbeRequest.CreateDisabled(
                    default,
                    true,
                    true));
        NullPageFaultProbePlanResult defaultOn =
            NullPageFaultProbePlan.Plan(
                NullPageFaultProbeRequest.CreateForValidation(
                    absence,
                    enabledByDefault: true,
                    callerAttestsOwnedDiagnosticsReady: true,
                    callerAttestsPageFaultHandlerReady: true,
                    executionRequested: false,
                    executionAttempted: false));
        NullPageFaultProbePlanResult attempted =
            NullPageFaultProbePlan.Plan(
                NullPageFaultProbeRequest.CreateForValidation(
                    absence,
                    enabledByDefault: false,
                    callerAttestsOwnedDiagnosticsReady: true,
                    callerAttestsPageFaultHandlerReady: true,
                    executionRequested: false,
                    executionAttempted: true));
        NullPageFaultProbePlanResult requested =
            NullPageFaultProbePlan.Plan(
                NullPageFaultProbeRequest.CreateForValidation(
                    absence,
                    enabledByDefault: false,
                    callerAttestsOwnedDiagnosticsReady: true,
                    callerAttestsPageFaultHandlerReady: true,
                    executionRequested: true,
                    executionAttempted: false));
        NullPageFaultProbePlanResult noDiagnostics =
            NullPageFaultProbePlan.Plan(
                NullPageFaultProbeRequest.CreateDisabled(
                    absence,
                    callerAttestsOwnedDiagnosticsReady: false,
                    callerAttestsPageFaultHandlerReady: true));
        NullPageFaultProbePlanResult noHandler =
            NullPageFaultProbePlan.Plan(
                NullPageFaultProbeRequest.CreateDisabled(
                    absence,
                    callerAttestsOwnedDiagnosticsReady: true,
                    callerAttestsPageFaultHandlerReady: false));
        NullPageFaultProbePlanResult defaultRequest =
            NullPageFaultProbePlan.Plan(default);

        PageZeroMappingPolicyResult intentOnly =
            ValidateIntent(0, 0x1000, present: false);
        NullPageFaultProbePlanResult intentIsNotProof =
            NullPageFaultProbePlan.Plan(
                NullPageFaultProbeRequest.CreateDisabled(
                    intentOnly,
                    true,
                    true));

        NullPageFaultProbeRequest hostileFlag =
            MutateRequestByte(
                disabled,
                RequestOwnedDiagnosticsReadyOffset,
                2);
        NullPageFaultProbeRequest hostileReserved =
            MutateRequestByte(
                disabled,
                RequestReservedOffset,
                1);
        NullPageFaultProbeRequest hostileSize =
            MutateRequestByte(
                disabled,
                RequestStructureSizeOffset,
                0);
        NullPageFaultProbeRequest hostileVersion =
            MutateRequestByte(
                disabled,
                RequestStructureVersionOffset,
                2);
        NullPageFaultProbePlanResult[] hostileRequests =
        [
            NullPageFaultProbePlan.Plan(hostileFlag),
            NullPageFaultProbePlan.Plan(hostileReserved),
            NullPageFaultProbePlan.Plan(hostileSize),
            NullPageFaultProbePlan.Plan(hostileVersion)
        ];

        PageZeroMappingPolicyResult hostileOperation =
            MutatePolicyByte(
                absence,
                PolicyOperationOffset,
                (byte)PageZeroMappingPolicyOperation.MappingIntent);
        PageZeroMappingPolicyResult hostileStatus =
            MutatePolicyByte(
                absence,
                PolicyStatusOffset,
                (byte)PageZeroMappingPolicyStatus.PageZeroPresent);
        PageZeroMappingPolicyResult hostileLevel =
            MutatePolicyByte(
                absence,
                PolicyAbsentAtLevelOffset,
                (byte)X64PageTableLevel.Pml4e);
        PageZeroMappingPolicyResult hostileReservedPolicy =
            MutatePolicyByte(
                absence,
                PolicyReserved0Offset,
                1);
        PageZeroMappingPolicyResult hostileIntentSize =
            MutatePolicyByte(
                absence,
                PolicyIntentStructureSizeOffset,
                1);
        PageZeroMappingPolicyResult hostileIntentVersion =
            MutatePolicyByte(
                absence,
                PolicyIntentStructureVersionOffset,
                1);
        PageZeroMappingPolicyResult hostileIntentInitialized =
            MutatePolicyByte(
                absence,
                PolicyIntentInitializedOffset,
                1);
        PageZeroMappingPolicyResult hostilePageCount =
            MutatePolicyUlong(
                absence,
                PolicyPageImageCountOffset,
                3);
        PageZeroMappingPolicyResult hostileTerminalPage =
            MutatePolicyUlong(
                absence,
                PolicyTerminalPageIndexOffset,
                1);
        PageZeroMappingPolicyResult hostileTerminalEntry =
            MutatePolicyUlong(
                absence,
                PolicyTerminalEntryIndexOffset,
                1);
        PageZeroMappingPolicyResult hostileInspectedCount =
            MutatePolicyUlong(
                absence,
                PolicyInspectedEntryCountOffset,
                1);
        PageZeroMappingPolicyResult hostileRawEntry =
            MutatePolicyUlong(
                absence,
                PolicyTerminalRawEntryOffset,
                1);
        PageZeroMappingPolicyResult[] hostileEvidence =
        [
            hostileOperation,
            hostileStatus,
            hostileLevel,
            hostileReservedPolicy,
            hostileIntentSize,
            hostileIntentVersion,
            hostileIntentInitialized,
            hostilePageCount,
            hostileTerminalPage,
            hostileTerminalEntry,
            hostileInspectedCount,
            hostileRawEntry
        ];
        bool hostileMutationsTargetExpectedFields =
            ReadRequestByte(
                hostileFlag,
                RequestOwnedDiagnosticsReadyOffset) == 2
            && !hostileFlag.IsInitialized
            && !hostileFlag.OwnedDiagnosticsReady
            && hostileFlag.PageFaultHandlerReady
            && ReadRequestByte(
                hostileReserved,
                RequestReservedOffset) == 1
            && !hostileReserved.IsInitialized
            && hostileReserved.OwnedDiagnosticsReady
            && ReadRequestByte(
                hostileSize,
                RequestStructureSizeOffset) == 0
            && !hostileSize.IsInitialized
            && hostileSize.OwnedDiagnosticsReady
            && ReadRequestByte(
                hostileVersion,
                RequestStructureVersionOffset) == 2
            && !hostileVersion.IsInitialized
            && hostileVersion.OwnedDiagnosticsReady
            && hostileOperation.Operation ==
                PageZeroMappingPolicyOperation.MappingIntent
            && hostileOperation.Status ==
                PageZeroMappingPolicyStatus.Success
            && hostileStatus.Status ==
                PageZeroMappingPolicyStatus.PageZeroPresent
            && hostileStatus.Operation ==
                PageZeroMappingPolicyOperation.TableImageAudit
            && hostileLevel.AbsentAtLevel ==
                X64PageTableLevel.Pml4e
            && ReadPolicyByte(
                hostileReservedPolicy,
                PolicyReserved0Offset) == 1
            && ReadPolicyByte(
                hostileIntentSize,
                PolicyIntentStructureSizeOffset) == 1
            && ReadPolicyByte(
                hostileIntentVersion,
                PolicyIntentStructureVersionOffset) == 1
            && ReadPolicyByte(
                hostileIntentInitialized,
                PolicyIntentInitializedOffset) == 1
            && !hostileIntentInitialized.Intent.IsInitialized
            && hostilePageCount.PageImageCount == 3
            && hostileTerminalPage.TerminalPageIndex == 1
            && hostileTerminalEntry.TerminalEntryIndex == 1
            && hostileInspectedCount.InspectedEntryCount == 1
            && hostileRawEntry.TerminalRawEntry == 1;
        bool hostileEvidenceRejected = true;
        foreach (PageZeroMappingPolicyResult evidence in hostileEvidence)
        {
            NullPageFaultProbePlanResult rejected =
                NullPageFaultProbePlan.Plan(
                    NullPageFaultProbeRequest.CreateDisabled(
                        evidence,
                        true,
                        true));
            hostileEvidenceRejected &=
                !evidence.ProvesPageZeroAbsentInSuppliedImage
                && !evidence.Succeeded
                && rejected.Status ==
                    NullPageFaultProbePlanStatus
                        .PageZeroPolicyUnavailable
                && !rejected.Succeeded;
        }

        NullPageFaultProbePlanResult[] ordinaryFailures =
        [
            noPolicy,
            defaultOn,
            attempted,
            requested,
            noDiagnostics,
            noHandler,
            defaultRequest,
            intentIsNotProof
        ];
        bool failuresStayFailed = true;
        foreach (NullPageFaultProbePlanResult failure
            in ordinaryFailures)
        {
            failuresStayFailed &= !failure.Succeeded;
        }

        bool hostileRequestsRejected = true;
        foreach (NullPageFaultProbePlanResult hostile
            in hostileRequests)
        {
            hostileRequestsRejected &=
                hostile.Status ==
                    NullPageFaultProbePlanStatus.InvalidRequestState
                && !hostile.Succeeded;
        }

        return ready.Succeeded
            && ready.Status ==
                NullPageFaultProbePlanStatus.ReadyDisabled
            && ready.AccessKind == NullPageFaultAccessKind.Read
            && ready.HasHardwareErrorCode
            && ready.ExpectedPrivilegeLevel == 0
            && !ready.IsEnabled
            && ready.ExpectedVector ==
                (ushort)FaultVectorCatalog.PageFault
            && ready.ExpectedErrorCode == 0
            && ready.CheckedErrorCodeMask == 0x1f
            && ready.ExpectedCr2Raw == 0
            && !NullPageFaultProbePlan.PerformsLiveFault
            && !NullPageFaultProbePlan.DereferencesPageZero
            && !NullPageFaultProbePlan.ExecutesProbe
            && noPolicy.Status ==
                NullPageFaultProbePlanStatus
                    .PageZeroPolicyUnavailable
            && intentIsNotProof.Status ==
                NullPageFaultProbePlanStatus
                    .PageZeroPolicyUnavailable
            && defaultOn.Status ==
                NullPageFaultProbePlanStatus
                    .EnabledByDefaultRejected
            && attempted.Status ==
                NullPageFaultProbePlanStatus
                    .ExecutionAttemptedRejected
            && requested.Status ==
                NullPageFaultProbePlanStatus
                    .ExecutionRequestedRejected
            && noDiagnostics.Status ==
                NullPageFaultProbePlanStatus
                    .OwnedDiagnosticsUnavailable
            && noHandler.Status ==
                NullPageFaultProbePlanStatus
                    .PageFaultHandlerUnavailable
            && defaultRequest.Status ==
                NullPageFaultProbePlanStatus.InvalidRequestState
            && !default(NullPageFaultProbePlanResult).Succeeded
            && hostileRequestsRejected
            && hostileMutationsTargetExpectedFields
            && hostileEvidenceRejected
            && failuresStayFailed;
    }

    private static bool TestSourceAndNoStdLibBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string policyPath = Path.Combine(
            root,
            @"src\OS.Kernel\Memory\PageZeroMappingPolicy.cs");
        string planPath = Path.Combine(
            root,
            @"src\OS.Kernel\Diagnostics\NullPageFaultProbePlan.cs");
        string policyRaw = File.ReadAllText(policyPath);
        string planRaw = File.ReadAllText(planPath);
        string policy = SanitizeCSharpSource(
            policyRaw,
            out bool policyHasStringLiteral,
            out bool policyHasInterpolatedString);
        string plan = SanitizeCSharpSource(
            planRaw,
            out bool planHasStringLiteral,
            out bool planHasInterpolatedString);
        if (policyHasStringLiteral ||
            policyHasInterpolatedString ||
            planHasStringLiteral ||
            planHasInterpolatedString)
        {
            Console.WriteLine(
                "M01_015_SOURCE_DIAGNOSTIC managed_literal");
            return false;
        }

        string compactPolicy = RemoveWhitespace(policy);
        string compactPlan = RemoveWhitespace(plan);

        string[] commonForbidden =
        [
            "System.",
            "Span<",
            "ReadOnlySpan<",
            "throw",
            "catch",
            "finally",
            " string ",
            " object ",
            " dynamic ",
            "typeof",
            "Reflection",
            "Func<",
            "Action<",
            "delegate",
            "stackalloc",
            "Runtime" + "Import",
            "Runtime" + "Export",
            "Dll" + "Import",
            "Library" + "Import",
            "Unmanaged" + "CallersOnly",
            "Native" + "Library",
            "Marshal",
            "GetDelegateFor" + "FunctionPointer",
            "Dynamic" + "Invoke",
            ".Invoke(",
            "Efi" + "Entry",
            "Boot" + "Sequence",
            "Probes."
        ];
        foreach (string token in commonForbidden)
        {
            if (policy.Contains(token, StringComparison.Ordinal) ||
                plan.Contains(token, StringComparison.Ordinal) ||
                compactPolicy.Contains(
                    RemoveWhitespace(token),
                    StringComparison.Ordinal) ||
                compactPlan.Contains(
                    RemoveWhitespace(token),
                    StringComparison.Ordinal))
            {
                Console.WriteLine(
                    "M01_015_SOURCE_DIAGNOSTIC token="
                    + Normalize(token));
                return false;
            }
        }

        string[] planForbidden =
        [
            "unsafe",
            "Unsafe.",
            "MemoryMarshal",
            "NativeMemory",
            "Volatile.",
            "Interlocked.",
            "Buffer.MemoryCopy",
            "FaultProbe" + "StubPlan",
            "FaultProbe" + "StubKind",
            "Null" + "Write",
            "Executable" + "Stub",
            "delegate" + "*",
            "Function" + "Pointer",
            "calli",
            "byte[",
            "new byte",
            "0xC6",
            "0xc6",
            "C6 04 " + "25",
            "->"
        ];
        foreach (string token in planForbidden)
        {
            if (plan.Contains(token, StringComparison.Ordinal) ||
                compactPlan.Contains(
                    RemoveWhitespace(token),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        string testSource = File.ReadAllText(
            Path.Combine(
                root,
                @"tests\OS.HostTests\PageZeroMappingPolicyTests.cs"));
        if (ContainsUnsafeNullPointerUse(testSource))
            return false;

        return policy.Contains(
                "PerformsLiveMutation = false",
                StringComparison.Ordinal)
            && policy.Contains(
                "WritesTableImage = false",
                StringComparison.Ordinal)
            && plan.Contains(
                "DereferencesPageZero = false",
                StringComparison.Ordinal)
            && plan.Contains(
                "ExecutesProbe = false",
                StringComparison.Ordinal)
            && BuildNoStdLibClosure(root);
    }

    private static bool TestEfiAndMarkerBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string policyPath = Path.GetFullPath(
            Path.Combine(
                root,
                @"src\OS.Kernel\Memory\PageZeroMappingPolicy.cs"));
        string planPath = Path.GetFullPath(
            Path.Combine(
                root,
                @"src\OS.Kernel\Diagnostics\NullPageFaultProbePlan.cs"));
        string testPath = Path.GetFullPath(
            Path.Combine(
                root,
                @"tests\OS.HostTests\PageZeroMappingPolicyTests.cs"));
        string efiProject = Path.Combine(
            root,
            @"src\OS\OS.csproj");

        if (!BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Memory\PageZeroMappingPolicy.cs",
                @"Memory\PageZeroMappingPolicy.cs") ||
            !EvaluatedEfiClosure(
                root,
                efiProject,
                "SingleMinimalApi",
                [policyPath],
                [planPath, testPath]) ||
            !EvaluatedEfiClosure(
                root,
                efiProject,
                "MultiMinimalApi",
                [policyPath],
                [planPath, testPath]))
        {
            return false;
        }

        int readyOwners = 0;
        string[] roots =
        [
            "src",
            "std",
            "apps",
            "tools",
            "tests",
            "scripts",
            Path.Combine(".gitea", "workflows")
        ];
        foreach (string relativeRoot in roots)
        {
            string scanRoot = Path.Combine(root, relativeRoot);
            if (!Directory.Exists(scanRoot))
                continue;

            foreach (string file in Directory.EnumerateFiles(
                scanRoot,
                "*",
                SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/bin/", StringComparison.Ordinal) ||
                    normalized.Contains("/obj/", StringComparison.Ordinal) ||
                    normalized.Contains(
                        "/artifacts/",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string content;
                try
                {
                    content = File.ReadAllText(file);
                }
                catch
                {
                    Console.WriteLine(
                        "M01_015_MARKER_DIAGNOSTIC unreadable="
                        + Path.GetFileName(file));
                    return false;
                }

                int count = CountOccurrences(content, ReadyMarker);
                if (count != 0)
                {
                    if (!Path.GetFullPath(file).Equals(
                            testPath,
                            StringComparison.OrdinalIgnoreCase) ||
                        count != 1)
                    {
                        return false;
                    }

                    readyOwners++;
                }
            }
        }

        string owner = File.ReadAllText(testPath);
        return readyOwners == 1
            && !owner.Contains(
                "OS_" + "BOOT_OK",
                StringComparison.Ordinal)
            && !owner.Contains(
                "OS_" + "SMOKE_PASS",
                StringComparison.Ordinal)
            && !owner.Contains(
                "OS_" + "SMOKE_FAIL",
                StringComparison.Ordinal);
    }

    private static PageZeroMappingPolicyResult ValidateIntent(
        ulong rawStart,
        ulong byteLength,
        bool present)
    {
        return PageZeroMappingPolicy.ValidateMappingIntent(
            PageZeroMappingIntent.CreateAtRawMappingBoundary(
                rawStart,
                byteLength,
                present));
    }

    private static PageZeroMappingPolicyResult
        ValidateHostilePresentFlag()
    {
        PageZeroMappingIntent intent =
            PageZeroMappingIntent.CreateAtRawMappingBoundary(
                0x1000,
                0x1000,
                present: false);
        Span<PageZeroMappingIntent> one =
            MemoryMarshal.CreateSpan(ref intent, 1);
        Span<byte> bytes = MemoryMarshal.AsBytes(one);
        bytes[7] = 2;
        return PageZeroMappingPolicy.ValidateMappingIntent(intent);
    }

    private static NullPageFaultProbeRequest MutateRequestByte(
        NullPageFaultProbeRequest request,
        int byteOffset,
        byte value)
    {
        Span<NullPageFaultProbeRequest> one =
            MemoryMarshal.CreateSpan(ref request, 1);
        Span<byte> bytes = MemoryMarshal.AsBytes(one);
        bytes[byteOffset] = value;
        return request;
    }

    private static PageZeroMappingPolicyResult MutatePolicyByte(
        PageZeroMappingPolicyResult result,
        int byteOffset,
        byte value)
    {
        Span<PageZeroMappingPolicyResult> one =
            MemoryMarshal.CreateSpan(ref result, 1);
        Span<byte> bytes = MemoryMarshal.AsBytes(one);
        bytes[byteOffset] = value;
        return result;
    }

    private static PageZeroMappingPolicyResult MutatePolicyUlong(
        PageZeroMappingPolicyResult result,
        int byteOffset,
        ulong value)
    {
        Span<PageZeroMappingPolicyResult> one =
            MemoryMarshal.CreateSpan(ref result, 1);
        Span<byte> bytes = MemoryMarshal.AsBytes(one);
        for (int index = 0; index < 8; index++)
        {
            bytes[byteOffset + index] =
                (byte)(value >> (index * 8));
        }

        return result;
    }

    private static byte ReadRequestByte(
        NullPageFaultProbeRequest request,
        int byteOffset)
    {
        ReadOnlySpan<NullPageFaultProbeRequest> one =
            MemoryMarshal.CreateReadOnlySpan(ref request, 1);
        return MemoryMarshal.AsBytes(one)[byteOffset];
    }

    private static byte ReadPolicyByte(
        PageZeroMappingPolicyResult result,
        int byteOffset)
    {
        ReadOnlySpan<PageZeroMappingPolicyResult> one =
            MemoryMarshal.CreateReadOnlySpan(ref result, 1);
        return MemoryMarshal.AsBytes(one)[byteOffset];
    }

    private static bool HasFieldOffset<T>(
        string fieldName,
        int expectedOffset)
        where T : struct
    {
        return Marshal.OffsetOf<T>(fieldName).ToInt64()
            == expectedOffset;
    }

    private static bool IsPresentLeaf(
        PageZeroMappingPolicyResult result,
        ulong expectedPageSize,
        ulong expectedEntryIndex,
        ulong expectedInspected)
    {
        return result.Status ==
                PageZeroMappingPolicyStatus.PageZeroPresent
            && result.Operation ==
                PageZeroMappingPolicyOperation.TableImageAudit
            && result.EntryStatus ==
                X64PageTableEntryStatus.Success
            && result.AbsentAtLevel ==
                X64PageTableLevel.Uninitialized
            && result.TerminalEntryIndex == expectedEntryIndex
            && result.InspectedEntryCount == expectedInspected
            && result.PresentLeafPageSize == expectedPageSize
            && !result.ProvesPageZeroAbsentInSuppliedImage;
    }

    private static bool BuildNoStdLibClosure(string root)
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-015-"
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
                        OS.Kernel.Memory.PageZeroMappingPolicyResult
                            policy =
                                OS.Kernel.Memory
                                    .PageZeroMappingPolicy
                                    .AuditTableImage(
                                        default,
                                        default,
                                        null,
                                        0,
                                        0,
                                        null,
                                        0,
                                        0);
                        OS.Kernel.Diagnostics.NullPageFaultProbeRequest
                            request =
                                OS.Kernel.Diagnostics
                                    .NullPageFaultProbeRequest
                                    .CreateDisabled(
                                        policy,
                                        false,
                                        false);
                        return (byte)OS.Kernel.Diagnostics
                            .NullPageFaultProbePlan
                            .Plan(request).Status;
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
                @"src\OS.Kernel\Memory\PageZeroMappingPolicy.cs",
                @"src\OS.Kernel\Diagnostics\NullPageFaultProbePlan.cs"
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
                    <AssemblyName>CLROS.M01.PageZeroClosure</AssemblyName>
                    <TargetFramework>net10.0</TargetFramework>
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
                "M01.015.NoStdLib.csproj");
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
            start.ArgumentList.Add("--no-dependencies");
            start.ArgumentList.Add("--no-incremental");
            start.ArgumentList.Add("--disable-build-servers");
            start.ArgumentList.Add("-v:minimal");
            start.ArgumentList.Add(
                "-p:RestoreIgnoreFailedSources=true");
            start.ArgumentList.Add("-p:NuGetAudit=false");
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException(
                    "Failed to start M01-015 no-stdlib build.");
            Task<string> stdout =
                process.StandardOutput.ReadToEndAsync();
            Task<string> stderr =
                process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(90_000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            Task.WaitAll(stdout, stderr);
            if (!exited || process.ExitCode != 0)
            {
                Console.WriteLine(
                    "M01_015_NOSTDLIB_DIAGNOSTIC "
                    + NormalizeCompilerOutput(
                        stdout.Result
                        + Environment.NewLine
                        + stderr.Result));
                return false;
            }

            string assemblyPath = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.PageZeroClosure.dll");
            using FileStream stream = File.OpenRead(assemblyPath);
            using PEReader reader = new(stream);
            if (!reader.HasMetadata)
                return false;

            MetadataReader metadata = reader.GetMetadataReader();
            if (metadata.AssemblyReferences.Count != 0 ||
                metadata.GetTableRowCount(TableIndex.ModuleRef) != 0)
            {
                return false;
            }

            foreach (MethodDefinitionHandle handle
                in metadata.MethodDefinitions)
            {
                if ((metadata.GetMethodDefinition(handle).Attributes
                        & MethodAttributes.PinvokeImpl)
                    != 0)
                {
                    return false;
                }
            }

            try
            {
                if (!HasOnlyNoRuntimeTypeDefinitions(metadata))
                    return false;
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine(
                    "M01_015_NOSTDLIB_DIAGNOSTIC "
                    + "invalid_type_metadata");
                return false;
            }

            return HasNoManagedAllocationInstructions(
                reader,
                metadata);
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

    private static bool HasOnlyNoRuntimeTypeDefinitions(
        MetadataReader metadata)
    {
        foreach (TypeDefinitionHandle handle
            in metadata.TypeDefinitions)
        {
            string name = "unknown";
            try
            {
                TypeDefinition definition =
                    metadata.GetTypeDefinition(handle);
                name = metadata.GetString(definition.Name);
                string typeNamespace =
                    metadata.GetString(definition.Namespace);
                if (name == "<Module>" ||
                    IsMetadataValueType(metadata, handle) ||
                    IsStaticMetadataClass(definition.Attributes) ||
                    IsNoStdLibShimReferenceType(
                        typeNamespace,
                        name))
                {
                    continue;
                }

                Console.WriteLine(
                    "M01_015_NOSTDLIB_DIAGNOSTIC managed_type="
                    + Normalize(typeNamespace + "." + name));
                return false;
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine(
                    "M01_015_NOSTDLIB_DIAGNOSTIC "
                    + "invalid_type row="
                    + MetadataTokens.GetRowNumber(handle)
                    + " name="
                    + Normalize(name));
                return false;
            }
        }

        return true;
    }

    private static bool IsStaticMetadataClass(
        TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.Abstract) != 0
            && (attributes & TypeAttributes.Sealed) != 0;
    }

    private static bool IsNoStdLibShimReferenceType(
        string typeNamespace,
        string name)
    {
        if (typeNamespace == "System")
        {
            return name == "Object"
                || name == "ValueType"
                || name == "Enum"
                || name == "Delegate"
                || name == "MulticastDelegate"
                || name == "Attribute"
                || name == "AttributeUsageAttribute";
        }

        return (typeNamespace ==
                    "System.Runtime.CompilerServices"
                && (name == "IsReadOnlyAttribute"
                    || name == "RefSafetyRulesAttribute"))
            || (typeNamespace == "Microsoft.CodeAnalysis"
                && name == "EmbeddedAttribute");
    }

    private static bool IsMetadataValueType(
        MetadataReader metadata,
        TypeDefinitionHandle handle)
    {
        EntityHandle baseType =
            metadata.GetTypeDefinition(handle).BaseType;
        return MetadataTypeNameEquals(
                metadata,
                baseType,
                "System",
                "ValueType")
            || MetadataTypeNameEquals(
                metadata,
                baseType,
                "System",
                "Enum");
    }

    private static bool MetadataTypeNameEquals(
        MetadataReader metadata,
        EntityHandle handle,
        string expectedNamespace,
        string expectedName)
    {
        if (handle.IsNil)
            return false;

        if (handle.Kind == HandleKind.TypeDefinition)
        {
            TypeDefinition definition =
                metadata.GetTypeDefinition(
                    (TypeDefinitionHandle)handle);
            return metadata.GetString(definition.Namespace) ==
                    expectedNamespace
                && metadata.GetString(definition.Name) ==
                    expectedName;
        }

        if (handle.Kind == HandleKind.TypeReference)
        {
            TypeReference reference =
                metadata.GetTypeReference(
                    (TypeReferenceHandle)handle);
            return metadata.GetString(reference.Namespace) ==
                    expectedNamespace
                && metadata.GetString(reference.Name) ==
                    expectedName;
        }

        return false;
    }

    private static bool HasNoManagedAllocationInstructions(
        PEReader reader,
        MetadataReader metadata)
    {
        Dictionary<ushort, OpCode> opCodes =
            BuildOpCodeMap();
        foreach (MethodDefinitionHandle handle
            in metadata.MethodDefinitions)
        {
            try
            {
                MethodDefinition method =
                    metadata.GetMethodDefinition(handle);
                if (method.RelativeVirtualAddress == 0)
                    continue;

                byte[]? il = reader
                    .GetMethodBody(method.RelativeVirtualAddress)
                    .GetILBytes();
                if (il is null)
                    return false;

                int offset = 0;
                while (offset < il.Length)
                {
                    if (!TryReadOpCode(
                            il,
                            ref offset,
                            opCodes,
                            out OpCode opCode))
                    {
                        return false;
                    }

                    if (opCode.Value == OpCodes.Newarr.Value ||
                        opCode.Value == OpCodes.Box.Value ||
                        opCode.Value == OpCodes.Ldstr.Value)
                    {
                        Console.WriteLine(
                            "M01_015_NOSTDLIB_DIAGNOSTIC "
                            + "managed_opcode="
                            + Normalize(opCode.Name ?? "unknown")
                            + " method="
                            + Normalize(metadata.GetString(method.Name)));
                        return false;
                    }

                    if (opCode.Value == OpCodes.Newobj.Value)
                    {
                        if (!TryReadInt32(
                                il,
                                offset,
                                out int constructorToken) ||
                            !ConstructorTargetsValueType(
                                metadata,
                                constructorToken))
                        {
                            Console.WriteLine(
                                "M01_015_NOSTDLIB_DIAGNOSTIC "
                                + "managed_newobj method="
                                + Normalize(
                                    metadata.GetString(method.Name)));
                            return false;
                        }
                    }

                    if (!TrySkipOperand(
                            il,
                            ref offset,
                            opCode.OperandType))
                    {
                        return false;
                    }
                }
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine(
                    "M01_015_NOSTDLIB_DIAGNOSTIC "
                    + "invalid_method_body row="
                    + MetadataTokens.GetRowNumber(handle));
                return false;
            }
        }

        return true;
    }

    private static Dictionary<ushort, OpCode> BuildOpCodeMap()
    {
        Dictionary<ushort, OpCode> result = new();
        foreach (FieldInfo field in typeof(OpCodes).GetFields(
            BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(OpCode) &&
                field.GetValue(null) is OpCode opCode)
            {
                result[unchecked((ushort)opCode.Value)] =
                    opCode;
            }
        }

        return result;
    }

    private static bool TryReadOpCode(
        byte[] il,
        ref int offset,
        Dictionary<ushort, OpCode> opCodes,
        out OpCode opCode)
    {
        opCode = default;
        if ((uint)offset >= (uint)il.Length)
            return false;

        ushort value = il[offset++];
        if (value == 0xfe)
        {
            if ((uint)offset >= (uint)il.Length)
                return false;

            value = (ushort)(0xfe00 | il[offset++]);
        }

        return opCodes.TryGetValue(value, out opCode);
    }

    private static bool TrySkipOperand(
        byte[] il,
        ref int offset,
        OperandType operandType)
    {
        int byteCount;
        switch (operandType)
        {
            case OperandType.InlineNone:
                byteCount = 0;
                break;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                byteCount = 1;
                break;
            case OperandType.InlineVar:
                byteCount = 2;
                break;
            case OperandType.InlineBrTarget:
            case OperandType.InlineField:
            case OperandType.InlineI:
            case OperandType.InlineMethod:
            case OperandType.InlineSig:
            case OperandType.InlineString:
            case OperandType.InlineTok:
            case OperandType.InlineType:
            case OperandType.ShortInlineR:
                byteCount = 4;
                break;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                byteCount = 8;
                break;
            case OperandType.InlineSwitch:
                if (!TryReadInt32(
                        il,
                        offset,
                        out int targetCount) ||
                    targetCount < 0 ||
                    targetCount >
                        (il.Length - offset - 4) / 4)
                {
                    return false;
                }

                byteCount = 4 + targetCount * 4;
                break;
            default:
                return false;
        }

        if (byteCount < 0 ||
            offset > il.Length - byteCount)
        {
            return false;
        }

        offset += byteCount;
        return true;
    }

    private static bool TryReadInt32(
        byte[] bytes,
        int offset,
        out int value)
    {
        value = 0;
        if (offset < 0 ||
            offset > bytes.Length - 4)
        {
            return false;
        }

        value = bytes[offset]
            | bytes[offset + 1] << 8
            | bytes[offset + 2] << 16
            | bytes[offset + 3] << 24;
        return true;
    }

    private static bool ConstructorTargetsValueType(
        MetadataReader metadata,
        int constructorToken)
    {
        EntityHandle constructor;
        try
        {
            constructor =
                MetadataTokens.EntityHandle(constructorToken);
        }
        catch (ArgumentException)
        {
            return false;
        }

        EntityHandle declaringType;
        if (constructor.Kind == HandleKind.MethodDefinition)
        {
            declaringType = metadata
                .GetMethodDefinition(
                    (MethodDefinitionHandle)constructor)
                .GetDeclaringType();
        }
        else if (constructor.Kind ==
            HandleKind.MemberReference)
        {
            declaringType = metadata
                .GetMemberReference(
                    (MemberReferenceHandle)constructor)
                .Parent;
        }
        else
        {
            return false;
        }

        if (declaringType.Kind == HandleKind.TypeDefinition)
        {
            return IsMetadataValueType(
                metadata,
                (TypeDefinitionHandle)declaringType);
        }

        if (declaringType.Kind != HandleKind.TypeReference)
            return false;

        TypeReference reference = metadata.GetTypeReference(
            (TypeReferenceHandle)declaringType);
        string targetNamespace =
            metadata.GetString(reference.Namespace);
        string targetName = metadata.GetString(reference.Name);
        foreach (TypeDefinitionHandle handle
            in metadata.TypeDefinitions)
        {
            TypeDefinition definition =
                metadata.GetTypeDefinition(handle);
            if (metadata.GetString(definition.Namespace) ==
                    targetNamespace &&
                metadata.GetString(definition.Name) ==
                    targetName)
            {
                return IsMetadataValueType(metadata, handle);
            }
        }

        return false;
    }

    private static bool EvaluatedEfiClosure(
        string repositoryRoot,
        string projectPath,
        string workloadProfile,
        IReadOnlyList<string> requiredPaths,
        IReadOnlyList<string> forbiddenPaths)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repositoryRoot
        };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add(
            "-getItem:Compile,ProjectReference,Reference");
        start.ArgumentList.Add("-p:Configuration=Release");
        start.ArgumentList.Add(
            "-p:OSWorkloadProfile=" + workloadProfile);
        start.ArgumentList.Add(
            "-p:RestoreIgnoreFailedSources=true");
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException(
                "Failed to evaluate EFI closure.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        bool exited = process.WaitForExit(60_000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        if (!exited || process.ExitCode != 0)
        {
            Console.WriteLine(
                "M01_015_EFI_EVAL_DIAGNOSTIC profile="
                + workloadProfile
                + " "
                + NormalizeCompilerOutput(stdout + stderr));
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement items =
                document.RootElement.GetProperty("Items");
            JsonElement compile = items.GetProperty("Compile");
            JsonElement projectReferences =
                items.GetProperty("ProjectReference");
            JsonElement references = items.GetProperty("Reference");
            if (compile.ValueKind != JsonValueKind.Array ||
                compile.GetArrayLength() == 0 ||
                projectReferences.GetArrayLength() != 0 ||
                references.GetArrayLength() != 0)
            {
                return false;
            }

            bool efiEntry = false;
            bool minimalRuntime = false;
            HashSet<string> required = new(
                requiredPaths.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> foundRequired =
                new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement item in compile.EnumerateArray())
            {
                string fullPath =
                    item.GetProperty("FullPath").GetString() ?? "";
                if (!Path.IsPathFullyQualified(fullPath))
                    return false;

                string canonicalPath = Path.GetFullPath(fullPath);
                efiEntry |= fullPath.EndsWith(
                    Path.Combine("Boot", "EfiEntry.cs"),
                    StringComparison.OrdinalIgnoreCase);
                minimalRuntime |= fullPath.EndsWith(
                    Path.Combine("Runtime", "MinimalRuntime.cs"),
                    StringComparison.OrdinalIgnoreCase);
                if (required.Contains(canonicalPath))
                    foundRequired.Add(canonicalPath);

                foreach (string forbiddenPath in forbiddenPaths)
                {
                    if (canonicalPath.Equals(
                            Path.GetFullPath(forbiddenPath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return efiEntry
                && minimalRuntime
                && foundRequired.Count == required.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "M01_015_EFI_EVAL_DIAGNOSTIC profile="
                + workloadProfile
                + " "
                + ex.GetType().Name);
            return false;
        }
    }

    private static string SanitizeCSharpSource(
        string source,
        out bool hasStringLiteral,
        out bool hasInterpolatedString)
    {
        StringBuilder result = new(source.Length);
        hasStringLiteral = false;
        hasInterpolatedString = false;
        int index = 0;
        while (index < source.Length)
        {
            char current = source[index];
            char next = index + 1 < source.Length
                ? source[index + 1]
                : '\0';
            if (current == '/' && next == '/')
            {
                int end = index + 2;
                while (end < source.Length &&
                    source[end] != '\r' &&
                    source[end] != '\n')
                {
                    end++;
                }

                AppendSanitizedRange(result, source, index, end);
                index = end;
                continue;
            }

            if (current == '/' && next == '*')
            {
                int end = index + 2;
                while (end + 1 < source.Length &&
                    !(source[end] == '*' &&
                        source[end + 1] == '/'))
                {
                    end++;
                }

                end = end + 1 < source.Length
                    ? end + 2
                    : source.Length;
                AppendSanitizedRange(result, source, index, end);
                index = end;
                continue;
            }

            if (current == '\'')
            {
                int end = FindRegularLiteralEnd(
                    source,
                    index,
                    '\'');
                AppendSanitizedRange(result, source, index, end);
                index = end;
                continue;
            }

            if (TryFindStringLiteralStart(
                    source,
                    index,
                    out int quoteIndex,
                    out bool verbatim,
                    out bool interpolated))
            {
                hasStringLiteral = true;
                hasInterpolatedString |= interpolated;
                int quoteCount = CountConsecutive(
                    source,
                    quoteIndex,
                    '"');
                int end = quoteCount >= 3
                    ? FindRawStringEnd(
                        source,
                        quoteIndex,
                        quoteCount)
                    : verbatim
                        ? FindVerbatimStringEnd(
                            source,
                            quoteIndex)
                        : FindRegularLiteralEnd(
                            source,
                            quoteIndex,
                            '"');
                AppendSanitizedRange(result, source, index, end);
                index = end;
                continue;
            }

            result.Append(current);
            index++;
        }

        return result.ToString();
    }

    private static bool TryFindStringLiteralStart(
        string source,
        int index,
        out int quoteIndex,
        out bool verbatim,
        out bool interpolated)
    {
        quoteIndex = -1;
        verbatim = false;
        interpolated = false;
        if (source[index] == '"')
        {
            quoteIndex = index;
            return true;
        }

        if (source[index] == '@')
        {
            if (index + 1 < source.Length &&
                source[index + 1] == '"')
            {
                quoteIndex = index + 1;
                verbatim = true;
                return true;
            }

            if (index + 2 < source.Length &&
                source[index + 1] == '$' &&
                source[index + 2] == '"')
            {
                quoteIndex = index + 2;
                verbatim = true;
                interpolated = true;
                return true;
            }

            return false;
        }

        if (source[index] != '$')
            return false;

        int cursor = index;
        while (cursor < source.Length &&
            source[cursor] == '$')
        {
            cursor++;
        }

        if (cursor < source.Length &&
            source[cursor] == '@')
        {
            verbatim = true;
            cursor++;
        }

        if (cursor >= source.Length ||
            source[cursor] != '"')
        {
            return false;
        }

        quoteIndex = cursor;
        interpolated = true;
        return true;
    }

    private static int FindRegularLiteralEnd(
        string source,
        int delimiterIndex,
        char delimiter)
    {
        int index = delimiterIndex + 1;
        while (index < source.Length)
        {
            if (source[index] == '\\')
            {
                index = index + 1 < source.Length
                    ? index + 2
                    : source.Length;
                continue;
            }

            if (source[index] == delimiter)
                return index + 1;

            index++;
        }

        return source.Length;
    }

    private static int FindVerbatimStringEnd(
        string source,
        int quoteIndex)
    {
        int index = quoteIndex + 1;
        while (index < source.Length)
        {
            if (source[index] != '"')
            {
                index++;
                continue;
            }

            if (index + 1 < source.Length &&
                source[index + 1] == '"')
            {
                index += 2;
                continue;
            }

            return index + 1;
        }

        return source.Length;
    }

    private static int FindRawStringEnd(
        string source,
        int quoteIndex,
        int quoteCount)
    {
        int index = quoteIndex + quoteCount;
        while (index < source.Length)
        {
            if (source[index] != '"')
            {
                index++;
                continue;
            }

            int runLength = CountConsecutive(
                source,
                index,
                '"');
            if (runLength >= quoteCount)
                return index + quoteCount;

            index += runLength;
        }

        return source.Length;
    }

    private static int CountConsecutive(
        string source,
        int index,
        char value)
    {
        int count = 0;
        while (index + count < source.Length &&
            source[index + count] == value)
        {
            count++;
        }

        return count;
    }

    private static void AppendSanitizedRange(
        StringBuilder destination,
        string source,
        int start,
        int end)
    {
        for (int index = start; index < end; index++)
        {
            char value = source[index];
            destination.Append(
                value == '\r' || value == '\n'
                    ? value
                    : ' ');
        }
    }

    private static string RemoveWhitespace(string source)
    {
        StringBuilder result = new(source.Length);
        foreach (char value in source)
        {
            if (!char.IsWhiteSpace(value))
                result.Append(value);
        }

        return result.ToString();
    }

    private static bool ContainsUnsafeNullPointerUse(string source)
    {
        string code = SanitizeCSharpSource(
            source,
            out _,
            out bool hasInterpolatedString);
        if (hasInterpolatedString)
            return true;

        const RegexOptions options =
            RegexOptions.CultureInvariant |
            RegexOptions.Multiline;
        const string typeName =
            @"(?:global\s*::\s*)?[A-Za-z_]\w*" +
            @"(?:\s*(?:\.|::)\s*[A-Za-z_]\w*)*";
        const string zeroValue =
            @"(?:\(\s*)*(?:0(?:[uUlL]*)|null|default" +
            @"(?:\s*\([^)]*\))?|(?:IntPtr|UIntPtr)" +
            @"\s*\.\s*Zero)(?:\s*\))*";
        string zeroPointerCast =
            @"\(\s*" + typeName +
            @"\s*\*+\s*\)\s*" + zeroValue;
        string defaultPointer =
            @"\bdefault\s*\(\s*" + typeName +
            @"\s*\*+\s*\)";
        if (Regex.IsMatch(code, zeroPointerCast, options) ||
            Regex.IsMatch(code, defaultPointer, options) ||
            Regex.IsMatch(
                code,
                @"\bUnsafe\s*\.\s*(?!SizeOf\s*<)",
                options) ||
            Regex.IsMatch(
                code,
                @"\bMarshal\s*\.\s*(?!OffsetOf\s*<)",
                options) ||
            Regex.IsMatch(code, @"->", options) ||
            Regex.IsMatch(
                code,
                @"(?:^|[({\[=,:;!?&|+\-/])\s*\*+" +
                @"\s*(?:\(\s*)*[A-Za-z_]\w*",
                options) ||
            Regex.IsMatch(
                code,
                @"\breturn\s+\*+\s*(?:\(\s*)*" +
                @"[A-Za-z_]\w*",
                options))
        {
            return true;
        }

        string pointerDeclaration =
            @"(?:^|[;{}(,])\s*" + typeName +
            @"\s*\*+\s*(?<name>[A-Za-z_]\w*)\b";
        foreach (Match match in Regex.Matches(
            code,
            pointerDeclaration,
            options))
        {
            string name = Regex.Escape(
                match.Groups["name"].Value);
            if (Regex.IsMatch(
                    code,
                    @"\b" + name +
                    @"\s*=\s*" + zeroValue,
                    options))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int start = 0;
        while (true)
        {
            int index = source.IndexOf(
                value,
                start,
                StringComparison.Ordinal);
            if (index < 0)
                return count;

            count++;
            start = index + value.Length;
        }
    }

    private static int CountDifferences(
        ulong[] first,
        ulong[] second)
    {
        if (first.Length != second.Length)
            return int.MaxValue;

        int differences = 0;
        for (int index = 0; index < first.Length; index++)
        {
            if (first[index] != second[index])
                differences++;
        }

        return differences;
    }

    private static int CountDifferences(
        X64PageTableImagePage[] first,
        X64PageTableImagePage[] second)
    {
        if (first.Length != second.Length)
            return int.MaxValue;

        ReadOnlySpan<byte> left =
            MemoryMarshal.AsBytes(first.AsSpan());
        ReadOnlySpan<byte> right =
            MemoryMarshal.AsBytes(second.AsSpan());
        int differences = 0;
        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
                differences++;
        }

        return differences;
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
            .Replace('-', '_')
            .Replace('#', '_');
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

internal static class M01015TestData
{
    private const ulong PatFixture = 0x0006_0501_0007_0406;

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
        {
            throw new InvalidOperationException(
                "M01-015 PAT fixture invalid: " + status);
        }

        return X64PageTableEncodingPreconditions.CreateObserved(
            physicalAddressWidth: 48,
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
        {
            throw new InvalidOperationException(
                "M01-015 physical fixture invalid: " + status);
        }

        return address;
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
                "M01-015 table pointer fixture invalid: "
                + result.Status);
        }

        return result.Entry.RawValue;
    }

    internal static X64PageTableImagePage Page(
        ulong physical,
        ulong entryStartIndex)
    {
        X64PageMapStatus status =
            X64PageTableImagePage.TryCreate(
                Physical(physical),
                entryStartIndex,
                out X64PageTableImagePage page);
        if (status != X64PageMapStatus.Success)
        {
            throw new InvalidOperationException(
                "M01-015 page fixture invalid: " + status);
        }

        return page;
    }
}

internal sealed unsafe class M01015PageZeroFixture : IDisposable
{
    internal X64PageTableImagePage[] Pages { get; }

    internal ulong[] Entries { get; }

    internal X64PageTableEncodingPreconditions Context { get; }

    internal PhysicalAddress Root { get; }

    internal M01015PageZeroFixture()
    {
        Context = M01015TestData.Context();
        Root = M01015TestData.Physical(0x0010_0000);
        Pages = new X64PageTableImagePage[4];
        Entries = new ulong[2048];

        for (int index = 0; index < Pages.Length; index++)
        {
            X64PageMapStatus status =
                X64PageTableImagePage.TryCreate(
                    M01015TestData.Physical(
                        0x0010_0000 + (ulong)index * 0x1000),
                    (ulong)index * 512,
                    out Pages[index]);
            if (status != X64PageMapStatus.Success)
            {
                throw new InvalidOperationException(
                    "Page fixture invalid: " + status);
            }
        }
    }

    internal void LinkThrough(int pointerCount)
    {
        Array.Clear(Entries);
        if (pointerCount >= 1)
        {
            Entries[0] = M01015TestData.EncodeTablePointer(
                X64PageTableLevel.Pml4e,
                Pages[1].PhysicalStart,
                Context);
        }

        if (pointerCount >= 2)
        {
            Entries[512] = M01015TestData.EncodeTablePointer(
                X64PageTableLevel.Pdpte,
                Pages[2].PhysicalStart,
                Context);
        }

        if (pointerCount >= 3)
        {
            Entries[1024] = M01015TestData.EncodeTablePointer(
                X64PageTableLevel.Pde,
                Pages[3].PhysicalStart,
                Context);
        }
    }

    internal ulong EncodeTablePointer(
        X64PageTableLevel level,
        ulong physical)
    {
        return M01015TestData.EncodeTablePointer(
            level,
            M01015TestData.Physical(physical),
            Context);
    }

    internal ulong EncodeLeaf(
        X64PageTableLevel level,
        ulong physical)
    {
        X64PageTableEntryResult result =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    level,
                    X64PageTableEntryKind.Leaf,
                    M01015TestData.Physical(physical),
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                Context);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Leaf fixture invalid: " + result.Status);
        }

        return result.Entry.RawValue;
    }

    internal PageZeroMappingPolicyResult Audit(
        PhysicalAddress root = default)
    {
        if (!root.IsValid)
            root = Root;

        fixed (X64PageTableImagePage* pages = Pages)
        fixed (ulong* entries = Entries)
        {
            return PageZeroMappingPolicy.AuditTableImage(
                root,
                Context,
                pages,
                (ulong)Pages.Length,
                (ulong)Pages.Length,
                entries,
                (ulong)Entries.Length,
                (ulong)Entries.Length);
        }
    }

    internal X64PageMapResult Apply(X64PageMapRequest request)
    {
        fixed (X64PageTableImagePage* pages = Pages)
        fixed (ulong* entries = Entries)
        {
            return X64PageMap.Apply(
                request,
                pages,
                (ulong)Pages.Length,
                (ulong)Pages.Length,
                entries,
                (ulong)Entries.Length,
                (ulong)Entries.Length,
                null,
                0,
                0);
        }
    }

    public void Dispose()
    {
    }
}
