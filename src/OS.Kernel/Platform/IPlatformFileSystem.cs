namespace OS.Kernel.Platform;

public interface IPlatformFileSystem
{
    PlatformFileSystemReadResult ReadFile(ReadOnlySpan<byte> relativePath, Span<byte> destination);
}
