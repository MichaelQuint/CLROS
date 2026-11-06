using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformPhysicalMemoryGuardTests
{
    [HostTest("platform.physical_memory read allowed before UEFI gone", HostLane.Fast, 0)]
    private static bool TestAllowedBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[10];
        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(0x1000, 8, destination);
        return HostCheck.Result(result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.Success && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.Operation == PlatformPhysicalMemoryOperations.ReadBytes && result.PhysicalAddress == 0x1000 && result.RequestedBytes == 8 && result.BytesRead == 8 && result.RequiredBytes == 8 && result.FirmwareStatus == 0 && source.ReadCount == 1 && source.LastPhysicalAddress == 0x1000 && source.LastByteCount == 8 && source.LastDestinationLength == 8 && destination[0] == 0x90 && destination[1] == 0x91 && destination[7] == 0x97 && destination[8] == 0 && destination[9] == 0);
    }

    [HostTest("platform.physical_memory read denied in UEFI-gone simulation", HostLane.Fast, 1)]
    private static bool TestDeniedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];
        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(0x1000, 8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Operation == PlatformPhysicalMemoryOperations.ReadBytes && result.RequestedBytes == 0 && result.BytesRead == 0 && source.ReadCount == 0 && BufferIsZero(destination));
    }

    [HostTest("platform.physical_memory read denied after ExitBootServices", HostLane.Fast, 2)]
    private static bool TestDeniedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];
        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(0x1000, 8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.BootServicesExited && result.Operation == PlatformPhysicalMemoryOperations.ReadBytes && result.RequestedBytes == 0 && result.BytesRead == 0 && source.ReadCount == 0 && BufferIsZero(destination));
    }

    [HostTest("platform.physical_memory read rejects invalid range", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidRange()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];
        PlatformPhysicalMemoryResult zeroAddress = physicalMemory.ReadBytes(0, 8, destination);
        PlatformPhysicalMemoryResult zeroLength = physicalMemory.ReadBytes(0x1000, 0, destination);
        PlatformPhysicalMemoryResult overflow = physicalMemory.ReadBytes(ulong.MaxValue - 3, 8, destination);
        return HostCheck.Result(!zeroAddress.Succeeded && zeroAddress.Status == PlatformPhysicalMemoryStatus.InvalidRange && zeroAddress.PhysicalAddress == 0 && zeroAddress.RequestedBytes == 8 && !zeroLength.Succeeded && zeroLength.Status == PlatformPhysicalMemoryStatus.InvalidRange && zeroLength.RequiredBytes == 0 && !overflow.Succeeded && overflow.Status == PlatformPhysicalMemoryStatus.InvalidRange && source.ReadCount == 0 && BufferIsZero(destination));
    }

    [HostTest("platform.physical_memory read rejects empty destination", HostLane.Fast, 4)]
    private static bool TestRejectsEmptyDestination()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 1);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(0x1000, 1, Span<byte>.Empty);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.InvalidDestination && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.PhysicalAddress == 0x1000 && result.RequestedBytes == 1 && result.RequiredBytes == 1 && source.ReadCount == 0);
    }

    [HostTest("platform.physical_memory read reports destination capacity", HostLane.Fast, 5)]
    private static bool TestReportsDestinationCapacity()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x90, bytesToRead: 8);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[4];
        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(0x1000, 8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.DestinationTooSmall && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.PhysicalAddress == 0x1000 && result.RequestedBytes == 8 && result.RequiredBytes == 8 && result.BytesRead == 0 && source.ReadCount == 0 && BufferIsZero(destination));
    }

    [HostTest("platform.physical_memory read reports source failure", HostLane.Fast, 6)]
    private static bool TestReportsSourceFailure()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        const ulong firmwareStatus = 0x8000000000000007;
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Fault(firmwareStatus);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];
        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(0x1000, 8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.SourceFailed && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.PhysicalAddress == 0x1000 && result.RequestedBytes == 8 && result.BytesRead == 0 && result.FirmwareStatus == firmwareStatus && source.ReadCount == 1 && BufferIsZero(destination));
    }

    [HostTest("platform.physical_memory read reports source unavailable", HostLane.Fast, 7)]
    private static bool TestReportsSourceUnavailable()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Unavailable();
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];
        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(0x1000, 8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.SourceUnavailable && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.PhysicalAddress == 0x1000 && result.RequestedBytes == 8 && result.BytesRead == 0 && source.ReadCount == 1 && BufferIsZero(destination));
    }

    [HostTest("platform.physical_memory read reports short read", HostLane.Fast, 8)]
    private static bool TestReportsShortRead()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        PhysicalMemorySourceDouble source = PhysicalMemorySourceDouble.Success(0x40, bytesToRead: 4);
        IPlatformPhysicalMemory physicalMemory = new GuardedPlatformPhysicalMemory(firmware, source);
        Span<byte> destination = stackalloc byte[8];
        PlatformPhysicalMemoryResult result = physicalMemory.ReadBytes(0x1000, 8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.ShortRead && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.PhysicalAddress == 0x1000 && result.RequestedBytes == 8 && result.BytesRead == 4 && result.RequiredBytes == 8 && source.ReadCount == 1 && destination[0] == 0x40 && destination[1] == 0x41 && destination[3] == 0x43 && destination[4] == 0 && destination[7] == 0);
    }

    [HostTest("platform.physical_memory result contains no managed references", HostLane.Fast, 9)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<PlatformPhysicalMemorySourceResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformPhysicalMemoryResult>());
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
}
