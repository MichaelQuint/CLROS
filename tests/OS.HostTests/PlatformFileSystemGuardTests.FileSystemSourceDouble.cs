using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformFileSystemGuardTests
{
    private sealed class FileSystemSourceDouble : IPlatformFileSystemSource
    {
        private static readonly byte[] Payload = [(byte)'O', (byte)'S', (byte)'F', (byte)'S'];
        private readonly byte[] _expectedPath;
        public FileSystemSourceDouble(ReadOnlySpan<byte> expectedPath)
        {
            _expectedPath = expectedPath.ToArray();
        }

        public int ReadCount
        {
            get; private set;
        }

        public PlatformFileSystemSourceReadResult ReadFile(ReadOnlySpan<byte> relativePath, Span<byte> destination)
        {
            ReadCount++;
            if (!relativePath.SequenceEqual(_expectedPath))
            {
                return PlatformFileSystemSourceReadResult.Missing();
            }

            if (destination.Length < Payload.Length)
            {
                return PlatformFileSystemSourceReadResult.DestinationTooSmall(Payload.Length, destination.Length);
            }

            Payload.CopyTo(destination);
            return PlatformFileSystemSourceReadResult.Read(Payload.Length, destination.Length);
        }
    }
}
