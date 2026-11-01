namespace OS.Kernel.Platform;
public interface IPlatformFileSystemSource
{
    PlatformFileSystemSourceReadResult ReadFile(ReadOnlySpan<byte> relativePath, Span<byte> destination);
}
