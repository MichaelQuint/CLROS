using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;

internal static class PlatformMemoryMapGuardTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("platform.memorymap allows snapshot copy before UEFI gone", TestAllowedBeforeUefiGone, ref failures);
        Run("platform.memorymap rejects snapshot copy in UEFI-gone simulation", TestRejectedAfterUefiGoneSimulation, ref failures);
        Run("platform.memorymap rejects snapshot copy after ExitBootServices", TestRejectedAfterExitBootServices, ref failures);
        Run("platform.memorymap reports invalid snapshot as status", TestInvalidSnapshotStatus, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Platform memory-map guard host tests failed: " + failures);
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
        IPlatformMemoryMap memoryMap = new GuardedMemoryMapDouble(firmware, CreateValidSnapshot());

        PlatformMemoryMapResult result = memoryMap.CopySnapshot();

        return result.Succeeded
            && result.Status == PlatformMemoryMapStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformMemoryMapOperations.GetMemoryMap
            && result.Message.Length == 0
            && result.Snapshot.MapKey == 0x55
            && result.Snapshot.DescriptorCount == 1;
    }

    private static bool TestRejectedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        IPlatformMemoryMap memoryMap = new GuardedMemoryMapDouble(firmware, CreateValidSnapshot());

        PlatformMemoryMapResult result = memoryMap.CopySnapshot();

        return !result.Succeeded
            && result.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformMemoryMapOperations.GetMemoryMap
            && result.Snapshot.DescriptorCount == 0
            && result.Message.Contains("UefiGoneSimulated", StringComparison.Ordinal);
    }

    private static bool TestRejectedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        IPlatformMemoryMap memoryMap = new GuardedMemoryMapDouble(firmware, CreateValidSnapshot());

        PlatformMemoryMapResult result = memoryMap.CopySnapshot();

        return !result.Succeeded
            && result.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && result.Operation == PlatformMemoryMapOperations.GetMemoryMap
            && result.Snapshot.DescriptorCount == 0
            && result.Message.Contains("BootServicesExited", StringComparison.Ordinal);
    }

    private static bool TestInvalidSnapshotStatus()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        IPlatformMemoryMap memoryMap = new GuardedMemoryMapDouble(firmware, default);

        PlatformMemoryMapResult result = memoryMap.CopySnapshot();

        return !result.Succeeded
            && result.Status == PlatformMemoryMapStatus.InvalidSnapshot
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformMemoryMapOperations.GetMemoryMap
            && result.Message.Contains("descriptor entries", StringComparison.Ordinal);
    }

    private static UefiMemoryMapSnapshot CreateValidSnapshot()
    {
        UefiMemoryDescriptor[] descriptors =
        [
            new UefiMemoryDescriptor(
                UefiMemoryType.BootServicesData,
                0x100000,
                0,
                2,
                0)
        ];

        UefiMemoryMapResult result = UefiMemoryMapResult.Captured(
            mapKey: 0x55,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorEntries: descriptors);

        return result.Snapshot;
    }

    private sealed class GuardedMemoryMapDouble : IPlatformMemoryMap
    {
        private readonly IPlatformFirmware _firmware;
        private readonly UefiMemoryMapSnapshot _snapshot;

        public GuardedMemoryMapDouble(IPlatformFirmware firmware, UefiMemoryMapSnapshot snapshot)
        {
            _firmware = firmware;
            _snapshot = snapshot;
        }

        public PlatformMemoryMapResult CopySnapshot()
        {
            FirmwareCallResult guardResult = _firmware.RequireBootServices(
                PlatformMemoryMapOperations.GetMemoryMap);

            return PlatformMemoryMapResult.Copied(guardResult, _snapshot);
        }
    }
}
