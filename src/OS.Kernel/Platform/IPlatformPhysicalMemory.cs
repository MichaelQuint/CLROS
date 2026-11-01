namespace OS.Kernel.Platform;
public interface IPlatformPhysicalMemory
{
    PlatformPhysicalMemoryResult ReadBytes(ulong physicalAddress, int byteCount, Span<byte> destination);
}
