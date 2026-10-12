using OS.Std.Binary;

namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxElfLoadStatus : byte
{
    Uninitialized = 0,
    Ready = 1,
    InvalidSource = 2,
    InvalidIdentification = 3,
    InvalidHeader = 4,
    InvalidProgramTable = 5,
    InterpreterRequired = 6,
    DynamicLinking = 7,
    ExecutableStack = 8,
    UnknownProgramType = 9,
    InvalidFileExtent = 10,
    InvalidLoad = 11,
    InvalidAlignment = 12,
    TooManyLoads = 13,
    OverlappingLoads = 14,
    InvalidImageSpan = 15,
    MemoryLimitExceeded = 16,
    InvalidEntry = 17,
    ProgramHeadersNotMapped = 18,
    DuplicateMetadata = 19,
    InvalidProgramHeaders = 20,
    InvalidTls = 21,
    InvalidRelro = 22,
    InvalidEhFrame = 23,
    InvalidResourceLimits = 24,
    InvalidProperties = 25
}

/// <summary>
/// Value-only geometry of one static Linux x64 image. Each load row contains
/// address, file offset, file bytes, memory bytes and ELF permission flags.
/// Source bytes remain caller-owned; this plan retains no pointers to them.
/// Ready describes admission only, without allocation, mapping or execution.
/// </summary>
public unsafe struct LinuxElfLoadPlan
{
    public LinuxElfLoadStatus Status;
    public uint SourceBytes, ProgramHeaderCount, LoadCount;
    public ulong ImageStart, ImageBytes, Entry, ProgramHeaders;
    public ulong TlsAddress, TlsFileBytes, TlsMemoryBytes, TlsAlignment;
    public fixed ulong Loads[80];
    public readonly bool Succeeded => Status == LinuxElfLoadStatus.Ready;
}

/// <summary>
/// Allocation-free admission of bounded, static ELF64 Linux x64 geometry.
/// The caller retains trusted readable source storage for the entire call.
/// Output storage must not alias the source. Physical ownership is assigned
/// separately; the ELF physical-address fields are not consumed.
/// This does not construct an initial stack, root mappings or a running image.
/// </summary>
public static unsafe partial class LinuxElfLoader
{
    public const uint MaximumLoads = 16;
    public const uint MaximumImageBytes = 64 * 1024 * 1024;
    private const ulong PageBytes = 4096, MaximumAddress = 0x10000000;
    private const ulong MaximumLoadAlignment = 2 * 1024 * 1024, MaximumTlsAlignment = 64 * 1024;
    private const uint Load = 1, Dynamic = 2, Interpreter = 3, Note = 4, Phdr = 6, Tls = 7;
    private const uint EhFrame = 0x6474e550, Stack = 0x6474e551, Relro = 0x6474e552, Property = 0x6474e553;

    private struct Segment
    {
        internal uint Type, Flags;
        internal ulong Offset, Address, FileBytes, MemoryBytes, Alignment;
    }

