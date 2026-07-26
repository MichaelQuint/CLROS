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
using OS.Kernel.Memory;

internal static class BootReservationSubtractorTests
{
    private const string IssueTestName = "m01.007";
    private const string ReadyMarker = "CLROS_M01_007_READY";
    private const ulong PageSize = 4096;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run("m01.007 01 statuses and layouts are stable unmanaged pods", TestStableStatusesAndLayouts, ref failures);
        Run("m01.007 02 defaults fail closed", TestDefaultsFailClosed, ref failures);
        Run("m01.007 03 source range factory accepts aligned nonwrapping ranges", TestRangeFactorySuccess, ref failures);
        Run("m01.007 04 source range factory rejects malformed inputs", TestRangeFactoryFailures, ref failures);
        Run("m01.007 05 zero reservation identity preserves exact values", TestIdentity, ref failures);
        Run("m01.007 06 head subtraction is exact", TestHeadSubtraction, ref failures);
        Run("m01.007 07 tail subtraction is exact", TestTailSubtraction, ref failures);
        Run("m01.007 08 middle subtraction splits exactly", TestMiddleSplit, ref failures);
        Run("m01.007 09 full cover permits null zero capacity output", TestFullCover, ref failures);
        Run("m01.007 10 multiple ranges preserve order provenance and totals", TestMultipleRanges, ref failures);
        Run("m01.007 11 adjacent source ranges remain separate", TestAdjacentSourcesRemainSeparate, ref failures);
        Run("m01.007 12 adjacent reservations emit no zero fragment", TestAdjacentReservations, ref failures);
        Run("m01.007 13 malformed unordered and overlapping sources fail", TestInvalidSources, ref failures);
        Run("m01.007 14 malformed unordered and overlapping reservations fail", TestInvalidReservations, ref failures);
        Run("m01.007 15 outside gap spanning and cross source reservations fail", TestReservationsOutsideOneSource, ref failures);
        Run("m01.007 16 pointer count capacity span and overflow preflight is bounded", TestPointerCountCapacityAndSpanPreflight, ref failures);
        Run("m01.007 17 all caller table aliases are rejected", TestPairwiseTableOverlap, ref failures);
        Run("m01.007 18 insufficient output reports exact capacity atomically", TestInsufficientDestination, ref failures);
        Run("m01.007 19 every reachable post read failure preserves destination", TestPostReadFailureAtomicity, ref failures);
        Run("m01.007 20 success mutates only the exact destination prefix", TestSuccessPrefixAndSentinels, ref failures);
        Run("m01.007 21 execution is deterministic and allocation free", TestDeterminismAndAllocation, ref failures);
        Run("m01.007 22 forged nominal success evidence fails closed", TestForgedSuccessEvidence, ref failures);
        Run("m01.007 23 production and integration source firewalls hold", TestSourceFirewalls, ref failures);
        Run("m01.007 24 exact sources compile under nostdlib efi", TestNoStdLibEfiCompileClosure, ref failures);
        Run("m01.007 25 readiness marker is unique and host only", TestMarkerBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-007 boot reservation subtractor host tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS " + IssueTestName);
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
            Console.WriteLine(
                "TEST_FAIL "
                + name
                + " "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static bool TestStableStatusesAndLayouts()
    {
        (string Name, byte Value)[] sourceStatuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("InvalidSourceRangeIndex", 2),
            ("ReservedBitsSet", 3),
            ("InvalidPhysicalStart", 4),
            ("MisalignedPhysicalStart", 5),
            ("InvalidPageCount", 6),
            ("PageByteCountOverflow", 7),
            ("ByteLengthMismatch", 8),
            ("InvalidPhysicalEndExclusive", 9),
            ("MisalignedPhysicalEndExclusive", 10),
            ("AddressRangeOverflow", 11),
            ("PhysicalEndMismatch", 12)
        ];
        (string Name, byte Value)[] subtractionStatuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("ContractLayoutMismatch", 2),
            ("InvalidPageSize", 3),
            ("InvalidSourceRangeCount", 4),
            ("SourceRangeTableByteCountOverflow", 5),
            ("TooManySourceRanges", 6),
            ("SourceRangeCapacityTooSmall", 7),
            ("MissingSourceRanges", 8),
            ("MisalignedSourceRanges", 9),
            ("SourceRangeTableAddressRangeOverflow", 10),
            ("ReservationTableByteCountOverflow", 11),
            ("TooManyReservations", 12),
            ("ReservationCapacityTooSmall", 13),
            ("MissingReservations", 14),
            ("MisalignedReservations", 15),
            ("ReservationTableAddressRangeOverflow", 16),
            ("SourceReservationTablesOverlap", 17),
            ("InvalidSourceRange", 18),
            ("UnorderedSourceProvenance", 19),
            ("UnsortedSourceRange", 20),
            ("OverlappingSourceRange", 21),
            ("TotalSourcePageCountOverflow", 22),
            ("TotalSourceByteCountOverflow", 23),
            ("InvalidReservation", 24),
            ("UnsortedReservation", 25),
            ("OverlappingReservation", 26),
            ("ReservationOutsideSourceRange", 27),
            ("RemovedPageCountOverflow", 28),
            ("RemovedByteCountOverflow", 29),
            ("RequiredOutputRangeCountOverflow", 30),
            ("RemainingPageCountOverflow", 31),
            ("RemainingByteCountOverflow", 32),
            ("DestinationCapacityTooSmall", 33),
            ("DestinationTableByteCountOverflow", 34),
            ("MissingDestination", 35),
            ("MisalignedDestination", 36),
            ("DestinationTableAddressRangeOverflow", 37),
            ("DestinationOverlapsSourceRanges", 38),
            ("DestinationOverlapsReservations", 39),
            ("OutputRangeConstructionFailed", 40),
            ("AccountingMismatch", 41)
        ];

        if (!HasExactByteEnum<BootReservationSourceRangeStatus>(sourceStatuses)
            || !HasExactByteEnum<BootReservationSubtractionStatus>(subtractionStatuses)
            || BootReservationSourceRange.StructureSize != 40
            || BootReservationSubtractor.RequiredPageSize != PageSize
            || BootReservationSubtractor.MaximumSourceRangeCount != 4096
            || BootReservationSubtractor.MaximumReservationCount != 256
            || BootReservationSubtractor.MaximumOutputRangeCount != 4096
            || BootReservationSubtractor.RequiredPointerAlignment != 8
            || BootReservationSubtractor.NoIndex != ~0UL)
        {
            return false;
        }

        Dictionary<string, (Type Type, int Offset)> rangeFields = new()
        {
            ["_sourceRangeIndex"] = (typeof(int), 0),
            ["_reserved"] = (typeof(uint), 4),
            ["_physicalStart"] = (typeof(PhysicalAddress), 8),
            ["_physicalEndExclusive"] = (typeof(PhysicalAddress), 16),
            ["_pageCount"] = (typeof(ulong), 24),
            ["_byteLength"] = (typeof(ulong), 32)
        };
        Dictionary<string, (Type Type, int Offset)> resultFields = new()
        {
            ["_reserved0"] = (typeof(byte), 0),
            ["_reserved1"] = (typeof(byte), 1),
            ["_reserved2"] = (typeof(byte), 2),
            ["_reserved3"] = (typeof(byte), 3),
            ["_reserved4"] = (typeof(byte), 4),
            ["<Status>k__BackingField"] = (typeof(BootReservationSubtractionStatus), 5),
            ["<SourceRangeStatus>k__BackingField"] = (typeof(BootReservationSourceRangeStatus), 6),
            ["<ReservationRangeStatus>k__BackingField"] = (typeof(BootReservedRangeStatus), 7),
            ["<SourceRangeCount>k__BackingField"] = (typeof(ulong), 8),
            ["<SourceRangeCapacity>k__BackingField"] = (typeof(ulong), 16),
            ["<ReservationCount>k__BackingField"] = (typeof(ulong), 24),
            ["<ReservationCapacity>k__BackingField"] = (typeof(ulong), 32),
            ["<RequiredOutputRangeCount>k__BackingField"] = (typeof(ulong), 40),
            ["<WrittenOutputRangeCount>k__BackingField"] = (typeof(ulong), 48),
            ["<AvailableDestinationCapacity>k__BackingField"] = (typeof(ulong), 56),
            ["<InvalidSourceRangeIndex>k__BackingField"] = (typeof(ulong), 64),
            ["<InvalidReservationIndex>k__BackingField"] = (typeof(ulong), 72),
            ["<ConflictingRangeIndex>k__BackingField"] = (typeof(ulong), 80),
            ["<PageSize>k__BackingField"] = (typeof(ulong), 88),
            ["<TotalSourcePages>k__BackingField"] = (typeof(ulong), 96),
            ["<TotalSourceBytes>k__BackingField"] = (typeof(ulong), 104),
            ["<RemovedPages>k__BackingField"] = (typeof(ulong), 112),
            ["<RemovedBytes>k__BackingField"] = (typeof(ulong), 120),
            ["<RemainingPages>k__BackingField"] = (typeof(ulong), 128),
            ["<RemainingBytes>k__BackingField"] = (typeof(ulong), 136)
        };

        return HasExactReadonlyPodLayout<BootReservationSourceRange>(
                expectedSize: 40,
                rangeFields)
            && HasExactReadonlyPodLayout<BootReservationSubtractionResult>(
                expectedSize: 144,
                resultFields)
            && HasExactSubtractApi();
    }

    private static bool TestDefaultsFailClosed()
    {
        BootReservationSourceRange range = default;
        BootReservationSubtractionResult result = default;

        return !range.IsValid
            && range.SourceRangeIndex == 0
            && !range.PhysicalStart.IsValid
            && !range.PhysicalEndExclusive.IsValid
            && range.PageCount == 0
            && range.ByteLength == 0
            && result.Status == BootReservationSubtractionStatus.Uninitialized
            && result.SourceRangeStatus == BootReservationSourceRangeStatus.Uninitialized
            && result.ReservationRangeStatus == BootReservedRangeStatus.Uninitialized
            && result.InvalidSourceRangeIndex == 0
            && result.InvalidReservationIndex == 0
            && result.ConflictingRangeIndex == 0
            && !result.Succeeded;
    }

