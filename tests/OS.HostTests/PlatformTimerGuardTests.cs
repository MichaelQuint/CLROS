using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class PlatformTimerGuardTests
{

    [HostTest("platform.timer monotonic allowed before UEFI gone", HostLane.Fast, 0)]
    private static bool TestAllowedBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        TimerSourceDouble source = new TimerSourceDouble(new PlatformTimerMetadata(12345, 10000000));
        IPlatformTimer timer = new GuardedPlatformTimer(firmware, source);

        PlatformTimerResult result = timer.ReadMonotonic();

        return HostCheck.Result(result.Succeeded
            && result.Status == PlatformTimerStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformTimerOperations.ReadMonotonic
            && result.MonotonicTicks == 12345
            && result.FrequencyHertz == 10000000
            && source.ReadCount == 1);
    }

    [HostTest("platform.timer monotonic denied in UEFI-gone simulation", HostLane.Fast, 1)]
    private static bool TestDeniedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        TimerSourceDouble source = new TimerSourceDouble(new PlatformTimerMetadata(12345, 10000000));
        IPlatformTimer timer = new GuardedPlatformTimer(firmware, source);

        PlatformTimerResult result = timer.ReadMonotonic();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformTimerStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformTimerOperations.ReadMonotonic
            && result.MonotonicTicks == 0
            && result.FrequencyHertz == 0
            && source.ReadCount == 0);
    }

    [HostTest("platform.timer monotonic denied after ExitBootServices", HostLane.Fast, 2)]
    private static bool TestDeniedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        TimerSourceDouble source = new TimerSourceDouble(new PlatformTimerMetadata(12345, 10000000));
        IPlatformTimer timer = new GuardedPlatformTimer(firmware, source);

        PlatformTimerResult result = timer.ReadMonotonic();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformTimerStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && result.Operation == PlatformTimerOperations.ReadMonotonic
            && result.MonotonicTicks == 0
            && result.FrequencyHertz == 0
            && source.ReadCount == 0);
    }

    [HostTest("platform.timer monotonic reports invalid metadata", HostLane.Fast, 3)]
    private static bool TestInvalidMetadataStatus()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        TimerSourceDouble source = new TimerSourceDouble(new PlatformTimerMetadata(12345, 0));
        IPlatformTimer timer = new GuardedPlatformTimer(firmware, source);

        PlatformTimerResult result = timer.ReadMonotonic();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformTimerStatus.InvalidMetadata
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformTimerOperations.ReadMonotonic
            && result.MonotonicTicks == 0
            && result.FrequencyHertz == 0
            && source.ReadCount == 1);
    }

    [HostTest("platform.timer metadata contains no managed references", HostLane.Fast, 4)]
    private static bool TestMetadataContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<PlatformTimerMetadata>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformTimerResult>());
    }

    private sealed class TimerSourceDouble : IPlatformTimerSource
    {
        private readonly PlatformTimerMetadata _metadata;

        public TimerSourceDouble(PlatformTimerMetadata metadata)
        {
            _metadata = metadata;
        }

        public int ReadCount { get; private set; }

        public PlatformTimerMetadata ReadMonotonic()
        {
            ReadCount++;
            return _metadata;
        }
    }
}
