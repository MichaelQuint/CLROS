using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Std.Binary;

internal static unsafe class EfiConfigurationTableScannerTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("uefi.config acpi2 preferred", TestAcpi20Preferred, ref failures);
        Run("uefi.config acpi1 fallback", TestAcpi10Fallback, ref failures);
        Run("uefi.config acpi missing", TestAcpiMissing, ref failures);
        Run("uefi.config acpi zero pointer rejected", TestZeroPointerRejected, ref failures);
        Run("uefi.config invalid metadata rejected", TestInvalidMetadataRejected, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("EFI configuration-table scanner host tests failed: " + failures);
        }
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
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAcpi20Preferred()
    {
        EfiConfigurationTableEntry[] tables =
        [
            new EfiConfigurationTableEntry(
                EfiConfigurationTableScanner.Acpi10TableGuid,
                0x1000),
            new EfiConfigurationTableEntry(
                EfiConfigurationTableScanner.Acpi20TableGuid,
                0x2000)
        ];

        EfiConfigurationTableScanResult spanResult = EfiConfigurationTableScanner.Scan(tables);
        EfiConfigurationTableScanResult pointerResult = ScanPointer(tables);

        return IsSuccess(spanResult, EfiAcpiTableKind.Acpi20, 0x2000)
            && IsSuccess(pointerResult, EfiAcpiTableKind.Acpi20, 0x2000);
    }

    private static bool TestAcpi10Fallback()
    {
        EfiConfigurationTableEntry[] tables =
        [
            new EfiConfigurationTableEntry(CreateOtherGuid(), 0xaaaa),
            new EfiConfigurationTableEntry(
                EfiConfigurationTableScanner.Acpi10TableGuid,
                0x3000)
        ];

        EfiConfigurationTableScanResult spanResult = EfiConfigurationTableScanner.Scan(tables);
        EfiConfigurationTableScanResult pointerResult = ScanPointer(tables);

        return IsSuccess(spanResult, EfiAcpiTableKind.Acpi10, 0x3000)
            && IsSuccess(pointerResult, EfiAcpiTableKind.Acpi10, 0x3000);
    }

    private static bool TestAcpiMissing()
    {
        EfiConfigurationTableEntry[] tables =
        [
            new EfiConfigurationTableEntry(CreateOtherGuid(), 0x4000)
        ];

        EfiConfigurationTableScanResult spanResult = EfiConfigurationTableScanner.Scan(tables);
        EfiConfigurationTableScanResult pointerResult = ScanPointer(tables);

        return IsFailure(spanResult, EfiConfigurationTableScanStatus.NotFound)
            && IsFailure(pointerResult, EfiConfigurationTableScanStatus.NotFound);
    }

    private static bool TestZeroPointerRejected()
    {
        EfiConfigurationTableEntry[] acpi20Zero =
        [
            new EfiConfigurationTableEntry(
                EfiConfigurationTableScanner.Acpi10TableGuid,
                0x5000),
            new EfiConfigurationTableEntry(
                EfiConfigurationTableScanner.Acpi20TableGuid,
                0)
        ];

        EfiConfigurationTableEntry[] acpi10Zero =
        [
            new EfiConfigurationTableEntry(
                EfiConfigurationTableScanner.Acpi10TableGuid,
                0)
        ];

        EfiConfigurationTableScanResult acpi20SpanResult = EfiConfigurationTableScanner.Scan(acpi20Zero);
        EfiConfigurationTableScanResult acpi20PointerResult = ScanPointer(acpi20Zero);
        EfiConfigurationTableScanResult acpi10SpanResult = EfiConfigurationTableScanner.Scan(acpi10Zero);
        EfiConfigurationTableScanResult acpi10PointerResult = ScanPointer(acpi10Zero);

        return IsFailure(acpi20SpanResult, EfiConfigurationTableScanStatus.InvalidPointer)
            && IsFailure(acpi20PointerResult, EfiConfigurationTableScanStatus.InvalidPointer)
            && IsFailure(acpi10SpanResult, EfiConfigurationTableScanStatus.InvalidPointer)
            && IsFailure(acpi10PointerResult, EfiConfigurationTableScanStatus.InvalidPointer);
    }

    private static bool TestInvalidMetadataRejected()
    {
        EfiConfigurationTableScanResult invalidAddress = EfiConfigurationTableScanner.Scan(
            null,
            tableCount: 1);

        EfiConfigurationTableScanResult invalidCount = EfiConfigurationTableScanner.Scan(
            null,
            EfiConfigurationTableScanner.MaximumTableCount + 1);

        return IsFailure(invalidAddress, EfiConfigurationTableScanStatus.InvalidConfigurationTableAddress)
            && IsFailure(invalidCount, EfiConfigurationTableScanStatus.InvalidConfigurationTableCount);
    }

    private static EfiConfigurationTableScanResult ScanPointer(EfiConfigurationTableEntry[] entries)
    {
        byte[] tableBytes = new byte[entries.Length * (int)EfiConfigurationTableScanner.EntrySize];

        for (int index = 0; index < entries.Length; index++)
        {
            WriteEntry(
                tableBytes.AsSpan(index * (int)EfiConfigurationTableScanner.EntrySize, (int)EfiConfigurationTableScanner.EntrySize),
                entries[index]);
        }

        fixed (byte* tablePointer = tableBytes)
        {
            return EfiConfigurationTableScanner.Scan(tablePointer, (ulong)entries.Length);
        }
    }

    private static bool IsSuccess(
        EfiConfigurationTableScanResult result,
        EfiAcpiTableKind kind,
        ulong rsdpAddress)
    {
        return result.Succeeded
            && result.Status == EfiConfigurationTableScanStatus.Ok
            && result.TableKind == kind
            && result.RsdpAddress == rsdpAddress;
    }

    private static bool IsFailure(
        EfiConfigurationTableScanResult result,
        EfiConfigurationTableScanStatus status)
    {
        return !result.Succeeded
            && result.Status == status
            && result.TableKind == EfiAcpiTableKind.None
            && result.RsdpAddress == 0;
    }

    private static EfiConfigurationTableGuid CreateOtherGuid()
    {
        return new EfiConfigurationTableGuid(
            0x11111111,
            0x2222,
            0x3333,
            0x44,
            0x55,
            0x66,
            0x77,
            0x88,
            0x99,
            0xaa,
            0xbb);
    }

    private static void WriteEntry(Span<byte> destination, EfiConfigurationTableEntry entry)
    {
        WriteGuid(destination[..16], entry.VendorGuid);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(16, 8), entry.VendorTable);
    }

    private static void WriteGuid(Span<byte> destination, EfiConfigurationTableGuid value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), value.Data1);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), value.Data2);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), value.Data3);
        destination[8] = value.Data4;
        destination[9] = value.Data5;
        destination[10] = value.Data6;
        destination[11] = value.Data7;
        destination[12] = value.Data8;
        destination[13] = value.Data9;
        destination[14] = value.Data10;
        destination[15] = value.Data11;
    }
}