    /// <summary>
    /// Validates file/table/load extents, permissions, page geometry, entry,
    /// mapped program headers and optional TLS/RELRO metadata. The memory limit
    /// includes the rounded image span and supplied stack budget. Rejection
    /// clears every output field except Status. No source bytes are changed.
    /// Section tables, relocation processing and runtime support are unsupported.
    /// </summary>
    public static LinuxElfLoadStatus Validate(byte* elf, uint bytes, uint memoryLimit,
        uint stackBytes, out LinuxElfLoadPlan plan)
    {
        plan = default;
        if (memoryLimit < 4096 || memoryLimit > 1024 * 1024 * 1024 || (memoryLimit & 4095) != 0 ||
            stackBytes < 4096 || stackBytes > 8 * 1024 * 1024 || (stackBytes & 4095) != 0 || stackBytes > memoryLimit)
            return Reject(LinuxElfLoadStatus.InvalidResourceLimits, out plan);
        if (elf == null || bytes < 64 || bytes > MaximumImageBytes ||
            (ulong)elf > ulong.MaxValue - bytes)
            return Reject(LinuxElfLoadStatus.InvalidSource, out plan);
        if (EndianPrimitives.ReadUInt32LittleEndian(elf) != 0x464c457f ||
            elf[4] != 2 || elf[5] != 1 || elf[6] != 1 ||
            (elf[7] != 0 && elf[7] != 3) || elf[8] != 0)
            return Reject(LinuxElfLoadStatus.InvalidIdentification, out plan);
        for (uint i = 9; i < 16; i++)
            if (elf[i] != 0) return Reject(LinuxElfLoadStatus.InvalidIdentification, out plan);
        if (EndianPrimitives.ReadUInt16LittleEndian(elf + 16) != 2 ||
            EndianPrimitives.ReadUInt16LittleEndian(elf + 18) != 62 ||
            EndianPrimitives.ReadUInt32LittleEndian(elf + 20) != 1 ||
            EndianPrimitives.ReadUInt32LittleEndian(elf + 48) != 0 ||
            EndianPrimitives.ReadUInt16LittleEndian(elf + 52) != 64 ||
            EndianPrimitives.ReadUInt16LittleEndian(elf + 54) != 56)
            return Reject(LinuxElfLoadStatus.InvalidHeader, out plan);

        uint count = EndianPrimitives.ReadUInt16LittleEndian(elf + 56);
        ulong phoff = EndianPrimitives.ReadUInt64LittleEndian(elf + 32);
        ulong tableBytes = (ulong)count * 56;
        if (count == 0 || count > 128 || phoff < 64 || !Fits(phoff, tableBytes, bytes))
            return Reject(LinuxElfLoadStatus.InvalidProgramTable, out plan);

        LinuxElfLoadPlan candidate = default;
        candidate.SourceBytes = bytes;
        candidate.ProgramHeaderCount = count;
        candidate.Entry = EndianPrimitives.ReadUInt64LittleEndian(elf + 24);
        Segment tls = default, phdr = default, relro = default, ehFrame = default, property = default;
        bool hasTls = false, hasPhdr = false, hasRelro = false, hasEhFrame = false, hasStack = false, hasProperty = false;
        ulong imageStart = MaximumAddress, imageEnd = 0;
        byte* table = elf + (uint)phoff;
        for (uint i = 0; i < count; i++)
        {
            Segment row = ReadSegment(table + i * 56);
            if (row.Type == Interpreter) return Reject(LinuxElfLoadStatus.InterpreterRequired, out plan);
            if (row.Type == Dynamic) return Reject(LinuxElfLoadStatus.DynamicLinking, out plan);
            if (row.Type != 0 && row.Type != Load && row.Type != Note && row.Type != Phdr &&
                row.Type != Tls && row.Type != EhFrame && row.Type != Stack && row.Type != Relro && row.Type != Property)
                return Reject(LinuxElfLoadStatus.UnknownProgramType, out plan);
            if (!Fits(row.Offset, row.FileBytes, bytes))
                return Reject(LinuxElfLoadStatus.InvalidFileExtent, out plan);

            if (row.Type == Load)
            {
                if (!MemoryExtent(row) || row.Flags > 7 || (row.Flags & 4) == 0 ||
                    (row.Flags & 3) == 3)
                    return Reject(LinuxElfLoadStatus.InvalidLoad, out plan);
                if (!Aligned(row, MaximumLoadAlignment, true) ||
                    (row.Address & (PageBytes - 1)) != (row.Offset & (PageBytes - 1)))
                    return Reject(LinuxElfLoadStatus.InvalidAlignment, out plan);
                if (candidate.LoadCount == MaximumLoads)
                    return Reject(LinuxElfLoadStatus.TooManyLoads, out plan);
                ulong first = PageStart(row.Address), end = PageEnd(row.Address + row.MemoryBytes);
                for (uint j = 0; j < candidate.LoadCount; j++)
                {
                    uint at = j * 5;
                    ulong other = candidate.Loads[at], otherBytes = candidate.Loads[at + 3];
                    if (Overlaps(row.Address, row.MemoryBytes, other, otherBytes) ||
                        (first < PageEnd(other + otherBytes) && PageStart(other) < end))
                        return Reject(LinuxElfLoadStatus.OverlappingLoads, out plan);
                }
                uint slot = candidate.LoadCount++ * 5;
                candidate.Loads[slot] = row.Address;
                candidate.Loads[slot + 1] = row.Offset;
                candidate.Loads[slot + 2] = row.FileBytes;
                candidate.Loads[slot + 3] = row.MemoryBytes;
                candidate.Loads[slot + 4] = row.Flags;
                if (first < imageStart) imageStart = first;
                if (end > imageEnd) imageEnd = end;
            }
            else if (row.Type == Tls)
            {
                if (hasTls) return Reject(LinuxElfLoadStatus.DuplicateMetadata, out plan);
                hasTls = true; tls = row;
            }
            else if (row.Type == Phdr)
            {
                if (hasPhdr) return Reject(LinuxElfLoadStatus.DuplicateMetadata, out plan);
                hasPhdr = true; phdr = row;
            }
            else if (row.Type == Relro)
            {
                if (hasRelro) return Reject(LinuxElfLoadStatus.DuplicateMetadata, out plan);
                hasRelro = true; relro = row;
            }
            else if (row.Type == EhFrame)
            {
                if (hasEhFrame) return Reject(LinuxElfLoadStatus.DuplicateMetadata, out plan);
                hasEhFrame = true; ehFrame = row;
            }
            else if (row.Type == Stack)
            {
                if (hasStack) return Reject(LinuxElfLoadStatus.DuplicateMetadata, out plan);
                hasStack = true;
                if ((row.Flags & 1) != 0) return Reject(LinuxElfLoadStatus.ExecutableStack, out plan);
            }
            else if (row.Type == Property)
            {
                if (hasProperty) return Reject(LinuxElfLoadStatus.DuplicateMetadata, out plan);
                hasProperty = true; property = row;
            }
        }

        if (candidate.LoadCount == 0) return Reject(LinuxElfLoadStatus.InvalidLoad, out plan);
        candidate.ImageStart = imageStart;
        candidate.ImageBytes = imageEnd - imageStart;
        if (candidate.ImageBytes == 0 || candidate.ImageBytes > MaximumImageBytes)
            return Reject(LinuxElfLoadStatus.InvalidImageSpan, out plan);
        if (candidate.ImageBytes + stackBytes > memoryLimit)
            return Reject(LinuxElfLoadStatus.MemoryLimitExceeded, out plan);

        bool entryFound = false, headersFound = false;
        for (uint i = 0; i < candidate.LoadCount; i++)
        {
            uint at = i * 5;
            ulong address = candidate.Loads[at], offset = candidate.Loads[at + 1];
            ulong fileBytes = candidate.Loads[at + 2], flags = candidate.Loads[at + 4];
            if ((flags & 3) == 1 && candidate.Entry >= address &&
                candidate.Entry - address < fileBytes) entryFound = true;
            if (phoff >= offset && Fits(phoff - offset, tableBytes, fileBytes))
            {
                if (headersFound) return Reject(LinuxElfLoadStatus.InvalidProgramHeaders, out plan);
                candidate.ProgramHeaders = address + (phoff - offset);
                headersFound = true;
            }
        }
        if (!entryFound) return Reject(LinuxElfLoadStatus.InvalidEntry, out plan);
        if (!headersFound) return Reject(LinuxElfLoadStatus.ProgramHeadersNotMapped, out plan);
        if (hasPhdr && (phdr.Offset != phoff || phdr.Address != candidate.ProgramHeaders ||
            phdr.FileBytes != tableBytes || phdr.MemoryBytes != tableBytes ||
            !MemoryExtent(phdr) || !Aligned(phdr, MaximumLoadAlignment, true) ||
            !Associated(ref candidate, phdr)))
            return Reject(LinuxElfLoadStatus.InvalidProgramHeaders, out plan);
        if (hasTls)
        {
            if (!MemoryExtent(tls) || !Aligned(tls, MaximumTlsAlignment, false) ||
                !Associated(ref candidate, tls))
                return Reject(LinuxElfLoadStatus.InvalidTls, out plan);
            candidate.TlsAddress = tls.Address;
            candidate.TlsFileBytes = tls.FileBytes;
            candidate.TlsMemoryBytes = tls.MemoryBytes;
            candidate.TlsAlignment = tls.Alignment;
        }
        if (hasRelro && (!MemoryExtent(relro) || !Aligned(relro, MaximumLoadAlignment, true) ||
            !Associated(ref candidate, relro)))
            return Reject(LinuxElfLoadStatus.InvalidRelro, out plan);
        if (hasEhFrame && (ehFrame.FileBytes == 0 || !MemoryExtent(ehFrame) ||
            !Aligned(ehFrame, MaximumLoadAlignment, true) || !Associated(ref candidate, ehFrame)))
            return Reject(LinuxElfLoadStatus.InvalidEhFrame, out plan);
        if (hasProperty && (!MemoryExtent(property) || property.Flags != 4 ||
            property.FileBytes != property.MemoryBytes || property.FileBytes < 16 || property.FileBytes > 1024 ||
            !Aligned(property, MaximumLoadAlignment, true) || !Associated(ref candidate, property) ||
            !Properties(elf + (uint)property.Offset, (uint)property.FileBytes, stackBytes)))
            return Reject(LinuxElfLoadStatus.InvalidProperties, out plan);

        candidate.Status = LinuxElfLoadStatus.Ready;
        plan = candidate;
        return LinuxElfLoadStatus.Ready;
    }