    private static bool TestRangeFactorySuccess()
    {
        PhysicalAddress start = Address(0x1234_5000);
        BootReservationSourceRangeStatus status =
            BootReservationSourceRange.TryCreate(
                7,
                start,
                5,
                out BootReservationSourceRange range);

        return status == BootReservationSourceRangeStatus.Success
            && range.IsValid
            && range.SourceRangeIndex == 7
            && Raw(range.PhysicalStart) == 0x1234_5000
            && Raw(range.PhysicalEndExclusive) == 0x1234_A000
            && range.PageCount == 5
            && range.ByteLength == 5 * PageSize
            && range.Equals(range)
            && !range.Equals(Source(7, 0x1234_5000, 4));
    }

    private static bool TestRangeFactoryFailures()
    {
        PhysicalAddress aligned = Address(0x4000);
        PhysicalAddress misaligned = Address(0x4001);
        PhysicalAddress wrapping = Address(0xFFFF_FFFF_FFFF_F000);
        ulong overflowingPages = (~0UL / PageSize) + 1;

        return FactoryStatus(
                -1,
                aligned,
                1) == BootReservationSourceRangeStatus.InvalidSourceRangeIndex
            && FactoryStatus(
                0,
                default,
                1) == BootReservationSourceRangeStatus.InvalidPhysicalStart
            && FactoryStatus(
                0,
                aligned,
                0) == BootReservationSourceRangeStatus.InvalidPageCount
            && FactoryStatus(
                0,
                misaligned,
                1) == BootReservationSourceRangeStatus.MisalignedPhysicalStart
            && FactoryStatus(
                0,
                aligned,
                overflowingPages) == BootReservationSourceRangeStatus.PageByteCountOverflow
            && FactoryStatus(
                0,
                wrapping,
                1) == BootReservationSourceRangeStatus.AddressRangeOverflow;
    }

    private static unsafe bool TestIdentity()
    {
        BootReservationSourceRange[] source =
        [
            Source(0, 0x10000, 4),
            Source(1, 0x20000, 3),
            Source(99, 0xF0000, 1)
        ];
        BootReservationSourceRange[] sourceBefore = source.ToArray();
        BootReservationSourceRange[] destination =
        [
            Source(77, 0xD0000, 1),
            Source(78, 0xD2000, 1),
            Source(79, 0xD4000, 1)
        ];
        BootReservationSourceRange trailing = destination[2];

        fixed (BootReservationSourceRange* sourcePointer = source)
        fixed (BootReservationSourceRange* destinationPointer = destination)
        {
            BootReservationSubtractionResult result =
                BootReservationSubtractor.Subtract(
                    sourcePointer,
                    sourceRangeCount: 2,
                    sourceRangeCapacity: 3,
                    reservations: null,
                    reservationCount: 0,
                    reservationCapacity: 0,
                    pageSize: PageSize,
                    destinationPointer,
                    destinationRangeCapacity: 3);

            return IsSuccess(result, 2, 3, 0, 0, 2, 3, 7, 0)
                && destination[0].Equals(source[0])
                && destination[1].Equals(source[1])
                && destination[2].Equals(trailing)
                && RangesEqual(source, sourceBefore);
        }
    }

    private static bool TestHeadSubtraction()
    {
        return SingleSubtractionCase(
            source: Source(0, 0x10000, 4),
            reservation: Reservation(
                BootReservationKind.AllocatorArena,
                0x10000,
                1),
            expected:
            [
                Source(0, 0x11000, 3)
            ],
            expectedRemovedPages: 1);
    }

    private static bool TestTailSubtraction()
    {
        return SingleSubtractionCase(
            source: Source(0, 0x10000, 4),
            reservation: Reservation(
                BootReservationKind.AllocatorArena,
                0x13000,
                1),
            expected:
            [
                Source(0, 0x10000, 3)
            ],
            expectedRemovedPages: 1);
    }

    private static bool TestMiddleSplit()
    {
        return SingleSubtractionCase(
            source: Source(0, 0x10000, 4),
            reservation: Reservation(
                BootReservationKind.AllocatorArena,
                0x11000,
                2),
            expected:
            [
                Source(0, 0x10000, 1),
                Source(0, 0x13000, 1)
            ],
            expectedRemovedPages: 2);
    }

    private static unsafe bool TestFullCover()
    {
        BootReservationSourceRange source = Source(0, 0x10000, 4);
        BootReservedRange reservation = Reservation(
            BootReservationKind.AllocatorArena,
            0x10000,
            4);

        BootReservationSubtractionResult result =
            BootReservationSubtractor.Subtract(
                &source,
                sourceRangeCount: 1,
                sourceRangeCapacity: 1,
                &reservation,
                reservationCount: 1,
                reservationCapacity: 1,
                pageSize: PageSize,
                destinationRanges: null,
                destinationRangeCapacity: 0);

        return IsSuccess(result, 1, 1, 1, 1, 0, 0, 4, 4)
            && result.WrittenOutputRangeCount == 0
            && result.RemainingPages == 0
            && result.RemainingBytes == 0;
    }

    private static unsafe bool TestMultipleRanges()
    {
        BootReservationSourceRange[] source =
        [
            Source(0, 0x10000, 6),
            Source(1, 0x30000, 5)
        ];
        BootReservedRange[] reservations =
        [
            Reservation(BootReservationKind.LoadedImage, 0x10000, 1),
            Reservation(BootReservationKind.BootStack, 0x13000, 1),
            Reservation(BootReservationKind.PageTableArena, 0x33000, 2)
        ];
        BootReservationSourceRange[] expected =
        [
            Source(0, 0x11000, 2),
            Source(0, 0x14000, 2),
            Source(1, 0x30000, 3)
        ];
        BootReservationSourceRange[] destination = new BootReservationSourceRange[4];

        fixed (BootReservationSourceRange* sourcePointer = source)
        fixed (BootReservedRange* reservationPointer = reservations)
        fixed (BootReservationSourceRange* destinationPointer = destination)
        {
            BootReservationSubtractionResult first =
                BootReservationSubtractor.Subtract(
                    sourcePointer,
                    2,
                    2,
                    reservationPointer,
                    3,
                    3,
                    PageSize,
                    destinationPointer,
                    4);
            BootReservationSourceRange[] firstOutput = destination.ToArray();
            BootReservationSubtractionResult second =
                BootReservationSubtractor.Subtract(
                    sourcePointer,
                    2,
                    2,
                    reservationPointer,
                    3,
                    3,
                    PageSize,
                    destinationPointer,
                    4);

            return IsSuccess(first, 2, 2, 3, 3, 3, 4, 11, 4)
                && first.RemainingPages == 7
                && first.RemainingBytes == 7 * PageSize
                && ResultsEqual(first, second)
                && RangesEqual(expected, destination.AsSpan(0, 3).ToArray())
                && RangesEqual(firstOutput, destination);
        }
    }

    private static unsafe bool TestAdjacentSourcesRemainSeparate()
    {
        BootReservationSourceRange[] source =
        [
            Source(0, 0x10000, 2),
            Source(1, 0x12000, 2)
        ];
        BootReservationSourceRange[] destination = new BootReservationSourceRange[2];

        fixed (BootReservationSourceRange* sourcePointer = source)
        fixed (BootReservationSourceRange* destinationPointer = destination)
        {
            BootReservationSubtractionResult result =
                BootReservationSubtractor.Subtract(
                    sourcePointer,
                    2,
                    2,
                    null,
                    0,
                    0,
                    PageSize,
                    destinationPointer,
                    2);

            return IsSuccess(result, 2, 2, 0, 0, 2, 2, 4, 0)
                && destination[0].Equals(source[0])
                && destination[1].Equals(source[1])
                && destination[0].SourceRangeIndex == 0
                && destination[1].SourceRangeIndex == 1;
        }
    }

    private static unsafe bool TestAdjacentReservations()
    {
        BootReservationSourceRange source = Source(0, 0x10000, 6);
        BootReservedRange[] reservations =
        [
            Reservation(BootReservationKind.LoadedImage, 0x11000, 1),
            Reservation(BootReservationKind.BootStack, 0x12000, 2)
        ];
        BootReservationSourceRange[] destination = new BootReservationSourceRange[3];

        fixed (BootReservedRange* reservationPointer = reservations)
        fixed (BootReservationSourceRange* destinationPointer = destination)
        {
            BootReservationSubtractionResult result =
                BootReservationSubtractor.Subtract(
                    &source,
                    1,
                    1,
                    reservationPointer,
                    2,
                    2,
                    PageSize,
                    destinationPointer,
                    3);

            return IsSuccess(result, 1, 1, 2, 2, 2, 3, 6, 3)
                && destination[0].Equals(Source(0, 0x10000, 1))
                && destination[1].Equals(Source(0, 0x14000, 2))
                && destination[0].PageCount != 0
                && destination[1].PageCount != 0;
        }
    }

    private static bool TestInvalidSources()
    {
        BootReservationSourceRange malformed = MutateByte(
            Source(0, 0x10000, 1),
            byteOffset: 4,
            replacement: 1);
        BootReservationSourceRange[] defaultSource = [default];
        BootReservationSourceRange[] malformedSource = [malformed];
        BootReservationSourceRange[] duplicateProvenance =
        [
            Source(0, 0x10000, 1),
            Source(0, 0x12000, 1)
        ];
        BootReservationSourceRange[] skippedProvenance =
        [
            Source(0, 0x10000, 1),
            Source(2, 0x12000, 1)
        ];
        BootReservationSourceRange[] unsorted =
        [
            Source(0, 0x12000, 1),
            Source(1, 0x10000, 1)
        ];
        BootReservationSourceRange[] duplicatePhysical =
        [
            Source(0, 0x10000, 1),
            Source(1, 0x10000, 1)
        ];
        BootReservationSourceRange[] overlapping =
        [
            Source(0, 0x10000, 3),
            Source(1, 0x12000, 2)
        ];

        return FailureStatus(
                defaultSource,
                [],
                BootReservationSubtractionStatus.InvalidSourceRange,
                BootReservationSourceRangeStatus.InvalidPhysicalStart,
                invalidSourceIndex: 0)
            && FailureStatus(
                malformedSource,
                [],
                BootReservationSubtractionStatus.InvalidSourceRange,
                BootReservationSourceRangeStatus.ReservedBitsSet,
                invalidSourceIndex: 0)
            && FailureStatus(
                duplicateProvenance,
                [],
                BootReservationSubtractionStatus.UnorderedSourceProvenance,
                invalidSourceIndex: 1)
            && FailureStatus(
                skippedProvenance,
                [],
                BootReservationSubtractionStatus.UnorderedSourceProvenance,
                invalidSourceIndex: 1)
            && FailureStatus(
                unsorted,
                [],
                BootReservationSubtractionStatus.UnsortedSourceRange,
                invalidSourceIndex: 1,
                conflictingIndex: 0)
            && FailureStatus(
                duplicatePhysical,
                [],
                BootReservationSubtractionStatus.OverlappingSourceRange,
                invalidSourceIndex: 1,
                conflictingIndex: 0)
            && FailureStatus(
                overlapping,
                [],
                BootReservationSubtractionStatus.OverlappingSourceRange,
                invalidSourceIndex: 1,
                conflictingIndex: 0);
    }

