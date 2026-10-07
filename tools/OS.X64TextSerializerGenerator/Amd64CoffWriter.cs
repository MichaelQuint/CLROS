using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;

public enum CoffWriteError
{
    None = 0,
    InvalidConfiguration,
    UnsupportedMachine,
    NonDeterministicTimestamp,
    InvalidSectionCount,
    InvalidSymbolCount,
    InvalidName,
    EmptySection,
    InvalidSectionCharacteristics,
    DuplicateSection,
    InvalidSymbol,
    DuplicateSymbol,
    InvalidRelocation,
    CapacityExceeded,
    ArithmeticOverflow,
    InternalInvariant,
}

public readonly record struct CoffRelocationDefinition(
    uint Offset,
    uint SymbolIndex,
    ushort Type);

public readonly record struct CoffSectionDefinition(
    string? Name,
    byte[]? RawData,
    uint Characteristics,
    CoffRelocationDefinition[]? Relocations);

public readonly record struct CoffSymbolDefinition(
    string? Name,
    uint Value,
    short SectionNumber,
    ushort Type,
    byte StorageClass);

public readonly record struct Amd64CoffObjectDefinition(
    ushort Machine,
    uint TimeDateStamp,
    CoffSectionDefinition[]? Sections,
    CoffSymbolDefinition[]? Symbols);

/// <summary>
/// Writes a deliberately small, deterministic subset of AMD64 COFF.
/// </summary>
/// <remarks>
/// This writer has no optional header, line numbers, auxiliary symbol records,
/// or relocation-overflow records. Raw-data and nonempty relocation-table
/// starts receive deterministic four-byte zero padding. Every section is file
/// backed. By default, only AMD64 image-relative 32-bit relocations and
/// section-defined symbols are admitted. An explicit opt-in also admits REL32
/// from code to undefined external functions (section/value zero, type 0x20,
/// class 2). A separate explicit opt-in admits undefined external data (type zero)
/// for RIP-relative address materialization. Neither admits common/absolute symbols or REL32_n variants.
/// Each REL32 occupies four file-backed bytes and cannot overlap another fixup.
/// Validation and sizing complete before the destination is modified.
/// </remarks>
public static class Amd64CoffWriter
{
    public const ushort Amd64Machine = 0x8664;
    public const ushort ImageRelAmd64Addr32Nb = 0x0003;
    public const ushort ImageRelAmd64Rel32 = 0x0004;
    public const ushort ImageSymTypeFunction = 0x0020;

    public const byte ImageSymClassExternal = 0x02;
    public const byte ImageSymClassStatic = 0x03;

    public const int FileHeaderSize = 20;
    public const int SectionHeaderSize = 40;
    public const int RelocationSize = 10;
    public const int SymbolSize = 18;
    public const int MaximumObjectSizeBytes = 16 * 1024 * 1024;

    private const uint ImageScnCntCode = 0x00000020;
    private const uint ImageScnCntInitializedData = 0x00000040;
    private const uint ImageScnCntUninitializedData = 0x00000080;
    private const uint ImageScnAlignmentMask = 0x00F00000;
    private const uint SmallestSupportedAlignment = 0x00100000;
    private const uint LargestSupportedAlignment = 0x00E00000;
    private const uint ImageScnMemExecute = 0x20000000;
    private const uint ImageScnMemRead = 0x40000000;
    private const uint ImageScnMemWrite = 0x80000000;
    private const uint AllowedSectionFlags =
        ImageScnCntCode |
        ImageScnCntInitializedData |
        ImageScnAlignmentMask |
        ImageScnMemExecute |
        ImageScnMemRead;

    public static bool TryGetRequiredSize(
        in Amd64CoffObjectDefinition definition,
        out int requiredSize,
        out CoffWriteError error)
        => TryGetRequiredSize(in definition, false, out requiredSize, out error);

    public static bool TryGetRequiredSize(
        in Amd64CoffObjectDefinition definition,
        bool allowRel32ExternalFunctions,
        out int requiredSize,
        out CoffWriteError error)
        => TryGetRequiredSize(in definition, allowRel32ExternalFunctions, false, out requiredSize, out error);

    public static bool TryGetRequiredSize(
        in Amd64CoffObjectDefinition definition,
        bool allowRel32ExternalFunctions,
        bool allowRel32ExternalData,
        out int requiredSize,
        out CoffWriteError error)
    {
        if (!TryBuildPlan(in definition, allowRel32ExternalFunctions, allowRel32ExternalData, out CoffWritePlan? plan, out error))
        {
            requiredSize = 0;
            return false;
        }

        requiredSize = plan!.TotalSize;
        return true;
    }

