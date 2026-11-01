using OS.Std.Binary;

namespace OS.Boot;
public static class AcpiSdtParser
{
    public const int HeaderLength = 36;
    public const int SignatureOffset = 0;
    public const int LengthOffset = 4;
    public const int RevisionOffset = 8;
    public const int ChecksumOffset = 9;
    public const int OemIdOffset = 10;
    public const int OemIdLength = 6;
    public const int OemTableIdOffset = 16;
    public const int OemTableIdLength = 8;
    public const int OemRevisionOffset = 24;
    public const int CreatorIdOffset = 28;
    public const int CreatorRevisionOffset = 32;
    public const int RsdtPointerSize = 4;
    public const int XsdtPointerSize = 8;
    public const uint RsdtSignature = 0x54445352;
    public const uint XsdtSignature = 0x54445358;
    public static AcpiSdtParseResult TryParsePointerList(ReadOnlySpan<byte> table, Span<ulong> destination)
    {
        if (table.Length < HeaderLength)
        {
            return AcpiSdtParseResult.Fail(AcpiSdtParseStatus.SourceTooShort, requiredLength: HeaderLength);
        }

        AcpiSdtHeader header = ReadHeader(table);
        if (header.TableKind == AcpiSdtTableKind.None)
        {
            return AcpiSdtParseResult.Fail(AcpiSdtParseStatus.UnsupportedSignature, header, header.Length);
        }

        if (header.Length < HeaderLength)
        {
            return AcpiSdtParseResult.Fail(AcpiSdtParseStatus.DeclaredLengthTooSmall, header, header.Length, HeaderLength);
        }

        if (header.Length > table.Length)
        {
            return AcpiSdtParseResult.Fail(AcpiSdtParseStatus.SourceTooShortForDeclaredLength, header, header.Length, header.Length);
        }

        ReadOnlySpan<byte> declaredTable = table.Slice(0, (int)header.Length);
        if (!IsChecksumValid(declaredTable))
        {
            return AcpiSdtParseResult.Fail(AcpiSdtParseStatus.InvalidChecksum, header, header.Length, header.Length);
        }

        int pointerSize = header.TableKind == AcpiSdtTableKind.Rsdt ? RsdtPointerSize : XsdtPointerSize;
        int pointerBytes = (int)header.Length - HeaderLength;
        int trailingBytes = pointerBytes % pointerSize;
        if (trailingBytes != 0)
        {
            uint requiredLength = (uint)(header.Length + (pointerSize - trailingBytes));
            return AcpiSdtParseResult.Fail(AcpiSdtParseStatus.InvalidPointerArrayLength, header, header.Length, requiredLength, entryIndex: pointerBytes / pointerSize);
        }

        int pointerCount = pointerBytes / pointerSize;
        for (int entryIndex = 0; entryIndex < pointerCount; entryIndex++)
        {
            if (ReadPointer(declaredTable, pointerSize, entryIndex) == 0)
            {
                return AcpiSdtParseResult.Fail(AcpiSdtParseStatus.ZeroTablePointer, header, header.Length, header.Length, entryIndex: entryIndex);
            }
        }

        if (destination.Length < pointerCount)
        {
            return AcpiSdtParseResult.Fail(AcpiSdtParseStatus.DestinationTooSmall, header, header.Length, header.Length, requiredPointerCapacity: pointerCount);
        }

        for (int entryIndex = 0; entryIndex < pointerCount; entryIndex++)
        {
            destination[entryIndex] = ReadPointer(declaredTable, pointerSize, entryIndex);
        }

        return AcpiSdtParseResult.Success(header, pointerCount);
    }

    public static AcpiSdtTableKind GetTableKind(uint signature)
    {
        if (signature == RsdtSignature)
        {
            return AcpiSdtTableKind.Rsdt;
        }

        if (signature == XsdtSignature)
        {
            return AcpiSdtTableKind.Xsdt;
        }

        return AcpiSdtTableKind.None;
    }

    private static AcpiSdtHeader ReadHeader(ReadOnlySpan<byte> table)
    {
        return new AcpiSdtHeader(EndianPrimitives.ReadUInt32LittleEndian(table.Slice(SignatureOffset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(table.Slice(LengthOffset, sizeof(uint))), table[RevisionOffset], table[ChecksumOffset], ReadPackedBytes(table.Slice(OemIdOffset, OemIdLength)), ReadPackedBytes(table.Slice(OemTableIdOffset, OemTableIdLength)), EndianPrimitives.ReadUInt32LittleEndian(table.Slice(OemRevisionOffset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(table.Slice(CreatorIdOffset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(table.Slice(CreatorRevisionOffset, sizeof(uint))));
    }

    private static ulong ReadPointer(ReadOnlySpan<byte> declaredTable, int pointerSize, int entryIndex)
    {
        int offset = HeaderLength + (entryIndex * pointerSize);
        if (pointerSize == RsdtPointerSize)
        {
            return EndianPrimitives.ReadUInt32LittleEndian(declaredTable.Slice(offset, sizeof(uint)));
        }

        return EndianPrimitives.ReadUInt64LittleEndian(declaredTable.Slice(offset, sizeof(ulong)));
    }

    private static ulong ReadPackedBytes(ReadOnlySpan<byte> source)
    {
        ulong value = 0;
        for (int index = 0; index < source.Length; index++)
        {
            value |= (ulong)source[index] << (index * 8);
        }

        return value;
    }

    private static bool IsChecksumValid(ReadOnlySpan<byte> source)
    {
        uint sum = 0;
        for (int index = 0; index < source.Length; index++)
        {
            sum += source[index];
        }

        return (sum & 0xff) == 0;
    }
}