    private static bool TestInvalidReservations()
    {
        BootReservationSourceRange[] source = [Source(0, 0x10000, 16)];
        BootReservedRange malformed = MutateByte(
            Reservation(BootReservationKind.LoadedImage, 0x11000, 1),
            byteOffset: 1,
            replacement: 1);
        BootReservedRange[] defaultReservation = [default];
        BootReservedRange[] malformedReservation = [malformed];
        BootReservedRange[] unsorted =
        [
            Reservation(BootReservationKind.LoadedImage, 0x14000, 1),
            Reservation(BootReservationKind.BootStack, 0x12000, 1)
        ];
        BootReservedRange[] duplicate =
        [
            Reservation(BootReservationKind.LoadedImage, 0x12000, 1),
            Reservation(BootReservationKind.BootStack, 0x12000, 1)
        ];
        BootReservedRange[] overlapping =
        [
            Reservation(BootReservationKind.LoadedImage, 0x12000, 3),
            Reservation(BootReservationKind.BootStack, 0x14000, 2)
        ];

        return FailureStatus(
                source,
                defaultReservation,
                BootReservationSubtractionStatus.InvalidReservation,
                reservationStatus: BootReservedRangeStatus.UninitializedKind,
                invalidReservationIndex: 0)
            && FailureStatus(
                source,
                malformedReservation,
                BootReservationSubtractionStatus.InvalidReservation,
                reservationStatus: BootReservedRangeStatus.ReservedBitsSet,
                invalidReservationIndex: 0)
            && FailureStatus(
                source,
                unsorted,
                BootReservationSubtractionStatus.UnsortedReservation,
                invalidReservationIndex: 1,
                conflictingIndex: 0)
            && FailureStatus(
                source,
                duplicate,
                BootReservationSubtractionStatus.OverlappingReservation,
                invalidReservationIndex: 1,
                conflictingIndex: 0)
            && FailureStatus(
                source,
                overlapping,
                BootReservationSubtractionStatus.OverlappingReservation,
                invalidReservationIndex: 1,
                conflictingIndex: 0);
    }

    private static bool TestReservationsOutsideOneSource()
    {
        BootReservationSourceRange[] separated =
        [
            Source(0, 0x10000, 2),
            Source(1, 0x14000, 2)
        ];
        BootReservationSourceRange[] adjacent =
        [
            Source(0, 0x20000, 2),
            Source(1, 0x22000, 2)
        ];
        BootReservedRange[] before =
        [
            Reservation(BootReservationKind.LoadedImage, 0x0F000, 1)
        ];
        BootReservedRange[] after =
        [
            Reservation(BootReservationKind.LoadedImage, 0x16000, 1)
        ];
        BootReservedRange[] partialHead =
        [
            Reservation(BootReservationKind.LoadedImage, 0x0F000, 2)
        ];
        BootReservedRange[] partialTail =
        [
            Reservation(BootReservationKind.LoadedImage, 0x15000, 2)
        ];
        BootReservedRange[] inGap =
        [
            Reservation(BootReservationKind.LoadedImage, 0x12000, 1)
        ];
        BootReservedRange[] spansGap =
        [
            Reservation(BootReservationKind.LoadedImage, 0x11000, 4)
        ];
        BootReservedRange[] crossesAdjacentSources =
        [
            Reservation(BootReservationKind.LoadedImage, 0x21000, 2)
        ];

        return OutsideFailure(separated, before)
            && OutsideFailure(separated, after)
            && OutsideFailure(separated, partialHead)
            && OutsideFailure(separated, partialTail)
            && OutsideFailure(separated, inGap)
            && OutsideFailure(separated, spansGap)
            && OutsideFailure(adjacent, crossesAdjacentSources);
    }

