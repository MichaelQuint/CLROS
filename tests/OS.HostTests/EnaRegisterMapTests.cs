using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class EnaRegisterMapTests
{

    [HostTest("drivers.ena.registermap rejects default results", HostLane.Fast, 4)]
    private static bool TestRejectsDefaultResults()
    {
        EnaRegisterApertureValidationResult aperture = default;
        EnaAdminReadinessResult readiness = default;

        return HostCheck.Result(!aperture.IsInitialized
            && !aperture.Succeeded
            && aperture.Status == EnaRegisterApertureValidationStatus.Success
            && !readiness.IsInitialized
            && !readiness.Succeeded
            && readiness.Status == EnaAdminReadinessStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaRegisterApertureValidationResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<EnaAdminReadinessResult>());
    }

    [HostTest("drivers.ena.registermap register offsets", HostLane.Fast, 0)]
    private static bool TestRegisterOffsets()
    {
        return HostCheck.Result(EnaRegisterMap.RegisterBaseOffset == 0x0000
            && EnaRegisterMap.RegisterWidth == 4
            && EnaRegisterMap.Version.Offset == 0x0000
            && EnaRegisterMap.Version.Width == 4
            && EnaRegisterMap.ControllerVersion.Offset == 0x0004
            && EnaRegisterMap.ControllerVersion.Width == 4
            && EnaRegisterMap.Capabilities.Offset == 0x0008
            && EnaRegisterMap.Capabilities.Width == 4
            && EnaRegisterMap.CapabilitiesExtension.Offset == 0x000C
            && EnaRegisterMap.AdminSubmissionQueueBase.LowOffset == 0x0010
            && EnaRegisterMap.AdminSubmissionQueueBase.HighOffset == 0x0014
            && EnaRegisterMap.AdminSubmissionQueueBase.Width == 8
            && EnaRegisterMap.AdminQueueCapabilities.Offset == 0x0018
            && EnaRegisterMap.AdminCompletionQueueBase.LowOffset == 0x0020
            && EnaRegisterMap.AdminCompletionQueueBase.HighOffset == 0x0024
            && EnaRegisterMap.AdminCompletionQueueBase.Width == 8
            && EnaRegisterMap.AdminCompletionQueueCapabilities.Offset == 0x0028
            && EnaRegisterMap.AdminSubmissionQueueDoorbell.Offset == 0x002C
            && EnaRegisterMap.AdminCompletionQueueTail.Offset == 0x0030
            && EnaRegisterMap.AsyncEventNotificationQueueCapabilities.Offset == 0x0034
            && EnaRegisterMap.AsyncEventNotificationQueueBase.LowOffset == 0x0038
            && EnaRegisterMap.AsyncEventNotificationQueueBase.HighOffset == 0x003C
            && EnaRegisterMap.AsyncEventNotificationQueueHeadDoorbell.Offset == 0x0040
            && EnaRegisterMap.AsyncEventNotificationQueueTail.Offset == 0x0044
            && EnaRegisterMap.InterruptMask.Offset == 0x004C
            && EnaRegisterMap.DeviceControl.Offset == 0x0054
            && EnaRegisterMap.DeviceStatus.Offset == 0x0058
            && EnaRegisterMap.MmioRegisterRead.Offset == 0x005C
            && EnaRegisterMap.MmioResponse.LowOffset == 0x0060
            && EnaRegisterMap.MmioResponse.HighOffset == 0x0064
            && EnaRegisterMap.RssIndirectionEntryUpdate.Offset == 0x0068
            && EnaRegisterMap.PhcDoorbell.Offset == 0x0100);
    }

    [HostTest("drivers.ena.registermap capability and status bits", HostLane.Fast, 1)]
    private static bool TestCapabilityAndStatusBits()
    {
        uint capabilities =
            EnaRegisterMap.Capabilities.ContiguousQueueRequired
            | (0x0Bu << EnaRegisterMap.Capabilities.ResetTimeoutShift)
            | (48u << EnaRegisterMap.Capabilities.DmaAddressWidthShift)
            | (0x05u << EnaRegisterMap.Capabilities.AdminCommandTimeoutShift);
        uint adminQueueCaps = 128u | (64u << EnaRegisterMap.AdminQueueCapabilities.EntrySizeShift);
        uint adminCompletionQueueCaps =
            256u | (16u << EnaRegisterMap.AdminCompletionQueueCapabilities.EntrySizeShift);

        return HostCheck.Result(EnaRegisterMap.Version.MinorVersionMask == 0x000000FF
            && EnaRegisterMap.Version.MajorVersionShift == 8
            && EnaRegisterMap.Version.MajorVersionMask == 0x0000FF00
            && EnaRegisterMap.ControllerVersion.SubminorVersionMask == 0x000000FF
            && EnaRegisterMap.ControllerVersion.MinorVersionShift == 8
            && EnaRegisterMap.ControllerVersion.MajorVersionShift == 16
            && EnaRegisterMap.ControllerVersion.ImplementationIdShift == 24
            && EnaRegisterMap.Capabilities.ContiguousQueueRequired == 0x00000001
            && EnaRegisterMap.Capabilities.ResetTimeoutMask == 0x0000003E
            && EnaRegisterMap.Capabilities.DmaAddressWidthMask == 0x0000FF00
            && EnaRegisterMap.Capabilities.AdminCommandTimeoutMask == 0x000F0000
            && EnaRegisterMap.Capabilities.RequiresContiguousQueueMemory(capabilities)
            && EnaRegisterMap.Capabilities.GetResetTimeoutEncoded(capabilities) == 0x0B
            && EnaRegisterMap.Capabilities.GetDmaAddressWidthBits(capabilities) == 48
            && EnaRegisterMap.Capabilities.GetAdminCommandTimeoutEncoded(capabilities) == 0x05
            && EnaRegisterMap.AdminQueueCapabilities.DepthMask == 0x0000FFFF
            && EnaRegisterMap.AdminQueueCapabilities.EntrySizeShift == 16
            && EnaRegisterMap.AdminQueueCapabilities.EntrySizeMask == 0xFFFF0000
            && EnaRegisterMap.AdminQueueCapabilities.GetDepth(adminQueueCaps) == 128
            && EnaRegisterMap.AdminQueueCapabilities.GetEntrySizeBytes(adminQueueCaps) == 64
            && EnaRegisterMap.AdminCompletionQueueCapabilities.GetDepth(adminCompletionQueueCaps) == 256
            && EnaRegisterMap.AdminCompletionQueueCapabilities.GetEntrySizeBytes(adminCompletionQueueCaps) == 16
            && EnaRegisterMap.DeviceControl.DeviceReset == 0x00000001
            && EnaRegisterMap.DeviceControl.AdminQueueRestart == 0x00000002
            && EnaRegisterMap.DeviceControl.Quiescent == 0x00000004
            && EnaRegisterMap.DeviceControl.IoResume == 0x00000008
            && EnaRegisterMap.DeviceControl.ResetReasonShift == 28
            && EnaRegisterMap.DeviceControl.ResetReasonMask == 0xF0000000
            && EnaRegisterMap.DeviceStatus.Ready == 0x00000001
            && EnaRegisterMap.DeviceStatus.AdminQueueRestartInProgress == 0x00000002
            && EnaRegisterMap.DeviceStatus.AdminQueueRestartFinished == 0x00000004
            && EnaRegisterMap.DeviceStatus.ResetInProgress == 0x00000008
            && EnaRegisterMap.DeviceStatus.ResetFinished == 0x00000010
            && EnaRegisterMap.DeviceStatus.FatalError == 0x00000020
            && EnaRegisterMap.DeviceStatus.QuiescentStateInProgress == 0x00000040
            && EnaRegisterMap.DeviceStatus.QuiescentStateAchieved == 0x00000080);
    }

    [HostTest("drivers.ena.registermap validates admin aperture", HostLane.Fast, 2)]
    private static bool TestValidatesAdminAperture()
    {
        EnaRegisterApertureValidationResult adminTooShort =
            EnaRegisterMap.ValidateAdminQueueRegisterAperture(0x0033);
        EnaRegisterApertureValidationResult adminExact =
            EnaRegisterMap.ValidateAdminQueueRegisterAperture(0x0034);
        EnaRegisterApertureValidationResult readinessTooShort =
            EnaRegisterMap.ValidateAdminReadinessRegisterAperture(0x005B);
        EnaRegisterApertureValidationResult readinessExact =
            EnaRegisterMap.ValidateAdminReadinessRegisterAperture(0x005C);

        return HostCheck.Result(EnaRegisterMap.RequiredAdminQueueRegisterApertureLength == 0x0034
            && EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength == 0x005C
            && !adminTooShort.Succeeded
            && adminTooShort.Status == EnaRegisterApertureValidationStatus.AdminQueueRegistersTruncated
            && adminTooShort.ApertureLength == 0x0033
            && adminTooShort.RequiredLength == 0x0034
            && adminExact.Succeeded
            && adminExact.Status == EnaRegisterApertureValidationStatus.Success
            && adminExact.ApertureLength == 0x0034
            && adminExact.RequiredLength == 0x0034
            && !readinessTooShort.Succeeded
            && readinessTooShort.Status == EnaRegisterApertureValidationStatus.ReadinessRegistersTruncated
            && readinessTooShort.RequiredLength == 0x005C
            && readinessExact.Succeeded
            && readinessExact.ApertureLength == 0x005C);
    }

    [HostTest("drivers.ena.registermap readiness accepts ready metadata", HostLane.Fast, 3)]
    private static bool TestReadinessAcceptsReadyMetadata()
    {
        uint capabilities =
            (48u << EnaRegisterMap.Capabilities.DmaAddressWidthShift)
            | (0x02u << EnaRegisterMap.Capabilities.ResetTimeoutShift)
            | (0x06u << EnaRegisterMap.Capabilities.AdminCommandTimeoutShift);
        uint deviceStatus = EnaRegisterMap.DeviceStatus.Ready;

        EnaAdminReadinessResult readiness = EnaAdminReadinessPlanner.Assess(
            EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength,
            capabilities,
            deviceStatus);

        return HostCheck.Result(readiness.Succeeded
            && readiness.Status == EnaAdminReadinessStatus.Success
            && readiness.ApertureValidation.Succeeded
            && readiness.Capabilities == capabilities
            && readiness.DeviceStatus == deviceStatus
            && !readiness.RequiresContiguousQueueMemory
            && readiness.DmaAddressWidthBits == 48
            && readiness.ResetTimeoutEncoded == 0x02
            && readiness.AdminCommandTimeoutEncoded == 0x06
            && readiness.DeviceReady
            && !readiness.DeviceFatalError
            && !readiness.DeviceResetInProgress
            && !readiness.AdminQueueRestartInProgress
            && !readiness.DeviceQuiescentStateInProgress);
    }

    [HostTest("drivers.ena.registermap readiness rejects unsupported reset busy states", HostLane.Fast, 5)]
    private static bool TestReadinessRejectsUnsupportedResetBusyStates()
    {
        uint ready = EnaRegisterMap.DeviceStatus.Ready;
        uint unsupportedCaps = EnaRegisterMap.Capabilities.ContiguousQueueRequired;

        EnaAdminReadinessResult tooSmall = EnaAdminReadinessPlanner.Assess(
            EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength - 1,
            capabilities: 0,
            deviceStatus: ready);
        EnaAdminReadinessResult unsupported = EnaAdminReadinessPlanner.Assess(
            EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength,
            unsupportedCaps,
            ready);
        EnaAdminReadinessResult fatal = EnaAdminReadinessPlanner.Assess(
            EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength,
            capabilities: 0,
            deviceStatus: ready | EnaRegisterMap.DeviceStatus.FatalError);
        EnaAdminReadinessResult reset = EnaAdminReadinessPlanner.Assess(
            EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength,
            capabilities: 0,
            deviceStatus: ready | EnaRegisterMap.DeviceStatus.ResetInProgress);
        EnaAdminReadinessResult adminRestart = EnaAdminReadinessPlanner.Assess(
            EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength,
            capabilities: 0,
            deviceStatus: ready | EnaRegisterMap.DeviceStatus.AdminQueueRestartInProgress);
        EnaAdminReadinessResult quiescent = EnaAdminReadinessPlanner.Assess(
            EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength,
            capabilities: 0,
            deviceStatus: ready | EnaRegisterMap.DeviceStatus.QuiescentStateInProgress);
        EnaAdminReadinessResult notReady = EnaAdminReadinessPlanner.Assess(
            EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength,
            capabilities: 0,
            deviceStatus: 0);

        return HostCheck.Result(!tooSmall.Succeeded
            && tooSmall.Status == EnaAdminReadinessStatus.RegisterApertureTooSmall
            && tooSmall.ApertureValidation.Status
                == EnaRegisterApertureValidationStatus.ReadinessRegistersTruncated
            && !unsupported.Succeeded
            && unsupported.Status == EnaAdminReadinessStatus.UnsupportedContiguousQueueRequirement
            && unsupported.RequiresContiguousQueueMemory
            && !fatal.Succeeded
            && fatal.Status == EnaAdminReadinessStatus.DeviceFatalError
            && fatal.DeviceFatalError
            && !reset.Succeeded
            && reset.Status == EnaAdminReadinessStatus.DeviceResetInProgress
            && reset.DeviceResetInProgress
            && !adminRestart.Succeeded
            && adminRestart.Status == EnaAdminReadinessStatus.AdminQueueRestartInProgress
            && adminRestart.AdminQueueRestartInProgress
            && !quiescent.Succeeded
            && quiescent.Status == EnaAdminReadinessStatus.DeviceQuiescentStateInProgress
            && quiescent.DeviceQuiescentStateInProgress
            && !notReady.Succeeded
            && notReady.Status == EnaAdminReadinessStatus.DeviceNotReady
            && !notReady.DeviceReady);
    }
}
