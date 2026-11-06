using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static partial class M01InstructionPublicationSerializerGeneratorTests
{
}

internal static partial class M01InstructionPublicationSerializerGeneratorTests
{
    private static partial class IndependentCoffParser
    {
        private const int CoffHeaderSize = 20;
        private const int SectionHeaderSize = 40;
        private const int RelocationSize = 10;
        private const int SymbolSize = 18;
        private const int MaxSections = 32;
        private const int MaxRelocationsPerSection = 4096;
        private const int MaxSymbols = 4096;
        public static ParsedCoff Parse(byte[] bytes)
        {
            if (bytes is null)
            {
                throw new CoffFormatException("null-object");
            }

            ReadOnlySpan<byte> data = bytes;
            RequireRange(data.Length, 0, CoffHeaderSize, "header");
            ushort machine = ReadU16(data, 0, "machine");
            ushort sectionCount = ReadU16(data, 2, "section-count");
            uint timestamp = ReadU32(data, 4, "timestamp");
            uint symbolTablePointer = ReadU32(data, 8, "symbol-pointer");
            uint symbolCount = ReadU32(data, 12, "symbol-count");
            ushort optionalHeaderSize = ReadU16(data, 16, "optional-header-size");
            ushort characteristics = ReadU16(data, 18, "characteristics");
            if (sectionCount == 0 || sectionCount > MaxSections)
            {
                throw new CoffFormatException("section-count-capacity");
            }

            if (symbolCount == 0 || symbolCount > MaxSymbols)
            {
                throw new CoffFormatException("symbol-count-capacity");
            }

            int sectionTableStart = CheckedAdd(CoffHeaderSize, optionalHeaderSize, "section-table-start");
            int sectionTableSize = CheckedMultiply(sectionCount, SectionHeaderSize, "section-table-size");
            RequireRange(data.Length, sectionTableStart, sectionTableSize, "section-table");
            int symbolTableStart = CheckedUIntToInt(symbolTablePointer, "symbol-table-pointer");
            int symbolTableSize = CheckedMultiply(CheckedUIntToInt(symbolCount, "symbol-count"), SymbolSize, "symbol-table-size");
            RequireRange(data.Length, symbolTableStart, symbolTableSize, "symbol-table");
            int stringTableStart = CheckedAdd(symbolTableStart, symbolTableSize, "string-table-start");
            RequireRange(data.Length, stringTableStart, 4, "string-table-prefix");
            uint stringTableSizeValue = ReadU32(data, stringTableStart, "string-table-size");
            int stringTableSize = CheckedUIntToInt(stringTableSizeValue, "string-table-size");
            if (stringTableSize < 4)
            {
                throw new CoffFormatException("string-table-too-small");
            }

            RequireRange(data.Length, stringTableStart, stringTableSize, "string-table");
            int stringTableEnd = CheckedAdd(stringTableStart, stringTableSize, "string-table-end");
            if (stringTableEnd != data.Length)
            {
                throw new CoffFormatException("trailing-or-missing-data");
            }

            ReadOnlySpan<byte> stringTable = data.Slice(stringTableStart, stringTableSize);
            List<ParsedSection> sections = new(sectionCount);
            List<RangeRecord> occupied = [new RangeRecord(0, CheckedAdd(sectionTableStart, sectionTableSize, "headers-end"), "headers"), new RangeRecord(symbolTableStart, stringTableStart, "symbols"), new RangeRecord(stringTableStart, stringTableEnd, "strings")];
            for (int index = 0; index < sectionCount; index++)
            {
                int offset = CheckedAdd(sectionTableStart, CheckedMultiply(index, SectionHeaderSize, "section-offset"), "section-offset");
                ReadOnlySpan<byte> header = data.Slice(offset, SectionHeaderSize);
                string name = DecodeName(header.Slice(0, 8), stringTable, allowSlashDecimal: true);
                uint virtualSize = ReadU32(header, 8, "section-virtual-size");
                uint virtualAddress = ReadU32(header, 12, "section-virtual-address");
                uint rawSizeValue = ReadU32(header, 16, "section-raw-size");
                uint rawPointerValue = ReadU32(header, 20, "section-raw-pointer");
                uint relocationPointerValue = ReadU32(header, 24, "section-relocation-pointer");
                uint linePointer = ReadU32(header, 28, "section-line-pointer");
                ushort relocationCount = ReadU16(header, 32, "section-relocation-count");
                ushort lineCount = ReadU16(header, 34, "section-line-count");
                uint sectionCharacteristics = ReadU32(header, 36, "section-characteristics");
                if (rawSizeValue == 0 || rawPointerValue == 0)
                {
                    throw new CoffFormatException("section-not-file-backed");
                }

                int rawSize = CheckedUIntToInt(rawSizeValue, "section-raw-size");
                int rawPointer = CheckedUIntToInt(rawPointerValue, "section-raw-pointer");
                RequireRange(data.Length, rawPointer, rawSize, "section-raw");
                occupied.Add(new RangeRecord(rawPointer, CheckedAdd(rawPointer, rawSize, "section-raw-end"), "section-raw"));
                if (relocationCount > MaxRelocationsPerSection)
                {
                    throw new CoffFormatException("relocation-count-capacity");
                }

                List<ParsedRelocation> relocations = new(relocationCount);
                if (relocationCount == 0)
                {
                    if (relocationPointerValue != 0)
                    {
                        throw new CoffFormatException("empty-relocation-pointer");
                    }
                }
                else
                {
                    int relocationPointer = CheckedUIntToInt(relocationPointerValue, "relocation-pointer");
                    int relocationBytes = CheckedMultiply(relocationCount, RelocationSize, "relocation-table-size");
                    RequireRange(data.Length, relocationPointer, relocationBytes, "relocations");
                    occupied.Add(new RangeRecord(relocationPointer, CheckedAdd(relocationPointer, relocationBytes, "relocations-end"), "relocations"));
                    for (int relocationIndex = 0; relocationIndex < relocationCount; relocationIndex++)
                    {
                        int relocationOffset = CheckedAdd(relocationPointer, CheckedMultiply(relocationIndex, RelocationSize, "relocation-offset"), "relocation-offset");
                        uint address = ReadU32(data, relocationOffset, "relocation-address");
                        uint symbolIndex = ReadU32(data, relocationOffset + 4, "relocation-symbol");
                        ushort type = ReadU16(data, relocationOffset + 8, "relocation-type");
                        if (symbolIndex >= symbolCount)
                        {
                            throw new CoffFormatException("relocation-symbol-out-of-range");
                        }

                        relocations.Add(new ParsedRelocation(address, symbolIndex, type));
                    }
                }

                sections.Add(new ParsedSection(name, virtualSize, virtualAddress, rawSizeValue, rawPointerValue, relocationPointerValue, linePointer, relocationCount, lineCount, sectionCharacteristics, data.Slice(rawPointer, rawSize).ToArray(), relocations));
            }

            ValidateNonOverlapping(occupied);
            List<ParsedSymbol> symbols = new(CheckedUIntToInt(symbolCount, "symbols-capacity"));
            int symbolIndexValue = 0;
            while (symbolIndexValue < symbolCount)
            {
                int recordOffset = CheckedAdd(symbolTableStart, CheckedMultiply(symbolIndexValue, SymbolSize, "symbol-offset"), "symbol-offset");
                ReadOnlySpan<byte> record = data.Slice(recordOffset, SymbolSize);
                string name = DecodeName(record.Slice(0, 8), stringTable, allowSlashDecimal: false);
                uint nameOffset = ReadU32(record, 0, "symbol-name-zero") == 0 ? ReadU32(record, 4, "symbol-name-offset") : 0;
                uint value = ReadU32(record, 8, "symbol-value");
                short sectionNumber = ReadI16(record, 12, "symbol-section");
                ushort type = ReadU16(record, 14, "symbol-type");
                byte storageClass = record[16];
                byte auxiliaryCount = record[17];
                int nextSymbol = CheckedAdd(symbolIndexValue, CheckedAdd(1, auxiliaryCount, "symbol-aux-count"), "next-symbol");
                if (nextSymbol > symbolCount)
                {
                    throw new CoffFormatException("symbol-aux-out-of-range");
                }

                symbols.Add(new ParsedSymbol(name, nameOffset, value, sectionNumber, type, storageClass, auxiliaryCount));
                symbolIndexValue = nextSymbol;
            }

            return new ParsedCoff(machine, sectionCount, timestamp, symbolTablePointer, symbolCount, optionalHeaderSize, characteristics, sections, symbols, stringTable.ToArray());
        }

