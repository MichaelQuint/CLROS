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

internal static class BootReservationTableValidatorTests
{
    private const string IssueTestName = "m01.002";

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run("m01.002 reservation kinds are stable and append only", TestStableKinds, ref failures);
        Run("m01.002 reservation records and results are unmanaged pods", TestPodAndApiSurface, ref failures);
        Run("m01.002 range factory creates typed half-open ranges", TestRangeFactorySuccess, ref failures);
        Run("m01.002 range factory rejects default alignment and overflow", TestRangeFactoryFailures, ref failures);
        Run("m01.002 validator rejects malformed reservation records", TestMalformedReservations, ref failures);
        Run("m01.002 validator accepts canonical sorted disjoint table", TestCanonicalTable, ref failures);
        Run("m01.002 validator classifies ordering and overlap conflicts", TestOrderingAndOverlap, ref failures);
        Run("m01.002 loaded image rejects duplicate metadata coverage", TestLoadedImageCoveragePolicy, ref failures);
        Run("m01.002 guard and stack exactly account for modeled arena", TestBootStackArenaPolicy, ref failures);
        Run("m01.002 pointer count and capacity fail before dereference", TestPointerCountAndCapacity, ref failures);
        Run("m01.002 validator is deterministic nonmutating and allocation free", TestDeterminismImmutabilityAndAllocation, ref failures);
        Run("m01.002 source excludes hosted runtime dependencies", TestNoRuntimeSourceSurface, ref failures);
        Run("m01.002 exact sources compile under nostdlib efi", TestNoStdLibEfiCompileClosure, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-002 boot reservation table host tests failed: " + failures);
        }

