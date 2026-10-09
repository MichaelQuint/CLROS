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

internal static class OwnedBootStackLayoutTests
{
    private const string IssueTestName = "m01.004";

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run(
            "m01.004 lifetime statuses and pod layouts are stable",
            TestStableSurface,
            ref failures);
        Run(
            "m01.004 default values fail closed",
            TestDefaultsFailClosed,
            ref failures);
        Run(
            "m01.004 minimum representative and multi guard layouts succeed",
            TestSuccessfulLayouts,
            ref failures);
        Run(
            "m01.004 high address layout remains checked",
            TestHighAddressLayout,
            ref failures);
        Run(
            "m01.004 pointer count and capacity fail before dereference",
            TestPointerContract,
            ref failures);
        Run(
            "m01.004 backing range and stack top failures are classified",
            TestBackingAndStackTopFailures,
            ref failures);
        Run(
            "m01.004 canonical table failures retain nested evidence",
            TestReservationTableFailures,
            ref failures);
        Run(
            "m01.004 backing topology and accounting are exact",
            TestBackingTopologyAndAccounting,
            ref failures);
        Run(
            "m01.004 canonical reservations remain ledger compatible",
            TestCanonicalLedgerCompatibility,
            ref failures);
        Run(
            "m01.004 reserved ready active transitions succeed",
            TestSuccessfulTransitions,
            ref failures);
        Run(
            "m01.004 repeated and illegal transitions fail closed",
            TestInvalidTransitions,
            ref failures);
        Run(
            "m01.004 malformed layouts and results cannot report success",
            TestMalformedValuesFailClosed,
            ref failures);
        Run(
            "m01.004 validation precedence is deterministic",
            TestValidationPrecedence,
            ref failures);
        Run(
            "m01.004 evaluation is nonmutating deterministic and allocation free",
            TestDeterminismImmutabilityAndAllocation,
            ref failures);
        Run(
            "m01.004 source excludes hosted live memory and stack switch dependencies",
            TestNoRuntimeSourceSurface,
            ref failures);
        Run(
            "m01.004 exact sources compile under nostdlib efi",
            TestNoStdLibEfiCompileClosure,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-004 owned boot-stack layout host tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_004_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestStableSurface()
    {
        (string Name, byte Value)[] lifetimes =
        [
            ("Uninitialized", 0),
            ("Reserved", 1),
            ("Ready", 2),
            ("Active", 3)
        ];
        (string Name, byte Value)[] statuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("InvalidReservationCount", 2),
            ("TooManyReservations", 3),
            ("ReservationCapacityTooSmall", 4),
            ("MissingReservationTable", 5),
            ("MisalignedReservationTable", 6),
            ("ReservationTableAddressRangeOverflow", 7),
            ("InvalidBackingStart", 8),
            ("MisalignedBackingStart", 9),
            ("InvalidBackingPageCount", 10),
            ("BackingByteCountOverflow", 11),
            ("BackingEndOverflow", 12),
            ("ReservationTableRejected", 13),
            ("InvalidStackTop", 14),
            ("MisalignedStackTop", 15),
            ("MissingGuardReservation", 16),
            ("MissingStackReservation", 17),
            ("BackingArenaMismatch", 18),
            ("StackTopMismatch", 19),
            ("InvalidLayout", 20),
            ("InvalidLifetime", 21),
            ("RepeatedLifetimeTransition", 22),
            ("IllegalLifetimeTransition", 23)
        ];

        if (!EnumMatches<OwnedBootStackLifetime>(lifetimes)
            || !EnumMatches<OwnedBootStackLayoutStatus>(statuses)
            || OwnedBootStackLayout.RequiredReservationCount != 2
            || OwnedBootStackLayout.X64StackAlignmentBytes != 16
            || !IsReadOnlyPod<OwnedBootStackLayout>(128)
            || !IsReadOnlyPod<OwnedBootStackLayoutResult>(176)
            || !HasOnlyPrivateConstructors<OwnedBootStackLayout>()
            || !HasOnlyPrivateConstructors<
                OwnedBootStackLayoutResult>())
        {
            return false;
        }

        if (!HasOffsets<OwnedBootStackLayout>(
                [
                    ("_backingStart", 0),
                    ("_backingEndExclusive", 8),
                    ("_backingPageCount", 16),
                    ("_backingByteLength", 24),
                    ("_guardReservation", 32),
                    ("_stackReservation", 72),
                    ("_physicalStackTop", 112),
                    ("_lifetime", 120),
                    ("_reserved0", 121),
                    ("_reserved1", 122),
                    ("_reserved2", 123),
                    ("_reserved3", 124),
                    ("_reserved4", 125),
                    ("_reserved5", 126),
                    ("_reserved6", 127)
                ])
            || !HasOffsets<OwnedBootStackLayoutResult>(
                [
                    ("_status", 0),
                    ("_addressStatus", 1),
                    ("_reservationTableStatus", 2),
                    ("_rangeStatus", 3),
                    ("_previousLifetime", 4),
                    ("_requestedLifetime", 5),
                    ("_currentLifetime", 6),
                    ("_reserved0", 7),
                    ("_reservationCount", 8),
                    ("_validatedReservationCount", 16),
                    ("_availableReservationCapacity", 24),
                    ("_invalidReservationIndex", 32),
                    ("_conflictingReservationIndex", 40),
                    ("_layout", 48)
                ]))
        {
            return false;
        }

        Type layoutType = typeof(OwnedBootStackLayout);
        FieldInfo[] layoutFields = layoutType.GetFields(
            BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
        if (layoutFields.Count(field =>
                field.FieldType == typeof(BootReservedRange)) != 2
            || layoutFields.Count(field =>
                field.FieldType == typeof(PhysicalAddress)) != 3
            || layoutFields.Any(field =>
                field.FieldType == typeof(VirtualAddress)
                || field.FieldType == typeof(DeviceAddress)
                || field.FieldType.IsPointer
                || field.FieldType == typeof(IntPtr)
                || field.FieldType == typeof(UIntPtr)
                || !field.IsInitOnly))
        {
            return false;
        }

        MethodInfo? create = layoutType.GetMethod(
            nameof(OwnedBootStackLayout.Create),
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo? transition = layoutType.GetMethod(
            nameof(OwnedBootStackLayout.TransitionTo),
            BindingFlags.Public | BindingFlags.Instance);
        if (create is null
            || create.ReturnType != typeof(OwnedBootStackLayoutResult)
            || !create.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .SequenceEqual(
                    [
                        typeof(BootReservedRange).MakePointerType(),
                        typeof(ulong),
                        typeof(ulong),
                        typeof(PhysicalAddress),
                        typeof(ulong),
                        typeof(PhysicalAddress)
                    ])
            || transition is null
            || transition.ReturnType
                != typeof(OwnedBootStackLayoutResult)
            || !transition.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .SequenceEqual([typeof(OwnedBootStackLifetime)]))
        {
            return false;
        }

        return layoutType.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance).Length == 0
            && typeof(OwnedBootStackLayoutResult).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance).Length == 0;
    }

    private static bool TestDefaultsFailClosed()
    {
        OwnedBootStackLayout layout = default;
        OwnedBootStackLayoutResult result = default;
        OwnedBootStackLayoutResult transition =
            layout.TransitionTo(OwnedBootStackLifetime.Ready);

        return !layout.IsValid
            && layout.Lifetime == OwnedBootStackLifetime.Uninitialized
            && !result.Succeeded
            && result.Status == OwnedBootStackLayoutStatus.Uninitialized
            && !result.Layout.IsValid
            && !transition.Succeeded
            && transition.Status
                == OwnedBootStackLayoutStatus.InvalidLayout
            && transition.CurrentLifetime
                == OwnedBootStackLifetime.Uninitialized;
    }

