using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;
/// <summary>Bounds-checked, file-backed PE32+ inspection. No image bytes are executed.</summary>

public sealed partial class EfiPeImage
{
    public byte[] Bytes { get; }
    public uint Timestamp { get; }
    public uint EntryPointRva { get; }
    public ulong ImageBase { get; }
    public uint SizeOfImage { get; }
    public string EntrySection { get; }
    public uint ExceptionRva { get; }
    public uint ExceptionSize { get; }
    public List<Section> Sections { get; } = [];
    public List<RuntimeFunction> RuntimeFunctions { get; } = [];
    public uint SerializerRva { get; private set; }
    public uint SerializerUnwindRva { get; private set; }

    public static void Require(bool valid, string reason, string detail = "")
    {
        if (!valid)
            throw new ToolFailure(reason, detail);
    }

    public static void Range(long offset, long count, long length, string reason) => Require(offset >= 0 && count >= 0 && offset <= length && count <= length - offset, reason);
    public ushort U16(long offset, string reason = "truncated_artifact")
    {
        Range(offset, 2, Bytes.Length, reason);
        return BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan((int)offset, 2));
    }

    public uint U32(long offset, string reason = "truncated_artifact")
    {
        Range(offset, 4, Bytes.Length, reason);
        return BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan((int)offset, 4));
    }

    public ulong U64(long offset, string reason = "truncated_artifact")
    {
        Range(offset, 8, Bytes.Length, reason);
        return BinaryPrimitives.ReadUInt64LittleEndian(Bytes.AsSpan((int)offset, 8));
    }

    public byte[] Copy(long offset, int count, string reason)
    {
        Range(offset, count, Bytes.Length, reason);
        return Bytes.AsSpan((int)offset, count).ToArray();
    }

    public static ulong RangeEnd(ulong rva, ulong size, string reason)
    {
        Require(size != 0 && rva < 0x100000000 && size <= 0x100000000 - rva, reason);
        return rva + size;
    }

    public Backing Resolve(ulong rva, ulong size, string missing, string ambiguous)
    {
        ulong end = RangeEnd(rva, size, missing);
        List<Backing> matches = [];
        foreach (Section section in Sections)
        {
            ulong start = section.VirtualAddress;
            if (section.VirtualSize == 0 || section.RawSize == 0 || start + section.VirtualSize > 0x100000000 || start + section.RawSize > 0x100000000 || rva < start || end > start + section.VirtualSize || end > start + section.RawSize)
                continue;
            ulong offset = section.RawOffset + rva - start;
            if (offset > (ulong)Bytes.Length || size > (ulong)Bytes.Length - offset)
                continue;
            matches.Add(new(section, checked((int)offset), checked((int)size)));
        }

        Require(matches.Count != 0, missing, $"rva=0x{rva:x8} size={size}");
        Require(matches.Count == 1, ambiguous, $"rva=0x{rva:x8} size={size} matches={matches.Count}");
        return matches[0];
    }

    public EfiPeImage(byte[] bytes)
    {
        Bytes = bytes;
        Require(bytes.Length != 0, "empty_artifact");
        Range(0, 64, bytes.Length, "truncated_dos_header");
        Require(bytes[0] == 0x4d && bytes[1] == 0x5a, "invalid_dos_signature");
        long pe = U32(0x3c, "truncated_dos_header");
        Require(pe >= 64, "invalid_pe_offset");
        Range(pe, 24, bytes.Length, "truncated_pe_header");
        Require(U32(pe) == 0x4550, "invalid_pe_signature");
        long coff = pe + 4;
        Require(U16(coff) == 0x8664, "machine_not_x64", $"actual=0x{U16(coff):x4}");
        int count = U16(coff + 2);
        Timestamp = U32(coff + 4);
        int optionalSize = U16(coff + 16);
        Require(count is> 0 and <= 96, "section_count_out_of_range");
        Require(optionalSize >= 112, "optional_header_too_small");
        long optional = coff + 20;
        Range(optional, optionalSize, bytes.Length, "truncated_optional_header");
        Require(U16(optional) == 0x20b, "format_not_pe32_plus", $"actual=0x{U16(optional):x4}");
        EntryPointRva = U32(optional + 16);
        Require(EntryPointRva != 0, "entrypoint_zero");
        ImageBase = U64(optional + 24);
        Require(ImageBase != 0, "image_base_zero");
        uint sectionAlignment = U32(optional + 32), fileAlignment = U32(optional + 36);
        SizeOfImage = U32(optional + 56);
        Require(SizeOfImage != 0, "size_of_image_zero");
        uint headers = U32(optional + 60);
        Require(sectionAlignment != 0 && fileAlignment != 0 && headers != 0 && headers <= bytes.Length && headers <= SizeOfImage, "pe_layout_bounds_invalid");
        Require(SizeOfImage <= ulong.MaxValue - ImageBase, "image_virtual_range_overflow");
        Require(EntryPointRva < SizeOfImage, "entrypoint_out_of_image");
        Require(U16(optional + 68) == 10, "subsystem_not_efi_application");
        uint directories = U32(optional + 108);
        Require(directories <= (optionalSize - 112) / 8, "data_directory_count_out_of_range");
        Require(directories > 3, "exception_directory_missing");
        ExceptionRva = U32(optional + 112 + 3 * 8);
        ExceptionSize = U32(optional + 116 + 3 * 8);
        Require(ExceptionRva != 0 && ExceptionSize != 0, "exception_directory_zero");
        Require((ExceptionRva & 3) == 0, "exception_directory_rva_misaligned");
        Require(ExceptionSize % 12 == 0, "exception_directory_size_not_record_aligned");
        Require(ExceptionSize / 12 <= EfiContracts.MaximumRuntimeFunctionRecords, "exception_directory_record_count_out_of_range");
        Require(RangeEnd(ExceptionRva, ExceptionSize, "exception_directory_range_invalid") <= SizeOfImage, "exception_directory_out_of_image");
        foreach ((int index, string name)in new[]
        {
            (1, "import"),
            (11, "bound-import"),
            (12, "iat"),
            (13, "delay-import")
        }

        )
        {
            if (directories <= index)
                continue;
            uint rva = U32(optional + 112 + index * 8), size = U32(optional + 116 + index * 8);
            Require((rva == 0) == (size == 0), "malformed_import_directory", "directory=" + name);
            Require(rva == 0 && size == 0, "ordinary_runtime_imports_present", "directory=" + name);
        }

        long table = optional + optionalSize;
        Range(table, count * 40, bytes.Length, "truncated_section_table");
        Require(table <= headers && count * 40 <= headers - table, "section_table_outside_headers");
        for (int i = 0; i < count; i++)
        {
            long at = table + i * 40;
            var section = new Section(Encoding.ASCII.GetString(Copy(at, 8, "truncated_section_table")).TrimEnd('\0'), U32(at + 8), U32(at + 12), U32(at + 16), U32(at + 20), U32(at + 36));
            if (section.RawSize != 0)
            {
                Require(section.RawOffset >= headers, "section_raw_range_overlaps_headers", section.Name);
                Range(section.RawOffset, section.RawSize, bytes.Length, "section_raw_data_out_of_range");
            }

            if (section.MappedSize != 0)
            {
                ulong end = section.VirtualAddress + section.MappedSize;
                Require(end <= 0x100000000, "section_virtual_range_overflow", section.Name);
                Require(section.VirtualAddress >= headers, "section_virtual_range_overlaps_headers", section.Name);
                Require(end <= SizeOfImage, "section_virtual_range_out_of_image", section.Name);
            }

            Sections.Add(section);
        }

        for (int i = 0; i < Sections.Count; i++)
            for (int j = i + 1; j < Sections.Count; j++)
            {
                Section a = Sections[i], b = Sections[j];
                Require(!Overlap(a.VirtualAddress, a.MappedSize, b.VirtualAddress, b.MappedSize), "section_virtual_ranges_overlap", $"left={a.Name} right={b.Name}");
                Require(!Overlap(a.RawOffset, a.RawSize, b.RawOffset, b.RawSize), "section_raw_ranges_overlap", $"left={a.Name} right={b.Name}");
            }

        Section[] entrySections = Sections.Where(s => s.VirtualSize != 0 && EntryPointRva >= s.VirtualAddress && EntryPointRva < (ulong)s.VirtualAddress + s.VirtualSize).ToArray();
        Require(entrySections.Length != 0, "entrypoint_outside_sections");
        Require(entrySections.Length == 1, "entrypoint_section_ambiguous");
        Section entry = entrySections[0];
        EntrySection = entry.Name;
        Require((entry.Characteristics & 0x20000000) != 0, "entrypoint_section_not_executable");
        ulong delta = EntryPointRva - entry.VirtualAddress, entryOffset = entry.RawOffset + delta;
        Require(delta < entry.RawSize && entryOffset < (ulong)bytes.Length, "entrypoint_not_file_backed");
        Require(bytes[(int)entryOffset] is not (0x00 or 0xc3 or 0xcb or 0xcc), "entrypoint_trivial_stub", $"byte=0x{bytes[(int)entryOffset]:x2}");
        Resolve(ExceptionRva, ExceptionSize, "exception_directory_not_file_backed", "exception_directory_section_ambiguous").Section.RequireReadOnlyData("exception_directory_section_characteristics_invalid");
    }

    private static bool Overlap(ulong a, ulong sizeA, ulong b, ulong sizeB) => sizeA != 0 && sizeB != 0 && a < b + sizeB && b < a + sizeA;
    public static MatchCollection Symbols(string map, string name) => Regex.Matches(map, @"(?m)^\s*([0-9A-Fa-f]{4}):([0-9A-Fa-f]{8})\s+" + Regex.Escape(name) + @"\s+([0-9A-Fa-f]{16})\s+(.+?)\s*$", RegexOptions.CultureInvariant);
    public static bool Owner(Match symbol, string owner) => Regex.IsMatch(symbol.Groups[4].Value, @"(?i)(?:^|[\s\\/()])" + Regex.Escape(owner) + @"(?=$|[\s()])", RegexOptions.CultureInvariant);
    public static ulong Hex(string text, string reason)
    {
        Require(ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong value), reason);
        return value;
    }

    public ulong SymbolAddress(string map, string name, string owner, string reason)
    {
        MatchCollection bindings = Symbols(map, name);
        Require(bindings.Count == 1 && Owner(bindings[0], owner), reason, name);
        return Hex(bindings[0].Groups[3].Value, reason + "_address");
    }

    public void VerifyMapAndRuntime(string map, string serializerObjectLeaf)
    {
        var preferred = Regex.Matches(map, @"(?m)^\s*Preferred load address is ([0-9A-Fa-f]{16})\s*$");
        var timestamp = Regex.Matches(map, @"(?m)^\s*Timestamp is ([0-9A-Fa-f]{8})(?:\s|\(|$)");
        var entryPoint = Regex.Matches(map, @"(?m)^\s*entry point at\s+([0-9A-Fa-f]{4}):([0-9A-Fa-f]{8})\s*$");
        var native = Symbols(map, "EfiMain");
        var managed = Symbols(map, "OS_OS_Boot_EfiEntry__EfiMain");
        var serializer = Symbols(map, EfiContracts.SerializerSymbol);
        Require(preferred.Count == 1 && timestamp.Count == 1 && entryPoint.Count == 1 && native.Count == 1 && managed.Count == 1, "efimain_linker_map_contract_missing");
        Require(serializer.Count == 1, "serializer_linker_map_contract_missing");
        Require(Owner(serializer[0], serializerObjectLeaf), "serializer_linker_map_object_mismatch");
        ulong baseAddress = Hex(preferred[0].Groups[1].Value, "invalid_map_preferred_base"), mapTimestamp = Hex(timestamp[0].Groups[1].Value, "invalid_map_timestamp");
        ulong nativeAddress = Hex(native[0].Groups[3].Value, "invalid_map_efimain_address"), managedAddress = Hex(managed[0].Groups[3].Value, "invalid_map_managed_entry_address");
        Require(baseAddress == ImageBase && mapTimestamp == Timestamp && nativeAddress == managedAddress && nativeAddress >= baseAddress && nativeAddress - baseAddress == EntryPointRva && native[0].Groups[1].Value == entryPoint[0].Groups[1].Value && native[0].Groups[2].Value == entryPoint[0].Groups[2].Value && native[0].Groups[1].Value == managed[0].Groups[1].Value && native[0].Groups[2].Value == managed[0].Groups[2].Value, "efimain_entrypoint_mismatch");
        ulong serializerAddress = Hex(serializer[0].Groups[3].Value, "invalid_map_serializer_address");
        Require(serializerAddress > baseAddress && serializerAddress - baseAddress <= uint.MaxValue, "serializer_map_rva_invalid");
        SerializerRva = (uint)(serializerAddress - baseAddress);
        Require((SerializerRva & 15) == 0, "serializer_rva_misaligned");
        ulong serializerEnd = RangeEnd(SerializerRva, (ulong)EfiContracts.SerializerBytes.Length, "serializer_rva_range_invalid");
        Require(SerializerRva < SizeOfImage && serializerEnd <= SizeOfImage, "serializer_rva_out_of_image");
        var backing = Resolve(SerializerRva, (ulong)EfiContracts.SerializerBytes.Length, "serializer_not_file_backed", "serializer_section_ambiguous");
        backing.Section.RequireCode("serializer_section_characteristics_invalid");
        byte[] serializerBytes = Copy(backing.Offset, backing.Size, "serializer_not_file_backed");
        Require(serializerBytes.AsSpan().SequenceEqual(EfiContracts.SerializerBytes) && ContractAudit.Hash(serializerBytes) == EfiContracts.SerializerBytesSha256, "serializer_bytes_mismatch");
        ValidateRuntimeFunctions();
        var selected = RuntimeFunctions.Where(f => f.Begin == SerializerRva).ToArray();
        if (selected.Length == 0)
        {
            Require(!RuntimeFunctions.Any(f => f.Begin < serializerEnd && f.End > SerializerRva), "serializer_runtime_function_overlap");
            throw new ToolFailure("serializer_runtime_function_missing");
        }

        Require(selected.Length == 1, "serializer_runtime_function_ambiguous");
        var function = selected[0];
        SerializerUnwindRva = function.Unwind;
        Require(function.End == serializerEnd, "serializer_runtime_function_range_mismatch");
        Require(RuntimeFunctions.Count(f => f.Unwind == function.Unwind) == 1, "serializer_unwind_aliased");
        Require(function.UnwindSize == EfiContracts.SerializerUnwindBytes.Length, "serializer_unwind_size_mismatch");
        Require(Copy(function.UnwindOffset, function.UnwindSize, "serializer_unwind_not_file_backed").AsSpan().SequenceEqual(EfiContracts.SerializerUnwindBytes), "serializer_unwind_bytes_mismatch");
    }

    private void ValidateRuntimeFunctions()
    {
        RuntimeFunctions.Clear();
        var exception = Resolve(ExceptionRva, ExceptionSize, "exception_directory_not_file_backed", "exception_directory_section_ambiguous");
        Dictionary<uint, (int Offset, int Size)> unwindCache = [];
        for (int i = 0; i < ExceptionSize / 12; i++)
        {
            int offset = exception.Offset + i * 12;
            uint begin = U32(offset), end = U32(offset + 4), unwind = U32(offset + 8);
            Require(begin < end, "runtime_function_range_invalid", "index=" + i);
            Require(begin < SizeOfImage && end <= SizeOfImage, "runtime_function_range_out_of_image", "index=" + i);
            Require(unwind != 0, "runtime_function_unwind_zero", "index=" + i);
            Require((unwind & 3) == 0, "runtime_function_unwind_misaligned", "index=" + i);
            Require(unwind < SizeOfImage, "runtime_function_unwind_out_of_image", "index=" + i);
            if (RuntimeFunctions.Count != 0)
            {
                var prior = RuntimeFunctions[^1];
                Require(begin != prior.Begin || end != prior.End || unwind != prior.Unwind, "runtime_function_duplicate", "index=" + i);
                Require(begin > prior.Begin, "runtime_function_not_strictly_sorted", "index=" + i);
                Require(begin >= prior.End, "runtime_function_overlap", "index=" + i);
            }

            if (!unwindCache.TryGetValue(unwind, out var cached))
            {
                var header = Resolve(unwind, 4, "runtime_function_unwind_not_file_backed", "runtime_function_unwind_section_ambiguous");
                header.Section.RequireReadOnlyData("runtime_function_unwind_section_characteristics_invalid");
                int version = Bytes[header.Offset] & 7, flags = Bytes[header.Offset] >> 3, count = Bytes[header.Offset + 2];
                Require(version == 1 && (flags & 0x18) == 0 && !((flags & 4) != 0 && (flags & 3) != 0), "runtime_function_unwind_header_invalid", "index=" + i);
                int size = 4 + ((count + 1) & ~1) * 2 + ((flags & 4) != 0 ? 12 : (flags & 3) != 0 ? 4 : 0);
                var full = Resolve(unwind, (ulong)size, "runtime_function_unwind_not_file_backed", "runtime_function_unwind_section_ambiguous");
                cached = (full.Offset, size);
                unwindCache.Add(unwind, cached);
            }

            RuntimeFunctions.Add(new(begin, end, unwind, cached.Offset, cached.Size));
        }
    }
}
