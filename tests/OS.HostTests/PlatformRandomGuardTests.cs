using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class PlatformRandomGuardTests
{

    [HostTest("platform.random fill allowed before UEFI gone", HostLane.Fast, 0)]
    private static bool TestAllowedBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        RandomSourceDouble source = RandomSourceDouble.Success(0x5a, bytesToWrite: 4);
        IPlatformRandom random = new GuardedPlatformRandom(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformRandomResult result = random.FillBytes(destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == PlatformRandomStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformRandomOperations.FillBytes
            && result.RequestedBytes == 4
            && result.BytesWritten == 4
            && result.FirmwareStatus == 0
            && source.FillCount == 1
            && destination[0] == 0x5a
            && destination[1] == 0x5b
            && destination[2] == 0x5c
            && destination[3] == 0x5d);
    }

    [HostTest("platform.random fill denied in UEFI-gone simulation", HostLane.Fast, 1)]
    private static bool TestDeniedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        RandomSourceDouble source = RandomSourceDouble.Success(0x5a, bytesToWrite: 4);
        IPlatformRandom random = new GuardedPlatformRandom(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformRandomResult result = random.FillBytes(destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformRandomStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformRandomOperations.FillBytes
            && result.RequestedBytes == 0
            && result.BytesWritten == 0
            && source.FillCount == 0
            && BufferIsZero(destination));
    }

    [HostTest("platform.random fill denied after ExitBootServices", HostLane.Fast, 2)]
    private static bool TestDeniedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        RandomSourceDouble source = RandomSourceDouble.Success(0x5a, bytesToWrite: 4);
        IPlatformRandom random = new GuardedPlatformRandom(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformRandomResult result = random.FillBytes(destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformRandomStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && result.Operation == PlatformRandomOperations.FillBytes
            && result.RequestedBytes == 0
            && result.BytesWritten == 0
            && source.FillCount == 0
            && BufferIsZero(destination));
    }

    [HostTest("platform.random fill rejects empty destination", HostLane.Fast, 3)]
    private static bool TestRejectsEmptyDestination()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        RandomSourceDouble source = RandomSourceDouble.Success(0x5a, bytesToWrite: 1);
        IPlatformRandom random = new GuardedPlatformRandom(firmware, source);

        PlatformRandomResult result = random.FillBytes(Span<byte>.Empty);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformRandomStatus.InvalidDestination
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformRandomOperations.FillBytes
            && result.RequestedBytes == 0
            && result.BytesWritten == 0
            && source.FillCount == 0);
    }

    [HostTest("platform.random fill reports source failure", HostLane.Fast, 4)]
    private static bool TestReportsSourceFailure()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        const ulong firmwareStatus = 0x8000000000000003;
        RandomSourceDouble source = RandomSourceDouble.FirmwareError(firmwareStatus);
        IPlatformRandom random = new GuardedPlatformRandom(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformRandomResult result = random.FillBytes(destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformRandomStatus.SourceFailed
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformRandomOperations.FillBytes
            && result.RequestedBytes == 4
            && result.BytesWritten == 0
            && result.FirmwareStatus == firmwareStatus
            && source.FillCount == 1
            && BufferIsZero(destination));
    }

    [HostTest("platform.random fill reports short read", HostLane.Fast, 5)]
    private static bool TestReportsShortRead()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        RandomSourceDouble source = RandomSourceDouble.Success(0x30, bytesToWrite: 2);
        IPlatformRandom random = new GuardedPlatformRandom(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformRandomResult result = random.FillBytes(destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformRandomStatus.ShortRead
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformRandomOperations.FillBytes
            && result.RequestedBytes == 4
            && result.BytesWritten == 2
            && source.FillCount == 1
            && destination[0] == 0x30
            && destination[1] == 0x31
            && destination[2] == 0
            && destination[3] == 0);
    }

    [HostTest("platform.random result contains no managed references", HostLane.Fast, 6)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<PlatformRandomSourceResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformRandomResult>());
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

    private sealed class RandomSourceDouble : IPlatformRandomSource
    {
        private readonly byte _firstByte;
        private readonly int _bytesToWrite;
        private readonly PlatformRandomSourceResult _result;

        private RandomSourceDouble(
            byte firstByte,
            int bytesToWrite,
            PlatformRandomSourceResult result)
        {
            _firstByte = firstByte;
            _bytesToWrite = bytesToWrite;
            _result = result;
        }

        public int FillCount { get; private set; }

        public static RandomSourceDouble Success(byte firstByte, int bytesToWrite)
        {
            return new RandomSourceDouble(
                firstByte,
                bytesToWrite,
                PlatformRandomSourceResult.Filled(bytesToWrite));
        }

        public static RandomSourceDouble FirmwareError(ulong firmwareStatus)
        {
            return new RandomSourceDouble(
                0,
                0,
                PlatformRandomSourceResult.FirmwareError(firmwareStatus));
        }

        public PlatformRandomSourceResult FillBytes(Span<byte> destination)
        {
            FillCount++;
            int bytesToWrite = Math.Min(_bytesToWrite, destination.Length);
            for (int index = 0; index < bytesToWrite; index++)
            {
                destination[index] = (byte)(_firstByte + index);
            }

            return _result;
        }
    }
}
