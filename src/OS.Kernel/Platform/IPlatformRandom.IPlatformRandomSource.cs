namespace OS.Kernel.Platform;

public interface IPlatformRandomSource
{
    PlatformRandomSourceResult FillBytes(Span<byte> destination);
}
