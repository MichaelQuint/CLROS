using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Memory;

internal static class EfiLoadedImageRangeCapturePlanTests
{
    private const string IssueTestName = "m01.003";

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run("m01.003 statuses and pod layouts are stable", TestStableLayouts, ref failures);
        Run("m01.003 default values fail closed", TestDefaultsFailClosed, ref failures);
        Run("m01.003 aligned image produces canonical reservation", TestAlignedSuccess, ref failures);
        Run("m01.003 outward rounding preserves exact extent", TestOutwardRounding, ref failures);
        Run("m01.003 boundary sized image remains checked", TestBoundarySuccess, ref failures);
        Run("m01.003 pointer count and capacity fail before dereference", TestPointerContract, ref failures);
        Run("m01.003 invalid page sizes fail before observation use", TestPageSizeFailures, ref failures);
        Run("m01.003 firmware transcript failures are classified", TestFirmwareTranscriptFailures, ref failures);
        Run("m01.003 image range failures are classified", TestImageRangeFailures, ref failures);
        Run("m01.003 validation precedence is deterministic", TestFailurePrecedence, ref failures);
        Run("m01.003 canonical reservation is accepted by ledger validator", TestCanonicalReservationIntegration, ref failures);
        Run("m01.003 malformed result values cannot report success", TestMalformedResultsFailClosed, ref failures);
        Run("m01.003 evaluation is nonmutating deterministic and allocation free", TestDeterminismImmutabilityAndAllocation, ref failures);
        Run("m01.003 result does not retain firmware protocol pointer", TestProtocolPointerIsNotRetained, ref failures);
        Run("m01.003 sources exclude hosted and live firmware dependencies", TestNoRuntimeSourceSurface, ref failures);
        Run("m01.003 exact sources compile under nostdlib efi", TestNoStdLibEfiCompileClosure, ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-003 loaded-image range capture host tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_003_READY");
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

    private static bool TestStableLayouts()
    {
        (string Name, byte Value)[] statuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("InvalidObservationCount", 2),
            ("TooManyObservations", 3),
            ("ObservationCapacityTooSmall", 4),
            ("MissingObservationBuffer", 5),
            ("MisalignedObservationBuffer", 6),
            ("ObservationAddressRangeOverflow", 7),
            ("InvalidPageSize", 8),
            ("InvalidImageHandle", 9),
            ("FirmwareFailure", 10),
            ("MissingProtocolPointer", 11),
            ("MisalignedProtocolPointer", 12),
            ("InvalidImageBase", 13),
            ("InvalidImageSize", 14),
            ("ImageEndOverflow", 15),
            ("ReservationStartZero", 16),
            ("ReservationRoundingOverflow", 17),
            ("ReservationRangeInvalid", 18),
            ("ReservationRejected", 19),
            ("ReservationEndMismatch", 20)
        ];

        if (Enum.GetUnderlyingType(
                typeof(EfiLoadedImageRangeCaptureStatus)) != typeof(byte)
            || Enum.GetValues<EfiLoadedImageRangeCaptureStatus>().Length
                != statuses.Length)
        {
            return false;
        }

        foreach ((string name, byte value) in statuses)
        {
            EfiLoadedImageRangeCaptureStatus status =
                (EfiLoadedImageRangeCaptureStatus)value;
            if (Enum.GetName(status) != name)
            {
                return false;
            }
        }

        return EfiLoadedImageRange.PageSize == BootReservedRange.PageSize
            && EfiLoadedImageRangeCapturePlan.EfiSuccess == 0
            && EfiLoadedImageRangeCapturePlan.RequiredObservationCount == 1
            && EfiLoadedImageRangeCapturePlan.MaximumObservationCount == 1
            && EfiLoadedImageRangeCapturePlan.NoIndex == ulong.MaxValue
            && IsReadOnlyPod<EfiLoadedImageProtocolObservation>(40)
            && IsReadOnlyPod<EfiLoadedImageRange>(80)
            && IsReadOnlyPod<EfiLoadedImageRangeCapturePlan>(144)
            && HasOnlyPrivateConstructors<
                EfiLoadedImageProtocolObservation>()
            && HasOnlyPrivateConstructors<EfiLoadedImageRange>()
            && HasOnlyPrivateConstructors<
                EfiLoadedImageRangeCapturePlan>()
            && HasOffsets<EfiLoadedImageProtocolObservation>(
                [
                    ("_firmwareStatus", 0),
                    ("_imageHandleFromUefi", 8),
                    ("_protocolAddressFromUefi", 16),
                    ("_imageBaseFromUefi", 24),
                    ("_imageSizeFromUefi", 32)
                ])
            && HasOffsets<EfiLoadedImageRange>(
                [
                    ("_imageBase", 0),
                    ("_imageEndExclusive", 8),
                    ("_imageSize", 16),
                    ("_reservation", 24),
                    ("_leadingPaddingBytes", 64),
                    ("_trailingPaddingBytes", 72)
                ])
            && HasOffsets<EfiLoadedImageRangeCapturePlan>(
                [
                    ("_status", 0),
                    ("_addressStatus", 1),
                    ("_reservationStatus", 2),
                    ("_reserved0", 3),
                    ("_reserved1", 4),
                    ("_reserved2", 5),
                    ("_reserved3", 6),
                    ("_reserved4", 7),
                    ("_observationCount", 8),
                    ("_validatedObservationCount", 16),
                    ("_availableObservationCapacity", 24),
                    ("_invalidObservationIndex", 32),
                    ("_pageSize", 40),
                    ("_firmwareStatus", 48),
                    ("_imageHandle", 56),
                    ("_range", 64)
                ]);
    }

