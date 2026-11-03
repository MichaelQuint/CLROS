using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class FileSystemSourceDouble : IPlatformFileSystemSource
    {
        public int ReadCount { get; private set; }

        public PlatformFileSystemSourceReadResult ReadFile(ReadOnlySpan<byte> relativePath, Span<byte> destination)
        {
            ReadCount++;
            if (destination.Length == 0)
            {
                return PlatformFileSystemSourceReadResult.DestinationTooSmall(1, 0);
            }

            destination[0] = 0x42;
            return PlatformFileSystemSourceReadResult.Read(1, destination.Length);
        }
    }
}