        private static string DecodeName(ReadOnlySpan<byte> nameBytes, ReadOnlySpan<byte> stringTable, bool allowSlashDecimal)
        {
            if (nameBytes.Length != 8)
            {
                throw new CoffFormatException("name-width");
            }

            if (ReadU32(nameBytes, 0, "name-zero") == 0)
            {
                uint offset = ReadU32(nameBytes, 4, "name-offset");
                return DecodeStringTableName(stringTable, offset);
            }

            if (allowSlashDecimal && nameBytes[0] == (byte)'/')
            {
                int terminator = nameBytes.IndexOf((byte)0);
                ReadOnlySpan<byte> decimalBytes = terminator < 0 ? nameBytes.Slice(1) : nameBytes.Slice(1, terminator - 1);
                if (decimalBytes.IsEmpty)
                {
                    throw new CoffFormatException("section-name-offset-empty");
                }

                uint offset = 0;
                foreach (byte digit in decimalBytes)
                {
                    if (digit < (byte)'0' || digit > (byte)'9')
                    {
                        throw new CoffFormatException("section-name-offset-invalid");
                    }

                    try
                    {
                        offset = checked(offset * 10 + (uint)(digit - (byte)'0'));
                    }
                    catch (OverflowException)
                    {
                        throw new CoffFormatException("section-name-offset-overflow");
                    }
                }

                return DecodeStringTableName(stringTable, offset);
            }

            int shortTerminator = nameBytes.IndexOf((byte)0);
            ReadOnlySpan<byte> shortName = shortTerminator < 0 ? nameBytes : nameBytes.Slice(0, shortTerminator);
            if (shortName.IsEmpty)
            {
                throw new CoffFormatException("short-name-empty");
            }

            return DecodeAscii(shortName, "short-name");
        }