    private static bool TestSuccessfulLayouts()
    {
        OwnedBootStackLayoutResult minimum = CreateLayout(
            0x0010_0000,
            guardPages: 1,
            stackPages: 1,
            capacity: 2);
        OwnedBootStackLayoutResult representative = CreateLayout(
            0x0020_0000,
            guardPages: 1,
            stackPages: 8,
            capacity: 9);
        OwnedBootStackLayoutResult multiGuard = CreateLayout(
            0x0030_0000,
            guardPages: 2,
            stackPages: 3,
            capacity: 2);

        BootReservedRange[] withPoisonedTail =
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0040_0000,
                1),
            Reservation(
                BootReservationKind.BootStack,
                0x0040_1000,
                2),
            default
        ];
        byte[] before = StructBytes(withPoisonedTail);
        OwnedBootStackLayoutResult ignoresTail;
        unsafe
        {
            fixed (BootReservedRange* pointer = withPoisonedTail)
            {
                ignoresTail = OwnedBootStackLayout.Create(
                    pointer,
                    2,
                    3,
                    Address(0x0040_0000),
                    3,
                    Address(0x0040_3000));
            }
        }

        return IsSuccessfulLayout(minimum, 2, 1, 1)
            && minimum.Layout.BackingByteLength == 8192
            && IsSuccessfulLayout(representative, 9, 1, 8)
            && representative.AvailableReservationCapacity == 9
            && representative.Layout.BackingByteLength == 36864
            && IsSuccessfulLayout(multiGuard, 5, 2, 3)
            && IsSuccessfulLayout(ignoresTail, 3, 1, 2)
            && before.SequenceEqual(StructBytes(withPoisonedTail));
    }

    private static bool TestHighAddressLayout()
    {
        OwnedBootStackLayoutResult result = CreateLayout(
            0xffff_ffff_ffff_d000,
            guardPages: 1,
            stackPages: 1,
            capacity: 2);

        return IsSuccessfulLayout(result, 2, 1, 1)
            && Raw(result.Layout.BackingStart)
                == 0xffff_ffff_ffff_d000
            && Raw(result.Layout.BackingEndExclusive)
                == 0xffff_ffff_ffff_f000
            && Raw(result.Layout.PhysicalStackTop)
                == 0xffff_ffff_ffff_f000;
    }

    private static unsafe bool TestPointerContract()
    {
        PhysicalAddress backing = Address(0x0010_0000);
        PhysicalAddress top = Address(0x0010_2000);

        OwnedBootStackLayoutResult zero =
            OwnedBootStackLayout.Create(null, 0, 0, backing, 2, top);
        OwnedBootStackLayoutResult one =
            OwnedBootStackLayout.Create(null, 1, 1, backing, 2, top);
        OwnedBootStackLayoutResult tooMany =
            OwnedBootStackLayout.Create(null, 3, 3, backing, 2, top);
        OwnedBootStackLayoutResult huge =
            OwnedBootStackLayout.Create(
                null,
                ~0UL,
                ~0UL,
                backing,
                2,
                top);
        OwnedBootStackLayoutResult capacity =
            OwnedBootStackLayout.Create(null, 2, 1, backing, 2, top);
        OwnedBootStackLayoutResult missing =
            OwnedBootStackLayout.Create(null, 2, 2, backing, 2, top);
        OwnedBootStackLayoutResult misaligned =
            OwnedBootStackLayout.Create(
                (BootReservedRange*)1,
                2,
                2,
                backing,
                2,
                top);
        OwnedBootStackLayoutResult wrapping =
            OwnedBootStackLayout.Create(
                (BootReservedRange*)(~7UL),
                2,
                2,
                backing,
                2,
                top);

        return HasFailure(
                zero,
                OwnedBootStackLayoutStatus.InvalidReservationCount,
                BootReservationTableStatus.Uninitialized)
            && HasFailure(
                one,
                OwnedBootStackLayoutStatus.InvalidReservationCount,
                BootReservationTableStatus.Uninitialized)
            && HasFailure(
                tooMany,
                OwnedBootStackLayoutStatus.TooManyReservations,
                BootReservationTableStatus.Uninitialized)
            && HasFailure(
                huge,
                OwnedBootStackLayoutStatus.TooManyReservations,
                BootReservationTableStatus.Uninitialized)
            && HasFailure(
                capacity,
                OwnedBootStackLayoutStatus.ReservationCapacityTooSmall,
                BootReservationTableStatus.Uninitialized)
            && HasFailure(
                missing,
                OwnedBootStackLayoutStatus.MissingReservationTable,
                BootReservationTableStatus.Uninitialized)
            && HasFailure(
                misaligned,
                OwnedBootStackLayoutStatus.MisalignedReservationTable,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.MisalignedAddress)
            && HasFailure(
                wrapping,
                OwnedBootStackLayoutStatus
                    .ReservationTableAddressRangeOverflow,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.AddressOverflow);
    }

    private static bool TestBackingAndStackTopFailures()
    {
        BootReservedRange[] table = ValidTable(
            0x0010_0000,
            guardPages: 1,
            stackPages: 1);
        PhysicalAddress validTop = Address(0x0010_2000);
        PhysicalAddress unalignedBacking =
            UnalignedAddress(0x0010_0001);
        PhysicalAddress wrappingBacking =
            Address(0xffff_ffff_ffff_f000);
        PhysicalAddress unalignedTop =
            UnalignedAddress(0x0010_1ff8);

        OwnedBootStackLayoutResult invalidStart = Create(
            table,
            default,
            2,
            validTop);
        OwnedBootStackLayoutResult misalignedStart = Create(
            table,
            unalignedBacking,
            2,
            validTop);
        OwnedBootStackLayoutResult zeroPages = Create(
            table,
            Address(0x0010_0000),
            0,
            validTop);
        OwnedBootStackLayoutResult onePage = Create(
            table,
            Address(0x0010_0000),
            1,
            validTop);
        OwnedBootStackLayoutResult byteOverflow = Create(
            table,
            Address(0x0010_0000),
            (~0UL / BootReservedRange.PageSize) + 1,
            validTop);
        OwnedBootStackLayoutResult endOverflow = Create(
            table,
            wrappingBacking,
            2,
            validTop);
        OwnedBootStackLayoutResult invalidTop = Create(
            table,
            Address(0x0010_0000),
            2,
            default);
        OwnedBootStackLayoutResult misaligned = Create(
            table,
            Address(0x0010_0000),
            2,
            unalignedTop);
        OwnedBootStackLayoutResult mismatchedTop = Create(
            table,
            Address(0x0010_0000),
            2,
            Address(0x0010_1000));

        return HasFailure(
                invalidStart,
                OwnedBootStackLayoutStatus.InvalidBackingStart,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.InvalidAddress)
            && HasFailure(
                misalignedStart,
                OwnedBootStackLayoutStatus.MisalignedBackingStart,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.MisalignedAddress)
            && HasFailure(
                zeroPages,
                OwnedBootStackLayoutStatus.InvalidBackingPageCount,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.ZeroLength)
            && HasFailure(
                onePage,
                OwnedBootStackLayoutStatus.InvalidBackingPageCount,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.Uninitialized)
            && HasFailure(
                byteOverflow,
                OwnedBootStackLayoutStatus.BackingByteCountOverflow,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.AddressOverflow)
            && HasFailure(
                endOverflow,
                OwnedBootStackLayoutStatus.BackingEndOverflow,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.AddressOverflow)
            && HasFailure(
                invalidTop,
                OwnedBootStackLayoutStatus.InvalidStackTop,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.InvalidAddress)
            && HasFailure(
                misaligned,
                OwnedBootStackLayoutStatus.MisalignedStackTop,
                BootReservationTableStatus.Uninitialized,
                AddressValueStatus.MisalignedAddress)
            && HasFailure(
                mismatchedTop,
                OwnedBootStackLayoutStatus.StackTopMismatch,
                BootReservationTableStatus.Success,
                AddressValueStatus.InvalidAddress);
    }

    private static bool TestReservationTableFailures()
    {
        PhysicalAddress backing = Address(0x0010_0000);
        PhysicalAddress top = Address(0x0010_2000);

        BootReservedRange[] defaultGuard =
        [
            default,
            Reservation(
                BootReservationKind.BootStack,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] reservedGuard =
        [
            RawRange(
                BootReservationKind.BootStackGuard,
                Address(0x0010_0000),
                Address(0x0010_1000),
                1,
                4096,
                reservedByteIndex: 0),
            Reservation(
                BootReservationKind.BootStack,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] zeroGuard =
        [
            RawRange(
                BootReservationKind.BootStackGuard,
                Address(0x0010_0000),
                Address(0x0010_0000),
                0,
                0),
            Reservation(
                BootReservationKind.BootStack,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] malformedGuard =
        [
            RawRange(
                BootReservationKind.BootStackGuard,
                Address(0x0010_0000),
                Address(0x0010_1000),
                1,
                8192),
            Reservation(
                BootReservationKind.BootStack,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] defaultStack =
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_0000,
                1),
            default
        ];
        BootReservedRange[] malformedStack =
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_0000,
                1),
            RawRange(
                BootReservationKind.BootStack,
                Address(0x0010_1000),
                Address(0x0010_3000),
                1,
                4096)
        ];
        BootReservedRange[] zeroStack =
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_0000,
                1),
            RawRange(
                BootReservationKind.BootStack,
                Address(0x0010_1000),
                Address(0x0010_1000),
                0,
                0)
        ];
        BootReservedRange[] noGuard =
        [
            Reservation(
                BootReservationKind.LoadedImage,
                0x0010_0000,
                1),
            Reservation(
                BootReservationKind.BootStack,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] neitherStackKind =
        [
            Reservation(
                BootReservationKind.LoadedImage,
                0x0010_0000,
                1),
            Reservation(
                BootReservationKind.AllocatorArena,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] noStack =
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_0000,
                1),
            Reservation(
                BootReservationKind.AllocatorArena,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] duplicateGuards =
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_0000,
                1),
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] duplicateStacks =
        [
            Reservation(
                BootReservationKind.BootStack,
                0x0010_0000,
                1),
            Reservation(
                BootReservationKind.BootStack,
                0x0010_1000,
                1)
        ];
        BootReservedRange[] gap =
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_0000,
                1),
            Reservation(
                BootReservationKind.BootStack,
                0x0010_2000,
                1)
        ];
        BootReservedRange[] overlap =
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_0000,
                2),
            Reservation(
                BootReservationKind.BootStack,
                0x0010_1000,
                2)
        ];
        BootReservedRange[] reversed =
        [
            Reservation(
                BootReservationKind.BootStack,
                0x0010_1000,
                1),
            Reservation(
                BootReservationKind.BootStackGuard,
                0x0010_0000,
                1)
        ];

        return HasRejectedTable(
                Create(defaultGuard, backing, 2, top),
                BootReservationTableStatus.InvalidReservation,
                BootReservedRangeStatus.UninitializedKind,
                0)
            && HasRejectedTable(
                Create(reservedGuard, backing, 2, top),
                BootReservationTableStatus.InvalidReservation,
                BootReservedRangeStatus.ReservedBitsSet,
                0)
            && HasRejectedTable(
                Create(zeroGuard, backing, 2, top),
                BootReservationTableStatus.InvalidReservation,
                BootReservedRangeStatus.InvalidPageCount,
                0)
            && HasRejectedTable(
                Create(malformedGuard, backing, 2, top),
                BootReservationTableStatus.InvalidReservation,
                BootReservedRangeStatus.ByteLengthMismatch,
                0)
            && HasRejectedTable(
                Create(defaultStack, backing, 2, top),
                BootReservationTableStatus.InvalidReservation,
                BootReservedRangeStatus.UninitializedKind,
                1)
            && HasRejectedTable(
                Create(malformedStack, backing, 2, top),
                BootReservationTableStatus.InvalidReservation,
                BootReservedRangeStatus.PhysicalEndMismatch,
                1)
            && HasRejectedTable(
                Create(zeroStack, backing, 2, top),
                BootReservationTableStatus.InvalidReservation,
                BootReservedRangeStatus.InvalidPageCount,
                1)
            && HasRejectedTable(
                Create(noGuard, backing, 2, top),
                BootReservationTableStatus.MissingBootStackGuard,
                BootReservedRangeStatus.Success,
                1,
                OwnedBootStackLayoutStatus.MissingGuardReservation)
            && HasRejectedTable(
                Create(neitherStackKind, backing, 2, top),
                BootReservationTableStatus.Success,
                BootReservedRangeStatus.Success,
                BootReservationTableValidationResult.NoIndex,
                OwnedBootStackLayoutStatus.MissingGuardReservation)
            && HasRejectedTable(
                Create(noStack, backing, 2, top),
                BootReservationTableStatus.MissingBootStack,
                BootReservedRangeStatus.Success,
                0,
                OwnedBootStackLayoutStatus.MissingStackReservation)
            && HasRejectedTable(
                Create(duplicateGuards, backing, 2, top),
                BootReservationTableStatus.MultipleBootStackGuards,
                BootReservedRangeStatus.Success,
                1)
            && HasRejectedTable(
                Create(duplicateStacks, backing, 2, top),
                BootReservationTableStatus.MultipleBootStacks,
                BootReservedRangeStatus.Success,
                1)
            && HasRejectedTable(
                Create(gap, backing, 3, Address(0x0010_3000)),
                BootReservationTableStatus.BootStackArenaGap,
                BootReservedRangeStatus.Success,
                1)
            && HasRejectedTable(
                Create(overlap, backing, 3, Address(0x0010_3000)),
                BootReservationTableStatus.OverlappingRange,
                BootReservedRangeStatus.Success,
                1)
            && HasRejectedTable(
                Create(reversed, backing, 2, top),
                BootReservationTableStatus.UnsortedRange,
                BootReservedRangeStatus.Success,
                1);
    }

    private static bool TestBackingTopologyAndAccounting()
    {
        BootReservedRange[] shifted = ValidTable(
            0x0010_1000,
            guardPages: 1,
            stackPages: 1);
        BootReservedRange[] threePages = ValidTable(
            0x0020_0000,
            guardPages: 1,
            stackPages: 2);

        OwnedBootStackLayoutResult leadingGap = Create(
            shifted,
            Address(0x0010_0000),
            2,
            Address(0x0010_3000));
        OwnedBootStackLayoutResult backingTooLarge = Create(
            threePages,
            Address(0x0020_0000),
            4,
            Address(0x0020_3000));
        OwnedBootStackLayoutResult backingTooSmall = Create(
            threePages,
            Address(0x0020_0000),
            2,
            Address(0x0020_3000));
        OwnedBootStackLayoutResult topMismatch = Create(
            threePages,
            Address(0x0020_0000),
            3,
            Address(0x0020_2000));

        return HasFailure(
                leadingGap,
                OwnedBootStackLayoutStatus.BackingArenaMismatch,
                BootReservationTableStatus.Success,
                AddressValueStatus.InvalidAddress)
            && HasFailure(
                backingTooLarge,
                OwnedBootStackLayoutStatus.BackingArenaMismatch,
                BootReservationTableStatus.Success,
                AddressValueStatus.InvalidAddress)
            && HasFailure(
                backingTooSmall,
                OwnedBootStackLayoutStatus.BackingArenaMismatch,
                BootReservationTableStatus.Success,
                AddressValueStatus.InvalidAddress)
            && HasFailure(
                topMismatch,
                OwnedBootStackLayoutStatus.StackTopMismatch,
                BootReservationTableStatus.Success,
                AddressValueStatus.InvalidAddress);
    }

    private static unsafe bool TestCanonicalLedgerCompatibility()
    {
        OwnedBootStackLayoutResult result = CreateLayout(
            0x0010_0000,
            guardPages: 2,
            stackPages: 4,
            capacity: 2);
        if (!result.Succeeded)
        {
            return false;
        }

        BootReservedRange* table = stackalloc BootReservedRange[2];
        table[0] = result.Layout.GuardReservation;
        table[1] = result.Layout.StackReservation;
        BootReservationTableValidationResult validation =
            BootReservationTableValidator.Validate(table, 2, 2);

        return validation.Succeeded
            && validation.BootStackGuardIndex == 0
            && validation.BootStackIndex == 1
            && validation.BootStackArenaPages == 6
            && validation.BootStackArenaBytes
                == 6 * BootReservedRange.PageSize;
    }

    private static bool TestSuccessfulTransitions()
    {
        OwnedBootStackLayoutResult creation = CreateLayout(
            0x0010_0000,
            guardPages: 1,
            stackPages: 8,
            capacity: 2);
        OwnedBootStackLayout reserved = creation.Layout;
        byte[] reservedBytes = StructBytes(reserved);

        OwnedBootStackLayoutResult readyResult =
            reserved.TransitionTo(OwnedBootStackLifetime.Ready);
        OwnedBootStackLayoutResult staleSnapshotReplay =
            reserved.TransitionTo(OwnedBootStackLifetime.Ready);
        OwnedBootStackLayout ready = readyResult.Layout;
        OwnedBootStackLayoutResult activeResult =
            ready.TransitionTo(OwnedBootStackLifetime.Active);
        OwnedBootStackLayout active = activeResult.Layout;

        byte[] readyBytes = StructBytes(ready);
        byte[] activeBytes = StructBytes(active);

        return creation.Succeeded
            && reserved.Lifetime == OwnedBootStackLifetime.Reserved
            && readyResult.Succeeded
            && readyResult.PreviousLifetime
                == OwnedBootStackLifetime.Reserved
            && readyResult.RequestedLifetime
                == OwnedBootStackLifetime.Ready
            && readyResult.CurrentLifetime
                == OwnedBootStackLifetime.Ready
            && readyResult.ReservationTableStatus
                == BootReservationTableStatus.Uninitialized
            && readyResult.RangeStatus
                == BootReservedRangeStatus.Uninitialized
            && readyResult.ReservationCount == 0
            && readyResult.ValidatedReservationCount == 0
            && readyResult.AvailableReservationCapacity == 0
            && staleSnapshotReplay.Succeeded
            && staleSnapshotReplay.Layout.Equals(ready)
            && ready.Lifetime == OwnedBootStackLifetime.Ready
            && activeResult.Succeeded
            && activeResult.PreviousLifetime
                == OwnedBootStackLifetime.Ready
            && activeResult.RequestedLifetime
                == OwnedBootStackLifetime.Active
            && activeResult.CurrentLifetime
                == OwnedBootStackLifetime.Active
            && activeResult.ReservationTableStatus
                == BootReservationTableStatus.Uninitialized
            && activeResult.ReservationCount == 0
            && active.Lifetime == OwnedBootStackLifetime.Active
            && reserved.Lifetime == OwnedBootStackLifetime.Reserved
            && GeometryBytesEqual(reservedBytes, readyBytes)
            && GeometryBytesEqual(readyBytes, activeBytes);
    }

    private static bool TestInvalidTransitions()
    {
        OwnedBootStackLayout reserved = CreateLayout(
            0x0010_0000,
            guardPages: 1,
            stackPages: 2,
            capacity: 2).Layout;
        OwnedBootStackLayout ready =
            reserved.TransitionTo(OwnedBootStackLifetime.Ready).Layout;
        OwnedBootStackLayout active =
            ready.TransitionTo(OwnedBootStackLifetime.Active).Layout;

        OwnedBootStackLayoutResult defaultSource =
            default(OwnedBootStackLayout).TransitionTo(
                OwnedBootStackLifetime.Ready);
        OwnedBootStackLayoutResult invalidTarget =
            reserved.TransitionTo((OwnedBootStackLifetime)255);
        OwnedBootStackLayoutResult uninitializedTarget =
            reserved.TransitionTo(OwnedBootStackLifetime.Uninitialized);
        OwnedBootStackLayoutResult repeatReserved =
            reserved.TransitionTo(OwnedBootStackLifetime.Reserved);
        OwnedBootStackLayoutResult skip =
            reserved.TransitionTo(OwnedBootStackLifetime.Active);
        OwnedBootStackLayoutResult repeatReady =
            ready.TransitionTo(OwnedBootStackLifetime.Ready);
        OwnedBootStackLayoutResult reverseReady =
            ready.TransitionTo(OwnedBootStackLifetime.Reserved);
        OwnedBootStackLayoutResult repeatActive =
            active.TransitionTo(OwnedBootStackLifetime.Active);
        OwnedBootStackLayoutResult reverseActiveReady =
            active.TransitionTo(OwnedBootStackLifetime.Ready);
        OwnedBootStackLayoutResult reverseActiveReserved =
            active.TransitionTo(OwnedBootStackLifetime.Reserved);

        return HasTransitionFailure(
                defaultSource,
                OwnedBootStackLayoutStatus.InvalidLayout,
                default,
                OwnedBootStackLifetime.Ready)
            && HasTransitionFailure(
                invalidTarget,
                OwnedBootStackLayoutStatus.InvalidLifetime,
                reserved,
                (OwnedBootStackLifetime)255)
            && HasTransitionFailure(
                uninitializedTarget,
                OwnedBootStackLayoutStatus.InvalidLifetime,
                reserved,
                OwnedBootStackLifetime.Uninitialized)
            && HasTransitionFailure(
                repeatReserved,
                OwnedBootStackLayoutStatus.RepeatedLifetimeTransition,
                reserved,
                OwnedBootStackLifetime.Reserved)
            && HasTransitionFailure(
                skip,
                OwnedBootStackLayoutStatus.IllegalLifetimeTransition,
                reserved,
                OwnedBootStackLifetime.Active)
            && HasTransitionFailure(
                repeatReady,
                OwnedBootStackLayoutStatus.RepeatedLifetimeTransition,
                ready,
                OwnedBootStackLifetime.Ready)
            && HasTransitionFailure(
                reverseReady,
                OwnedBootStackLayoutStatus.IllegalLifetimeTransition,
                ready,
                OwnedBootStackLifetime.Reserved)
            && HasTransitionFailure(
                repeatActive,
                OwnedBootStackLayoutStatus.RepeatedLifetimeTransition,
                active,
                OwnedBootStackLifetime.Active)
            && HasTransitionFailure(
                reverseActiveReady,
                OwnedBootStackLayoutStatus.IllegalLifetimeTransition,
                active,
                OwnedBootStackLifetime.Ready)
            && HasTransitionFailure(
                reverseActiveReserved,
                OwnedBootStackLayoutStatus.IllegalLifetimeTransition,
                active,
                OwnedBootStackLifetime.Reserved)
            && reserved.Lifetime == OwnedBootStackLifetime.Reserved
            && ready.Lifetime == OwnedBootStackLifetime.Ready
            && active.Lifetime == OwnedBootStackLifetime.Active;
    }

    private static bool TestMalformedValuesFailClosed()
    {
        OwnedBootStackLayout valid = CreateLayout(
            0x0010_0000,
            guardPages: 1,
            stackPages: 2,
            capacity: 2).Layout;
        RawOwnedBootStackLayout raw =
            Unsafe.As<OwnedBootStackLayout, RawOwnedBootStackLayout>(
                ref valid);

        RawOwnedBootStackLayout[] malformed =
        [
            raw with { Lifetime = OwnedBootStackLifetime.Uninitialized },
            raw with { Lifetime = (OwnedBootStackLifetime)255 },
            raw with { Reserved0 = 1 },
            raw with { Reserved6 = 1 },
            raw with { BackingStart = default },
            raw with
            {
                BackingStart = UnalignedAddress(0x0010_0001)
            },
            raw with { BackingEndExclusive = Address(0x0010_4000) },
            raw with { BackingPageCount = 1 },
            raw with { BackingByteLength = 4096 },
            raw with { GuardReservation = default },
            raw with { StackReservation = default },
            raw with
            {
                GuardReservation = Reservation(
                    BootReservationKind.LoadedImage,
                    0x0010_0000,
                    1)
            },
            raw with
            {
                StackReservation = Reservation(
                    BootReservationKind.AllocatorArena,
                    0x0010_1000,
                    2)
            },
            raw with { PhysicalStackTop = default },
            raw with
            {
                PhysicalStackTop = UnalignedAddress(0x0010_2ff8)
            },
            raw with { PhysicalStackTop = Address(0x0010_2000) }
        ];

        foreach (RawOwnedBootStackLayout malformedRaw in malformed)
        {
            RawOwnedBootStackLayout copy = malformedRaw;
            OwnedBootStackLayout malformedLayout = Unsafe.As<
                RawOwnedBootStackLayout,
                OwnedBootStackLayout>(ref copy);
            OwnedBootStackLayoutResult transition =
                malformedLayout.TransitionTo(OwnedBootStackLifetime.Ready);
            if (malformedLayout.IsValid
                || transition.Succeeded
                || transition.Status
                    != OwnedBootStackLayoutStatus.InvalidLayout)
            {
                return false;
            }
        }

        OwnedBootStackLayoutResult successful = CreateLayout(
            0x0020_0000,
            guardPages: 1,
            stackPages: 1,
            capacity: 2);
        RawOwnedBootStackLayoutResult rawResult = Unsafe.As<
            OwnedBootStackLayoutResult,
            RawOwnedBootStackLayoutResult>(ref successful);
        RawOwnedBootStackLayoutResult[] malformedResults =
        [
            rawResult with
            {
                Status = OwnedBootStackLayoutStatus.Uninitialized
            },
            rawResult with
            {
                AddressStatus = AddressValueStatus.Uninitialized
            },
            rawResult with
            {
                ReservationTableStatus =
                    BootReservationTableStatus.Uninitialized
            },
            rawResult with
            {
                RangeStatus = BootReservedRangeStatus.Uninitialized
            },
            rawResult with { Reserved0 = 1 },
            rawResult with
            {
                PreviousLifetime = OwnedBootStackLifetime.Ready
            },
            rawResult with
            {
                RequestedLifetime = OwnedBootStackLifetime.Active
            },
            rawResult with
            {
                CurrentLifetime = OwnedBootStackLifetime.Active
            },
            rawResult with { ReservationCount = 1 },
            rawResult with { ValidatedReservationCount = 1 },
            rawResult with { AvailableReservationCapacity = 1 },
            rawResult with { InvalidReservationIndex = 0 },
            rawResult with { ConflictingReservationIndex = 0 },
            rawResult with { Layout = default }
        ];

        foreach (RawOwnedBootStackLayoutResult malformedRaw
            in malformedResults)
        {
            RawOwnedBootStackLayoutResult copy = malformedRaw;
            OwnedBootStackLayoutResult malformedResult = Unsafe.As<
                RawOwnedBootStackLayoutResult,
                OwnedBootStackLayoutResult>(ref copy);
            if (malformedResult.Succeeded)
            {
                return false;
            }
        }

        return true;
    }

    private static unsafe bool TestValidationPrecedence()
    {
        BootReservedRange[] malformed = [default, default];
        PhysicalAddress validBacking = Address(0x0010_0000);
        PhysicalAddress validTop = Address(0x0010_2000);

        OwnedBootStackLayoutResult countDominates =
            OwnedBootStackLayout.Create(
                null,
                0,
                0,
                default,
                0,
                default);
        OwnedBootStackLayoutResult capacityDominates =
            OwnedBootStackLayout.Create(
                null,
                2,
                1,
                default,
                0,
                default);
        OwnedBootStackLayoutResult nullDominates =
            OwnedBootStackLayout.Create(
                null,
                2,
                2,
                default,
                0,
                default);
        OwnedBootStackLayoutResult pointerDominates =
            OwnedBootStackLayout.Create(
                (BootReservedRange*)1,
                2,
                2,
                default,
                0,
                default);
        OwnedBootStackLayoutResult backingDominates = Create(
            malformed,
            default,
            0,
            default);
        OwnedBootStackLayoutResult backingEndDominates = Create(
            malformed,
            Address(0xffff_ffff_ffff_f000),
            2,
            default);
        OwnedBootStackLayoutResult topDominates = Create(
            malformed,
            validBacking,
            2,
            default);
        OwnedBootStackLayoutResult tableDominates = Create(
            malformed,
            validBacking,
            2,
            validTop);

        return countDominates.Status
                == OwnedBootStackLayoutStatus.InvalidReservationCount
            && capacityDominates.Status
                == OwnedBootStackLayoutStatus
                    .ReservationCapacityTooSmall
            && nullDominates.Status
                == OwnedBootStackLayoutStatus.MissingReservationTable
            && pointerDominates.Status
                == OwnedBootStackLayoutStatus.MisalignedReservationTable
            && backingDominates.Status
                == OwnedBootStackLayoutStatus.InvalidBackingStart
            && backingEndDominates.Status
                == OwnedBootStackLayoutStatus.BackingEndOverflow
            && topDominates.Status
                == OwnedBootStackLayoutStatus.InvalidStackTop
            && tableDominates.Status
                == OwnedBootStackLayoutStatus.ReservationTableRejected
            && tableDominates.ReservationTableStatus
                == BootReservationTableStatus.InvalidReservation
            && tableDominates.InvalidReservationIndex == 0;
    }

    private static unsafe bool TestDeterminismImmutabilityAndAllocation()
    {
        BootReservedRange[] table = ValidTable(
            0x0010_0000,
            guardPages: 1,
            stackPages: 8);
        byte[] before = StructBytes(table);
        PhysicalAddress backing = Address(0x0010_0000);
        PhysicalAddress top = Address(0x0010_9000);

        OwnedBootStackLayoutResult first;
        OwnedBootStackLayoutResult second;
        OwnedBootStackLayoutResult firstFailure;
        OwnedBootStackLayoutResult secondFailure;
        OwnedBootStackLayoutResult last = default;

        fixed (BootReservedRange* pointer = table)
        {
            first = OwnedBootStackLayout.Create(
                pointer,
                2,
                2,
                backing,
                9,
                top);
            second = OwnedBootStackLayout.Create(
                pointer,
                2,
                2,
                backing,
                9,
                top);
            firstFailure = OwnedBootStackLayout.Create(
                pointer,
                2,
                2,
                backing,
                1,
                top);
            secondFailure = OwnedBootStackLayout.Create(
                pointer,
                2,
                2,
                backing,
                1,
                top);

            for (int index = 0; index < 32; index++)
            {
                OwnedBootStackLayoutResult warm =
                    OwnedBootStackLayout.Create(
                        pointer,
                        2,
                        2,
                        backing,
                        9,
                        top);
                OwnedBootStackLayoutResult ready =
                    warm.Layout.TransitionTo(
                        OwnedBootStackLifetime.Ready);
                last = ready.Layout.TransitionTo(
                    OwnedBootStackLifetime.Active);
                _ = OwnedBootStackLayout.Create(
                    pointer,
                    2,
                    2,
                    backing,
                    1,
                    top);
            }

            long allocatedBefore =
                GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                OwnedBootStackLayoutResult created =
                    OwnedBootStackLayout.Create(
                        pointer,
                        2,
                        2,
                        backing,
                        9,
                        top);
                OwnedBootStackLayoutResult ready =
                    created.Layout.TransitionTo(
                        OwnedBootStackLifetime.Ready);
                last = ready.Layout.TransitionTo(
                    OwnedBootStackLifetime.Active);
                _ = OwnedBootStackLayout.Create(
                    pointer,
                    2,
                    2,
                    backing,
                    1,
                    top);
            }

            long allocatedAfter =
                GC.GetAllocatedBytesForCurrentThread();
            if (allocatedAfter != allocatedBefore)
            {
                Console.WriteLine(
                    "M01_004_ALLOCATION_DIAGNOSTIC bytes="
                    + (allocatedAfter - allocatedBefore));
                return false;
            }
        }

        return first.Succeeded
            && second.Succeeded
            && first.Equals(second)
            && firstFailure.Equals(secondFailure)
            && last.Succeeded
            && last.CurrentLifetime == OwnedBootStackLifetime.Active
            && before.SequenceEqual(StructBytes(table));
    }

    private static bool TestNoRuntimeSourceSurface()
    {
        string sourcePath = GetLayoutSourcePath();
        string source = RemoveLineComments(File.ReadAllText(sourcePath));
        string[] forbiddenFragments =
        [
            "using ",
            "System.",
            "throw",
            "catch",
            "finally",
            "typeof",
            "Reflection",
            "Activator",
            "dynamic",
            "delegate",
            "interface",
            "record",
            "string",
            "object",
            "Span<",
            "ReadOnlySpan<",
            "Memory<",
            "ReadOnlyMemory<",
            "List<",
            "Dictionary<",
            "IEnumerable<",
            "ulong.MaxValue",
            "stackalloc",
            "fixed",
            "operator ",
            "RuntimeImport",
            "RuntimeExport",
            "DllImport",
            "LibraryImport",
            "NativeLibrary",
            "UnmanagedCallersOnly",
            "delegate*",
            "AllocatePages",
            "FreePages",
            "OpenProtocol",
            "HandleProtocol",
            "BootServices",
            "EfiEntry",
            "BootSequence",
            "Probes",
            "VirtualAddress",
            "DeviceAddress",
            " RSP",
            "mov rsp"
        ];

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
                @"\b[A-Za-z_][A-Za-z0-9_]*\s*\[\s*\]",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                source,
                @"\bnew\s*\(",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                source,
                @"reservations\s*\[[^\]]+\]\s*=",
                RegexOptions.CultureInvariant))
        {
            return SourceFirewallFailure("lexical-surface");
        }

        foreach (Match allocation in Regex.Matches(
            source,
            @"\bnew\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant))
        {
            string targetType = allocation.Groups[1].Value;
            if (targetType != nameof(OwnedBootStackLayout)
                && targetType != nameof(OwnedBootStackLayoutResult))
            {
                return SourceFirewallFailure(
                    "allocation-target=" + targetType);
            }
        }

        if (!source.Contains(
                "BootReservationTableValidator.Validate(",
                StringComparison.Ordinal)
            || !source.Contains(
                "RequiredReservationCount = 2",
                StringComparison.Ordinal)
            || !source.Contains(
                "X64StackAlignmentBytes = 16",
                StringComparison.Ordinal)
            || !source.Contains(
                "OwnedBootStackLifetime.Reserved",
                StringComparison.Ordinal)
            || !source.Contains(
                "OwnedBootStackLifetime.Ready",
                StringComparison.Ordinal)
            || !source.Contains(
                "OwnedBootStackLifetime.Active",
                StringComparison.Ordinal))
        {
            return SourceFirewallFailure("required-contract-token");
        }

        Type[] types =
        [
            typeof(OwnedBootStackLayout),
            typeof(OwnedBootStackLayoutResult)
        ];
        foreach (Type type in types)
        {
            if (!HasOnlyValueTypeDeclaredSurface(type))
            {
                return SourceFirewallFailure(
                    "managed-surface=" + type.Name);
            }

            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsValueType
                    || field.FieldType.IsPointer
                    || field.FieldType.IsArray
                    || field.IsStatic && !field.IsLiteral
                    || !field.IsStatic && !field.IsInitOnly)
                {
                    return SourceFirewallFailure(
                        "field-surface="
                        + type.Name
                        + "."
                        + field.Name);
                }
            }
        }

        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        if (!BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Boot\OwnedBootStackLayout.cs",
                @"Boot\OwnedBootStackLayout.cs")
            || source.Contains(
                "CLROS_M01_004_READY",
                StringComparison.Ordinal))
        {
            return SourceFirewallFailure("canonical-efi-boundary");
        }

        string testSourcePath = Path.GetFullPath(GetTestSourcePath());
        List<string> markerSources = [];
        foreach (string candidate in Directory.EnumerateFiles(
            repositoryRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            string fullCandidate = Path.GetFullPath(candidate);
            if (IsGeneratedOrReferencePath(fullCandidate)
                || !File.ReadAllText(fullCandidate).Contains(
                    "CLROS_M01_004_READY",
                    StringComparison.Ordinal))
            {
                continue;
            }

            markerSources.Add(fullCandidate);
        }

        if (markerSources.Count != 1
            || !string.Equals(
                markerSources[0],
                testSourcePath,
                StringComparison.OrdinalIgnoreCase)
            || Regex.Matches(
                File.ReadAllText(testSourcePath),
                "Console\\.WriteLine\\(\"CLROS_M01_004_READY\"\\)",
                RegexOptions.CultureInvariant).Count != 1)
        {
            return SourceFirewallFailure("host-marker-boundary");
        }

        return true;
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();

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

                internal static unsafe class PositiveOwnedBootStackProbe
                {
                    public static OwnedBootStackLayoutStatus Exercise()
                    {
                        if (PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                                0x100000UL,
                                BootReservedRange.PageSize,
                                out PhysicalAddress backing)
                            != AddressValueStatus.Success
                            || PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                                0x101000UL,
                                BootReservedRange.PageSize,
                                out PhysicalAddress stackStart)
                            != AddressValueStatus.Success
                            || PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                                0x103000UL,
                                OwnedBootStackLayout.X64StackAlignmentBytes,
                                out PhysicalAddress stackTop)
                            != AddressValueStatus.Success)
                        {
                            return OwnedBootStackLayoutStatus.InvalidBackingStart;
                        }

                        BootReservedRange* table =
                            stackalloc BootReservedRange[2];
                        if (BootReservedRange.TryCreate(
                                BootReservationKind.BootStackGuard,
                                backing,
                                1UL,
                                out table[0])
                            != BootReservedRangeStatus.Success
                            || BootReservedRange.TryCreate(
                                BootReservationKind.BootStack,
                                stackStart,
                                2UL,
                                out table[1])
                            != BootReservedRangeStatus.Success)
                        {
                            return OwnedBootStackLayoutStatus
                                .ReservationTableRejected;
                        }

                        OwnedBootStackLayoutResult created =
                            OwnedBootStackLayout.Create(
                                table,
                                2UL,
                                2UL,
                                backing,
                                3UL,
                                stackTop);
                        if (!created.Succeeded
                            || created.Layout.PhysicalStackTop.Equals(
                                default(PhysicalAddress)))
                        {
                            return created.Status;
                        }

                        OwnedBootStackLayoutResult ready =
                            created.Layout.TransitionTo(
                                OwnedBootStackLifetime.Ready);
                        OwnedBootStackLayoutResult active =
                            ready.Layout.TransitionTo(
                                OwnedBootStackLifetime.Active);
                        return active.Status;
                    }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            NoStdLibBuildResult result = RunNoStdLibBuild(workspace);
            string outputAssembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.OwnedBootStackCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasZeroAssemblyReferences(outputAssembly))
            {
                Console.WriteLine(
                    "M01_004_NOSTDLIB_COMPILE_DIAGNOSTIC "
                    + NormalizeCompilerOutput(result.Output));
                return false;
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static bool EnumMatches<TEnum>(
        (string Name, byte Value)[] expected)
        where TEnum : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(TEnum)) != typeof(byte)
            || Enum.GetValues<TEnum>().Length != expected.Length)
        {
            return false;
        }

        foreach ((string name, byte value) in expected)
        {
            byte rawValue = value;
            TEnum item = Unsafe.As<byte, TEnum>(ref rawValue);
            if (Enum.GetName(item) != name)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReadOnlyPod<T>(int expectedSize)
        where T : struct
    {
        Type type = typeof(T);
        return !RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            && type.IsLayoutSequential
            && type.IsValueType
            && type.IsDefined(
                typeof(IsReadOnlyAttribute),
                inherit: false)
            && Unsafe.SizeOf<T>() == expectedSize
            && Marshal.SizeOf<T>() == expectedSize
            && type.GetFields(
                    BindingFlags.Instance
                        | BindingFlags.NonPublic
                        | BindingFlags.Public
                        | BindingFlags.DeclaredOnly)
                .All(field =>
                    field.IsInitOnly && field.FieldType.IsValueType);
    }

    private static bool HasOnlyPrivateConstructors<T>()
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
        (string FieldName, int Offset)[] expected)
    {
        Type type = typeof(T);
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
        if (fields.Length != expected.Length)
        {
            return false;
        }

        foreach ((string fieldName, int offset) in expected)
        {
            FieldInfo? field = fields.SingleOrDefault(
                candidate => candidate.Name == fieldName);
            if (field is null
                || Marshal.OffsetOf<T>(fieldName).ToInt32() != offset)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasOnlyValueTypeDeclaredSurface(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(
            BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly))
        {
            if (!IsValueOnlyType(method.ReturnType)
                || method.GetParameters().Any(parameter =>
                    !IsValueOnlyType(parameter.ParameterType)))
            {
                return false;
            }
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(
            BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly))
        {
            if (constructor.GetParameters().Any(parameter =>
                !IsValueOnlyType(parameter.ParameterType)))
            {
                return false;
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

    private static bool IsSuccessfulLayout(
        OwnedBootStackLayoutResult result,
        ulong backingPages,
        ulong guardPages,
        ulong stackPages)
    {
        OwnedBootStackLayout layout = result.Layout;
        return result.Succeeded
            && result.Status == OwnedBootStackLayoutStatus.Success
            && result.AddressStatus == AddressValueStatus.Success
            && result.ReservationTableStatus
                == BootReservationTableStatus.Success
            && result.RangeStatus == BootReservedRangeStatus.Success
            && result.PreviousLifetime
                == OwnedBootStackLifetime.Uninitialized
            && result.RequestedLifetime
                == OwnedBootStackLifetime.Reserved
            && result.CurrentLifetime
                == OwnedBootStackLifetime.Reserved
            && result.ReservationCount == 2
            && result.ValidatedReservationCount == 2
            && result.InvalidReservationIndex
                == BootReservationTableValidationResult.NoIndex
            && result.ConflictingReservationIndex
                == BootReservationTableValidationResult.NoIndex
            && layout.IsValid
            && layout.Lifetime == OwnedBootStackLifetime.Reserved
            && layout.BackingPageCount == backingPages
            && layout.BackingByteLength
                == backingPages * BootReservedRange.PageSize
            && layout.GuardReservation.Kind
                == BootReservationKind.BootStackGuard
            && layout.GuardReservation.PageCount == guardPages
            && layout.StackReservation.Kind
                == BootReservationKind.BootStack
            && layout.StackReservation.PageCount == stackPages
            && layout.GuardReservation.PhysicalEndExclusive.Equals(
                layout.StackReservation.PhysicalStart)
            && layout.StackReservation.PhysicalEndExclusive.Equals(
                layout.BackingEndExclusive)
            && layout.PhysicalStackTop.Equals(
                layout.StackReservation.PhysicalEndExclusive)
            && layout.PhysicalStackTop.IsAligned(
                OwnedBootStackLayout.X64StackAlignmentBytes);
    }

    private static bool HasFailure(
        OwnedBootStackLayoutResult result,
        OwnedBootStackLayoutStatus status,
        BootReservationTableStatus tableStatus,
        AddressValueStatus addressStatus =
            AddressValueStatus.Uninitialized)
    {
        return !result.Succeeded
            && result.Status == status
            && result.AddressStatus == addressStatus
            && result.ReservationTableStatus == tableStatus
            && !result.Layout.IsValid;
    }

    private static bool HasRejectedTable(
        OwnedBootStackLayoutResult result,
        BootReservationTableStatus tableStatus,
        BootReservedRangeStatus rangeStatus,
        ulong invalidIndex,
        OwnedBootStackLayoutStatus layoutStatus =
            OwnedBootStackLayoutStatus.ReservationTableRejected)
    {
        return !result.Succeeded
            && result.Status == layoutStatus
            && result.ReservationTableStatus == tableStatus
            && result.RangeStatus == rangeStatus
            && result.InvalidReservationIndex == invalidIndex
            && !result.Layout.IsValid;
    }

    private static bool HasTransitionFailure(
        OwnedBootStackLayoutResult result,
        OwnedBootStackLayoutStatus status,
        OwnedBootStackLayout expectedCurrent,
        OwnedBootStackLifetime expectedRequested)
    {
        return !result.Succeeded
            && result.Status == status
            && result.AddressStatus == AddressValueStatus.Uninitialized
            && result.ReservationTableStatus
                == BootReservationTableStatus.Uninitialized
            && result.RangeStatus == BootReservedRangeStatus.Uninitialized
            && result.PreviousLifetime == expectedCurrent.Lifetime
            && result.RequestedLifetime == expectedRequested
            && result.CurrentLifetime == expectedCurrent.Lifetime
            && result.ReservationCount == 0
            && result.ValidatedReservationCount == 0
            && result.AvailableReservationCapacity == 0
            && result.InvalidReservationIndex
                == BootReservationTableValidationResult.NoIndex
            && result.ConflictingReservationIndex
                == BootReservationTableValidationResult.NoIndex
            && result.Layout.Equals(expectedCurrent);
    }

    private static bool GeometryBytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != 128 || right.Length != 128)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (index == 120)
            {
                continue;
            }

            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static OwnedBootStackLayoutResult CreateLayout(
        ulong backingStart,
        ulong guardPages,
        ulong stackPages,
        ulong capacity)
    {
        BootReservedRange[] table = ValidTable(
            backingStart,
            guardPages,
            stackPages);
        ulong totalPages = guardPages + stackPages;
        ulong top = backingStart
            + totalPages * BootReservedRange.PageSize;
        return Create(
            table,
            Address(backingStart),
            totalPages,
            Address(top),
            capacity);
    }

    private static OwnedBootStackLayoutResult Create(
        BootReservedRange[] reservations,
        PhysicalAddress backingStart,
        ulong backingPageCount,
        PhysicalAddress physicalStackTop,
        ulong? capacity = null)
    {
        unsafe
        {
            fixed (BootReservedRange* pointer = reservations)
            {
                return OwnedBootStackLayout.Create(
                    pointer,
                    2,
                    capacity ?? (ulong)reservations.Length,
                    backingStart,
                    backingPageCount,
                    physicalStackTop);
            }
        }
    }

    private static BootReservedRange[] ValidTable(
        ulong backingStart,
        ulong guardPages,
        ulong stackPages)
    {
        return
        [
            Reservation(
                BootReservationKind.BootStackGuard,
                backingStart,
                guardPages),
            Reservation(
                BootReservationKind.BootStack,
                backingStart
                    + guardPages * BootReservedRange.PageSize,
                stackPages)
        ];
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
                "Test reservation construction failed: " + status);
        }

        return range;
    }

    private static PhysicalAddress Address(ulong value)
    {
        AddressValueStatus status =
            PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                value,
                BootReservedRange.PageSize,
                out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Test aligned physical address construction failed: "
                + status);
        }

        return address;
    }

    private static PhysicalAddress UnalignedAddress(ulong value)
    {
        AddressValueStatus status =
            PhysicalAddress.TryCreateFromUefiBoundary(
                value,
                out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Test physical address construction failed: " + status);
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
                "Test raw-address extraction failed: " + status);
        }

        return value;
    }

    private static BootReservedRange RawRange(
        BootReservationKind kind,
        PhysicalAddress physicalStart,
        PhysicalAddress physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        int reservedByteIndex = -1)
    {
        RawBootReservedRange raw = new RawBootReservedRange
        {
            Kind = kind,
            PhysicalStart = physicalStart,
            PhysicalEndExclusive = physicalEndExclusive,
            PageCount = pageCount,
            ByteLength = byteLength
        };

        switch (reservedByteIndex)
        {
            case -1:
                break;
            case 0:
                raw.Reserved0 = 1;
                break;
            case 1:
                raw.Reserved1 = 1;
                break;
            case 2:
                raw.Reserved2 = 1;
                break;
            case 3:
                raw.Reserved3 = 1;
                break;
            case 4:
                raw.Reserved4 = 1;
                break;
            case 5:
                raw.Reserved5 = 1;
                break;
            case 6:
                raw.Reserved6 = 1;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(reservedByteIndex));
        }

        return Unsafe.As<RawBootReservedRange, BootReservedRange>(
            ref raw);
    }

    private static byte[] StructBytes<T>(T value)
        where T : struct
    {
        T[] values = [value];
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private static byte[] StructBytes<T>(T[] values)
        where T : struct
    {
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private static bool SourceFirewallFailure(string reason)
    {
        Console.WriteLine(
            "M01_004_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
        return false;
    }

    private static string RemoveLineComments(string source)
    {
        StringBuilder result = new StringBuilder(source.Length);
        using StringReader reader = new StringReader(source);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            int commentStart = line.IndexOf(
                "//",
                StringComparison.Ordinal);
            result.AppendLine(
                commentStart >= 0 ? line[..commentStart] : line);
        }

        return result.ToString();
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-004-" + Guid.NewGuid().ToString("N"));
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
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.OwnedBootStackCompileClosure</AssemblyName>
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
                <Compile Include="__TABLE_VALIDATOR__" Link="Memory\BootReservationTableValidator.cs" />
                <Compile Include="__STACK_LAYOUT__" Link="Boot\OwnedBootStackLayout.cs" />
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
                EscapeXml(GetRangeSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__TABLE_VALIDATOR__",
                EscapeXml(GetValidatorSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__STACK_LAYOUT__",
                EscapeXml(GetLayoutSourcePath()),
                StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(workspace, "M01.004.NoStdLib.csproj"),
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
                // Preserve the workspace-creation failure as primary.
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
        start.ArgumentList.Add("M01.004.NoStdLib.csproj");
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

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90_000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Timeout remains the primary evidence.
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
        using PEReader peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = peReader.GetMetadataReader();
        return metadata.AssemblyReferences.Count == 0;
    }

    private static bool ContainsInfrastructureFailure(string output)
    {
        return output.Contains("MSB1009", StringComparison.OrdinalIgnoreCase)
            || output.Contains("NU1301", StringComparison.OrdinalIgnoreCase)
            || output.Contains(
                "A compatible .NET SDK was not found",
                StringComparison.OrdinalIgnoreCase);
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

    private static string GetTypedAddressSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
    }

    private static string GetRangeSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootReservedRange.cs");
    }

    private static string GetValidatorSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootReservationTableValidator.cs");
    }

    private static string GetLayoutSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "OwnedBootStackLayout.cs");
    }

    private static string GetTestSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "tests",
            "OS.HostTests",
            "OwnedBootStackLayoutTests.cs");
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
                @"\Aclros-m01-004-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-004 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(fullWorkspace);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Refusing to delete a reparse-point M01-004 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawBootReservedRange
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
    private record struct RawOwnedBootStackLayout
    {
        public PhysicalAddress BackingStart;
        public PhysicalAddress BackingEndExclusive;
        public ulong BackingPageCount;
        public ulong BackingByteLength;
        public BootReservedRange GuardReservation;
        public BootReservedRange StackReservation;
        public PhysicalAddress PhysicalStackTop;
        public OwnedBootStackLifetime Lifetime;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
        public byte Reserved4;
        public byte Reserved5;
        public byte Reserved6;
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct RawOwnedBootStackLayoutResult
    {
        public OwnedBootStackLayoutStatus Status;
        public AddressValueStatus AddressStatus;
        public BootReservationTableStatus ReservationTableStatus;
        public BootReservedRangeStatus RangeStatus;
        public OwnedBootStackLifetime PreviousLifetime;
        public OwnedBootStackLifetime RequestedLifetime;
        public OwnedBootStackLifetime CurrentLifetime;
        public byte Reserved0;
        public ulong ReservationCount;
        public ulong ValidatedReservationCount;
        public ulong AvailableReservationCapacity;
        public ulong InvalidReservationIndex;
        public ulong ConflictingReservationIndex;
        public OwnedBootStackLayout Layout;
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
