namespace OS.Kernel.Platform;

public enum OwnedAcpiDirectorySelectionStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidStorage = 2,
    InvalidDirectory = 3,
    InvalidRsdp = 4,
    InvalidTable = 5,
    InvalidRoot = 6
}

/// <summary>Metadata into the caller's retained bytes, never a physical-memory view.</summary>
public readonly struct OwnedAcpiSelectedTable
{
    public readonly ulong PhysicalAddress;
    public readonly ulong Offset;
    public readonly ulong Length;
    public readonly ulong RootIndex;

    internal OwnedAcpiSelectedTable(ulong physicalAddress, ulong offset, ulong length, ulong rootIndex)
    {
        PhysicalAddress = physicalAddress;
        Offset = offset;
        Length = length;
        RootIndex = rootIndex;
    }
}

public readonly struct OwnedAcpiDirectorySelectionResult
{
    public readonly OwnedAcpiDirectorySelectionStatus Status;
    public readonly OwnedAcpiSelectedTable Mcfg;
    public readonly OwnedAcpiSelectedTable Madt;
    public readonly OwnedAcpiSelectedTable Hpet;
    public bool Succeeded => Status == OwnedAcpiDirectorySelectionStatus.Success;

    internal OwnedAcpiDirectorySelectionResult(OwnedAcpiDirectorySelectionStatus status)
    {
        Status = status;
        Mcfg = default;
        Madt = default;
        Hpet = default;
    }

    internal OwnedAcpiDirectorySelectionResult(
        OwnedAcpiSelectedTable mcfg, OwnedAcpiSelectedTable madt, OwnedAcpiSelectedTable hpet)
    {
        Status = OwnedAcpiDirectorySelectionStatus.Success;
        Mcfg = mcfg;
        Madt = madt;
        Hpet = hpet;
    }
}

/// <summary>
/// Validates the five retained M03 rows: RSDP, root, MCFG, APIC and HPET.
/// The caller guarantees readable, stable storage and its original retained lifetime.
/// Embedded physical addresses are checked as numbers and are never dereferenced.
/// Omitted firmware children and device programming are outside this contract.
/// </summary>
public static unsafe class OwnedACPIDirectorySelectMADTHPETMCFG
{
    public const uint RequiredDescriptorWords = 30;
    public const ulong MaximumOwnedBytes = 266240;
    private const ulong DescriptorBytes = RequiredDescriptorWords * sizeof(ulong);
    private const ulong RsdpSignature = 0x2052545020445352;
    private const uint RsdtSignature = 0x54445352;
    private const uint XsdtSignature = 0x54445358;
    private const uint McfgSignature = 0x4746434D;
    private const uint MadtSignature = 0x43495041;
    private const uint HpetSignature = 0x54455048;

