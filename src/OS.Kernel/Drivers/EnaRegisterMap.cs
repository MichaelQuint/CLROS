namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public const uint RegisterBaseOffset = 0x0000;
    public const uint RegisterWidth = sizeof(uint);
    public const uint RequiredAdminQueueRegisterApertureLength = AdminCompletionQueueTail.Offset + AdminCompletionQueueTail.Width;
    public const uint RequiredAdminReadinessRegisterApertureLength = DeviceStatus.Offset + DeviceStatus.Width;
    public static EnaRegisterApertureValidationResult ValidateAdminQueueRegisterAperture(ulong apertureLength)
    {
        const ulong requiredLength = RequiredAdminQueueRegisterApertureLength;
        if (apertureLength < requiredLength)
        {
            return EnaRegisterApertureValidationResult.Fail(EnaRegisterApertureValidationStatus.AdminQueueRegistersTruncated, apertureLength, requiredLength);
        }

        return EnaRegisterApertureValidationResult.Success(apertureLength, requiredLength);
    }

    public static EnaRegisterApertureValidationResult ValidateAdminReadinessRegisterAperture(ulong apertureLength)
    {
        const ulong requiredLength = RequiredAdminReadinessRegisterApertureLength;
        if (apertureLength < requiredLength)
        {
            return EnaRegisterApertureValidationResult.Fail(EnaRegisterApertureValidationStatus.ReadinessRegistersTruncated, apertureLength, requiredLength);
        }

        return EnaRegisterApertureValidationResult.Success(apertureLength, requiredLength);
    }
}
