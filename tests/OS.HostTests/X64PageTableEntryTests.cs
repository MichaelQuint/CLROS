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

internal static class X64PageTableEntryTests
{
    private const string IssueTestName = "m01.011";
    private const string ReadyMarker = "CLROS_M01_011_READY";
    private const ulong PatFixture = 0x0006_0501_0007_0406;
    private const ulong Nx = 1UL << 63;
    private const ulong Present = 1UL << 0;
    private const ulong Writable = 1UL << 1;
    private const ulong Pwt = 1UL << 3;
    private const ulong Pcd = 1UL << 4;
    private const ulong Accessed = 1UL << 5;
    private const ulong Dirty = 1UL << 6;
    private const ulong PageSizeOrPtePat = 1UL << 7;
    private const ulong Global = 1UL << 8;
    private const ulong LargePat = 1UL << 12;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run("m01.011 01 type values layouts and defaults are stable", TestTypeContracts, ref failures);
        Run("m01.011 02 protection truth table fails closed", TestProtectionTruthTable, ref failures);
        Run("m01.011 03 unmapped and default inputs are distinct", TestUnmappedAndDefaults, ref failures);
        Run("m01.011 04 four-kibibyte leaf encodings are exact", TestPteEncodings, ref failures);
        Run("m01.011 05 nonleaf table pointers are exact", TestTablePointerEncodings, ref failures);
        Run("m01.011 06 large-page encodings are level exact", TestLargePageEncodings, ref failures);
        Run("m01.011 07 validated cache encodings are level aware", TestCacheEncodings, ref failures);
        Run("m01.011 08 malformed PAT and cache evidence fail", TestPatAndCacheFailures, ref failures);
        Run("m01.011 09 NXE WP and paging preconditions fail closed", TestPreconditionFailures, ref failures);
        Run("m01.011 10 address alignment and width are bounded", TestAddressAndWidthValidation, ref failures);
        Run("m01.011 11 decoder classifies hardware and reserved bits", TestDecoderValidation, ref failures);
        Run("m01.011 12 pointer count capacity and atomicity hold", TestPointerCore, ref failures);
        Run("m01.011 13 source and nostdlib efi firewalls hold", TestSourceAndNoStdLibBoundary, ref failures);
        Run("m01.011 14 evaluated efi and marker firewalls hold", TestEfiAndMarkerBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-011 x64 page-table entry tests failed: " + failures);
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

    private static bool TestTypeContracts()
    {
        (string Name, byte Value)[] protectionValues =
        [
            ("None", 0),
            ("Read", 1),
            ("Write", 2),
            ("Execute", 4)
        ];
        (string Name, byte Value)[] cacheValues =
        [
            ("Uninitialized", 0),
            ("WriteBack", 1),
            ("Uncached", 2),
            ("WriteCombining", 3)
        ];
        (string Name, byte Value)[] levelValues =
        [
            ("Uninitialized", 0),
            ("Pte", 1),
            ("Pde", 2),
            ("Pdpte", 3),
            ("Pml4e", 4)
        ];
        (string Name, byte Value)[] kindValues =
        [
            ("Uninitialized", 0),
            ("Unmapped", 1),
            ("TablePointer", 2),
            ("Leaf", 3)
        ];
        (string Name, byte Value)[] hardwareStatePolicyValues =
        [
            ("Uninitialized", 0),
            ("Clear", 1),
            ("Preserve", 2)
        ];
        (string Name, byte Value)[] statusValues =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("RequestNotInitialized", 2),
            ("InvalidLevel", 3),
            ("InvalidEntryKind", 4),
            ("InvalidUnmappedEntry", 5),
            ("InvalidTablePointer", 6),
            ("InvalidProtection", 7),
            ("WritableExecutableRejected", 8),
            ("InvalidCachePolicy", 9),
            ("CachePolicyNotSupported", 10),
            ("PatMappingNotInitialized", 11),
            ("InvalidPatEntry", 12),
            ("PatIndexOutOfRange", 13),
            ("PatMemoryTypeMismatch", 14),
            ("PreconditionsNotInitialized", 15),
            ("NxeNotEnabled", 16),
            ("WriteProtectNotEnabled", 17),
            ("FiveLevelPagingNotSupported", 18),
            ("UnsupportedPhysicalAddressWidth", 19),
            ("InvalidPhysicalAddress", 20),
            ("MisalignedPhysicalAddress", 21),
            ("PhysicalAddressOutsideWidth", 22),
            ("NonCanonicalUnmappedEntry", 23),
            ("UnsupportedEntryBits", 24),
            ("InvalidLargePage", 25),
            ("DirtyBitOnTablePointer", 26),
            ("NonCanonicalTablePointer", 27),
            ("InvalidBatchCount", 28),
            ("TooManyEntries", 29),
            ("SourceCapacityTooSmall", 30),
            ("DestinationCapacityTooSmall", 31),
            ("CapacityExceedsLimit", 32),
            ("MissingSource", 33),
            ("MissingDestination", 34),
            ("MisalignedSource", 35),
            ("MisalignedDestination", 36),
            ("SourceSpanOverflow", 37),
            ("DestinationSpanOverflow", 38),
            ("SourceDestinationOverlap", 39),
            ("ContractLayoutMismatch", 40),
            ("InvalidPatMappingState", 41),
            ("InvalidPreconditionState", 42),
            ("OneGiBPagesNotSupported", 43),
            ("InvalidRequestState", 44),
            ("InvalidHardwareStatePolicy", 45),
            ("ExistingEntryNotLeaf", 46)
        ];

        X64PageTableEntryResult result = default;
        X64PageTableEntryBatchResult batch = default;
        X64PatMappingToken pat = default;
        X64PageTableEncodingPreconditions preconditions = default;
        X64PageTableEntryRequest request = default;

        return HasExactByteEnum<PageProtection>(protectionValues)
            && HasExactByteEnum<PageCachePolicy>(cacheValues)
            && HasExactByteEnum<X64PageTableLevel>(levelValues)
            && HasExactByteEnum<X64PageTableEntryKind>(kindValues)
            && HasExactByteEnum<X64HardwareStatePolicy>(
                hardwareStatePolicyValues)
            && HasExactByteEnum<X64PageTableEntryStatus>(statusValues)
            && !typeof(PageProtection).IsDefined(
                typeof(FlagsAttribute),
                inherit: false)
            && IsReadOnlySequentialUnmanaged<X64PatMappingToken>()
            && IsReadOnlySequentialUnmanaged<
                X64PageTableEncodingPreconditions>()
            && IsReadOnlySequentialUnmanaged<X64PageTableEntryRequest>()
            && IsReadOnlySequentialUnmanaged<X64PageTableEntry>()
            && IsReadOnlySequentialUnmanaged<X64PageTableEntryResult>()
            && IsReadOnlySequentialUnmanaged<
                X64PageTableEntryBatchResult>()
            && (ulong)Unsafe.SizeOf<X64PatMappingToken>()
                == X64PatMappingToken.StructureSize
            && (ulong)Unsafe.SizeOf<X64PageTableEncodingPreconditions>()
                == X64PageTableEncodingPreconditions.StructureSize
            && (ulong)Unsafe.SizeOf<X64PageTableEntryRequest>()
                == X64PageTableEntryRequest.StructureSize
            && (ulong)Unsafe.SizeOf<X64PageTableEntry>()
                == X64PageTableEntry.StructureSize
            && (ulong)Unsafe.SizeOf<X64PageTableEntryResult>()
                == X64PageTableEntryResult.StructureSize
            && (ulong)Unsafe.SizeOf<X64PageTableEntryBatchResult>()
                == X64PageTableEntryBatchResult.StructureSize
            && HasExactFieldOffsets()
            && !result.IsInitialized
            && !result.Succeeded
            && result.Status == X64PageTableEntryStatus.Uninitialized
            && result.Entry.RawValue == 0
            && !batch.IsInitialized
            && !batch.Succeeded
            && batch.Status == X64PageTableEntryStatus.Uninitialized
            && batch.InvalidRequestIndex == 0
            && !pat.IsInitialized
            && pat.Validate()
                == X64PageTableEntryStatus.PatMappingNotInitialized
            && !preconditions.IsInitialized
            && !preconditions.OneGiBPagesSupported
            && preconditions.Validate()
                == X64PageTableEntryStatus.PreconditionsNotInitialized
            && !request.IsInitialized
            && X64PageTableEntry.MaximumBatchEntryCount == 512
            && X64PageTableEntry.RequiredPointerAlignment == 8
            && X64PageTableEntry.NoIndex == ~0UL
            && X64PageTableEntry.PageSize4KiB == 4096
            && X64PageTableEntry.PageSize2MiB == 2UL * 1024 * 1024
            && X64PageTableEntry.PageSize1GiB
                == 1024UL * 1024 * 1024;
    }

    private static bool TestProtectionTruthTable()
    {
        X64PageTableEncodingPreconditions context = ValidContext();
        PhysicalAddress address = Physical(0x1234_5000);

        for (int raw = 0; raw <= byte.MaxValue; raw++)
        {
            PageProtection protection = (PageProtection)(byte)raw;
            X64PageTableEntryResult result = X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pte,
                    X64PageTableEntryKind.Leaf,
                    address,
                    protection,
                    PageCachePolicy.WriteBack),
                context);
            bool expectedSuccess = raw == 1 || raw == 3 || raw == 5;
            if (result.Succeeded != expectedSuccess)
            {
                return false;
            }

