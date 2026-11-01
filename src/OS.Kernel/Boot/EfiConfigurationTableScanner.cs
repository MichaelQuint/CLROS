namespace OS.Boot;
public static unsafe class EfiConfigurationTableScanner
{
    public const ulong EntrySize = 24;
    public const ulong MaximumTableCount = 4096;
    public const ulong MaximumUnsignedLong = 0xffffffffffffffff;
    public static EfiConfigurationTableGuid Acpi20TableGuid
    {
        get
        {
            return new EfiConfigurationTableGuid(0x8868e871, 0xe4f1, 0x11d3, 0xbc, 0x22, 0x00, 0x80, 0xc7, 0x3c, 0x88, 0x81);
        }
    }

    public static EfiConfigurationTableGuid Acpi10TableGuid
    {
        get
        {
            return new EfiConfigurationTableGuid(0xeb9d2d30, 0x2d88, 0x11d3, 0x9a, 0x16, 0x00, 0x90, 0x27, 0x3f, 0xc1, 0x4d);
        }
    }

#if !NOSTDLIB_EFI
    public static EfiConfigurationTableScanResult Scan(ReadOnlySpan<EfiConfigurationTableEntry> tables)
    {
        ulong acpi10Rsdp = 0;
        for (int index = 0; index < tables.Length; index++)
        {
            EfiConfigurationTableEntry entry = tables[index];
            if (EqualsGuid(entry.VendorGuid, Acpi20TableGuid))
            {
                if (entry.VendorTable == 0)
                {
                    return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.InvalidPointer);
                }

                return EfiConfigurationTableScanResult.Success(EfiAcpiTableKind.Acpi20, entry.VendorTable);
            }

            if (acpi10Rsdp == 0 && EqualsGuid(entry.VendorGuid, Acpi10TableGuid))
            {
                acpi10Rsdp = entry.VendorTable;
            }
        }

        if (acpi10Rsdp != 0)
        {
            return EfiConfigurationTableScanResult.Success(EfiAcpiTableKind.Acpi10, acpi10Rsdp);
        }

        for (int index = 0; index < tables.Length; index++)
        {
            EfiConfigurationTableEntry entry = tables[index];
            if (EqualsGuid(entry.VendorGuid, Acpi10TableGuid) && entry.VendorTable == 0)
            {
                return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.InvalidPointer);
            }
        }

        return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.NotFound);
    }

#endif
    public static EfiConfigurationTableScanResult Scan(void* configurationTable, ulong tableCount)
    {
        if (tableCount > MaximumTableCount)
        {
            return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.InvalidConfigurationTableCount);
        }

        if (tableCount == 0)
        {
            return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.NotFound);
        }

        if (configurationTable == null)
        {
            return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.InvalidConfigurationTableAddress);
        }

        if (tableCount > MaximumUnsignedLong / EntrySize)
        {
            return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.InvalidConfigurationTableCount);
        }

        byte* tableBytes = (byte*)configurationTable;
        ulong acpi10Rsdp = 0;
        bool sawAcpi10ZeroPointer = false;
        for (ulong index = 0; index < tableCount; index++)
        {
            byte* entry = tableBytes + (index * EntrySize);
            EfiConfigurationTableGuid vendorGuid = ReadGuid(entry);
            ulong vendorTable = ReadUInt64LittleEndian(entry + 16);
            if (EqualsGuid(vendorGuid, Acpi20TableGuid))
            {
                if (vendorTable == 0)
                {
                    return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.InvalidPointer);
                }

                return EfiConfigurationTableScanResult.Success(EfiAcpiTableKind.Acpi20, vendorTable);
            }

            if (EqualsGuid(vendorGuid, Acpi10TableGuid))
            {
                if (vendorTable == 0)
                {
                    sawAcpi10ZeroPointer = true;
                }
                else if (acpi10Rsdp == 0)
                {
                    acpi10Rsdp = vendorTable;
                }
            }
        }

        if (acpi10Rsdp != 0)
        {
            return EfiConfigurationTableScanResult.Success(EfiAcpiTableKind.Acpi10, acpi10Rsdp);
        }

        if (sawAcpi10ZeroPointer)
        {
            return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.InvalidPointer);
        }

        return EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.NotFound);
    }

    private static bool EqualsGuid(EfiConfigurationTableGuid left, EfiConfigurationTableGuid right)
    {
        return left.Data1 == right.Data1 && left.Data2 == right.Data2 && left.Data3 == right.Data3 && left.Data4 == right.Data4 && left.Data5 == right.Data5 && left.Data6 == right.Data6 && left.Data7 == right.Data7 && left.Data8 == right.Data8 && left.Data9 == right.Data9 && left.Data10 == right.Data10 && left.Data11 == right.Data11;
    }

    private static EfiConfigurationTableGuid ReadGuid(byte* source)
    {
        return new EfiConfigurationTableGuid(ReadUInt32LittleEndian(source), ReadUInt16LittleEndian(source + 4), ReadUInt16LittleEndian(source + 6), source[8], source[9], source[10], source[11], source[12], source[13], source[14], source[15]);
    }

    private static ushort ReadUInt16LittleEndian(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static uint ReadUInt32LittleEndian(byte* source)
    {
        return source[0] | ((uint)source[1] << 8) | ((uint)source[2] << 16) | ((uint)source[3] << 24);
    }

    private static ulong ReadUInt64LittleEndian(byte* source)
    {
        return (ulong)source[0] | ((ulong)source[1] << 8) | ((ulong)source[2] << 16) | ((ulong)source[3] << 24) | ((ulong)source[4] << 32) | ((ulong)source[5] << 40) | ((ulong)source[6] << 48) | ((ulong)source[7] << 56);
    }
}