    private static bool IsReadOnlyPod<T>(int expectedSize)
        where T : struct
    {
        Type type = typeof(T);
        return type.IsValueType
            && type.IsLayoutSequential
            && type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            && Marshal.SizeOf<T>() == expectedSize
            && Unsafe.SizeOf<T>() == expectedSize
            && type.GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic)
                .All(field =>
                    field.IsInitOnly
                    && field.IsPrivate
                    && field.FieldType.IsValueType
                    && !field.FieldType.IsPointer);
    }

    private static bool HasOnlyPrivateConstructors<T>()
        where T : struct
    {
        ConstructorInfo[] constructors = typeof(T).GetConstructors(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
        return constructors.Length == 1
            && constructors[0].IsPrivate;
    }

    private static bool HasOffsets<T>(
        (string Name, int Offset)[] expected)
        where T : struct
    {
        FieldInfo[] fields = typeof(T).GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
        if (fields.Length != expected.Length)
        {
            return false;
        }

        foreach ((string name, int offset) in expected)
        {
            FieldInfo? field = fields.SingleOrDefault(
                candidate => candidate.Name == name);
            if (field is null
                || Marshal.OffsetOf<T>(name).ToInt64() != offset)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestDefaultsFailClosed()
    {
        EfiLoadedImageProtocolObservation observation = default;
        EfiLoadedImageRange range = default;
        EfiLoadedImageRangeCapturePlan plan = default;

        return !range.IsValid
            && !plan.Succeeded
            && plan.Status
                == EfiLoadedImageRangeCaptureStatus.Uninitialized
            && plan.AddressStatus == AddressValueStatus.Uninitialized
            && plan.ReservationStatus
                == BootReservedRangeStatus.Uninitialized
            && plan.ObservationCount == 0
            && plan.ValidatedObservationCount == 0
            && plan.AvailableObservationCapacity == 0
            && plan.RequiredObservationCapacity == 1
            && plan.InvalidObservationIndex == 0
            && plan.PageSize == 0
            && plan.FirmwareStatus == 0
            && plan.ImageHandle == 0
            && plan.Range.Equals(default)
            && observation.Equals(default);
    }

    private static bool TestAlignedSuccess()
    {
        const ulong imageBase = 0x00400000;
        const ulong imageSize = 0x00003000;
        EfiLoadedImageRangeCapturePlan plan = Evaluate(
            Observation(
                firmwareStatus: 0,
                imageHandle: 0x11110000,
                protocolAddress: 0x22220000,
                imageBase,
                imageSize));

        return IsSuccessEvidence(plan, capacity: 1)
            && Raw(plan.Range.ImageBase) == imageBase
            && Raw(plan.Range.ImageEndExclusive)
                == imageBase + imageSize
            && plan.Range.ImageSize == imageSize
            && plan.Range.LeadingPaddingBytes == 0
            && plan.Range.TrailingPaddingBytes == 0
            && plan.Range.Reservation.Kind
                == BootReservationKind.LoadedImage
            && Raw(plan.Range.Reservation.PhysicalStart) == imageBase
            && Raw(plan.Range.Reservation.PhysicalEndExclusive)
                == imageBase + imageSize
            && plan.Range.Reservation.PageCount == 3
            && plan.Range.Reservation.ByteLength == imageSize;
    }

    private static bool TestOutwardRounding()
    {
        EfiLoadedImageRangeCapturePlan unaligned = Evaluate(
            Observation(0, 0x1010, 0x2020, 0x00401123, 0x2234));
        EfiLoadedImageRangeCapturePlan oneByte = Evaluate(
            Observation(0, 0x3030, 0x4040, 0x00500001, 1));
        EfiLoadedImageRangeCapturePlan alignedEnd = Evaluate(
            Observation(0, 0x5050, 0x6060, 0x00600123, 0xEDD));
        EfiLoadedImageRangeCapturePlan crossing = Evaluate(
            Observation(0, 0x7070, 0x8080, 0x1FFF, 2));
        EfiLoadedImageRangeCapturePlan highestPage = Evaluate(
            Observation(
                0,
                0x9090,
                0xA0A0,
                ulong.MaxValue - 0x1000,
                1));

        return IsSuccessEvidence(unaligned, 1)
            && Raw(unaligned.Range.ImageBase) == 0x00401123
            && Raw(unaligned.Range.ImageEndExclusive) == 0x00403357
            && Raw(unaligned.Range.Reservation.PhysicalStart)
                == 0x00401000
            && Raw(unaligned.Range.Reservation.PhysicalEndExclusive)
                == 0x00404000
            && unaligned.Range.Reservation.PageCount == 3
            && unaligned.Range.LeadingPaddingBytes == 0x123
            && unaligned.Range.TrailingPaddingBytes == 0xCA9
            && IsSuccessEvidence(oneByte, 1)
            && oneByte.Range.LeadingPaddingBytes == 1
            && oneByte.Range.TrailingPaddingBytes == 4094
            && oneByte.Range.Reservation.PageCount == 1
            && IsSuccessEvidence(alignedEnd, 1)
            && Raw(alignedEnd.Range.ImageEndExclusive) == 0x00601000
            && alignedEnd.Range.LeadingPaddingBytes == 0x123
            && alignedEnd.Range.TrailingPaddingBytes == 0
            && alignedEnd.Range.Reservation.PageCount == 1
            && IsSuccessEvidence(crossing, 1)
            && Raw(crossing.Range.ImageEndExclusive) == 0x2001
            && Raw(crossing.Range.Reservation.PhysicalStart) == 0x1000
            && Raw(crossing.Range.Reservation.PhysicalEndExclusive)
                == 0x3000
            && crossing.Range.Reservation.PageCount == 2
            && crossing.Range.LeadingPaddingBytes == 0xFFF
            && crossing.Range.TrailingPaddingBytes == 0xFFF
            && IsSuccessEvidence(highestPage, 1)
            && Raw(highestPage.Range.ImageBase)
                == ulong.MaxValue - 0x1000
            && Raw(highestPage.Range.ImageEndExclusive)
                == ulong.MaxValue - 0xFFF
            && Raw(highestPage.Range.Reservation.PhysicalStart)
                == ulong.MaxValue - 0x1FFF
            && Raw(highestPage.Range.Reservation.PhysicalEndExclusive)
                == ulong.MaxValue - 0xFFF
            && highestPage.Range.Reservation.PageCount == 1
            && highestPage.Range.LeadingPaddingBytes == 0xFFF
            && highestPage.Range.TrailingPaddingBytes == 0;
    }

    private static bool TestBoundarySuccess()
    {
        const ulong imageBase = 0x1000;
        const ulong reservationEnd = ulong.MaxValue - 0xFFF;
        const ulong imageSize = reservationEnd - imageBase;
        EfiLoadedImageRangeCapturePlan plan = Evaluate(
            Observation(
                0,
                0x7000,
                0x8000,
                imageBase,
                imageSize),
            capacity: ulong.MaxValue);

        return IsSuccessEvidence(plan, ulong.MaxValue)
            && Raw(plan.Range.ImageEndExclusive) == reservationEnd
            && Raw(plan.Range.Reservation.PhysicalEndExclusive)
                == reservationEnd
            && plan.Range.Reservation.ByteLength == imageSize
            && plan.Range.Reservation.PageCount
                == imageSize / BootReservedRange.PageSize
            && plan.Range.LeadingPaddingBytes == 0
            && plan.Range.TrailingPaddingBytes == 0;
    }

    private static unsafe bool TestPointerContract()
    {
        EfiLoadedImageProtocolObservation observation =
            Observation(0, 0x1000, 0x2000, 0x3000, 0x1000);
        EfiLoadedImageRangeCapturePlan zeroCount =
            EfiLoadedImageRangeCapturePlan.Create(
                null,
                0,
                0,
                0);
        EfiLoadedImageRangeCapturePlan tooMany =
            EfiLoadedImageRangeCapturePlan.Create(
                (EfiLoadedImageProtocolObservation*)1,
                2,
                0,
                0);
        EfiLoadedImageRangeCapturePlan capacity =
            EfiLoadedImageRangeCapturePlan.Create(
                (EfiLoadedImageProtocolObservation*)1,
                1,
                0,
                0);
        EfiLoadedImageRangeCapturePlan missing =
            EfiLoadedImageRangeCapturePlan.Create(
                null,
                1,
                1,
                EfiLoadedImageRange.PageSize);
        EfiLoadedImageRangeCapturePlan misaligned =
            EfiLoadedImageRangeCapturePlan.Create(
                (EfiLoadedImageProtocolObservation*)
                    ((byte*)&observation + 1),
                1,
                1,
                EfiLoadedImageRange.PageSize);
        EfiLoadedImageRangeCapturePlan wrapping =
            EfiLoadedImageRangeCapturePlan.Create(
                (EfiLoadedImageProtocolObservation*)
                    unchecked((nuint)(ulong.MaxValue - 7)),
                1,
                1,
                EfiLoadedImageRange.PageSize);
        EfiLoadedImageProtocolObservation* two =
            stackalloc EfiLoadedImageProtocolObservation[2];
        two[0] = observation;
        two[1] = Observation(
            ulong.MaxValue,
            0,
            1,
            0,
            0);
        EfiLoadedImageProtocolObservation poisonBefore = two[1];
        EfiLoadedImageRangeCapturePlan ignoresSecond =
            EfiLoadedImageRangeCapturePlan.Create(
                two,
                1,
                2,
                EfiLoadedImageRange.PageSize);

        return IsPreObservationFailure(
                zeroCount,
                EfiLoadedImageRangeCaptureStatus.InvalidObservationCount,
                AddressValueStatus.Uninitialized,
                count: 0,
                capacity: 0,
                pageSize: 0)
            && IsPreObservationFailure(
                tooMany,
                EfiLoadedImageRangeCaptureStatus.TooManyObservations,
                AddressValueStatus.Uninitialized,
                count: 2,
                capacity: 0,
                pageSize: 0)
            && IsPreObservationFailure(
                capacity,
                EfiLoadedImageRangeCaptureStatus
                    .ObservationCapacityTooSmall,
                AddressValueStatus.Uninitialized,
                count: 1,
                capacity: 0,
                pageSize: 0)
            && IsPreObservationFailure(
                missing,
                EfiLoadedImageRangeCaptureStatus.MissingObservationBuffer,
                AddressValueStatus.Uninitialized,
                count: 1,
                capacity: 1,
                pageSize: EfiLoadedImageRange.PageSize)
            && IsPreObservationFailure(
                misaligned,
                EfiLoadedImageRangeCaptureStatus
                    .MisalignedObservationBuffer,
                AddressValueStatus.MisalignedAddress,
                count: 1,
                capacity: 1,
                pageSize: EfiLoadedImageRange.PageSize)
            && IsPreObservationFailure(
                wrapping,
                EfiLoadedImageRangeCaptureStatus
                    .ObservationAddressRangeOverflow,
                AddressValueStatus.AddressOverflow,
                count: 1,
                capacity: 1,
                pageSize: EfiLoadedImageRange.PageSize)
            && IsSuccessEvidence(ignoresSecond, capacity: 2)
            && two[1].Equals(poisonBefore);
    }

    private static bool TestPageSizeFailures()
    {
        EfiLoadedImageProtocolObservation invalidTranscript =
            Observation(
                firmwareStatus: 0x8000000000000001,
                imageHandle: 0,
                protocolAddress: 0,
                imageBase: 0,
                imageSize: 0);
        ulong[] invalidPageSizes =
        [
            0,
            1,
            2048,
            4095,
            4097,
            8192,
            ulong.MaxValue
        ];

        foreach (ulong pageSize in invalidPageSizes)
        {
            EfiLoadedImageRangeCapturePlan plan = Evaluate(
                invalidTranscript,
                pageSize: pageSize);
            if (!IsPreObservationFailure(
                    plan,
                    EfiLoadedImageRangeCaptureStatus.InvalidPageSize,
                    AddressValueStatus.InvalidAlignment,
                    count: 1,
                    capacity: 1,
                    pageSize))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestFirmwareTranscriptFailures()
    {
        EfiLoadedImageRangeCapturePlan invalidHandle = Evaluate(
            Observation(
                firmwareStatus: 0x8000000000000001,
                imageHandle: 0,
                protocolAddress: 0,
                imageBase: 0,
                imageSize: 0));
        EfiLoadedImageRangeCapturePlan firmwareError = Evaluate(
            Observation(
                firmwareStatus: 0x8000000000000002,
                imageHandle: 0x1000,
                protocolAddress: 0,
                imageBase: 0,
                imageSize: 0));
        EfiLoadedImageRangeCapturePlan firmwareWarning = Evaluate(
            Observation(
                firmwareStatus: 1,
                imageHandle: 0x1000,
                protocolAddress: 0x2000,
                imageBase: 0x3000,
                imageSize: 0x1000));
        EfiLoadedImageRangeCapturePlan missingProtocol = Evaluate(
            Observation(0, 0x1000, 0, 0x3000, 0x1000));
        EfiLoadedImageRangeCapturePlan misalignedProtocol = Evaluate(
            Observation(0, 0x1000, 0x2001, 0x3000, 0x1000));

        return IsObservationFailure(
                invalidHandle,
                EfiLoadedImageRangeCaptureStatus.InvalidImageHandle,
                AddressValueStatus.ZeroAddress,
                BootReservedRangeStatus.Uninitialized,
                firmwareStatus: 0x8000000000000001,
                imageHandle: 0)
            && IsObservationFailure(
                firmwareError,
                EfiLoadedImageRangeCaptureStatus.FirmwareFailure,
                AddressValueStatus.Uninitialized,
                BootReservedRangeStatus.Uninitialized,
                firmwareStatus: 0x8000000000000002,
                imageHandle: 0x1000)
            && IsObservationFailure(
                firmwareWarning,
                EfiLoadedImageRangeCaptureStatus.FirmwareFailure,
                AddressValueStatus.Uninitialized,
                BootReservedRangeStatus.Uninitialized,
                firmwareStatus: 1,
                imageHandle: 0x1000)
            && IsObservationFailure(
                missingProtocol,
                EfiLoadedImageRangeCaptureStatus.MissingProtocolPointer,
                AddressValueStatus.ZeroAddress,
                BootReservedRangeStatus.Uninitialized,
                firmwareStatus: 0,
                imageHandle: 0x1000)
            && IsObservationFailure(
                misalignedProtocol,
                EfiLoadedImageRangeCaptureStatus.MisalignedProtocolPointer,
                AddressValueStatus.MisalignedAddress,
                BootReservedRangeStatus.Uninitialized,
                firmwareStatus: 0,
                imageHandle: 0x1000);
    }

    private static bool TestImageRangeFailures()
    {
        EfiLoadedImageRangeCapturePlan zeroBase = Evaluate(
            Observation(0, 0x1000, 0x2000, 0, 0x1000));
        EfiLoadedImageRangeCapturePlan zeroSize = Evaluate(
            Observation(0, 0x1000, 0x2000, 0x3000, 0));
        EfiLoadedImageRangeCapturePlan endOverflow = Evaluate(
            Observation(
                0,
                0x1000,
                0x2000,
                ulong.MaxValue - 8,
                9));
        EfiLoadedImageRangeCapturePlan pageZero = Evaluate(
            Observation(0, 0x1000, 0x2000, 1, 1));
        EfiLoadedImageRangeCapturePlan roundingOverflow = Evaluate(
            Observation(
                0,
                0x1000,
                0x2000,
                0x1000,
                ulong.MaxValue - 0x1000));

        return IsObservationFailure(
                zeroBase,
                EfiLoadedImageRangeCaptureStatus.InvalidImageBase,
                AddressValueStatus.ZeroAddress,
                BootReservedRangeStatus.Uninitialized,
                0,
                0x1000)
            && IsObservationFailure(
                zeroSize,
                EfiLoadedImageRangeCaptureStatus.InvalidImageSize,
                AddressValueStatus.ZeroLength,
                BootReservedRangeStatus.Uninitialized,
                0,
                0x1000)
            && IsObservationFailure(
                endOverflow,
                EfiLoadedImageRangeCaptureStatus.ImageEndOverflow,
                AddressValueStatus.AddressOverflow,
                BootReservedRangeStatus.Uninitialized,
                0,
                0x1000)
            && IsObservationFailure(
                pageZero,
                EfiLoadedImageRangeCaptureStatus.ReservationStartZero,
                AddressValueStatus.ZeroAddress,
                BootReservedRangeStatus.InvalidPhysicalStart,
                0,
                0x1000)
            && IsObservationFailure(
                roundingOverflow,
                EfiLoadedImageRangeCaptureStatus
                    .ReservationRoundingOverflow,
                AddressValueStatus.AddressOverflow,
                BootReservedRangeStatus.AddressRangeOverflow,
                0,
                0x1000);
    }

    private static unsafe bool TestFailurePrecedence()
    {
        EfiLoadedImageProtocolObservation allInvalid =
            Observation(
                firmwareStatus: 9,
                imageHandle: 0,
                protocolAddress: 1,
                imageBase: 0,
                imageSize: 0);

        EfiLoadedImageRangeCapturePlan countDominates =
            EfiLoadedImageRangeCapturePlan.Create(
                (EfiLoadedImageProtocolObservation*)1,
                0,
                0,
                0);
        EfiLoadedImageRangeCapturePlan capacityDominates =
            EfiLoadedImageRangeCapturePlan.Create(
                (EfiLoadedImageProtocolObservation*)1,
                1,
                0,
                0);
        EfiLoadedImageRangeCapturePlan pageDominates = Evaluate(
            allInvalid,
            pageSize: 0);
        EfiLoadedImageRangeCapturePlan handleDominates = Evaluate(allInvalid);
        EfiLoadedImageRangeCapturePlan firmwareDominates = Evaluate(
            Observation(9, 0x1000, 1, 0, 0));
        EfiLoadedImageRangeCapturePlan protocolDominates = Evaluate(
            Observation(0, 0x1000, 0, 0, 0));
        EfiLoadedImageRangeCapturePlan baseDominates = Evaluate(
            Observation(0, 0x1000, 0x2000, 0, 0));

        return countDominates.Status
                == EfiLoadedImageRangeCaptureStatus.InvalidObservationCount
            && capacityDominates.Status
                == EfiLoadedImageRangeCaptureStatus
                    .ObservationCapacityTooSmall
            && pageDominates.Status
                == EfiLoadedImageRangeCaptureStatus.InvalidPageSize
            && handleDominates.Status
                == EfiLoadedImageRangeCaptureStatus.InvalidImageHandle
            && firmwareDominates.Status
                == EfiLoadedImageRangeCaptureStatus.FirmwareFailure
            && protocolDominates.Status
                == EfiLoadedImageRangeCaptureStatus.MissingProtocolPointer
            && baseDominates.Status
                == EfiLoadedImageRangeCaptureStatus.InvalidImageBase;
    }

    private static unsafe bool TestCanonicalReservationIntegration()
    {
        EfiLoadedImageRangeCapturePlan plan = Evaluate(
            Observation(0, 0x1000, 0x2000, 0x401123, 0x2234));
        if (!plan.Succeeded)
        {
            return false;
        }

        BootReservedRange reservation = plan.Range.Reservation;
        BootReservationTableValidationResult validation =
            BootReservationTableValidator.Validate(
                &reservation,
                reservationCount: 1,
                reservationCapacity: 1);

        return validation.Succeeded
            && validation.Status == BootReservationTableStatus.Success
            && validation.RangeStatus == BootReservedRangeStatus.Success
            && validation.FirstLoadedImageIndex == 0
            && validation.ValidatedReservationCount == 1
            && validation.TotalPages == reservation.PageCount
            && validation.TotalBytes == reservation.ByteLength;
    }

    private static bool TestMalformedResultsFailClosed()
    {
        EfiLoadedImageRangeCapturePlan valid = Evaluate(
            Observation(0, 0x1000, 0x2000, 0x401123, 0x2234));
        if (!valid.Succeeded)
        {
            return false;
        }

        RawCapturePlan raw = Unsafe.As<
            EfiLoadedImageRangeCapturePlan,
            RawCapturePlan>(ref valid);

        RawCapturePlan[] malformed =
        [
            raw with { Status = EfiLoadedImageRangeCaptureStatus.Uninitialized },
            raw with { AddressStatus = AddressValueStatus.Uninitialized },
            raw with { ReservationStatus = BootReservedRangeStatus.Uninitialized },
            raw with { Reserved0 = 1 },
            raw with { Reserved1 = 1 },
            raw with { Reserved2 = 1 },
            raw with { Reserved3 = 1 },
            raw with { Reserved4 = 1 },
            raw with { ObservationCount = 0 },
            raw with { ValidatedObservationCount = 0 },
            raw with { AvailableObservationCapacity = 0 },
            raw with { InvalidObservationIndex = 0 },
            raw with { PageSize = 8192 },
            raw with { FirmwareStatus = 1 },
            raw with { ImageHandle = 0 },
            raw with { Range = default }
        ];

        foreach (RawCapturePlan malformedRaw in malformed)
        {
            RawCapturePlan copy = malformedRaw;
            EfiLoadedImageRangeCapturePlan malformedPlan = Unsafe.As<
                RawCapturePlan,
                EfiLoadedImageRangeCapturePlan>(ref copy);
            if (malformedPlan.Succeeded)
            {
                return false;
            }
        }

        EfiLoadedImageRange range = valid.Range;
        RawLoadedImageRange rawRange = Unsafe.As<
            EfiLoadedImageRange,
            RawLoadedImageRange>(ref range);
        BootReservedRange reservation = rawRange.Reservation;
        RawBootReservedRange rawReservation = Unsafe.As<
            BootReservedRange,
            RawBootReservedRange>(ref reservation);

        RawLoadedImageRange[] malformedRanges =
        [
            rawRange with
            {
                ImageEndExclusive = Address(
                    Raw(rawRange.ImageEndExclusive) + 1)
            },
            rawRange with { ImageSize = rawRange.ImageSize + 1 },
            rawRange with
            {
                LeadingPaddingBytes =
                    rawRange.LeadingPaddingBytes + 1
            },
            rawRange with
            {
                TrailingPaddingBytes =
                    EfiLoadedImageRange.PageSize
            },
            rawRange with
            {
                Reservation = Reservation(
                    rawReservation with
                    {
                        Kind = BootReservationKind.BootStack
                    })
            },
            rawRange with
            {
                Reservation = Reservation(
                    rawReservation with { Reserved6 = 1 })
            },
            rawRange with
            {
                Reservation = Reservation(
                    rawReservation with
                    {
                        PhysicalStart = Address(
                            Raw(rawReservation.PhysicalStart)
                                + EfiLoadedImageRange.PageSize)
                    })
            },
            rawRange with
            {
                Reservation = Reservation(
                    rawReservation with
                    {
                        PhysicalEndExclusive = Address(
                            Raw(rawReservation.PhysicalEndExclusive)
                                + EfiLoadedImageRange.PageSize)
                    })
            },
            rawRange with
            {
                Reservation = Reservation(
                    rawReservation with
                    {
                        PageCount = rawReservation.PageCount + 1
                    })
            },
            rawRange with
            {
                Reservation = Reservation(
                    rawReservation with
                    {
                        ByteLength = rawReservation.ByteLength
                            + EfiLoadedImageRange.PageSize
                    })
            }
        ];

        foreach (RawLoadedImageRange malformedRawRange
            in malformedRanges)
        {
            RawLoadedImageRange copy = malformedRawRange;
            EfiLoadedImageRange malformedRange = Unsafe.As<
                RawLoadedImageRange,
                EfiLoadedImageRange>(ref copy);
            if (malformedRange.IsValid)
            {
                return false;
            }
        }

        return true;
    }

    private static unsafe bool TestDeterminismImmutabilityAndAllocation()
    {
        EfiLoadedImageProtocolObservation valid =
            Observation(0, 0x1110, 0x2220, 0x401123, 0x2234);
        EfiLoadedImageProtocolObservation invalid =
            Observation(0, 0x3330, 0x4440, 0x5000, 0);
        byte[] validBefore = Bytes(valid);
        byte[] invalidBefore = Bytes(invalid);

        EfiLoadedImageRangeCapturePlan firstValid = Evaluate(valid);
        EfiLoadedImageRangeCapturePlan secondValid = Evaluate(valid);
        EfiLoadedImageRangeCapturePlan firstInvalid = Evaluate(invalid);
        EfiLoadedImageRangeCapturePlan secondInvalid = Evaluate(invalid);
        if (!firstValid.Equals(secondValid)
            || !firstInvalid.Equals(secondInvalid))
        {
            return false;
        }

        EfiLoadedImageProtocolObservation* validPointer = &valid;
        EfiLoadedImageProtocolObservation* invalidPointer = &invalid;
        _ = EfiLoadedImageRangeCapturePlan.Create(
            validPointer,
            1,
            1,
            EfiLoadedImageRange.PageSize);
        _ = EfiLoadedImageRangeCapturePlan.Create(
            invalidPointer,
            1,
            1,
            EfiLoadedImageRange.PageSize);

        long before = GC.GetAllocatedBytesForCurrentThread();
        EfiLoadedImageRangeCapturePlan lastValid = default;
        EfiLoadedImageRangeCapturePlan lastInvalid = default;
        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            lastValid = EfiLoadedImageRangeCapturePlan.Create(
                validPointer,
                1,
                1,
                EfiLoadedImageRange.PageSize);
            lastInvalid = EfiLoadedImageRangeCapturePlan.Create(
                invalidPointer,
                1,
                1,
                EfiLoadedImageRange.PageSize);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        return after == before
            && lastValid.Equals(firstValid)
            && lastInvalid.Equals(firstInvalid)
            && Bytes(valid).AsSpan().SequenceEqual(validBefore)
            && Bytes(invalid).AsSpan().SequenceEqual(invalidBefore);
    }

    private static bool TestProtocolPointerIsNotRetained()
    {
        const ulong firstProtocolAddress = 0xA1B2C3D4E5F60708;
        const ulong secondProtocolAddress = 0x1020304050607080;
        EfiLoadedImageRangeCapturePlan first = Evaluate(
            Observation(
                0,
                0x1111222233334444,
                firstProtocolAddress,
                0x400000,
                0x2000));
        EfiLoadedImageRangeCapturePlan second = Evaluate(
            Observation(
                0,
                0x1111222233334444,
                secondProtocolAddress,
                0x400000,
                0x2000));
        byte[] firstBytes = Bytes(first);
        byte[] firstPointerBytes =
            BitConverter.GetBytes(firstProtocolAddress);
        byte[] secondPointerBytes =
            BitConverter.GetBytes(secondProtocolAddress);

        return first.Succeeded
            && second.Succeeded
            && first.Equals(second)
            && firstBytes.AsSpan().SequenceEqual(Bytes(second))
            && !ContainsSequence(firstBytes, firstPointerBytes)
            && !ContainsSequence(firstBytes, secondPointerBytes)
            && typeof(EfiLoadedImageRangeCapturePlan)
                .GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic)
                .All(field =>
                    !field.Name.Contains(
                        "protocol",
                        StringComparison.OrdinalIgnoreCase)
                    && field.FieldType != typeof(VirtualAddress)
                    && !field.FieldType.IsPointer)
            && typeof(EfiLoadedImageRangeCapturePlan)
                .GetProperties(
                    BindingFlags.Instance | BindingFlags.Public)
                .All(property =>
                    !property.Name.Contains(
                        "protocol",
                        StringComparison.OrdinalIgnoreCase)
                    && property.PropertyType != typeof(VirtualAddress));
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (int start = 0; start <= haystack.Length - needle.Length; start++)
        {
            bool match = true;
            for (int index = 0; index < needle.Length; index++)
            {
                if (haystack[start + index] != needle[index])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TestNoRuntimeSourceSurface()
    {
        string[] sourcePaths =
        [
            GetLoadedImageRangeSourcePath(),
            GetCapturePlanSourcePath()
        ];
        string[] forbiddenFragments =
        [
            "using ",
            "System.",
            "System::",
            "throw ",
            "catch ",
            "finally ",
            "typeof",
            "Reflection",
            "Activator",
            "dynamic",
            "delegate",
            "interface ",
            "record ",
            "Span<",
            "ReadOnlySpan<",
            "Memory<",
            "ReadOnlyMemory<",
            "ulong.MaxValue",
            "List<",
            "Dictionary<",
            "IEnumerable<",
            "OpenProtocol",
            "HandleProtocol",
            "LocateProtocol",
            "RuntimeImport",
            "DllImport",
            "delegate*",
            "EfiEntry",
            "operator "
        ];

        foreach (string sourcePath in sourcePaths)
        {
            string source = RemoveLineComments(
                File.ReadAllText(sourcePath));
            foreach (string fragment in forbiddenFragments)
            {
                if (source.Contains(fragment, StringComparison.Ordinal))
                {
                    return SourceFirewallFailure(
                        "forbidden-fragment=" + fragment);
                }
            }

            if (Regex.IsMatch(
                    source,
                    @"(?m)^\s*(?:global\s+)?using\b",
                    RegexOptions.CultureInvariant)
                || Regex.IsMatch(
                    source,
                    @"\b(string|object|dynamic|delegate|interface|record|var|Object|Delegate|MulticastDelegate|Attribute|String|Array|Type|Exception|Span|ReadOnlySpan)\b",
                    RegexOptions.CultureInvariant)
                || Regex.IsMatch(
                    source,
                    @"\[\s*\]",
                    RegexOptions.CultureInvariant)
                || Regex.IsMatch(
                    source,
                    @"\bnew\s*\(",
                    RegexOptions.CultureInvariant))
            {
                return SourceFirewallFailure(
                    "lexical-surface=" + Path.GetFileName(sourcePath));
            }

            foreach (Match allocation in Regex.Matches(
                source,
                @"\bnew\s+([A-Za-z_][A-Za-z0-9_]*)",
                RegexOptions.CultureInvariant))
            {
                string targetType = allocation.Groups[1].Value;
                if (targetType != nameof(EfiLoadedImageRange)
                    && targetType
                        != nameof(EfiLoadedImageProtocolObservation)
                    && targetType
                        != nameof(EfiLoadedImageRangeCapturePlan))
                {
                    return SourceFirewallFailure(
                        "allocation-target=" + targetType);
                }
            }
        }

        Type observationType =
            typeof(EfiLoadedImageProtocolObservation);
        Type rangeType = typeof(EfiLoadedImageRange);
        Type planType = typeof(EfiLoadedImageRangeCapturePlan);
        if (!HasOnlyValueTypeDeclaredSurface(observationType)
            || !HasOnlyValueTypeDeclaredSurface(rangeType)
            || !HasOnlyValueTypeDeclaredSurface(planType))
        {
            return SourceFirewallFailure("managed-surface");
        }

        string captureSource = RemoveLineComments(
            File.ReadAllText(GetCapturePlanSourcePath()));
        int countZeroIndex = captureSource.IndexOf(
            "observationCount == 0",
            StringComparison.Ordinal);
        int maximumIndex = captureSource.IndexOf(
            "observationCount > MaximumObservationCount",
            StringComparison.Ordinal);
        int capacityIndex = captureSource.IndexOf(
            "observationCapacity < observationCount",
            StringComparison.Ordinal);
        int nullIndex = captureSource.IndexOf(
            "observations == null",
            StringComparison.Ordinal);
        int addressIndex = captureSource.IndexOf(
            "ulong observationAddress = (ulong)observations",
            StringComparison.Ordinal);
        int spanIndex = captureSource.IndexOf(
            "MaximumUnsignedLong - observationByteLength",
            StringComparison.Ordinal);
        int pageSizeIndex = captureSource.IndexOf(
            "pageSize != EfiLoadedImageRange.PageSize",
            StringComparison.Ordinal);
        int dereferenceIndex = captureSource.IndexOf(
            "observations[0]",
            StringComparison.Ordinal);
        if (countZeroIndex < 0
            || maximumIndex <= countZeroIndex
            || capacityIndex <= maximumIndex
            || nullIndex <= capacityIndex
            || addressIndex <= nullIndex
            || spanIndex <= addressIndex
            || pageSizeIndex <= spanIndex
            || dereferenceIndex <= pageSizeIndex
            || Regex.Matches(
                    captureSource,
                    @"observations\s*\[\s*0\s*\]",
                    RegexOptions.CultureInvariant).Count != 1)
        {
            return SourceFirewallFailure("validation-order");
        }

        if (!HasExactDeclaredMemberNames(
                observationType,
                [
                    "_firmwareStatus",
                    "_imageHandleFromUefi",
                    "_protocolAddressFromUefi",
                    "_imageBaseFromUefi",
                    "_imageSizeFromUefi"
                ],
                [
                    "get_FirmwareStatus",
                    "get_ImageHandleFromUefi",
                    "get_ProtocolAddressFromUefi",
                    "get_ImageBaseFromUefi",
                    "get_ImageSizeFromUefi",
                    "CaptureFromUefiBoundary",
                    "Equals"
                ],
                expectedConstructorCount: 1)
            || !HasExactDeclaredMemberNames(
                rangeType,
                [
                    "PageSize",
                    "MaximumUnsignedLong",
                    "_imageBase",
                    "_imageEndExclusive",
                    "_imageSize",
                    "_reservation",
                    "_leadingPaddingBytes",
                    "_trailingPaddingBytes"
                ],
                [
                    "get_ImageBase",
                    "get_ImageEndExclusive",
                    "get_ImageSize",
                    "get_Reservation",
                    "get_LeadingPaddingBytes",
                    "get_TrailingPaddingBytes",
                    "get_IsValid",
                    "Equals",
                    "CreateValidated",
                    "Validate"
                ],
                expectedConstructorCount: 1)
            || !HasExactDeclaredMemberNames(
                planType,
                [
                    "EfiSuccess",
                    "RequiredObservationCount",
                    "MaximumObservationCount",
                    "NoIndex",
                    "RequiredObservationAlignment",
                    "RequiredProtocolPointerAlignment",
                    "MaximumUnsignedLong",
                    "_status",
                    "_addressStatus",
                    "_reservationStatus",
                    "_reserved0",
                    "_reserved1",
                    "_reserved2",
                    "_reserved3",
                    "_reserved4",
                    "_observationCount",
                    "_validatedObservationCount",
                    "_availableObservationCapacity",
                    "_invalidObservationIndex",
                    "_pageSize",
                    "_firmwareStatus",
                    "_imageHandle",
                    "_range"
                ],
                [
                    "get_Status",
                    "get_AddressStatus",
                    "get_ReservationStatus",
                    "get_Succeeded",
                    "get_ObservationCount",
                    "get_ValidatedObservationCount",
                    "get_AvailableObservationCapacity",
                    "get_RequiredObservationCapacity",
                    "get_InvalidObservationIndex",
                    "get_PageSize",
                    "get_FirmwareStatus",
                    "get_ImageHandle",
                    "get_Range",
                    "Create",
                    "Equals",
                    "Validate",
                    "FailBeforeObservation",
                    "FailObservation"
                ],
                expectedConstructorCount: 1))
        {
            return SourceFirewallFailure("member-set");
        }

        MethodInfo? create = planType.GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.Public);
        ParameterInfo[]? parameters = create?.GetParameters();
        bool noRetainedProtocolSurface = rangeType
                .GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic)
                .Concat(planType.GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic))
                .All(field =>
                    !field.FieldType.IsPointer
                    && field.FieldType != typeof(IntPtr)
                    && field.FieldType != typeof(UIntPtr)
                    && field.FieldType != typeof(VirtualAddress)
                    && !field.Name.Contains(
                        "protocol",
                        StringComparison.OrdinalIgnoreCase))
            && rangeType
                .GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic)
                .Count(field =>
                    field.FieldType == typeof(BootReservedRange)) == 1;
        return create is not null
            && create.ReturnType == planType
            && parameters is not null
            && parameters.Length == 4
            && parameters[0].ParameterType.IsPointer
            && parameters[0].ParameterType.GetElementType()
                == observationType
            && parameters.Skip(1)
                .All(parameter => parameter.ParameterType == typeof(ulong))
            && noRetainedProtocolSurface
            || SourceFirewallFailure("pointer-core");
    }

    private static bool SourceFirewallFailure(string reason)
    {
        Console.WriteLine(
            "M01_003_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
        return false;
    }

    private static bool HasOnlyValueTypeDeclaredSurface(Type type)
    {
        foreach (FieldInfo field in type.GetFields(
            BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly))
        {
            if (!field.FieldType.IsValueType
                || field.IsStatic && !field.IsLiteral)
            {
                return false;
            }
        }

        foreach (MethodInfo method in type.GetMethods(
            BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly))
        {
            if (!IsValueOnlyType(method.ReturnType))
            {
                return false;
            }

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (!IsValueOnlyType(parameter.ParameterType))
                {
                    return false;
                }
            }
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(
            BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly))
        {
            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                if (!IsValueOnlyType(parameter.ParameterType))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsValueOnlyType(Type type)
    {
        if (type == typeof(void))
        {
            return true;
        }

        while (type.IsByRef || type.IsPointer)
        {
            type = type.GetElementType()!;
        }

        return type.IsValueType;
    }

    private static bool HasExactDeclaredMemberNames(
        Type type,
        string[] expectedFieldNames,
        string[] expectedMethodNames,
        int expectedConstructorCount)
    {
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
        MethodInfo[] methods = type.GetMethods(
            BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
        ConstructorInfo[] constructors = type.GetConstructors(
            BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

        return fields.Length == expectedFieldNames.Length
            && fields.Select(field => field.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    expectedFieldNames.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal)
            && methods.Length == expectedMethodNames.Length
            && methods.Select(method => method.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    expectedMethodNames.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal)
            && constructors.Length == expectedConstructorCount;
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();
        bool completedSuccessfully = false;

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                using OS.Boot;
                using OS.Kernel.Memory;

                internal static unsafe class PositiveLoadedImageProbe
                {
                    public static EfiLoadedImageRangeCaptureStatus Exercise()
                    {
                        EfiLoadedImageProtocolObservation observation =
                            EfiLoadedImageProtocolObservation
                                .CaptureFromUefiBoundary(
                                    0UL,
                                    0x1000UL,
                                    0x2000UL,
                                    0x401123UL,
                                    0x2234UL);
                        EfiLoadedImageRangeCapturePlan result =
                            EfiLoadedImageRangeCapturePlan.Create(
                                &observation,
                                1UL,
                                1UL,
                                BootReservedRange.PageSize);
                        BootReservationKind kind =
                            result.Range.Reservation.Kind;
                        return kind == BootReservationKind.LoadedImage
                            ? result.Status
                            : EfiLoadedImageRangeCaptureStatus.Uninitialized;
                    }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            NoStdLibBuildResult result = RunNoStdLibBuild(workspace);
            string outputAssembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.LoadedImageCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasZeroAssemblyReferences(outputAssembly))
            {
                Console.WriteLine(
                    "M01_003_NOSTDLIB_COMPILE_DIAGNOSTIC "
                    + NormalizeCompilerOutput(result.Output));
                return false;
            }

            completedSuccessfully = true;
            return true;
        }
        finally
        {
            try
            {
                DeleteNoStdLibWorkspace(workspace);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "M01_003_NOSTDLIB_CLEANUP_DIAGNOSTIC "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                if (completedSuccessfully)
                {
                    throw;
                }
            }
        }
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-003-" + Guid.NewGuid().ToString("N"));
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
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.LoadedImageCompileClosure</AssemblyName>
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
                <Compile Include="__TYPED_ADDRESSES__" Link="Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs" />
                <Compile Include="__BOOT_RANGE__" Link="Memory\BootReservedRange.cs" />
                <Compile Include="__LOADED_IMAGE_RANGE__" Link="Boot\EfiLoadedImageRange.cs" />
                <Compile Include="__CAPTURE_PLAN__" Link="Boot\EfiLoadedImageRangeCapturePlan.cs" />
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
                "__TYPED_ADDRESSES__",
                EscapeXml(GetTypedAddressSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__BOOT_RANGE__",
                EscapeXml(GetBootReservedRangeSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__LOADED_IMAGE_RANGE__",
                EscapeXml(GetLoadedImageRangeSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__CAPTURE_PLAN__",
                EscapeXml(GetCapturePlanSourcePath()),
                StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(workspace, "M01.003.NoStdLib.csproj"),
                projectText,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
                // Preserve the workspace-creation failure as primary evidence.
            }

            throw;
        }
    }

    private static NoStdLibBuildResult RunNoStdLibBuild(string workspace)
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
        start.ArgumentList.Add("M01.003.NoStdLib.csproj");
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
        process.Start();
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
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Timeout remains the primary failure.
                }
            }
        }

        bool processStopped = exited || process.WaitForExit(5_000);
        bool streamsCompleted = Task.WaitAll(
            [standardOutput, standardError],
            millisecondsTimeout: 5_000);
        string output = streamsCompleted
            ? standardOutput.GetAwaiter().GetResult()
                + Environment.NewLine
                + standardError.GetAwaiter().GetResult()
            : "Compiler output streams did not close within the bounded timeout.";
        return new NoStdLibBuildResult(
            processStopped ? process.ExitCode : -1,
            TimedOut: !exited || !processStopped || !streamsCompleted,
            output);
    }

    private static bool ContainsInfrastructureFailure(string output)
    {
        string[] diagnostics =
        [
            "error MSB",
            "error NETSDK",
            "error NU",
            "CS0518",
            "CS0656",
            "CS8021"
        ];

        return diagnostics.Any(diagnostic =>
            output.Contains(diagnostic, StringComparison.Ordinal));
    }

    private static bool HasZeroAssemblyReferences(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = peReader.GetMetadataReader();
        return metadata.AssemblyReferences.Count == 0;
    }

    private static string NormalizeCompilerOutput(string output)
    {
        return output
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.GetTempPath()));
        string fullWorkspace = Path.GetFullPath(workspace);
        string? parent = Directory.GetParent(fullWorkspace)?.FullName;
        string leafName = Path.GetFileName(fullWorkspace);

        if (parent is null
            || !Path.TrimEndingDirectorySeparator(parent).Equals(
                tempRoot,
                StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(
                leafName,
                @"\Aclros-m01-003-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-003 NoStdLib workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        if ((File.GetAttributes(fullWorkspace)
                & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Refusing to delete a reparse-point M01-003 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    private static string RemoveLineComments(string source)
    {
        StringBuilder result = new StringBuilder(source.Length);
        using StringReader reader = new StringReader(source);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            int commentStart = line.IndexOf("//", StringComparison.Ordinal);
            result.AppendLine(commentStart >= 0 ? line[..commentStart] : line);
        }

        return result.ToString();
    }

    private static EfiLoadedImageProtocolObservation Observation(
        ulong firmwareStatus,
        ulong imageHandle,
        ulong protocolAddress,
        ulong imageBase,
        ulong imageSize)
    {
        return EfiLoadedImageProtocolObservation
            .CaptureFromUefiBoundary(
                firmwareStatus,
                imageHandle,
                protocolAddress,
                imageBase,
                imageSize);
    }

    private static unsafe EfiLoadedImageRangeCapturePlan Evaluate(
        EfiLoadedImageProtocolObservation observation,
        ulong count = 1,
        ulong capacity = 1,
        ulong pageSize = EfiLoadedImageRange.PageSize)
    {
        return EfiLoadedImageRangeCapturePlan.Create(
            &observation,
            count,
            capacity,
            pageSize);
    }

    private static bool IsSuccessEvidence(
        EfiLoadedImageRangeCapturePlan plan,
        ulong capacity)
    {
        return plan.Succeeded
            && plan.Status == EfiLoadedImageRangeCaptureStatus.Success
            && plan.AddressStatus == AddressValueStatus.Success
            && plan.ReservationStatus == BootReservedRangeStatus.Success
            && plan.ObservationCount == 1
            && plan.ValidatedObservationCount == 1
            && plan.AvailableObservationCapacity == capacity
            && plan.RequiredObservationCapacity == 1
            && plan.InvalidObservationIndex
                == EfiLoadedImageRangeCapturePlan.NoIndex
            && plan.PageSize == EfiLoadedImageRange.PageSize
            && plan.FirmwareStatus == 0
            && plan.ImageHandle != 0
            && plan.Range.IsValid;
    }

    private static bool IsPreObservationFailure(
        EfiLoadedImageRangeCapturePlan plan,
        EfiLoadedImageRangeCaptureStatus status,
        AddressValueStatus addressStatus,
        ulong count,
        ulong capacity,
        ulong pageSize)
    {
        return !plan.Succeeded
            && plan.Status == status
            && plan.AddressStatus == addressStatus
            && plan.ReservationStatus
                == BootReservedRangeStatus.Uninitialized
            && plan.ObservationCount == count
            && plan.ValidatedObservationCount == 0
            && plan.AvailableObservationCapacity == capacity
            && plan.RequiredObservationCapacity == 1
            && plan.InvalidObservationIndex
                == EfiLoadedImageRangeCapturePlan.NoIndex
            && plan.PageSize == pageSize
            && plan.FirmwareStatus == 0
            && plan.ImageHandle == 0
            && plan.Range.Equals(default);
    }

    private static bool IsObservationFailure(
        EfiLoadedImageRangeCapturePlan plan,
        EfiLoadedImageRangeCaptureStatus status,
        AddressValueStatus addressStatus,
        BootReservedRangeStatus reservationStatus,
        ulong firmwareStatus,
        ulong imageHandle)
    {
        return !plan.Succeeded
            && plan.Status == status
            && plan.AddressStatus == addressStatus
            && plan.ReservationStatus == reservationStatus
            && plan.ObservationCount == 1
            && plan.ValidatedObservationCount == 0
            && plan.AvailableObservationCapacity == 1
            && plan.RequiredObservationCapacity == 1
            && plan.InvalidObservationIndex == 0
            && plan.PageSize == EfiLoadedImageRange.PageSize
            && plan.FirmwareStatus == firmwareStatus
            && plan.ImageHandle == imageHandle
            && plan.Range.Equals(default);
    }

    private static ulong Raw(PhysicalAddress address)
    {
        if (address.TryGetRawValueForUefiBoundary(out ulong value)
            != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Expected a valid physical address.");
        }

        return value;
    }

    private static PhysicalAddress Address(ulong value)
    {
        if (PhysicalAddress.TryCreateFromUefiBoundary(
                value,
                out PhysicalAddress address)
            != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Expected a constructible physical address.");
        }

        return address;
    }

    private static BootReservedRange Reservation(
        RawBootReservedRange raw)
    {
        return Unsafe.As<
            RawBootReservedRange,
            BootReservedRange>(ref raw);
    }

    private static byte[] Bytes<T>(T value)
        where T : struct
    {
        return MemoryMarshal.AsBytes(
                MemoryMarshal.CreateReadOnlySpan(ref value, 1))
            .ToArray();
    }

    private static string GetTypedAddressSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
    }

    private static string GetBootReservedRangeSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootReservedRange.cs");
    }

    private static string GetLoadedImageRangeSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "EfiLoadedImageRange.cs");
    }

    private static string GetCapturePlanSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "EfiLoadedImageRangeCapturePlan.cs");
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct RawLoadedImageRange
    {
        public PhysicalAddress ImageBase;
        public PhysicalAddress ImageEndExclusive;
        public ulong ImageSize;
        public BootReservedRange Reservation;
        public ulong LeadingPaddingBytes;
        public ulong TrailingPaddingBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct RawBootReservedRange
    {
        public BootReservationKind Kind;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
        public byte Reserved4;
        public byte Reserved5;
        public byte Reserved6;
        public PhysicalAddress PhysicalStart;
        public PhysicalAddress PhysicalEndExclusive;
        public ulong PageCount;
        public ulong ByteLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct RawCapturePlan
    {
        public EfiLoadedImageRangeCaptureStatus Status;
        public AddressValueStatus AddressStatus;
        public BootReservedRangeStatus ReservationStatus;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
        public byte Reserved4;
        public ulong ObservationCount;
        public ulong ValidatedObservationCount;
        public ulong AvailableObservationCapacity;
        public ulong InvalidObservationIndex;
        public ulong PageSize;
        public ulong FirmwareStatus;
        public ulong ImageHandle;
        public EfiLoadedImageRange Range;
    }

    private readonly record struct NoStdLibBuildResult(
        int ExitCode,
        bool TimedOut,
        string Output);
}