    public static bool TryWrite(
        in Amd64CoffObjectDefinition definition,
        Span<byte> destination,
        out int bytesWritten,
        out CoffWriteError error)
        => TryWrite(in definition, destination, false, out bytesWritten, out error);

    public static bool TryWrite(
        in Amd64CoffObjectDefinition definition,
        Span<byte> destination,
        bool allowRel32ExternalFunctions,
        out int bytesWritten,
        out CoffWriteError error)
        => TryWrite(in definition, destination, allowRel32ExternalFunctions, false, out bytesWritten, out error);

    public static bool TryWrite(
        in Amd64CoffObjectDefinition definition,
        Span<byte> destination,
        bool allowRel32ExternalFunctions,
        bool allowRel32ExternalData,
        out int bytesWritten,
        out CoffWriteError error)
    {
        bytesWritten = 0;

        if (!TryBuildPlan(in definition, allowRel32ExternalFunctions, allowRel32ExternalData, out CoffWritePlan? plan, out error))
        {
            return false;
        }

        if (destination.Length < plan!.TotalSize)
        {
            error = CoffWriteError.CapacityExceeded;
            return false;
        }

        byte[] completedObject = new byte[plan.TotalSize];
        if (!TryWriteValidatedObject(in definition, plan, completedObject, out error))
        {
            return false;
        }

        completedObject.CopyTo(destination);
        bytesWritten = completedObject.Length;
        error = CoffWriteError.None;
        return true;
    }

    public static string GetStableErrorCode(CoffWriteError error) =>
        error switch
        {
            CoffWriteError.None => "none",
            CoffWriteError.InvalidConfiguration => "invalid_configuration",
            CoffWriteError.UnsupportedMachine => "unsupported_machine",
            CoffWriteError.NonDeterministicTimestamp => "nonzero_timestamp",
            CoffWriteError.InvalidSectionCount => "invalid_section_count",
            CoffWriteError.InvalidSymbolCount => "invalid_symbol_count",
            CoffWriteError.InvalidName => "invalid_name",
            CoffWriteError.EmptySection => "empty_section",
            CoffWriteError.InvalidSectionCharacteristics => "invalid_section_characteristics",
            CoffWriteError.DuplicateSection => "duplicate_section",
            CoffWriteError.InvalidSymbol => "invalid_symbol",
            CoffWriteError.DuplicateSymbol => "duplicate_symbol",
            CoffWriteError.InvalidRelocation => "invalid_relocation",
            CoffWriteError.CapacityExceeded => "capacity_exceeded",
            CoffWriteError.ArithmeticOverflow => "arithmetic_overflow",
            CoffWriteError.InternalInvariant => "internal_invariant",
            _ => "unknown",
        };

