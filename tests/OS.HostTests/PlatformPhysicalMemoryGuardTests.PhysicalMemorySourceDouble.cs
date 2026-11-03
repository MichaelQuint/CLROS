using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformPhysicalMemoryGuardTests
{
    private sealed class PhysicalMemorySourceDouble : IPlatformPhysicalMemorySource
    {
        private readonly byte _firstByte;
        private readonly int _bytesToRead;
        private readonly PlatformPhysicalMemorySourceResult _result;
        private PhysicalMemorySourceDouble(byte firstByte, int bytesToRead, PlatformPhysicalMemorySourceResult result)
        {
            _firstByte = firstByte;
            _bytesToRead = bytesToRead;
            _result = result;
        }

        public int ReadCount
        {
            get; private set;
        }
        public ulong LastPhysicalAddress
        {
            get; private set;
        }
        public int LastByteCount
        {
            get; private set;
        }
        public int LastDestinationLength
        {
            get; private set;
        }

        public static PhysicalMemorySourceDouble Success(byte firstByte, int bytesToRead)
        {
            return new PhysicalMemorySourceDouble(firstByte, bytesToRead, PlatformPhysicalMemorySourceResult.Read(bytesToRead));
        }

        public static PhysicalMemorySourceDouble Unavailable()
        {
            return new PhysicalMemorySourceDouble(0, 0, PlatformPhysicalMemorySourceResult.Unavailable());
        }

        public static PhysicalMemorySourceDouble Fault(ulong firmwareStatus)
        {
            return new PhysicalMemorySourceDouble(0, 0, PlatformPhysicalMemorySourceResult.Fault(firmwareStatus));
        }

        public PlatformPhysicalMemorySourceResult ReadBytes(ulong physicalAddress, int byteCount, Span<byte> destination)
        {
            ReadCount++;
            LastPhysicalAddress = physicalAddress;
            LastByteCount = byteCount;
            LastDestinationLength = destination.Length;
            int bytesToWrite = Math.Min(_bytesToRead, destination.Length);
            for (int index = 0; index < bytesToWrite; index++)
            {
                destination[index] = (byte)(_firstByte + index);
            }

            return _result;
        }
    }
}
