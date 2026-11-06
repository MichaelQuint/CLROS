namespace OS.Kernel.Platform;

public interface IPlatformPhysicalMemorySource
{
    PlatformPhysicalMemorySourceResult ReadBytes(ulong physicalAddress, int byteCount, Span<byte> destination);
}