    private static bool TryBuildPlan(
        in Amd64CoffObjectDefinition definition,
        bool allowRel32ExternalFunctions,
        bool allowRel32ExternalData,
        out CoffWritePlan? plan,
        out CoffWriteError error)
    {
        plan = null;
        bool allowRel32 = allowRel32ExternalFunctions || allowRel32ExternalData;

        if (definition.Machine != Amd64Machine)
        {
            error = CoffWriteError.UnsupportedMachine;
            return false;
        }

        if (definition.TimeDateStamp != 0)
        {
            error = CoffWriteError.NonDeterministicTimestamp;
            return false;
        }

        CoffSectionDefinition[]? sections = definition.Sections;
        if (sections is null || sections.Length == 0 || sections.Length > short.MaxValue)
        {
            error = CoffWriteError.InvalidSectionCount;
            return false;
        }

        CoffSymbolDefinition[]? symbols = definition.Symbols;
        if (symbols is null || symbols.Length == 0)
        {
            error = CoffWriteError.InvalidSymbolCount;
            return false;
        }

        var longNameOffsets = new Dictionary<string, uint>(StringComparer.Ordinal);
        var longNames = new List<string>();
        var sectionNames = new HashSet<string>(StringComparer.Ordinal);
        long stringTableSize = sizeof(uint);

        for (int index = 0; index < sections.Length; index++)
        {
            ref readonly CoffSectionDefinition section = ref sections[index];
            if (!TryValidateName(section.Name, isSectionName: true))
            {
                error = CoffWriteError.InvalidName;
                return false;
            }

            if (!sectionNames.Add(section.Name!))
            {
                error = CoffWriteError.DuplicateSection;
                return false;
            }

            if (section.RawData is null || section.RawData.Length == 0)
            {
                error = CoffWriteError.EmptySection;
                return false;
            }

            if (!HasValidCharacteristics(section.Characteristics))
            {
                error = CoffWriteError.InvalidSectionCharacteristics;
                return false;
            }

            if (section.Relocations is null)
            {
                error = CoffWriteError.InvalidConfiguration;
                return false;
            }

            if (section.Relocations.Length > ushort.MaxValue)
            {
                error = CoffWriteError.InvalidRelocation;
                return false;
            }

            var offsets = new HashSet<uint>();
            var fixupBytes = allowRel32 ? new HashSet<uint>() : null;
            var rel32Bytes = allowRel32 ? new HashSet<uint>() : null;
            foreach (CoffRelocationDefinition relocation in section.Relocations)
            {
                bool rel32 = relocation.Type == ImageRelAmd64Rel32;
                if ((relocation.Type != ImageRelAmd64Addr32Nb &&
                     !(allowRel32 && rel32)) ||
                    relocation.SymbolIndex >= (uint)symbols.Length ||
                    relocation.Offset > (uint)section.RawData.Length ||
                    (uint)section.RawData.Length - relocation.Offset < sizeof(uint) ||
                    !offsets.Add(relocation.Offset))
                {
                    error = CoffWriteError.InvalidRelocation;
                    return false;
                }

                ref readonly CoffSymbolDefinition target = ref symbols[relocation.SymbolIndex];
                if ((rel32 && ((section.Characteristics & ImageScnCntCode) == 0 ||
                               !(allowRel32ExternalFunctions && IsUndefinedExternalFunction(in target)) &&
                               !(allowRel32ExternalData && IsUndefinedExternalData(in target)))) ||
                    (allowRel32 && !rel32 && target.SectionNumber == 0))
                {
                    error = CoffWriteError.InvalidRelocation;
                    return false;
                }

                if (allowRel32)
                {
                    for (uint indexInFixup = 0; indexInFixup < sizeof(uint); indexInFixup++)
                    {
                        uint position = relocation.Offset + indexInFixup;
                        if ((rel32 && fixupBytes!.Contains(position)) || rel32Bytes!.Contains(position))
                        {
                            error = CoffWriteError.InvalidRelocation;
                            return false;
                        }

                        fixupBytes!.Add(position);
                        if (rel32)
                        {
                            rel32Bytes!.Add(position);
                        }
                    }
                }
            }

            if (!TryAddLongName(
                    section.Name!,
                    longNameOffsets,
                    longNames,
                    ref stringTableSize,
                    sectionName: true,
                    out error))
            {
                return false;
            }
        }

        var symbolNames = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < symbols.Length; index++)
        {
            ref readonly CoffSymbolDefinition symbol = ref symbols[index];
            if (!TryValidateName(symbol.Name, isSectionName: false))
            {
                error = CoffWriteError.InvalidName;
                return false;
            }

            if (!symbolNames.Add(symbol.Name!))
            {
                error = CoffWriteError.DuplicateSymbol;
                return false;
            }

            bool externalSymbol = (allowRel32ExternalFunctions && IsUndefinedExternalFunction(in symbol)) ||
                (allowRel32ExternalData && IsUndefinedExternalData(in symbol));
            if (!externalSymbol && (symbol.SectionNumber <= 0 ||
                symbol.SectionNumber > sections.Length ||
                (symbol.StorageClass != ImageSymClassExternal &&
                 symbol.StorageClass != ImageSymClassStatic)))
            {
                error = CoffWriteError.InvalidSymbol;
                return false;
            }

            if (!TryAddLongName(
                    symbol.Name!,
                    longNameOffsets,
                    longNames,
                    ref stringTableSize,
                    sectionName: false,
                    out error))
            {
                return false;
            }
        }

