using OS.Kernel.Platform;
using OS.Std.Binary;

internal static unsafe class OwnedACPIDirectorySelectMADTHPETMCFGTests
{
    [HostTest("acpi.owned_directory selects xsdt rsdt and extended rsdt fallback", HostLane.Fast, 0)]
    private static bool SelectsRetainedTables()
    {
        foreach ((bool xsdt, bool extended) in new[] { (true, true), (false, false), (false, true) })
        {
            var (owned, rows) = Fixture(xsdt, extended);
            var result = SelectPreservingInputs(owned, rows);
            HostCheck.True(result.Succeeded && result.Status == OwnedAcpiDirectorySelectionStatus.Success);
            HostCheck.True(Matches(result.Mcfg, rows, 2) && Matches(result.Madt, rows, 3) && Matches(result.Hpet, rows, 4));
            // Root order is APIC, HPET, MCFG, then an omitted child; output order is fixed.
            HostCheck.Equal(2UL, result.Mcfg.RootIndex);
            HostCheck.Equal(0UL, result.Madt.RootIndex);
            HostCheck.Equal(1UL, result.Hpet.RootIndex);
        }
        return true;
    }

    [HostTest("acpi.owned_directory rejects invalid storage before access", HostLane.Fast, 1)]
    private static bool RejectsInvalidStorage()
    {
        var (owned, rows) = Fixture();
        fixed (byte* bytes = owned)
        fixed (ulong* directory = rows)
        {
            Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select(null, (ulong)owned.Length, directory, 30));
            Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select(bytes, 0, directory, 30));
            Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select(bytes, 266241, directory, 30));
            Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select((byte*)(~0UL - 7), 16, directory, 30));
            Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select(bytes, (ulong)owned.Length, null, 30));
            Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select(bytes, (ulong)owned.Length, (ulong*)(~0UL - 7), 30));
            Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select(bytes, (ulong)owned.Length, (ulong*)((byte*)directory + 1), 30));
            Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select(bytes, (ulong)owned.Length, (ulong*)bytes, 30));
            foreach (uint words in new uint[] { 0, 29, 31, uint.MaxValue })
                Invalid(OwnedACPIDirectorySelectMADTHPETMCFG.Select(bytes, (ulong)owned.Length, directory, words));
        }
        return true;
    }

    [HostTest("acpi.owned_directory rejects corrupt retained rows without mutation", HostLane.Fast, 2)]
    private static bool RejectsInvalidRows()
    {
        (string Name, Action<ulong[]> Change)[] cases =
        [
            ("missing HPET", r => r[26] = 0),
            ("short RSDP", r => r[2] = 19),
            ("short SDT", r => r[14] = 35),
            ("large RSDP", r => r[2] = 4097),
            ("large SDT", r => r[14] = 65537),
            ("overflowing length", r => r[14] = ulong.MaxValue),
            ("overflowing offset", r => r[16] = ulong.MaxValue),
            ("noncontiguous offset", r => r[22]++),
            ("uncompared byte", r => r[17]--),
            ("null physical", r => r[12] = 0),
            ("overflowing physical range", r => r[12] = ulong.MaxValue - 35),
            ("duplicate physical", r => r[18] = r[12]),
            ("overlapping physical ranges", r => r[18] = r[12] + 48),
            ("wrong retained signature", r => r[13] = 0),
            ("wrong RSDP signature", r => r[1] = 0),
            ("wrong RSDP root index", r => r[3] = 0),
            ("wrong root index sentinel", r => r[9] = 0),
            ("wrong selected root index", r => r[15] = 1),
            ("unbounded selected root index", r => r[15] = ulong.MaxValue)
        ];
        foreach (var test in cases)
        {
            var (owned, rows) = Fixture();
            test.Change(rows);
            Invalid(SelectPreservingInputs(owned, rows), test.Name);
        }
        var (bytes, descriptors) = Fixture();
        Invalid(SelectPreservingInputs(bytes, descriptors, (ulong)bytes.Length - 1), "truncated retained bytes");
        Invalid(SelectPreservingInputs(bytes, descriptors, (ulong)bytes.Length + 1), "nonexact TotalBytes");
        fixed (byte* ownedPointer = bytes)
        fixed (ulong* directoryPointer = descriptors)
        {
            descriptors[12] = (ulong)ownedPointer;
            Invalid(SelectPreservingInputs(bytes, descriptors), "physical range aliases retained storage");
            descriptors[12] = (ulong)directoryPointer;
            Invalid(SelectPreservingInputs(bytes, descriptors), "physical range aliases directory");
        }
        return true;
    }

    [HostTest("acpi.owned_directory rejects corrupt envelopes and root associations", HostLane.Fast, 3)]
    private static bool RejectsInvalidBytes()
    {
        (string Name, Action<byte[], ulong[]> Change)[] cases =
        [
            ("RSDP signature", (b, r) => { b[0] = 0; FixRsdp(b, (int)r[2]); }),
            ("RSDP checksum", (b, _) => b[8]++),
            ("extended RSDP checksum", (b, _) => b[32]++),
            ("reserved revision", (b, r) => { b[15] = 1; FixRsdp(b, (int)r[2]); }),
            ("legacy RSDP with extended length", (b, r) => { b[15] = 0; FixRsdp(b, (int)r[2]); }),
            ("zero OEM ID", (b, r) => { b.AsSpan(9, 6).Clear(); FixRsdp(b, (int)r[2]); }),
            ("declared RSDP length", (b, r) => { EndianPrimitives.WriteUInt32LittleEndian(b.AsSpan(20), 35); FixRsdp(b, (int)r[2]); }),
            ("root physical association", (b, r) => { EndianPrimitives.WriteUInt64LittleEndian(b.AsSpan(24), 0x9000); FixRsdp(b, (int)r[2]); }),
            ("SDT signature", (b, r) => { b[(int)r[16]] = 0; FixTable(b, r, 2); }),
            ("SDT declared length", (b, r) => { EndianPrimitives.WriteUInt32LittleEndian(b.AsSpan((int)r[22] + 4), uint.MaxValue); FixTable(b, r, 3); }),
            ("SDT checksum", (b, r) => b[(int)r[28] + 9]++),
            ("duplicate root pointer", (b, r) => ChangePointer(b, r, 3, 0x5000)),
            ("null root pointer", (b, r) => ChangePointer(b, r, 3, 0)),
            ("overflowing root pointer", (b, r) => ChangePointer(b, r, 3, ulong.MaxValue - 20)),
            ("root pointer inside selected table", (b, r) => ChangePointer(b, r, 3, 0x5000 + 40)),
            ("root pointer overlapping another header", (b, r) => ChangePointer(b, r, 3, 0x3000 - 20)),
            ("root pointer overlapping RSDP", (b, r) => ChangePointer(b, r, 3, 0x1000)),
            ("selected physical association", (b, r) => ChangePointer(b, r, 2, 0xA000))
        ];
        foreach (var test in cases)
        {
            var (owned, rows) = Fixture();
            test.Change(owned, rows);
            Invalid(SelectPreservingInputs(owned, rows), test.Name);
        }
        return true;
    }

    [HostTest("acpi.owned_directory bounds root shape count and default result", HostLane.Fast, 4)]
    private static bool BoundsRootAndDefault()
    {
        Invalid(default, "default result");
        foreach ((int count, int extra) in new[] { (0, 0), (257, 0), (4, 1) })
        {
            var (owned, rows) = Fixture(pointerCount: count, extraRootByte: extra);
            var result = SelectPreservingInputs(owned, rows);
            Invalid(result, "invalid root shape/count");
            HostCheck.Equal(OwnedAcpiDirectorySelectionStatus.InvalidRoot, result.Status);
        }
        var (bounded, boundedRows) = Fixture(pointerCount: 256);
        HostCheck.True(SelectPreservingInputs(bounded, boundedRows).Succeeded);
        return true;
    }

    private static OwnedAcpiDirectorySelectionResult SelectPreservingInputs(
        byte[] owned, ulong[] rows, ulong? ownedBytes = null)
    {
        byte[] original = (byte[])owned.Clone();
        ulong[] originalRows = (ulong[])rows.Clone();
        OwnedAcpiDirectorySelectionResult result;
        fixed (byte* bytes = owned)
        fixed (ulong* directory = rows)
            result = OwnedACPIDirectorySelectMADTHPETMCFG.Select(bytes, ownedBytes ?? (ulong)owned.Length, directory, (uint)rows.Length);
        HostCheck.True(owned.AsSpan().SequenceEqual(original), "retained bytes changed");
        HostCheck.True(rows.AsSpan().SequenceEqual(originalRows), "directory changed");
        return result;
    }

    private static void Invalid(OwnedAcpiDirectorySelectionResult result, string reason = "invalid storage") =>
        HostCheck.True(!result.Succeeded && result.Status != OwnedAcpiDirectorySelectionStatus.Success &&
            Empty(result.Mcfg) && Empty(result.Madt) && Empty(result.Hpet), reason);

    private static bool Empty(OwnedAcpiSelectedTable table) =>
        table.PhysicalAddress == 0 && table.Offset == 0 && table.Length == 0 && table.RootIndex == 0;

    private static bool Matches(OwnedAcpiSelectedTable table, ulong[] rows, int role) =>
        table.PhysicalAddress == rows[role * 6] && table.Offset == rows[role * 6 + 4] &&
        table.Length == rows[role * 6 + 2] && table.RootIndex == rows[role * 6 + 3];

    private static (byte[] Owned, ulong[] Rows) Fixture(
        bool xsdt = true, bool extended = true, int pointerCount = 4, int extraRootByte = 0)
    {
        int rsdpLength = extended ? 36 : 20;
        int width = xsdt ? 8 : 4;
        int rootLength = 36 + pointerCount * width + extraRootByte;
        // Literal ACPI signatures and lengths provide independent wire-format fixtures.
        ulong[] signatures = [0x2052545020445352, xsdt ? 0x54445358UL : 0x54445352UL, 0x4746434D, 0x43495041, 0x54455048];
        ulong[] addresses = [0x1000, 0x2000, 0x5000, 0x3000, 0x4000];
        int[] lengths = [rsdpLength, rootLength, 60, 44, 56];
        ulong[] indices = [ulong.MaxValue, ulong.MaxValue, 2, 0, 1];
        byte[] owned = new byte[rsdpLength + rootLength + 60 + 44 + 56];
        ulong[] rows = new ulong[30];
        int offset = 0;
        for (int role = 0; role < 5; role++)
        {
            int row = role * 6;
            rows[row] = addresses[role];
            rows[row + 1] = signatures[role];
            rows[row + 2] = (ulong)lengths[role];
            rows[row + 3] = indices[role];
            rows[row + 4] = (ulong)offset;
            rows[row + 5] = (ulong)lengths[role];
            if (role > 0)
            {
                EndianPrimitives.WriteUInt32LittleEndian(owned.AsSpan(offset), (uint)signatures[role]);
                EndianPrimitives.WriteUInt32LittleEndian(owned.AsSpan(offset + 4), (uint)lengths[role]);
                owned[offset + 8] = 1;
            }
            offset += lengths[role];
        }
        "RSD PTR "u8.CopyTo(owned);
        "CLROS "u8.CopyTo(owned.AsSpan(9));
        owned[15] = extended ? (byte)2 : (byte)0;
        EndianPrimitives.WriteUInt32LittleEndian(owned.AsSpan(16), xsdt ? 0x9000U : 0x2000U);
        if (extended)
        {
            EndianPrimitives.WriteUInt32LittleEndian(owned.AsSpan(20), (uint)rsdpLength);
            EndianPrimitives.WriteUInt64LittleEndian(owned.AsSpan(24), xsdt ? 0x2000UL : 0);
        }
        for (int i = 0; i < pointerCount; i++)
        {
            ulong address = i == 0 ? 0x3000UL : i == 1 ? 0x4000UL : i == 2 ? 0x5000UL : 0x6000UL + (ulong)(i - 3) * 4096;
            Span<byte> slot = owned.AsSpan(rsdpLength + 36 + i * width);
            if (xsdt)
                EndianPrimitives.WriteUInt64LittleEndian(slot, address);
            else
                EndianPrimitives.WriteUInt32LittleEndian(slot, (uint)address);
        }
        FixRsdp(owned, rsdpLength);
        for (int role = 1; role < 5; role++)
            FixTable(owned, rows, role);
        return (owned, rows);
    }

    private static void ChangePointer(byte[] bytes, ulong[] rows, int index, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(bytes.AsSpan((int)rows[10] + 36 + index * 8), value);
        FixTable(bytes, rows, 1);
    }

    private static void FixTable(byte[] bytes, ulong[] rows, int role) =>
        FixChecksum(bytes.AsSpan((int)rows[role * 6 + 4], (int)rows[role * 6 + 2]), 9);

    private static void FixRsdp(byte[] bytes, int length)
    {
        FixChecksum(bytes.AsSpan(0, 20), 8);
        if (length >= 36)
            FixChecksum(bytes.AsSpan(0, length), 32);
    }

    private static void FixChecksum(Span<byte> bytes, int checksumIndex)
    {
        bytes[checksumIndex] = 0;
        int sum = 0;
        foreach (byte value in bytes)
            sum += value;
        bytes[checksumIndex] = unchecked((byte)-sum);
    }
}
