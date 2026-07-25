using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;

internal static class UefiExitBootServicesPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("uefi.exitboot plan reports success", TestReportsSuccess, ref failures);
        Run("uefi.exitboot plan requires fresh memory map", TestRequiresFreshMemoryMap, ref failures);
        Run("uefi.exitboot plan retries stale map key", TestRetriesStaleMapKey, ref failures);
        Run("uefi.exitboot plan reports retry exhaustion", TestReportsRetryExhaustion, ref failures);
        Run("uefi.exitboot plan rejects missing owned map", TestRejectsMissingOwnedMap, ref failures);
        Run("uefi.exitboot plan rejects invalid descriptor metadata", TestRejectsInvalidDescriptorMetadata, ref failures);
        Run("uefi.exitboot fresh map plan accepts current key", TestFreshMapPlanAcceptsCurrentKey, ref failures);
        Run("uefi.exitboot fresh map plan rejects stale key", TestFreshMapPlanRejectsStaleKey, ref failures);
        Run("platform.firmware exitboot reports success", TestPlatformFirmwareExitReportsSuccess, ref failures);
        Run("platform.firmware exitboot reports stale map key", TestPlatformFirmwareExitReportsStaleMapKey, ref failures);
        Run(
            "platform.firmware exitboot rejects unavailable services",
            TestPlatformFirmwareExitRejectsUnavailableServices,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("UEFI ExitBootServices plan host tests failed: " + failures);
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

    private static bool TestReportsSuccess()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.ExitSucceeded,
            attemptIndex: 0,
            maximumRetryCount: 3);

        return result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.Success
            && result.HandoffStatus == EfiBootHandoffStatus.Success
            && !result.ShouldRetry
            && result.AttemptIndex == 0
            && result.MaximumRetryCount == 3;
    }

    private static bool TestRequiresFreshMemoryMap()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: false,
            UefiExitBootServicesAttemptOutcome.ExitSucceeded,
            attemptIndex: 0,
            maximumRetryCount: 3);

        return !result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.FreshMemoryMapRequired
            && !result.ShouldRetry;
    }

    private static bool TestRetriesStaleMapKey()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.MapKeyRejected,
            attemptIndex: 1,
            maximumRetryCount: 3);

        return !result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.RetryRequired
            && result.ShouldRetry
            && result.AttemptIndex == 1
            && result.MaximumRetryCount == 3;
    }

    private static bool TestReportsRetryExhaustion()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.MapKeyRejected,
            attemptIndex: 3,
            maximumRetryCount: 3);

        return !result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.RetryExhausted
            && !result.ShouldRetry;
    }

    private static bool TestRejectsMissingOwnedMap()
    {
        BootHandoffRecord record = CreateValidOwnedRecord();
        record.MemoryMap.Flags = 0;

        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            record,
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.ExitSucceeded,
            attemptIndex: 0,
            maximumRetryCount: 3);

        return !result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.MissingOwnedMemoryMap
            && result.HandoffStatus == EfiBootHandoffStatus.MemoryMapNotOwned;
    }

    private static bool TestRejectsInvalidDescriptorMetadata()
    {
        BootHandoffRecord record = CreateValidOwnedRecord();
        record.MemoryMap.DescriptorSize = 56;

        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            record,
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.ExitSucceeded,
            attemptIndex: 0,
            maximumRetryCount: 3);

        return !result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.InvalidDescriptorMetadata
            && result.HandoffStatus == EfiBootHandoffStatus.InvalidDescriptorSize;
    }

    private static bool TestFreshMapPlanAcceptsCurrentKey()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.ExitCallSkipped,
            attemptIndex: 0,
            maximumRetryCount: 3);

        return !result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.ExitCallSkipped
            && result.HandoffStatus == EfiBootHandoffStatus.Success
            && !result.ShouldRetry
            && result.AttemptIndex == 0
            && result.MaximumRetryCount == 3;
    }

    private static bool TestFreshMapPlanRejectsStaleKey()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.MapKeyRejected,
            attemptIndex: 1,
            maximumRetryCount: 3);

        return !result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.RetryRequired
            && result.HandoffStatus == EfiBootHandoffStatus.Success
            && result.ShouldRetry
            && result.AttemptIndex == 1
            && result.MaximumRetryCount == 3;
    }

    private static bool TestPlatformFirmwareExitReportsSuccess()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FirmwareCallResult guard = firmware.RequireBootServices(
            PlatformFirmwareOperations.ExitBootServices);

        PlatformFirmwareExitBootServicesResult result =
            PlatformFirmwareExitBootServicesResult.FromFirmwareStatus(
                guard,
                exitBootServicesFunctionAvailable: true,
                PlatformFirmwareExitBootServicesResult.EfiSuccess);

        return result.Succeeded
            && result.Status == PlatformFirmwareExitBootServicesStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformFirmwareOperations.ExitBootServices
            && result.FirmwareStatus == PlatformFirmwareExitBootServicesResult.EfiSuccess
            && UefiExitBootServicesAttemptMapper.ToAttemptOutcome(result)
                == UefiExitBootServicesAttemptOutcome.ExitSucceeded;
    }

    private static bool TestPlatformFirmwareExitReportsStaleMapKey()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FirmwareCallResult guard = firmware.RequireBootServices(
            PlatformFirmwareOperations.ExitBootServices);

        PlatformFirmwareExitBootServicesResult result =
            PlatformFirmwareExitBootServicesResult.FromFirmwareStatus(
                guard,
                exitBootServicesFunctionAvailable: true,
                PlatformFirmwareExitBootServicesResult.EfiInvalidParameter);

        UefiExitBootServicesPlanResult plan = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptMapper.ToAttemptOutcome(result),
            attemptIndex: 1,
            maximumRetryCount: 3);

        return !result.Succeeded
            && result.Status == PlatformFirmwareExitBootServicesStatus.MapKeyRejected
            && result.FirmwareStatus == PlatformFirmwareExitBootServicesResult.EfiInvalidParameter
            && UefiExitBootServicesAttemptMapper.ToAttemptOutcome(result)
                == UefiExitBootServicesAttemptOutcome.MapKeyRejected
            && plan.Status == UefiExitBootServicesPlanStatus.RetryRequired
            && plan.ShouldRetry;
    }

    private static bool TestPlatformFirmwareExitRejectsUnavailableServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FirmwareCallResult guard = firmware.RequireBootServices(
            PlatformFirmwareOperations.ExitBootServices);
        PlatformFirmwareExitBootServicesResult missingFunction =
            PlatformFirmwareExitBootServicesResult.FromFirmwareStatus(
                guard,
                exitBootServicesFunctionAvailable: false,
                PlatformFirmwareExitBootServicesResult.EfiSuccess);

        _ = firmware.MarkBootServicesExited();
        FirmwareCallResult denied = firmware.RequireBootServices(
            PlatformFirmwareOperations.ExitBootServices);
        PlatformFirmwareExitBootServicesResult guardDenied =
            PlatformFirmwareExitBootServicesResult.FromFirmwareStatus(
                denied,
                exitBootServicesFunctionAvailable: true,
                PlatformFirmwareExitBootServicesResult.EfiSuccess);

        return !missingFunction.Succeeded
            && missingFunction.Status == PlatformFirmwareExitBootServicesStatus.MissingFunctionPointer
            && missingFunction.Mode == FirmwareServicesMode.BootServicesAvailable
            && UefiExitBootServicesAttemptMapper.ToAttemptOutcome(missingFunction)
                == UefiExitBootServicesAttemptOutcome.FirmwareError
            && !guardDenied.Succeeded
            && guardDenied.Status == PlatformFirmwareExitBootServicesStatus.GuardDenied
            && guardDenied.Mode == FirmwareServicesMode.BootServicesExited
            && UefiExitBootServicesAttemptMapper.ToAttemptOutcome(guardDenied)
                == UefiExitBootServicesAttemptOutcome.FirmwareError;
    }

    private static BootHandoffRecord CreateValidOwnedRecord()
    {
        return BootHandoffRecord.CreateUefiWithOwnedMemoryMap(
            firmwareMemoryMapBufferAddress: 0x100000,
            firmwareMemoryMapBufferSize: 4096,
            ownedMemoryMapBufferAddress: 0x900000,
            ownedMemoryMapBufferSize: 128 * 1024,
            memoryMapSize: 48 * 3,
            mapKey: 0x77,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorCount: 3);
    }
}
