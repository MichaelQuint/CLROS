using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class AcpiRootSdtSnapshotTests
{
    private sealed class PhysicalMemoryDouble : IPlatformPhysicalMemory
    {
        private readonly PhysicalMemoryEntry[] _entries;
        private readonly int _failOnCall;
        private readonly PlatformPhysicalMemoryStatus _failureStatus;
        private PhysicalMemoryDouble(PhysicalMemoryEntry[] entries, int failOnCall, PlatformPhysicalMemoryStatus failureStatus)
        {
            _entries = entries;
            _failOnCall = failOnCall;
            _failureStatus = failureStatus;
        }

        public int ReadCount
        {
            get; private set;
        }
        public int[] RequestedBytes { get; } = new int[8];

        public static PhysicalMemoryDouble Success(params PhysicalMemoryEntry[] entries)
        {
            return new PhysicalMemoryDouble(entries, failOnCall: -1, PlatformPhysicalMemoryStatus.Success);
        }

        public static PhysicalMemoryDouble FailOnCall(int failOnCall, PlatformPhysicalMemoryStatus failureStatus, params PhysicalMemoryEntry[] entries)
        {
            return new PhysicalMemoryDouble(entries, failOnCall, failureStatus);
        }

        public PlatformPhysicalMemoryResult ReadBytes(ulong physicalAddress, int byteCount, Span<byte> destination)
        {
            ReadCount++;
            RequestedBytes[ReadCount - 1] = byteCount;
            FirmwareCallResult guard = FirmwareCallResult.Allowed(PlatformPhysicalMemoryOperations.ReadBytes, FirmwareServicesMode.BootServicesAvailable);
            if (ReadCount == _failOnCall)
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