    private static Segment ReadSegment(byte* row) => new Segment
    {
        Type = EndianPrimitives.ReadUInt32LittleEndian(row),
        Flags = EndianPrimitives.ReadUInt32LittleEndian(row + 4),
        Offset = EndianPrimitives.ReadUInt64LittleEndian(row + 8),
        Address = EndianPrimitives.ReadUInt64LittleEndian(row + 16),
        FileBytes = EndianPrimitives.ReadUInt64LittleEndian(row + 32),
        MemoryBytes = EndianPrimitives.ReadUInt64LittleEndian(row + 40),
        Alignment = EndianPrimitives.ReadUInt64LittleEndian(row + 48)
    };

    private static bool MemoryExtent(Segment row) =>
        row.Address >= PageBytes && row.Address < MaximumAddress &&
        row.MemoryBytes != 0 && row.FileBytes <= row.MemoryBytes &&
        row.MemoryBytes <= MaximumAddress - row.Address;

    private static bool Aligned(Segment row, ulong maximum, bool allowZero)
    {
        ulong alignment = row.Alignment;
        if (alignment == 0) return allowZero;
        return alignment <= maximum && (alignment & (alignment - 1)) == 0 &&
            (row.Address & (alignment - 1)) == (row.Offset & (alignment - 1));
    }