    private static unsafe bool TestPointerCountCapacityAndSpanPreflight()
    {
        BootReservationSourceRange source = Source(0, 0x10000, 2);
        BootReservedRange reservation = Reservation(
            BootReservationKind.LoadedImage,
            0x10000,
            1);
        ulong sourceOverflowCapacity =
            (~0UL / (ulong)sizeof(BootReservationSourceRange)) + 1;
        ulong reservationOverflowCapacity =
            (~0UL / (ulong)sizeof(BootReservedRange)) + 1;
        ulong destinationOverflowCapacity =
            (~0UL / (ulong)sizeof(BootReservationSourceRange)) + 1;
        BootReservationSourceRange* poisonedSource =
            (BootReservationSourceRange*)unchecked(
                (nuint)0xFFFF_FFFF_FFFF_FFF8UL);
        BootReservedRange* poisonedReservation =
            (BootReservedRange*)unchecked(
                (nuint)0xFFFF_FFFF_FFFF_FFF8UL);
        return PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                null,
                0,
                0,
                pageSize: 0,
                destinationRangeCapacity: 2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.InvalidPageSize)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                null,
                0,
                0,
                pageSize: PageSize * 2,
                destinationRangeCapacity: 2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.InvalidPageSize)
            && PreflightFailurePreservesDestination(
                null,
                0,
                0,
                null,
                0,
                0,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.InvalidSourceRangeCount)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                sourceOverflowCapacity,
                null,
                0,
                0,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus
                    .SourceRangeTableByteCountOverflow)
            && PreflightFailurePreservesDestination(
                &source,
                BootReservationSubtractor.MaximumSourceRangeCount + 1,
                BootReservationSubtractor.MaximumSourceRangeCount + 1,
                null,
                0,
                0,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.TooManySourceRanges)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                0,
                null,
                0,
                0,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus
                    .SourceRangeCapacityTooSmall)
            && PreflightFailurePreservesDestination(
                null,
                1,
                1,
                null,
                0,
                0,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.MissingSourceRanges)
            && PreflightFailurePreservesDestination(
                (BootReservationSourceRange*)((byte*)&source + 1),
                1,
                1,
                null,
                0,
                0,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.MisalignedSourceRanges)
            && PreflightFailurePreservesDestination(
                poisonedSource,
                1,
                1,
                null,
                0,
                0,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus
                    .SourceRangeTableAddressRangeOverflow)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                &reservation,
                1,
                reservationOverflowCapacity,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus
                    .ReservationTableByteCountOverflow)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                &reservation,
                BootReservationSubtractor.MaximumReservationCount + 1,
                BootReservationSubtractor.MaximumReservationCount + 1,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.TooManyReservations)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                &reservation,
                1,
                0,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus
                    .ReservationCapacityTooSmall)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                null,
                1,
                1,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.MissingReservations)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                (BootReservedRange*)((byte*)&reservation + 1),
                1,
                1,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus.MisalignedReservations)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                poisonedReservation,
                1,
                1,
                PageSize,
                2,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus
                    .ReservationTableAddressRangeOverflow)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                null,
                0,
                0,
                PageSize,
                destinationOverflowCapacity,
                PreflightDestinationMode.Valid,
                BootReservationSubtractionStatus
                    .DestinationTableByteCountOverflow)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                null,
                0,
                0,
                PageSize,
                1,
                PreflightDestinationMode.Null,
                BootReservationSubtractionStatus.MissingDestination)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                null,
                0,
                0,
                PageSize,
                1,
                PreflightDestinationMode.Misaligned,
                BootReservationSubtractionStatus.MisalignedDestination)
            && PreflightFailurePreservesDestination(
                &source,
                1,
                1,
                null,
                0,
                0,
                PageSize,
                1,
                PreflightDestinationMode.Poison,
                BootReservationSubtractionStatus
                    .DestinationTableAddressRangeOverflow);
    }

    private static unsafe bool TestPairwiseTableOverlap()
    {
        BootReservationSourceRange source = Source(0, 0x10000, 2);
        BootReservedRange reservation = Reservation(
            BootReservationKind.LoadedImage,
            0x10000,
            1);
        BootReservationSourceRange destination = Source(77, 0x70000, 1);
        byte[] sourceBefore = StructBytes(source);
        byte[] reservationBefore = StructBytes(reservation);
        byte[] destinationBefore = StructBytes(destination);

        BootReservationSubtractionResult sourceReservation =
            RawSubtract(
                &source,
                1,
                1,
                (BootReservedRange*)&source,
                1,
                1,
                PageSize,
                &destination,
                1);
        if (!IsFailure(
                sourceReservation,
                BootReservationSubtractionStatus
                    .SourceReservationTablesOverlap)
            || !sourceBefore.AsSpan().SequenceEqual(StructBytes(source))
            || !reservationBefore.AsSpan().SequenceEqual(
                StructBytes(reservation))
            || !destinationBefore.AsSpan().SequenceEqual(
                StructBytes(destination)))
        {
            return false;
        }

        source = Source(0, 0x10000, 2);
        reservation = Reservation(
            BootReservationKind.LoadedImage,
            0x10000,
            1);
        destination = Source(77, 0x70000, 1);
        sourceBefore = StructBytes(source);
        reservationBefore = StructBytes(reservation);
        destinationBefore = StructBytes(destination);
        BootReservationSubtractionResult destinationSource =
            RawSubtract(
                &source,
                1,
                1,
                null,
                0,
                0,
                PageSize,
                &source,
                1);
        if (!IsFailure(
                destinationSource,
                BootReservationSubtractionStatus
                    .DestinationOverlapsSourceRanges)
            || !sourceBefore.AsSpan().SequenceEqual(StructBytes(source))
            || !reservationBefore.AsSpan().SequenceEqual(
                StructBytes(reservation))
            || !destinationBefore.AsSpan().SequenceEqual(
                StructBytes(destination)))
        {
            return false;
        }

        source = Source(0, 0x10000, 2);
        reservation = Reservation(
            BootReservationKind.LoadedImage,
            0x10000,
            1);
        destination = Source(77, 0x70000, 1);
        sourceBefore = StructBytes(source);
        reservationBefore = StructBytes(reservation);
        destinationBefore = StructBytes(destination);
        BootReservationSubtractionResult destinationReservation =
            RawSubtract(
                &source,
                1,
                1,
                &reservation,
                1,
                1,
                PageSize,
                (BootReservationSourceRange*)&reservation,
                1);

        if (!IsFailure(
                destinationReservation,
                BootReservationSubtractionStatus
                    .DestinationOverlapsReservations)
            || !sourceBefore.AsSpan().SequenceEqual(StructBytes(source))
            || !reservationBefore.AsSpan().SequenceEqual(
                StructBytes(reservation))
            || !destinationBefore.AsSpan().SequenceEqual(
                StructBytes(destination)))
        {
            return false;
        }

        return DeclaredCapacityTailAliasesPreserveBacking();
    }

    private static unsafe bool DeclaredCapacityTailAliasesPreserveBacking()
    {
        BootReservationSourceRange* sourceWithReservationTail =
            stackalloc BootReservationSourceRange[2];
        sourceWithReservationTail[0] = Source(0, 0x10000, 4);
        BootReservedRange* reservationTail =
            (BootReservedRange*)(sourceWithReservationTail + 1);
        *reservationTail = Reservation(
            BootReservationKind.LoadedImage,
            0x10000,
            1);
        BootReservationSourceRange destination =
            Source(77, 0x70000, 1);
        byte[] sourceReservationBackingBefore =
            PointerBytes(sourceWithReservationTail, 2);
        byte[] destinationBefore = StructBytes(destination);
        BootReservationSubtractionResult sourceReservationTailResult =
            RawSubtract(
                sourceWithReservationTail,
                sourceRangeCount: 1,
                sourceRangeCapacity: 2,
                reservationTail,
                reservationCount: 1,
                reservationCapacity: 1,
                PageSize,
                &destination,
                destinationRangeCapacity: 1);
        if (!IsFailure(
                sourceReservationTailResult,
                BootReservationSubtractionStatus
                    .SourceReservationTablesOverlap)
            || !sourceReservationBackingBefore.AsSpan().SequenceEqual(
                PointerBytes(sourceWithReservationTail, 2))
            || !destinationBefore.AsSpan().SequenceEqual(
                StructBytes(destination)))
        {
            return false;
        }

        BootReservationSourceRange* sourceWithDestinationTail =
            stackalloc BootReservationSourceRange[2];
        sourceWithDestinationTail[0] = Source(0, 0x10000, 2);
        sourceWithDestinationTail[1] = Source(77, 0x70000, 1);
        byte[] sourceDestinationBackingBefore =
            PointerBytes(sourceWithDestinationTail, 2);
        BootReservationSubtractionResult sourceDestinationTailResult =
            RawSubtract(
                sourceWithDestinationTail,
                sourceRangeCount: 1,
                sourceRangeCapacity: 2,
                reservations: null,
                reservationCount: 0,
                reservationCapacity: 0,
                PageSize,
                destinationRanges: sourceWithDestinationTail + 1,
                destinationRangeCapacity: 1);
        if (!IsFailure(
                sourceDestinationTailResult,
                BootReservationSubtractionStatus
                    .DestinationOverlapsSourceRanges)
            || !sourceDestinationBackingBefore.AsSpan().SequenceEqual(
                PointerBytes(sourceWithDestinationTail, 2)))
        {
            return false;
        }

        BootReservationSourceRange source = Source(0, 0x10000, 2);
        BootReservedRange* reservationWithDestinationTail =
            stackalloc BootReservedRange[2];
        reservationWithDestinationTail[0] = Reservation(
            BootReservationKind.LoadedImage,
            0x10000,
            1);
        BootReservationSourceRange* destinationTail =
            (BootReservationSourceRange*)
                (reservationWithDestinationTail + 1);
        *destinationTail = Source(77, 0x70000, 1);
        byte[] sourceBefore = StructBytes(source);
        byte[] reservationDestinationBackingBefore =
            PointerBytes(reservationWithDestinationTail, 2);
        BootReservationSubtractionResult reservationDestinationTailResult =
            RawSubtract(
                &source,
                sourceRangeCount: 1,
                sourceRangeCapacity: 1,
                reservationWithDestinationTail,
                reservationCount: 1,
                reservationCapacity: 2,
                PageSize,
                destinationTail,
                destinationRangeCapacity: 1);
        return IsFailure(
                reservationDestinationTailResult,
                BootReservationSubtractionStatus
                    .DestinationOverlapsReservations)
            && sourceBefore.AsSpan().SequenceEqual(StructBytes(source))
            && reservationDestinationBackingBefore.AsSpan().SequenceEqual(
                PointerBytes(reservationWithDestinationTail, 2));
    }

    private static unsafe bool TestInsufficientDestination()
    {
        BootReservationSourceRange source = Source(0, 0x10000, 4);
        BootReservedRange reservation = Reservation(
            BootReservationKind.LoadedImage,
            0x11000,
            2);
        BootReservationSourceRange[] destination =
        [
            Source(70, 0x70000, 1),
            Source(71, 0x72000, 1),
            Source(72, 0x74000, 1)
        ];
        byte[] before = StructArrayBytes(destination);

        fixed (BootReservationSourceRange* destinationPointer = destination)
        {
            BootReservationSubtractionResult result =
                RawSubtract(
                    &source,
                    1,
                    1,
                    &reservation,
                    1,
                    1,
                    PageSize,
                    destinationPointer,
                    1);

            return IsFailure(
                    result,
                    BootReservationSubtractionStatus.DestinationCapacityTooSmall)
                && result.RequiredOutputRangeCount == 2
                && result.WrittenOutputRangeCount == 0
                && result.AvailableDestinationCapacity == 1
                && result.TotalSourcePages == 4
                && result.RemovedPages == 2
                && result.RemainingPages == 2
                && before.AsSpan().SequenceEqual(StructArrayBytes(destination));
        }
    }

    private static bool TestPostReadFailureAtomicity()
    {
        BootReservationSourceRange sentinel0 = Source(90, 0x90000, 1);
        BootReservationSourceRange sentinel1 = Source(91, 0x92000, 1);
        BootReservationSourceRange[] malformedSource =
        [
            MutateByte(Source(0, 0x10000, 2), 4, 1)
        ];
        BootReservationSourceRange[] badProvenance =
        [
            Source(1, 0x10000, 2)
        ];
        BootReservationSourceRange[] unsortedSource =
        [
            Source(0, 0x12000, 1),
            Source(1, 0x10000, 1)
        ];
        BootReservationSourceRange[] overlappingSource =
        [
            Source(0, 0x10000, 3),
            Source(1, 0x12000, 2)
        ];
        BootReservationSourceRange[] valid = [Source(0, 0x10000, 8)];
        BootReservedRange[] malformedReservation =
        [
            MutateByte(
                Reservation(BootReservationKind.LoadedImage, 0x11000, 1),
                1,
                1)
        ];
        BootReservedRange[] unsortedReservation =
        [
            Reservation(BootReservationKind.LoadedImage, 0x14000, 1),
            Reservation(BootReservationKind.BootStack, 0x12000, 1)
        ];
        BootReservedRange[] overlappingReservation =
        [
            Reservation(BootReservationKind.LoadedImage, 0x12000, 3),
            Reservation(BootReservationKind.BootStack, 0x14000, 2)
        ];
        BootReservedRange[] outside =
        [
            Reservation(BootReservationKind.LoadedImage, 0x20000, 1)
        ];
        BootReservedRange[] split =
        [
            Reservation(BootReservationKind.LoadedImage, 0x12000, 1)
        ];

        return AtomicFailure(
                malformedSource,
                [],
                2,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.InvalidSourceRange)
            && AtomicFailure(
                badProvenance,
                [],
                2,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.UnorderedSourceProvenance)
            && AtomicFailure(
                unsortedSource,
                [],
                2,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.UnsortedSourceRange)
            && AtomicFailure(
                overlappingSource,
                [],
                2,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.OverlappingSourceRange)
            && AtomicFailure(
                valid,
                malformedReservation,
                2,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.InvalidReservation)
            && AtomicFailure(
                valid,
                unsortedReservation,
                2,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.UnsortedReservation)
            && AtomicFailure(
                valid,
                overlappingReservation,
                2,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.OverlappingReservation)
            && AtomicFailure(
                valid,
                outside,
                2,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.ReservationOutsideSourceRange)
            && AtomicFailure(
                valid,
                split,
                1,
                sentinel0,
                sentinel1,
                BootReservationSubtractionStatus.DestinationCapacityTooSmall)
            && RequiredOutputOverflowPreservesDestination(
                sentinel0,
                sentinel1);
    }

    private static unsafe bool TestSuccessPrefixAndSentinels()
    {
        BootReservationSourceRange source = Source(0, 0x10000, 4);
        BootReservedRange reservation = Reservation(
            BootReservationKind.LoadedImage,
            0x11000,
            2);
        BootReservationSourceRange[] destination =
        [
            Source(70, 0x70000, 1),
            Source(71, 0x72000, 1),
            Source(72, 0x74000, 1),
            Source(73, 0x76000, 1)
        ];
        BootReservationSourceRange sentinel2 = destination[2];
        BootReservationSourceRange sentinel3 = destination[3];

        fixed (BootReservationSourceRange* destinationPointer = destination)
        {
            BootReservationSubtractionResult result =
                RawSubtract(
                    &source,
                    1,
                    1,
                    &reservation,
                    1,
                    1,
                    PageSize,
                    destinationPointer,
                    4);

            return IsSuccess(result, 1, 1, 1, 1, 2, 4, 4, 2)
                && destination[0].Equals(Source(0, 0x10000, 1))
                && destination[1].Equals(Source(0, 0x13000, 1))
                && destination[2].Equals(sentinel2)
                && destination[3].Equals(sentinel3);
        }
    }

    private static unsafe bool TestDeterminismAndAllocation()
    {
        BootReservationSourceRange* source =
            stackalloc BootReservationSourceRange[2];
        BootReservedRange* reservations = stackalloc BootReservedRange[2];
        BootReservationSourceRange* destination =
            stackalloc BootReservationSourceRange[4];
        source[0] = Source(0, 0x10000, 6);
        source[1] = Source(1, 0x30000, 4);
        reservations[0] = Reservation(
            BootReservationKind.LoadedImage,
            0x11000,
            2);
        reservations[1] = Reservation(
            BootReservationKind.BootStack,
            0x32000,
            1);

        BootReservationSubtractionResult expected =
            RawSubtract(
                source,
                2,
                2,
                reservations,
                2,
                2,
                PageSize,
                destination,
                4);
        _ = RawSubtract(
            source,
            2,
            2,
            reservations,
            2,
            2,
            PageSize,
            destination,
            4);

        long before = GC.GetAllocatedBytesForCurrentThread();
        BootReservationSubtractionResult last = default;
        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            last = RawSubtract(
                source,
                2,
                2,
                reservations,
                2,
                2,
                PageSize,
                destination,
                4);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        return expected.Succeeded
            && ResultsEqual(expected, last)
            && after == before
            && source[0].Equals(Source(0, 0x10000, 6))
            && source[1].Equals(Source(1, 0x30000, 4))
            && reservations[0].Equals(
                Reservation(BootReservationKind.LoadedImage, 0x11000, 2))
            && reservations[1].Equals(
                Reservation(BootReservationKind.BootStack, 0x32000, 1));
    }

    private static unsafe bool TestForgedSuccessEvidence()
    {
        BootReservationSubtractionResult forgedDefault =
            MutateByte(
                default(BootReservationSubtractionResult),
                byteOffset: 5,
                replacement: (byte)BootReservationSubtractionStatus.Success);
        if (forgedDefault.Succeeded)
        {
            return false;
        }

        BootReservationSourceRange source = Source(0, 0x10000, 4);
        BootReservedRange reservation = Reservation(
            BootReservationKind.LoadedImage,
            0x11000,
            2);
        BootReservationSourceRange* destination =
            stackalloc BootReservationSourceRange[2];
        BootReservationSubtractionResult valid =
            RawSubtract(
                &source,
                1,
                1,
                &reservation,
                1,
                1,
                PageSize,
                destination,
                2);
        if (!valid.Succeeded)
        {
            return false;
        }

        int[] evidenceOffsets =
        [
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            16,
            24,
            32,
            40,
            48,
            56,
            64,
            72,
            80,
            88,
            96,
            104,
            112,
            120,
            128,
            136
        ];
        foreach (int offset in evidenceOffsets)
        {
            byte current = StructBytes(valid)[offset];
            byte replacement = current == 0 ? (byte)1 : (byte)0;
            if (MutateByte(valid, offset, replacement).Succeeded)
            {
                return false;
            }
        }

        BootReservationSubtractionResult plausible =
            InvokeSuccessFactory(
                sourceRangeCount: 2,
                sourceRangeCapacity: 2,
                reservationCount: 1,
                reservationCapacity: 1,
                requiredOutputRangeCount: 2,
                writtenOutputRangeCount: 2,
                availableDestinationCapacity: 2,
                totalSourcePages: 4,
                removedPages: 1,
                remainingPages: 3);
        BootReservationSubtractionResult zeroReservationWrongOutput =
            InvokeSuccessFactory(
                sourceRangeCount: 2,
                sourceRangeCapacity: 2,
                reservationCount: 0,
                reservationCapacity: 0,
                requiredOutputRangeCount: 1,
                writtenOutputRangeCount: 1,
                availableDestinationCapacity: 2,
                totalSourcePages: 2,
                removedPages: 0,
                remainingPages: 2);
        BootReservationSubtractionResult tooManyOutputFragments =
            InvokeSuccessFactory(
                sourceRangeCount: 1,
                sourceRangeCapacity: 1,
                reservationCount: 1,
                reservationCapacity: 1,
                requiredOutputRangeCount: 3,
                writtenOutputRangeCount: 3,
                availableDestinationCapacity: 3,
                totalSourcePages: 4,
                removedPages: 1,
                remainingPages: 3);
        BootReservationSubtractionResult fewerPagesThanSources =
            InvokeSuccessFactory(
                sourceRangeCount: 2,
                sourceRangeCapacity: 2,
                reservationCount: 1,
                reservationCapacity: 1,
                requiredOutputRangeCount: 1,
                writtenOutputRangeCount: 1,
                availableDestinationCapacity: 1,
                totalSourcePages: 1,
                removedPages: 1,
                remainingPages: 0);
        BootReservationSubtractionResult fewerRemovedPagesThanReservations =
            InvokeSuccessFactory(
                sourceRangeCount: 1,
                sourceRangeCapacity: 1,
                reservationCount: 2,
                reservationCapacity: 2,
                requiredOutputRangeCount: 1,
                writtenOutputRangeCount: 1,
                availableDestinationCapacity: 1,
                totalSourcePages: 3,
                removedPages: 1,
                remainingPages: 2);
        BootReservationSubtractionResult fewerRemainingPagesThanOutputs =
            InvokeSuccessFactory(
                sourceRangeCount: 1,
                sourceRangeCapacity: 1,
                reservationCount: 1,
                reservationCapacity: 1,
                requiredOutputRangeCount: 2,
                writtenOutputRangeCount: 2,
                availableDestinationCapacity: 2,
                totalSourcePages: 2,
                removedPages: 1,
                remainingPages: 1);
        BootReservationSubtractionResult tooFewFragmentsForSources =
            InvokeSuccessFactory(
                sourceRangeCount: 10,
                sourceRangeCapacity: 10,
                reservationCount: 1,
                reservationCapacity: 1,
                requiredOutputRangeCount: 1,
                writtenOutputRangeCount: 1,
                availableDestinationCapacity: 1,
                totalSourcePages: 10,
                removedPages: 1,
                remainingPages: 9);

        return plausible.Succeeded
            && !zeroReservationWrongOutput.Succeeded
            && !tooManyOutputFragments.Succeeded
            && !fewerPagesThanSources.Succeeded
            && !fewerRemovedPagesThanReservations.Succeeded
            && !fewerRemainingPagesThanOutputs.Succeeded
            && !tooFewFragmentsForSources.Succeeded;
    }

    private static bool TestSourceFirewalls()
    {
        string productionPath = GetSubtractorSourcePath();
        string production = RemoveComments(File.ReadAllText(productionPath));
        string[] forbiddenFragments =
        [
            "Span<",
            "ReadOnlySpan<",
            "Memory<",
            "ReadOnlyMemory<",
            "string",
            "System.",
            "System::",
            "throw ",
            "try ",
            "catch ",
            "finally ",
            "typeof",
            "Reflection",
            "Activator",
            "dynamic",
            "delegate",
            "record ",
            "List<",
            "Dictionary<",
            "IEnumerable<",
            "BootMemoryRegionPlan",
            "BootPageAllocatorPlan",
            "ulong.MaxValue",
            "EfiEntry",
            "BootSequence",
            "Qemu",
            "QEMU",
            "OSHost_",
            "RuntimeExport",
            "UnmanagedCallersOnly"
        ];
        foreach (string fragment in forbiddenFragments)
        {
            if (production.Contains(fragment, StringComparison.Ordinal))
            {
                return SourceFailure("forbidden-fragment=" + fragment);
            }
        }

        if (Regex.IsMatch(
                production,
                @"(?m)^\s*(?:global\s+)?using\b",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                production,
                @"\b(?:throw|try|catch|finally)\b",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                production,
                @"\b(object|dynamic|delegate|record|Exception|String|Array|Type)\b",
                RegexOptions.CultureInvariant)
            || !production.Contains(
                "public static unsafe class BootReservationSubtractor",
                StringComparison.Ordinal)
            || !production.Contains(
                "public readonly struct BootReservationSourceRange",
                StringComparison.Ordinal)
            || !production.Contains(
                "public readonly struct BootReservationSubtractionResult",
                StringComparison.Ordinal)
            || !production.Contains(
                "private const ulong MaximumUnsignedLong = ~0UL;",
                StringComparison.Ordinal))
        {
            return SourceFailure("production-surface");
        }

        string[] integrationPaths =
        [
            Path.Combine(
                HostToolProcess.FindRepositoryRoot(),
                "src",
                "OS",
                "OS.csproj"),
            Path.Combine(
                HostToolProcess.FindRepositoryRoot(),
                "src",
                "OS",
                "Boot",
                "EfiEntry.cs"),
            Path.Combine(
                HostToolProcess.FindRepositoryRoot(),
                "src",
                "OS.Kernel",
                "Boot",
                "BootSequence.cs"),
            Path.Combine(
                HostToolProcess.FindRepositoryRoot(),
                "src",
                "OS.Kernel",
                "Diagnostics",
                "Probes.cs")
        ];
        string[] forbiddenIntegrationNames =
        [
            nameof(BootReservationSubtractor),
            nameof(BootReservationSourceRange),
            nameof(BootReservationSubtractionResult),
            ReadyMarker
        ];
        foreach (string path in integrationPaths)
        {
            string source = File.ReadAllText(path);
            foreach (string name in forbiddenIntegrationNames)
            {
                if (source.Contains(name, StringComparison.Ordinal))
                {
                    return SourceFailure(
                        "integration="
                        + Path.GetFileName(path)
                        + ";name="
                        + name);
                }
            }
        }

        return RuntimeHelpers
                .IsReferenceOrContainsReferences<BootReservationSourceRange>()
                == false
            && RuntimeHelpers
                .IsReferenceOrContainsReferences<BootReservationSubtractionResult>()
                == false
            && EvaluatedEfiCompileClosureExcludesSubtractor();
    }

    private static bool EvaluatedEfiCompileClosureExcludesSubtractor()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        string projectSource = File.ReadAllText(projectPath);
        if (!Regex.IsMatch(
                projectSource,
                @"<EnableDefaultCompileItems>\s*false\s*</EnableDefaultCompileItems>",
                RegexOptions.CultureInvariant
                    | RegexOptions.IgnoreCase))
        {
            return SourceFailure("efi-default-compile-items");
        }

        NoStdLibBuildResult evaluation =
            RunEfiCompileItemEvaluation(repositoryRoot, projectPath);
        if (evaluation.TimedOut || evaluation.ExitCode != 0)
        {
            return SourceFailure(
                "efi-compile-evaluation="
                + NormalizeCompilerOutput(evaluation.Output));
        }

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(evaluation.Output);
            if (!document.RootElement.TryGetProperty(
                    "Items",
                    out JsonElement items)
                || !items.TryGetProperty(
                    "Compile",
                    out JsonElement compileItems)
                || compileItems.ValueKind != JsonValueKind.Array)
            {
                return SourceFailure("efi-compile-json-shape");
            }

            string forbiddenProductionPath = Path.GetFullPath(
                GetSubtractorSourcePath());
            string forbiddenRegionPlanPath = Path.GetFullPath(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Memory",
                    "BootMemoryRegionPlan.cs"));
            string forbiddenAllocatorPlanPath = Path.GetFullPath(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Memory",
                    "BootPageAllocatorPlan.cs"));
            foreach (JsonElement item in compileItems.EnumerateArray())
            {
                string identity = item.TryGetProperty(
                        "Identity",
                        out JsonElement identityElement)
                    ? identityElement.GetString() ?? string.Empty
                    : string.Empty;
                string fullPath = item.TryGetProperty(
                        "FullPath",
                        out JsonElement fullPathElement)
                    ? fullPathElement.GetString() ?? string.Empty
                    : string.Empty;
                string canonicalFullPath =
                    string.IsNullOrWhiteSpace(fullPath)
                        ? string.Empty
                        : Path.GetFullPath(fullPath);

                bool forbidden =
                    string.Equals(
                        canonicalFullPath,
                        forbiddenProductionPath,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        canonicalFullPath,
                        forbiddenRegionPlanPath,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        canonicalFullPath,
                        forbiddenAllocatorPlanPath,
                        StringComparison.OrdinalIgnoreCase)
                    || identity.EndsWith(
                        "BootReservationSubtractor.cs",
                        StringComparison.OrdinalIgnoreCase)
                    || identity.EndsWith(
                        "BootReservationSourceRange.cs",
                        StringComparison.OrdinalIgnoreCase)
                    || identity.EndsWith(
                        "BootMemoryRegionPlan.cs",
                        StringComparison.OrdinalIgnoreCase)
                    || identity.EndsWith(
                        "BootPageAllocatorPlan.cs",
                        StringComparison.OrdinalIgnoreCase)
                    || canonicalFullPath.EndsWith(
                        "BootReservationSourceRange.cs",
                        StringComparison.OrdinalIgnoreCase)
                    || canonicalFullPath.EndsWith(
                        "BootMemoryRegionPlan.cs",
                        StringComparison.OrdinalIgnoreCase)
                    || canonicalFullPath.EndsWith(
                        "BootPageAllocatorPlan.cs",
                        StringComparison.OrdinalIgnoreCase);
                if (forbidden)
                {
                    return SourceFailure(
                        "efi-compile-import=" + identity);
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            return SourceFailure(
                "efi-compile-json=" + ex.GetType().Name);
        }
    }

    private static NoStdLibBuildResult RunEfiCompileItemEvaluation(
        string repositoryRoot,
        string projectPath)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
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
        start.ArgumentList.Add("-getItem:Compile");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";

        using Process process = new Process { StartInfo = start };
        if (!process.Start())
        {
            return new NoStdLibBuildResult(
                -1,
                timedOut: false,
                "dotnet msbuild process did not start");
        }

        Task<string> standardOutput =
            process.StandardOutput.ReadToEndAsync();
        Task<string> standardError =
            process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(60_000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            process.WaitForExit(5_000);
        }

        string output = standardOutput.Wait(5_000)
            ? standardOutput.Result
            : "stdout did not close";
        string error = standardError.Wait(5_000)
            ? standardError.Result
            : "stderr did not close";
        return new NoStdLibBuildResult(
            exited ? process.ExitCode : -1,
            timedOut: !exited,
            output + error);
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();
        try
        {
            NoStdLibBuildResult build = RunNoStdLibBuild(workspace);
            string assemblyPath = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.BootReservationSubtractorCompileClosure.dll");
            if (build.TimedOut
                || build.ExitCode != 0
                || !HasZeroAssemblyReferences(assemblyPath))
            {
                Console.WriteLine(
                    "M01_007_NOSTDLIB_DIAGNOSTIC "
                    + NormalizeCompilerOutput(build.Output));
                return false;
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static bool TestMarkerBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string testSource = File.ReadAllText(GetTestSourcePath());
        string productionSource = File.ReadAllText(GetSubtractorSourcePath());
        string canonicalTestPath = Path.GetFullPath(GetTestSourcePath());
        string canonicalBootDocPath = Path.GetFullPath(
            Path.Combine(repositoryRoot, "docs", "boot.md"));
        string canonicalBacklogPath = Path.GetFullPath(
            Path.Combine(
                repositoryRoot,
                ".gitea",
                "backlog",
                "clros-issues.json"));
        string readyWriteCall = string.Concat(
            "Console.WriteLine(",
            "ReadyMarker);");
        if (CountOccurrences(
                testSource,
                "\"" + ReadyMarker + "\"") != 1
            || CountOccurrences(
                testSource,
                ReadyMarker) != 1
            || CountOccurrences(
                testSource,
                readyWriteCall) != 1
            || productionSource.Contains(
                ReadyMarker,
                StringComparison.Ordinal))
        {
            return false;
        }

        string[] scanRoots =
        [
            Path.Combine(repositoryRoot, "src"),
            Path.Combine(repositoryRoot, "std"),
            Path.Combine(repositoryRoot, "apps"),
            Path.Combine(repositoryRoot, "tools"),
            Path.Combine(repositoryRoot, "tests"),
            Path.Combine(repositoryRoot, "scripts"),
            Path.Combine(repositoryRoot, "docs"),
            Path.Combine(repositoryRoot, ".gitea")
        ];
        foreach (string root in scanRoots)
        {
            if (!Directory.Exists(root))
            {
                return false;
            }

            foreach (string path in Directory.EnumerateFiles(
                root,
                "*",
                SearchOption.AllDirectories))
            {
                if (IsGeneratedOrReferencePath(path)
                    || !HasTextExtension(path))
                {
                    continue;
                }

                string contents = File.ReadAllText(path);
                int occurrences = CountOccurrences(
                    contents,
                    ReadyMarker);
                if (occurrences == 0)
                {
                    continue;
                }

                string canonicalPath = Path.GetFullPath(path);
                if (string.Equals(
                        canonicalPath,
                        canonicalTestPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (occurrences != 1
                        || CountOccurrences(
                            contents,
                            readyWriteCall) != 1)
                    {
                        return false;
                    }

                    continue;
                }

                bool isCanonicalDeclaration =
                    string.Equals(
                        canonicalPath,
                        canonicalBootDocPath,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        canonicalPath,
                        canonicalBacklogPath,
                        StringComparison.OrdinalIgnoreCase);
                if (!isCanonicalDeclaration
                    || contents.Contains(
                        readyWriteCall,
                        StringComparison.Ordinal)
                    || contents.Contains(
                        "Console.WriteLine(\"" + ReadyMarker,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        string bootGuestMarker = string.Concat("OS_BOOT_", "OK");
        string smokeGuestMarker = string.Concat("OS_SMOKE_", "PASS");
        return !testSource.Contains(
                bootGuestMarker,
                StringComparison.Ordinal)
            && !testSource.Contains(
                smokeGuestMarker,
                StringComparison.Ordinal);
    }

    private static bool HasExactByteEnum<T>(
        (string Name, byte Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
        {
            return false;
        }

        Array values = Enum.GetValues(typeof(T));
        string[] names = Enum.GetNames(typeof(T));
        if (values.Length != expected.Length
            || names.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            byte value = Convert.ToByte(values.GetValue(index));
            if (names[index] != expected[index].Name
                || value != expected[index].Value)
            {
                return false;
            }
        }

        string source = RemoveComments(
            File.ReadAllText(GetSubtractorSourcePath()));
        Match match = Regex.Match(
            source,
            @"public\s+enum\s+"
                + Regex.Escape(typeof(T).Name)
                + @"\s*:\s*byte\s*\{(?<body>[^}]*)\}",
            RegexOptions.CultureInvariant
                | RegexOptions.Singleline);
        if (!match.Success)
        {
            return false;
        }

        MatchCollection declarations = Regex.Matches(
            match.Groups["body"].Value,
            @"(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[0-9]+)\s*,?\s*$",
            RegexOptions.CultureInvariant);
        if (declarations.Count != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            Match declaration = declarations[index];
            if (declaration.Groups["name"].Value != expected[index].Name
                || !byte.TryParse(
                    declaration.Groups["value"].Value,
                    out byte value)
                || value != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExactReadonlyPodLayout<T>(
        int expectedSize,
        Dictionary<string, (Type Type, int Offset)> expectedFields)
        where T : struct
    {
        Type type = typeof(T);
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
        if (!type.IsValueType
            || !type.IsLayoutSequential
            || !type.IsDefined(
                typeof(IsReadOnlyAttribute),
                inherit: false)
            || fields.Length != expectedFields.Count
            || Unsafe.SizeOf<T>() != expectedSize
            || Marshal.SizeOf<T>() != expectedSize
            || RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            || type.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public).Length != 0)
        {
            return false;
        }

        foreach (FieldInfo field in fields)
        {
            if (!expectedFields.TryGetValue(
                    field.Name,
                    out (Type Type, int Offset) expected)
                || !field.IsPrivate
                || !field.IsInitOnly
                || field.FieldType != expected.Type
                || Marshal.OffsetOf<T>(field.Name).ToInt32()
                    != expected.Offset)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExactSubtractApi()
    {
        MethodInfo? method = typeof(BootReservationSubtractor).GetMethod(
            nameof(BootReservationSubtractor.Subtract),
            BindingFlags.Public | BindingFlags.Static);
        if (method is null
            || method.ReturnType != typeof(BootReservationSubtractionResult))
        {
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        Type sourcePointer =
            typeof(BootReservationSourceRange).MakePointerType();
        Type reservationPointer =
            typeof(BootReservedRange).MakePointerType();
        Type[] expected =
        [
            sourcePointer,
            typeof(ulong),
            typeof(ulong),
            reservationPointer,
            typeof(ulong),
            typeof(ulong),
            typeof(ulong),
            sourcePointer,
            typeof(ulong)
        ];
        if (parameters.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (parameters[index].ParameterType != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static BootReservationSourceRangeStatus FactoryStatus(
        int sourceRangeIndex,
        PhysicalAddress start,
        ulong pageCount)
    {
        return BootReservationSourceRange.TryCreate(
            sourceRangeIndex,
            start,
            pageCount,
            out _);
    }

    private static bool SingleSubtractionCase(
        BootReservationSourceRange source,
        BootReservedRange reservation,
        BootReservationSourceRange[] expected,
        ulong expectedRemovedPages)
    {
        unsafe
        {
            BootReservationSourceRange[] destination =
                new BootReservationSourceRange[expected.Length + 1];
            destination[^1] = Source(77, 0x70000, 1);
            BootReservationSourceRange sentinel = destination[^1];

            fixed (BootReservationSourceRange* destinationPointer = destination)
            {
                BootReservationSubtractionResult result =
                    RawSubtract(
                        &source,
                        1,
                        1,
                        &reservation,
                        1,
                        1,
                        PageSize,
                        destinationPointer,
                        (ulong)destination.Length);
                return IsSuccess(
                        result,
                        1,
                        1,
                        1,
                        1,
                        (ulong)expected.Length,
                        (ulong)destination.Length,
                        source.PageCount,
                        expectedRemovedPages)
                    && RangesEqual(
                        expected,
                        destination.AsSpan(0, expected.Length).ToArray())
                    && destination[^1].Equals(sentinel);
            }
        }
    }

    private static bool FailureStatus(
        BootReservationSourceRange[] source,
        BootReservedRange[] reservations,
        BootReservationSubtractionStatus expectedStatus,
        BootReservationSourceRangeStatus sourceStatus =
            BootReservationSourceRangeStatus.Success,
        BootReservedRangeStatus reservationStatus =
            BootReservedRangeStatus.Success,
        ulong invalidSourceIndex = BootReservationSubtractor.NoIndex,
        ulong invalidReservationIndex = BootReservationSubtractor.NoIndex,
        ulong conflictingIndex = BootReservationSubtractor.NoIndex)
    {
        unsafe
        {
            BootReservationSourceRange[] destination =
            [
                Source(70, 0x70000, 1),
                Source(71, 0x72000, 1)
            ];
            byte[] before = StructArrayBytes(destination);

            fixed (BootReservationSourceRange* sourcePointer = source)
            fixed (BootReservedRange* reservationPointer = reservations)
            fixed (BootReservationSourceRange* destinationPointer = destination)
            {
                BootReservationSubtractionResult result =
                    RawSubtract(
                        sourcePointer,
                        (ulong)source.Length,
                        (ulong)source.Length,
                        reservationPointer,
                        (ulong)reservations.Length,
                        (ulong)reservations.Length,
                        PageSize,
                        destinationPointer,
                        2);
                return result.Status == expectedStatus
                    && result.SourceRangeStatus == sourceStatus
                    && result.ReservationRangeStatus == reservationStatus
                    && result.InvalidSourceRangeIndex == invalidSourceIndex
                    && result.InvalidReservationIndex
                        == invalidReservationIndex
                    && result.ConflictingRangeIndex == conflictingIndex
                    && !result.Succeeded
                    && result.WrittenOutputRangeCount == 0
                    && before.AsSpan().SequenceEqual(
                        StructArrayBytes(destination));
            }
        }
    }

    private static bool OutsideFailure(
        BootReservationSourceRange[] source,
        BootReservedRange[] reservations)
    {
        unsafe
        {
            BootReservationSourceRange[] destination =
            [
                Source(70, 0x70000, 1),
                Source(71, 0x72000, 1),
                Source(72, 0x74000, 1)
            ];
            byte[] before = StructArrayBytes(destination);

            fixed (BootReservationSourceRange* sourcePointer = source)
            fixed (BootReservedRange* reservationPointer = reservations)
            fixed (BootReservationSourceRange* destinationPointer = destination)
            {
                BootReservationSubtractionResult result =
                    RawSubtract(
                        sourcePointer,
                        (ulong)source.Length,
                        (ulong)source.Length,
                        reservationPointer,
                        (ulong)reservations.Length,
                        (ulong)reservations.Length,
                        PageSize,
                        destinationPointer,
                        3);
                return result.Status
                        == BootReservationSubtractionStatus
                            .ReservationOutsideSourceRange
                    && result.InvalidReservationIndex
                        != BootReservationSubtractor.NoIndex
                    && !result.Succeeded
                    && result.WrittenOutputRangeCount == 0
                    && before.AsSpan().SequenceEqual(
                        StructArrayBytes(destination));
            }
        }
    }

    private static bool AtomicFailure(
        BootReservationSourceRange[] source,
        BootReservedRange[] reservations,
        ulong destinationCapacity,
        BootReservationSourceRange sentinel0,
        BootReservationSourceRange sentinel1,
        BootReservationSubtractionStatus expectedStatus)
    {
        unsafe
        {
            BootReservationSourceRange[] destination =
            [
                sentinel0,
                sentinel1
            ];
            byte[] before = StructArrayBytes(destination);

            fixed (BootReservationSourceRange* sourcePointer = source)
            fixed (BootReservedRange* reservationPointer = reservations)
            fixed (BootReservationSourceRange* destinationPointer = destination)
            {
                BootReservationSubtractionResult result =
                    RawSubtract(
                        sourcePointer,
                        (ulong)source.Length,
                        (ulong)source.Length,
                        reservationPointer,
                        (ulong)reservations.Length,
                        (ulong)reservations.Length,
                        PageSize,
                        destinationPointer,
                        destinationCapacity);
                return IsFailure(result, expectedStatus)
                    && before.AsSpan().SequenceEqual(
                        StructArrayBytes(destination));
            }
        }
    }

    private static unsafe bool RequiredOutputOverflowPreservesDestination(
        BootReservationSourceRange sentinel0,
        BootReservationSourceRange sentinel1)
    {
        int sourceCount =
            checked((int)BootReservationSubtractor.MaximumSourceRangeCount);
        BootReservationSourceRange[] source =
            new BootReservationSourceRange[sourceCount];
        source[0] = Source(0, 0x10000, 3);
        for (int index = 1; index < source.Length; index++)
        {
            ulong start = 0x20000 + ((ulong)(index - 1) * 0x2000);
            source[index] = Source(index, start, 1);
        }

        BootReservedRange[] reservations =
        [
            Reservation(BootReservationKind.LoadedImage, 0x11000, 1)
        ];
        BootReservationSourceRange[] destination =
        [
            sentinel0,
            sentinel1
        ];
        byte[] before = StructArrayBytes(destination);

        fixed (BootReservationSourceRange* sourcePointer = source)
        fixed (BootReservedRange* reservationPointer = reservations)
        fixed (BootReservationSourceRange* destinationPointer = destination)
        {
            BootReservationSubtractionResult result =
                RawSubtract(
                    sourcePointer,
                    (ulong)source.Length,
                    (ulong)source.Length,
                    reservationPointer,
                    1,
                    1,
                    PageSize,
                    destinationPointer,
                    2);
            return IsFailure(
                    result,
                    BootReservationSubtractionStatus
                        .RequiredOutputRangeCountOverflow)
                && result.RequiredOutputRangeCount
                    == BootReservationSubtractor.MaximumOutputRangeCount
                && before.AsSpan().SequenceEqual(
                    StructArrayBytes(destination));
        }
    }

    private static unsafe bool PreflightFailurePreservesDestination(
        BootReservationSourceRange* sourceRanges,
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong pageSize,
        ulong destinationRangeCapacity,
        PreflightDestinationMode destinationMode,
        BootReservationSubtractionStatus expectedStatus)
    {
        BootReservationSourceRange* destination =
            stackalloc BootReservationSourceRange[2];
        destination[0] = Source(70, 0x70000, 1);
        destination[1] = Source(71, 0x72000, 1);
        BootReservationSourceRange before0 = destination[0];
        BootReservationSourceRange before1 = destination[1];

        BootReservationSourceRange* callDestination = destination;
        if (destinationMode == PreflightDestinationMode.Null)
        {
            callDestination = null;
        }
        else if (destinationMode
            == PreflightDestinationMode.Misaligned)
        {
            callDestination =
                (BootReservationSourceRange*)((byte*)destination + 1);
        }
        else if (destinationMode == PreflightDestinationMode.Poison)
        {
            callDestination =
                (BootReservationSourceRange*)unchecked(
                    (nuint)0xFFFF_FFFF_FFFF_FFF8UL);
        }

        BootReservationSubtractionResult result = RawSubtract(
            sourceRanges,
            sourceRangeCount,
            sourceRangeCapacity,
            reservations,
            reservationCount,
            reservationCapacity,
            pageSize,
            callDestination,
            destinationRangeCapacity);
        return IsFailure(result, expectedStatus)
            && destination[0].Equals(before0)
            && destination[1].Equals(before1);
    }

    private enum PreflightDestinationMode : byte
    {
        Valid = 0,
        Null = 1,
        Misaligned = 2,
        Poison = 3
    }

    private static unsafe BootReservationSubtractionResult RawSubtract(
        BootReservationSourceRange* sourceRanges,
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong pageSize,
        BootReservationSourceRange* destinationRanges,
        ulong destinationRangeCapacity)
    {
        return BootReservationSubtractor.Subtract(
            sourceRanges,
            sourceRangeCount,
            sourceRangeCapacity,
            reservations,
            reservationCount,
            reservationCapacity,
            pageSize,
            destinationRanges,
            destinationRangeCapacity);
    }

    private static bool IsSuccess(
        BootReservationSubtractionResult result,
        ulong sourceCount,
        ulong sourceCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong outputCount,
        ulong destinationCapacity,
        ulong totalPages,
        ulong removedPages)
    {
        return result.Succeeded
            && result.Status == BootReservationSubtractionStatus.Success
            && result.SourceRangeStatus
                == BootReservationSourceRangeStatus.Success
            && result.ReservationRangeStatus
                == BootReservedRangeStatus.Success
            && result.SourceRangeCount == sourceCount
            && result.SourceRangeCapacity == sourceCapacity
            && result.ReservationCount == reservationCount
            && result.ReservationCapacity == reservationCapacity
            && result.RequiredOutputRangeCount == outputCount
            && result.WrittenOutputRangeCount == outputCount
            && result.AvailableDestinationCapacity
                == destinationCapacity
            && result.InvalidSourceRangeIndex
                == BootReservationSubtractor.NoIndex
            && result.InvalidReservationIndex
                == BootReservationSubtractor.NoIndex
            && result.ConflictingRangeIndex
                == BootReservationSubtractor.NoIndex
            && result.PageSize == PageSize
            && result.TotalSourcePages == totalPages
            && result.TotalSourceBytes == totalPages * PageSize
            && result.RemovedPages == removedPages
            && result.RemovedBytes == removedPages * PageSize
            && result.RemainingPages == totalPages - removedPages
            && result.RemainingBytes
                == (totalPages - removedPages) * PageSize;
    }

    private static bool IsFailure(
        BootReservationSubtractionResult result,
        BootReservationSubtractionStatus expectedStatus)
    {
        return result.Status == expectedStatus
            && !result.Succeeded
            && result.WrittenOutputRangeCount == 0;
    }

    private static bool ResultsEqual(
        BootReservationSubtractionResult left,
        BootReservationSubtractionResult right)
    {
        return left.Status == right.Status
            && left.SourceRangeStatus == right.SourceRangeStatus
            && left.ReservationRangeStatus == right.ReservationRangeStatus
            && left.SourceRangeCount == right.SourceRangeCount
            && left.SourceRangeCapacity == right.SourceRangeCapacity
            && left.ReservationCount == right.ReservationCount
            && left.ReservationCapacity == right.ReservationCapacity
            && left.RequiredOutputRangeCount
                == right.RequiredOutputRangeCount
            && left.WrittenOutputRangeCount
                == right.WrittenOutputRangeCount
            && left.AvailableDestinationCapacity
                == right.AvailableDestinationCapacity
            && left.InvalidSourceRangeIndex
                == right.InvalidSourceRangeIndex
            && left.InvalidReservationIndex
                == right.InvalidReservationIndex
            && left.ConflictingRangeIndex == right.ConflictingRangeIndex
            && left.PageSize == right.PageSize
            && left.TotalSourcePages == right.TotalSourcePages
            && left.TotalSourceBytes == right.TotalSourceBytes
            && left.RemovedPages == right.RemovedPages
            && left.RemovedBytes == right.RemovedBytes
            && left.RemainingPages == right.RemainingPages
            && left.RemainingBytes == right.RemainingBytes
            && left.Succeeded == right.Succeeded;
    }

    private static BootReservationSubtractionResult InvokeSuccessFactory(
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong requiredOutputRangeCount,
        ulong writtenOutputRangeCount,
        ulong availableDestinationCapacity,
        ulong totalSourcePages,
        ulong removedPages,
        ulong remainingPages)
    {
        MethodInfo? success = typeof(BootReservationSubtractionResult)
            .GetMethod(
                "Success",
                BindingFlags.Static | BindingFlags.NonPublic);
        if (success is null)
        {
            throw new MissingMethodException(
                nameof(BootReservationSubtractionResult),
                "Success");
        }

        object? boxed = success.Invoke(
            null,
            [
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                writtenOutputRangeCount,
                availableDestinationCapacity,
                PageSize,
                totalSourcePages,
                totalSourcePages * PageSize,
                removedPages,
                removedPages * PageSize,
                remainingPages,
                remainingPages * PageSize
            ]);
        if (boxed is not BootReservationSubtractionResult result)
        {
            throw new InvalidOperationException(
                "Internal subtraction success factory returned no result.");
        }

        return result;
    }

    private static BootReservationSourceRange Source(
        int sourceRangeIndex,
        ulong physicalStart,
        ulong pageCount)
    {
        BootReservationSourceRangeStatus status =
            BootReservationSourceRange.TryCreate(
                sourceRangeIndex,
                Address(physicalStart),
                pageCount,
                out BootReservationSourceRange range);
        if (status != BootReservationSourceRangeStatus.Success)
        {
            throw new InvalidOperationException(
                "Could not create source range: " + status);
        }

        return range;
    }

    private static BootReservedRange Reservation(
        BootReservationKind kind,
        ulong physicalStart,
        ulong pageCount)
    {
        BootReservedRangeStatus status = BootReservedRange.TryCreate(
            kind,
            Address(physicalStart),
            pageCount,
            out BootReservedRange range);
        if (status != BootReservedRangeStatus.Success)
        {
            throw new InvalidOperationException(
                "Could not create reservation: " + status);
        }

        return range;
    }

    private static PhysicalAddress Address(ulong value)
    {
        AddressValueStatus status =
            PhysicalAddress.TryCreateFromUefiBoundary(
                value,
                out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Could not create physical address: " + status);
        }

        return address;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        AddressValueStatus status =
            address.TryGetRawValueForUefiBoundary(out ulong value);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Could not read physical address: " + status);
        }

        return value;
    }

    private static bool RangesEqual(
        BootReservationSourceRange[] left,
        BootReservationSourceRange[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] StructArrayBytes<T>(T[] values)
        where T : struct
    {
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private static unsafe byte[] PointerBytes<T>(
        T* pointer,
        int count)
        where T : unmanaged
    {
        return new ReadOnlySpan<byte>(
            pointer,
            checked(count * sizeof(T))).ToArray();
    }

    private static byte[] StructBytes<T>(T value)
        where T : struct
    {
        ReadOnlySpan<T> values =
            MemoryMarshal.CreateReadOnlySpan(ref value, 1);
        return MemoryMarshal.AsBytes(values).ToArray();
    }

    private static T MutateByte<T>(
        T value,
        int byteOffset,
        byte replacement)
        where T : struct
    {
        Span<T> values = MemoryMarshal.CreateSpan(ref value, 1);
        Span<byte> bytes = MemoryMarshal.AsBytes(values);
        bytes[byteOffset] = replacement;
        return value;
    }

    private static string RemoveComments(string source)
    {
        string withoutBlocks = Regex.Replace(
            source,
            @"/\*.*?\*/",
            string.Empty,
            RegexOptions.Singleline
                | RegexOptions.CultureInvariant);
        return Regex.Replace(
            withoutBlocks,
            @"//.*?$",
            string.Empty,
            RegexOptions.Multiline
                | RegexOptions.CultureInvariant);
    }

    private static bool SourceFailure(string reason)
    {
        Console.WriteLine(
            "M01_007_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
        return false;
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int offset = 0;
        while (offset <= source.Length - value.Length)
        {
            int found = source.IndexOf(
                value,
                offset,
                StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            count++;
            offset = found + value.Length;
        }

        return count;
    }

    private static bool HasTextExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedOrReferencePath(string path)
    {
        string[] excludedSegments =
        [
            "bin",
            "obj",
            "artifacts",
            "references",
            ".git"
        ];
        foreach (string segment in excludedSegments)
        {
            string needle =
                Path.DirectorySeparatorChar
                + segment
                + Path.DirectorySeparatorChar;
            if (path.Contains(
                    needle,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-007-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.Copy(
                Path.Combine(repositoryRoot, "global.json"),
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
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                namespace CLROS.M01.CompileClosure;

                public static unsafe class Probe
                {
                    public static bool Run()
                    {
                        OS.Boot.AddressValueStatus addressStatus =
                            OS.Boot.PhysicalAddress
                                .TryCreateAlignedFromUefiBoundary(
                                    0x10000,
                                    4096,
                                    out OS.Boot.PhysicalAddress sourceStart);
                        if (addressStatus != OS.Boot.AddressValueStatus.Success)
                        {
                            return false;
                        }

                        OS.Kernel.Memory.BootReservationSourceRangeStatus
                            sourceStatus =
                                OS.Kernel.Memory.BootReservationSourceRange
                                    .TryCreate(
                                        0,
                                        sourceStart,
                                        2,
                                        out OS.Kernel.Memory
                                            .BootReservationSourceRange source);
                        if (sourceStatus
                            != OS.Kernel.Memory
                                .BootReservationSourceRangeStatus.Success)
                        {
                            return false;
                        }

                        OS.Kernel.Memory.BootReservedRangeStatus
                            reservationStatus =
                                OS.Kernel.Memory.BootReservedRange.TryCreate(
                                    OS.Kernel.Memory.BootReservationKind
                                        .LoadedImage,
                                    sourceStart,
                                    1,
                                    out OS.Kernel.Memory
                                        .BootReservedRange reservation);
                        if (reservationStatus
                            != OS.Kernel.Memory
                                .BootReservedRangeStatus.Success)
                        {
                            return false;
                        }

                        OS.Kernel.Memory.BootReservationSourceRange output =
                            default;
                        OS.Kernel.Memory.BootReservationSubtractionResult
                            result =
                                OS.Kernel.Memory.BootReservationSubtractor
                                    .Subtract(
                                        &source,
                                        1,
                                        1,
                                        &reservation,
                                        1,
                                        1,
                                        4096,
                                        &output,
                                        1);
                        return result.Succeeded
                            && result.RequiredOutputRangeCount == 1
                            && result.WrittenOutputRangeCount == 1
                            && output.IsValid
                            && output.SourceRangeIndex == 0
                            && output.PageCount == 1;
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.BootReservationSubtractorCompileClosure</AssemblyName>
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
                <Compile Include="NoStdLibShim.cs" />
                <Compile Include="__ADDRESSES__" Link="Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs" />
                <Compile Include="__RESERVATION__" Link="Memory\BootReservedRange.cs" />
                <Compile Include="__SUBTRACTOR__" Link="Memory\BootReservationSubtractor.cs" />
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
            """
            .Replace(
                "__ADDRESSES__",
                EscapeXml(GetAddressSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__RESERVATION__",
                EscapeXml(GetReservationSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__SUBTRACTOR__",
                EscapeXml(GetSubtractorSourcePath()),
                StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(workspace, "M01.007.NoStdLib.csproj"),
                projectText,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            return workspace;
        }
        catch
        {
            try
            {
                DeleteNoStdLibWorkspace(workspace);
            }
            catch
            {
            }

            throw;
        }
    }

    private static NoStdLibBuildResult RunNoStdLibBuild(
        string workspace)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspace
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add("M01.007.NoStdLib.csproj");
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

        using Process process = new Process { StartInfo = start };
        if (!process.Start())
        {
            return new NoStdLibBuildResult(
                -1,
                timedOut: false,
                "dotnet process did not start");
        }

        Task<string> standardOutput =
            process.StandardOutput.ReadToEndAsync();
        Task<string> standardError =
            process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90_000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            process.WaitForExit(5_000);
        }

        string output = standardOutput.Wait(5_000)
            ? standardOutput.Result
            : "stdout did not close";
        string error = standardError.Wait(5_000)
            ? standardError.Result
            : "stderr did not close";
        return new NoStdLibBuildResult(
            exited ? process.ExitCode : -1,
            timedOut: !exited,
            output + Environment.NewLine + error);
    }

    private static bool HasZeroAssemblyReferences(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader reader = new PEReader(stream);
        if (!reader.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = reader.GetMetadataReader();
        return metadata.AssemblyReferences.Count == 0;
    }

    private static string NormalizeCompilerOutput(string output)
    {
        string normalized = Regex.Replace(
            output,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim();
        return normalized.Length <= 1500
            ? normalized
            : normalized[..1500] + "...";
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string GetAddressSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
    }

    private static string GetReservationSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootReservedRange.cs");
    }

    private static string GetSubtractorSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootReservationSubtractor.cs");
    }

    private static string GetTestSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "tests",
            "OS.HostTests",
            "BootReservationSubtractorTests.cs");
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string fullWorkspace = Path.GetFullPath(workspace)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string? parent = Directory.GetParent(fullWorkspace)?.FullName
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string leaf = Path.GetFileName(fullWorkspace);

        if (!string.Equals(
                parent,
                tempRoot,
                StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(
                leaf,
                @"\Aclros-m01-007-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-007 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        FileAttributes attributes =
            File.GetAttributes(fullWorkspace);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Refusing to delete a reparse-point M01-007 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    private readonly struct NoStdLibBuildResult
    {
        public NoStdLibBuildResult(
            int exitCode,
            bool timedOut,
            string output)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
        }

        public int ExitCode { get; }

        public bool TimedOut { get; }

        public string Output { get; }
    }
}
