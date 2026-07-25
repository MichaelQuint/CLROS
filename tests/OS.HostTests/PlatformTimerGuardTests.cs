using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class PlatformTimerGuardTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("platform.timer monotonic allowed before UEFI gone", TestAllowedBeforeUefiGone, ref failures);
        Run("platform.timer monotonic denied in UEFI-gone simulation", TestDeniedAfterUefiGoneSimulation, ref failures);
        Run("platform.timer monotonic denied after ExitBootServices", TestDeniedAfterExitBootServices, ref failures);
        Run("platform.timer monotonic reports invalid metadata", TestInvalidMetadataStatus, ref failures);
        Run("platform.timer metadata contains no managed references", TestMetadataContainsNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Platform timer guard host tests failed: " + failures);
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
        TimerSourceDouble source = new TimerSourceDouble(new PlatformTimerMetadata(12345, 10000000));
        IPlatformTimer timer = new GuardedPlatformTimer(firmware, source);

        PlatformTimerResult result = timer.ReadMonotonic();

        return result.Succeeded
            && result.Status == PlatformTimerStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformTimerOperations.ReadMonotonic
            && result.MonotonicTicks == 12345
            && result.FrequencyHertz == 10000000
            && source.ReadCount == 1;
    }

    private static bool TestDeniedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        TimerSourceDouble source = new TimerSourceDouble(new PlatformTimerMetadata(12345, 10000000));
        IPlatformTimer timer = new GuardedPlatformTimer(firmware, source);

        PlatformTimerResult result = timer.ReadMonotonic();

        return !result.Succeeded
            && result.Status == PlatformTimerStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformTimerOperations.ReadMonotonic
            && result.MonotonicTicks == 0
            && result.FrequencyHertz == 0
            && source.ReadCount == 0;
    }

    private static bool TestDeniedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        TimerSourceDouble source = new TimerSourceDouble(new PlatformTimerMetadata(12345, 10000000));
        IPlatformTimer timer = new GuardedPlatformTimer(firmware, source);

        PlatformTimerResult result = timer.ReadMonotonic();

        return !result.Succeeded
            && result.Status == PlatformTimerStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && result.Operation == PlatformTimerOperations.ReadMonotonic
            && result.MonotonicTicks == 0
            && result.FrequencyHertz == 0
            && source.ReadCount == 0;
    }

    private static bool TestInvalidMetadataStatus()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        TimerSourceDouble source = new TimerSourceDouble(new PlatformTimerMetadata(12345, 0));
        IPlatformTimer timer = new GuardedPlatformTimer(firmware, source);

        PlatformTimerResult result = timer.ReadMonotonic();

        return !result.Succeeded
            && result.Status == PlatformTimerStatus.InvalidMetadata
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformTimerOperations.ReadMonotonic
            && result.MonotonicTicks == 0
            && result.FrequencyHertz == 0
            && source.ReadCount == 1;
    }

    private static bool TestMetadataContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformTimerMetadata>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformTimerResult>();
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