    private static bool Associated(ref LinuxElfLoadPlan plan, Segment row)
    {
        fixed (ulong* loads = plan.Loads)
        {
            for (uint i = 0; i < plan.LoadCount; i++)
            {
                uint at = i * 5;
                if (row.Address < loads[at]) continue;
                ulong delta = row.Address - loads[at];
                if (Fits(delta, row.MemoryBytes, loads[at + 3]) &&
                    Fits(delta, row.FileBytes, loads[at + 2]) &&
                    row.Offset == loads[at + 1] + delta) return true;
            }
        }
        return false;
    }

    private static bool Fits(ulong offset, ulong length, ulong available) =>
        offset <= available && length <= available - offset;

    private static bool Properties(byte* note, uint bytes, uint stackBytes)
    {
        if (EndianPrimitives.ReadUInt32LittleEndian(note) != 4 ||
            EndianPrimitives.ReadUInt32LittleEndian(note + 8) != 5 ||
            EndianPrimitives.ReadUInt32LittleEndian(note + 12) != 0x00554e47) return false;
        uint descriptorBytes = EndianPrimitives.ReadUInt32LittleEndian(note + 4);
        if (descriptorBytes == 0 || descriptorBytes != bytes - 16 || (descriptorBytes & 7) != 0) return false;
        uint offset = 16, previous = 0; bool first = true;
        while (offset < bytes)
        {
            if (bytes - offset < 8) return false;
            uint type = EndianPrimitives.ReadUInt32LittleEndian(note + offset);
            uint length = EndianPrimitives.ReadUInt32LittleEndian(note + offset + 4);
            if ((!first && type <= previous) || length > bytes - offset - 8) return false;
            uint padded = (length + 7) & ~7U;
            if (padded > bytes - offset - 8) return false;
            byte* value = note + offset + 8;
            // The qualified GNU note declares stack size. Wider ISA/CET startup
            // requirements are admitted only at the baseline/disabled values.
            if (type == 1)
            {
                if (length != 8 || EndianPrimitives.ReadUInt64LittleEndian(value) > stackBytes) return false;
            }
            else if (type == 0xc0008002)
            {
                if (length != 4 || EndianPrimitives.ReadUInt32LittleEndian(value) != 1) return false;
            }
            else if (type == 0xc0000002)
            {
                if (length != 4 || EndianPrimitives.ReadUInt32LittleEndian(value) != 0) return false;
            }
            else return false;
            for (uint i = length; i < padded; i++) if (value[i] != 0) return false;
            offset += 8 + padded; previous = type; first = false;
        }
        return offset == bytes;
    }
    private static ulong PageStart(ulong address) => address & ~(PageBytes - 1);
    private static ulong PageEnd(ulong address) => (address + PageBytes - 1) & ~(PageBytes - 1);
    private static bool Overlaps(ulong first, ulong firstBytes, ulong second, ulong secondBytes) =>
        first < second + secondBytes && second < first + firstBytes;

    private static LinuxElfLoadStatus Reject(LinuxElfLoadStatus status, out LinuxElfLoadPlan plan)
    {
        plan = default;
        plan.Status = status;
        return status;
    }
}
