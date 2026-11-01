using OS.Std.Binary;

namespace OS.Kernel.Compatibility.LinuxX64;
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