    /// <param name="owned">First RSDP byte, after the M03 canary prefix.</param>
    /// <param name="ownedBytes">Exact retained TotalBytes, not buffer capacity.</param>
    /// <param name="descriptors">Five rows of physical, signature, length, root index, offset, compared bytes.</param>
    public static OwnedAcpiDirectorySelectionResult Select(
        byte* owned, ulong ownedBytes, ulong* descriptors, uint descriptorWords)
    {
        ulong storage = (ulong)owned, directory = (ulong)descriptors;
        if (descriptorWords != RequiredDescriptorWords || ownedBytes > MaximumOwnedBytes ||
            !ValidRange(storage, ownedBytes) || !ValidRange(directory, DescriptorBytes) ||
            (directory & 7) != 0 || Overlaps(storage, ownedBytes, directory, DescriptorBytes))
            return new(OwnedAcpiDirectorySelectionStatus.InvalidStorage);

        // Admit every row and the entire byte envelope before parsing any table.
        ulong offset = 0;
        for (int role = 0; role < 5; role++)
        {
            ulong* row = descriptors + role * 6;
            ulong length = row[2];
            if (length < (role == 0 ? 20UL : 36UL) || length > (role == 0 ? 4096UL : 65536UL) ||
                row[4] != offset || length > ownedBytes - offset || row[5] != length ||
                !ValidRange(row[0], length) || Overlaps(row[0], length, storage, ownedBytes) ||
                Overlaps(row[0], length, directory, DescriptorBytes))
                return new(OwnedAcpiDirectorySelectionStatus.InvalidDirectory);
            for (int earlier = 0; earlier < role; earlier++)
                if (Overlaps(row[0], length, descriptors[earlier * 6], descriptors[earlier * 6 + 2]))
                    return new(OwnedAcpiDirectorySelectionStatus.InvalidDirectory);
            offset += length;
        }
        if (offset != ownedBytes)
            return new(OwnedAcpiDirectorySelectionStatus.InvalidDirectory);

        ulong rsdpLength = descriptors[2];
        byte revision = owned[15];
        if (descriptors[1] != RsdpSignature || descriptors[3] != ~0UL ||
            OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(owned) != RsdpSignature ||
            (owned[9] | owned[10] | owned[11] | owned[12] | owned[13] | owned[14]) == 0 ||
            revision == 1 || !ChecksumValid(owned, 20) ||
            (revision == 0 ? rsdpLength != 20 : rsdpLength < 36 ||
                OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(owned + 20) != rsdpLength ||
                !ChecksumValid(owned, rsdpLength)))
            return new(OwnedAcpiDirectorySelectionStatus.InvalidRsdp);

        ulong xsdt = revision >= 2 ? OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(owned + 24) : 0;
        ulong rootPhysical = xsdt != 0 ? xsdt : OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(owned + 16);
        uint rootSignature = xsdt != 0 ? XsdtSignature : RsdtSignature;
        if (descriptors[6] != rootPhysical || descriptors[9] != ~0UL)
            return new(OwnedAcpiDirectorySelectionStatus.InvalidRoot);

        for (int role = 1; role < 5; role++)
        {
            ulong* row = descriptors + role * 6;
            uint signature = role == 1 ? rootSignature : role == 2 ? McfgSignature : role == 3 ? MadtSignature : HpetSignature;
            byte* table = owned + row[4];
            if (row[1] != signature || OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(table) != signature ||
                OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(table + 4) != row[2] || !ChecksumValid(table, row[2]))
                return new(OwnedAcpiDirectorySelectionStatus.InvalidTable);
        }

        int width = xsdt != 0 ? 8 : 4;
        ulong pointerBytes = descriptors[8] - 36;
        ulong pointerCount = pointerBytes / (ulong)width;
        if (pointerBytes % (ulong)width != 0 || pointerCount == 0 || pointerCount > 256)
            return new(OwnedAcpiDirectorySelectionStatus.InvalidRoot);
        byte* pointers = owned + descriptors[10] + 36;
        for (ulong i = 0; i < pointerCount; i++)
        {
            ulong physical = RootPointer(pointers, i, width);
            if (!ValidRange(physical, 36) || Overlaps(physical, 36, storage, ownedBytes) ||
                Overlaps(physical, 36, directory, DescriptorBytes))
                return new(OwnedAcpiDirectorySelectionStatus.InvalidRoot);
            for (ulong earlier = 0; earlier < i; earlier++)
                if (Overlaps(physical, 36, RootPointer(pointers, earlier, width), 36))
                    return new(OwnedAcpiDirectorySelectionStatus.InvalidRoot);
            for (int role = 0; role < 5; role++)
            {
                ulong* row = descriptors + role * 6;
                if (role >= 2 && row[3] == i && row[0] == physical) continue;
                if (Overlaps(physical, 36, row[0], row[2]))
                    return new(OwnedAcpiDirectorySelectionStatus.InvalidRoot);
            }
        }
        for (int role = 2; role < 5; role++)
        {
            ulong* row = descriptors + role * 6;
            if (row[3] >= pointerCount || row[0] != RootPointer(pointers, row[3], width))
                return new(OwnedAcpiDirectorySelectionStatus.InvalidRoot);
        }

        return new(Selected(descriptors + 12), Selected(descriptors + 18), Selected(descriptors + 24));
    }

    private static OwnedAcpiSelectedTable Selected(ulong* row) => new(row[0], row[4], row[2], row[3]);

    private static ulong RootPointer(byte* pointers, ulong index, int width) => width == 8
        ? OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(pointers + index * 8)
        : OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(pointers + index * 4);

    private static bool ValidRange(ulong start, ulong length) =>
        start != 0 && length != 0 && length <= ~0UL - start;

    // Both complete ranges have already passed ValidRange.
    private static bool Overlaps(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;

    private static bool ChecksumValid(byte* bytes, ulong length)
    {
        uint sum = 0;
        for (ulong i = 0; i < length; i++) sum += bytes[i];
        return (sum & 255) == 0;
    }
}