        try
        {
            long cursor = checked(FileHeaderSize + ((long)sections.Length * SectionHeaderSize));
            var sectionLayouts = new CoffSectionLayout[sections.Length];

            for (int index = 0; index < sections.Length; index++)
            {
                cursor = AlignToFourBytes(cursor);
                sectionLayouts[index] = sectionLayouts[index] with
                {
                    RawDataPointer = checked((uint)cursor),
                };
                cursor = checked(cursor + sections[index].RawData!.Length);
            }

            for (int index = 0; index < sections.Length; index++)
            {
                int relocationCount = sections[index].Relocations!.Length;
                if (relocationCount != 0)
                {
                    cursor = AlignToFourBytes(cursor);
                }

                sectionLayouts[index] = sectionLayouts[index] with
                {
                    RelocationPointer = relocationCount == 0
                        ? 0
                        : checked((uint)cursor),
                };
                cursor = checked(cursor + ((long)relocationCount * RelocationSize));
            }

            uint symbolTablePointer = checked((uint)cursor);
            cursor = checked(cursor + ((long)symbols.Length * SymbolSize));
            cursor = checked(cursor + stringTableSize);

            if (cursor > MaximumObjectSizeBytes)
            {
                error = CoffWriteError.CapacityExceeded;
                return false;
            }

            plan = new CoffWritePlan(
                checked((int)cursor),
                checked((uint)stringTableSize),
                symbolTablePointer,
                sectionLayouts,
                longNameOffsets,
                longNames);
            error = CoffWriteError.None;
            return true;
        }
        catch (OverflowException)
        {
            error = CoffWriteError.ArithmeticOverflow;
            return false;
        }
    }

    private static bool TryWriteValidatedObject(
        in Amd64CoffObjectDefinition definition,
        CoffWritePlan plan,
        Span<byte> output,
        out CoffWriteError error)
    {
        CoffSectionDefinition[] sections = definition.Sections!;
        CoffSymbolDefinition[] symbols = definition.Symbols!;
        int cursor = 0;

        WriteUInt16(output, ref cursor, definition.Machine);
        WriteUInt16(output, ref cursor, checked((ushort)sections.Length));
        WriteUInt32(output, ref cursor, definition.TimeDateStamp);
        WriteUInt32(output, ref cursor, plan.SymbolTablePointer);
        WriteUInt32(output, ref cursor, checked((uint)symbols.Length));
        WriteUInt16(output, ref cursor, 0);
        WriteUInt16(output, ref cursor, 0);

        for (int index = 0; index < sections.Length; index++)
        {
            ref readonly CoffSectionDefinition section = ref sections[index];
            CoffSectionLayout layout = plan.SectionLayouts[index];

            WriteSectionName(output, ref cursor, section.Name!, plan.LongNameOffsets);
            WriteUInt32(output, ref cursor, 0);
            WriteUInt32(output, ref cursor, 0);
            WriteUInt32(output, ref cursor, checked((uint)section.RawData!.Length));
            WriteUInt32(output, ref cursor, layout.RawDataPointer);
            WriteUInt32(output, ref cursor, layout.RelocationPointer);
            WriteUInt32(output, ref cursor, 0);
            WriteUInt16(output, ref cursor, checked((ushort)section.Relocations!.Length));
            WriteUInt16(output, ref cursor, 0);
            WriteUInt32(output, ref cursor, section.Characteristics);
        }

        for (int index = 0; index < sections.Length; index++)
        {
            byte[] rawData = sections[index].RawData!;
            cursor = checked((int)plan.SectionLayouts[index].RawDataPointer);
            rawData.CopyTo(output[cursor..]);
            cursor += rawData.Length;
        }

        for (int index = 0; index < sections.Length; index++)
        {
            if (sections[index].Relocations!.Length != 0)
            {
                cursor = checked((int)plan.SectionLayouts[index].RelocationPointer);
            }

            foreach (CoffRelocationDefinition relocation in sections[index].Relocations!)
            {
                WriteUInt32(output, ref cursor, relocation.Offset);
                WriteUInt32(output, ref cursor, relocation.SymbolIndex);
                WriteUInt16(output, ref cursor, relocation.Type);
            }
        }

        cursor = checked((int)plan.SymbolTablePointer);
        foreach (CoffSymbolDefinition symbol in symbols)
        {
            WriteSymbolName(output, ref cursor, symbol.Name!, plan.LongNameOffsets);
            WriteUInt32(output, ref cursor, symbol.Value);
            WriteUInt16(output, ref cursor, checked((ushort)symbol.SectionNumber));
            WriteUInt16(output, ref cursor, symbol.Type);
            output[cursor++] = symbol.StorageClass;
            output[cursor++] = 0;
        }

        WriteUInt32(output, ref cursor, plan.StringTableSize);
        foreach (string longName in plan.LongNames)
        {
            WriteAscii(output, ref cursor, longName);
            output[cursor++] = 0;
        }

        if (cursor != output.Length)
        {
            error = CoffWriteError.InternalInvariant;
            return false;
        }

        error = CoffWriteError.None;
        return true;
    }

    private static bool TryAddLongName(
        string name,
        Dictionary<string, uint> offsets,
        List<string> names,
        ref long stringTableSize,
        bool sectionName,
        out CoffWriteError error)
    {
        if (name.Length <= 8 || offsets.ContainsKey(name))
        {
            error = CoffWriteError.None;
            return true;
        }

        try
        {
            uint offset = checked((uint)stringTableSize);
            if (sectionName && offset > 9_999_999)
            {
                error = CoffWriteError.CapacityExceeded;
                return false;
            }

            stringTableSize = checked(stringTableSize + name.Length + 1L);
            if (stringTableSize > MaximumObjectSizeBytes)
            {
                error = CoffWriteError.CapacityExceeded;
                return false;
            }

            offsets.Add(name, offset);
            names.Add(name);
            error = CoffWriteError.None;
            return true;
        }
        catch (OverflowException)
        {
            error = CoffWriteError.ArithmeticOverflow;
            return false;
        }
    }

    private static bool TryValidateName(string? name, bool isSectionName)
    {
        if (string.IsNullOrEmpty(name) ||
            name.Length > 1024 ||
            (isSectionName && name[0] == '/'))
        {
            return false;
        }

        foreach (char character in name)
        {
            if (character is < (char)0x21 or > (char)0x7E)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUndefinedExternalFunction(in CoffSymbolDefinition symbol) =>
        symbol.SectionNumber == 0 && symbol.Value == 0 &&
        symbol.Type == ImageSymTypeFunction && symbol.StorageClass == ImageSymClassExternal;

    private static bool IsUndefinedExternalData(in CoffSymbolDefinition symbol) =>
        symbol.SectionNumber == 0 && symbol.Value == 0 &&
        symbol.Type == 0 && symbol.StorageClass == ImageSymClassExternal;

    private static bool HasValidCharacteristics(uint characteristics)
    {
        if ((characteristics & ~AllowedSectionFlags) != 0 ||
            (characteristics & ImageScnMemRead) == 0 ||
            (characteristics & ImageScnMemWrite) != 0 ||
            (characteristics & ImageScnCntUninitializedData) != 0)
        {
            return false;
        }

        uint alignment = characteristics & ImageScnAlignmentMask;
        if (alignment < SmallestSupportedAlignment ||
            alignment > LargestSupportedAlignment)
        {
            return false;
        }

        bool containsCode = (characteristics & ImageScnCntCode) != 0;
        bool containsInitializedData =
            (characteristics & ImageScnCntInitializedData) != 0;
        bool isExecutable = (characteristics & ImageScnMemExecute) != 0;
        return containsCode != containsInitializedData &&
            isExecutable == containsCode;
    }

    private static long AlignToFourBytes(long value) =>
        checked((value + 3L) & ~3L);

    private static void WriteSectionName(
        Span<byte> output,
        ref int cursor,
        string name,
        IReadOnlyDictionary<string, uint> longNameOffsets)
    {
        if (name.Length <= 8)
        {
            WritePaddedAscii(output, ref cursor, name, 8);
            return;
        }

        string encodedOffset = "/" +
            longNameOffsets[name].ToString(System.Globalization.CultureInfo.InvariantCulture);
        WritePaddedAscii(output, ref cursor, encodedOffset, 8);
    }

    private static void WriteSymbolName(
        Span<byte> output,
        ref int cursor,
        string name,
        IReadOnlyDictionary<string, uint> longNameOffsets)
    {
        if (name.Length <= 8)
        {
            WritePaddedAscii(output, ref cursor, name, 8);
            return;
        }

        WriteUInt32(output, ref cursor, 0);
        WriteUInt32(output, ref cursor, longNameOffsets[name]);
    }

    private static void WritePaddedAscii(
        Span<byte> output,
        ref int cursor,
        string value,
        int width)
    {
        int start = cursor;
        WriteAscii(output, ref cursor, value);
        cursor = start + width;
    }

    private static void WriteAscii(Span<byte> output, ref int cursor, string value)
    {
        foreach (char character in value)
        {
            output[cursor++] = checked((byte)character);
        }
    }

    private static void WriteUInt16(Span<byte> output, ref int cursor, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(output[cursor..], value);
        cursor += sizeof(ushort);
    }

    private static void WriteUInt32(Span<byte> output, ref int cursor, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(output[cursor..], value);
        cursor += sizeof(uint);
    }

    private sealed record CoffWritePlan(
        int TotalSize,
        uint StringTableSize,
        uint SymbolTablePointer,
        CoffSectionLayout[] SectionLayouts,
        IReadOnlyDictionary<string, uint> LongNameOffsets,
        IReadOnlyList<string> LongNames);

    private readonly record struct CoffSectionLayout(
        uint RawDataPointer,
        uint RelocationPointer);
}
