namespace OS.Kernel.Platform;
public interface IPlatformRandom
{
    PlatformRandomResult FillBytes(Span<byte> destination);
}
