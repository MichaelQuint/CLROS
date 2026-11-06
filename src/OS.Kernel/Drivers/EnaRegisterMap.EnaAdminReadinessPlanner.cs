namespace OS.Drivers;

public static class EnaAdminReadinessPlanner
{
    public static EnaAdminReadinessResult Assess(ulong registerBarApertureLength, uint capabilities, uint deviceStatus)
    {
        EnaRegisterApertureValidationResult apertureValidation = EnaRegisterMap.ValidateAdminReadinessRegisterAperture(registerBarApertureLength);
        if (!apertureValidation.Succeeded)
        {
            return EnaAdminReadinessResult.Fail(EnaAdminReadinessStatus.RegisterApertureTooSmall, apertureValidation, capabilities, deviceStatus);
        }

        if (EnaRegisterMap.Capabilities.RequiresContiguousQueueMemory(capabilities))
        {
            return EnaAdminReadinessResult.Fail(EnaAdminReadinessStatus.UnsupportedContiguousQueueRequirement, apertureValidation, capabilities, deviceStatus);
        }

        if (EnaRegisterMap.DeviceStatus.HasFatalError(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(EnaAdminReadinessStatus.DeviceFatalError, apertureValidation, capabilities, deviceStatus);
        }

        if (EnaRegisterMap.DeviceStatus.IsResetInProgress(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(EnaAdminReadinessStatus.DeviceResetInProgress, apertureValidation, capabilities, deviceStatus);
        }

        if (EnaRegisterMap.DeviceStatus.IsAdminQueueRestartInProgress(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(EnaAdminReadinessStatus.AdminQueueRestartInProgress, apertureValidation, capabilities, deviceStatus);
        }

        if (EnaRegisterMap.DeviceStatus.IsQuiescentStateInProgress(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(EnaAdminReadinessStatus.DeviceQuiescentStateInProgress, apertureValidation, capabilities, deviceStatus);
        }

        if (!EnaRegisterMap.DeviceStatus.IsReady(deviceStatus))
        {
            return EnaAdminReadinessResult.Fail(EnaAdminReadinessStatus.DeviceNotReady, apertureValidation, capabilities, deviceStatus);
        }

        return EnaAdminReadinessResult.Success(apertureValidation, capabilities, deviceStatus);
    }
}
