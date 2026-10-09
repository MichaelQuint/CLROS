using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;

internal static class UefiExitBootServicesPlanTests
{

    [HostTest("uefi.exitboot plan reports success", HostLane.Fast, 0)]
    private static bool TestReportsSuccess()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.ExitSucceeded,
            attemptIndex: 0,
            maximumRetryCount: 3);

        return HostCheck.Result(result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.Success
            && result.HandoffStatus == EfiBootHandoffStatus.Success
            && !result.ShouldRetry
            && result.AttemptIndex == 0
            && result.MaximumRetryCount == 3);
    }

    [HostTest("uefi.exitboot plan requires fresh memory map", HostLane.Fast, 1)]
    private static bool TestRequiresFreshMemoryMap()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: false,
            UefiExitBootServicesAttemptOutcome.ExitSucceeded,
            attemptIndex: 0,
            maximumRetryCount: 3);

        return HostCheck.Result(!result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.FreshMemoryMapRequired
            && !result.ShouldRetry);
    }

    [HostTest("uefi.exitboot plan retries stale map key", HostLane.Fast, 2)]
    private static bool TestRetriesStaleMapKey()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.MapKeyRejected,
            attemptIndex: 1,
            maximumRetryCount: 3);

        return HostCheck.Result(!result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.RetryRequired
            && result.ShouldRetry
            && result.AttemptIndex == 1
            && result.MaximumRetryCount == 3);
    }

    [HostTest("uefi.exitboot plan reports retry exhaustion", HostLane.Fast, 3)]
    private static bool TestReportsRetryExhaustion()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.MapKeyRejected,
            attemptIndex: 3,
            maximumRetryCount: 3);

        return HostCheck.Result(!result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.RetryExhausted
            && !result.ShouldRetry);
    }

    [HostTest("uefi.exitboot plan rejects missing owned map", HostLane.Fast, 4)]
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

        return HostCheck.Result(!result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.MissingOwnedMemoryMap
            && result.HandoffStatus == EfiBootHandoffStatus.MemoryMapNotOwned);
    }

    [HostTest("uefi.exitboot plan rejects invalid descriptor metadata", HostLane.Fast, 5)]
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

        return HostCheck.Result(!result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.InvalidDescriptorMetadata
            && result.HandoffStatus == EfiBootHandoffStatus.InvalidDescriptorSize);
    }

    [HostTest("uefi.exitboot fresh map plan accepts current key", HostLane.Fast, 6)]
    private static bool TestFreshMapPlanAcceptsCurrentKey()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.ExitCallSkipped,
            attemptIndex: 0,
            maximumRetryCount: 3);

        return HostCheck.Result(!result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.ExitCallSkipped
            && result.HandoffStatus == EfiBootHandoffStatus.Success
            && !result.ShouldRetry
            && result.AttemptIndex == 0
            && result.MaximumRetryCount == 3);
    }

    [HostTest("uefi.exitboot fresh map plan rejects stale key", HostLane.Fast, 7)]
    private static bool TestFreshMapPlanRejectsStaleKey()
    {
        UefiExitBootServicesPlanResult result = UefiExitBootServicesPlan.Evaluate(
            CreateValidOwnedRecord(),
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.MapKeyRejected,
            attemptIndex: 1,
            maximumRetryCount: 3);

        return HostCheck.Result(!result.Succeeded
            && result.Status == UefiExitBootServicesPlanStatus.RetryRequired
            && result.HandoffStatus == EfiBootHandoffStatus.Success
            && result.ShouldRetry
            && result.AttemptIndex == 1
            && result.MaximumRetryCount == 3);
    }

    [HostTest("platform.firmware exitboot reports success", HostLane.Fast, 8)]
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

        return HostCheck.Result(result.Succeeded
            && result.Status == PlatformFirmwareExitBootServicesStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformFirmwareOperations.ExitBootServices
            && result.FirmwareStatus == PlatformFirmwareExitBootServicesResult.EfiSuccess
            && UefiExitBootServicesAttemptMapper.ToAttemptOutcome(result)
                == UefiExitBootServicesAttemptOutcome.ExitSucceeded);
    }

    [HostTest("platform.firmware exitboot reports stale map key", HostLane.Fast, 9)]
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

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformFirmwareExitBootServicesStatus.MapKeyRejected
            && result.FirmwareStatus == PlatformFirmwareExitBootServicesResult.EfiInvalidParameter
            && UefiExitBootServicesAttemptMapper.ToAttemptOutcome(result)
                == UefiExitBootServicesAttemptOutcome.MapKeyRejected
            && plan.Status == UefiExitBootServicesPlanStatus.RetryRequired
            && plan.ShouldRetry);
    }

    [HostTest("platform.firmware exitboot rejects unavailable services", HostLane.Fast, 10)]
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

        return HostCheck.Result(!missingFunction.Succeeded
            && missingFunction.Status == PlatformFirmwareExitBootServicesStatus.MissingFunctionPointer
            && missingFunction.Mode == FirmwareServicesMode.BootServicesAvailable
            && UefiExitBootServicesAttemptMapper.ToAttemptOutcome(missingFunction)
                == UefiExitBootServicesAttemptOutcome.FirmwareError
            && !guardDenied.Succeeded
            && guardDenied.Status == PlatformFirmwareExitBootServicesStatus.GuardDenied
            && guardDenied.Mode == FirmwareServicesMode.BootServicesExited
            && UefiExitBootServicesAttemptMapper.ToAttemptOutcome(guardDenied)
                == UefiExitBootServicesAttemptOutcome.FirmwareError);
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
