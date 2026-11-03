using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PciEcamPhysicalHeaderReaderTests
{
    private sealed class PhysicalMemoryDouble : IPlatformPhysicalMemory
    {
        private readonly PhysicalMemoryEntry[] _entries;
        private readonly PlatformPhysicalMemoryStatus _failureStatus;
        private PhysicalMemoryDouble(PhysicalMemoryEntry[] entries, PlatformPhysicalMemoryStatus failureStatus)
        {
            _entries = entries;
            _failureStatus = failureStatus;
        }

        public int ReadCount { get; private set; }
        public ulong RequestedAddress { get; private set; }

        public static PhysicalMemoryDouble Success(params PhysicalMemoryEntry[] entries)
        {
            return new PhysicalMemoryDouble(entries, PlatformPhysicalMemoryStatus.Success);
        }

        public static PhysicalMemoryDouble Fail(PlatformPhysicalMemoryStatus failureStatus)
        {
            return new PhysicalMemoryDouble([], failureStatus);
        }

        public PlatformPhysicalMemoryResult ReadBytes(ulong physicalAddress, int byteCount, Span<byte> destination)
        {
            ReadCount++;
            RequestedAddress = physicalAddress;
            FirmwareCallResult guard = FirmwareCallResult.Allowed(PlatformPhysicalMemoryOperations.ReadBytes, FirmwareServicesMode.BootServicesAvailable);
            if (_failureStatus != PlatformPhysicalMemoryStatus.Success)
            {
                PlatformPhysicalMemorySourceResult failedSource = _failureStatus == PlatformPhysicalMemoryStatus.SourceUnavailable ? PlatformPhysicalMemorySourceResult.Unavailable() : PlatformPhysicalMemorySourceResult.Fault(0x8000000000000009);
                return PlatformPhysicalMemoryResult.Read(guard, failedSource, physicalAddress, byteCount);
            }

            for (int index = 0; index < _entries.Length; index++)
            {
                if (_entries[index].PhysicalAddress != physicalAddress)
                {
                    continue;
                }

                int copyLength = Math.Min(byteCount, _entries[index].Bytes.Length);
                _entries[index].Bytes.AsSpan(0, copyLength).CopyTo(destination);
                return PlatformPhysicalMemoryResult.Read(guard, PlatformPhysicalMemorySourceResult.Read(copyLength), physicalAddress, byteCount);
            }

            return PlatformPhysicalMemoryResult.Read(guard, PlatformPhysicalMemorySourceResult.Unavailable(), physicalAddress, byteCount);
        }
    }
}