            if (!expectedSuccess)
            {
                byte known = (byte)(
                    (byte)PageProtection.Read
                    | (byte)PageProtection.Write
                    | (byte)PageProtection.Execute);
                bool unknown = ((byte)raw & (byte)~known) != 0;
                bool writableExecutable =
                    ((byte)raw & (byte)PageProtection.Write) != 0
                    && ((byte)raw & (byte)PageProtection.Execute) != 0;
                X64PageTableEntryStatus expected = !unknown
                    && writableExecutable
                    ? X64PageTableEntryStatus
                        .WritableExecutableRejected
                    : X64PageTableEntryStatus.InvalidProtection;
                if (result.Status != expected ||
                    result.Entry.RawValue != 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TestUnmappedAndDefaults()
    {
        X64PageTableEntryResult defaultRequest =
            X64PageTableEntry.TryEncode(default, default);
        X64PageTableEntryRequest unmapped = new(
            X64PageTableLevel.Pte,
            X64PageTableEntryKind.Unmapped,
            default,
            PageProtection.None,
            PageCachePolicy.Uninitialized);
        X64PageTableEntryResult encoded =
            X64PageTableEntry.TryEncode(unmapped, default);
        X64PageTableEntryResult decoded =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                0,
                default);
        X64PageTableEntryResult nonCanonical =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                Nx,
                ValidContext());
        X64PageTableEntryResult invalidLevel =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Uninitialized,
                0,
                default);
        X64PageTableEntryResult invalidEncodeLevel =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Uninitialized,
                    X64PageTableEntryKind.Unmapped,
                    default,
                    PageProtection.None,
                    PageCachePolicy.Uninitialized),
                default);
        X64PageTableEntryResult invalidKind =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pte,
                    (X64PageTableEntryKind)0xff,
                    default,
                    PageProtection.None,
                    PageCachePolicy.Uninitialized),
                default);
        X64PageTableEntryResult invalidAddress =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pte,
                    X64PageTableEntryKind.Unmapped,
                    Physical(0x1000),
                    PageProtection.None,
                    PageCachePolicy.Uninitialized),
                default);
        X64PageTableEntryResult invalidProtection =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pte,
                    X64PageTableEntryKind.Unmapped,
                    default,
                    PageProtection.Read,
                    PageCachePolicy.Uninitialized),
                default);
        X64PageTableEntryResult invalidCache =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pte,
                    X64PageTableEntryKind.Unmapped,
                    default,
                    PageProtection.None,
                    PageCachePolicy.WriteBack),
                default);
        X64PageTableEntryResult missingPresentAddress =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pte,
                    X64PageTableEntryKind.Leaf,
                    default,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                ValidContext());
        X64PageTableEntryRequest malformedInitialization =
            OverwriteByte(unmapped, 8, 2);
        X64PageTableEntryRequest malformedReserved =
            OverwriteByte(unmapped, 13, 1);

        return defaultRequest.Status
                == X64PageTableEntryStatus.RequestNotInitialized
            && !defaultRequest.Succeeded
            && encoded.Succeeded
            && encoded.Entry.RawValue == 0
            && !encoded.IsPresent
            && encoded.Kind == X64PageTableEntryKind.Unmapped
            && decoded.Succeeded
            && decoded.Entry.RawValue == 0
            && decoded.Level == X64PageTableLevel.Pte
            && decoded.Kind == X64PageTableEntryKind.Unmapped
            && decoded.Protection == PageProtection.None
            && decoded.CachePolicy == PageCachePolicy.Uninitialized
            && !decoded.PhysicalAddress.IsValid
            && nonCanonical.Status
                == X64PageTableEntryStatus.NonCanonicalUnmappedEntry
            && invalidLevel.Status
                == X64PageTableEntryStatus.InvalidLevel
            && invalidEncodeLevel.Status
                == X64PageTableEntryStatus.InvalidLevel
            && invalidKind.Status
                == X64PageTableEntryStatus.InvalidEntryKind
            && invalidAddress.Status
                == X64PageTableEntryStatus.InvalidUnmappedEntry
            && invalidProtection.Status
                == X64PageTableEntryStatus.InvalidUnmappedEntry
            && invalidCache.Status
                == X64PageTableEntryStatus.InvalidUnmappedEntry
            && missingPresentAddress.Status
                == X64PageTableEntryStatus.InvalidPhysicalAddress
            && malformedInitialization.Validate()
                == X64PageTableEntryStatus.InvalidRequestState
            && X64PageTableEntry.TryEncode(
                    malformedInitialization,
                    default).Status
                == X64PageTableEntryStatus.InvalidRequestState
            && malformedReserved.Validate()
                == X64PageTableEntryStatus.InvalidRequestState
            && X64PageTableEntry.TryEncode(
                    malformedReserved,
                    default).Status
                == X64PageTableEntryStatus.InvalidRequestState;
    }

    private static bool TestPteEncodings()
    {
        const ulong addressValue = 0x1234_5000;
        PhysicalAddress address = Physical(addressValue);
        X64PageTableEncodingPreconditions context = ValidContext();
        PageProtection readWrite = Combine(
            PageProtection.Read,
            PageProtection.Write);
        PageProtection readExecute = Combine(
            PageProtection.Read,
            PageProtection.Execute);
        (PageProtection Protection, ulong Expected)[] cases =
        [
            (PageProtection.Read, addressValue | Present | Nx),
            (readWrite, addressValue | Present | Writable | Nx),
            (readExecute, addressValue | Present)
        ];

        foreach ((PageProtection protection, ulong expected) in cases)
        {
            X64PageTableEntryResult encoded =
                X64PageTableEntry.TryEncode(
                    Leaf(
                        X64PageTableLevel.Pte,
                        address,
                        protection,
                        PageCachePolicy.WriteBack),
                    context);
            if (!encoded.Succeeded
                || encoded.Entry.RawValue != expected
                || encoded.Level != X64PageTableLevel.Pte
                || encoded.Kind != X64PageTableEntryKind.Leaf
                || encoded.Protection != protection
                || encoded.CachePolicy != PageCachePolicy.WriteBack
                || encoded.PageSizeBytes
                    != X64PageTableEntry.PageSize4KiB
                || encoded.PatIndex != 0
                || encoded.Accessed
                || encoded.Dirty
                || !RawEquals(encoded.PhysicalAddress, addressValue))
            {
                return false;
            }

            X64PageTableEntryResult decoded =
                X64PageTableEntry.TryDecode(
                    X64PageTableLevel.Pte,
                    encoded.Entry.RawValue,
                    context);
            if (!decoded.Succeeded
                || decoded.Entry.RawValue != expected
                || decoded.Protection != protection
                || decoded.CachePolicy != PageCachePolicy.WriteBack
                || !RawEquals(decoded.PhysicalAddress, addressValue))
            {
                return false;
            }

            X64PageTableEntryResult reencoded =
                X64PageTableEntry.TryEncode(
                    Leaf(
                        decoded.Level,
                        decoded.PhysicalAddress,
                        decoded.Protection,
                        decoded.CachePolicy),
                    context);
            if (!reencoded.Succeeded
                || reencoded.Entry.RawValue != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestTablePointerEncodings()
    {
        const ulong addressValue = 0x2345_6000;
        PhysicalAddress address = Physical(addressValue);
        X64PageTableEncodingPreconditions context = ValidContext();
        X64PageTableLevel[] levels =
        [
            X64PageTableLevel.Pde,
            X64PageTableLevel.Pdpte,
            X64PageTableLevel.Pml4e
        ];

        foreach (X64PageTableLevel level in levels)
        {
            X64PageTableEntryResult encoded =
                X64PageTableEntry.TryEncode(
                    TablePointer(level, address),
                    context);
            if (!encoded.Succeeded
                || encoded.Entry.RawValue
                    != (addressValue | Present | Writable)
                || encoded.Kind
                    != X64PageTableEntryKind.TablePointer
                || encoded.Protection != PageProtection.None
                || encoded.CachePolicy != PageCachePolicy.WriteBack
                || encoded.PageSizeBytes != 0
                || encoded.PatIndex != 0
                || !RawEquals(encoded.PhysicalAddress, addressValue))
            {
                return false;
            }

            X64PageTableEntryResult decoded =
                X64PageTableEntry.TryDecode(
                    level,
                    encoded.Entry.RawValue | Accessed,
                    context);
            if (!decoded.Succeeded
                || !decoded.Accessed
                || decoded.Dirty
                || decoded.Kind
                    != X64PageTableEntryKind.TablePointer
                || !RawEquals(decoded.PhysicalAddress, addressValue))
            {
                return false;
            }
        }

        X64PageTableEntryResult pteTable =
            X64PageTableEntry.TryEncode(
                TablePointer(X64PageTableLevel.Pte, address),
                context);
        X64PageTableEntryResult protectedTable =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pde,
                    X64PageTableEntryKind.TablePointer,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                context);
        X64PageTableEntryResult cachedTable =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pde,
                    X64PageTableEntryKind.TablePointer,
                    address,
                    PageProtection.None,
                    PageCachePolicy.Uncached),
                context);

        return pteTable.Status
                == X64PageTableEntryStatus.InvalidTablePointer
            && protectedTable.Status
                == X64PageTableEntryStatus.InvalidTablePointer
            && cachedTable.Status
                == X64PageTableEntryStatus.InvalidTablePointer;
    }

    private static bool TestLargePageEncodings()
    {
        PageProtection read = PageProtection.Read;
        X64PageTableEncodingPreconditions context = ValidContext();
        PhysicalAddress twoMiB = Physical(0x0040_0000);
        PhysicalAddress oneGiB = Physical(0x8000_0000);
        X64PageTableEntryResult pde =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pde,
                    twoMiB,
                    read,
                    PageCachePolicy.WriteBack),
                context);
        X64PageTableEntryResult pdpte =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pdpte,
                    oneGiB,
                    read,
                    PageCachePolicy.WriteBack),
                context);
        X64PageTableEntryResult pml4Leaf =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pml4e,
                    Physical(0x4000_0000),
                    read,
                    PageCachePolicy.WriteBack),
                context);
        X64PageTableEntryResult pml4Ps =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pml4e,
                0x4000_0000 | Present | Writable
                    | PageSizeOrPtePat,
                context);
        X64PageTableEncodingPreconditions noOneGiBContext =
            Context(
                48,
                nxeEnabled: true,
                writeProtectEnabled: true,
                fiveLevelPagingEnabled: false,
                ValidPat(),
                oneGiBPagesSupported: false);
        X64PageTableEntryResult unsupportedOneGiBEncode =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pdpte,
                    oneGiB,
                    read,
                    PageCachePolicy.WriteBack),
                noOneGiBContext);
        X64PageTableEntryResult unsupportedOneGiBDecode =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pdpte,
                0x8000_0000 | Present | PageSizeOrPtePat | Nx,
                noOneGiBContext);
        X64PageTableEntryResult supportedTwoMiBWithoutOneGiB =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pde,
                    twoMiB,
                    read,
                    PageCachePolicy.WriteBack),
                noOneGiBContext);

        return pde.Succeeded
            && pde.IsLargePage
            && pde.PageSizeBytes == X64PageTableEntry.PageSize2MiB
            && pde.Entry.RawValue
                == (0x0040_0000 | Present
                    | PageSizeOrPtePat | Nx)
            && pdpte.Succeeded
            && pdpte.IsLargePage
            && pdpte.PageSizeBytes == X64PageTableEntry.PageSize1GiB
            && pdpte.Entry.RawValue
                == (0x8000_0000 | Present
                    | PageSizeOrPtePat | Nx)
            && X64PageTableEntry.TryDecode(
                    X64PageTableLevel.Pde,
                    pde.Entry.RawValue,
                    context).Succeeded
            && X64PageTableEntry.TryDecode(
                    X64PageTableLevel.Pdpte,
                    pdpte.Entry.RawValue,
                    context).Succeeded
            && pml4Leaf.Status
                == X64PageTableEntryStatus.InvalidLargePage
            && pml4Ps.Status
                == X64PageTableEntryStatus.InvalidLargePage
            && unsupportedOneGiBEncode.Status
                == X64PageTableEntryStatus.OneGiBPagesNotSupported
            && unsupportedOneGiBDecode.Status
                == X64PageTableEntryStatus.OneGiBPagesNotSupported
            && supportedTwoMiBWithoutOneGiB.Succeeded;
    }

    private static bool TestCacheEncodings()
    {
        X64PageTableEncodingPreconditions context = ValidContext();
        PhysicalAddress pteAddress = Physical(0x3456_7000);
        PhysicalAddress pdeAddress = Physical(0x0060_0000);
        PhysicalAddress pdpteAddress = Physical(0xC000_0000);

        X64PageTableEntryResult wb = X64PageTableEntry.TryEncode(
            Leaf(
                X64PageTableLevel.Pte,
                pteAddress,
                PageProtection.Read,
                PageCachePolicy.WriteBack),
            context);
        X64PageTableEntryResult uc = X64PageTableEntry.TryEncode(
            Leaf(
                X64PageTableLevel.Pte,
                pteAddress,
                PageProtection.Read,
                PageCachePolicy.Uncached),
            context);
        X64PageTableEntryResult wc4KiB = X64PageTableEntry.TryEncode(
            Leaf(
                X64PageTableLevel.Pte,
                pteAddress,
                PageProtection.Read,
                PageCachePolicy.WriteCombining),
            context);
        X64PageTableEntryResult wc2MiB = X64PageTableEntry.TryEncode(
            Leaf(
                X64PageTableLevel.Pde,
                pdeAddress,
                PageProtection.Read,
                PageCachePolicy.WriteCombining),
            context);
        X64PageTableEntryResult wc1GiB = X64PageTableEntry.TryEncode(
            Leaf(
                X64PageTableLevel.Pdpte,
                pdpteAddress,
                PageProtection.Read,
                PageCachePolicy.WriteCombining),
            context);

        if (!wb.Succeeded
            || wb.PatIndex != 0
            || (wb.Entry.RawValue & (Pwt | Pcd
                | PageSizeOrPtePat)) != 0
            || !uc.Succeeded
            || uc.PatIndex != 3
            || (uc.Entry.RawValue & (Pwt | Pcd))
                != (Pwt | Pcd)
            || (uc.Entry.RawValue & PageSizeOrPtePat) != 0
            || !wc4KiB.Succeeded
            || wc4KiB.PatIndex != 4
            || (wc4KiB.Entry.RawValue & PageSizeOrPtePat) == 0
            || !wc2MiB.Succeeded
            || wc2MiB.PatIndex != 4
            || (wc2MiB.Entry.RawValue & PageSizeOrPtePat) == 0
            || (wc2MiB.Entry.RawValue & LargePat) == 0
            || !wc1GiB.Succeeded
            || wc1GiB.PatIndex != 4
            || (wc1GiB.Entry.RawValue & PageSizeOrPtePat) == 0
            || (wc1GiB.Entry.RawValue & LargePat) == 0)
        {
            return false;
        }

        X64PageTableEntryResult[] encoded =
        [
            wb, uc, wc4KiB, wc2MiB, wc1GiB
        ];
        foreach (X64PageTableEntryResult item in encoded)
        {
            X64PageTableEntryResult decoded =
                X64PageTableEntry.TryDecode(
                    item.Level,
                    item.Entry.RawValue,
                    context);
            if (!decoded.Succeeded
                || decoded.CachePolicy != item.CachePolicy
                || decoded.PatIndex != item.PatIndex)
            {
                return false;
            }

            X64PageTableEntryResult reencoded =
                X64PageTableEntry.TryEncode(
                    Leaf(
                        decoded.Level,
                        decoded.PhysicalAddress,
                        decoded.Protection,
                        decoded.CachePolicy),
                    context);
            if (!reencoded.Succeeded
                || reencoded.Entry.RawValue != item.Entry.RawValue)
            {
                return false;
            }
        }

        X64PatMappingToken.TryCreate(
            PatFixture,
            writeBackIndex: 6,
            uncachedIndex: 3,
            writeCombiningIndex: 4,
            out X64PatMappingToken highWb);
        X64PageTableEncodingPreconditions highWbContext =
            X64PageTableEncodingPreconditions.CreateObserved(
                48,
                nxeEnabled: true,
                writeProtectEnabled: true,
                fiveLevelPagingEnabled: false,
                oneGiBPagesSupported: true,
                highWb);
        X64PageTableEntryResult highWbLeaf =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    pteAddress,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                highWbContext);

        return highWbLeaf.Succeeded
            && highWbLeaf.PatIndex == 6
            && (highWbLeaf.Entry.RawValue
                & (Pwt | Pcd | PageSizeOrPtePat))
                == (Pcd | PageSizeOrPtePat);
    }

    private static bool TestPatAndCacheFailures()
    {
        X64PageTableEntryStatus validStatus =
            X64PatMappingToken.TryCreate(
                PatFixture,
                writeBackIndex: 0,
                uncachedIndex: 3,
                writeCombiningIndex: 4,
                out X64PatMappingToken valid);
        X64PageTableEntryStatus reservedBits =
            X64PatMappingToken.TryCreate(
                PatFixture | 0x08,
                writeBackIndex: 0,
                uncachedIndex: 3,
                writeCombiningIndex: 4,
                out X64PatMappingToken reservedToken);
        ulong reservedTypePat =
            (PatFixture & ~(0xffUL << 32)) | (2UL << 32);
        X64PageTableEntryStatus reservedType =
            X64PatMappingToken.TryCreate(
                reservedTypePat,
                writeBackIndex: 0,
                uncachedIndex: 3,
                writeCombiningIndex: 4,
                out _);
        X64PageTableEntryStatus badWbIndex =
            X64PatMappingToken.TryCreate(
                PatFixture,
                writeBackIndex: 8,
                uncachedIndex: 3,
                writeCombiningIndex: 4,
                out _);
        X64PageTableEntryStatus wrongWbType =
            X64PatMappingToken.TryCreate(
                PatFixture,
                writeBackIndex: 1,
                uncachedIndex: 3,
                writeCombiningIndex: 4,
                out _);
        X64PageTableEntryStatus wrongUcType =
            X64PatMappingToken.TryCreate(
                PatFixture,
                writeBackIndex: 0,
                uncachedIndex: 1,
                writeCombiningIndex: 4,
                out _);
        X64PageTableEntryStatus wrongWcType =
            X64PatMappingToken.TryCreate(
                PatFixture,
                writeBackIndex: 0,
                uncachedIndex: 3,
                writeCombiningIndex: 1,
                out _);
        X64PageTableEntryStatus optionalStatus =
            X64PatMappingToken.TryCreate(
                PatFixture,
                writeBackIndex: 0,
                uncachedIndex: X64PatMappingToken.UnsupportedIndex,
                writeCombiningIndex:
                    X64PatMappingToken.UnsupportedIndex,
                out X64PatMappingToken wbOnly);
        PhysicalAddress address = Physical(0x4567_8000);
        X64PageTableEncodingPreconditions wbOnlyContext =
            Context(48, true, true, false, wbOnly);
        X64PageTableEntryResult ucUnsupported =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.Uncached),
                wbOnlyContext);
        X64PageTableEntryResult wcUnsupported =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteCombining),
                wbOnlyContext);
        X64PageTableEntryResult defaultPat =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                Context(48, true, true, false, default));
        X64PageTableEntryResult uninitializedCache =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.Uninitialized),
                ValidContext());
        X64PageTableEntryResult unknownCache =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    (PageCachePolicy)0xff),
                ValidContext());
        X64PageTableEntryResult tableWithHighWb =
            X64PageTableEntry.TryEncode(
                TablePointer(
                    X64PageTableLevel.Pde,
                    Physical(0x5678_9000)),
                Context(48, true, true, false, HighWriteBackPat()));
        ulong patZeroNotWriteBack =
            (PatFixture & ~0xffUL) | 4UL;
        X64PageTableEntryStatus patZeroNotWriteBackStatus =
            X64PatMappingToken.TryCreate(
                patZeroNotWriteBack,
                writeBackIndex: 6,
                uncachedIndex: 3,
                writeCombiningIndex: 4,
                out X64PatMappingToken highWbWithoutWalkWb);
        X64PageTableEncodingPreconditions
            highWbWithoutWalkWbContext = Context(
                48,
                true,
                true,
                false,
                highWbWithoutWalkWb);
        X64PageTableEntryResult highWbLeafWithoutWalkWb =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                highWbWithoutWalkWbContext);
        X64PageTableEntryResult tableWithoutWalkWb =
            X64PageTableEntry.TryEncode(
                TablePointer(
                    X64PageTableLevel.Pde,
                    Physical(0x5678_9000)),
                highWbWithoutWalkWbContext);
        X64PageTableEntryResult decodeTableWithoutWalkWb =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pde,
                0x5678_9000 | Present | Writable,
                highWbWithoutWalkWbContext);
        X64PatMappingToken malformedInitialization =
            OverwriteByte(valid, 8, 2);
        X64PatMappingToken malformedIndex =
            OverwriteByte(valid, 9, 8);
        X64PatMappingToken mismatchedIndex =
            OverwriteByte(valid, 9, 3);
        X64PatMappingToken malformedRaw =
            OverwriteByte(valid, 0, 0x0e);
        X64PatMappingToken malformedReserved =
            OverwriteByte(valid, 12, 1);
        X64PageTableEntryResult malformedInitializationUse =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                Context(
                    48,
                    true,
                    true,
                    false,
                    malformedInitialization));
        X64PageTableEntryResult malformedIndexUse =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                Context(48, true, true, false, malformedIndex));
        X64PageTableEntryResult mismatchedIndexUse =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                Context(48, true, true, false, mismatchedIndex));
        X64PageTableEntryResult malformedRawUse =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                Context(48, true, true, false, malformedRaw));
        X64PageTableEntryResult malformedReservedUse =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    address,
                    PageProtection.Read,
                    PageCachePolicy.WriteBack),
                Context(48, true, true, false, malformedReserved));

        return validStatus == X64PageTableEntryStatus.Success
            && valid.IsInitialized
            && valid.RawIa32Pat == PatFixture
            && valid.WriteBackIndex == 0
            && valid.UncachedIndex == 3
            && valid.WriteCombiningIndex == 4
            && reservedBits
                == X64PageTableEntryStatus.InvalidPatEntry
            && !reservedToken.IsInitialized
            && reservedType
                == X64PageTableEntryStatus.InvalidPatEntry
            && badWbIndex
                == X64PageTableEntryStatus.PatIndexOutOfRange
            && wrongWbType
                == X64PageTableEntryStatus.PatMemoryTypeMismatch
            && wrongUcType
                == X64PageTableEntryStatus.PatMemoryTypeMismatch
            && wrongWcType
                == X64PageTableEntryStatus.PatMemoryTypeMismatch
            && optionalStatus == X64PageTableEntryStatus.Success
            && wbOnly.IsInitialized
            && ucUnsupported.Status
                == X64PageTableEntryStatus.CachePolicyNotSupported
            && wcUnsupported.Status
                == X64PageTableEntryStatus.CachePolicyNotSupported
            && defaultPat.Status
                == X64PageTableEntryStatus.PatMappingNotInitialized
            && uninitializedCache.Status
                == X64PageTableEntryStatus.InvalidCachePolicy
            && unknownCache.Status
                == X64PageTableEntryStatus.InvalidCachePolicy
            && tableWithHighWb.Succeeded
            && tableWithHighWb.PatIndex == 0
            && tableWithHighWb.Entry.RawValue
                == (0x5678_9000 | Present | Writable)
            && patZeroNotWriteBackStatus
                == X64PageTableEntryStatus.Success
            && highWbLeafWithoutWalkWb.Succeeded
            && highWbLeafWithoutWalkWb.PatIndex == 6
            && (highWbLeafWithoutWalkWb.Entry.RawValue
                & (Pwt | Pcd | PageSizeOrPtePat))
                == (Pcd | PageSizeOrPtePat)
            && tableWithoutWalkWb.Status
                == X64PageTableEntryStatus.PatMemoryTypeMismatch
            && decodeTableWithoutWalkWb.Status
                == X64PageTableEntryStatus.PatMemoryTypeMismatch
            && malformedInitialization.Validate()
                == X64PageTableEntryStatus.InvalidPatMappingState
            && malformedInitializationUse.Status
                == X64PageTableEntryStatus.InvalidPatMappingState
            && malformedIndex.Validate()
                == X64PageTableEntryStatus.PatIndexOutOfRange
            && malformedIndexUse.Status
                == X64PageTableEntryStatus.PatIndexOutOfRange
            && mismatchedIndex.Validate()
                == X64PageTableEntryStatus.PatMemoryTypeMismatch
            && mismatchedIndexUse.Status
                == X64PageTableEntryStatus.PatMemoryTypeMismatch
            && malformedRaw.Validate()
                == X64PageTableEntryStatus.InvalidPatEntry
            && malformedRawUse.Status
                == X64PageTableEntryStatus.InvalidPatEntry
            && malformedReserved.Validate()
                == X64PageTableEntryStatus.InvalidPatMappingState
            && malformedReservedUse.Status
                == X64PageTableEntryStatus.InvalidPatMappingState;
    }

    private static bool TestPreconditionFailures()
    {
        PhysicalAddress address = Physical(0x5678_9000);
        X64PageTableEntryRequest request = Leaf(
            X64PageTableLevel.Pte,
            address,
            PageProtection.Read,
            PageCachePolicy.WriteBack);
        X64PatMappingToken pat = ValidPat();
        X64PageTableEntryResult missing =
            X64PageTableEntry.TryEncode(request, default);
        X64PageTableEntryResult noNxe =
            X64PageTableEntry.TryEncode(
                request,
                Context(48, false, true, false, pat));
        X64PageTableEntryResult noWp =
            X64PageTableEntry.TryEncode(
                request,
                Context(48, true, false, false, pat));
        X64PageTableEntryResult neither =
            X64PageTableEntry.TryEncode(
                request,
                Context(48, false, false, false, pat));
        X64PageTableEntryResult la57 =
            X64PageTableEntry.TryEncode(
                request,
                Context(48, true, true, true, pat));
        X64PageTableEntryResult decodeNoNxe =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                0x5678_9000 | Present | Nx,
                Context(48, false, true, false, pat));
        X64PageTableEntryResult decodeNoWp =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                0x5678_9000 | Present | Nx,
                Context(48, true, false, false, pat));
        X64PageTableEncodingPreconditions malformedInitialization =
            OverwriteByte(ValidContext(), 16, 2);
        X64PageTableEncodingPreconditions malformedNxe =
            OverwriteByte(ValidContext(), 18, 2);
        X64PageTableEncodingPreconditions malformedWp =
            OverwriteByte(ValidContext(), 19, 2);
        X64PageTableEncodingPreconditions malformedLa57 =
            OverwriteByte(ValidContext(), 20, 2);
        X64PageTableEncodingPreconditions malformedOneGiB =
            OverwriteByte(ValidContext(), 21, 2);
        X64PageTableEncodingPreconditions malformedReserved0 =
            OverwriteByte(ValidContext(), 22, 1);
        X64PageTableEncodingPreconditions malformedReserved1 =
            OverwriteByte(ValidContext(), 23, 1);

        return missing.Status
                == X64PageTableEntryStatus.PreconditionsNotInitialized
            && noNxe.Status == X64PageTableEntryStatus.NxeNotEnabled
            && noWp.Status
                == X64PageTableEntryStatus.WriteProtectNotEnabled
            && neither.Status
                == X64PageTableEntryStatus.NxeNotEnabled
            && la57.Status
                == X64PageTableEntryStatus.FiveLevelPagingNotSupported
            && decodeNoNxe.Status
                == X64PageTableEntryStatus.NxeNotEnabled
            && decodeNoWp.Status
                == X64PageTableEntryStatus.WriteProtectNotEnabled
            && X64PageTableEntry.TryEncode(
                    request,
                    malformedInitialization).Status
                == X64PageTableEntryStatus.InvalidPreconditionState
            && X64PageTableEntry.TryEncode(
                    request,
                    malformedNxe).Status
                == X64PageTableEntryStatus.InvalidPreconditionState
            && X64PageTableEntry.TryEncode(
                    request,
                    malformedWp).Status
                == X64PageTableEntryStatus.InvalidPreconditionState
            && X64PageTableEntry.TryEncode(
                    request,
                    malformedLa57).Status
                == X64PageTableEntryStatus.InvalidPreconditionState
            && X64PageTableEntry.TryEncode(
                    request,
                    malformedOneGiB).Status
                == X64PageTableEntryStatus.InvalidPreconditionState
            && X64PageTableEntry.TryEncode(
                    request,
                    malformedReserved0).Status
                == X64PageTableEntryStatus.InvalidPreconditionState
            && X64PageTableEntry.TryEncode(
                    request,
                    malformedReserved1).Status
                == X64PageTableEntryStatus.InvalidPreconditionState
            && X64PageTableEntry.TryEncode(
                    new X64PageTableEntryRequest(
                        X64PageTableLevel.Pte,
                        X64PageTableEntryKind.Unmapped,
                        default,
                        PageProtection.None,
                        PageCachePolicy.Uninitialized),
                    default).Succeeded;
    }

    private static bool TestAddressAndWidthValidation()
    {
        X64PatMappingToken pat = ValidPat();
        PageProtection read = PageProtection.Read;
        X64PageTableEntryResult zero = X64PageTableEntry.TryEncode(
            Leaf(
                X64PageTableLevel.Pte,
                default,
                read,
                PageCachePolicy.WriteBack),
            Context(48, true, true, false, pat));
        X64PageTableEntryResult pteMisaligned =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pte,
                    Physical(0x1001),
                    read,
                    PageCachePolicy.WriteBack),
                Context(48, true, true, false, pat));
        X64PageTableEntryResult pdeMisaligned =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pde,
                    Physical(0x3f_f000),
                    read,
                    PageCachePolicy.WriteBack),
                Context(48, true, true, false, pat));
        X64PageTableEntryResult pdpteMisaligned =
            X64PageTableEntry.TryEncode(
                Leaf(
                    X64PageTableLevel.Pdpte,
                    Physical(0x7fff_f000),
                    read,
                    PageCachePolicy.WriteBack),
                Context(48, true, true, false, pat));

        byte[] invalidWidths = [0, 1, 35, 53, 64, 255];
        foreach (byte width in invalidWidths)
        {
            X64PageTableEntryResult invalid =
                X64PageTableEntry.TryEncode(
                    Leaf(
                        X64PageTableLevel.Pte,
                        Physical(0x1000),
                        read,
                        PageCachePolicy.WriteBack),
                    Context(width, true, true, false, pat));
            if (invalid.Status
                != X64PageTableEntryStatus
                    .UnsupportedPhysicalAddressWidth)
            {
                return false;
            }
        }

        foreach (byte width in new byte[] { 36, 48, 52 })
        {
            ulong limit = 1UL << width;
            ulong lastPage =
                limit - X64PageTableEntry.PageSize4KiB;
            X64PageTableEntryResult last =
                X64PageTableEntry.TryEncode(
                    Leaf(
                        X64PageTableLevel.Pte,
                        Physical(lastPage),
                        read,
                        PageCachePolicy.WriteBack),
                    Context(width, true, true, false, pat));
            X64PageTableEntryResult firstOutside =
                X64PageTableEntry.TryEncode(
                    Leaf(
                        X64PageTableLevel.Pte,
                        Physical(limit),
                        read,
                        PageCachePolicy.WriteBack),
                    Context(width, true, true, false, pat));
            if (!last.Succeeded
                || last.Entry.RawValue
                    != (lastPage | Present | Nx)
                || firstOutside.Status
                    != X64PageTableEntryStatus
                        .PhysicalAddressOutsideWidth)
            {
                return false;
            }
        }

        return zero.Status
                == X64PageTableEntryStatus.InvalidPhysicalAddress
            && pteMisaligned.Status
                == X64PageTableEntryStatus.MisalignedPhysicalAddress
            && pdeMisaligned.Status
                == X64PageTableEntryStatus.MisalignedPhysicalAddress
            && pdpteMisaligned.Status
                == X64PageTableEntryStatus.MisalignedPhysicalAddress;
    }

    private static bool TestDecoderValidation()
    {
        X64PageTableEncodingPreconditions context = ValidContext();
        const ulong pteBase = 0x6789_A000;
        ulong canonicalPte = pteBase | Present | Nx;
        ulong[] unsupportedPteBits =
        [
            1UL << 2,
            Global,
            1UL << 9,
            1UL << 52,
            1UL << 59
        ];
        foreach (ulong bit in unsupportedPteBits)
        {
            X64PageTableEntryResult result =
                X64PageTableEntry.TryDecode(
                    X64PageTableLevel.Pte,
                    canonicalPte | bit,
                    context);
            if (result.Status
                != X64PageTableEntryStatus.UnsupportedEntryBits)
            {
                return false;
            }
        }

        X64PageTableEntryResult hardwareBits =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                canonicalPte | Accessed | Dirty,
                context);
        X64PageTableEntryResult tableDirty =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pde,
                0x789A_B000 | Present | Writable | Dirty,
                context);
        X64PageTableEntryResult tableReadOnly =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pde,
                0x789A_B000 | Present,
                context);
        X64PageTableEntryResult tableNx =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pde,
                0x789A_B000 | Present | Writable | Nx,
                context);
        X64PageTableEntryResult rwxLeaf =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                pteBase | Present | Writable,
                context);
        X64PageTableEntryResult pdeReservedLow =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pde,
                0x0080_0000 | Present | PageSizeOrPtePat
                    | Nx | (1UL << 13),
                context);
        X64PageTableEntryResult pdpteReservedLow =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pdpte,
                0xC000_0000 | Present | PageSizeOrPtePat
                    | Nx | (1UL << 20),
                context);
        X64PageTableEntryResult unselectedPatIndex =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                pteBase | Present | Nx | Pcd,
                context);
        X64PageTableEntryResult outsideWidth =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                (1UL << 40) | Present | Nx,
                Context(36, true, true, false, ValidPat()));

        if (!hardwareBits.Succeeded
            || !hardwareBits.Accessed
            || !hardwareBits.Dirty
            || hardwareBits.Entry.RawValue
                != (canonicalPte | Accessed | Dirty))
        {
            return false;
        }

        X64PageTableEntryResult canonicalized =
            X64PageTableEntry.TryEncode(
                Leaf(
                    hardwareBits.Level,
                    hardwareBits.PhysicalAddress,
                    hardwareBits.Protection,
                    hardwareBits.CachePolicy),
                context);
        PageProtection readExecute = Combine(
            PageProtection.Read,
            PageProtection.Execute);
        ulong readWriteWithHardwareState =
            pteBase | Present | Writable | Nx | Accessed | Dirty;
        X64PageTableEntryResult preservedProtectionRewrite =
            X64PageTableEntry.TryReencodeLeafProtection(
                X64PageTableLevel.Pte,
                readWriteWithHardwareState,
                readExecute,
                X64HardwareStatePolicy.Preserve,
                context);
        X64PageTableEntryResult clearedProtectionRewrite =
            X64PageTableEntry.TryReencodeLeafProtection(
                X64PageTableLevel.Pte,
                readWriteWithHardwareState,
                readExecute,
                X64HardwareStatePolicy.Clear,
                context);
        X64PageTableEntryResult invalidRewritePolicy =
            X64PageTableEntry.TryReencodeLeafProtection(
                X64PageTableLevel.Pte,
                readWriteWithHardwareState,
                readExecute,
                X64HardwareStatePolicy.Uninitialized,
                context);
        X64PageTableEntryResult rewriteUnmapped =
            X64PageTableEntry.TryReencodeLeafProtection(
                X64PageTableLevel.Pte,
                0,
                readExecute,
                X64HardwareStatePolicy.Preserve,
                context);
        X64PageTableEntryResult rewriteTablePointer =
            X64PageTableEntry.TryReencodeLeafProtection(
                X64PageTableLevel.Pde,
                0x789A_B000 | Present | Writable,
                readExecute,
                X64HardwareStatePolicy.Preserve,
                context);
        X64PageTableEntryResult rejectedRewriteProtection =
            X64PageTableEntry.TryReencodeLeafProtection(
                X64PageTableLevel.Pte,
                readWriteWithHardwareState,
                Combine(
                    Combine(
                        PageProtection.Read,
                        PageProtection.Write),
                    PageProtection.Execute),
                X64HardwareStatePolicy.Preserve,
                context);

        return canonicalized.Succeeded
            && canonicalized.Entry.RawValue == canonicalPte
            && tableDirty.Status
                == X64PageTableEntryStatus.DirtyBitOnTablePointer
            && tableReadOnly.Status
                == X64PageTableEntryStatus.NonCanonicalTablePointer
            && tableNx.Status
                == X64PageTableEntryStatus.NonCanonicalTablePointer
            && rwxLeaf.Status
                == X64PageTableEntryStatus
                    .WritableExecutableRejected
            && pdeReservedLow.Status
                == X64PageTableEntryStatus.UnsupportedEntryBits
            && pdpteReservedLow.Status
                == X64PageTableEntryStatus.UnsupportedEntryBits
            && unselectedPatIndex.Status
                == X64PageTableEntryStatus.CachePolicyNotSupported
            && outsideWidth.Status
                == X64PageTableEntryStatus.UnsupportedEntryBits
            && preservedProtectionRewrite.Succeeded
            && preservedProtectionRewrite.Protection == readExecute
            && preservedProtectionRewrite.Accessed
            && preservedProtectionRewrite.Dirty
            && preservedProtectionRewrite.Entry.RawValue
                == (pteBase | Present | Accessed | Dirty)
            && clearedProtectionRewrite.Succeeded
            && !clearedProtectionRewrite.Accessed
            && !clearedProtectionRewrite.Dirty
            && clearedProtectionRewrite.Entry.RawValue
                == (pteBase | Present)
            && invalidRewritePolicy.Status
                == X64PageTableEntryStatus.InvalidHardwareStatePolicy
            && rewriteUnmapped.Status
                == X64PageTableEntryStatus.ExistingEntryNotLeaf
            && rewriteTablePointer.Status
                == X64PageTableEntryStatus.ExistingEntryNotLeaf
            && rejectedRewriteProtection.Status
                == X64PageTableEntryStatus.WritableExecutableRejected;
    }

    private static unsafe bool TestPointerCore()
    {
        X64PageTableEncodingPreconditions context = ValidContext();
        X64PageTableEntryRequest* source =
            stackalloc X64PageTableEntryRequest[4];
        X64PageTableEntry* destination =
            stackalloc X64PageTableEntry[5];
        source[0] = Leaf(
            X64PageTableLevel.Pte,
            Physical(0x1000),
            PageProtection.Read,
            PageCachePolicy.WriteBack);
        source[1] = Leaf(
            X64PageTableLevel.Pde,
            Physical(0x0040_0000),
            Combine(PageProtection.Read, PageProtection.Write),
            PageCachePolicy.Uncached);
        source[2] = TablePointer(
            X64PageTableLevel.Pml4e,
            Physical(0x9000));
        source[3] = default;
        ulong[] sourceBefore = CopyBytesAsUlongs(
            source,
            4 * (int)X64PageTableEntryRequest.StructureSize);
        const ulong CanaryA = 0xA5A5_A5A5_A5A5_A5A5;
        const ulong CanaryB = 0x5A5A_5A5A_5A5A_5A5A;
        ((ulong*)destination)[0] = CanaryA;
        ((ulong*)destination)[1] = CanaryA;
        ((ulong*)destination)[2] = CanaryA;
        ((ulong*)destination)[3] = CanaryA;
        ((ulong*)destination)[4] = CanaryB;

        X64PageTableEntryBatchResult valid =
            X64PageTableEntry.TryEncodeMany(
                source,
                requestCount: 3,
                sourceCapacity: 4,
                destination,
                destinationCapacity: 5,
                context);
        if (!valid.Succeeded
            || valid.RequestCount != 3
            || valid.SourceCapacity != 4
            || valid.DestinationCapacity != 5
            || valid.EncodedCount != 3
            || valid.InvalidRequestIndex != X64PageTableEntry.NoIndex
            || destination[0].RawValue != (0x1000 | Present | Nx)
            || destination[1].RawValue
                != (0x0040_0000 | Present | Writable
                    | PageSizeOrPtePat | Pwt | Pcd | Nx)
            || destination[2].RawValue
                != (0x9000 | Present | Writable)
            || ((ulong*)destination)[3] != CanaryA
            || ((ulong*)destination)[4] != CanaryB
            || !EqualBytes(
                sourceBefore,
                source,
                4 * (int)X64PageTableEntryRequest.StructureSize))
        {
            return false;
        }

        X64PageTableEntry* repeated =
            stackalloc X64PageTableEntry[5];
        for (int index = 0; index < 5; index++)
        {
            ((ulong*)repeated)[index] = CanaryB;
        }

        X64PageTableEntryBatchResult repeatResult =
            X64PageTableEntry.TryEncodeMany(
                source,
                requestCount: 3,
                sourceCapacity: 4,
                repeated,
                destinationCapacity: 5,
                context);
        if (!repeatResult.Succeeded
            || repeated[0].RawValue != destination[0].RawValue
            || repeated[1].RawValue != destination[1].RawValue
            || repeated[2].RawValue != destination[2].RawValue
            || ((ulong*)repeated)[3] != CanaryB
            || ((ulong*)repeated)[4] != CanaryB)
        {
            return false;
        }

        ((ulong*)destination)[0] = CanaryA;
        ((ulong*)destination)[1] = CanaryA;
        ((ulong*)destination)[2] = CanaryA;
        source[2] = default;
        X64PageTableEntryBatchResult invalidTail =
            X64PageTableEntry.TryEncodeMany(
                source,
                3,
                4,
                destination,
                5,
                context);
        if (invalidTail.Status
                != X64PageTableEntryStatus.RequestNotInitialized
            || invalidTail.InvalidRequestIndex != 2
            || invalidTail.EncodedCount != 0
            || ((ulong*)destination)[0] != CanaryA
            || ((ulong*)destination)[1] != CanaryA
            || ((ulong*)destination)[2] != CanaryA)
        {
            return false;
        }

        source[2] = TablePointer(
            X64PageTableLevel.Pml4e,
            Physical(0x9000));
        byte* scratch = stackalloc byte[256];
        X64PageTableEntryRequest* alignedSource =
            (X64PageTableEntryRequest*)scratch;
        X64PageTableEntry* overlappingDestination =
            (X64PageTableEntry*)(scratch + 8);
        alignedSource[0] = source[0];
        X64PageTableEntryBatchResult overlap =
            X64PageTableEntry.TryEncodeMany(
                alignedSource,
                1,
                1,
                overlappingDestination,
                1,
                context);

        X64PageTableEntryBatchResult zero =
            X64PageTableEntry.TryEncodeMany(
                source, 0, 4, destination, 5, context);
        X64PageTableEntryBatchResult tooMany =
            X64PageTableEntry.TryEncodeMany(
                null,
                X64PageTableEntry.MaximumBatchEntryCount + 1,
                0,
                null,
                0,
                context);
        X64PageTableEntryBatchResult sourceCapacity =
            X64PageTableEntry.TryEncodeMany(
                source, 2, 1, destination, 5, context);
        X64PageTableEntryBatchResult destinationCapacity =
            X64PageTableEntry.TryEncodeMany(
                source, 2, 4, destination, 1, context);
        X64PageTableEntryBatchResult excessiveCapacity =
            X64PageTableEntry.TryEncodeMany(
                source,
                1,
                X64PageTableEntry.MaximumBatchEntryCount + 1,
                destination,
                1,
                context);
        X64PageTableEntryBatchResult missingSource =
            X64PageTableEntry.TryEncodeMany(
                null, 1, 1, destination, 1, context);
        X64PageTableEntryBatchResult missingDestination =
            X64PageTableEntry.TryEncodeMany(
                source, 1, 1, null, 1, context);
        X64PageTableEntryBatchResult misalignedSource =
            X64PageTableEntry.TryEncodeMany(
                (X64PageTableEntryRequest*)(scratch + 1),
                1,
                1,
                destination,
                1,
                context);
        X64PageTableEntryBatchResult misalignedDestination =
            X64PageTableEntry.TryEncodeMany(
                source,
                1,
                1,
                (X64PageTableEntry*)(scratch + 1),
                1,
                context);
        X64PageTableEntryBatchResult sourceOverflow =
            X64PageTableEntry.TryEncodeMany(
                (X64PageTableEntryRequest*)(~0UL - 7),
                1,
                1,
                destination,
                1,
                context);
        X64PageTableEntryBatchResult destinationOverflow =
            X64PageTableEntry.TryEncodeMany(
                source,
                1,
                1,
                (X64PageTableEntry*)(~0UL - 7),
                1,
                context);

        X64PageTableEntryRequest* maximumSource =
            stackalloc X64PageTableEntryRequest[
                (int)X64PageTableEntry.MaximumBatchEntryCount];
        X64PageTableEntry* maximumDestination =
            stackalloc X64PageTableEntry[
                (int)X64PageTableEntry.MaximumBatchEntryCount];
        for (ulong index = 0;
             index < X64PageTableEntry.MaximumBatchEntryCount;
             index++)
        {
            maximumSource[index] = Leaf(
                X64PageTableLevel.Pte,
                Physical((index + 1) * X64PageTableEntry.PageSize4KiB),
                PageProtection.Read,
                PageCachePolicy.WriteBack);
            ((ulong*)maximumDestination)[index] = CanaryA;
        }

        X64PageTableEntryBatchResult maximum =
            X64PageTableEntry.TryEncodeMany(
                maximumSource,
                X64PageTableEntry.MaximumBatchEntryCount,
                X64PageTableEntry.MaximumBatchEntryCount,
                maximumDestination,
                X64PageTableEntry.MaximumBatchEntryCount,
                context);

        return overlap.Status
                == X64PageTableEntryStatus.SourceDestinationOverlap
            && zero.Status
                == X64PageTableEntryStatus.InvalidBatchCount
            && tooMany.Status
                == X64PageTableEntryStatus.TooManyEntries
            && sourceCapacity.Status
                == X64PageTableEntryStatus.SourceCapacityTooSmall
            && destinationCapacity.Status
                == X64PageTableEntryStatus.DestinationCapacityTooSmall
            && excessiveCapacity.Status
                == X64PageTableEntryStatus.CapacityExceedsLimit
            && missingSource.Status
                == X64PageTableEntryStatus.MissingSource
            && missingDestination.Status
                == X64PageTableEntryStatus.MissingDestination
            && misalignedSource.Status
                == X64PageTableEntryStatus.MisalignedSource
            && misalignedDestination.Status
                == X64PageTableEntryStatus.MisalignedDestination
            && sourceOverflow.Status
                == X64PageTableEntryStatus.SourceSpanOverflow
            && destinationOverflow.Status
                == X64PageTableEntryStatus.DestinationSpanOverflow
            && maximum.Succeeded
            && maximum.EncodedCount
                == X64PageTableEntry.MaximumBatchEntryCount
            && maximumDestination[0].RawValue
                == (X64PageTableEntry.PageSize4KiB | Present | Nx)
            && maximumDestination[
                    X64PageTableEntry.MaximumBatchEntryCount - 1]
                .RawValue
                == (X64PageTableEntry.MaximumBatchEntryCount
                    * X64PageTableEntry.PageSize4KiB
                    | Present
                    | Nx);
    }

    private static bool TestSourceAndNoStdLibBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string memory = Path.Combine(root, "src", "OS.Kernel", "Memory");
        string protectionPath = Path.Combine(
            memory,
            "PageProtection.cs");
        string cachePath = Path.Combine(
            memory,
            "PageCachePolicy.cs");
        string entryPath = Path.Combine(
            memory,
            "X64PageTableEntry.cs");
        string addressPath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Boot",
            "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
        string[] paths =
        [
            protectionPath,
            cachePath,
            entryPath
        ];
        if (paths.Any(path => !File.Exists(path))
            || !File.Exists(addressPath))
        {
            return false;
        }

        string core = string.Join(
            "\n",
            paths.Select(path =>
                RemoveComments(File.ReadAllText(path))));
        string[] forbiddenFragments =
        [
            "using System;",
            "System.FlagsAttribute",
            "[Flags",
            "Span<",
            "ReadOnlySpan<",
            "throw ",
            "try {",
            "catch ",
            "finally",
            "typeof(",
            "dynamic ",
            "OSHostPageProtection",
            "DmaMapping",
            "EfiEntry",
            "BootSequence",
            "RuntimeExport",
            "ulong.MaxValue"
        ];
        if (forbiddenFragments.Any(fragment =>
                core.Contains(fragment, StringComparison.Ordinal))
            || Regex.IsMatch(
                core,
                @"\b(string|String|object|Object)\b",
                RegexOptions.CultureInvariant)
            || !core.Contains("~0UL", StringComparison.Ordinal)
            || !core.Contains(
                "unsafe X64PageTableEntryBatchResult TryEncodeMany(",
                StringComparison.Ordinal)
            || !core.Contains("sourceCapacity", StringComparison.Ordinal)
            || !core.Contains(
                "destinationCapacity",
                StringComparison.Ordinal)
            || !core.Contains(
                "sizeof(X64PageTableEntryRequest)",
                StringComparison.Ordinal)
            || core.Contains(
                "stackalloc X64PageTableEntry",
                StringComparison.Ordinal))
        {
            return false;
        }

        string[] productionFiles = Directory
            .EnumerateFiles(
                Path.Combine(root, "src"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path =>
                !HasDirectorySegment(path, "bin")
                && !HasDirectorySegment(path, "obj"))
            .ToArray();
        string[] levelDeclarations = productionFiles
            .Where(path => Regex.IsMatch(
                RemoveComments(File.ReadAllText(path)),
                @"\benum\s+X64PageTableLevel\b",
                RegexOptions.CultureInvariant))
            .ToArray();
        if (levelDeclarations.Length != 1
            || !Path.GetFullPath(levelDeclarations[0]).Equals(
                Path.GetFullPath(entryPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        NoStdLibBuildResult build = BuildNoStdLibClosure(
            root,
            addressPath,
            protectionPath,
            cachePath,
            entryPath);
        if (build.TimedOut
            || build.ExitCode != 0
            || !HasZeroAssemblyReferences(build.AssemblyBytes))
        {
            Console.WriteLine(
                "M01_011_NOSTDLIB_DIAGNOSTIC "
                + NormalizeOutput(build.Output));
            return false;
        }

        return true;
    }

    private static bool TestEfiAndMarkerBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string entryPath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Memory",
            "X64PageTableEntry.cs");
        string protectionPath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Memory",
            "PageProtection.cs");
        string cachePath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Memory",
            "PageCachePolicy.cs");
        string ownerPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "X64PageTableEntryTests.cs");
        string efiProject = Path.Combine(
            root,
            "src",
            "OS",
            "OS.csproj");
        if (!File.Exists(ownerPath)
            || !File.Exists(efiProject))
        {
            Console.WriteLine(
                "M01_011_BOUNDARY_DIAGNOSTIC missing_path");
            return false;
        }

        if (!EvaluatedEfiClosure(
                root,
                efiProject,
                "SingleMinimalApi",
                protectionPath,
                cachePath,
                entryPath,
                ownerPath))
        {
            Console.WriteLine(
                "M01_011_BOUNDARY_DIAGNOSTIC efi_single");
            return false;
        }

        if (!EvaluatedEfiClosure(
                root,
                efiProject,
                "MultiMinimalApi",
                protectionPath,
                cachePath,
                entryPath,
                ownerPath))
        {
            Console.WriteLine(
                "M01_011_BOUNDARY_DIAGNOSTIC efi_multi");
            return false;
        }

        string owner = File.ReadAllText(ownerPath);
        int failureGate = owner.IndexOf(
            "if (failures != 0)",
            StringComparison.Ordinal);
        int aggregateFailure = owner.IndexOf(
            "Console.WriteLine(\"TEST_FAIL \" + IssueTestName",
            StringComparison.Ordinal);
        int aggregateThrow = owner.IndexOf(
            "\"M01-011 x64 page-table entry tests failed:",
            StringComparison.Ordinal);
        int readyWrite = owner.IndexOf(
            "Console.WriteLine(ReadyMarker)",
            StringComparison.Ordinal);
        int aggregatePass = owner.IndexOf(
            "Console.WriteLine(\"TEST_PASS \" + IssueTestName)",
            StringComparison.Ordinal);
        if (CountOccurrences(owner, "\"" + ReadyMarker + "\"") != 1
            || CountOccurrences(
                owner,
                "Console.WriteLine(\"TEST_START \" + IssueTestName)") != 1
            || CountOccurrences(
                owner,
                "Console.WriteLine(\"TEST_FAIL \" + IssueTestName") != 1
            || CountOccurrences(
                owner,
                "Console.WriteLine(\"TEST_PASS \" + IssueTestName)") != 1
            || failureGate < 0
            || aggregateFailure <= failureGate
            || aggregateThrow <= aggregateFailure
            || readyWrite <= aggregateThrow
            || aggregatePass <= readyWrite
            || owner.Contains(
                "OS_" + "BOOT_OK",
                StringComparison.Ordinal)
            || owner.Contains(
                "OS_" + "SMOKE_PASS",
                StringComparison.Ordinal))
        {
            Console.WriteLine(
                "M01_011_BOUNDARY_DIAGNOSTIC marker_order"
                + " ready_literals="
                + CountOccurrences(owner, "\"" + ReadyMarker + "\"")
                + " start_writes="
                + CountOccurrences(
                    owner,
                    "Console.WriteLine(\"TEST_START \" + IssueTestName)")
                + " fail_writes="
                + CountOccurrences(
                    owner,
                    "Console.WriteLine(\"TEST_FAIL \" + IssueTestName")
                + " pass_writes="
                + CountOccurrences(
                    owner,
                    "Console.WriteLine(\"TEST_PASS \" + IssueTestName)")
                + " indexes="
                + failureGate
                + ","
                + aggregateFailure
                + ","
                + aggregateThrow
                + ","
                + readyWrite
                + ","
                + aggregatePass);
            return false;
        }

        string[] scanRoots =
        [
            "src",
            "std",
            "apps",
            "tools",
            "tests",
            "scripts",
            Path.Combine(".gitea", "workflows")
        ];
        foreach (string relativeRoot in scanRoots)
        {
            string scanRoot = Path.Combine(root, relativeRoot);
            if (!Directory.Exists(scanRoot))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(
                         scanRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                if (Path.GetFullPath(file).Equals(
                        Path.GetFullPath(ownerPath),
                        StringComparison.OrdinalIgnoreCase)
                    || HasDirectorySegment(file, "bin")
                    || HasDirectorySegment(file, "obj")
                    || HasDirectorySegment(file, "artifacts")
                    || !IsExecutableSource(file))
                {
                    continue;
                }

                if (File.ReadAllText(file).Contains(
                        ReadyMarker,
                        StringComparison.Ordinal))
                {
                    Console.WriteLine(
                        "M01_011_BOUNDARY_DIAGNOSTIC duplicate="
                        + Path.GetRelativePath(root, file));
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasExactFieldOffsets()
    {
        return Offset<X64PatMappingToken>("_rawIa32Pat") == 0
            && Offset<X64PatMappingToken>("_isInitialized") == 8
            && Offset<X64PatMappingToken>("_writeBackIndex") == 9
            && Offset<X64PatMappingToken>("_uncachedIndex") == 10
            && Offset<X64PatMappingToken>("_writeCombiningIndex") == 11
            && Offset<X64PatMappingToken>("_reserved") == 12
            && Offset<X64PageTableEncodingPreconditions>(
                "_patMapping") == 0
            && Offset<X64PageTableEncodingPreconditions>(
                "_isInitialized") == 16
            && Offset<X64PageTableEncodingPreconditions>(
                "_physicalAddressWidth") == 17
            && Offset<X64PageTableEncodingPreconditions>(
                "_nxeEnabled") == 18
            && Offset<X64PageTableEncodingPreconditions>(
                "_writeProtectEnabled") == 19
            && Offset<X64PageTableEncodingPreconditions>(
                "_fiveLevelPagingEnabled") == 20
            && Offset<X64PageTableEncodingPreconditions>(
                "_oneGiBPagesSupported") == 21
            && Offset<X64PageTableEncodingPreconditions>(
                "_reserved0") == 22
            && Offset<X64PageTableEncodingPreconditions>(
                "_reserved1") == 23
            && Offset<X64PageTableEntryRequest>(
                "_physicalAddress") == 0
            && Offset<X64PageTableEntryRequest>(
                "_isInitialized") == 8
            && Offset<X64PageTableEntryRequest>("_level") == 9
            && Offset<X64PageTableEntryRequest>("_kind") == 10
            && Offset<X64PageTableEntryRequest>(
                "_protection") == 11
            && Offset<X64PageTableEntryRequest>(
                "_cachePolicy") == 12
            && Offset<X64PageTableEntryRequest>("_reserved0") == 13
            && Offset<X64PageTableEntryRequest>("_reserved1") == 14
            && Offset<X64PageTableEntryRequest>("_reserved2") == 15
            && Offset<X64PageTableEntry>("_rawValue") == 0
            && Offset<X64PageTableEntryResult>("_entry") == 0
            && Offset<X64PageTableEntryResult>(
                "_physicalAddress") == 8
            && Offset<X64PageTableEntryResult>(
                "_pageSizeBytes") == 16
            && Offset<X64PageTableEntryResult>(
                "_isInitialized") == 24
            && Offset<X64PageTableEntryResult>("_status") == 25
            && Offset<X64PageTableEntryResult>("_level") == 26
            && Offset<X64PageTableEntryResult>("_kind") == 27
            && Offset<X64PageTableEntryResult>(
                "_protection") == 28
            && Offset<X64PageTableEntryResult>(
                "_cachePolicy") == 29
            && Offset<X64PageTableEntryResult>("_patIndex") == 30
            && Offset<X64PageTableEntryResult>("_accessed") == 31
            && Offset<X64PageTableEntryResult>("_dirty") == 32
            && Offset<X64PageTableEntryBatchResult>(
                "_requestCount") == 0
            && Offset<X64PageTableEntryBatchResult>(
                "_sourceCapacity") == 8
            && Offset<X64PageTableEntryBatchResult>(
                "_destinationCapacity") == 16
            && Offset<X64PageTableEntryBatchResult>(
                "_encodedCount") == 24
            && Offset<X64PageTableEntryBatchResult>(
                "_invalidRequestIndex") == 32
            && Offset<X64PageTableEntryBatchResult>(
                "_isInitialized") == 40
            && Offset<X64PageTableEntryBatchResult>(
                "_status") == 41;
    }

    private static bool HasExactByteEnum<T>(
        (string Name, byte Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
        {
            return false;
        }

        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();
        if (names.Length != expected.Length
            || values.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    names[index],
                    expected[index].Name,
                    StringComparison.Ordinal)
                || Convert.ToByte(values[index])
                    != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReadOnlySequentialUnmanaged<T>()
        where T : struct
    {
        Type type = typeof(T);
        return type.IsValueType
            && type.IsLayoutSequential
            && type.IsDefined(
                typeof(IsReadOnlyAttribute),
                inherit: false)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    private static long Offset<T>(string fieldName)
        where T : struct
    {
        return Marshal.OffsetOf<T>(fieldName).ToInt64();
    }

    private static X64PageTableEntryRequest Leaf(
        X64PageTableLevel level,
        PhysicalAddress address,
        PageProtection protection,
        PageCachePolicy cachePolicy)
    {
        return new X64PageTableEntryRequest(
            level,
            X64PageTableEntryKind.Leaf,
            address,
            protection,
            cachePolicy);
    }

    private static X64PageTableEntryRequest TablePointer(
        X64PageTableLevel level,
        PhysicalAddress address)
    {
        return new X64PageTableEntryRequest(
            level,
            X64PageTableEntryKind.TablePointer,
            address,
            PageProtection.None,
            PageCachePolicy.WriteBack);
    }

    private static PageProtection Combine(
        PageProtection left,
        PageProtection right)
    {
        return (PageProtection)((byte)left | (byte)right);
    }

    private static PhysicalAddress Physical(ulong value)
    {
        AddressValueStatus status =
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary(
                value,
                out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Could not construct test physical address 0x"
                + value.ToString("X16")
                + ".");
        }

        return address;
    }

    private static bool RawEquals(
        PhysicalAddress address,
        ulong expected)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value) == AddressValueStatus.Success
            && value == expected;
    }

    private static X64PatMappingToken ValidPat()
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
                "The canonical PAT fixture is invalid.");
        }

        return token;
    }

    private static X64PatMappingToken HighWriteBackPat()
    {
        X64PageTableEntryStatus status =
            X64PatMappingToken.TryCreate(
                PatFixture,
                writeBackIndex: 6,
                uncachedIndex: 3,
                writeCombiningIndex: 4,
                out X64PatMappingToken token);
        if (status != X64PageTableEntryStatus.Success)
        {
            throw new InvalidOperationException(
                "The high-index WB PAT fixture is invalid.");
        }

        return token;
    }

    private static X64PageTableEncodingPreconditions ValidContext()
    {
        return Context(
            48,
            nxeEnabled: true,
            writeProtectEnabled: true,
            fiveLevelPagingEnabled: false,
            ValidPat());
    }

    private static X64PageTableEncodingPreconditions Context(
        byte physicalAddressWidth,
        bool nxeEnabled,
        bool writeProtectEnabled,
        bool fiveLevelPagingEnabled,
        X64PatMappingToken pat,
        bool oneGiBPagesSupported = true)
    {
        return X64PageTableEncodingPreconditions.CreateObserved(
            physicalAddressWidth,
            nxeEnabled,
            writeProtectEnabled,
            fiveLevelPagingEnabled,
            oneGiBPagesSupported,
            pat);
    }

    private static unsafe T OverwriteByte<T>(
        T value,
        int byteOffset,
        byte replacement)
        where T : unmanaged
    {
        if (byteOffset < 0 || byteOffset >= sizeof(T))
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        ((byte*)&value)[byteOffset] = replacement;
        return value;
    }

    private static unsafe ulong[] CopyBytesAsUlongs(
        void* source,
        int byteCount)
    {
        if ((byteCount & 7) != 0)
        {
            throw new InvalidOperationException(
                "Test byte snapshot must be ulong aligned.");
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

    private static unsafe bool EqualBytes(
        ulong[] expected,
        void* actual,
        int byteCount)
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

    private static NoStdLibBuildResult BuildNoStdLibClosure(
        string repositoryRoot,
        string addressPath,
        string protectionPath,
        string cachePath,
        string entryPath)
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-011-" + Guid.NewGuid().ToString("N"));
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
                            OS.Boot.AddressValueStatus addressStatus =
                                OS.Boot.PhysicalAddress
                                    .TryCreateFromHardwareRegisterBoundary(
                                        0x2000,
                                        out OS.Boot.PhysicalAddress address);
                            if (patStatus !=
                                    OS.Kernel.Memory.X64PageTableEntryStatus.Success ||
                                addressStatus !=
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
                            OS.Kernel.Memory.X64PageTableEntryRequest* requests =
                                stackalloc
                                    OS.Kernel.Memory
                                        .X64PageTableEntryRequest[1];
                            OS.Kernel.Memory.X64PageTableEntry* entries =
                                stackalloc
                                    OS.Kernel.Memory.X64PageTableEntry[1];
                            requests[0] =
                                new OS.Kernel.Memory
                                    .X64PageTableEntryRequest(
                                        OS.Kernel.Memory
                                            .X64PageTableLevel.Pte,
                                        OS.Kernel.Memory
                                            .X64PageTableEntryKind.Leaf,
                                        address,
                                        OS.Kernel.Memory
                                            .PageProtection.Read,
                                        OS.Kernel.Memory
                                            .PageCachePolicy.WriteBack);
                            OS.Kernel.Memory.X64PageTableEntryBatchResult batch =
                                OS.Kernel.Memory.X64PageTableEntry.TryEncodeMany(
                                    requests,
                                    1,
                                    1,
                                    entries,
                                    1,
                                    context);
                            OS.Kernel.Memory.X64PageTableEntryResult decoded =
                                OS.Kernel.Memory.X64PageTableEntry.TryDecode(
                                    OS.Kernel.Memory
                                        .X64PageTableLevel.Pte,
                                    entries[0].RawValue,
                                    context);
                            return batch.Succeeded && decoded.Succeeded
                                ? entries[0].RawValue
                                : 0;
                        }
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            string project =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.X64PageTableEntryCompileClosure</AssemblyName>
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
                    <Compile Include="__ADDRESS__" Link="Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs" />
                    <Compile Include="__PROTECTION__" Link="Memory\PageProtection.cs" />
                    <Compile Include="__CACHE__" Link="Memory\PageCachePolicy.cs" />
                    <Compile Include="__ENTRY__" Link="Memory\X64PageTableEntry.cs" />
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
                    "__ADDRESS__",
                    EscapeXml(addressPath),
                    StringComparison.Ordinal)
                .Replace(
                    "__PROTECTION__",
                    EscapeXml(protectionPath),
                    StringComparison.Ordinal)
                .Replace(
                    "__CACHE__",
                    EscapeXml(cachePath),
                    StringComparison.Ordinal)
                .Replace(
                    "__ENTRY__",
                    EscapeXml(entryPath),
                    StringComparison.Ordinal);
            string projectPath = Path.Combine(
                workspace,
                "M01.011.NoStdLib.csproj");
            File.WriteAllText(
                projectPath,
                project,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            ProcessStartInfo start = DotnetStart(workspace);
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
            ProcessResult process = RunBounded(start, 90_000);
            string assemblyPath = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.X64PageTableEntryCompileClosure.dll");
            byte[] assembly = File.Exists(assemblyPath)
                ? File.ReadAllBytes(assemblyPath)
                : [];
            return new NoStdLibBuildResult(
                process.ExitCode,
                process.TimedOut,
                process.Output,
                assembly);
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static bool EvaluatedEfiClosure(
        string repositoryRoot,
        string projectPath,
        string workloadProfile,
        params string[] forbiddenPaths)
    {
        ProcessStartInfo start = DotnetStart(repositoryRoot);
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
        ProcessResult result = RunBounded(start, 60_000);
        if (result.TimedOut || result.ExitCode != 0)
        {
            Console.WriteLine(
                "M01_011_EFI_EVAL_DIAGNOSTIC profile="
                + workloadProfile
                + " "
                + NormalizeOutput(result.Output));
            return false;
        }

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(result.StandardOutput);
            JsonElement items = document.RootElement
                .GetProperty("Items");
            JsonElement compile = items.GetProperty("Compile");
            JsonElement projectReferences =
                items.GetProperty("ProjectReference");
            JsonElement references = items.GetProperty("Reference");
            if (compile.ValueKind != JsonValueKind.Array
                || compile.GetArrayLength() == 0
                || projectReferences.ValueKind != JsonValueKind.Array
                || projectReferences.GetArrayLength() != 0
                || references.ValueKind != JsonValueKind.Array
                || references.GetArrayLength() != 0)
            {
                Console.WriteLine(
                    "M01_011_EFI_EVAL_DIAGNOSTIC profile="
                    + workloadProfile
                    + " malformed_items");
                return false;
            }

            bool efiEntry = false;
            bool minimalRuntime = false;
            foreach (JsonElement item in compile.EnumerateArray())
            {
                string identity =
                    item.GetProperty("Identity").GetString() ?? "";
                string fullPath =
                    item.GetProperty("FullPath").GetString() ?? "";
                if (identity.Length == 0
                    || fullPath.Length == 0
                    || !Path.IsPathFullyQualified(fullPath))
                {
                    Console.WriteLine(
                        "M01_011_EFI_EVAL_DIAGNOSTIC profile="
                        + workloadProfile
                        + " malformed_compile_item");
                    return false;
                }

                efiEntry |= fullPath.EndsWith(
                    Path.Combine("Boot", "EfiEntry.cs"),
                    StringComparison.OrdinalIgnoreCase);
                minimalRuntime |= fullPath.EndsWith(
                    Path.Combine("Runtime", "MinimalRuntime.cs"),
                    StringComparison.OrdinalIgnoreCase);
                foreach (string forbiddenPath in forbiddenPaths)
                {
                    if (Path.GetFullPath(fullPath).Equals(
                            Path.GetFullPath(forbiddenPath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            "M01_011_EFI_EVAL_DIAGNOSTIC profile="
                            + workloadProfile
                            + " forbidden="
                            + Path.GetFileName(forbiddenPath));
                        return false;
                    }
                }
            }

            if (!efiEntry || !minimalRuntime)
            {
                Console.WriteLine(
                    "M01_011_EFI_EVAL_DIAGNOSTIC profile="
                    + workloadProfile
                    + " anchors="
                    + efiEntry
                    + ","
                    + minimalRuntime);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "M01_011_EFI_EVAL_DIAGNOSTIC profile="
                + workloadProfile
                + " parse="
                + ex.GetType().Name);
            return false;
        }
    }

    private static ProcessStartInfo DotnetStart(
        string workingDirectory)
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

    private static ProcessResult RunBounded(
        ProcessStartInfo start,
        int timeoutMilliseconds)
    {
        using Process process = new() { StartInfo = start };
        if (!process.Start())
        {
            return new ProcessResult(
                -1,
                timedOut: false,
                "",
                "Process did not start.");
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
        bool streamsCompleted = Task.WaitAll(
            [stdout, stderr],
            millisecondsTimeout: 5_000);
        string standardOutput = streamsCompleted
            ? stdout.GetAwaiter().GetResult()
            : "";
        string standardError = streamsCompleted
            ? stderr.GetAwaiter().GetResult()
            : "Output streams did not close within the bounded timeout.";
        return new ProcessResult(
            stopped ? process.ExitCode : -1,
            timedOut: !exited || !stopped || !streamsCompleted,
            standardOutput,
            standardError);
    }

    private static bool HasZeroAssemblyReferences(byte[] assembly)
    {
        if (assembly.Length == 0)
        {
            return false;
        }

        using MemoryStream stream = new(assembly, writable: false);
        using PEReader peReader = new(stream);
        return peReader.HasMetadata
            && peReader.GetMetadataReader().AssemblyReferences.Count == 0;
    }

    private static string RemoveComments(string source)
    {
        return Regex.Replace(
            source,
            @"//.*?$|/\*.*?\*/",
            "",
            RegexOptions.Multiline
                | RegexOptions.Singleline
                | RegexOptions.CultureInvariant);
    }

    private static bool HasDirectorySegment(
        string path,
        string segment)
    {
        string marker =
            Path.DirectorySeparatorChar
            + segment
            + Path.DirectorySeparatorChar;
        return path.Contains(
            marker,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExecutableSource(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".ps1",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".yml",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".yaml",
                StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(
        string value,
        string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(
                    search,
                    index,
                    StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
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

    private static string NormalizeOutput(string output)
    {
        string normalized = output
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return normalized.Length <= 1200
            ? normalized
            : normalized[..1200];
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string fullWorkspace = Path.GetFullPath(workspace);
        string expectedPrefix =
            Path.TrimEndingDirectorySeparator(tempRoot)
            + Path.DirectorySeparatorChar;
        string leaf = Path.GetFileName(fullWorkspace);
        if (!fullWorkspace.StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase)
            || !leaf.StartsWith(
                "clros-m01-011-",
                StringComparison.Ordinal)
            || leaf.Length <= "clros-m01-011-".Length)
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-011 workspace.");
        }

        if (Directory.Exists(fullWorkspace))
        {
            Directory.Delete(fullWorkspace, recursive: true);
        }
    }

    private readonly struct ProcessResult
    {
        public ProcessResult(
            int exitCode,
            bool timedOut,
            string standardOutput,
            string standardError)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public int ExitCode { get; }

        public bool TimedOut { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public string Output =>
            StandardOutput
            + Environment.NewLine
            + StandardError;
    }

    private readonly struct NoStdLibBuildResult
    {
        public NoStdLibBuildResult(
            int exitCode,
            bool timedOut,
            string output,
            byte[] assembly)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
            Assembly = assembly;
        }

        public int ExitCode { get; }

        public bool TimedOut { get; }

        public string Output { get; }

        public byte[] Assembly { get; }

        public byte[] AssemblyBytes => Assembly;
    }
}
