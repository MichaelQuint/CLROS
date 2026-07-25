using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class PlatformPhysicalMemoryGuardTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("platform.physical_memory read allowed before UEFI gone", TestAllowedBeforeUefiGone, ref failures);
        Run("platform.physical_memory read denied in UEFI-gone simulation", TestDeniedAfterUefiGoneSimulation, ref failures);
        Run("platform.physical_memory read denied after ExitBootServices", TestDeniedAfterExitBootServices, ref failures);
        Run("platform.physical_memory read rejects invalid range", TestRejectsInvalidRange, ref failures);
        Run("platform.physical_memory read rejects empty destination", TestRejectsEmptyDestination, ref failures);
        Run("platform.physical_memory read reports destination capacity", TestReportsDestinationCapacity, ref failures);
        Run("platform.physical_memory read reports source failure", TestReportsSourceFailure, ref failures);
        Run("platform.physical_memory read reports source unavailable", TestReportsSourceUnavailable, ref failures);
        Run("platform.physical_memory read reports short read", TestReportsShortRead, ref failures);
        Run("platform.physical_memory result contains no managed references", TestResultContainsNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Platform physical-memory guard host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAllowedBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[10];

        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(
            0x1000,
            8,
            destination);

        return result.Succeeded
            && result.Status == PlatformPhysicalMemoryStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformPhysicalMemoryOperations.ReadBytes
            && result.PhysicalAddress == 0x1000
            && result.RequestedBytes == 8
            && result.BytesRead == 8
            && result.RequiredBytes == 8
            && result.FirmwareStatus == 0
            && source.ReadCount == 1
            && source.LastPhysicalAddress == 0x1000
            && source.LastByteCount == 8
            && source.LastDestinationLength == 8
            && destination[0] == 0x90
            && destination[1] == 0x91
            && destination[7] == 0x97
            && destination[8] == 0
            && destination[9] == 0;
    }

    private static bool TestDeniedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];

        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(
            0x1000,
            8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformPhysicalMemoryOperations.ReadBytes
            && result.RequestedBytes == 0
            && result.BytesRead == 0
            && source.ReadCount == 0
            && BufferIsZero(destination);
    }

    private static bool TestDeniedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];

        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(
            0x1000,
            8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && result.Operation == PlatformPhysicalMemoryOperations.ReadBytes
            && result.RequestedBytes == 0
            && result.BytesRead == 0
            && source.ReadCount == 0
            && BufferIsZero(destination);
    }

    private static bool TestRejectsInvalidRange()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];

        PlatformPhysicalMemoryResult zeroAddress = physicalMemory.ReadBytes(
            0,
            8,
            destination);
        PlatformPhysicalMemoryResult zeroLength = physicalMemory.ReadBytes(
            0x1000,
            0,
            destination);
        PlatformPhysicalMemoryResult overflow = physicalMemory.ReadBytes(
            ulong.MaxValue - 3,
            8,
            destination);

        return !zeroAddress.Succeeded
            && zeroAddress.Status == PlatformPhysicalMemoryStatus.InvalidRange
            && zeroAddress.PhysicalAddress == 0
            && zeroAddress.RequestedBytes == 8
            && !zeroLength.Succeeded
            && zeroLength.Status == PlatformPhysicalMemoryStatus.InvalidRange
            && zeroLength.RequiredBytes == 0
            && !overflow.Succeeded
            && overflow.Status == PlatformPhysicalMemoryStatus.InvalidRange
            && source.ReadCount == 0
            && BufferIsZero(destination);
    }

    private static bool TestRejectsEmptyDestination()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 1);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);

        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(
            0x1000,
            1,
            Span<byte>.Empty);

        return !result.Succeeded
            && result.Status == PlatformPhysicalMemoryStatus.InvalidDestination
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.PhysicalAddress == 0x1000
            && result.RequestedBytes == 1
            && result.RequiredBytes == 1
            && source.ReadCount == 0;
    }

    private static bool TestReportsDestinationCapacity()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(
            0x1000,
            8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformPhysicalMemoryStatus.DestinationTooSmall
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.PhysicalAddress == 0x1000
            && result.RequestedBytes == 8
            && result.RequiredBytes == 8
            && result.BytesRead == 0
            && source.ReadCount == 0
            && BufferIsZero(destination);
    }

    private static bool TestReportsSourceFailure()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        const ulong firmwareStatus = 0x8000000000000007;
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Fault(firmwareStatus);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];

        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(
            0x1000,
            8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformPhysicalMemoryStatus.SourceFailed
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.PhysicalAddress == 0x1000
            && result.RequestedBytes == 8
            && result.BytesRead == 0
            && result.FirmwareStatus == firmwareStatus
            && source.ReadCount == 1
            && BufferIsZero(destination);
    }

    private static bool TestReportsSourceUnavailable()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Unavailable();
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];

        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(
            0x1000,
            8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformPhysicalMemoryStatus.SourceUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.PhysicalAddress == 0x1000
            && result.RequestedBytes == 8
            && result.BytesRead == 0
            && source.ReadCount == 1
            && BufferIsZero(destination);
    }

    private static bool TestReportsShortRead()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x40, bytesToRead: 4);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];

        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(
            0x1000,
            8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformPhysicalMemoryStatus.ShortRead
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.PhysicalAddress == 0x1000
            && result.RequestedBytes == 8
            && result.BytesRead == 4
            && result.RequiredBytes == 8
            && source.ReadCount == 1
            && destination[0] == 0x40
            && destination[1] == 0x41
            && destination[3] == 0x43
            && destination[4] == 0
            && destination[7] == 0;
    }

    private static bool TestResultContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformPhysicalMemorySourceResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformPhysicalMemoryResult>();
    }

    private static bool BufferIsZero(ReadOnlySpan<byte> buffer)
    {
        for (int index = 0; index < buffer.Length; index++)
        {
            if (buffer[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class PhysicalMemorySourceDouble : IPlatformPhysicalMemorySource
    {
        private readonly byte _firstByte;
        private readonly int _bytesToRead;
        private readonly PlatformPhysicalMemorySourceResult _result;

        private PhysicalMemorySourceDouble(
            byte firstByte,
            int bytesToRead,
            PlatformPhysicalMemorySourceResult result)
        {
            _firstByte = firstByte;
            _bytesToRead = bytesToRead;
            _result = result;
        }

        public int ReadCount { get; private set; }

        public ulong LastPhysicalAddress { get; private set; }

        public int LastByteCount { get; private set; }

        public int LastDestinationLength { get; private set; }

        public static PhysicalMemorySourceDouble Success(byte firstByte, int bytesToRead)
        {
            return new PhysicalMemorySourceDouble(
                firstByte,
                bytesToRead,
                PlatformPhysicalMemorySourceResult.Read(bytesToRead));
        }

        public static PhysicalMemorySourceDouble Unavailable()
        {
            return new PhysicalMemorySourceDouble(
                0,
                0,
                PlatformPhysicalMemorySourceResult.Unavailable());
        }

        public static PhysicalMemorySourceDouble Fault(ulong firmwareStatus)
        {
            return new PhysicalMemorySourceDouble(
                0,
                0,
                PlatformPhysicalMemorySourceResult.Fault(firmwareStatus));
        }

        public PlatformPhysicalMemorySourceResult ReadBytes(
            ulong physicalAddress,
            int byteCount,
            Span<byte> destination)
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