        private static string DecodeStringTableName(ReadOnlySpan<byte> stringTable, uint offsetValue)
        {
            int offset = CheckedUIntToInt(offsetValue, "string-name-offset");
            if (offset < 4 || offset >= stringTable.Length)
            {
                throw new CoffFormatException("string-name-offset-out-of-range");
            }

            ReadOnlySpan<byte> tail = stringTable.Slice(offset);
            int terminator = tail.IndexOf((byte)0);
            if (terminator <= 0)
            {
                throw new CoffFormatException("string-name-unterminated-or-empty");
            }

            return DecodeAscii(tail.Slice(0, terminator), "string-name");
        }

        private static string DecodeAscii(ReadOnlySpan<byte> bytes, string code)
        {
            foreach (byte value in bytes)
            {
                if (value < 0x21 || value > 0x7E)
                {
                    throw new CoffFormatException(code + "-non-ascii");
                }
            }

            return Encoding.ASCII.GetString(bytes);
        }

        private static void ValidateNonOverlapping(List<RangeRecord> ranges)
        {
            RangeRecord[] ordered = ranges.OrderBy(range => range.Start).ThenBy(range => range.End).ToArray();
            for (int index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].Start < ordered[index - 1].End)
                {
                    throw new CoffFormatException("range-overlap-" + ordered[index - 1].Kind + "-" + ordered[index].Kind);
                }
            }
        }

        private static ushort ReadU16(ReadOnlySpan<byte> bytes, int offset, string code)
        {
            RequireRange(bytes.Length, offset, sizeof(ushort), code);
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));
        }

        private static short ReadI16(ReadOnlySpan<byte> bytes, int offset, string code)
        {
            RequireRange(bytes.Length, offset, sizeof(short), code);
            return BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(offset, sizeof(short)));
        }

        private static uint ReadU32(ReadOnlySpan<byte> bytes, int offset, string code)
        {
            RequireRange(bytes.Length, offset, sizeof(uint), code);
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));
        }

        private static int CheckedUIntToInt(uint value, string code)
        {
            if (value > int.MaxValue)
            {
                throw new CoffFormatException(code + "-overflow");
            }

            return (int)value;
        }

        private static int CheckedAdd(int left, int right, string code)
        {
            try
            {
                return checked(left + right);
            }
            catch (OverflowException)
            {
                throw new CoffFormatException(code + "-overflow");
            }
        }

        private static int CheckedMultiply(int left, int right, string code)
        {
            try
            {
                return checked(left * right);
            }
            catch (OverflowException)
            {
                throw new CoffFormatException(code + "-overflow");
            }
        }

        private static void RequireRange(int length, int offset, int count, string code)
        {
            if (offset < 0 || count < 0 || offset > length || count > length - offset)
            {
                throw new CoffFormatException(code + "-out-of-range");
            }
        }
    }
}