        Console.WriteLine("CLROS_M01_002_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestStableKinds()
    {
        (string Name, BootReservationKind Kind, byte Value)[] required =
        [
            ("Uninitialized", BootReservationKind.Uninitialized, 0),
            ("LoadedImage", BootReservationKind.LoadedImage, 1),
            ("BootStackSwitchStub", BootReservationKind.BootStackSwitchStub, 2),
            ("BootStackGuard", BootReservationKind.BootStackGuard, 3),
            ("BootStack", BootReservationKind.BootStack, 4),
            ("OwnedMemoryMap", BootReservationKind.OwnedMemoryMap, 5),
            ("AcpiSnapshot", BootReservationKind.AcpiSnapshot, 6),
            ("AcpiScratch", BootReservationKind.AcpiScratch, 7),
            ("DiagnosticState", BootReservationKind.DiagnosticState, 8),
            ("AllocatorArena", BootReservationKind.AllocatorArena, 9),
            ("PageTableArena", BootReservationKind.PageTableArena, 10),
            ("Cr3TransitionStub", BootReservationKind.Cr3TransitionStub, 11),
            ("BootHandoff", BootReservationKind.BootHandoff, 12),
            ("FramebufferMetadata", BootReservationKind.FramebufferMetadata, 13),
            ("PageProtectionProbe", BootReservationKind.PageProtectionProbe, 14),
            ("LinuxElfPackage", BootReservationKind.LinuxElfPackage, 15),
            ("LinuxElfBacking", BootReservationKind.LinuxElfBacking, 16),
            ("LinuxElfUserTables", BootReservationKind.LinuxElfUserTables, 17),
            ("LinuxSyscallCode", BootReservationKind.LinuxSyscallCode, 18),
            ("LinuxSyscallData", BootReservationKind.LinuxSyscallData, 19)
        ];

        if (Enum.GetUnderlyingType(typeof(BootReservationKind)) != typeof(byte))
        {
            return false;
        }

        HashSet<byte> observedValues = new HashSet<byte>();
        foreach (BootReservationKind value in Enum.GetValues<BootReservationKind>())
        {
            byte numericValue = (byte)value;
            if (!observedValues.Add(numericValue))
            {
                return false;
            }

            if (numericValue > 14)
            {
                continue;
            }

            bool isRequired = false;
            foreach ((string _, BootReservationKind requiredKind, byte requiredValue) in required)
            {
                if (numericValue == requiredValue && value == requiredKind)
                {
                    isRequired = true;
                    break;
                }
            }

            if (!isRequired)
            {
                return false;
            }
        }

        string source = RemoveLineComments(
            File.ReadAllText(GetRangeSourcePath()));
        Match enumMatch = Regex.Match(
            source,
            @"public\s+enum\s+BootReservationKind\s*:\s*byte\s*\{(?<body>[^}]*)\}",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        if (!enumMatch.Success)
        {
            return false;
        }

        MatchCollection declarations = Regex.Matches(
            enumMatch.Groups["body"].Value,
            @"(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[0-9]+)\s*,?\s*$",
            RegexOptions.CultureInvariant);
        if (declarations.Count != observedValues.Count
            || declarations.Count < required.Length)
        {
            return false;
        }

        int previousValue = -1;
        for (int index = 0; index < declarations.Count; index++)
        {
            Match declaration = declarations[index];
            if (!byte.TryParse(
                    declaration.Groups["value"].Value,
                    out byte declaredValue)
                || declaredValue <= previousValue)
            {
                return false;
            }

            previousValue = declaredValue;
            string declaredName = declaration.Groups["name"].Value;
            if (index < required.Length)
            {
                (string requiredName, BootReservationKind requiredKind, byte requiredValue) =
                    required[index];
                if (declaredName != requiredName
                    || declaredValue != requiredValue
                    || (byte)requiredKind != requiredValue
                    || Enum.GetName(requiredKind) != requiredName)
                {
                    return false;
                }
            }
            else if (declaredValue <= (byte)BootReservationKind.PageProtectionProbe)
            {
                return false;
            }
        }

        PhysicalAddress start = Address(0x1000);
        foreach (BootReservationKind kind in Enum.GetValues<BootReservationKind>())
        {
            if (kind == BootReservationKind.Uninitialized)
            {
                continue;
            }

            if (BootReservedRange.TryCreate(
                    kind,
                    start,
                    1,
                    out BootReservedRange range)
                    != BootReservedRangeStatus.Success
                || !range.IsValid
                || range.Kind != kind)
            {
                return false;
            }
        }

        return observedValues.Count >= required.Length
            && BootReservationKind.Uninitialized != BootReservationKind.LoadedImage;
    }

    private static bool TestPodAndApiSurface()
    {
        Type rangeType = typeof(BootReservedRange);
        FieldInfo[] rangeFields = rangeType.GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Dictionary<string, (Type Type, int Offset)> expectedFields = new()
        {
            ["_kind"] = (typeof(BootReservationKind), 0),
            ["_reserved0"] = (typeof(byte), 1),
            ["_reserved1"] = (typeof(byte), 2),
            ["_reserved2"] = (typeof(byte), 3),
            ["_reserved3"] = (typeof(byte), 4),
            ["_reserved4"] = (typeof(byte), 5),
            ["_reserved5"] = (typeof(byte), 6),
            ["_reserved6"] = (typeof(byte), 7),
            ["_physicalStart"] = (typeof(PhysicalAddress), 8),
            ["_physicalEndExclusive"] = (typeof(PhysicalAddress), 16),
            ["_pageCount"] = (typeof(ulong), 24),
            ["_byteLength"] = (typeof(ulong), 32)
        };

        if (!rangeType.IsValueType
            || !rangeType.IsLayoutSequential
            || !rangeType.IsDefined(typeof(IsReadOnlyAttribute), inherit: false)
            || rangeFields.Length != expectedFields.Count
            || Marshal.SizeOf<BootReservedRange>() != 40
            || Unsafe.SizeOf<BootReservedRange>() != 40
            || RuntimeHelpers.IsReferenceOrContainsReferences<BootReservedRange>()
            || rangeType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public).Length != 0
            || typeof(BootMemoryReservedRange) == rangeType)
        {
            return false;
        }

        foreach (FieldInfo field in rangeFields)
        {
            if (!expectedFields.TryGetValue(
                    field.Name,
                    out (Type Type, int Offset) expected)
                || !field.IsPrivate
                || !field.IsInitOnly
                || field.FieldType != expected.Type
                || Marshal.OffsetOf<BootReservedRange>(field.Name).ToInt64()
                    != expected.Offset)
            {
                return false;
            }
        }

        MethodInfo? factory = rangeType.GetMethod(
            "TryCreate",
            BindingFlags.Public | BindingFlags.Static);
        ParameterInfo[]? factoryParameters = factory?.GetParameters();
        if (factory is null
            || factory.ReturnType != typeof(BootReservedRangeStatus)
            || factoryParameters is null
            || factoryParameters.Length != 4
            || factoryParameters[0].ParameterType != typeof(BootReservationKind)
            || factoryParameters[1].ParameterType != typeof(PhysicalAddress)
            || factoryParameters[2].ParameterType != typeof(ulong)
            || !factoryParameters[3].IsOut
            || factoryParameters[3].ParameterType.GetElementType()
                != typeof(BootReservedRange)
            || rangeType.GetProperty("PhysicalStart")?.PropertyType
                != typeof(PhysicalAddress)
            || rangeType.GetProperty("PhysicalEndExclusive")?.PropertyType
                != typeof(PhysicalAddress)
            || rangeType.GetProperty("Value") is not null
            || rangeType.GetMethods(
                BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Static)
                .Any(method => method.Name.StartsWith(
                    "op_",
                    StringComparison.Ordinal)))
        {
            return false;
        }

        Type resultType = typeof(BootReservationTableValidationResult);
        FieldInfo[] resultFields = resultType.GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (!resultType.IsValueType
            || !resultType.IsLayoutSequential
            || !resultType.IsDefined(typeof(IsReadOnlyAttribute), inherit: false)
            || RuntimeHelpers.IsReferenceOrContainsReferences<
                BootReservationTableValidationResult>()
            || resultFields.Length == 0
            || resultFields.Any(field =>
                !field.IsPrivate
                || !field.IsInitOnly
                || !field.FieldType.IsValueType)
            || default(BootReservationTableValidationResult).Succeeded
            || default(BootReservationTableValidationResult).Status
                == BootReservationTableStatus.Success
            || BootReservationTableValidationResult.NoIndex != ~0UL)
        {
            return false;
        }

        Type validatorType = typeof(BootReservationTableValidator);
        MethodInfo[] validateMethods = validatorType.GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (validateMethods.Length != 1
            || validateMethods[0].Name != "Validate"
            || validateMethods[0].ReturnType
                != typeof(BootReservationTableValidationResult))
        {
            return false;
        }

        ParameterInfo[] validatorParameters = validateMethods[0].GetParameters();
        return validatorParameters.Length == 3
            && validatorParameters[0].ParameterType.IsPointer
            && validatorParameters[0].ParameterType.GetElementType()
                == typeof(BootReservedRange)
            && validatorParameters[1].ParameterType == typeof(ulong)
            && validatorParameters[2].ParameterType == typeof(ulong)
            && typeof(BootReservationTableValidator)
                .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .All(field => field.IsLiteral);
    }

    private static bool TestRangeFactorySuccess()
    {
        PhysicalAddress arbitraryStart = Address(0x1234_5000);
        BootReservedRangeStatus status = BootReservedRange.TryCreate(
            BootReservationKind.AllocatorArena,
            arbitraryStart,
            pageCount: 3,
            out BootReservedRange range);
        PhysicalAddress expectedEnd = Address(0x1234_8000);

        PhysicalAddress edgeStart = Address(0xFFFF_FFFF_FFFF_D000);
        BootReservedRangeStatus edgeStatus = BootReservedRange.TryCreate(
            BootReservationKind.PageProtectionProbe,
            edgeStart,
            pageCount: 2,
            out BootReservedRange edge);

        return status == BootReservedRangeStatus.Success
            && range.IsValid
            && range.Kind == BootReservationKind.AllocatorArena
            && range.PhysicalStart.Equals(arbitraryStart)
            && range.PhysicalEndExclusive.Equals(expectedEnd)
            && range.PageCount == 3
            && range.ByteLength == 3 * BootReservedRange.PageSize
            && range.Equals(range)
            && edgeStatus == BootReservedRangeStatus.Success
            && edge.IsValid
            && edge.PhysicalEndExclusive.Equals(
                Address(0xFFFF_FFFF_FFFF_F000))
            && edge.ByteLength == 2 * BootReservedRange.PageSize;
    }

    private static bool TestRangeFactoryFailures()
    {
        PhysicalAddress aligned = Address(0x4000);
        PhysicalAddress unaligned = UnalignedAddress(0x4001);
        PhysicalAddress wrapping = Address(0xFFFF_FFFF_FFFF_F000);

        BootReservedRangeStatus uninitializedKind = BootReservedRange.TryCreate(
            BootReservationKind.Uninitialized,
            aligned,
            1,
            out BootReservedRange uninitializedKindRange);
        BootReservedRangeStatus unknownKind = BootReservedRange.TryCreate(
            (BootReservationKind)0xfe,
            aligned,
            1,
            out BootReservedRange unknownKindRange);
        BootReservedRangeStatus maximumUnknownKind = BootReservedRange.TryCreate(
            (BootReservationKind)byte.MaxValue,
            aligned,
            1,
            out BootReservedRange maximumUnknownKindRange);
        BootReservedRangeStatus invalidStart = BootReservedRange.TryCreate(
            BootReservationKind.LoadedImage,
            default,
            1,
            out BootReservedRange invalidStartRange);
        BootReservedRangeStatus misalignedStart = BootReservedRange.TryCreate(
            BootReservationKind.LoadedImage,
            unaligned,
            1,
            out BootReservedRange misalignedStartRange);
        BootReservedRangeStatus zeroPages = BootReservedRange.TryCreate(
            BootReservationKind.LoadedImage,
            aligned,
            0,
            out BootReservedRange zeroPagesRange);
        BootReservedRangeStatus byteOverflow = BootReservedRange.TryCreate(
            BootReservationKind.LoadedImage,
            aligned,
            (~0UL / BootReservedRange.PageSize) + 1,
            out BootReservedRange byteOverflowRange);
        BootReservedRangeStatus addressOverflow = BootReservedRange.TryCreate(
            BootReservationKind.LoadedImage,
            wrapping,
            1,
            out BootReservedRange addressOverflowRange);

        return uninitializedKind == BootReservedRangeStatus.UninitializedKind
            && unknownKind == BootReservedRangeStatus.UnknownKind
            && maximumUnknownKind == BootReservedRangeStatus.UnknownKind
            && invalidStart == BootReservedRangeStatus.InvalidPhysicalStart
            && misalignedStart == BootReservedRangeStatus.MisalignedPhysicalStart
            && zeroPages == BootReservedRangeStatus.InvalidPageCount
            && byteOverflow == BootReservedRangeStatus.PageByteCountOverflow
            && addressOverflow == BootReservedRangeStatus.AddressRangeOverflow
            && uninitializedKindRange.Equals(default)
            && unknownKindRange.Equals(default)
            && maximumUnknownKindRange.Equals(default)
            && invalidStartRange.Equals(default)
            && misalignedStartRange.Equals(default)
            && zeroPagesRange.Equals(default)
            && byteOverflowRange.Equals(default)
            && addressOverflowRange.Equals(default);
    }

    private static bool TestMalformedReservations()
    {
        PhysicalAddress start = Address(0x1000);
        PhysicalAddress end = Address(0x2000);

        (BootReservedRange Range, BootReservedRangeStatus Expected)[] cases =
        [
            (default, BootReservedRangeStatus.UninitializedKind),
            (Raw((BootReservationKind)0xfe, start, end, 1, 4096), BootReservedRangeStatus.UnknownKind),
            (Raw(BootReservationKind.LoadedImage, default, end, 1, 4096), BootReservedRangeStatus.InvalidPhysicalStart),
            (Raw(BootReservationKind.LoadedImage, UnalignedAddress(0x1001), end, 1, 4096), BootReservedRangeStatus.MisalignedPhysicalStart),
            (Raw(BootReservationKind.LoadedImage, start, end, 0, 0), BootReservedRangeStatus.InvalidPageCount),
            (Raw(BootReservationKind.LoadedImage, start, end, (~0UL / 4096) + 1, 0), BootReservedRangeStatus.PageByteCountOverflow),
            (Raw(BootReservationKind.LoadedImage, start, end, 1, 8192), BootReservedRangeStatus.ByteLengthMismatch),
            (Raw(BootReservationKind.LoadedImage, start, default, 1, 4096), BootReservedRangeStatus.InvalidPhysicalEndExclusive),
            (Raw(BootReservationKind.LoadedImage, start, UnalignedAddress(0x2001), 1, 4096), BootReservedRangeStatus.MisalignedPhysicalEndExclusive),
            (Raw(BootReservationKind.LoadedImage, Address(0xFFFF_FFFF_FFFF_F000), end, 1, 4096), BootReservedRangeStatus.AddressRangeOverflow),
            (Raw(BootReservationKind.LoadedImage, start, Address(0x3000), 1, 4096), BootReservedRangeStatus.PhysicalEndMismatch),
            (Raw(BootReservationKind.LoadedImage, start, Address(0x1000), 1, 4096), BootReservedRangeStatus.PhysicalEndMismatch)
        ];

        foreach ((BootReservedRange range, BootReservedRangeStatus expected) in cases)
        {
            BootReservationTableValidationResult result = Validate([range]);
            if (result.Succeeded
                || result.Status != BootReservationTableStatus.InvalidReservation
                || result.RangeStatus != expected
                || result.InvalidReservationIndex != 0
                || result.ConflictingReservationIndex
                    != BootReservationTableValidationResult.NoIndex
                || result.ValidatedReservationCount != 0
                || result.TotalPages != 0
                || result.TotalBytes != 0)
            {
                return false;
            }
        }

        for (int reservedByteIndex = 0; reservedByteIndex < 7; reservedByteIndex++)
        {
            BootReservationTableValidationResult reservedByteResult = Validate(
            [
                Raw(
                    BootReservationKind.LoadedImage,
                    start,
                    end,
                    1,
                    4096,
                    reservedByteIndex)
            ]);
            if (reservedByteResult.Status
                    != BootReservationTableStatus.InvalidReservation
                || reservedByteResult.RangeStatus
                    != BootReservedRangeStatus.ReservedBitsSet
                || reservedByteResult.ValidatedReservationCount != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestCanonicalTable()
    {
        BootReservedRange[] reservations =
        [
            Reservation(BootReservationKind.LoadedImage, 0x0010_0000, 16),
            Reservation(BootReservationKind.BootStackSwitchStub, 0x0020_0000, 1),
            Reservation(BootReservationKind.BootStackGuard, 0x0030_0000, 1),
            Reservation(BootReservationKind.BootStack, 0x0030_1000, 8),
            Reservation(BootReservationKind.AllocatorArena, 0x0040_0000, 16),
            Reservation(BootReservationKind.PageTableArena, 0x0050_0000, 8),
            Reservation(BootReservationKind.Cr3TransitionStub, 0x0060_0000, 1),
            Reservation(BootReservationKind.PageProtectionProbe, 0x0070_0000, 1),
            Reservation(BootReservationKind.AcpiSnapshot, 0x0080_0000, 2),
            Reservation(BootReservationKind.LinuxSyscallCode, 0x0090_0000, 1),
            Reservation(BootReservationKind.LinuxSyscallData, 0x00a0_0000, 20)
        ];

        BootReservationTableValidationResult result = Validate(
            reservations,
            (ulong)reservations.Length + 4);

        BootReservedRange[] adjacent =
        [
            Reservation(BootReservationKind.LoadedImage, 0x1000, 1),
            Reservation(BootReservationKind.OwnedMemoryMap, 0x2000, 1),
            Reservation(BootReservationKind.BootStackGuard, 0x3000, 1),
            Reservation(BootReservationKind.BootStack, 0x4000, 1)
        ];
        BootReservationTableValidationResult adjacentResult = Validate(adjacent);

        BootReservedRange[] partialWithoutStack =
        [
            Reservation(BootReservationKind.LoadedImage, 0x1000, 1),
            Reservation(BootReservationKind.AcpiSnapshot, 0x3000, 1)
        ];
        BootReservationTableValidationResult partialResult = Validate(
            partialWithoutStack);

        const ulong expectedPages = 75;
        return result.Succeeded
            && result.Status == BootReservationTableStatus.Success
            && result.RangeStatus == BootReservedRangeStatus.Success
            && result.ReservationCount == (ulong)reservations.Length
            && result.ValidatedReservationCount == (ulong)reservations.Length
            && result.RequiredReservationCapacity == (ulong)reservations.Length
            && result.AvailableReservationCapacity
                == (ulong)reservations.Length + 4
            && result.InvalidReservationIndex
                == BootReservationTableValidationResult.NoIndex
            && result.ConflictingReservationIndex
                == BootReservationTableValidationResult.NoIndex
            && result.FirstLoadedImageIndex == 0
            && result.BootStackGuardIndex == 2
            && result.BootStackIndex == 3
            && result.TotalPages == expectedPages
            && result.TotalBytes == expectedPages * BootReservedRange.PageSize
            && result.BootStackArenaPages == 9
            && result.BootStackArenaBytes == 9 * BootReservedRange.PageSize
            && adjacentResult.Succeeded
            && adjacentResult.TotalPages == 4
            && adjacentResult.BootStackArenaPages == 2
            && partialResult.Succeeded
            && partialResult.BootStackGuardIndex
                == BootReservationTableValidationResult.NoIndex
            && partialResult.BootStackIndex
                == BootReservationTableValidationResult.NoIndex
            && partialResult.BootStackArenaPages == 0
            && partialResult.BootStackArenaBytes == 0;
    }

    private static bool TestOrderingAndOverlap()
    {
        BootReservationTableValidationResult unsorted = Validate(
        [
            Reservation(BootReservationKind.AllocatorArena, 0x4000, 1),
            Reservation(BootReservationKind.LoadedImage, 0x1000, 1)
        ]);
        BootReservationTableValidationResult duplicate = Validate(
        [
            Reservation(BootReservationKind.AllocatorArena, 0x1000, 1),
            Reservation(BootReservationKind.PageTableArena, 0x1000, 1)
        ]);
        BootReservationTableValidationResult nested = Validate(
        [
            Reservation(BootReservationKind.AllocatorArena, 0x1000, 4),
            Reservation(BootReservationKind.PageTableArena, 0x2000, 1)
        ]);
        BootReservationTableValidationResult sameStartNested = Validate(
        [
            Reservation(BootReservationKind.AllocatorArena, 0x1000, 1),
            Reservation(BootReservationKind.PageTableArena, 0x1000, 2)
        ]);
        BootReservationTableValidationResult partialOverlap = Validate(
        [
            Reservation(BootReservationKind.AllocatorArena, 0x1000, 2),
            Reservation(BootReservationKind.PageTableArena, 0x2000, 2)
        ]);
        BootReservationTableValidationResult adjacency = Validate(
        [
            Reservation(BootReservationKind.AllocatorArena, 0x1000, 1),
            Reservation(BootReservationKind.PageTableArena, 0x2000, 1)
        ]);

        return HasConflict(
                unsorted,
                BootReservationTableStatus.UnsortedRange,
                invalidIndex: 1,
                conflictingIndex: 0,
                validatedCount: 1,
                prefixPages: 1)
            && HasConflict(
                duplicate,
                BootReservationTableStatus.DuplicateRange,
                1,
                0,
                1,
                1)
            && HasConflict(
                nested,
                BootReservationTableStatus.NestedRange,
                1,
                0,
                1,
                4)
            && HasConflict(
                sameStartNested,
                BootReservationTableStatus.NestedRange,
                1,
                0,
                1,
                1)
            && HasConflict(
                partialOverlap,
                BootReservationTableStatus.OverlappingRange,
                1,
                0,
                1,
                2)
            && adjacency.Succeeded
            && adjacency.TotalPages == 2
            && adjacency.TotalBytes == 2 * BootReservedRange.PageSize;
    }

    private static bool TestLoadedImageCoveragePolicy()
    {
        BootReservationKind[] metadataKinds =
        [
            BootReservationKind.OwnedMemoryMap,
            BootReservationKind.AcpiSnapshot,
            BootReservationKind.AcpiScratch,
            BootReservationKind.DiagnosticState,
            BootReservationKind.BootHandoff,
            BootReservationKind.FramebufferMetadata
        ];

        foreach (BootReservationKind metadataKind in metadataKinds)
        {
            BootReservationTableValidationResult contained = Validate(
            [
                Reservation(BootReservationKind.LoadedImage, 0x0010_0000, 16),
                Reservation(metadataKind, 0x0010_2000, 1)
            ]);
            if (!HasConflict(
                    contained,
                    BootReservationTableStatus.DuplicateLoadedImageCoverage,
                    invalidIndex: 1,
                    conflictingIndex: 0,
                    validatedCount: 1,
                    prefixPages: 16))
            {
                return false;
            }
        }

        BootReservationTableValidationResult reverseSameStart = Validate(
        [
            Reservation(BootReservationKind.BootHandoff, 0x0010_0000, 1),
            Reservation(BootReservationKind.LoadedImage, 0x0010_0000, 16)
        ]);
        BootReservationTableValidationResult external = Validate(
        [
            Reservation(BootReservationKind.LoadedImage, 0x0010_0000, 16),
            Reservation(BootReservationKind.OwnedMemoryMap, 0x0020_0000, 1)
        ]);
        BootReservationTableValidationResult partial = Validate(
        [
            Reservation(BootReservationKind.LoadedImage, 0x0010_0000, 16),
            Reservation(BootReservationKind.AcpiSnapshot, 0x0010_F000, 2)
        ]);

        return HasConflict(
                reverseSameStart,
                BootReservationTableStatus.DuplicateLoadedImageCoverage,
                1,
                0,
                1,
                1)
            && external.Succeeded
            && external.FirstLoadedImageIndex == 0
            && partial.Status == BootReservationTableStatus.OverlappingRange
            && partial.InvalidReservationIndex == 1
            && partial.ConflictingReservationIndex == 0;
    }

    private static bool TestBootStackArenaPolicy()
    {
        BootReservationTableValidationResult valid = Validate(
        [
            Reservation(BootReservationKind.BootStackGuard, 0x1000, 2),
            Reservation(BootReservationKind.BootStack, 0x3000, 3)
        ]);
        BootReservationTableValidationResult guardOnly = Validate(
        [
            Reservation(BootReservationKind.BootStackGuard, 0x1000, 1)
        ]);
        BootReservationTableValidationResult stackOnly = Validate(
        [
            Reservation(BootReservationKind.BootStack, 0x1000, 1)
        ]);
        BootReservationTableValidationResult reversed = Validate(
        [
            Reservation(BootReservationKind.BootStack, 0x1000, 1),
            Reservation(BootReservationKind.BootStackGuard, 0x2000, 1)
        ]);
        BootReservationTableValidationResult gap = Validate(
        [
            Reservation(BootReservationKind.BootStackGuard, 0x1000, 1),
            Reservation(BootReservationKind.BootStack, 0x3000, 1)
        ]);
        BootReservationTableValidationResult overlap = Validate(
        [
            Reservation(BootReservationKind.BootStackGuard, 0x1000, 2),
            Reservation(BootReservationKind.BootStack, 0x2000, 2)
        ]);
        BootReservationTableValidationResult multipleGuards = Validate(
        [
            Reservation(BootReservationKind.BootStackGuard, 0x1000, 1),
            Reservation(BootReservationKind.BootStackGuard, 0x2000, 1),
            Reservation(BootReservationKind.BootStack, 0x3000, 1)
        ]);
        BootReservationTableValidationResult multipleStacks = Validate(
        [
            Reservation(BootReservationKind.BootStackGuard, 0x1000, 1),
            Reservation(BootReservationKind.BootStack, 0x2000, 1),
            Reservation(BootReservationKind.BootStack, 0x3000, 1)
        ]);

        return valid.Succeeded
            && valid.BootStackGuardIndex == 0
            && valid.BootStackIndex == 1
            && valid.BootStackArenaPages == 5
            && valid.BootStackArenaBytes == 5 * BootReservedRange.PageSize
            && guardOnly.Status == BootReservationTableStatus.MissingBootStack
            && guardOnly.InvalidReservationIndex == 0
            && stackOnly.Status == BootReservationTableStatus.MissingBootStackGuard
            && stackOnly.InvalidReservationIndex == 0
            && reversed.Status == BootReservationTableStatus.BootStackOrderInvalid
            && reversed.InvalidReservationIndex == 0
            && reversed.ConflictingReservationIndex == 1
            && gap.Status == BootReservationTableStatus.BootStackArenaGap
            && gap.InvalidReservationIndex == 1
            && gap.ConflictingReservationIndex == 0
            && overlap.Status == BootReservationTableStatus.OverlappingRange
            && overlap.InvalidReservationIndex == 1
            && overlap.ConflictingReservationIndex == 0
            && multipleGuards.Status
                == BootReservationTableStatus.MultipleBootStackGuards
            && multipleGuards.InvalidReservationIndex == 1
            && multipleGuards.ConflictingReservationIndex == 0
            && multipleStacks.Status
                == BootReservationTableStatus.MultipleBootStacks
            && multipleStacks.InvalidReservationIndex == 2
            && multipleStacks.ConflictingReservationIndex == 1;
    }

    private static unsafe bool TestPointerCountAndCapacity()
    {
        if (IntPtr.Size != sizeof(ulong))
        {
            return false;
        }

        if (!HasExpectedMaximumReservationCount(
                BootReservationTableValidator.MaximumReservationCount))
        {
            return false;
        }

        BootReservedRange[] maximum = new BootReservedRange[
            checked((int)BootReservationTableValidator.MaximumReservationCount)];
        for (int index = 0; index < maximum.Length; index++)
        {
            maximum[index] = Reservation(
                BootReservationKind.AllocatorArena,
                checked((ulong)(index + 1) * BootReservedRange.PageSize),
                1);
        }

        BootReservationTableValidationResult maximumResult = Validate(maximum);
        BootReservationTableValidationResult empty =
            BootReservationTableValidator.Validate(null, 0, 0);
        BootReservationTableValidationResult tooMany =
            BootReservationTableValidator.Validate(
                null,
                BootReservationTableValidator.MaximumReservationCount + 1,
                0);
        BootReservationTableValidationResult capacityBeforePointer =
            BootReservationTableValidator.Validate(null, 1, 0);
        BootReservationTableValidationResult missing =
            BootReservationTableValidator.Validate(null, 1, 1);

        byte* scratch = stackalloc byte[Unsafe.SizeOf<BootReservedRange>() + 8];
        BootReservedRange* misaligned = (BootReservedRange*)(scratch + 1);
        BootReservationTableValidationResult misalignedResult =
            BootReservationTableValidator.Validate(misaligned, 1, 1);

        BootReservedRange* wrapping =
            (BootReservedRange*)(~0UL - 7UL);
        BootReservationTableValidationResult wrappingResult =
            BootReservationTableValidator.Validate(wrapping, 1, 1);

        BootReservedRange one = Reservation(
            BootReservationKind.LoadedImage,
            0x1000,
            1);
        BootReservationTableValidationResult declaredBeyondCapacity =
            BootReservationTableValidator.Validate(&one, 2, 1);

        return maximumResult.Succeeded
            && maximumResult.ValidatedReservationCount == 256
            && maximumResult.TotalPages == 256
            && empty.Status
                == BootReservationTableStatus.InvalidReservationCount
            && tooMany.Status
                == BootReservationTableStatus.TooManyReservations
            && capacityBeforePointer.Status
                == BootReservationTableStatus.ReservationCapacityTooSmall
            && capacityBeforePointer.RequiredReservationCapacity == 1
            && capacityBeforePointer.AvailableReservationCapacity == 0
            && missing.Status
                == BootReservationTableStatus.MissingReservationTable
            && misalignedResult.Status
                == BootReservationTableStatus.MisalignedReservationTable
            && wrappingResult.Status
                == BootReservationTableStatus.ReservationTableAddressRangeOverflow
            && wrappingResult.ValidatedReservationCount == 0
            && wrappingResult.InvalidReservationIndex
                == BootReservationTableValidationResult.NoIndex
            && declaredBeyondCapacity.Status
                == BootReservationTableStatus.ReservationCapacityTooSmall
            && declaredBeyondCapacity.ValidatedReservationCount == 0
            && declaredBeyondCapacity.InvalidReservationIndex
                == BootReservationTableValidationResult.NoIndex;
    }

    private static bool HasExpectedMaximumReservationCount(ulong value)
    {
        return value == 256;
    }

    private static unsafe bool TestDeterminismImmutabilityAndAllocation()
    {
        BootReservedRange[] valid =
        [
            Reservation(BootReservationKind.LoadedImage, 0x1000, 1),
            Reservation(BootReservationKind.BootStackGuard, 0x3000, 1),
            Reservation(BootReservationKind.BootStack, 0x4000, 2),
            Reservation(BootReservationKind.AllocatorArena, 0x8000, 4)
        ];
        BootReservedRange[] invalid =
        [
            Reservation(BootReservationKind.AllocatorArena, 0x4000, 1),
            Reservation(BootReservationKind.PageTableArena, 0x1000, 1)
        ];
        byte[] validBefore = MemoryMarshal.AsBytes(
            valid.AsSpan()).ToArray();
        byte[] invalidBefore = MemoryMarshal.AsBytes(
            invalid.AsSpan()).ToArray();

        BootReservationTableValidationResult firstValid = Validate(valid);
        BootReservationTableValidationResult secondValid = Validate(valid);
        BootReservationTableValidationResult firstInvalid = Validate(invalid);
        BootReservationTableValidationResult secondInvalid = Validate(invalid);

        if (!validBefore.AsSpan().SequenceEqual(
                MemoryMarshal.AsBytes(valid.AsSpan()))
            || !invalidBefore.AsSpan().SequenceEqual(
                MemoryMarshal.AsBytes(invalid.AsSpan()))
            || !ResultsEqual(firstValid, secondValid)
            || !ResultsEqual(firstInvalid, secondInvalid))
        {
            return false;
        }

        fixed (BootReservedRange* validPointer = valid)
        fixed (BootReservedRange* invalidPointer = invalid)
        {
            _ = BootReservationTableValidator.Validate(
                validPointer,
                (ulong)valid.Length,
                (ulong)valid.Length);
            _ = BootReservationTableValidator.Validate(
                invalidPointer,
                (ulong)invalid.Length,
                (ulong)invalid.Length);

            long before = GC.GetAllocatedBytesForCurrentThread();
            BootReservationTableValidationResult lastValid = default;
            BootReservationTableValidationResult lastInvalid = default;
            for (int iteration = 0; iteration < 1_000; iteration++)
            {
                lastValid = BootReservationTableValidator.Validate(
                    validPointer,
                    (ulong)valid.Length,
                    (ulong)valid.Length);
                lastInvalid = BootReservationTableValidator.Validate(
                    invalidPointer,
                    (ulong)invalid.Length,
                    (ulong)invalid.Length);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            return after == before
                && lastValid.Succeeded
                && lastValid.TotalPages == firstValid.TotalPages
                && lastInvalid.Status == BootReservationTableStatus.UnsortedRange
                && ResultsEqual(lastInvalid, firstInvalid)
                && validBefore.AsSpan().SequenceEqual(
                    MemoryMarshal.AsBytes(valid.AsSpan()))
                && invalidBefore.AsSpan().SequenceEqual(
                    MemoryMarshal.AsBytes(invalid.AsSpan()));
        }
    }

    private static bool TestNoRuntimeSourceSurface()
    {
        string[] sourcePaths =
        [
            GetRangeSourcePath(),
            GetValidatorSourcePath()
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
            "GetRawValue",
            "operator "
        ];

        foreach (string sourcePath in sourcePaths)
        {
            string source = RemoveLineComments(File.ReadAllText(sourcePath));
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
                    @"\b(string|object|dynamic|delegate|interface|record|var|is|as|Object|Delegate|MulticastDelegate|Attribute|String|Array|Type|Exception|Span|ReadOnlySpan)\b",
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
                if (targetType != nameof(BootReservedRange)
                    && targetType
                        != nameof(BootReservationTableValidationResult)
                    && targetType != "ValidationState")
                {
                    return SourceFirewallFailure(
                        "allocation-target=" + targetType);
                }
            }
        }

        string combinedSource = string.Join(
            Environment.NewLine,
            sourcePaths.Select(path =>
                RemoveLineComments(File.ReadAllText(path))));
        if (Regex.Matches(
                combinedSource,
                @"\bclass\b",
                RegexOptions.CultureInvariant).Count != 1
            || !combinedSource.Contains(
                "public static unsafe class BootReservationTableValidator",
                StringComparison.Ordinal))
        {
            return SourceFirewallFailure("class-shape");
        }

        Type rangeType = typeof(BootReservedRange);
        Type resultType = typeof(BootReservationTableValidationResult);
        Type validatorType = typeof(BootReservationTableValidator);
        Type[] nestedTypes = validatorType.GetNestedTypes(
            BindingFlags.Public | BindingFlags.NonPublic);
        if (nestedTypes.Length != 1
            || nestedTypes[0].Name != "ValidationState"
            || !nestedTypes[0].IsValueType
            || nestedTypes[0].IsPublic)
        {
            return SourceFirewallFailure("nested-type-shape");
        }

        Type stateType = nestedTypes[0];
        if (!HasOnlyValueTypeDeclaredSurface(rangeType))
        {
            return SourceFirewallFailure("range-value-surface");
        }

        if (!HasOnlyValueTypeDeclaredSurface(resultType))
        {
            return SourceFirewallFailure("result-value-surface");
        }

        if (!HasOnlyValueTypeDeclaredSurface(stateType))
        {
            return SourceFirewallFailure("state-value-surface");
        }

        if (!HasExactDeclaredMemberNames(
                rangeType,
                [
                    "PageSize",
                    "MaximumUnsignedLong",
                    "_kind",
                    "_reserved0",
                    "_reserved1",
                    "_reserved2",
                    "_reserved3",
                    "_reserved4",
                    "_reserved5",
                    "_reserved6",
                    "_physicalStart",
                    "_physicalEndExclusive",
                    "_pageCount",
                    "_byteLength"
                ],
                [
                    "get_Kind",
                    "get_PhysicalStart",
                    "get_PhysicalEndExclusive",
                    "get_PageCount",
                    "get_ByteLength",
                    "get_IsValid",
                    "TryCreate",
                    "Equals",
                    "Validate",
                    "ValidateKind"
                ],
                expectedConstructorCount: 1))
        {
            return SourceFirewallFailure("range-member-set");
        }

        if (!HasExactDeclaredMemberNames(
                resultType,
                [
                    "NoIndex",
                    "_status",
                    "_rangeStatus",
                    "_reservationCount",
                    "_validatedReservationCount",
                    "_requiredReservationCapacity",
                    "_availableReservationCapacity",
                    "_invalidReservationIndex",
                    "_conflictingReservationIndex",
                    "_firstLoadedImageIndex",
                    "_bootStackGuardIndex",
                    "_bootStackIndex",
                    "_totalPages",
                    "_totalBytes",
                    "_bootStackArenaPages",
                    "_bootStackArenaBytes"
                ],
                [
                    "get_Status",
                    "get_RangeStatus",
                    "get_Succeeded",
                    "get_ReservationCount",
                    "get_ValidatedReservationCount",
                    "get_RequiredReservationCapacity",
                    "get_AvailableReservationCapacity",
                    "get_InvalidReservationIndex",
                    "get_ConflictingReservationIndex",
                    "get_FirstLoadedImageIndex",
                    "get_BootStackGuardIndex",
                    "get_BootStackIndex",
                    "get_TotalPages",
                    "get_TotalBytes",
                    "get_BootStackArenaPages",
                    "get_BootStackArenaBytes",
                    "Create"
                ],
                expectedConstructorCount: 1))
        {
            return SourceFirewallFailure("result-member-set");
        }

        if (!HasExactDeclaredMemberNames(
                validatorType,
                [
                    "MaximumReservationCount",
                    "MaximumUnsignedLong",
                    "RequiredTableAlignment"
                ],
                [
                    "Validate",
                    "ValidateBootStackArena",
                    "IsDuplicateLoadedImageCoverage",
                    "IsImageCoveredMetadata",
                    "TryAdd",
                    "Succeed",
                    "Fail"
                ],
                expectedConstructorCount: 0,
                requireLiteralFields: true))
        {
            return SourceFirewallFailure("validator-member-set");
        }

        if (!HasExactDeclaredMemberNames(
                stateType,
                [
                    "ReservationCount",
                    "AvailableReservationCapacity",
                    "ValidatedReservationCount",
                    "FirstLoadedImageIndex",
                    "BootStackGuardIndex",
                    "BootStackIndex",
                    "BootStackGuard",
                    "BootStack",
                    "TotalPages",
                    "TotalBytes",
                    "BootStackArenaPages",
                    "BootStackArenaBytes"
                ],
                [],
                expectedConstructorCount: 1))
        {
            return SourceFirewallFailure("state-member-set");
        }

        bool typedAddressShape = rangeType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Count(field => field.FieldType == typeof(PhysicalAddress)) == 2
            && rangeType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .All(property =>
                    property.Name is not "PhysicalStart"
                        and not "PhysicalEndExclusive"
                    || property.PropertyType == typeof(PhysicalAddress));
        return typedAddressShape
            || SourceFirewallFailure("typed-address-shape");
    }

    private static bool SourceFirewallFailure(string reason)
    {
        Console.WriteLine(
            "M01_002_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
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
        int expectedConstructorCount,
        bool requireLiteralFields = false)
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
            && (!requireLiteralFields || fields.All(field => field.IsLiteral))
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

                internal static unsafe class PositiveBootReservationProbe
                {
                    public static BootReservationTableStatus Exercise()
                    {
                        if (PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                                0x1000UL,
                                BootReservedRange.PageSize,
                                out PhysicalAddress imageStart)
                            != AddressValueStatus.Success
                            || PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                                0x3000UL,
                                BootReservedRange.PageSize,
                                out PhysicalAddress guardStart)
                            != AddressValueStatus.Success
                            || PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                                0x4000UL,
                                BootReservedRange.PageSize,
                                out PhysicalAddress stackStart)
                            != AddressValueStatus.Success)
                        {
                            return BootReservationTableStatus.InvalidReservation;
                        }

                        BootReservedRange* table = stackalloc BootReservedRange[3];
                        if (BootReservedRange.TryCreate(
                                BootReservationKind.LoadedImage,
                                imageStart,
                                1UL,
                                out table[0])
                            != BootReservedRangeStatus.Success
                            || BootReservedRange.TryCreate(
                                BootReservationKind.BootStackGuard,
                                guardStart,
                                1UL,
                                out table[1])
                            != BootReservedRangeStatus.Success
                            || BootReservedRange.TryCreate(
                                BootReservationKind.BootStack,
                                stackStart,
                                2UL,
                                out table[2])
                            != BootReservedRangeStatus.Success)
                        {
                            return BootReservationTableStatus.InvalidReservation;
                        }

                        BootReservationTableValidationResult result =
                            BootReservationTableValidator.Validate(
                                table,
                                3UL,
                                3UL);
                        return result.Status;
                    }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            NoStdLibBuildResult result = RunNoStdLibBuild(workspace);
            string outputAssembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.BootReservationCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasZeroAssemblyReferences(outputAssembly))
            {
                Console.WriteLine(
                    "M01_002_NOSTDLIB_COMPILE_DIAGNOSTIC "
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

    private static bool HasConflict(
        BootReservationTableValidationResult result,
        BootReservationTableStatus status,
        ulong invalidIndex,
        ulong conflictingIndex,
        ulong validatedCount,
        ulong prefixPages)
    {
        return !result.Succeeded
            && result.Status == status
            && result.RangeStatus == BootReservedRangeStatus.Success
            && result.InvalidReservationIndex == invalidIndex
            && result.ConflictingReservationIndex == conflictingIndex
            && result.ValidatedReservationCount == validatedCount
            && result.TotalPages == prefixPages
            && result.TotalBytes == prefixPages * BootReservedRange.PageSize;
    }

    private static bool ResultsEqual(
        BootReservationTableValidationResult left,
        BootReservationTableValidationResult right)
    {
        return left.Status == right.Status
            && left.RangeStatus == right.RangeStatus
            && left.Succeeded == right.Succeeded
            && left.ReservationCount == right.ReservationCount
            && left.ValidatedReservationCount == right.ValidatedReservationCount
            && left.RequiredReservationCapacity == right.RequiredReservationCapacity
            && left.AvailableReservationCapacity == right.AvailableReservationCapacity
            && left.InvalidReservationIndex == right.InvalidReservationIndex
            && left.ConflictingReservationIndex == right.ConflictingReservationIndex
            && left.FirstLoadedImageIndex == right.FirstLoadedImageIndex
            && left.BootStackGuardIndex == right.BootStackGuardIndex
            && left.BootStackIndex == right.BootStackIndex
            && left.TotalPages == right.TotalPages
            && left.TotalBytes == right.TotalBytes
            && left.BootStackArenaPages == right.BootStackArenaPages
            && left.BootStackArenaBytes == right.BootStackArenaBytes;
    }

    private static unsafe BootReservationTableValidationResult Validate(
        BootReservedRange[] reservations,
        ulong? capacity = null)
    {
        fixed (BootReservedRange* pointer = reservations)
        {
            return BootReservationTableValidator.Validate(
                pointer,
                (ulong)reservations.Length,
                capacity ?? (ulong)reservations.Length);
        }
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
                "Test aligned physical address construction failed: " + status);
        }

        return address;
    }

    private static PhysicalAddress UnalignedAddress(ulong value)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateFromUefiBoundary(
            value,
            out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Test physical address construction failed: " + status);
        }

        return address;
    }

    private static BootReservedRange Raw(
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

        return Unsafe.As<RawBootReservedRange, BootReservedRange>(ref raw);
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

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-002-" + Guid.NewGuid().ToString("N"));
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
                <AssemblyName>CLROS.M01.BootReservationCompileClosure</AssemblyName>
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
                StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(workspace, "M01.002.NoStdLib.csproj"),
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
        start.ArgumentList.Add("M01.002.NoStdLib.csproj");
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
            timedOut: !exited || !processStopped || !streamsCompleted,
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

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string fullWorkspace = Path.GetFullPath(workspace);
        string expectedPrefix = Path.TrimEndingDirectorySeparator(tempRoot)
            + Path.DirectorySeparatorChar;
        string leafName = Path.GetFileName(fullWorkspace);

        if (!fullWorkspace.StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase)
            || !leafName.StartsWith(
                "clros-m01-002-",
                StringComparison.Ordinal)
            || leafName.Length <= "clros-m01-002-".Length)
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-002 NoStdLib workspace.");
        }

        if (Directory.Exists(fullWorkspace))
        {
            Directory.Delete(fullWorkspace, recursive: true);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct RawBootReservedRange
    {
        [FieldOffset(0)]
        public BootReservationKind Kind;

        [FieldOffset(1)]
        public byte Reserved0;

        [FieldOffset(2)]
        public byte Reserved1;

        [FieldOffset(3)]
        public byte Reserved2;

        [FieldOffset(4)]
        public byte Reserved3;

        [FieldOffset(5)]
        public byte Reserved4;

        [FieldOffset(6)]
        public byte Reserved5;

        [FieldOffset(7)]
        public byte Reserved6;

        [FieldOffset(8)]
        public PhysicalAddress PhysicalStart;

        [FieldOffset(16)]
        public PhysicalAddress PhysicalEndExclusive;

        [FieldOffset(24)]
        public ulong PageCount;

        [FieldOffset(32)]
        public ulong ByteLength;
    }

    private readonly struct NoStdLibBuildResult
    {
        public NoStdLibBuildResult(int exitCode, bool timedOut, string output)
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
