using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class AcpiRsdpSnapshotTests
{
    private sealed class PhysicalMemoryDouble : IPlatformPhysicalMemory
    {
        private readonly byte[] _source;
        private readonly int _failOnCall;
        private readonly PlatformPhysicalMemoryStatus _failureStatus;
        private PhysicalMemoryDouble(byte[] source, int failOnCall, PlatformPhysicalMemoryStatus failureStatus)
        {
            _source = source;
            _failOnCall = failOnCall;
            _failureStatus = failureStatus;
        }

        public int ReadCount { get; private set; }
        public int[] RequestedBytes { get; } = new int[8];

        public static PhysicalMemoryDouble Success(byte[] source)
        {
            return new PhysicalMemoryDouble(source, failOnCall: -1, PlatformPhysicalMemoryStatus.Success);
        }

        public static PhysicalMemoryDouble FailOnCall(byte[] source, int failOnCall, PlatformPhysicalMemoryStatus failureStatus)
        {
            return new PhysicalMemoryDouble(source, failOnCall, failureStatus);
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

            int copyLength = Math.Min(byteCount, _source.Length);
            _source.AsSpan(0, copyLength).CopyTo(destination);
            return PlatformPhysicalMemoryResult.Read(guard, PlatformPhysicalMemorySourceResult.Read(copyLength), physicalAddress, byteCount);
        }
    }
}
