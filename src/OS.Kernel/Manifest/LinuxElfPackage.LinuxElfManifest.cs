using OS.Std.Hash;

namespace OS.Manifest;
/// <summary>Value-only offsets into stable manifest bytes, never firmware handles.</summary>

public unsafe struct LinuxElfManifest
{
    public LinuxElfStatus Status;
    public uint ErrorOffset, ByteCount, MemoryBytes, StackBytes, Threads, Descriptors, Ipv4, Port;
    public LinuxElfField Path, Args, Digest;
    public uint EnvironmentCount;
    public fixed uint EnvironmentRanges[64]; // key offset/length, value offset/length
    public readonly bool Succeeded => Status == LinuxElfStatus.Success;
}
